using System.Collections.Generic;
using PersistentCity.Core;

internal sealed class NoRoutes : IRoutePlanner
{
    public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
}

internal sealed class FixedWalk : IRoutePlanner
{
    private readonly double minutes;
    public FixedWalk(double minutes) { this.minutes = minutes; }
    public Route Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route
    { Legs = new List<RouteLeg> { new RouteLeg { From = from.Position, To = to.Position, Minutes = minutes, Mode = TravelMode.Walk } } };
}
