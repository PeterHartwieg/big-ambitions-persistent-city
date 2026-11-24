using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5CensusCacheTests
{
    private static Route Copy(Route source)
    {
        var legs = new RouteLegs(source.Legs.Count);
        foreach (var leg in source.Legs) legs.Add(new RouteLeg { From = leg.From, To = leg.To, Mode = leg.Mode, Minutes = leg.Minutes });
        return new Route { Legs = legs };
    }

    [Fact]
    public void BoundedRouteCachesAreReportedByNameAndKeptOutOfTheLeakSeries()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
        var journeys = PersonTable.Schedules(state).Where(p => p.Journey != null).Select(p => p.Journey!).ToList();
        Assert.True(journeys.Count >= 8, "travellers at 15:30: " + journeys.Count);
        int cached = journeys.Count - 1;
        var cache = new Dictionary<int, Route>();
        int exclusive = 0;
        for (int i = 0; i < cached; i++)
        {
            var route = i % 2 == 0 ? Copy(journeys[i]) : new Route { Legs = new RouteLegs(journeys[i].Legs) };
            if (i % 2 == 0) exclusive += route.Legs.Count;
            cache.Add(i, route);
        }
        var stale = new List<Route> { Copy(journeys[cached]) };
        int staleLegs = stale[0].Legs.Count;

        JsonElement Read(bool deferCache)
        {
            var census = new RetainedCensus();
            census.TrackPerRoot(new[] { typeof(RouteLeg), typeof(Route), typeof(RouteLeg[]) });
            if (deferCache) census.DeferCache("cache.routes.walking.cache", cache, 1024);
            census.Add("runtime.simulation", city);
            census.Add("runtime.cache", cache);
            census.Add("runtime.stale", stale);
            census.AddDeferredCaches();
            using var report = JsonDocument.Parse(census.ReportJson(1, 0, null, RetainedCensus.InstrumentRoot,
                new[] { typeof(RouteLeg), typeof(Route), typeof(RouteLeg[]) }, RetainedCensus.JourneyLegs(state)));
            return report.RootElement.Clone();
        }

        var deferred = Read(true);
        var legs = deferred.GetProperty("route_legs");
        Assert.Equal(exclusive, legs.GetProperty("in_caches").GetInt64());
        Assert.Equal(exclusive + staleLegs, legs.GetProperty("outside_current_journeys").GetInt64());
        Assert.Equal(staleLegs, legs.GetProperty("outside_current_journeys_and_caches").GetInt64());
        var root = deferred.GetProperty("cache_roots").EnumerateArray().Single();
        Assert.Equal("cache.routes.walking.cache", root.GetProperty("name").GetString());
        Assert.Equal(1024, root.GetProperty("bound").GetInt32());
        Assert.Equal(cached, root.GetProperty("entries").GetInt32());
        Assert.Equal(cached, root.GetProperty("routes").GetInt64());
        Assert.Equal(exclusive, root.GetProperty("route_legs").GetInt64());
        var outsideCaches = deferred.GetProperty("tracked_types_outside_caches").EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("objects").GetInt64());
        var all = deferred.GetProperty("tracked_types").EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("objects").GetInt64());
        Assert.Equal(all["RouteLeg"] - exclusive, outsideCaches["RouteLeg"]);
        Assert.Equal(all["Route"] - cached, outsideCaches["Route"]);
        var holders = deferred.GetProperty("tracked_roots").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "RouteLeg")
            .GetProperty("roots").EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("objects").GetInt64());
        Assert.Equal(exclusive, holders["cache.routes.walking.cache"]);
        Assert.Equal(staleLegs, holders["runtime.stale"]);
        Assert.Equal(legs.GetProperty("in_current_journeys").GetInt64(), holders["runtime.simulation"]);
        var arrayHolders = deferred.GetProperty("tracked_roots").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "RouteLeg[]")
            .GetProperty("roots").EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("bytes").GetInt64());
        Assert.Equal(journeys.Distinct().Count(), legs.GetProperty("leg_arrays_in_current_journeys").GetInt64());
        Assert.Equal(arrayHolders["runtime.simulation"], legs.GetProperty("leg_array_bytes_in_current_journeys").GetInt64());
        var journeyArrays = new HashSet<RouteLegs>(journeys.Select(j => j.Legs), ReferenceEqualityComparer.Instance);
        Assert.Equal(journeyArrays.Sum(a => (long)a.Capacity), legs.GetProperty("leg_array_slots_in_current_journeys").GetInt64());
        Assert.Equal(journeyArrays.Sum(a => (32 + 8L * a.Capacity + 7) / 8 * 8), legs.GetProperty("leg_array_bytes_in_current_journeys").GetInt64());
        Assert.Equal(arrayHolders["cache.routes.walking.cache"], legs.GetProperty("leg_array_bytes_in_caches").GetInt64());
        Assert.Equal(arrayHolders["runtime.stale"], legs.GetProperty("leg_array_bytes_outside_current_journeys_and_caches").GetInt64());
        Assert.Equal((32 + 8L * staleLegs + 7) / 8 * 8, arrayHolders["runtime.stale"]);
        Assert.Equal(arrayHolders.Values.Sum(), legs.GetProperty("leg_array_bytes_retained").GetInt64());

        var plain = Read(false);
        Assert.Equal(0, plain.GetProperty("route_legs").GetProperty("in_caches").GetInt64());
        Assert.Equal(deferred.GetProperty("route_legs").GetProperty("retained").GetInt64(), plain.GetProperty("route_legs").GetProperty("retained").GetInt64());
        Assert.Equal(deferred.GetProperty("bytes").GetInt64(), plain.GetProperty("bytes").GetInt64());
    }
}
