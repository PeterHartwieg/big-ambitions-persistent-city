using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>F9e (plan f9-city-acceptance.md, Decision 8): the read-only city for observers (diagnostics, the food panel,
    /// the resident inspector and the F9 oracles), returned by <see cref="CitySimulator.View"/>. A view is a contract over
    /// the live state, not a copy: every read sees the simulator's current values, and no member can change them. Lists are
    /// the simulator's own lists seen through interface covariance, so a read allocates nothing. Every member returns a view
    /// type or an immutable value; tests/OwnershipBoundaryTests.cs checks that by reflection. Writes keep going through the
    /// simulator's own methods. Collection members are named apart from every saved field (ResidentList, not Residents):
    /// the compact reader derives each field's bound by property name over all Core types, interfaces included.</summary>
    public interface ICityView
    {
        /// <summary>The logical city minute (drained events), not the native clock.</summary>
        double Minute { get; }
        /// <summary>The residents, in the simulator's order.</summary>
        IReadOnlyList<IPersonView> ResidentList { get; }
        /// <summary>Schema 27 visitor routines, one per visitor in visitor order.</summary>
        IReadOnlyList<IPersonView> VisitorRoutineList { get; }
        /// <summary>The saved visitor persons, including any without a routine yet.</summary>
        int VisitorCount { get; }
        int PortalCount { get; }
        /// <summary>Saved places; vendor places are rebuilt from the vendor rows and not listed here.</summary>
        IReadOnlyList<IPlaceView> PlaceList { get; }
        /// <summary>Null until the city owns a food scope.</summary>
        IFoodView? Food { get; }
        IEconomicLedgerView Economy { get; }
        IResearchView Research { get; }
        /// <summary>The Street Food vendor rows, ordinal by vehicle id.</summary>
        IReadOnlyList<IVendorRowView> VendorRowList { get; }
        IVendorRowView? FindVendor(string vehicleId);
    }

    public interface IPersonView
    {
        string Id { get; }
        string Name { get; }
        string District { get; }
        int Age { get; }
        bool Female { get; }
        Persona Persona { get; }
        string HomeId { get; }
        string DayPlaceId { get; }
        string ShopId { get; }
        string LeisureId { get; }
        CitizenPhase Phase { get; }
        Activity Activity { get; }
        string PlaceId { get; }
        string DestinationId { get; }
        Activity DestinationActivity { get; }
        double DepartureMinute { get; }
        double NextEventMinute { get; }
        double ActivityUntilMinute { get; }
        int CompletedJourneys { get; }
        int MissedActivities { get; }
        double JoinedMinute { get; }
        int EmploymentStartDay { get; }
        int JobChanges { get; }
        double PlayerHoldMinutes { get; }
        bool IsVisitor { get; }
    }

    public interface IPlaceView
    {
        string Id { get; }
        string Name { get; }
        string District { get; }
        PlaceKind Kind { get; }
        Position Position { get; }
        int Capacity { get; }
        int OpenMinute { get; }
        int CloseMinute { get; }
        double AttractionWeight { get; }
        int NativeTrafficIndex { get; }
        string EvidenceSource { get; }
        double ResidentialAreaSquareMetres { get; }
    }

    /// <summary>The city's food scopes and the counts the panel shows.</summary>
    public interface IFoodView
    {
        IReadOnlyList<IFoodScopeView> ScopeList { get; }
        /// <summary>Persons with a meal record (one per consumer).</summary>
        int ConsumerCount { get; }
        int MealRhythmVersion { get; }
        int ChoiceStartsDay { get; }
    }

    public interface IFoodScopeView
    {
        string PlaceId { get; }
        string BusinessId { get; }
        string ProductId { get; }
        IReadOnlyList<string> ExtraProductIds { get; }
        FoodScopeChannel Channel { get; }
        FoodAlternativeKind AlternativeKind { get; }
        double NextServiceAt { get; }
    }

    public interface IEconomicLedgerView
    {
        /// <summary>Recorded outside-spending days (at most <see cref="EconomicLedger.Days"/>).</summary>
        int OutsideDays { get; }
        /// <summary>Shops with an observation journal.</summary>
        int ObservedShops { get; }
    }

    public interface IResearchView
    {
        IReadOnlyList<IResearchOrderView> OpenOrders { get; }
        IReadOnlyList<IResearchOrderView> KeptOrders { get; }
        IReadOnlyList<IResearchOrderView> ClosedOrders { get; }
        long NextSequence { get; }
        IResearchOrderView? Find(string? orderId);
    }

    public interface IResearchOrderView
    {
        string Id { get; }
        long Sequence { get; }
        string ProductCode { get; }
        int PriceDollars { get; }
        Money Price { get; }
        string BusinessAddress { get; }
        string AgencyAddress { get; }
        bool TaxDeductible { get; }
        int RadiusMetres { get; }
        double OrderMinute { get; }
        double SurveyAtMinute { get; }
        ResearchOrderState State { get; }
        ResearchDeclineReason DeclineReason { get; }
        string ReportId { get; }
        bool HasReport { get; }
        bool IsOpen { get; }
    }

    public interface IVendorRowView
    {
        string VehicleId { get; }
        string Kind { get; }
        int Generation { get; }
        VendorRowState State { get; }
        string PlaceId { get; }
        string Signature { get; }
        string BusinessId { get; }
        bool PlacePublished { get; }
        string Reason { get; }
        bool AuthorityDesired { get; }
        bool AuthorityManaged { get; }
        bool Suspended { get; }
        bool HandbackPending { get; }
        /// <summary>The Street Food opening PC serves; -1 when none.</summary>
        long OpeningId { get; }
        bool HasStand { get; }
    }

    /// <summary>The one <see cref="ICityView"/> implementation: a wrapper that reads its state's current lists and values on
    /// every access, so a replaced list is seen at once. <see cref="CitySimulator.View"/> allocates it once.</summary>
    internal sealed class CityView : ICityView
    {
        private readonly FoodView food;
        private readonly ResearchView research;
        internal CityView(CityState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            food = new FoodView(state); research = new ResearchView(state);
        }
        internal CityState State { get; }
        public double Minute => State.Minute;
        public IReadOnlyList<IPersonView> ResidentList => State.Citizens;
        public IReadOnlyList<IPersonView> VisitorRoutineList => State.VisitorRoutines;
        public int VisitorCount => State.Visitors.Count;
        public int PortalCount => State.Portals.Count;
        public IReadOnlyList<IPlaceView> PlaceList => State.Places;
        public IFoodView? Food => State.Food == null ? null : food;
        public IEconomicLedgerView Economy => State.Economy;
        public IResearchView Research => research;
        public IReadOnlyList<IVendorRowView> VendorRowList => State.Vendors.Rows;
        public IVendorRowView? FindVendor(string vehicleId) => State.Vendors.Find(vehicleId);

        private sealed class FoodView : IFoodView
        {
            private readonly CityState state;
            public FoodView(CityState state) { this.state = state; }
            private FoodCommerceState Food => state.Food ?? throw new InvalidOperationException("The city has no food scope.");
            public IReadOnlyList<IFoodScopeView> ScopeList => Food.Shops;
            public int ConsumerCount => Food.Residents.Count;
            public int MealRhythmVersion => Food.MealRhythmVersion;
            public int ChoiceStartsDay => Food.ChoiceStartsDay;
        }

        private sealed class ResearchView : IResearchView
        {
            private readonly CityState state;
            public ResearchView(CityState state) { this.state = state; }
            public IReadOnlyList<IResearchOrderView> OpenOrders => state.Research.Open;
            public IReadOnlyList<IResearchOrderView> KeptOrders => state.Research.Kept;
            public IReadOnlyList<IResearchOrderView> ClosedOrders => state.Research.Closed;
            public long NextSequence => state.Research.NextSequence;
            public IResearchOrderView? Find(string? orderId) => state.Research.Find(orderId);
        }
    }

    /// <summary>Observer reports that run an existing Core helper on the state behind a view and return its result. The
    /// helpers keep their concrete parameters (their files are outside F9e); the state never leaves this class, so the
    /// observer files hold only the view. Each throws for a view this assembly did not create.</summary>
    public static class CityViewReports
    {
        private static CityState Of(ICityView view)
            => (view as CityView ?? throw new ArgumentException("Not a simulator's city view.", nameof(view))).State;
        private static FoodCommerceState FoodOf(ICityView view)
            => Of(view).Food ?? throw new InvalidOperationException("The city has no food scope.");

        /// <summary><see cref="FoodEconomyInsight.Capture"/>: the on-demand economy overview, field for field in the same order,
        /// with its history rows replaced by detached copies, so nothing in the result is part of the city.</summary>
        public static IReadOnlyDictionary<string, object?> EconomyOverview(ICityView view)
        {
            object overview = FoodEconomyInsight.Capture(Of(view));
            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in overview.GetType().GetProperties())
            {
                object? value = property.GetValue(overview);
                fields.Add(property.Name, value is FoodShopDay[] rows ? Detach(rows) : value);
            }
            return fields;
        }
        /// <summary><see cref="FoodMoneyCounters.Rescan"/>: city money totals from a full scan of the wallets.</summary>
        public static FoodMoneySnapshot RescanFoodMoney(ICityView view) => FoodMoneyCounters.Rescan(FoodOf(view));
        /// <summary>The kept food history, indexed once for one panel refresh. The rows stay inside the report.</summary>
        public static FoodHistoryReport FoodHistory(ICityView view) => new FoodHistoryReport(FoodOf(view).History);
        public static int FoodRecordingSince(ICityView view) => FoodFeedback.RecordingSince(FoodOf(view));
        public static int FoodRecordingSinceV2(ICityView view) => FoodFeedbackV2.RecordingSince(FoodOf(view));
        /// <summary><see cref="FoodFeedbackV2.AppendCity"/> with the city's economic ledger.</summary>
        public static void AppendCityEconomy(StringBuilder text, FoodMoneySnapshot money, ICityView view, int today)
            => FoodFeedbackV2.AppendCity(text, money, Of(view).Economy, today);
        /// <summary><see cref="VisitorSchedules.DescribePerson"/>: the <c>pc population person</c> line.</summary>
        public static string? DescribePerson(ICityView view, string id, long anchorReads) => VisitorSchedules.DescribePerson(Of(view), id, anchorReads);
        /// <summary><see cref="PopulationCensusInput.CaptureResidents"/>: detached census facts of the residents.</summary>
        public static CensusResident[] CaptureCensusResidents(ICityView view) => PopulationCensusInput.CaptureResidents(Of(view).Citizens);
        /// <summary>A detached copy of the research ledger (the F7c save copy: same orders, states and order, shared immutable
        /// reports) for the research window and report text, which take a ledger. Changing it changes nothing in the city.</summary>
        public static ResearchLedger ResearchSnapshot(ICityView view) => Of(view).Research.CopyForSave();
        /// <summary>Detached copies of the meal records at Street Food vendor places, wallets included, for <c>pc vendor sales</c>
        /// and its receipt check.</summary>
        public static IReadOnlyList<ResidentMeal> VendorMeals(ICityView view)
            => FoodOf(view).Residents.Where(m => VendorNames.IsVendorPlace(m.PlaceId)).Select(Detach).ToArray();

        internal static T Detach<T>(T value) where T : class => (T)Copy(value)!;

        /// <summary>A deep copy through public members: strings and value types as they are, arrays and lists element by
        /// element, a Core class by its parameterless constructor and its public settable properties (a get-only list is
        /// refilled). Used only for on-demand diagnostics; anything else is refused rather than shared.</summary>
        private static object? Copy(object? value)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (value is string || type.IsValueType) return value;
            if (value is Array array)
            {
                var copy = Array.CreateInstance(type.GetElementType()!, array.Length);
                for (int i = 0; i < array.Length; i++) copy.SetValue(Copy(array.GetValue(i)), i);
                return copy;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var copy = (IList)Activator.CreateInstance(type)!;
                foreach (var item in (IList)value) copy.Add(Copy(item));
                return copy;
            }
            if (type.Namespace != typeof(CityState).Namespace || type.GetConstructor(Type.EmptyTypes) == null)
                throw new NotSupportedException("Cannot detach " + type.FullName + ".");
            object result = Activator.CreateInstance(type)!;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || property.GetMethod == null) continue;
                if (property.SetMethod != null && property.SetMethod.IsPublic) property.SetValue(result, Copy(property.GetValue(value)));
                else if (property.GetValue(result) is IList target && !target.IsReadOnly && property.GetValue(value) is IList source)
                { target.Clear(); foreach (var item in source) target.Add(Copy(item)); }
            }
            return result;
        }
    }

    /// <summary>The kept food history indexed by shop for one panel refresh (<see cref="FoodFeedback.IndexHistory"/>). The
    /// saved day rows never leave it: callers get text only.</summary>
    public sealed class FoodHistoryReport
    {
        private static readonly List<FoodShopDay> NoRows = new List<FoodShopDay>();
        private readonly Dictionary<(string, string), List<FoodShopDay>> index;
        internal FoodHistoryReport(IReadOnlyList<FoodShopDay> history) { index = FoodFeedback.IndexHistory(history); }
        private List<FoodShopDay> Rows(string placeId, string businessId)
            => index.TryGetValue(FoodFeedback.Key(placeId, businessId), out var rows) ? rows : NoRows;

        /// <summary><see cref="FoodFeedback.Weeks"/> text of one shop.</summary>
        public string WeeksText(string placeId, string businessId, int today, int recordingSince)
            => FoodFeedback.Weeks(Rows(placeId, businessId), placeId, businessId, today, recordingSince).Text();
        public void AppendPeriods(StringBuilder text, string placeId, string businessId, int today, int recordingSince)
            => FoodFeedbackV2.AppendPeriods(text, Rows(placeId, businessId), today, recordingSince);
        public void AppendJournal(StringBuilder text, ShopObservation? journal, string placeId, string businessId, int today, int recordingSince,
            Func<string, string> productName)
            => FoodFeedbackV2.AppendJournal(text, journal, Rows(placeId, businessId), today, recordingSince, productName);

        /// <summary>The panel's shop-days block: "No evaluated food offers recorded yet.", or the weeks and the latest
        /// recorded day (purchases, meals, products, offer outcomes and purchase hours).</summary>
        public void AppendShopDays(StringBuilder text, string placeId, string businessId, int today, int recordingSince,
            Func<Money, string> amount, Func<string, string> productName)
        {
            var rows = Rows(placeId, businessId);
            FoodShopDay? observed = null;
            foreach (var row in rows) if (observed == null || row.Day > observed.Day) observed = row;
            if (observed == null) { text.AppendLine("No evaluated food offers recorded yet."); return; }
            FoodFeedback.Weeks(rows, placeId, businessId, today, recordingSince).AppendTo(text);
            text.AppendLine("Latest recorded day " + observed.Day + ": " + observed.Purchases + " purchases · " + observed.ExistingStopPurchases + " during existing visits · " + amount(observed.Revenue) + " sales");
            text.AppendLine("Meals: breakfast " + observed.PurchaseMeals[1] + " · lunch " + observed.PurchaseMeals[2] + " · dinner " + observed.PurchaseMeals[3]
                + (observed.PurchaseMeals[0] > 0 ? " · earlier single-meal routine " + observed.PurchaseMeals[0] : ""));
            foreach (var product in observed.ProductSales.OrderByDescending(p => p.Purchases))
                text.AppendLine(productName(product.ProductId) + ": " + product.Purchases + " sales · breakfast " + product.PurchaseMeals[1]
                    + " / lunch " + product.PurchaseMeals[2] + " / dinner " + product.PurchaseMeals[3]);
            text.AppendLine("Evaluated offer outcomes that day: " + observed.PriceRejections + " unaffordable, " + observed.AccessRejections + " travel/time, "
                + observed.ClosedRejections + " closed/late, " + observed.StockRejections + " stock unavailable, " + observed.StaffRejections + " no staffed counter, " + observed.ServiceRejections + " service/queue, "
                + observed.PreferredAlternatives + " preferred another option, " + observed.UnknownEvidence + " unknown.");
            text.AppendLine("Purchase hours: " + string.Join(", ", observed.PurchaseHours.Select((n, h) => new { n, h })
                .Where(p => p.n > 0).Select(p => p.h.ToString("00") + ":00 " + p.n)));
        }
    }
}
