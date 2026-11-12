using PersistentCity.Core;

namespace PersistentCity.Tests;

internal static class TestCity
{
    internal static CityState Minimal(string key = "current-development", string rloVersion = "0.41.1", double minute = 0, string residentId = "r")
        => Population.Create(
            new[] { new CitizenIdentity { Id = residentId, Name = "Resident", Age = 35, Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } },
            key, rloVersion, 42, minute);

    internal static CityState Banded(int seed = 5150, double? runTo = null)
    {
        var (state, market) = WorkplaceCapacityTests.Fresh(3, seed);
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk()); city.AttachFoodMarket(market);
        if (runTo is double minute) while (!city.AdvanceTo(minute, 128).CaughtUp) { }
        return city.State;
    }
}
