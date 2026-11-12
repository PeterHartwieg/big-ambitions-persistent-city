using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using PersistentCity.Tests.Acceptance;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class OwnershipBoundaryTests
{
    private readonly ITestOutputHelper output;
    public OwnershipBoundaryTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public void CandidateQueriesReturnViewTypes()
    {
        var query = typeof(PresentationGrid).GetMethod(nameof(PresentationGrid.Query))!;
        Assert.Equal(typeof(IReadOnlyList<IPersonView>), query.ReturnType);
        Assert.Equal(typeof(List<IPersonView>), query.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(bool), typeof(ActiveJourneyIndex).GetMethod(nameof(ActiveJourneyIndex.Contains))!.ReturnType);
        Assert.Equal(typeof(IPersonView), Assert.Single(typeof(ActiveJourneyIndex).GetMethod(nameof(ActiveJourneyIndex.Contains))!.GetParameters()).ParameterType);
        static bool Entity(Type t) => typeof(IPersonView).IsAssignableFrom(t) && !t.IsInterface
            || t.IsGenericType && t.GetGenericArguments().Any(Entity) || t.IsArray && Entity(t.GetElementType()!);
        var entityResults = new[] { typeof(ActiveJourneyIndex), typeof(PresentationGrid) }
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => (t, m, type: m is PropertyInfo p ? p.PropertyType : m is MethodInfo mi && !mi.IsSpecialName ? mi.ReturnType : null)))
            .Where(x => x.type != null && Entity(x.type)).Select(x => x.t.Name + "." + x.m.Name).ToArray();
        Assert.Equal(new[] { "ActiveJourneyIndex.BodyCandidates" }, entityResults);
        var people = new List<CitizenState>
        {
            new() { Identity = new CitizenIdentity { Id = "w" }, Phase = CitizenPhase.WaitingForOpening, PlaceId = "shop" },
            new() { Identity = new CitizenIdentity { Id = "pv:1" }, Phase = CitizenPhase.WaitingForOpening, PlaceId = "shop" },
            new() { Identity = new CitizenIdentity { Id = "home" }, Phase = CitizenPhase.AtPlace, PlaceId = "shop" },
        };
        var index = new ActiveJourneyIndex(id => id == "shop" ? new Position(10, 0, 10) : (Position?)null);
        index.Rebuild(people);
        var near = index.Grid.Query(new Position(10, 0, 10), 0, new List<IPersonView>());
        Assert.Equal(new[] { "pv:1", "w" }, near.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(near, p => Assert.Contains(people, c => ReferenceEquals(c, p)));
        Assert.All(near, p => Assert.True(index.Contains(p)));
        Assert.False(index.Contains(people[2]));
    }


    private static readonly Type[] Values = { typeof(string), typeof(bool), typeof(int), typeof(long), typeof(double), typeof(float), typeof(Position), typeof(Money) };

    private static bool Allowed(Type type, Queue<Type> views, out string why)
    {
        why = "";
        if (Nullable.GetUnderlyingType(type) is Type inner) type = inner;
        if (type.IsEnum || Values.Contains(type)) return true;
        if (type.IsInterface && type.Namespace == "PersistentCity.Core" && type.Name.EndsWith("View", StringComparison.Ordinal)) { views.Enqueue(type); return true; }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) return Allowed(type.GetGenericArguments()[0], views, out why);
        why = type.FullName ?? type.Name;
        return false;
    }

    [Fact]
    public void NoTypeReachableFromTheCityViewExposesASetterAnEntityOrAMutableCollection()
    {
        var seen = new HashSet<Type>(); var queue = new Queue<Type>(); queue.Enqueue(typeof(ICityView));
        var problems = new List<string>();
        while (queue.Count > 0)
        {
            var view = queue.Dequeue();
            if (!seen.Add(view)) continue;
            Assert.True(view.IsInterface, view.Name + " is not an interface");
            foreach (var type in view.GetInterfaces().Prepend(view))
            {
                foreach (var p in type.GetProperties())
                {
                    if (p.CanWrite || p.SetMethod != null) problems.Add(type.Name + "." + p.Name + " has a setter");
                    if (!Allowed(p.PropertyType, queue, out var why)) problems.Add(type.Name + "." + p.Name + " returns " + why);
                }
                foreach (var m in type.GetMethods().Where(m => !m.IsSpecialName))
                {
                    if (!Allowed(m.ReturnType, queue, out var why)) problems.Add(type.Name + "." + m.Name + "() returns " + why);
                    foreach (var parameter in m.GetParameters())
                        if (parameter.ParameterType.IsByRef || !Allowed(parameter.ParameterType, new Queue<Type>(), out _))
                            problems.Add(type.Name + "." + m.Name + " takes " + parameter.ParameterType.Name);
                }
                foreach (var e in type.GetEvents()) problems.Add(type.Name + "." + e.Name + " is an event");
            }
        }
        Assert.Empty(problems);
        var expected = new[] { typeof(ICityView), typeof(IPersonView), typeof(IPlaceView), typeof(IFoodView), typeof(IFoodScopeView),
            typeof(IEconomicLedgerView), typeof(IResearchView), typeof(IResearchOrderView), typeof(IVendorRowView) };
        Assert.Equal(expected.OrderBy(t => t.Name), seen.OrderBy(t => t.Name));
        foreach (var (entity, view) in new[] { (typeof(CitizenState), typeof(IPersonView)), (typeof(Place), typeof(IPlaceView)),
            (typeof(FoodScope), typeof(IFoodScopeView)), (typeof(ResearchOrder), typeof(IResearchOrderView)),
            (typeof(VendorRow), typeof(IVendorRowView)), (typeof(EconomicLedger), typeof(IEconomicLedgerView)) })
        {
            Assert.True(view.IsAssignableFrom(entity), entity.Name + " does not implement " + view.Name);
            var map = entity.GetInterfaceMap(view);
            Assert.All(map.TargetMethods, m => Assert.True(m.IsPrivate && m.Name.Contains(view.Name, StringComparison.Ordinal), entity.Name + "." + m.Name));
        }
        var viewCollections = seen.SelectMany(t => t.GetProperties()).Where(p => p.PropertyType != typeof(string)
            && typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("ExtraProductIds", viewCollections);
        foreach (var type in typeof(CityState).Assembly.GetTypes().Concat(typeof(SaveCodec).Assembly.GetTypes()).Where(t => !seen.Any(v => v.IsAssignableFrom(t))))
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.False(viewCollections.Contains(p.Name), type.Name + "." + p.Name + " shares its name with a view collection");
        Assert.False(typeof(CitySimulator).Assembly.GetType("PersistentCity.Core.CityView")!.IsPublic);
    }


    private static string Amount(Money amount) => ((decimal)amount.Units / Money.UnitsPerDollar).ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
    private static string Name(string id) => "<" + id + ">";

    private static void BaseShopText(StringBuilder text, List<FoodShopDay> rows, string placeId, string businessId, int day, int since, int since2)
    {
        text.AppendLine(FoodFeedback.Weeks(rows, placeId, businessId, day, since).Text());
        FoodFeedbackV2.AppendPeriods(text, rows, day, since2); FoodFeedbackV2.AppendJournal(text, null, rows, day, since2, Name);
        FoodShopDay? observed = null;
        foreach (var row in rows) if (observed == null || row.Day > observed.Day) observed = row;
        if (observed == null) text.AppendLine("No evaluated food offers recorded yet.");
        else
        {
            FoodFeedback.Weeks(rows, placeId, businessId, day, since).AppendTo(text);
            text.AppendLine("Latest recorded day " + observed.Day + ": " + observed.Purchases + " purchases · " + observed.ExistingStopPurchases + " during existing visits · " + Amount(observed.Revenue) + " sales");
            text.AppendLine("Meals: breakfast " + observed.PurchaseMeals[1] + " · lunch " + observed.PurchaseMeals[2] + " · dinner " + observed.PurchaseMeals[3]
                + (observed.PurchaseMeals[0] > 0 ? " · earlier single-meal routine " + observed.PurchaseMeals[0] : ""));
            foreach (var product in observed.ProductSales.OrderByDescending(p => p.Purchases))
                text.AppendLine(Name(product.ProductId) + ": " + product.Purchases + " sales · breakfast " + product.PurchaseMeals[1]
                    + " / lunch " + product.PurchaseMeals[2] + " / dinner " + product.PurchaseMeals[3]);
            text.AppendLine("Evaluated offer outcomes that day: " + observed.PriceRejections + " unaffordable, " + observed.AccessRejections + " travel/time, "
                + observed.ClosedRejections + " closed/late, " + observed.StockRejections + " stock unavailable, " + observed.StaffRejections + " no staffed counter, " + observed.ServiceRejections + " service/queue, "
                + observed.PreferredAlternatives + " preferred another option, " + observed.UnknownEvidence + " unknown.");
            text.AppendLine("Purchase hours: " + string.Join(", ", observed.PurchaseHours.Select((n, h) => new { n, h })
                .Where(p => p.n > 0).Select(p => p.h.ToString("00") + ":00 " + p.n)));
        }
        FoodFeedbackV2.AppendPeriods(text, rows, day, since2);
        FoodFeedbackV2.AppendJournal(text, null, rows, day, since2, Name);
    }

    private static string VendorSales(IEnumerable<ResidentMeal> meals, IDictionary<string, string>? receipts = null) => JsonConvert.SerializeObject(meals.Select(m => new { m.IntentId,
        m.ResidentId, m.BusinessId, m.ProductId, cents = m.Price.ToCentsText(), m.Status, receipt = FoodSaleReceipts.ReadStatus(receipts ?? new Dictionary<string, string>(), m).ToString(), wallet = m.Budget }));
    private static string VendorSales(IEnumerable<VendorMealReport> meals, IDictionary<string, string>? receipts = null) => JsonConvert.SerializeObject(meals.Select(v => new { v.Record.IntentId,
        v.Record.ResidentId, v.Record.BusinessId, v.Record.ProductId, cents = v.Record.Price.ToCentsText(), v.Record.Status,
        receipt = FoodSaleReceipts.ReadStatus(receipts ?? new Dictionary<string, string>(), v.Record).ToString(), wallet = v.Wallet }));

    internal static string ThroughState(CitySimulator sim, string payload)
    {
        var city = sim.State; var food = city.Food!; var text = new StringBuilder();
        int day = (int)(city.Minute / 1440);
        text.AppendLine(PopulationWords.Headline(day, city.Citizens.Count, city.Visitors.Count, food.Residents.Count));
        text.AppendLine(food.MealRhythmVersion + "|" + PopulationWords.Lines(city.Visitors.Count, city.Portals.Count) + "|" + food.Shops.Count);
        var history = FoodFeedback.IndexHistory(food.History); int since = FoodFeedback.RecordingSince(food), since2 = FoodFeedbackV2.RecordingSince(food);
        foreach (var scope in food.Shops)
        {
            var place = city.Places.FirstOrDefault(p => p.Id == scope.PlaceId);
            text.AppendLine(scope.PlaceId + "|" + scope.BusinessId + "|" + place?.Name);
            var rows = history.TryGetValue(FoodFeedback.Key(scope.PlaceId, scope.BusinessId), out var r) ? r : new();
            BaseShopText(text, rows, scope.PlaceId, scope.BusinessId, day, since, since2);
        }
        var money = FoodMoneyCounters.Rescan(food);
        text.AppendLine(money.ToString());
        FoodFeedbackV2.AppendCity(text, money, city.Economy, day);
        text.AppendLine(JsonConvert.SerializeObject(FoodEconomyInsight.Capture(city)));
        text.AppendLine("minute " + city.Minute.ToString("R"));
        var ledger = city.Research;
        text.AppendLine(ledger.Open.Count + "|" + ledger.Kept.Count + "|" + ledger.Closed.Count + "|" + ledger.NextSequence);
        foreach (var order in ResearchReportText.Orders(ledger))
            text.AppendLine(ResearchReportText.Line(order, order.BusinessAddress)).AppendLine(ResearchReportText.Report(ledger, order, order.BusinessAddress));
        text.AppendLine(VendorSales(food.Residents.Where(m => VendorNames.IsVendorPlace(m.PlaceId))));
        text.AppendLine(SaveCodec.CountRecords(city, payload).ToString());
        foreach (var r in city.Vendors.Rows) text.AppendLine(r.VehicleId + "|" + r.BusinessId + "|" + r.PlaceId + "|" + r.State + "|" + r.Reason);
        foreach (var c in PopulationCensusInput.CaptureResidents(city.Citizens)) text.Append(c.HomeId).Append(c.DayPlaceId).Append(c.Persona).Append(',');
        text.AppendLine();
        foreach (var id in city.Citizens.Select(c => c.Identity.Id).Concat(city.Visitors.Select(v => v.Id)))
            text.AppendLine(VisitorSchedules.DescribePerson(city, id, sim.FoodAnchorReads));
        foreach (var c in city.Citizens) Inspected(text, sim, c.Identity.Id);
        return text.ToString();
    }

    internal static string ThroughView(CitySimulator sim, string payload)
    {
        var city = sim.View; var food = city.Food!; var text = new StringBuilder();
        int day = (int)(city.Minute / 1440);
        text.AppendLine(PopulationWords.Headline(day, city.ResidentList.Count, city.VisitorCount, food.ConsumerCount));
        text.AppendLine(food.MealRhythmVersion + "|" + PopulationWords.Lines(city.VisitorCount, city.PortalCount) + "|" + food.ScopeList.Count);
        var history = CityViewReports.FoodHistory(city); int since = CityViewReports.FoodRecordingSince(city), since2 = CityViewReports.FoodRecordingSinceV2(city);
        foreach (var scope in food.ScopeList)
        {
            var place = city.PlaceList.FirstOrDefault(p => p.Id == scope.PlaceId);
            text.AppendLine(scope.PlaceId + "|" + scope.BusinessId + "|" + place?.Name);
            text.AppendLine(history.WeeksText(scope.PlaceId, scope.BusinessId, day, since));
            history.AppendPeriods(text, scope.PlaceId, scope.BusinessId, day, since2); history.AppendJournal(text, null, scope.PlaceId, scope.BusinessId, day, since2, Name);
            history.AppendShopDays(text, scope.PlaceId, scope.BusinessId, day, since, Amount, Name);
            history.AppendPeriods(text, scope.PlaceId, scope.BusinessId, day, since2);
            history.AppendJournal(text, null, scope.PlaceId, scope.BusinessId, day, since2, Name);
        }
        var money = CityViewReports.RescanFoodMoney(city);
        text.AppendLine(money.ToString());
        CityViewReports.AppendCityEconomy(text, money, city, day);
        text.AppendLine(JsonConvert.SerializeObject(CityViewReports.EconomyOverview(city)));
        text.AppendLine("minute " + city.Minute.ToString("R"));
        var research = city.Research;
        text.AppendLine(research.OpenOrders.Count + "|" + research.KeptOrders.Count + "|" + research.ClosedOrders.Count + "|" + research.NextSequence);
        var ledger = CityViewReports.ResearchSnapshot(city);
        Assert.Equal(ResearchReportText.Orders(ledger).Select(o => o.Id), research.OpenOrders.Concat(research.KeptOrders).Concat(research.ClosedOrders).Select(o => o.Id));
        foreach (var order in ResearchReportText.Orders(ledger))
            text.AppendLine(ResearchReportText.Line(order, order.BusinessAddress)).AppendLine(ResearchReportText.Report(ledger, order, order.BusinessAddress));
        text.AppendLine(VendorSales(CityViewReports.VendorMeals(city)));
        text.AppendLine(SaveCodec.CountRecords(sim.State, payload).ToString());
        foreach (var r in city.VendorRowList) text.AppendLine(r.VehicleId + "|" + r.BusinessId + "|" + r.PlaceId + "|" + r.State + "|" + r.Reason);
        foreach (var c in CityViewReports.CaptureCensusResidents(city)) text.Append(c.HomeId).Append(c.DayPlaceId).Append(c.Persona).Append(',');
        text.AppendLine();
        foreach (var id in city.ResidentList.Select(c => c.Id).Concat(sim.State.Visitors.Select(v => v.Id)))
            text.AppendLine(CityViewReports.DescribePerson(city, id, sim.FoodAnchorReads));
        foreach (var c in city.ResidentList) Inspected(text, sim, c.Id);
        return text.ToString();
    }

    private static void Inspected(StringBuilder text, CitySimulator sim, string id)
    {
        var insight = sim.Inspect(id)!;
        text.AppendLine(insight.Text()).AppendLine(insight.Home?.Label + "|" + insight.Work?.Label + "|" + insight.WorkRole + "|" + insight.HouseholdSize
            + "|" + string.Join(",", insight.Personality) + "|" + insight.PersonWords + "|" + string.Join(",", insight.MemoryWords));
    }

    internal static CitySimulator F5hCity()
    {
        var shape = F5hIntegratedAcceptanceTests.ReferenceShape();
        var (state, market) = shape.City();
        var city = F5gPopulationFillTests.Start(state, market, shape.Catalogue, new F5gPopulationFillTests.CachedPortalPlanner());
        double target = city.State.Minute + 1440 + 7 * 60;
        while (!city.IsCaughtUpTo(target)) city.AdvanceFrameSlice(target, 4096);
        return city;
    }

    internal static CitySimulator F9cCity()
    {
        var run = CityScenario.Run(CityFixture.Create(CitySize.Small()), CityArm.ReferenceArm);
        return new CitySimulator(SaveCodec.Read(run.Final).State!, new CityFixture.SyncWalk());
    }

    [Fact]
    public void PanelInspectorAndDiagnosticOutputsAreIdenticalThroughTheViewOnTheF5hAndF9cCities()
    {
        foreach (var (name, city) in new[] { ("f5h", F5hCity()), ("f9c", F9cCity()) })
        {
            string payload = SaveCodec.Write(city.State);
            string before = ThroughState(city, payload), after = ThroughView(city, payload);
            output.WriteLine(name + " outputs sha256 " + CityOracles.Sha(after) + ", " + after.Length + " characters");
            Assert.Equal(before, after);
            Assert.True(city.View.ResidentList.Count > 100);
            Assert.NotEmpty(city.View.Food!.ScopeList);
            if (name == "f9c") { Assert.NotEmpty(city.View.Research.KeptOrders); Assert.NotEmpty(city.View.VendorRowList); }
            var s = city.State; var v = city.View;
            Assert.Same(s.Citizens, v.ResidentList); Assert.Same(s.Places, v.PlaceList); Assert.Same(s.Food!.Shops, v.Food!.ScopeList);
            Assert.Same(s.VisitorRoutines, v.VisitorRoutineList); Assert.Same(s.Vendors.Rows, v.VendorRowList); Assert.Same(s.Economy, v.Economy);
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                var c = s.Citizens[i]; IPersonView p = c;
                Assert.Equal((c.Identity.Id, c.Identity.Name, c.Identity.District, c.Identity.Age, c.Identity.Female, c.Identity.Persona, c.HomeId, c.DayPlaceId),
                    (p.Id, p.Name, p.District, p.Age, p.Female, p.Persona, p.HomeId, p.DayPlaceId));
                Assert.Equal((c.ShopId, c.LeisureId, c.Phase, c.Activity, c.PlaceId, c.DestinationId, c.DestinationActivity, c.DepartureMinute),
                    (p.ShopId, p.LeisureId, p.Phase, p.Activity, p.PlaceId, p.DestinationId, p.DestinationActivity, p.DepartureMinute));
                Assert.Equal((c.NextEventMinute, c.ActivityUntilMinute, c.CompletedJourneys, c.MissedActivities, c.JoinedMinute, c.EmploymentStartDay, c.JobChanges, c.PlayerHoldMinutes, c.IsVisitor),
                    (p.NextEventMinute, p.ActivityUntilMinute, p.CompletedJourneys, p.MissedActivities, p.JoinedMinute, p.EmploymentStartDay, p.JobChanges, p.PlayerHoldMinutes, p.IsVisitor));
            }
            foreach (var place in s.Places)
            {
                IPlaceView p = place;
                Assert.Equal((place.Id, place.Name, place.District, place.Kind, place.Position, place.Capacity, place.OpenMinute, place.CloseMinute),
                    (p.Id, p.Name, p.District, p.Kind, p.Position, p.Capacity, p.OpenMinute, p.CloseMinute));
                Assert.Equal((place.AttractionWeight, place.NativeTrafficIndex, place.EvidenceSource, place.ResidentialAreaSquareMetres),
                    (p.AttractionWeight, p.NativeTrafficIndex, p.EvidenceSource, p.ResidentialAreaSquareMetres));
            }
            foreach (var scope in s.Food.Shops)
            {
                IFoodScopeView f = scope;
                Assert.Equal((scope.PlaceId, scope.BusinessId, scope.ProductId, scope.Channel, scope.AlternativeKind, scope.NextServiceAt),
                    (f.PlaceId, f.BusinessId, f.ProductId, f.Channel, f.AlternativeKind, f.NextServiceAt));
                Assert.Equal(scope.AdditionalProductIds, f.ExtraProductIds);
            }
            foreach (var order in s.Research.Open.Concat(s.Research.Kept).Concat(s.Research.Closed))
            {
                IResearchOrderView o = order;
                Assert.Same(o, v.Research.Find(order.Id));
                Assert.Equal((order.Id, order.Sequence, order.ProductCode, order.Product.PriceDollars, order.Price, order.BusinessAddress, order.AgencyAddress, order.TaxDeductible),
                    (o.Id, o.Sequence, o.ProductCode, o.PriceDollars, o.Price, o.BusinessAddress, o.AgencyAddress, o.TaxDeductible));
                Assert.Equal((order.RadiusMetres, order.OrderMinute, order.SurveyAtMinute, order.State, order.DeclineReason, order.ReportId, order.Report != null, order.IsOpen),
                    (o.RadiusMetres, o.OrderMinute, o.SurveyAtMinute, o.State, o.DeclineReason, o.ReportId, o.HasReport, o.IsOpen));
            }
            foreach (var row in s.Vendors.Rows)
            {
                IVendorRowView r = row;
                Assert.Same(r, v.FindVendor(row.VehicleId));
                Assert.Equal((row.VehicleId, row.Kind, row.Generation, row.State, row.PlaceId, row.Signature, row.BusinessId, row.PlacePublished, row.Reason),
                    (r.VehicleId, r.Kind, r.Generation, r.State, r.PlaceId, r.Signature, r.BusinessId, r.PlacePublished, r.Reason));
                Assert.Equal((row.Authority.Desired, row.Authority.Managed, row.Authority.Suspended, row.Authority.HandbackPending, row.Authority.OpeningId, row.Stand != null),
                    (r.AuthorityDesired, r.AuthorityManaged, r.Suspended, r.HandbackPending, r.OpeningId, r.HasStand));
            }
            Assert.Equal((s.Economy.Outside.Count, s.Economy.Shops.Count), (v.Economy.OutsideDays, v.Economy.ObservedShops));
            Assert.Equal((s.Visitors.Count, s.Portals.Count, s.Food.Residents.Count, s.Food.MealRhythmVersion, s.Food.ChoiceStartsDay),
                (v.VisitorCount, v.PortalCount, v.Food.ConsumerCount, v.Food.MealRhythmVersion, v.Food.ChoiceStartsDay));
        }
    }

    private static readonly Dictionary<string, string> Reports = new(StringComparer.Ordinal)
    {
        ["EconomyOverview"] = "values and detached history rows",
        ["RescanFoodMoney"] = "FoodMoneySnapshot: get-only totals",
        ["FoodHistory"] = "FoodHistoryReport: text only",
        ["FoodRecordingSince"] = "int", ["FoodRecordingSinceV2"] = "int",
        ["AppendCityEconomy"] = "text only",
        ["DescribePerson"] = "string",
        ["CaptureCensusResidents"] = "a new array of readonly structs",
        ["ResearchSnapshot"] = "a detached ledger copy",
        ["VendorMeals"] = "VendorMealReport: a new meal record of strings and values and a detached wallet",
    };

    [Fact]
    public void ObserverReportsShareNoSavedObjectWithTheCity()
    {
        var reports = typeof(CityViewReports).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(Reports.Keys.OrderBy(k => k, StringComparer.Ordinal), reports.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(typeof(FoodHistoryReport).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.DeclaringType == typeof(FoodHistoryReport)),
            m => Assert.True(m.ReturnType == typeof(string) || m.ReturnType == typeof(void), m.Name + " returns " + m.ReturnType.Name));
        Assert.Empty(typeof(FoodHistoryReport).GetProperties());
        Assert.All(typeof(FoodMoneySnapshot).GetProperties(), p => Assert.False(p.CanWrite, p.Name));
        Assert.True(typeof(CensusResident).IsValueType && typeof(CensusResident).GetProperties().All(p => !p.CanWrite));

        var city = F9cCity(); var view = city.View; var state = city.State; var food = state.Food!;
        Assert.NotEmpty(food.History);
        var rows = new HashSet<object>(food.History, ReferenceEqualityComparer.Instance);

        string json = JsonConvert.SerializeObject(FoodEconomyInsight.Capture(state));
        var overview = CityViewReports.EconomyOverview(view);
        Assert.Equal(json, JsonConvert.SerializeObject(overview));
        var copies = Assert.IsType<FoodShopDay[]>(overview["history"]);
        Assert.Equal(food.History.Count, copies.Length);
        Assert.DoesNotContain(copies, r => rows.Contains(r) || food.History.Any(h => ReferenceEquals(h.ProductSales, r.ProductSales) || ReferenceEquals(h.PurchaseHours, r.PurchaseHours)));
        foreach (var row in copies) { row.Purchases += 1000; row.PurchaseHours[0] += 7; row.ProductSales.Clear(); row.PeriodCauses.Counts[0] += 3; }
        Assert.Equal(json, JsonConvert.SerializeObject(FoodEconomyInsight.Capture(state)));

        string meals = JsonConvert.SerializeObject(food.Residents.Select(m => new { m.IntentId, m.Status, m.Price, m.Budget }));
        var detached = food.Residents.Select(CityViewReports.Detach).ToList();
        Assert.Equal(meals, JsonConvert.SerializeObject(detached.Select(m => new { m.IntentId, m.Status, m.Price, m.Budget })));
        for (int i = 0; i < detached.Count; i++)
        {
            Assert.NotSame(food.Residents[i], detached[i]);
            if (food.Residents[i].Budget != null) { Assert.NotSame(food.Residents[i].Budget, detached[i].Budget); detached[i].Budget!.Balance = Money.FromCents(1); }
            detached[i].Status = MealStatus.Uncertain;
        }
        Assert.Equal(meals, JsonConvert.SerializeObject(food.Residents.Select(m => new { m.IntentId, m.Status, m.Price, m.Budget })));
        var vendorMeals = CityViewReports.VendorMeals(view);
        Assert.Equal(food.Residents.Count(m => VendorNames.IsVendorPlace(m.PlaceId)), vendorMeals.Count);
        Assert.DoesNotContain(vendorMeals, v => food.Residents.Any(r => ReferenceEquals(r, v.Record) || v.Wallet != null && ReferenceEquals(r.Budget, v.Wallet)));
        output.WriteLine("vendor meals " + vendorMeals.Count + ", history rows " + copies.Length + ", meals " + detached.Count);

        var ledger = CityViewReports.ResearchSnapshot(view);
        Assert.NotSame(state.Research, ledger);
        Assert.Equal(state.Research.Kept.Select(o => o.Id + "=" + o.State), ledger.Kept.Select(o => o.Id + "=" + o.State));
        Assert.DoesNotContain(ledger.Open.Concat(ledger.Kept).Concat(ledger.Closed), o => state.Research.Find(o.Id) == o);
        long next = state.Research.NextSequence; int open = state.Research.Open.Count;
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(ResearchProducts.BasicLocalCode, CityFixture.ResearchBusiness, CityFixture.Agency, false,
            CityFixture.ResearchRadius, state.Minute, out _));
        Assert.Equal((next, open), (state.Research.NextSequence, state.Research.Open.Count));
    }

    [Fact]
    public void VendorSalesListAVendorMealHoldingARetryDecision()
    {
        var city = new CitySimulator(SaveCodec.Read(SaveCodec.Write(F9cCity().State)).State!, new CityFixture.SyncWalk());
        var food = city.State.Food!;
        var meals = food.Residents.Where(m => m.Budget != null).Take(3).ToList();
        Assert.Equal(3, meals.Count);
        string vendorPlace = VendorNames.PlaceId(CityFixture.Vehicle);
        foreach (var meal in meals)
        {
            meal.PlaceId = vendorPlace; meal.BusinessId = VendorNames.Signature(CityFixture.Vehicle) + ":generation:1";
            meal.Status = MealStatus.Declined;
            meal.Decision = new FoodDecisionState { Tuning = FoodDecisionTuning.Capture(new FoodChoiceSettings()) };
        }
        Assert.Throws<NotSupportedException>(() => CityViewReports.Detach(meals[0]));
        var receipts = new Dictionary<string, string>
        {
            [FoodSaleReceipts.Prefix + meals[1].IntentId] = "paid|" + meals[1].Price.ToCentsText() + "|" + Uri.EscapeDataString(meals[1].BusinessId) + "|" + Uri.EscapeDataString(meals[1].ProductId),
            [FoodSaleReceipts.Prefix + meals[2].IntentId] = "paid|x",
        };
        var reports = CityViewReports.VendorMeals(city.View);
        Assert.Equal(meals.Count, reports.Count);
        string live = VendorSales(food.Residents.Where(m => VendorNames.IsVendorPlace(m.PlaceId)), receipts);
        Assert.Equal(live, VendorSales(reports, receipts));
        foreach (var status in new[] { "\"receipt\":\"Paid\"", "\"receipt\":\"Conflict\"", "\"receipt\":\"Missing\"" }) Assert.Contains(status, live);
        output.WriteLine(live.Length + " characters: " + live.Substring(0, Math.Min(240, live.Length)));
        foreach (var report in reports)
        {
            Assert.DoesNotContain(food.Residents, r => ReferenceEquals(r, report.Record) || ReferenceEquals(r.Budget, report.Wallet));
            Assert.Null(report.Record.Decision); Assert.Null(report.Record.Budget);
            report.Record.Status = MealStatus.Paid; report.Record.ResidentId = "edited"; report.Wallet!.Balance = Money.FromCents(1);
        }
        Assert.Equal(live, VendorSales(food.Residents.Where(m => VendorNames.IsVendorPlace(m.PlaceId)), receipts));
    }

    [Fact]
    public void TheViewIsLiveAndAStateWithoutFoodHasNoFoodView()
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "a", Name = "A", Age = 30 } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } }, "f9e", "0", 1, 1440);
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        var view = city.View;
        Assert.Same(view, city.View);
        Assert.Null(view.Food);
        Assert.Equal(1440, view.Minute);
        state.Places = new List<Place> { new() { Id = "other", Name = "Other" } };
        Assert.Equal("other", Assert.Single(view.PlaceList).Id);
        state.Minute = 2000;
        Assert.Equal(2000, view.Minute);
        state.Food = new FoodCommerceState();
        Assert.NotNull(view.Food);
        Assert.Empty(view.Food!.ScopeList);
        Assert.Throws<ArgumentException>(() => CityViewReports.DescribePerson(new ForeignView(), "a", 0));
    }

    private sealed class ForeignView : ICityView
    {
        public double Minute => 0;
        public IReadOnlyList<IPersonView> ResidentList => Array.Empty<IPersonView>();
        public IReadOnlyList<IPersonView> VisitorRoutineList => Array.Empty<IPersonView>();
        public int VisitorCount => 0;
        public int PortalCount => 0;
        public IReadOnlyList<IPlaceView> PlaceList => Array.Empty<IPlaceView>();
        public IFoodView? Food => null;
        public IEconomicLedgerView Economy => new EconomicLedger();
        public IResearchView Research => throw new NotSupportedException();
        public IReadOnlyList<IVendorRowView> VendorRowList => Array.Empty<IVendorRowView>();
        public IVendorRowView? FindVendor(string vehicleId) => null;
    }


    private static long PanelReads(ICityView city)
    {
        long sum = (long)city.Minute / 1440 + city.ResidentList.Count + city.VisitorCount + city.PortalCount;
        var food = city.Food!;
        sum += food.ConsumerCount + food.MealRhythmVersion;
        var scopes = food.ScopeList;
        for (int i = 0; i < scopes.Count; i++) sum += scopes[i].PlaceId.Length + scopes[i].BusinessId.Length;
        var places = city.PlaceList;
        for (int i = 0; i < places.Count; i++) sum += places[i].Id.Length + places[i].Name.Length;
        return sum;
    }

    [Fact]
    public void PanelRefreshReadsThroughTheViewAllocateNothing()
    {
        var city = F5hCity();
        long expected = PanelReads(city.View);
        for (int i = 0; i < 100; i++) PanelReads(city.View);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long total = 0;
        const int refreshes = 1000;
        for (int i = 0; i < refreshes; i++) total += PanelReads(city.View);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine("panel view reads: " + bytes + " bytes over " + refreshes + " refreshes of " + city.View.PlaceList.Count + " places, "
            + city.View.Food!.ScopeList.Count + " shops");
        Assert.Equal(expected * refreshes, total);
        Assert.Equal(0, bytes);
    }
}
