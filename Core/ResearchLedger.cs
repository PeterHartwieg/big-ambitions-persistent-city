using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>Ordered → Surveyed → Paid → Replaced, or Surveyed → Declined, Cancelled or PaymentUnknown.
    /// Declined, Cancelled, PaymentUnknown and Replaced are terminal. Paid means paid and delivered.</summary>
    public enum ResearchOrderState { Ordered = 0, Surveyed = 1, Paid = 2, Declined = 3, Cancelled = 4, PaymentUnknown = 5, Replaced = 6 }
    /// <summary>Set only on a Declined order. Neither reason touched native money.</summary>
    public enum ResearchDeclineReason { None = 0, InsufficientFunds = 1, BalancePrecision = 2 }
    public enum ResearchOrderRefusal { None, UnknownProduct, InvalidBusiness, InvalidAgency, InvalidRadius, InvalidMinute, BusinessHasOpenOrder, CityOrderLimit, SequenceExhausted }
    public enum ResearchMoveRefusal { None, UnknownOrder, WrongState, InvalidReport, InvalidReason }

    /// <summary>One research order. Only the ledger changes it.</summary>
    public sealed class ResearchOrder : IResearchOrderView
    {
        internal ResearchOrder(long sequence, ResearchProduct product, string business, string agency, bool taxDeductible,
            int radiusMetres, double orderMinute, double surveyAtMinute)
        {
            Sequence = sequence; Id = ResearchLedger.OrderId(sequence); Product = product;
            BusinessAddress = business; AgencyAddress = agency; TaxDeductible = taxDeductible;
            RadiusMetres = radiusMetres; OrderMinute = orderMinute; SurveyAtMinute = surveyAtMinute;
        }
        public long Sequence { get; }
        /// <summary>"pcr-&lt;sequence&gt;". Never reused within a city. Also the entitlement ID.</summary>
        public string Id { get; }
        public ResearchProduct Product { get; }
        public string ProductCode => Product.Code;
        /// <summary>Taken from the product table at order time, exact.</summary>
        public Money Price => Product.Price;
        public string BusinessAddress { get; }
        public string AgencyAddress { get; }
        /// <summary>The agency's hasTaxDeductiblePurchases at order time.</summary>
        public bool TaxDeductible { get; }
        /// <summary>The configured catchment at order time; 0 for city coverage.</summary>
        public int RadiusMetres { get; }
        public double OrderMinute { get; }
        /// <summary>AccountingClock.Target(OrderMinute): the survey describes the city after every event
        /// through this minute and before any later one.</summary>
        public double SurveyAtMinute { get; }
        public ResearchOrderState State { get; internal set; }
        public ResearchDeclineReason DeclineReason { get; internal set; }
        /// <summary>The sealed report's query key; empty until surveyed, kept after the report is released.</summary>
        public string ReportId { get; internal set; } = "";
        /// <summary>Held only while Surveyed or Paid. Numbers still need TryRead with this ledger.</summary>
        public MarketResearchReport? Report { get; internal set; }
        public bool IsOpen => State == ResearchOrderState.Ordered || State == ResearchOrderState.Surveyed;
        // F9e: the read-only view (getters only; no field, wire or save change).
        string IResearchOrderView.Id => Id;
        long IResearchOrderView.Sequence => Sequence;
        string IResearchOrderView.ProductCode => ProductCode;
        int IResearchOrderView.PriceDollars => Product.PriceDollars;
        Money IResearchOrderView.Price => Price;
        string IResearchOrderView.BusinessAddress => BusinessAddress;
        string IResearchOrderView.AgencyAddress => AgencyAddress;
        bool IResearchOrderView.TaxDeductible => TaxDeductible;
        int IResearchOrderView.RadiusMetres => RadiusMetres;
        double IResearchOrderView.OrderMinute => OrderMinute;
        double IResearchOrderView.SurveyAtMinute => SurveyAtMinute;
        ResearchOrderState IResearchOrderView.State => State;
        ResearchDeclineReason IResearchOrderView.DeclineReason => DeclineReason;
        string IResearchOrderView.ReportId => ReportId;
        bool IResearchOrderView.HasReport => Report != null;
        bool IResearchOrderView.IsOpen => IsOpen;
    }

    /// <summary>Per-city research orders and the C5 entitlement. Not attached to CityState yet (C5c).
    /// Bounds: one open order per business, 8 open per city, 16 kept reports (the oldest Paid becomes
    /// Replaced when a 17th is delivered) and 16 closed records (oldest dropped first). Nothing retries,
    /// refunds or resets a terminal order; every order has its own ID, so an unknown payment blocks
    /// only itself. Single-threaded: the dialog, the survey and the daily bill all run on the game thread.</summary>
    public sealed class ResearchLedger : IMarketResearchEntitlement
    {
        public const int MaximumOpenOrders = 8;
        public const int MaximumOpenOrdersPerBusiness = 1;
        public const int MaximumKeptReports = 16;
        public const int MaximumClosedOrders = 16;
        public const int MaximumAddressLength = 256;
        public const string OrderIdPrefix = "pcr-";

        private readonly List<ResearchOrder> open = new List<ResearchOrder>(MaximumOpenOrders);
        private readonly List<ResearchOrder> kept = new List<ResearchOrder>(MaximumKeptReports);
        private readonly List<ResearchOrder> closed = new List<ResearchOrder>(MaximumClosedOrders);

        public ResearchLedger() { }

        /// <summary>The next order's sequence. Starts at 1 and never decreases.</summary>
        public long NextSequence { get; private set; } = 1;

        /// <summary>Save snapshot capture (F7c): a detached copy with the same lists, order and order states. Reports
        /// are immutable and shared.</summary>
        internal ResearchLedger CopyForSave()
        {
            var copy = new ResearchLedger { NextSequence = NextSequence };
            foreach (var (rows, target) in new[] { (open, copy.open), (kept, copy.kept), (closed, copy.closed) })
                foreach (var order in rows)
                    target.Add(new ResearchOrder(order.Sequence, order.Product, order.BusinessAddress, order.AgencyAddress, order.TaxDeductible,
                        order.RadiusMetres, order.OrderMinute, order.SurveyAtMinute)
                    { State = order.State, DeclineReason = order.DeclineReason, ReportId = order.ReportId, Report = order.Report });
            return copy;
        }
        /// <summary>Ordered and Surveyed, in order sequence.</summary>
        public IReadOnlyList<ResearchOrder> Open => open;
        /// <summary>Paid, oldest delivery first.</summary>
        public IReadOnlyList<ResearchOrder> Kept => kept;
        /// <summary>Declined, Cancelled, PaymentUnknown and Replaced, oldest closing first.</summary>
        public IReadOnlyList<ResearchOrder> Closed => closed;

        internal static string OrderId(long sequence) => OrderIdPrefix + sequence.ToString(CultureInfo.InvariantCulture);

        /// <summary>Stores an order at the product's fixed price. The caller has checked that the business is
        /// an eligible Persistent City food business. radiusMetres is the configured catchment for a
        /// catchment product and ignored for city coverage.</summary>
        public ResearchOrderRefusal TryOrder(string? productCode, string? businessAddress, string? agencyAddress,
            bool taxDeductible, int radiusMetres, double orderMinute, out ResearchOrder? order)
        {
            order = null;
            if (!ResearchProducts.TryGet(productCode, out var product) || product == null) return ResearchOrderRefusal.UnknownProduct;
            if (!ValidAddress(businessAddress)) return ResearchOrderRefusal.InvalidBusiness;
            if (!ValidAddress(agencyAddress)) return ResearchOrderRefusal.InvalidAgency;
            if (product.Coverage == ResearchCoverage.City) radiusMetres = 0;
            else if (!ValidRadius(radiusMetres)) return ResearchOrderRefusal.InvalidRadius;
            if (!ValidMinute(orderMinute)) return ResearchOrderRefusal.InvalidMinute;
            foreach (var row in open)
                if (string.Equals(row.BusinessAddress, businessAddress, StringComparison.Ordinal)) return ResearchOrderRefusal.BusinessHasOpenOrder;
            if (open.Count >= MaximumOpenOrders) return ResearchOrderRefusal.CityOrderLimit;
            if (NextSequence == long.MaxValue) return ResearchOrderRefusal.SequenceExhausted;
            order = new ResearchOrder(NextSequence, product, businessAddress!, agencyAddress!, taxDeductible, radiusMetres,
                orderMinute, AccountingClock.Target(orderMinute));
            NextSequence++;
            open.Add(order);
            return ResearchOrderRefusal.None;
        }

        /// <summary>Ordered → Surveyed. The report must match the product's tier and coverage and have an
        /// ID no other order in this ledger uses.</summary>
        public ResearchMoveRefusal TrySeal(string? orderId, MarketResearchReport? report)
        {
            var order = Find(orderId);
            if (order == null) return ResearchMoveRefusal.UnknownOrder;
            if (order.State != ResearchOrderState.Ordered) return ResearchMoveRefusal.WrongState;
            if (!ReportMatches(order, report) || ReportIdInUse(report!.Id)) return ResearchMoveRefusal.InvalidReport;
            order.Report = report; order.ReportId = report.Id; order.State = ResearchOrderState.Surveyed;
            return ResearchMoveRefusal.None;
        }

        /// <summary>Surveyed → Paid, on matching native evidence. Keeps at most 16 reports.</summary>
        public ResearchMoveRefusal TryMarkPaid(string? orderId)
        {
            var order = FindSurveyed(orderId, out var refusal);
            if (order == null) return refusal;
            open.Remove(order);
            order.State = ResearchOrderState.Paid;
            if (kept.Count >= MaximumKeptReports)
            {
                var oldest = kept[0];
                kept.RemoveAt(0);
                oldest.State = ResearchOrderState.Replaced; oldest.Report = null;
                Close(oldest);
            }
            kept.Add(order);
            return ResearchMoveRefusal.None;
        }

        /// <summary>Surveyed → Declined. No money changed: native refused, or the preflight refused first.</summary>
        public ResearchMoveRefusal TryDecline(string? orderId, ResearchDeclineReason reason)
        {
            if (reason != ResearchDeclineReason.InsufficientFunds && reason != ResearchDeclineReason.BalancePrecision)
                return ResearchMoveRefusal.InvalidReason;
            return Finish(orderId, ResearchOrderState.Declined, reason);
        }

        /// <summary>Surveyed → Cancelled: the business is no longer eligible at billing.</summary>
        public ResearchMoveRefusal TryCancel(string? orderId) => Finish(orderId, ResearchOrderState.Cancelled, ResearchDeclineReason.None);

        /// <summary>Surveyed → PaymentUnknown: native code threw after entry, or the evidence did not match.</summary>
        public ResearchMoveRefusal TryMarkPaymentUnknown(string? orderId) => Finish(orderId, ResearchOrderState.PaymentUnknown, ResearchDeclineReason.None);

        /// <summary>True while any order is Ordered or Surveyed.</summary>
        public bool HasOpenOrders => open.Count > 0;

        public ResearchOrder? Find(string? orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return null;
            return Find(open, orderId!) ?? Find(kept, orderId!) ?? Find(closed, orderId!);
        }

        /// <summary>A grant only for a Paid order (Paid = Delivered = true) or a Replaced one (Replaced = true).
        /// The report ID is the C4 query key, so TryRead checks the exact pairing.</summary>
        public bool TryGet(string entitlementId, out MarketResearchGrant? grant)
        {
            grant = null;
            var order = Find(entitlementId);
            if (order == null || (order.State != ResearchOrderState.Paid && order.State != ResearchOrderState.Replaced)) return false;
            grant = new MarketResearchGrant
            {
                EntitlementId = order.Id, ReportId = order.ReportId, Paid = true, Delivered = true,
                Replaced = order.State == ResearchOrderState.Replaced
            };
            return true;
        }

        private ResearchMoveRefusal Finish(string? orderId, ResearchOrderState state, ResearchDeclineReason reason)
        {
            var order = FindSurveyed(orderId, out var refusal);
            if (order == null) return refusal;
            open.Remove(order);
            order.State = state; order.DeclineReason = reason; order.Report = null;
            Close(order);
            return ResearchMoveRefusal.None;
        }

        private void Close(ResearchOrder order)
        {
            if (closed.Count >= MaximumClosedOrders) closed.RemoveAt(0);
            closed.Add(order);
        }

        private ResearchOrder? FindSurveyed(string? orderId, out ResearchMoveRefusal refusal)
        {
            var order = Find(orderId);
            refusal = order == null ? ResearchMoveRefusal.UnknownOrder
                : order.State != ResearchOrderState.Surveyed ? ResearchMoveRefusal.WrongState : ResearchMoveRefusal.None;
            return refusal == ResearchMoveRefusal.None ? order : null;
        }

        private static ResearchOrder? Find(List<ResearchOrder> rows, string orderId)
        {
            foreach (var row in rows) if (string.Equals(row.Id, orderId, StringComparison.Ordinal)) return row;
            return null;
        }

        private bool ReportIdInUse(string reportId)
        {
            return HasReport(open, reportId) || HasReport(kept, reportId) || HasReport(closed, reportId);
        }

        private static bool HasReport(List<ResearchOrder> rows, string reportId)
        {
            foreach (var row in rows) if (string.Equals(row.ReportId, reportId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ReportMatches(ResearchOrder order, MarketResearchReport? report)
        {
            if (report == null || string.IsNullOrEmpty(report.Id) || report.Tier != order.Product.Tier) return false;
            return order.Product.Coverage == ResearchCoverage.City
                ? report.Address == null
                : string.Equals(report.Address, order.BusinessAddress, StringComparison.Ordinal) && report.RadiusMetres == order.RadiusMetres;
        }

        private static bool ValidAddress(string? address)
            => !string.IsNullOrWhiteSpace(address) && address!.Length <= MaximumAddressLength;
        // The C4 catchment bounds (MarketResearchSettings.Clamp and MarketResearchScope).
        private static bool ValidRadius(int radius) => radius >= 50 && radius <= 2000;
        // The AccountingClock domain.
        private static bool ValidMinute(double minute) => Position.Finite(minute) && minute >= 0 && minute <= 1e9;

        /// <summary>Rebuilds a saved ledger for the codec (C5c). Strict: any out-of-bound, inconsistent or
        /// reused value refuses the whole ledger. Internal keeps it off the public surface; the multi-project
        /// build enforces that. The shipped DLL compiles Core and Runtime into one assembly, so there it is a
        /// convention: only the codec calls TryRestore.</summary>
        internal static bool TryRestore(long nextSequence, IReadOnlyList<ResearchOrderImage> openRows,
            IReadOnlyList<ResearchOrderImage> keptRows, IReadOnlyList<ResearchOrderImage> closedRows,
            out ResearchLedger? ledger, out string error)
        {
            ledger = null;
            if (nextSequence < 1) { error = "next sequence"; return false; }
            if (openRows == null || keptRows == null || closedRows == null) { error = "missing list"; return false; }
            if (openRows.Count > MaximumOpenOrders || keptRows.Count > MaximumKeptReports || closedRows.Count > MaximumClosedOrders)
            { error = "too many orders"; return false; }
            var result = new ResearchLedger { NextSequence = nextSequence };
            var sequences = new HashSet<long>();
            var reportIds = new HashSet<string>(StringComparer.Ordinal);
            long previousOpen = 0;
            foreach (var (rows, target) in new[] { (openRows, result.open), (keptRows, result.kept), (closedRows, result.closed) })
                foreach (var image in rows)
                {
                    if (!TryRestoreOrder(image, nextSequence, out var order, out error)) return false;
                    if (!sequences.Add(order!.Sequence)) { error = "reused sequence"; return false; }
                    if (order.ReportId.Length > 0 && !reportIds.Add(order.ReportId)) { error = "reused report"; return false; }
                    bool wanted = target == result.open ? order.IsOpen
                        : target == result.kept ? order.State == ResearchOrderState.Paid
                        : !order.IsOpen && order.State != ResearchOrderState.Paid;
                    if (!wanted) { error = "state in wrong list"; return false; }
                    if (target == result.open)
                    {
                        if (order.Sequence <= previousOpen) { error = "open order sequence"; return false; }
                        previousOpen = order.Sequence;
                        foreach (var row in result.open)
                            if (string.Equals(row.BusinessAddress, order.BusinessAddress, StringComparison.Ordinal))
                            { error = "two open orders for a business"; return false; }
                    }
                    target.Add(order);
                }
            ledger = result; error = "";
            return true;
        }

        private static bool TryRestoreOrder(ResearchOrderImage image, long nextSequence, out ResearchOrder? order, out string error)
        {
            order = null;
            if (image == null) { error = "missing order"; return false; }
            if (image.Sequence < 1 || image.Sequence >= nextSequence) { error = "order sequence"; return false; }
            if (!ResearchProducts.TryGet(image.ProductCode, out var product) || product == null) { error = "unknown product"; return false; }
            if (image.Price != product.Price) { error = "price"; return false; }
            if (!ValidAddress(image.BusinessAddress) || !ValidAddress(image.AgencyAddress)) { error = "address"; return false; }
            if (product.Coverage == ResearchCoverage.City ? image.RadiusMetres != 0 : !ValidRadius(image.RadiusMetres)) { error = "radius"; return false; }
            if (!ValidMinute(image.OrderMinute) || image.SurveyAtMinute != AccountingClock.Target(image.OrderMinute)) { error = "minute"; return false; }
            if (!Enum.IsDefined(typeof(ResearchOrderState), image.State) || !Enum.IsDefined(typeof(ResearchDeclineReason), image.DeclineReason))
            { error = "state"; return false; }
            if ((image.State == ResearchOrderState.Declined) != (image.DeclineReason != ResearchDeclineReason.None)) { error = "decline reason"; return false; }
            bool holdsReport = image.State == ResearchOrderState.Surveyed || image.State == ResearchOrderState.Paid;
            bool surveyed = image.State != ResearchOrderState.Ordered;
            if (holdsReport != (image.Report != null) || surveyed != !string.IsNullOrEmpty(image.ReportId)) { error = "report"; return false; }
            var restored = new ResearchOrder(image.Sequence, product, image.BusinessAddress!, image.AgencyAddress!, image.TaxDeductible,
                image.RadiusMetres, image.OrderMinute, image.SurveyAtMinute)
            { State = image.State, DeclineReason = image.DeclineReason, ReportId = image.ReportId ?? "" };
            if (holdsReport)
            {
                if (!ReportMatches(restored, image.Report) || image.Report!.Id != restored.ReportId) { error = "report"; return false; }
                restored.Report = image.Report;
            }
            order = restored; error = "";
            return true;
        }
    }

    /// <summary>The saved fields of one order, for the codec. Restore validates every field.</summary>
    internal sealed class ResearchOrderImage
    {
        internal long Sequence { get; set; }
        internal string? ProductCode { get; set; }
        internal Money Price { get; set; }
        internal string? BusinessAddress { get; set; }
        internal string? AgencyAddress { get; set; }
        internal bool TaxDeductible { get; set; }
        internal int RadiusMetres { get; set; }
        internal double OrderMinute { get; set; }
        internal double SurveyAtMinute { get; set; }
        internal ResearchOrderState State { get; set; }
        internal ResearchDeclineReason DeclineReason { get; set; }
        internal string? ReportId { get; set; }
        internal MarketResearchReport? Report { get; set; }

        internal static ResearchOrderImage Of(ResearchOrder order) => new ResearchOrderImage
        {
            Sequence = order.Sequence, ProductCode = order.ProductCode, Price = order.Price, BusinessAddress = order.BusinessAddress,
            AgencyAddress = order.AgencyAddress, TaxDeductible = order.TaxDeductible, RadiusMetres = order.RadiusMetres,
            OrderMinute = order.OrderMinute, SurveyAtMinute = order.SurveyAtMinute, State = order.State,
            DeclineReason = order.DeclineReason, ReportId = order.ReportId, Report = order.Report
        };
    }
}
