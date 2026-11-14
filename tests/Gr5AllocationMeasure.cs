using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5AllocationMeasure
{
    private sealed class AllocationTypes : EventListener
    {
        public readonly Dictionary<string, long> Bytes = new Dictionary<string, long>();
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
            lock (Bytes) Bytes[type] = (Bytes.TryGetValue(type, out long b) ? b : 0) + bytes;
        }
    }

    private sealed class StopSpots : IOptionalStopEnvironment
    {
        private readonly Dictionary<(string, DwellKind), OptionalStopSpot[]> spots = new Dictionary<(string, DwellKind), OptionalStopSpot[]>();
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            if (spots.TryGetValue((place.Id, kind), out var known)) return known;
            var made = Enumerable.Range(0, 4).Select(i => new OptionalStopSpot { Id = place.Id + ":" + kind + ":" + i, TargetId = place.Id,
                Position = new Position(place.Position.X + i, 0, place.Position.Z + 2 + (int)kind * 3), Facing = place.Position }).ToArray();
            spots.Add((place.Id, kind), made);
            return made;
        }
        public OptionalStopAvailability Available(OptionalStopState stop) => OptionalStopAvailability.Ready;
    }

    private static void Frame(CitySimulator simulator, double minute)
    {
        simulator.RefreshOptionalStops();
        simulator.AdvanceFrameSlice(minute, 32);
    }

    internal static CityState City()
    {
        string? source = PerfGate.Variable("PC_GR5_PAYLOAD");
        if (!string.IsNullOrEmpty(source))
        {
            var read = SaveCodec.Read(CapturedPayload.Load(source));
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            return read.State!;
        }
        var state = EconomyScenario.Create(CityBalance.CityResidentTotal, RepresentativeFixture.Seed);
        PortalPlaces.Add(state, RepresentativeFixture.Portals);
        VisitorSchedules.Create(state, 2 * CityBalance.CityResidentTotal);
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
            Shops = Enumerable.Range(1, 3).Select(i => new FoodScope { PlaceId = "shop-" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
        return state;
    }

    [Fact]
    public void MeasureDayAndNightHourAllocation()
    {
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (string.IsNullOrEmpty(path)) return;
        string label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "";
        const int fps = 30;
        using var types = new AllocationTypes();
        for (int run = 1; run <= 3; run++)
        {
            var state = City();
            var simulator = new CitySimulator(state, new FreshCityTraceTests.Walk());
            simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            simulator.AttachOptionalStops(new StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
            foreach (var (window, start) in new[] { ("night-01:00", day + 60), ("day-15:30", day + 15.5 * 60) })
            {
                simulator.AdvanceTo(start - 30, int.MaxValue);
                for (double minute = start - 30; minute < start; minute += 1.0 / fps) Frame(simulator, minute);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                lock (types.Bytes) types.Bytes.Clear();
                types.Recording = run == 3;
                int gen0 = GC.CollectionCount(0), frames = 0;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (double minute = start; minute < start + 60; minute += 1.0 / fps) { Frame(simulator, minute); frames++; }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                types.Recording = false;
                List<KeyValuePair<string, long>> top;
                lock (types.Bytes) top = types.Bytes.OrderByDescending(p => p.Value).Take(15).ToList();
                System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(new
                {
                    label, run, window, optionalStops = OptionalStopPolicy.ApprovedChoicePercentage, residents = state.Citizens.Count, visitors = state.Visitors.Count, places = state.Places.Count,
                    gameMinutes = 60, frames, allocatedBytes = allocated, bytesPerGameMinute = allocated / 60, bytesPerFrame = allocated / frames,
                    gen0 = GC.CollectionCount(0) - gen0,
                    topTypes = run == 3 ? top.ToDictionary(p => p.Key, p => p.Value) : null,
                }) + Environment.NewLine);
            }
        }
    }
}
