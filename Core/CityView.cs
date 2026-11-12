using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PersistentCity.Core
{
    public interface ICityView
    {
        double Minute { get; }
        IReadOnlyList<IPersonView> ResidentList { get; }
        IReadOnlyList<IPersonView> VisitorRoutineList { get; }
        int VisitorCount { get; }
        int PortalCount { get; }
        IReadOnlyList<IPlaceView> PlaceList { get; }
        IFoodView? Food { get; }
        IEconomicLedgerView Economy { get; }
        IResearchView Research { get; }
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

    public interface IFoodView
    {
        IReadOnlyList<IFoodScopeView> ScopeList { get; }
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
        int OutsideDays { get; }
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
        long OpeningId { get; }
        bool HasStand { get; }
    }

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

    public static class CityViewReports
    {
        private static CityState Of(ICityView view)
            => (view as CityView ?? throw new ArgumentException("Not a simulator's city view.", nameof(view))).State;
        private static FoodCommerceState FoodOf(ICityView view)
            => Of(view).Food ?? throw new InvalidOperationException("The city has no food scope.");

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
        public static FoodMoneySnapshot RescanFoodMoney(ICityView view) => FoodMoneyCounters.Rescan(FoodOf(view));
        public static FoodHistoryReport FoodHistory(ICityView view) => new FoodHistoryReport(FoodOf(view).History);
        public static int FoodRecordingSince(ICityView view) => FoodFeedback.RecordingSince(FoodOf(view));
        public static int FoodRecordingSinceV2(ICityView view) => FoodFeedbackV2.RecordingSince(FoodOf(view));
        public static void AppendCityEconomy(StringBuilder text, FoodMoneySnapshot money, ICityView view, int today)
            => FoodFeedbackV2.AppendCity(text, money, Of(view).Economy, today);
        public static string? DescribePerson(ICityView view, string id, long anchorReads) => VisitorSchedules.DescribePerson(Of(view), id, anchorReads);
        public static CensusResident[] CaptureCensusResidents(ICityView view) => PopulationCensusInput.CaptureResidents(Of(view).Citizens);
        public static ResearchLedger ResearchSnapshot(ICityView view) => Of(view).Research.CopyForSave();
        public static IReadOnlyList<VendorMealReport> VendorMeals(ICityView view)
            => FoodOf(view).Residents.Where(m => VendorNames.IsVendorPlace(m.PlaceId)).Select(m => new VendorMealReport(m)).ToArray();

        internal static ResidentMeal Record(ResidentMeal meal)
        {
            var record = new ResidentMeal();
            foreach (var property in typeof(ResidentMeal).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.GetIndexParameters().Length == 0 && property.SetMethod != null && property.SetMethod.IsPublic
                    && (property.PropertyType == typeof(string) || property.PropertyType.IsValueType))
                    property.SetValue(record, property.GetValue(meal));
            return record;
        }

        internal static T Detach<T>(T value) where T : class => (T)Copy(value)!;

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

    public sealed class VendorMealReport
    {
        internal VendorMealReport(ResidentMeal meal)
        {
            Record = CityViewReports.Record(meal);
            Wallet = meal.Budget == null ? null : CityViewReports.Detach(meal.Budget);
        }
        public ResidentMeal Record { get; }
        public ResidentBudget? Wallet { get; }
    }

    public sealed class FoodHistoryReport
    {
        private static readonly List<FoodShopDay> NoRows = new List<FoodShopDay>();
        private readonly Dictionary<(string, string), List<FoodShopDay>> index;
        internal FoodHistoryReport(IReadOnlyList<FoodShopDay> history) { index = FoodFeedback.IndexHistory(history); }
        private List<FoodShopDay> Rows(string placeId, string businessId)
            => index.TryGetValue(FoodFeedback.Key(placeId, businessId), out var rows) ? rows : NoRows;

        public string WeeksText(string placeId, string businessId, int today, int recordingSince)
            => FoodFeedback.Weeks(Rows(placeId, businessId), placeId, businessId, today, recordingSince).Text();
        public void AppendPeriods(StringBuilder text, string placeId, string businessId, int today, int recordingSince)
            => FoodFeedbackV2.AppendPeriods(text, Rows(placeId, businessId), today, recordingSince);
        public void AppendJournal(StringBuilder text, ShopObservation? journal, string placeId, string businessId, int today, int recordingSince,
            Func<string, string> productName)
            => FoodFeedbackV2.AppendJournal(text, journal, Rows(placeId, businessId), today, recordingSince, productName);

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
