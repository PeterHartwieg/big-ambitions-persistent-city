using PersistentCity.Core;

namespace PersistentCity.Tests;

/// <summary>The shared test city builder (X9). <see cref="Minimal"/> is the one-resident, one-home current-schema city
/// the save, session and epoch tests start from; <see cref="Banded"/> is the 3-factor banded city with a food market
/// attached that the ledger and change-journal goldens pin. Larger fixtures (the F5h shape, the 957-resident economy
/// city, the movement fixture) stay with their owners and are built on these types.</summary>
internal static class TestCity
{
    internal static CityState Minimal(string key = "current-development", string rloVersion = "0.41.1", double minute = 0, string residentId = "r")
        => Population.Create(
            new[] { new CitizenIdentity { Id = residentId, Name = "Resident", Age = 35, Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } },
            key, rloVersion, 42, minute);

    /// <summary>WorkplaceCapacityTests' banded city at factor 3 with its market attached, run to <paramref name="runTo"/>
    /// in 128-event slices when given.</summary>
    internal static CityState Banded(int seed = 5150, double? runTo = null)
    {
        var (state, market) = WorkplaceCapacityTests.Fresh(3, seed);
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk()); city.AttachFoodMarket(market);
        if (runTo is double minute) while (!city.AdvanceTo(minute, 128).CaughtUp) { }
        return city.State;
    }
}
