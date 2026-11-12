using PersistentCity.Core;

namespace PersistentCity.Tests;

internal static class RouteEdits
{
    public static Route Thaw(Route route)
    {
        var copy = new Route();
        foreach (var leg in route.Legs)
            copy.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Mode = leg.Mode, Minutes = leg.Minutes, ServiceId = leg.ServiceId });
        return copy;
    }

    public static Route EditableJourney(CitizenState citizen) => citizen.Journey = Thaw(citizen.Journey!);
}
