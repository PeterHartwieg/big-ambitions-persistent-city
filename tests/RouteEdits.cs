using PersistentCity.Core;

namespace PersistentCity.Tests;

/// <summary>F7c: a journey the simulator committed is immutable. A test that corrupts or rewrites a running city's
/// journey edits an uncommitted copy instead, which is the shape a journey has after it is loaded.</summary>
internal static class RouteEdits
{
    public static Route Thaw(Route route)
    {
        var copy = new Route();
        foreach (var leg in route.Legs)
            copy.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Mode = leg.Mode, Minutes = leg.Minutes, ServiceId = leg.ServiceId });
        return copy;
    }

    /// <summary>Replace the citizen's journey with an uncommitted copy and return it for editing.</summary>
    public static Route EditableJourney(CitizenState citizen) => citizen.Journey = Thaw(citizen.Journey!);
}
