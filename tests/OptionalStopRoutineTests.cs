using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;
// The realised share under real daily routines, where every resident makes repeated eligible visits for weeks
// (review ob2). The single-visit share fixtures cannot lose a chance to a repeated visit; these can.
public sealed class OptionalStopRoutineTests(ITestOutputHelper output)
{
    // Tolerance: +/- 3 percentage points of min(setting, feasible share), as in OptionalStopShareTests.
    private const double Tolerance = 0.03;
    // Walks of `minutes`, or of at least three minutes at 80 m a minute.
    private sealed class Walking(double minutes = 0) : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences p) => new()
        { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
            Minutes = minutes > 0 ? minutes : Math.Max(3, Position.Distance(from.Position, to.Position) / 80) } } };
    }
    // Feasible venues offer `individual` break or window-shopping spots and four conversation spots.
    private sealed class Geometry(int individual) : IOptionalStopEnvironment
    {
        public HashSet<string> Feasible = new(StringComparer.Ordinal);
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            if (!Feasible.Contains(place.Id)) return Array.Empty<OptionalStopSpot>();
            int count = kind == DwellKind.ChatOutsideVenue ? 4 : kind == DwellKind.Audience || kind == DwellKind.Resting ? 0 : individual;
            return Enumerable.Range(0, count).Select(i => new OptionalStopSpot { Id = place.Id + ":" + kind + ":" + i, TargetId = place.Id,
                Position = new(place.Position.X + i, 0, place.Position.Z + 2 + (int)kind * 3), Facing = place.Position }).ToArray();
        }
        public OptionalStopAvailability Available(OptionalStopState stop) => Feasible.Contains(stop.PlaceId) ? OptionalStopAvailability.Ready : OptionalStopAvailability.Unavailable;
    }
    // Counted independently of the scheduler: eligible admissions by citizen/place/minute, draws from each visit's
    // key, and stop recipients by member and visit minute, including the waiting member of a new conversation.
    private sealed class Observer
    {
        public int Eligible, Drawn; public readonly HashSet<string> Stops = new(StringComparer.Ordinal);
        public double Share => (double)Stops.Count / Eligible;
    }
    private static Observer Watch(CitySimulator sim, int rate)
    {
        var o = new Observer(); var visits = new HashSet<string>(StringComparer.Ordinal);
        var places = sim.State.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        sim.ActivityObserved += (id, c, minute) =>
        {
            if (c.Phase != CitizenPhase.AtPlace || c.MealOuting != null || c.RecentVisits.Count == 0 || c.RecentVisits[^1].AtMinute != minute
                || c.RecentVisits[^1].PlaceId != c.PlaceId || c.Activity == Activity.Home || c.Activity == Activity.Study
                || !DailyRoutine.Open(places[c.PlaceId], minute)) return;
            string key = OptionalStopPolicy.VisitKey(c, minute); Assert.True(visits.Add(key)); o.Eligible++;
            if (OptionalStopPolicy.Choose(key, sim.State.Seed, rate)) o.Drawn++;
            if (c.OptionalStop != null && c.OptionalStop.VisitMinute == minute)
                foreach (var member in c.OptionalStop.Members.DefaultIfEmpty(id)) o.Stops.Add(member + "@" + minute);
        };
        return o;
    }
    private static int Pending(CitySimulator sim) => sim.State.Citizens.Count(c => c.OptionalStopChoice == true && c.OptionalStop == null);
    private static void Until(CitySimulator sim, double minute) { while (!sim.AdvanceTo(minute).CaughtUp) { } }
    private static CitySimulator Reload(CitySimulator sim, IOptionalStopEnvironment env, int rate)
    {
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, rate);
        Assert.True(loaded.OptionalStopIndexesMatchState());
        return loaded;
    }
    // The audit with its denominator in the same struct, against the independent counts.
    private static void AssertAudit(CitySimulator sim, Observer o, int pendingAtAttach)
    {
        var t = sim.OptionalStopChances;
        Assert.Equal(o.Eligible, t.Eligible); Assert.Equal(o.Drawn, t.Drawn); Assert.Equal(o.Stops.Count, t.Realised);
        Assert.Equal(t.Drawn, t.Realised + Pending(sim) - pendingAtAttach + t.Dropped);
        Assert.True(sim.OptionalStopIndexesMatchState());
    }

    // 1,500 commuters keep the workplace and shops Population.Create gave them for 112 days. Only every fourth
    // shop has window-shopping geometry and no workplace has any, so the feasible share (what 100 realises) is
    // only just above 25%: almost every feasible visit must take a chance drawn at an infeasible one.
    private static (CitySimulator Sim, Observer Observed) Commuters(int rate, bool allShops = false)
    {
        const int count = 1500;
        var places = new List<Place> { new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count },
            new() { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new(100, 0, 0) } };
        var env = new Geometry(40);
        for (int i = 0; i < 40; i++)
        {
            places.Add(new() { Id = "shop" + i, Name = "Shop", Kind = PlaceKind.Shop, Capacity = count, Position = new(1000 * (i + 1), 0, 0) });
            if (allShops || i % 4 == 0) env.Feasible.Add(places[^1].Id);
        }
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity { Id = "r" + i.ToString("D4"), Name = "Resident",
            Age = 30, Persona = Persona.Commuter }), places, "astra-routines", "0.40.4", 17, 1440);
        var sim = new CitySimulator(state, new Walking(10)); sim.AttachOptionalStops(env, rate);
        return (sim, Watch(sim, rate));
    }

    // d438cc6: 21.07% at 25 over 112 days (8,110 chances dropped) against a feasible share of 25.30%.
    [Fact]
    public void RepeatedDailyRoutinesRealiseTheSettingWithoutLosingChances()
    {
        var (zero, none) = Commuters(0); Until(zero, 29 * 1440);
        Assert.Empty(none.Stops); AssertAudit(zero, none, 0);

        var (hundred, all) = Commuters(100); Until(hundred, 113 * 1440);
        AssertAudit(hundred, all, 0);
        double target = Math.Min(0.25, all.Share);

        var (sim, share) = Commuters(25); Until(sim, 29 * 1440);
        int earlyEligible = share.Eligible, earlyStops = share.Stops.Count; double early = share.Share;
        Until(sim, 113 * 1440);
        // Days 29-112 alone: a startup backlog would have cleared, a permanent loss would remain.
        double late = (double)(share.Stops.Count - earlyStops) / (share.Eligible - earlyEligible);
        var t = sim.OptionalStopChances;
        output.WriteLine($"feasible {all.Share:P2} ({all.Stops.Count}/{all.Eligible}); 25: days 1-28 {early:P2}, days 29-112 {late:P2}, "
            + $"whole {share.Share:P2}; drawn {t.Drawn}, handed on {t.HandedOn}, passed on {t.PassedOn}, dropped {t.Dropped}, pending {Pending(sim)}");
        Assert.InRange(all.Share, 0.25, 0.26);
        Assert.Equal(all.Eligible, share.Eligible);
        Assert.InRange(share.Share, target - Tolerance, target + Tolerance);
        Assert.InRange(late, target - Tolerance, target + Tolerance);
        Assert.InRange(early, target - Tolerance, target + Tolerance);
        // Repeated visits pass their second chance on, and the bound holds the backlog: nothing is lost.
        Assert.Equal(0, t.Dropped);
        Assert.True(t.PassedOn > 1000, "expected repeated holders to be drawn again: " + t.PassedOn);
        AssertAudit(sim, share, 0);
        Assert.True(SaveCodec.Read(SaveCodec.Write(sim.State)).Status == SaveReadStatus.Loaded);
    }

    // 600 residents of every persona with homes, workplaces, schools, shops and leisure venues; about a third of the
    // venues ("narrow") or most of them ("wide") have geometry. Conversations count both members.
    private static (CitySimulator Sim, Geometry Env) Mixed(int rate, bool wide, bool mid = false)
    {
        var personas = (Persona[])Enum.GetValues(typeof(Persona));
        var ids = Enumerable.Range(0, 600).Select(i => new CitizenIdentity { Id = "r" + i.ToString("D4"), Name = "R" + i, Age = 20 + i % 50,
            Persona = personas[i % personas.Length] }).ToArray();
        var places = new List<Place>();
        for (int i = 0; i < 30; i++) places.Add(new Place { Id = "home" + i.ToString("D2"), Name = "H", Kind = PlaceKind.Home, Capacity = 40, Position = new(i * 50, 0, 0) });
        for (int i = 0; i < 15; i++) places.Add(new Place { Id = "work" + i.ToString("D2"), Name = "W", Kind = PlaceKind.Work, Capacity = 80, Position = new(i * 70, 0, 400), OpenMinute = 360, CloseMinute = 1320 });
        for (int i = 0; i < 3; i++) places.Add(new Place { Id = "school" + i, Name = "S", Kind = PlaceKind.Study, Capacity = 200, Position = new(i * 70, 0, 600) });
        for (int i = 0; i < 12; i++) places.Add(new Place { Id = "shop" + i.ToString("D2"), Name = "Sh", Kind = PlaceKind.Shop, Capacity = 80, Position = new(i * 60, 0, 200), OpenMinute = 480, CloseMinute = 1260 });
        for (int i = 0; i < 8; i++) places.Add(new Place { Id = "fun" + i.ToString("D2"), Name = "L", Kind = PlaceKind.Leisure, Capacity = 80, Position = new(i * 90, 0, -200), OpenMinute = 600, CloseMinute = 1439 });
        var state = Population.Create(ids, places, "routine-mixed", "0.40.4", 23, 1440 * 3);
        var env = new Geometry(3);
        foreach (var id in new[] { "work01", "work05", "work09", "work13", "shop00", "shop04", "shop08", "fun00", "fun03", "fun06" }) env.Feasible.Add(id);
        if (wide) foreach (var p in places.Where(p => p.Kind != PlaceKind.Home && string.CompareOrdinal(p.Id, "work08") < 0
            || p.Id.StartsWith("shop0", StringComparison.Ordinal) || p.Id.StartsWith("fun0", StringComparison.Ordinal) && p.Id != "fun07")) env.Feasible.Add(p.Id);
        else foreach (var id in new[] { "work02", "work03", "work04", "shop01", "shop02", "fun01", "fun02" }) env.Feasible.Add(id);
        if (mid) foreach (var id in new[] { "work06", "work07", "work10", "shop03", "shop05", "fun04" }) env.Feasible.Add(id);
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(env, rate);
        return (sim, env);
    }

    // Ten days of mixed routines with conversations. The indexes stay exact after every event.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MixedRoutinesWithConversationsTrackTheSetting(bool wide)
    {
        var (all, _) = Mixed(100, wide); var feasible = Watch(all, 100); Until(all, 1440 * 13);
        AssertAudit(all, feasible, 0);
        foreach (int rate in new[] { 0, 10, 25, 50, 75, 100 })
        {
            var (sim, _) = Mixed(rate, wide); var o = Watch(sim, rate); int stale = 0;
            sim.ActivityObserved += (_, _, _) => { if (!sim.OptionalStopIndexesMatchState()) stale++; };
            Until(sim, 1440 * 13);
            var t = sim.OptionalStopChances; double target = Math.Min(rate / 100.0, feasible.Share);
            output.WriteLine($"{(wide ? "wide" : "narrow")} {rate}: {o.Share:P2} of {o.Eligible} (feasible {feasible.Share:P2}); drawn {t.Drawn}, "
                + $"handed on {t.HandedOn}, passed on {t.PassedOn}, dropped {t.Dropped}, pending {Pending(sim)}");
            Assert.Equal(0, stale);
            Assert.Equal(feasible.Eligible, o.Eligible);
            Assert.InRange(o.Share, target - Tolerance, target + Tolerance);
            AssertAudit(sim, o, 0);
        }
    }

    // Lowering the setting after days at 100 (d438cc6: 44.3% on the first day at 25 against 25.6% for a city
    // that ran at 25 throughout). At 100 nothing is banked, so the old setting leaves no stock to release.
    [Fact]
    public void LoweringFromHundredReleasesNoStockBankedAtTheHigherSetting()
    {
        var (high, env) = Mixed(100, wide: true); Until(high, 1440 * 6);
        int inherited = Pending(high);
        var (fresh, _) = Mixed(25, wide: true); Until(fresh, 1440 * 6);
        var lowered = Reload(high, env, 25);
        // 100 -> 0 -> 25: a day at 0 neither spends nor trims the saved chances.
        var paused = Reload(high, env, 0);
        var before = paused.State.Citizens.Where(c => c.OptionalStopChoice == true && c.OptionalStop == null).Select(c => c.Identity.Id).ToArray();
        var none = Watch(paused, 0); Until(paused, 1440 * 7);
        Assert.Empty(none.Stops); Assert.Equal(0, paused.OptionalStopChances.Dropped);
        Assert.Equal(before, paused.State.Citizens.Where(c => c.OptionalStopChoice == true && c.OptionalStop == null).Select(c => c.Identity.Id));
        var resumed = Reload(paused, env, 25);
        output.WriteLine($"pending after 3 days at 100: {inherited}; fresh 25: {Pending(fresh)}");
        for (int day = 6; day < 10; day++)
        {
            var a = Watch(lowered, 25); var b = Watch(fresh, 25); var c = day > 6 ? Watch(resumed, 25) : null;
            Until(lowered, 1440 * (day + 1)); Until(fresh, 1440 * (day + 1)); if (c != null) Until(resumed, 1440 * (day + 1));
            output.WriteLine($"day {day}: 100->25 {a.Share:P1}, 25 throughout {b.Share:P1}" + (c == null ? "" : $", 100->0->25 {c.Share:P1}"));
            Assert.InRange(a.Share, b.Share - Tolerance, b.Share + Tolerance);
            if (c != null) Assert.InRange(c.Share, b.Share - Tolerance, b.Share + Tolerance);
        }
        Assert.True(inherited <= high.State.Citizens.Count / 10, "pending after 100: " + inherited);
    }

    // Accepted schema-16 boundary, not a target for the setting: loss remains at 50 even with feasibility > 50.
    // The matched default arm also guards the empirical 4x bound under a second full-routine geometry.
    [Fact]
    public void HighSettingDeficitIsReconciledWhileTheDefaultTracksInAnotherGeometry()
    {
        var (all, feasible) = Commuters(100, allShops: true); Until(all, 113 * 1440);
        AssertAudit(all, feasible, 0);
        Assert.InRange(feasible.Share, 0.53, 0.55);
        foreach (int rate in new[] { 25, 50 })
        {
            var (sim, o) = Commuters(rate, allShops: true); Until(sim, 113 * 1440);
            Assert.Equal(feasible.Eligible, o.Eligible);
            AssertAudit(sim, o, 0);
            Assert.Equal(0, Pending(sim));
            output.WriteLine($"all shops, setting {rate}: {o.Share:P3} ({o.Stops.Count}/{o.Eligible}), feasible {feasible.Share:P3}, "
                + $"drawn {sim.OptionalStopChances.Drawn}, dropped {sim.OptionalStopChances.Dropped}");
            if (rate == 25)
            {
                Assert.InRange(o.Share, 0.25 - Tolerance, 0.25 + Tolerance);
                Assert.Equal(0, sim.OptionalStopChances.Dropped);
            }
            else
            {
                // Pin awareness of the reviewed deficit (>3 points), without promising min(setting, feasibility).
                Assert.InRange(o.Share, 0.46, 0.47);
                Assert.True(sim.OptionalStopChances.Dropped > 0);
            }
        }
    }

    // A decrease within 25-99 cannot distinguish inherited stock from an ordinary backlog without saved state.
    [Fact]
    public void LoweringFromFiftyRetainsInBoundStockAndCanSpendItOverSeveralDays()
    {
        var (high, env) = Mixed(50, wide: false, mid: true); Until(high, 1440 * 9);
        int inherited = Pending(high);
        Assert.InRange(inherited, 400, 600);
        var lowered = Reload(high, env, 25);
        Assert.Equal(inherited, Pending(lowered));
        Assert.Equal(0, lowered.OptionalStopChances.Dropped);
        var observed = Watch(lowered, 25);
        var (control, controlEnv) = Mixed(25, wide: false, mid: true); Until(control, 1440 * 9);
        control = Reload(control, controlEnv, 25);
        int controlOpening = Pending(control);
        var kept = Watch(control, 25);
        int elevatedDays = 0;
        for (int day = 9; day < 15; day++)
        {
            int admissions = observed.Eligible, stops = observed.Stops.Count;
            int controlAdmissions = kept.Eligible, controlStops = kept.Stops.Count;
            Until(lowered, 1440 * (day + 1)); Until(control, 1440 * (day + 1));
            double share = (double)(observed.Stops.Count - stops) / (observed.Eligible - admissions);
            double baseline = (double)(kept.Stops.Count - controlStops) / (kept.Eligible - controlAdmissions);
            if (share > baseline + Tolerance) elevatedDays++;
            output.WriteLine($"day {day}: 50->25 {share:P1}, kept at 25 {baseline:P1}, pending {Pending(lowered)}");
            AssertAudit(lowered, observed, inherited);
        }
        Assert.True(elevatedDays >= 2, "expected inherited stock to elevate multiple daily windows");
        Assert.True(Pending(lowered) < inherited);
        AssertAudit(control, kept, controlOpening);
    }
}
