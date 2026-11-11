using System.Collections.Generic;
using PersistentCity.Core;

/// <summary>The shared route-planner doubles (X9). A test that only needs "no route" or "a direct walk of N minutes" uses
/// these instead of its own copy; planners with a network, a budget or recorded calls stay with their tests. Global
/// namespace so every test file can use them, like <see cref="PerfGate"/>.</summary>
internal sealed class NoRoutes : IRoutePlanner
{
    public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
}

/// <summary>One direct walking leg of a fixed duration from door to door.</summary>
internal sealed class FixedWalk : IRoutePlanner
{
    private readonly double minutes;
    public FixedWalk(double minutes) { this.minutes = minutes; }
    public Route Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route
    { Legs = new List<RouteLeg> { new RouteLeg { From = from.Position, To = to.Position, Minutes = minutes, Mode = TravelMode.Walk } } };
}
