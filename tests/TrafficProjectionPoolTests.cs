using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class TrafficProjectionPoolTests
{
    private const int Links = 48;
    private const ulong GoldenDigest = 1842371438352668522;
    private readonly ITestOutputHelper output;
    public TrafficProjectionPoolTests(ITestOutputHelper output) { this.output = output; }

    private sealed class City
    {
        public readonly TrafficKernel Kernel;
        public readonly Random Random;
        public readonly List<string> Completed = new List<string>();
        public readonly HashSet<int> Closed = new HashSet<int>();
        private readonly List<TrafficLinkDefinition> definitions = new List<TrafficLinkDefinition>();
        public readonly int Size;
        private int next;

        public City(int seed, int size = Links)
        {
            Random = new Random(seed);
            Size = size;
            for (int id = 0; id < size; id++)
                definitions.Add(new TrafficLinkDefinition
                {
                    Id = id, Capacity = 1 + Random.Next(4), FreeFlowMinutes = 0.5 + Random.NextDouble() * 1.5, HeadwayMinutes = Random.Next(3) * 0.1,
                    Signal = id % 5 == 0 ? new TrafficSignalSchedule { CycleMinutes = 2, GreenStartMinutes = 0.5, GreenDurationMinutes = 1, PhaseOffsetMinutes = Random.NextDouble() * 2 } : null,
                });
            Kernel = new TrafficKernel(definitions);
            Kernel.VehicleCompleted += (id, _) => Completed.Add(id);
        }

        public int Successor(int link, int choice) => (link + (choice == 0 ? 1 : choice == 1 ? 7 : 13)) % Size;

        public void Enqueue(int legs)
        {
            var route = new List<TrafficRouteLeg>();
            int link = Random.Next(Size);
            double nominal = 0;
            for (int i = 0; i < legs; i++)
            {
                if (i > 0) link = Successor(link, Random.Next(3));
                double free = definitions[link].FreeFlowMinutes;
                route.Add(new TrafficRouteLeg { LinkId = link, StartNominalMinutes = nominal, EndNominalMinutes = nominal + free, FreeFlowMinutes = free });
                nominal += free;
            }
            if (Random.Next(10) < 3) route[0].StartLinkFraction = 0.1 + Random.NextDouble() * 0.5;
            if (Random.Next(10) < 3) route[route.Count - 1].EndLinkFraction = Math.Max(route[route.Count - 1].StartLinkFraction + 0.2, 0.5 + Random.NextDouble() * 0.45);
            Kernel.Enqueue("v" + next++, route);
        }

        public void Advance(double minute)
        {
            Kernel.AdvanceTo(minute, 100000);
            foreach (string id in Completed) Kernel.RemoveCompleted(id);
            Completed.Clear();
        }
    }

    private sealed class Digest
    {
        public ulong Value = 14695981039346656037;
        public int Finite, Positive;
        public void Add(long bits)
        {
            for (int i = 0; i < 8; i++) { Value ^= (byte)(bits >> (8 * i)); Value *= 1099511628211; }
        }
        public void Add(double value)
        {
            Add(BitConverter.DoubleToInt64Bits(value));
            if (Position.Finite(value)) { Finite++; if (value > 0) Positive++; }
        }
        public void Add(string text) { foreach (char c in text) Add(c); }
    }

    [Fact]
    public void PooledEntryProjectionMatchesTheOriginalAndAFreshProjectionBitForBit()
    {
        var digest = new Digest();
        int fresh = 0;
        foreach (int seed in new[] { 11, 23, 37 })
        {
            var city = new City(seed);
            var random = city.Random;
            double minute = 0;
            for (int step = 0; step < 400; step++)
            {
                minute += 0.25;
                int arrivals = random.Next(3);
                for (int i = 0; i < arrivals; i++) city.Enqueue(2 + random.Next(7));
                if (random.Next(100) < 3) { int id = random.Next(Links); city.Kernel.SetLinkClosed(id, true); city.Closed.Add(id); }
                foreach (int id in new List<int>(city.Closed))
                    if (random.Next(10) == 0) { city.Kernel.SetLinkClosed(id, false); city.Closed.Remove(id); }
                city.Advance(minute);
                TrafficKernel? reference = step % 10 == 0 ? TrafficKernel.Restore(city.Kernel.Capture()) : null;
                for (int query = 0; query < 6; query++)
                {
                    int link = random.Next(Links);
                    double entry = minute + random.NextDouble() * 3, fraction = 0.05 + random.NextDouble() * 0.9, share = 0.2 + random.NextDouble() * 0.8;
                    double value = city.Kernel.ForecastEntry(link, entry, fraction, share);
                    digest.Add(value);
                    var forecast = city.Kernel.Forecast(link, entry);
                    digest.Add(forecast.DelayMinutes); digest.Add(forecast.Occupancy); digest.Add(forecast.WaitingVehicles);
                    if (reference != null)
                    {
                        Assert.Equal(BitConverter.DoubleToInt64Bits(reference.ForecastEntry(link, entry, fraction, share)), BitConverter.DoubleToInt64Bits(value));
                        fresh++;
                    }
                }
                if (step % 50 == 0) digest.Add(JsonConvert.SerializeObject(city.Kernel.Capture()));
                digest.Add(city.Kernel.ProcessedEvents);
            }
        }
        output.WriteLine(string.Format(CultureInfo.InvariantCulture, "digest {0}, finite {1}, positive {2}, fresh comparisons {3}", digest.Value, digest.Finite, digest.Positive, fresh));
        Assert.True(digest.Positive > 500, "only " + digest.Positive + " positive forecasts");
        Assert.True(fresh > 600);
        Assert.Equal(GoldenDigest, digest.Value);
    }

    private static City Busy(out double minute)
    {
        var city = new City(5);
        minute = 0;
        for (int step = 0; step < 160; step++)
        {
            minute += 0.25;
            city.Enqueue(3 + city.Random.Next(6));
            city.Advance(minute);
        }
        return city;
    }

    private static (long Bytes, int Calls, int Finite) Forecasts(City city, double minute, int rounds)
    {
        int calls = 0, finite = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int round = 0; round < rounds; round++)
            for (int link = 0; link < Links; link++)
            {
                double value = city.Kernel.ForecastEntry(link, minute + 0.5, 0.4, 0.6);
                calls++;
                if (Position.Finite(value)) finite++;
                city.Kernel.Forecast(link, minute + 0.5);
            }
        return (GC.GetAllocatedBytesForCurrentThread() - before, calls, finite);
    }

    [Fact]
    public void APooledEntryProjectionAllocatesNothingAfterWarmUp()
    {
        var city = Busy(out double minute);
        Forecasts(city, minute, 2);
        var measured = Forecasts(city, minute, 20);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} vehicles, {1} forecasts ({2} finite): {3:N0} B, {4:N1} B/forecast",
            city.Kernel.VehicleCount, measured.Calls, measured.Finite, measured.Bytes, (double)measured.Bytes / measured.Calls));
        Assert.True(city.Kernel.VehicleCount >= 20, city.Kernel.VehicleCount + " vehicles");
        Assert.True(measured.Finite * 5 >= measured.Calls, measured.Finite + " finite of " + measured.Calls);
        Assert.Equal(0, measured.Bytes);
    }

    [Fact]
    public void ASteadyTrafficAdvanceAllocatesNothingPerEvent()
    {
        var city = new City(7);
        for (int i = 0; i < 40; i++) city.Enqueue(60);
        double minute = 0;
        for (int step = 0; step < 40; step++) { minute += 0.25; city.Advance(minute); }
        long processed = city.Kernel.ProcessedEvents;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int step = 0; step < 400; step++) { minute += 0.25; city.Kernel.AdvanceTo(minute, 100000); }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        long events = city.Kernel.ProcessedEvents - processed;
        output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} vehicles, {1} events: {2:N0} B, {3:N1} B/event",
            city.Kernel.VehicleCount, events, bytes, events == 0 ? 0 : (double)bytes / events));
        Assert.True(events > 400, events + " events");
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void ReusedProjectionsDoNotLeakStateBetweenForecasts()
    {
        var city = Busy(out double minute);
        var reference = TrafficKernel.Restore(city.Kernel.Capture());
        var expected = new double[Links];
        for (int link = 0; link < Links; link++) expected[link] = TrafficKernel.Restore(city.Kernel.Capture()).ForecastEntry(link, minute + 0.5, 0.4, 0.6);
        for (int round = 0; round < 3; round++)
            for (int link = Links - 1; link >= 0; link--)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[link]), BitConverter.DoubleToInt64Bits(city.Kernel.ForecastEntry(link, minute + 0.5, 0.4, 0.6)));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[(link * 7) % Links]), BitConverter.DoubleToInt64Bits(reference.ForecastEntry((link * 7) % Links, minute + 0.5, 0.4, 0.6)));
            }
        Assert.Equal(JsonConvert.SerializeObject(reference.Capture()), JsonConvert.SerializeObject(city.Kernel.Capture()));
    }

    private static (long Bytes, long EnqueueBytes, long Plans, long Events) Window(City city, ref double minute, int minutes)
    {
        const int plansPerStep = 6, forecastsPerPlan = 30;
        long processed = city.Kernel.ProcessedEvents, plans = 0, enqueueBytes = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int step = 0; step < minutes * 4; step++)
        {
            minute += 0.25;
            for (int i = 0; i < plansPerStep; i++)
            {
                int from = city.Random.Next(city.Size);
                city.Kernel.ForecastEntry(from, minute + 0.2, 0.3, 0.7);
                for (int f = 0; f < forecastsPerPlan; f++) city.Kernel.Forecast((from + f * 7) % city.Size, minute + 0.2);
                plans++;
            }
            long enqueue = GC.GetAllocatedBytesForCurrentThread();
            for (int car = 0; car < 2; car++) city.Enqueue(4 + city.Random.Next(12));
            enqueueBytes += GC.GetAllocatedBytesForCurrentThread() - enqueue;
            city.Advance(minute);
        }
        return (GC.GetAllocatedBytesForCurrentThread() - before - enqueueBytes, enqueueBytes, plans, city.Kernel.ProcessedEvents - processed);
    }

    [Fact]
    public void MeasureADayWindowOfCityTrafficAndForecasts()
    {
        const int minutes = 174;
        var city = new City(3, 600);
        double minute = 0;
        Window(city, ref minute, 120);
        var measured = Window(city, ref minute, minutes);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0} links, {1} vehicles, {2} game minutes: {3} plans (30 forecasts each plus one entry forecast), {4} traffic events, {5:N0} B ({6:N2} MB) outside Enqueue, {7:N2} MB in Enqueue",
            city.Size, city.Kernel.VehicleCount, minutes, measured.Plans, measured.Events, measured.Bytes, measured.Bytes / 1048576.0, measured.EnqueueBytes / 1048576.0));
        Assert.True(measured.Events > 5000, measured.Events + " events");
        Assert.True(measured.Bytes < 64 * 1024, measured.Bytes + " B");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WeakReference> ForecastEveryQueuedLink(City city, double minute)
    {
        var vehicles = new List<TrafficVehicleState>();
        city.Kernel.CopyVehiclesInOrder(vehicles);
        var weak = new List<WeakReference>();
        foreach (var vehicle in vehicles)
        {
            weak.Add(new WeakReference(vehicle)); weak.Add(new WeakReference(vehicle.Route));
            foreach (var leg in vehicle.Route) weak.Add(new WeakReference(leg));
        }
        for (int link = 0; link < city.Size; link++) city.Kernel.ForecastEntry(link, minute + 0.2, 0.4, 0.6);
        return weak;
    }

    [Fact]
    public void AnEntryForecastKeepsNoCarRouteOrLegOnceItReturns()
    {
        var city = new City(17);
        for (int i = 0; i < 40; i++) city.Enqueue(3 + city.Random.Next(4));
        city.Advance(0.3);
        Assert.True(city.Kernel.VehicleCount > 20, city.Kernel.VehicleCount + " cars");
        var weak = ForecastEveryQueuedLink(city, 0.3);
        Assert.Equal(0, city.Kernel.ProjectionVehiclesHeld());
        city.Advance(10_000);
        Assert.Equal(0, city.Kernel.VehicleCount);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        int alive = 0;
        foreach (var reference in weak) if (reference.IsAlive) alive++;
        Assert.True(weak.Count > 100, weak.Count + " references");
        Assert.Equal(0, alive);
    }

    [Fact]
    public void TheCensusSeparatesLiveCarsAndTheForecastPoolFromAStrayCar()
    {
        var city = new City(17, 24);
        for (int i = 0; i < 30; i++) city.Enqueue(2 + city.Random.Next(3));
        city.Advance(0.6);
        for (int link = 0; link < city.Size; link++) city.Kernel.ForecastEntry(link, 0.8, 0.4, 0.6);
        var stray = new List<TrafficVehicleState> { new TrafficVehicleState { VehicleId = "stray", Route = { new TrafficRouteLeg { LinkId = 1, EndNominalMinutes = 1, FreeFlowMinutes = 1 } } } };
        var tracked = new[] { typeof(TrafficVehicleState), typeof(TrafficRouteLeg), typeof(List<TrafficRouteLeg>) };
        System.Text.Json.JsonElement Read(bool withStray)
        {
            var census = new RetainedCensus();
            census.TrackPerRoot(tracked);
            city.Kernel.RetainedCaches((name, cache, bound) => census.DeferCache("cache.traffic." + name, cache, bound));
            census.Add("runtime.simulation", city.Kernel);
            if (withStray) census.Add("runtime.stray", stray);
            census.AddDeferredCaches();
            using var report = System.Text.Json.JsonDocument.Parse(census.ReportJson(1, 0, null, RetainedCensus.InstrumentRoot, tracked, null, RetainedCensus.TrafficVehicles(city.Kernel)));
            return report.RootElement.GetProperty("traffic_vehicles").Clone();
        }
        var clean = Read(false);
        var vehicles = clean.GetProperty("vehicles");
        Assert.Equal(city.Kernel.VehicleCount, vehicles.GetProperty("live").GetInt64());
        Assert.True(vehicles.GetProperty("in_caches").GetInt64() > 0, clean.ToString());
        foreach (var series in new[] { "vehicles", "routes", "legs" })
            Assert.Equal(0, clean.GetProperty(series).GetProperty("outside_live_and_caches").GetInt64());
        Assert.Equal(0, clean.GetProperty("legs").GetProperty("in_caches").GetInt64());
        var leaked = Read(true);
        foreach (var series in new[] { "vehicles", "routes", "legs" })
            Assert.Equal(1, leaked.GetProperty(series).GetProperty("outside_live_and_caches").GetInt64());
    }
}
