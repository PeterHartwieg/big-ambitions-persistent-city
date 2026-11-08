using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal enum ResearchNoticeKind { Delivered, Declined }

    /// <summary>One agency text message owed for an order that moved at the native daily bill.</summary>
    internal readonly struct ResearchNotice
    {
        internal ResearchNotice(ResearchNoticeKind kind, string orderId, string businessAddress, string agencyAddress)
        { Kind = kind; OrderId = orderId; BusinessAddress = businessAddress; AgencyAddress = agencyAddress; }
        internal ResearchNoticeKind Kind { get; }
        internal string OrderId { get; }
        internal string BusinessAddress { get; }
        internal string AgencyAddress { get; }
        internal string MessageKey => Kind == ResearchNoticeKind.Delivered ? ResearchNoticeText.Delivered : ResearchNoticeText.Declined;
    }

    /// <summary>Plan C5 section 3.7: a delivery message only for Paid and a decline message only for Declined
    /// (InsufficientFunds or BalancePrecision), once per order. A notice is owed only for an order that was Surveyed
    /// before a billing boundary and moved during it; Surveyed → Paid or Declined happens once per order, so no extra
    /// saved state is needed. A reloaded Paid or Declined order was never Surveyed at a boundary of this session.
    /// Cancelled and PaymentUnknown send nothing. Fixed buffer, no allocation while idle. Single-threaded.</summary>
    internal sealed class ResearchNoticeTracker
    {
        private readonly ResearchOrder?[] due = new ResearchOrder?[ResearchLedger.MaximumOpenOrders];
        private int count;

        /// <summary>Remembers the orders that are Surveyed now, before the boundary bills them.</summary>
        internal void Before(ResearchLedger ledger)
        {
            Array.Clear(due, 0, due.Length); count = 0;
            var open = ledger.Open;
            for (int i = 0; i < open.Count && count < due.Length; i++)
                if (open[i].State == ResearchOrderState.Surveyed) due[count++] = open[i];
        }

        /// <summary>Hands one notice per remembered order that is now Paid or Declined, oldest first, then forgets them all.</summary>
        internal void After(Action<ResearchNotice> send)
        {
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var order = due[i]!;
                    if (order.State == ResearchOrderState.Paid)
                        send(new ResearchNotice(ResearchNoticeKind.Delivered, order.Id, order.BusinessAddress, order.AgencyAddress));
                    else if (order.State == ResearchOrderState.Declined)
                        send(new ResearchNotice(ResearchNoticeKind.Declined, order.Id, order.BusinessAddress, order.AgencyAddress));
                }
            }
            finally { Array.Clear(due, 0, due.Length); count = 0; }
        }

        /// <summary>Forgets the remembered orders without sending, for a boundary that ended with the runtime stopped.</summary>
        internal void Forget() { Array.Clear(due, 0, due.Length); count = 0; }
    }

    /// <summary>The mod's delivery and decline message keys, in Runtime/Localization/en.json. Both take {businessName},
    /// the argument native agency messages use.</summary>
    internal static class ResearchNoticeText
    {
        internal const string Delivered = ResearchText.Prefix + "message_delivered";
        internal const string Declined = ResearchText.Prefix + "message_declined";
        internal const string BusinessNameArgument = "businessName";
        internal static IEnumerable<string> Keys() { yield return Delivered; yield return Declined; }

        /// <summary>Reads back C5e's agency ID "address:&lt;escaped street&gt;:&lt;number&gt;:agency".</summary>
        internal static bool TryParseAgency(string? agency, out string street, out int number)
        {
            street = ""; number = 0;
            const string head = "address:", tail = ":agency";
            if (agency == null || !agency.StartsWith(head, StringComparison.Ordinal) || !agency.EndsWith(tail, StringComparison.Ordinal)) return false;
            string body = agency.Substring(head.Length, agency.Length - head.Length - tail.Length);
            int colon = body.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(body.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out number)) return false;
            try { street = Uri.UnescapeDataString(body.Substring(0, colon)); }
            catch (UriFormatException) { return false; }
            return street.Length > 0;
        }
    }

    /// <summary>English labels made from C4's fixed metric and band codes. No native name is read.</summary>
    internal static class ResearchCellLabels
    {
        internal static string Label(MarketResearchMetric metric, MarketResearchBand band)
        {
            string name = Metric(metric);
            return band == MarketResearchBand.All ? name : name + ": " + Band(metric, band);
        }

        internal static string Metric(MarketResearchMetric metric)
        {
            switch (metric)
            {
                case MarketResearchMetric.Consumers: return "Consumers";
                case MarketResearchMetric.RepresentedConsumers: return "Represented consumers";
                case MarketResearchMetric.BreakfastOpportunities: return "Breakfast opportunities";
                case MarketResearchMetric.LunchOpportunities: return "Lunch opportunities";
                case MarketResearchMetric.DinnerOpportunities: return "Dinner opportunities";
                case MarketResearchMetric.HouseholdFood: return "Household food";
                case MarketResearchMetric.DiscretionaryBudget: return "Prepared-meal budget";
                case MarketResearchMetric.UsualLocationUnknown: return "Usual home or day place unknown";
                case MarketResearchMetric.CroissantTaste: return "Croissant taste";
                case MarketResearchMetric.BurgerTaste: return "Burger taste";
                case MarketResearchMetric.HotDogTaste: return "Hot dog taste";
                case MarketResearchMetric.PriceSensitivity: return "Price sensitivity";
                case MarketResearchMetric.QueuePatience: return "Queue patience";
                case MarketResearchMetric.TravelWillingness: return "Travel willingness";
                case MarketResearchMetric.HealthOrientation: return "Health orientation";
                case MarketResearchMetric.QualityOrientation: return "Quality orientation";
                case MarketResearchMetric.TimePressure: return "Time pressure";
                case MarketResearchMetric.UsualDayGroup: return "Usual day";
                case MarketResearchMetric.TraitsUnknown: return "Taste and lifestyle unknown";
                case MarketResearchMetric.LoyaltyDistribution: return "Loyalty";
                case MarketResearchMetric.RivalMarketShare: return "Rival share";
                default: return "Metric " + ((int)metric).ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static string Band(MarketResearchMetric metric, MarketResearchBand band)
        {
            if (metric == MarketResearchMetric.DiscretionaryBudget)
                switch (band)
                {
                    // C4's budget bands (MarketResearchReport.Evidence).
                    case MarketResearchBand.Low: return "below $10";
                    case MarketResearchBand.Neutral: return "$10 to below $25";
                    case MarketResearchBand.High: return "$25 or more";
                }
            switch (band)
            {
                case MarketResearchBand.All: return "all";
                case MarketResearchBand.Low: return "low";
                case MarketResearchBand.SomewhatLow: return "somewhat low";
                case MarketResearchBand.Neutral: return "neutral";
                case MarketResearchBand.SomewhatHigh: return "somewhat high";
                case MarketResearchBand.High: return "high";
                case MarketResearchBand.Unknown: return "unknown";
                case MarketResearchBand.Funded: return "funded";
                case MarketResearchBand.Unfunded: return "not funded";
                case MarketResearchBand.Work: return "work";
                case MarketResearchBand.Study: return "study";
                case MarketResearchBand.Other: return "other";
                default: return "band " + ((int)band).ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static string Value(int? value) => value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "unknown";
    }

    /// <summary>Text for the report window and the pcresearch console lines. Numbers come only from
    /// MarketResearchReport.TryRead with the saved ledger, so only a Paid order shows them.</summary>
    internal static class ResearchReportText
    {
        internal static string ProductName(string code)
        {
            switch (code)
            {
                case ResearchProducts.BasicLocalCode: return "Neighbourhood snapshot";
                case ResearchProducts.DetailedLocalCode: return "Customer profile";
                case ResearchProducts.DetailedCityCode: return "City profile";
                default: return code;
            }
        }

        internal static string State(ResearchOrder order)
        {
            switch (order.State)
            {
                case ResearchOrderState.Ordered: return "Ordered, survey running";
                case ResearchOrderState.Surveyed: return "Surveyed, billed with tonight's marketing bill";
                case ResearchOrderState.Paid: return "Paid and delivered";
                case ResearchOrderState.Declined:
                    return order.DeclineReason == ResearchDeclineReason.BalancePrecision
                        ? "Declined: the balance could not be charged exactly, nothing was charged"
                        : "Declined: not enough money for the bill, nothing was charged";
                case ResearchOrderState.Cancelled: return "Cancelled: the business could no longer order research";
                case ResearchOrderState.PaymentUnknown: return "Payment unknown: blocked, never retried";
                case ResearchOrderState.Replaced: return "Replaced by newer reports";
                default: return order.State.ToString();
            }
        }

        internal static string Time(double minute)
        {
            if (!(minute >= 0)) return "unknown";
            long whole = (long)Math.Floor(minute);
            return "day " + (whole / 1440).ToString(CultureInfo.InvariantCulture) + " " + (whole % 1440 / 60).ToString("00", CultureInfo.InvariantCulture)
                + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>One order without numbers.</summary>
        internal static string Line(ResearchOrder order, string businessName)
            => order.Id + " · " + ProductName(order.ProductCode) + " · " + businessName + " · " + ResearchText.Price(order.Product)
                + " · ordered " + Time(order.OrderMinute) + " · " + State(order);

        /// <summary>Every order the ledger holds: open, then delivered, then closed, each oldest first.</summary>
        internal static IEnumerable<ResearchOrder> Orders(ResearchLedger ledger)
        {
            foreach (var order in ledger.Open) yield return order;
            foreach (var order in ledger.Kept) yield return order;
            foreach (var order in ledger.Closed) yield return order;
        }

        /// <summary>The report of one order. Cells only when TryRead grants them, which needs a Paid order.</summary>
        internal static string Report(ResearchLedger ledger, ResearchOrder order, string businessName)
        {
            var text = new StringBuilder(4096);
            text.AppendLine(Line(order, businessName));
            var report = order.Report;
            if (order.State != ResearchOrderState.Paid || report == null || !report.TryRead(ledger, order.Id, out var cells) || cells == null)
            {
                text.Append("No report numbers: ").Append(order.State == ResearchOrderState.Paid ? "the report cannot be read." : "only a paid order has a report.");
                return text.ToString();
            }
            text.Append("Coverage: ").AppendLine(report.Address == null ? "the whole city"
                : report.RadiusMetres.ToString(CultureInfo.InvariantCulture) + " m around " + businessName);
            text.Append("Surveyed as of ").Append(Time(order.SurveyAtMinute)).Append(" (logical day ")
                .Append(report.LogicalDay.ToString(CultureInfo.InvariantCulture)).AppendLine(")");
            text.AppendLine();
            foreach (var cell in cells)
                text.Append(ResearchCellLabels.Label(cell.Metric, cell.Band)).Append(": ").AppendLine(ResearchCellLabels.Value(cell.Value));
            text.AppendLine();
            text.Append(report.Evidence);
            return text.ToString();
        }
    }

    /// <summary>What the open report window draws: the order rows and the selected order's text.</summary>
    internal sealed class ResearchWindowView
    {
        internal ResearchWindowView(string[] ids, string[] rows, string detail) { Ids = ids; Rows = rows; Detail = detail; }
        internal IReadOnlyList<string> Ids { get; }
        internal IReadOnlyList<string> Rows { get; }
        internal string Detail { get; }
    }

    /// <summary>The report window's state. Closed, it reads nothing and builds nothing; open, it rebuilds its view at
    /// most once a second, on opening and on a new selection. Draw code only shows the cached view.</summary>
    internal sealed class ResearchWindowModel
    {
        internal const double RefreshSeconds = 1;
        private double next = double.NegativeInfinity;
        internal bool Open { get; private set; }
        internal string Selected { get; private set; } = "";
        internal ResearchWindowView? View { get; private set; }
        internal long Builds { get; private set; }

        internal void Show() { Open = true; next = double.NegativeInfinity; }
        internal void Hide() { Open = false; View = null; }
        internal void Toggle() { if (Open) Hide(); else Show(); }
        internal void Select(string id) { Selected = id ?? ""; next = double.NegativeInfinity; }

        /// <summary>Null while closed. businessName resolves a business address to its name.</summary>
        internal ResearchWindowView? Update(double now, ResearchLedger? ledger, Func<string, string> businessName)
        {
            if (!Open) return null;
            if (View != null && now < next) return View;
            View = Build(ledger, businessName); Builds++; next = now + RefreshSeconds;
            return View;
        }

        private ResearchWindowView Build(ResearchLedger? ledger, Func<string, string> businessName)
        {
            if (ledger == null) return new ResearchWindowView(Array.Empty<string>(), Array.Empty<string>(), "Market research is not attached to this city.");
            var ids = new List<string>(ResearchLedger.MaximumOpenOrders + ResearchLedger.MaximumKeptReports + ResearchLedger.MaximumClosedOrders);
            var rows = new List<string>(ids.Capacity);
            ResearchOrder? selected = null, newestPaid = null;
            foreach (var order in ResearchReportText.Orders(ledger))
            {
                ids.Add(order.Id);
                rows.Add(ResearchReportText.Line(order, businessName(order.BusinessAddress)));
                if (string.Equals(order.Id, Selected, StringComparison.Ordinal)) selected = order;
                if (order.State == ResearchOrderState.Paid) newestPaid = order;
            }
            selected ??= newestPaid;
            string detail = selected != null ? ResearchReportText.Report(ledger, selected, businessName(selected.BusinessAddress))
                : ids.Count == 0 ? "No market research orders yet. Call a marketing agency and choose Market research on its campaign screen."
                : "No delivered report yet.";
            return new ResearchWindowView(ids.ToArray(), rows.ToArray(), detail);
        }
    }
}
