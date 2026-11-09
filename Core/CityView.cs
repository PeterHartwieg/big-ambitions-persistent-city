using System;
using System.Collections.Generic;
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

        /// <summary><see cref="FoodEconomyInsight.Capture"/>: the on-demand economy overview.</summary>
        public static object EconomyOverview(ICityView view) => FoodEconomyInsight.Capture(Of(view));
        /// <summary><see cref="FoodMoneyCounters.Rescan"/>: city money totals from a full scan of the wallets.</summary>
        public static FoodMoneySnapshot RescanFoodMoney(ICityView view) => FoodMoneyCounters.Rescan(FoodOf(view));
        /// <summary><see cref="FoodFeedback.IndexHistory"/> over the kept food history: a new index of the shared day rows.</summary>
        public static Dictionary<(string, string), List<FoodShopDay>> IndexFoodHistory(ICityView view) => FoodFeedback.IndexHistory(FoodOf(view).History);
        public static int FoodRecordingSince(ICityView view) => FoodFeedback.RecordingSince(FoodOf(view));
        public static int FoodRecordingSinceV2(ICityView view) => FoodFeedbackV2.RecordingSince(FoodOf(view));
        /// <summary><see cref="FoodFeedbackV2.AppendCity"/> with the city's economic ledger.</summary>
        public static void AppendCityEconomy(StringBuilder text, FoodMoneySnapshot money, ICityView view, int today)
            => FoodFeedbackV2.AppendCity(text, money, Of(view).Economy, today);
        /// <summary><see cref="VisitorSchedules.DescribePerson"/>: the <c>pc population person</c> line.</summary>
        public static string? DescribePerson(ICityView view, string id, long anchorReads) => VisitorSchedules.DescribePerson(Of(view), id, anchorReads);
        /// <summary><see cref="PopulationCensusInput.CaptureResidents"/>: detached census facts of the residents.</summary>
        public static CensusResident[] CaptureCensusResidents(ICityView view) => PopulationCensusInput.CaptureResidents(Of(view).Citizens);
    }
}
