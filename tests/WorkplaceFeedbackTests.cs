using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class WorkplaceFeedbackTests
{
    private const int Open = 8 * 60, Close = 18 * 60;

    private static void Run(CitySimulator city, double to) { while (!city.AdvanceTo(to, 128).CaughtUp) { } }

    private sealed class TimedWalk : ICommuteTimingPlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
            => new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
                Minutes = 1 + Position.Distance(from.Position, to.Position) / 80 } } };
    }

    private static (CitySimulator City, Place Office, List<CitizenState> Staff) OfficeCity(int seed = 5150, bool timed = false)
    {
        var (state, market) = WorkplaceCapacityTests.Fresh(3, seed);
        var office = state.Places.Where(p => p.Kind == PlaceKind.Work)
            .OrderByDescending(p => state.Citizens.Count(c => !c.IsVisitor && c.DayPlaceId == p.Id)).First();
        office.BusinessType = "ba:businesstype_lawfirm";
        office.WeeklyHours = Enumerable.Range(0, 5).Select(d => new OpeningWindow { Day = d, FromMinute = Open, ToMinute = Close }).ToList();
        var city = new CitySimulator(state, timed ? new TimedWalk() : new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(market);
        var staff = state.Citizens.Where(c => !c.IsVisitor && c.DayPlaceId == office.Id && c.Identity.Persona != Persona.Student).ToList();
        return (city, office, staff);
    }

    private static bool Working(CitizenState c, Place at) => c.Phase == CitizenPhase.AtPlace && c.PlaceId == at.Id && c.Activity == Activity.Work;

    [Fact]
    public void OfficeWorkersArriveBeforeOpeningAndLeaveAfterClosing()
    {
        var (city, office, staff) = OfficeCity(timed: true);
        Assert.True(staff.Count >= 2, "the office has staff");
        Assert.Contains(staff, p => WorkplaceStaffing.Rota(p.Identity.Id) == ShiftType.Early);
        Assert.Contains(staff, p => WorkplaceStaffing.Rota(p.Identity.Id) == ShiftType.Late);
        var index = city.State.Places.ToDictionary(p => p.Id);
        int early = 0, late = 0;
        for (int day = 2; day < 16; day++)
        {
            double midnight = day * 1440.0;
            var shifts = staff.ToDictionary(p => p.Identity.Id, p => DayPlanner.WorkOn(p, day, RoutineKeys.For(p, city.State.Seed).HourShift, index));
            bool open = DayPlanner.Weekday(day) < 5;
            if (!open) Assert.All(shifts.Values, s => Assert.Null(s));
            foreach (var pair in shifts.Where(p => p.Value.HasValue))
            {
                var shift = pair.Value!.Value;
                Assert.Equal(480, shift.EndsAt - shift.StartsAt);
                if (WorkplaceStaffing.Rota(pair.Key) == ShiftType.Early) Assert.Equal(midnight + Open, shift.StartsAt);
                else Assert.Equal(midnight + Close, shift.EndsAt);
            }
            Run(city, midnight + Open);
            foreach (var person in staff.Where(p => shifts[p.Identity.Id].HasValue && WorkplaceStaffing.Rota(p.Identity.Id) == ShiftType.Early))
                if (Working(person, office) || person.Phase == CitizenPhase.WaitingForOpening && person.PlaceId == office.Id) early++;
            Run(city, midnight + Close - 0.5);
            foreach (var person in staff.Where(p => shifts[p.Identity.Id].HasValue && WorkplaceStaffing.Rota(p.Identity.Id) == ShiftType.Late))
                if (Working(person, office)) late++;
            Run(city, midnight + 1440);
            Assert.DoesNotContain(staff, p => Working(p, office) && !DailyRoutine.Open(office, city.State.Minute) && DayPlanner.Weekday(day) >= 5);
        }
        Assert.True(early > 0, "early workers were at the office at opening");
        Assert.True(late > 0, "late workers were at the office at closing");
    }

    [Fact]
    public void NoWorkplaceHoldsMoreThanItsCapacity()
    {
        var (city, office, _) = OfficeCity();
        var factory = city.State.Places.Where(p => p.Kind == PlaceKind.Work && p.Id != office.Id && p.Capacity > 0).First();
        factory.BusinessType = "ba:businesstype_factory";
        factory.WeeklyHours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList();
        var work = city.State.Places.Where(p => p.Kind == PlaceKind.Work).ToList();
        for (double minute = 2 * 1440; minute < 9 * 1440; minute += 30)
        {
            Run(city, minute);
            foreach (var place in work)
            {
                int present = PersonTable.Schedules(city.State).Count(c => c.Phase == CitizenPhase.AtPlace && c.PlaceId == place.Id && c.Activity == Activity.Work);
                int seated = PersonTable.Schedules(city.State).Count(c => c.DayPlaceId == place.Id);
                Assert.True(present <= place.Capacity, place.Id + " holds " + present + " over " + place.Capacity);
                Assert.True(seated <= place.Capacity, place.Id + " seats " + seated + " over " + place.Capacity);
            }
        }
        Assert.True(city.OccupancyMatchesFullCount());
    }

    [Fact]
    public void AClosedVenueStaffsNobody()
    {
        var (city, office, staff) = OfficeCity();
        office.WeeklyHours = new List<OpeningWindow>();
        office.OpenMinute = office.CloseMinute = 0;
        var index = city.State.Places.ToDictionary(p => p.Id);
        for (int day = 2; day < 16; day++)
        {
            Assert.False(WorkplaceStaffing.Staffs(office, day));
            Assert.All(staff, p => Assert.Null(DayPlanner.WorkOn(p, day, RoutineKeys.For(p, city.State.Seed).HourShift, index)));
        }
        var shortDay = new Place { Kind = PlaceKind.Work, BusinessType = "ba:businesstype_warehouse",
            WeeklyHours = new List<OpeningWindow> { new OpeningWindow { Day = 0, FromMinute = 600, ToMinute = 1000 } } };
        Assert.False(WorkplaceStaffing.Staffs(shortDay, 7));
        var income = staff.ToDictionary(p => p.Identity.Id, p => p.Wallet?.Ledger.EmploymentIncome ?? Money.Zero);
        for (double minute = 2 * 1440; minute < 9 * 1440; minute += 60)
        {
            Run(city, minute);
            Assert.DoesNotContain(staff, p => Working(p, office));
        }
        Assert.All(staff, p => Assert.Equal(income[p.Identity.Id], p.Wallet?.Ledger.EmploymentIncome ?? Money.Zero));
    }

    [Fact]
    public void NativeProductionAndWagesAreUnchangedByTheGoldenFixture()
    {
        string Outcome(Action<CityState>? retype)
        {
            var (state, market) = WorkplaceCapacityTests.Fresh(3, 5150);
            retype?.Invoke(state);
            var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
            city.AttachFoodMarket(market);
            Run(city, 1440 * 9);
            foreach (var place in state.Places) place.BusinessType = "";
            return SaveCodec.Write(state);
        }
        string golden = Outcome(null);
        Assert.Equal(golden, Outcome(s => { foreach (var p in s.Places.Where(p => p.Kind == PlaceKind.Work)) p.BusinessType = "ba:businesstype_clothingstore"; }));
        var (city, _, workers) = OfficeCity();
        var before = workers.ToDictionary(p => p.Identity.Id, p => p.Wallet!.Ledger.EmploymentIncome);
        Run(city, 1440 * 16);
        var (reference, referenceMarket) = WorkplaceCapacityTests.Fresh(3, 5150);
        var plain = new CitySimulator(reference, new FreshCityTraceTests.Walk());
        plain.AttachFoodMarket(referenceMarket);
        Run(plain, 1440 * 16);
        foreach (var worker in workers)
        {
            var twin = reference.Citizens.Single(c => c.Identity.Id == worker.Identity.Id);
            var earned = worker.Wallet!.Ledger.EmploymentIncome - before[worker.Identity.Id];
            var twinEarned = twin.Wallet!.Ledger.EmploymentIncome - before[worker.Identity.Id];
            Assert.True(earned.Units > 0, worker.Identity.Id + " earned nothing at the office");
            double ratio = (double)earned.Units / (double)twinEarned.Units;
            Assert.InRange(ratio, 6.0 / 8, 10.0 / 8);
        }
    }

    [Fact]
    public void EveryRefusedVisitAndPurchaseRecordsExactlyOneCauseAndThePanelListsIt()
    {
        var (city, market) = NeedPurchaseTests.Banded();
        var causes = new[] { PurchaseRefusal.VenueNotReady, PurchaseRefusal.VenueFull, PurchaseRefusal.Dropped };
        market.Decide = r =>
        {
            int at = market.Venues.IndexOf(r.VenueId);
            return at < causes.Length ? PurchaseNativeOutcome.Refused(causes[at], "test") : (PurchaseNativeOutcome?)null;
        };
        foreach (var person in city.State.Citizens.Where(c => c.Wallet != null))
        {
            person.Needs = new PersonNeeds();
            person.Needs.Add(new NeedState(NeedPurchaseTestsGift, 0, "", 0));
            person.Needs.Add(new NeedState("ba:businesstype_gym", 0, "", 0));
        }
        var log = city.VenueFeedback;
        int checkedDays = 0;
        for (int day = 2; day < 8; day++)
        {
            Run(city, day * 1440.0 + 1439);
            var rows = VenueFeedbackPanel.Build(log, market.Venues, day);
            var refusedToday = market.Log.Where(l => l.Minute / 1440 == day && l.Refusal != PurchaseRefusal.None).ToList();
            foreach (var row in rows)
            {
                foreach (var cause in causes)
                {
                    int native = refusedToday.Count(l => l.Venue == row.VenueId && l.Refusal == cause);
                    row.Refusals.TryGetValue(VenueWords.RefusalKey(cause), out int listed);
                    Assert.Equal(native, listed);
                }
                int bought = market.Log.Count(l => l.Minute / 1440 == day && l.Venue == row.VenueId && l.Refusal == PurchaseRefusal.None);
                Assert.Equal(bought, row.Bought);
                string text = VenueFeedbackPanel.Text(row, row.VenueId);
                foreach (var pair in row.Refusals) Assert.Contains(VenueWords.English(pair.Key), text);
            }
            if (refusedToday.Count > 0) checkedDays++;
        }
        Assert.True(checkedDays > 0);
        Assert.True(log.Count > 0);
        for (int i = 0; i < log.Count; i++)
        {
            var item = log[i];
            int causesRecorded = (item.Refusal != PurchaseRefusal.None ? 1 : 0) + (item.Miss != PlanMissCause.None ? 1 : 0);
            Assert.Equal(item.Kind == VenueEventKind.Refused || item.Kind == VenueEventKind.Missed ? 1 : 0, causesRecorded);
        }
        Assert.Contains(Enumerable.Range(0, log.Count).Select(i => log[i]), e => e.Kind == VenueEventKind.Visit);
        Assert.Contains(Enumerable.Range(0, log.Count).Select(i => log[i]), e => e.Kind == VenueEventKind.Missed);
        for (int i = 0; i < log.Count; i++)
            if (log[i].Kind == VenueEventKind.Refused)
                Assert.DoesNotContain(Enumerable.Range(0, log.Count).Select(j => log[j]),
                    e => e.Kind == VenueEventKind.Missed && e.ResidentId == log[i].ResidentId && e.VenueId == log[i].VenueId && e.Miss == PlanMissCause.Refused && e.Minute >= log[i].Minute);
        var refused = Enumerable.Range(0, log.Count).Select(i => log[i]).Last(e => e.Kind == VenueEventKind.Refused);
        var lines = VenueFeedbackPanel.ResidentLines(log, refused.ResidentId, refused.Day, id => id);
        Assert.Contains(lines, l => l.Contains(refused.VenueId) && l.Contains(VenueWords.English(refused.CauseKey)));
        Assert.Throws<ArgumentException>(() => new VenueEvent(1, "r", "v", VenueEventKind.Refused, PurchaseRefusal.Ceiling, PlanMissCause.Late, Money.Zero));
        Assert.Throws<ArgumentException>(() => new VenueEvent(1, "r", "v", VenueEventKind.Missed, PurchaseRefusal.None, PlanMissCause.None, Money.Zero));
        Assert.Throws<ArgumentException>(() => new VenueEvent(1, "r", "v", VenueEventKind.Visit, PurchaseRefusal.Ceiling, PlanMissCause.None, Money.Zero));
    }

    private const string NeedPurchaseTestsGift = "ba:businesstype_giftshop";

    [Fact]
    public void TheWordsAreTheEnglishTableShippedForLocalization()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Path.GetDirectoryName(root)!;
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Runtime", "Localization", "en.json")));
        var table = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        foreach (var pair in VenueWords.All) Assert.Equal(pair.Value, table[pair.Key]);
        Assert.Equal(ThroughTraffic.InspectorLine, table[ThroughTraffic.InspectorKey]);
        foreach (PurchaseRefusal refusal in Enum.GetValues(typeof(PurchaseRefusal)))
            if (refusal != PurchaseRefusal.None) Assert.True(VenueWords.All.ContainsKey(VenueWords.RefusalKey(refusal)));
        foreach (PlanMissCause miss in Enum.GetValues(typeof(PlanMissCause)))
            if (miss != PlanMissCause.None) Assert.True(VenueWords.All.ContainsKey(VenueWords.MissKey(miss)));
    }
}
