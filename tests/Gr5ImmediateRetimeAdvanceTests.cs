using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5ImmediateRetimeAdvanceTests
{
    private readonly ITestOutputHelper output;
    public Gr5ImmediateRetimeAdvanceTests(ITestOutputHelper output) { this.output = output; }

    private const double Day = 2 * 1440, Start = Day + 15.5 * 60;
    private const string Reference = "33E60DD6698AA9833154E1B337641A188D666FAB7496A6FF2868E7D2D9CF791C";

    internal static void Run(CitySimulator city, Gr5AdvanceAllocationTests.Planner planner, double from, double to, double rate,
        Gr5AdvanceAllocationTests.Window? window, Action<GcAllocationSegment>? commit = null)
    {
        for (double minute = from; minute < to; minute += rate / 30)
        {
            city.RefreshOptionalStops();
            commit?.Invoke(GcAllocationSegment.ModOptionalStops);
            var budget = new PopulationFrameBudget(1.0 / 30, false);
            int citizenEvents = 0;
            for (int unit = 0; unit < budget.Units; unit++)
            {
                long plannerBefore = planner.PlannerBytes;
                long before = GC.GetAllocatedBytesForCurrentThread();
                GcAllocationSegment kind;
                bool stop;
                if (city.PlanningPending)
                {
                    bool finished = city.ContinuePlanning(2);
                    kind = finished ? GcAllocationSegment.AdvancePlanningFinish : GcAllocationSegment.AdvancePlanningSlice;
                    if (finished) citizenEvents++;
                    stop = finished && citizenEvents >= budget.CitizenEvents;
                }
                else
                {
                    var result = city.AdvanceFrameSlice(minute, 1);
                    kind = GcPauseSampler.AdvanceEventSegment(result.TrafficEvents, result.FoodContinuationEvents, result.CitizenEvents, result.PlanningPending);
                    citizenEvents += result.CitizenEvents;
                    stop = !result.PlanningPending && (result.CaughtUp || result.ThroughRoutePending || citizenEvents >= budget.CitizenEvents);
                }
                commit?.Invoke(kind);
                long used = GC.GetAllocatedBytesForCurrentThread() - before - (planner.PlannerBytes - plannerBefore);
                if (window != null) { window.Bytes[(int)kind] += used; window.Calls[(int)kind]++; window.PlannerBytes += planner.PlannerBytes - plannerBefore; }
                if (stop) break;
            }
            city.SynchronizeSnapshot();
            commit?.Invoke(GcAllocationSegment.AdvanceOther);
        }
    }

    internal static (CityState State, CitySimulator City, Gr5AdvanceAllocationTests.Planner Planner) AfterImmediateRetime()
    {
        var (state, city, planner) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(Start - 30, 256).CaughtUp) { }
        Gr5AdvanceAllocationTests.Run(city, planner, Start - 30, Start, null);
        var driver = new ClockRetimeDriver();
        while (true)
        {
            var outcome = driver.Update(state, 2.0, false, Gr5AdvanceAllocationTests.WalkMetresPerMinute / 60, 4);
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            var rebuild = city.BeginRebuildRoutes(planner, driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            city = rebuild.TakeResult();
            break;
        }
        Assert.Equal(2.0, state.TravelClockRate);
        return (state, city, planner);
    }

    [Fact]
    public void TheWindowAfterAnImmediateRetimeAt2xKeepsTheSaveHashAndStaysUnderItsBytesPerCall()
    {
        var (state, city, planner) = AfterImmediateRetime();
        var window = new Gr5AdvanceAllocationTests.Window();
        Run(city, planner, Start, Start + 240, 2, window);
        city.SynchronizeSnapshot();
        string hash = RushSpikeReplay.Sha(DevelopmentChain.Write(state));
        foreach (var s in Gr5AdvanceAllocationTests.Reported)
            output.WriteLine("2x after 1->2 " + GcPauseSampler.SegmentName(s) + ": " + window.Bytes[(int)s] + " B / " + window.Calls[(int)s] + " calls = " + window.Per(s).ToString("0") + " B/call (.NET)");
        output.WriteLine("2x after 1->2 total " + window.Total + " B, planner fixture " + window.PlannerBytes + " B (.NET)");
        output.WriteLine(hash);
        Assert.True(window.Calls[(int)GcAllocationSegment.AdvanceEventsCitizen] > 1000, "citizen calls " + window.Calls[(int)GcAllocationSegment.AdvanceEventsCitizen]);
        Assert.True(window.Per(GcAllocationSegment.AdvanceEventsCitizen) < 700, "citizen bytes per call " + window.Per(GcAllocationSegment.AdvanceEventsCitizen));
        Assert.True(window.Per(GcAllocationSegment.AdvanceEventsFood) < 560, "food bytes per call " + window.Per(GcAllocationSegment.AdvanceEventsFood));
        Assert.True(window.Per(GcAllocationSegment.AdvancePlanningSlice) < 1, "planning slice bytes per call " + window.Per(GcAllocationSegment.AdvancePlanningSlice));
        Assert.True(window.Per(GcAllocationSegment.AdvanceEventsOther) < 1, "idle bytes per call " + window.Per(GcAllocationSegment.AdvanceEventsOther));
        Assert.True(window.Per(GcAllocationSegment.AdvanceEventsRouteBegin) < 470, "route begin bytes per call " + window.Per(GcAllocationSegment.AdvanceEventsRouteBegin));
        Assert.True(window.Total < 18_500_000, "window bytes " + window.Total);
        Assert.Equal(Reference, hash);
    }

    private static FoodAlternativeObservation Rival(string owner = "rival-owner", int opens = 0) =>
        new("rival", "s0", "street", 1, 1, "food", owner, FoodAlternativeKind.Rival, true, true, false, null, 0,
            new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(250)) },
            Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, opens, 1320)));

    [Fact]
    public void ARivalFingerprintIsComputedOnceAndTheHeldTermsCompareWithoutACopy()
    {
        var rival = Rival();
        string first = RivalFoodTerms.Fingerprint(rival);
        string held = "10" + first, paid = "11" + first;
        long before = GC.GetAllocatedBytesForCurrentThread();
        string again = RivalFoodTerms.Fingerprint(rival);
        for (int i = 0; i < 100; i++)
        {
            Assert.True(RivalFoodTerms.TermsMatch(rival, held, 2));
            Assert.True(RivalFoodTerms.TermsMatch(rival, paid, 2));
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(first, again);
        Assert.Equal(first, RivalFoodTerms.Fingerprint(Rival()));
        Assert.True(RivalFoodTerms.Canonical(first));
        Assert.True(RivalFoodTerms.TermsMatch(rival, first, 0));
        Assert.False(RivalFoodTerms.TermsMatch(rival, "10" + RivalFoodTerms.Fingerprint(Rival("other-owner")), 2));
        Assert.False(RivalFoodTerms.TermsMatch(rival, "10" + RivalFoodTerms.Fingerprint(Rival(opens: 60)), 2));
        Assert.False(RivalFoodTerms.TermsMatch(rival, "10" + first.Substring(1), 2));
        Assert.False(RivalFoodTerms.TermsMatch(rival, held + "x", 2));
        Assert.False(RivalFoodTerms.TermsMatch(rival, "1", 2));
    }

    [Fact]
    public void ARivalCaptureCopiesTheAdvertisedHoursAndSharesOneClosingPlacePerObservation()
    {
        var rival = Rival(opens: 420);
        var business = RivalFoodTerms.Capture(rival, 5);
        var copied = RivalFoodTerms.CopyHours(rival);
        Assert.Equal(business.Hours.Select(h => (h.Day, h.FromMinute, h.ToMinute)), copied.Select(h => (h.Day, h.FromMinute, h.ToMinute)));
        Assert.NotSame(RivalFoodTerms.CopyHours(rival), copied);
        var place = RivalFoodTerms.HoursPlace(rival);
        Assert.Same(place, RivalFoodTerms.HoursPlace(rival));
        var fresh = new Place { WeeklyHours = rival.Hours.Select(h => new OpeningWindow { Day = h.Day, FromMinute = h.FromMinute, ToMinute = h.ToMinute }).ToList(), OpenMinute = 0, CloseMinute = 0 };
        for (double minute = Day; minute < Day + 3 * 1440; minute += 37)
        {
            Assert.Equal(DailyRoutine.NextClose(fresh, minute), DailyRoutine.NextClose(place, minute));
            Assert.Equal(DailyRoutine.Open(fresh, minute), DailyRoutine.Open(place, minute));
        }
        DailyRoutine.NextClose(place, Day + 600);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 50; i++) { RivalFoodTerms.HoursPlace(rival); DailyRoutine.NextClose(place, Day + 600); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void AHeldDecisionTuningIsReusedUntilAnyChoiceSettingChanges()
    {
        var live = new FoodChoiceSettings();
        var held = FoodDecisionTuning.Capture(live, null);
        Assert.Same(held, FoodDecisionTuning.Capture(live, held));
        Assert.Equal(FoodDecisionTuning.Capture(live).ToWire(), held.ToWire());
        Assert.True(FoodDecisionTuning.TryParse(held.ToWire(), out var parsed));
        Assert.NotSame(parsed, FoodDecisionTuning.Capture(live, parsed));
        var properties = typeof(FoodChoiceSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).ToArray();
        Assert.Equal(15, properties.Length);
        foreach (var property in properties)
        {
            var changed = new FoodChoiceSettings();
            var current = FoodDecisionTuning.Capture(changed, null);
            if (property.PropertyType == typeof(int)) property.SetValue(changed, (int)property.GetValue(changed)! - 1);
            else property.SetValue(changed, (double)property.GetValue(changed)! + 0.125);
            var next = FoodDecisionTuning.Capture(changed, current);
            Assert.NotSame(current, next);
            Assert.Equal(FoodDecisionTuning.Capture(changed).ToWire(), next.ToWire());
            Assert.Same(next, FoodDecisionTuning.Capture(changed, next));
        }
        live.FoodChoiceTemperature = double.NaN;
        var nan = FoodDecisionTuning.Capture(live, held);
        Assert.NotSame(held, nan);
        Assert.Same(nan, FoodDecisionTuning.Capture(live, nan));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) FoodDecisionTuning.Capture(live, nan);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void AnOptionalChoiceKeyHashesTheSameCharactersAsItsStringWithoutAllocating()
    {
        var ends = new List<double> { 0, 1, 1020, 1020.5, 4083.1, 1.0 / 3, 0.1 + 0.2, 1e-7, 123456789.125, 2 * 1440 + 15.5 * 60 + 1.0 / 30, double.Epsilon, 5e15 };
        var random = new Random(39);
        for (int i = 0; i < 400; i++) ends.Add(random.NextDouble() * 20000);
        foreach (var seed in new[] { 0, 7, -12345 })
            foreach (var id in new[] { "rlo:rig:c00001", "", "visitor:42" })
                foreach (Activity activity in Enum.GetValues(typeof(Activity)))
                    foreach (double end in ends)
                    {
                        string key = CitySimulator.OptionalKey(id, activity, end);
                        uint state = CitySimulator.OptionalKeyState(id, activity, end, seed);
                        Assert.Equal(StableRandom.Append(StableRandom.Begin(seed), key), state);
                        Assert.Equal(WeatherPolicy.AdmissionDraw(key, seed), WeatherPolicy.AdmissionDraw(state));
                    }
        Assert.Equal("p:optional:9:3", CitySimulator.OptionalKey("p", (Activity)9, 3));
        Assert.Equal(StableRandom.Append(StableRandom.Begin(1), "p:optional:9:3"), CitySimulator.OptionalKeyState("p", (Activity)9, 3, 1));
        var places = Enumerable.Range(0, 40).Select(i => new Place { Id = "shop-" + i, Kind = PlaceKind.Shop, Capacity = 1, Position = new Position(i * 13 % 70, 0, i * 7 % 50) }).ToList();
        var origin = new Place { Id = "home", Kind = PlaceKind.Home, Position = new Position(10, 0, 10) };
        var byString = new List<(Place Place, double Score)>(9);
        var byState = new List<(Place Place, double Score)>(9);
        foreach (double end in ends.Take(40))
        {
            DestinationChoice.Shortlist(places, origin, PlaceKind.Shop, CitySimulator.OptionalKey("c", Activity.Shop, end), 5, byString);
            DestinationChoice.Shortlist(places, origin, PlaceKind.Shop, CitySimulator.OptionalKeyState("c", Activity.Shop, end, 5), byState);
            Assert.Equal(byString.Select(r => (r.Place.Id, r.Score)), byState.Select(r => (r.Place.Id, r.Score)));
        }
        CitySimulator.OptionalKeyState("rlo:rig:c00001", Activity.Leisure, 4083.1, 7);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++) CitySimulator.OptionalKeyState("rlo:rig:c00001", (Activity)(i % 5), 1000 + i / 7.0, 7);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private sealed class Types : EventListener
    {
        public readonly Dictionary<(GcAllocationSegment, string), long> Bytes = new Dictionary<(GcAllocationSegment, string), long>();
        private readonly Dictionary<string, long> pending = new Dictionary<string, long>();
        public bool Recording;
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }
        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!Recording || e.EventName == null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || e.Payload == null) return;
            int name = e.PayloadNames!.IndexOf("TypeName"), amount = e.PayloadNames.IndexOf("AllocationAmount64");
            if (name < 0) return;
            string type = (string)e.Payload[name]!;
            long bytes = amount >= 0 ? Convert.ToInt64(e.Payload[amount]) : 100_000;
            lock (pending) pending[type] = (pending.TryGetValue(type, out long b) ? b : 0) + bytes;
        }
        public void Commit(GcAllocationSegment kind)
        {
            lock (pending)
            {
                foreach (var pair in pending) Bytes[(kind, pair.Key)] = (Bytes.TryGetValue((kind, pair.Key), out long b) ? b : 0) + pair.Value;
                pending.Clear();
            }
        }
    }

    [Fact]
    public void MeasureImmediateRetimeAdvanceTypes()
    {
        if (PerfGate.Variable("PC_GR5_ADVANCE_TYPES") == null) return;
        var (_, city, planner) = AfterImmediateRetime();
        using var types = new Types();
        var window = new Gr5AdvanceAllocationTests.Window();
        types.Recording = true;
        Run(city, planner, Start, Start + 240, 2, window, types.Commit);
        types.Recording = false;
        output.WriteLine("2x total " + window.Total + " B, planner fixture " + window.PlannerBytes + " B (sampled types include the fixture)");
        lock (types.Bytes)
            foreach (var pair in types.Bytes.OrderByDescending(p => p.Value).Take(70))
                output.WriteLine("type " + GcPauseSampler.SegmentName(pair.Key.Item1) + " " + pair.Key.Item2 + " " + pair.Value);
    }
}
