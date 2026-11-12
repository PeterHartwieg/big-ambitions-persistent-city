using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class AttendanceWageTests
{
    private readonly ITestOutputHelper output;
    public AttendanceWageTests(ITestOutputHelper output) { this.output = output; }
    internal sealed class Routes : ICitizenRoutePlanner, ICommuteTimingPlanner
    {
        internal double Minutes = 1;
        internal bool Blocked;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
            => Blocked && to.Kind == PlaceKind.Work ? null : new Route { Legs = { new RouteLeg {
                From = from.Position, To = to.Position, Minutes = Minutes, Mode = TravelMode.Walk } } };
        public Route? Plan(Place from, Place to, double minute, CitizenState p) => Plan(from, to, minute, p.Preferences);
    }
    internal sealed class Market : IFoodMarket
    {
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal meal, double at) => throw new InvalidOperationException("No native payroll or sales in wage fixture.");
    }
    internal static CitySimulator Fresh(int seed = 42, Routes? routes = null, int count = 1, double minute = 1440)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity { Id = "c" + i,
            Name = "Worker " + i, Age = 30, Persona = Persona.Commuter }).ToArray(), new[] {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(10, 0, 0) } },
            "attendance-wages", "0.41.1", seed, minute);
        state.Food = new FoodCommerceState { RepresentationFactor = 1, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
            MealRhythmVersion = MealRhythm.PolicyVersion };
        var city = new CitySimulator(state, routes ?? new Routes()); city.AttachFoodMarket(new Market()); return city;
    }
    internal static ResidentBudget Budget(CitySimulator city, int index = 0) => city.State.Food!.Residents[index].Budget!;
    internal static void Advance(CitySimulator city, double at, int budget = 64)
    { while (!city.AdvanceTo(at, budget).CaughtUp) { } }
    private static CitySimulator Reload(CitySimulator city, Routes routes)
    {
        string payload = SaveCodec.Write(city.State);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        var loaded = new CitySimulator(read.State!, routes); loaded.AttachFoodMarket(new Market()); return loaded;
    }

    internal const string Pin42 = "63fb9079a50ef7f7b26a70543e1d2a0e730771236f12269f9de3dfa5e88966ba";

    [Theory]
    [InlineData(42, Pin42)]
    [InlineData(9217, "be6b1a14a59045d51b9385c9e6dd849205cd259bb1b41bbb60457034405d7b46")]
    public void FreshWriterGoldenAndSnapshotArePinned(int seed, string pin)
    {
        var city = Fresh(seed); Advance(city, 1440 + 1100);
        string payload = SaveCodec.Write(city.State);
        Assert.Equal(payload, SaveCodec.Encode(SaveCodec.Capture(city.State)));
        output.WriteLine($"golden {seed}: {EconomicLedgerTests.Sha(payload)} {payload.Length}");
        Assert.True(pin == EconomicLedgerTests.Sha(payload), "golden " + seed + ": " + EconomicLedgerTests.Sha(payload));
        Reload(city, new Routes());
    }

    [Fact]
    public void FullAttendanceWeekEarnsSevenBaseDailyIncomesWithinOneExactUnit()
    {
        var city = Fresh(); var p = city.State.Citizens[0]; var b = Budget(city);
        long expected = 7 * Money.FromCents(b.BaseIncomeCents).Units;
        for (int day = 1; day <= 7; day++)
        {
            Advance(city, (day + 1) * 1440 - .001);
            Assert.Equal(Money.Zero, b.IncomeReceived);
            Assert.True(HouseholdFinance.CashConserved(b));
            output.WriteLine($"day {day}: shift income={b.Ledger.EmploymentIncome.Units}; attended={p.Accrual.AttendedMinutes}");
        }
        Assert.InRange(Math.Abs(expected - b.Ledger.EmploymentIncome.Units), 0, 1);
        Assert.Equal(0, p.Accrual.MissMask);
    }

    [Fact]
    public void AFreshCityCreatedMidShiftPaysOnlyItsFirstDaysAttendedMinutes()
    {
        var city = Fresh(minute: 1440 + 700); var p = city.State.Citizens[0];
        var w = DailyRoutine.WorkOnDay(p, city.State.Seed, city.State.Minute)!.Value;
        Advance(city, w.EndsAt + 1);
        Assert.True(Budget(city).Ledger.EmploymentIncome > Money.Zero);
        Assert.InRange(p.Accrual.AttendedMinutes, 1, (int)(w.EndsAt - p.JoinedMinute));
        Assert.Equal(Money.Zero, Budget(city).IncomeReceived);
        Assert.True(HouseholdFinance.CashConserved(Budget(city)));
    }

    [Fact]
    public void EveryShiftStageAndEveryPostingUnitReloadExactlyOnce()
    {
        var routes = new Routes(); var baseline = Fresh(routes: routes, count: 35);
        var restored = Fresh(routes: routes, count: 35);
        double end = 3 * 1440;
        int reloads = 0;
        while (!restored.IsCaughtUpTo(end))
        {
            string before = SaveCodec.Write(restored.State);
            restored.AdvanceTo(end, 1);
            // Shared transient awareness preparation deliberately restarts on load. Let those units
            // finish; interrupt every published shift/citizen/posting transition instead.
            if (before != SaveCodec.Write(restored.State)) { restored = Reload(restored, routes); reloads++; }
        }
        Advance(baseline, end);
        Assert.True(reloads > 100);
        Assert.Equal(SaveCodec.Write(baseline.State), SaveCodec.Write(restored.State));
        Assert.All(restored.State.Food!.Residents, m => Assert.True(HouseholdFinance.CashConserved(m.Budget!)));
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void FrameCadenceDoesNotChangeWagesOrEmployment(int fps)
    {
        var reference = Fresh(count: 4); Advance(reference, 5 * 1440);
        var actual = Fresh(count: 4);
        double start = actual.State.Minute;
        for (int frame = 1; frame <= fps * 60; frame++)
        {
            double at = Math.Min(5 * 1440, start + 4 * 1440.0 * frame / (fps * 60));
            while (!actual.AdvanceFrameSlice(at, fps == 30 ? 1 : fps == 60 ? 16 : 128).CaughtUp) { }
        }
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(actual.State));
    }

    [Fact]
    public void BlockedShiftEarnsNoWageOrSubstituteSupportButAClosureIsNotAPersonalMiss()
    {
        var blocked = Fresh(routes: new Routes { Blocked = true });
        Advance(blocked, 2 * 1440 - 1);
        Assert.Equal(Money.Zero, Budget(blocked).Ledger.EmploymentIncome);
        Assert.Equal(Money.Zero, Budget(blocked).IncomeReceived);
        Assert.Equal(1, blocked.State.Citizens[0].Accrual.MissMask);
        Assert.Equal(1, blocked.State.Citizens[0].MissedActivities);
        var closed = Fresh(); Assert.True(closed.SetLogicalWorkplaceClosure("work", true));
        Advance(closed, 2 * 1440 - 1);
        Assert.Equal(Money.Zero, Budget(closed).Ledger.EmploymentIncome);
        Assert.Equal(EmploymentWages.ClosureDay, closed.State.Citizens[0].Accrual.Shift);
        Assert.Equal(0, closed.State.Citizens[0].Accrual.MissMask);
        Assert.Equal(0, closed.State.Citizens[0].MissedActivities);
        Assert.True(closed.SetLogicalWorkplaceClosure("work", false));
        Assert.True(HouseholdFinance.CashConserved(Budget(closed)));
    }

    [Fact]
    public void AClosedVenueFoundByAnEarlyCommuteNeverCountsAsAPersonalMiss()
    {
        var city = Fresh(); var place = city.State.Places.Single(p => p.Id == "work");
        place.OpenMinute = place.CloseMinute = 0;
        Advance(city, 2 * 1440 - 1);
        Assert.Equal(EmploymentWages.ClosureDay, city.State.Citizens[0].Accrual.Shift);
        Assert.Equal(0, city.State.Citizens[0].MissedActivities);
        Assert.Equal(0, city.State.Citizens[0].Accrual.MissMask);
        Assert.Equal(Money.Zero, Budget(city).Ledger.EmploymentIncome);
    }

    [Theory]
    [InlineData(Activity.Shop, false)] [InlineData(Activity.Shop, true)]
    [InlineData(Activity.Leisure, false)] [InlineData(Activity.Leisure, true)]
    [InlineData(Activity.Study, false)] [InlineData(Activity.Study, true)]
    public void AClosedWorkShiftDoesNotBlockOtherArrivalsOrTheirOpeningWaits(Activity activity, bool opensLater)
    {
        var routes = new Routes(); var city = Fresh(routes: routes);
        var p = city.State.Citizens[0];
        var w = DailyRoutine.WorkOnDay(p, city.State.Seed, city.State.Minute)!.Value;
        city.SetLogicalWorkplaceClosure("work", true); Advance(city, w.StartsAt + 5);
        Assert.Equal(EmploymentWages.Closed, p.Accrual.Shift);
        double now = city.State.Minute, arrival = now + 1, opening = opensLater ? now + 10 : arrival;
        var venue = new Place { Id = "other", Name = "Other venue", Capacity = 1, Position = new Position(20, 0, 0),
            Kind = activity == Activity.Shop ? PlaceKind.Shop : activity == Activity.Study ? PlaceKind.Study : PlaceKind.Leisure,
            OpenMinute = (int)(opening % 1440), CloseMinute = 1440 };
        city.State.Places.Add(venue);
        p.Phase = CitizenPhase.Travelling; p.PlaceId = p.HomeId; p.Activity = Activity.Home;
        p.DestinationId = venue.Id; p.DestinationActivity = activity;
        p.DepartureMinute = now; p.NextEventMinute = arrival; p.ActivityUntilMinute = now + 45;
        p.Journey = routes.Plan(city.State.Places.Single(v => v.Id == p.HomeId), venue, now, p.Preferences);
        city = new CitySimulator(city.State, routes); city.AttachFoodMarket(new Market());
        Advance(city, arrival);
        if (opensLater)
        {
            Assert.Equal(CitizenPhase.WaitingForOpening, p.Phase);
            Assert.Equal(opening, p.NextEventMinute);
            Advance(city, opening);
        }
        Assert.Equal(CitizenPhase.AtPlace, p.Phase);
        Assert.Equal((venue.Id, activity), (p.PlaceId, p.Activity));
        Assert.Contains(p.RecentVisits, v => v.PlaceId == venue.Id && v.Activity == activity && v.AtMinute == opening);
        Assert.Equal(EmploymentWages.Closed, p.Accrual.Shift);
        Assert.Equal(0, p.MissedActivities);
    }

    private static (CitizenState Person, ResidentBudget Budget, WorkWindow Window) Shift(double start = 1500, double end = 1980)
    {
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "c0", Age = 30, Persona = Persona.Commuter },
            DayPlaceId = "work", PlaceId = "work", Phase = CitizenPhase.AtPlace, Activity = Activity.Work,
            ActivityUntilMinute = end, RecentVisits = new List<VisitRecord> { new VisitRecord { PlaceId = "work", Activity = Activity.Work, AtMinute = start } } };
        return (person, new ResidentBudget { BaseIncomeCents = 101 }, new WorkWindow("work", Activity.Work, start, end));
    }

    [Fact]
    public void LateArrivalAndEarlyDeparturePayOnlyTheAttendedMinutesAndCarryRounding()
    {
        var (p, b, w) = Shift(); EmploymentWages.Begin(p, w, true);
        p.RecentVisits[0].AtMinute += 90;
        var leaving = CitySimulator.CloneForEvent(p); leaving.Activity = Activity.Home;
        EmploymentWages.Leave(p, leaving, w, w.EndsAt - 30);
        Assert.Equal(360, leaving.Accrual.AttendedMinutes);
        EmploymentWages.Settle(leaving, b, w, w.EndsAt);
        Assert.Equal(Money.FromCents(101).Units * 7 * 360 / 1920, b.Ledger.EmploymentIncome.Units);
        Assert.True(HouseholdFinance.CashConserved(b));
        string once = JsonConvert.SerializeObject(new { leaving.Accrual, b });
        EmploymentWages.Settle(leaving, b, w, w.EndsAt);
        Assert.Equal(once, JsonConvert.SerializeObject(new { leaving.Accrual, b }));
    }

    [Fact]
    public void FractionalLateArrivalDoesNotRoundAnUnattendedMinuteIntoPay()
    {
        var (p, b, w) = Shift(); p.RecentVisits[0].AtMinute += .75;
        EmploymentWages.Begin(p, w, true); EmploymentWages.Settle(p, b, w, w.EndsAt);
        Assert.Equal(479, p.Accrual.AttendedMinutes);
        Assert.Equal((Money.FromCents(101).Units * 7 * 479 + 960) / 1920, b.Ledger.EmploymentIncome.Units);
        Assert.True(HouseholdFinance.CashConserved(b));
    }

    [Fact]
    public void FractionalExactUnitsCarryAcrossShiftsAndReloadWithoutRoundingDrift()
    {
        var (p, b, _) = Shift();
        System.Numerics.BigInteger numerator = 0;
        for (int day = 1; day <= 28; day++)
        {
            int minutes = 1 + day * 17 % 479;
            var w = new WorkWindow("work", Activity.Work, day * 1440 + 500, day * 1440 + 980);
            p.ActivityUntilMinute = w.EndsAt;
            p.RecentVisits[0].AtMinute = w.EndsAt - minutes;
            EmploymentWages.Begin(p, w, true); EmploymentWages.Settle(p, b, w, w.EndsAt);
            numerator += (System.Numerics.BigInteger)Money.FromCents(b.BaseIncomeCents).Units * 7 * minutes;
            Assert.Equal((long)((numerator + 960) / 1920), b.Ledger.EmploymentIncome.Units);
            Assert.InRange(p.Accrual.UnpostedWage.Units, 0, 1919);
            Assert.True(HouseholdFinance.CashConserved(b));
            p = JsonConvert.DeserializeObject<CitizenState>(JsonConvert.SerializeObject(p))!;
            b = JsonConvert.DeserializeObject<ResidentBudget>(JsonConvert.SerializeObject(b))!;
        }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void MidnightPortionAndShiftEndSurviveReloadAndCannotBeReposted(bool open)
    {
        var (p, b, w) = Shift(2800, 3280); EmploymentWages.Begin(p, w, open);
        EmploymentWages.Settle(p, b, w, 2880);
        var savedP = JsonConvert.DeserializeObject<CitizenState>(JsonConvert.SerializeObject(p))!;
        var savedB = JsonConvert.DeserializeObject<ResidentBudget>(JsonConvert.SerializeObject(b))!;
        string midpoint = JsonConvert.SerializeObject(savedB);
        EmploymentWages.Settle(savedP, savedB, w, 2880);
        Assert.Equal(midpoint, JsonConvert.SerializeObject(savedB));
        EmploymentWages.Settle(p, b, w, 3280); EmploymentWages.Settle(savedP, savedB, w, 3280);
        Assert.Equal(JsonConvert.SerializeObject(b), JsonConvert.SerializeObject(savedB));
        Assert.Equal(open ? Money.FromCents(101).Units * 7 / 4 : 0, b.Ledger.EmploymentIncome.Units);
        Assert.Equal(0, p.Accrual.MissMask);
        Assert.Equal(open ? EmploymentWages.Settled : EmploymentWages.ClosureDay, p.Accrual.Shift);
    }

    [Fact]
    public void RetainedOvernightWindowUsesTheBoundedPostingHeapAcrossStrictSaveReload()
    {
        var routes = new Routes(); var baseline = Overnight(routes); var restored = Overnight(routes);
        var food = (FoodCommerce)typeof(CitySimulator).GetField("food", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)!.GetValue(restored)!;
        int chunks = 0;
        while (!restored.IsCaughtUpTo(2880))
        {
            string before = SaveCodec.Write(restored.State);
            restored.AdvanceTo(2880, 1);
            Assert.InRange(food.LastBudgetPostings, 0, 32);
            if (before != SaveCodec.Write(restored.State)) restored = Reload(restored, routes);
            food = (FoodCommerce)typeof(CitySimulator).GetField("food", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!.GetValue(restored)!;
            chunks++;
        }
        Assert.True(chunks >= 3);
        Advance(restored, 3280); Advance(baseline, 3280);
        Assert.Equal(SaveCodec.Write(baseline.State), SaveCodec.Write(restored.State));
        foreach (var meal in restored.State.Food!.Residents)
        {
            Assert.Equal(Money.FromCents(meal.Budget!.BaseIncomeCents).Units * 7 / 4, meal.Budget.Ledger.EmploymentIncome.Units);
            Assert.True(HouseholdFinance.CashConserved(meal.Budget));
        }
        static CitySimulator Overnight(Routes routes)
        {
            var city = Fresh(routes: routes, count: 35, minute: 2800);
            Advance(city, 2800);
            foreach (var p in city.State.Citizens)
            {
                p.PlaceId = "work"; p.Phase = CitizenPhase.AtPlace; p.Activity = Activity.Work;
                p.Journey = null; p.DestinationId = ""; p.DepartureMinute = 0;
                p.NextEventMinute = p.ActivityUntilMinute = 3280;
                p.CommutePreparation = new CommutePreparation { PlaceId = "work", WorkStartsAt = 2800,
                    WorkEndsAt = 3280, PreparedAt = 2800, TravelMinutes = 1 };
                VisitorSchedules.RecordArrival(p, 2800, "work", Activity.Work);
                EmploymentWages.Begin(p, new WorkWindow("work", Activity.Work, 2800, 3280), true);
            }
            return city.RebuildRoutes(routes);
        }
    }

    [Fact]
    public void AnOvernightMissIsRecordedOnlyAtShiftEnd()
    {
        var (p, b, w) = Shift(2800, 3280); p.Activity = Activity.Home; p.PlaceId = "home";
        EmploymentWages.Begin(p, w, true); EmploymentWages.Settle(p, b, w, 2880);
        Assert.Equal(0, EmploymentWages.RecentMisses(p.Accrual, 2)); Assert.Equal(0, p.MissedActivities);
        EmploymentWages.Settle(p, b, w, 3280);
        Assert.Equal(1, EmploymentWages.RecentMisses(p.Accrual, 2)); Assert.Equal(1, p.MissedActivities);
        Assert.Equal(Money.Zero, b.Ledger.EmploymentIncome);
        EmploymentWages.Settle(p, b, w, 3280); Assert.Equal(1, p.MissedActivities);
    }

    [Fact]
    public void MissHistoryAgesOnCalendarDaysAndIncreasesTheSameStableJobLossDraw()
    {
        var a = new EmploymentAccrual { Day = 10, MissMask = (1 << 14) - 1 };
        Assert.Equal(14, EmploymentWages.RecentMisses(a, 10));
        Assert.Equal(1, EmploymentWages.RecentMisses(a, 23));
        Assert.Equal(0, EmploymentWages.RecentMisses(a, 24));
        Assert.InRange(EmploymentWages.JobLossChance(a, 10), CityBalance.DailyJobLossChance * 2, CityBalance.MaximumJobLossChance);
        Assert.Equal(CityBalance.DailyJobLossChance, EmploymentWages.JobLossChance(a, 24));
        var city = Fresh().State; var p = city.Citizens[0];
        p.LastEmploymentCheckDay = 9; p.Accrual = a;
        var copy = CitySimulator.CloneForEvent(p);
        ResidentLifecycle.BeginEmploymentCheck(city, p, 10 * 1440);
        ResidentLifecycle.BeginEmploymentCheck(city, copy, 10 * 1440);
        Assert.Equal(p.DayPlaceId, copy.DayPlaceId); Assert.Equal(p.Accrual, copy.Accrual);
    }

    [Fact]
    public void TheSameDrawLosesTheJobOnlyWithRecentMissesAndSupportStartsAfterLoss()
    {
        var state = Fresh().State; var clean = state.Citizens[0]; clean.LastEmploymentCheckDay = 9;
        state.Seed = Enumerable.Range(0, 100000).First(seed => {
            double draw = (StableRandom.Hash("c0:employment:10", seed) + 1.0) / (uint.MaxValue + 2.0);
            return draw >= CityBalance.DailyJobLossChance && draw < CityBalance.DailyJobLossChance * 7.5;
        });
        var absent = CitySimulator.CloneForEvent(clean);
        absent.Accrual = new EmploymentAccrual { Day = 9, Shift = EmploymentWages.Settled, MissMask = (1 << 13) - 1 };
        var reloaded = JsonConvert.DeserializeObject<CitizenState>(JsonConvert.SerializeObject(absent))!;
        ResidentLifecycle.BeginEmploymentCheck(state, clean, 10 * 1440);
        ResidentLifecycle.BeginEmploymentCheck(state, absent, 10 * 1440);
        ResidentLifecycle.BeginEmploymentCheck(state, reloaded, 10 * 1440);
        Assert.Equal("work", clean.DayPlaceId); Assert.Equal("", absent.DayPlaceId);
        Assert.Equal(absent.Accrual, reloaded.Accrual); Assert.Equal(absent.JobChanges, reloaded.JobChanges);
        Assert.Equal(default, absent.Accrual);
        var budget = HouseholdFinance.Create(clean, 42, 10 * 1440, null);
        HouseholdFinance.PostDay(budget, 10 * 1440, clean); Assert.Equal(Money.Zero, budget.IncomeReceived);
        HouseholdFinance.PostDay(budget, 11 * 1440, absent);
        Assert.Equal(Money.FromCents(budget.BaseIncomeCents * CityBalance.SupportIncomePercent / 100), budget.IncomeReceived);
        Assert.True(HouseholdFinance.CashConserved(budget));
    }

    [Fact]
    public void ReopeningDuringACancelledShiftAndReloadKeepTheClosureDay()
    {
        var routes = new Routes(); var city = Fresh(routes: routes);
        var w = DailyRoutine.WorkOnDay(city.State.Citizens[0], city.State.Seed, city.State.Minute)!.Value;
        city.SetLogicalWorkplaceClosure("work", true); Advance(city, w.StartsAt + 1);
        Assert.Equal(EmploymentWages.Closed, city.State.Citizens[0].Accrual.Shift);
        city.SetLogicalWorkplaceClosure("work", false); city = Reload(city, routes);
        Advance(city, w.EndsAt + 1);
        Assert.Equal(Money.Zero, Budget(city).Ledger.EmploymentIncome);
        Assert.Equal(EmploymentWages.ClosureDay, city.State.Citizens[0].Accrual.Shift);
        Assert.Equal(0, city.State.Citizens[0].MissedActivities);
    }

    [Fact]
    public void WageOverflowIsAtomicAndTheCoreCannotReferenceNativePayroll()
    {
        var (p, b, w) = Shift(); EmploymentWages.Begin(p, w, true);
        b.Balance = b.OpeningBalance = Money.FromUnits(long.MaxValue);
        string before = JsonConvert.SerializeObject(new { p.Accrual, b });
        Assert.Throws<OverflowException>(() => EmploymentWages.Settle(p, b, w, w.EndsAt));
        Assert.Equal(before, JsonConvert.SerializeObject(new { p.Accrual, b }));
        Assert.All(typeof(EmploymentWages).Assembly.GetReferencedAssemblies(), a => {
            string name = a.Name ?? "";
            Assert.False(name.StartsWith("Assembly-CSharp", StringComparison.Ordinal));
            Assert.False(name.StartsWith("UnityEngine", StringComparison.Ordinal));
            Assert.NotEqual("PersistentCity.Runtime", name);
        });
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    }
}
