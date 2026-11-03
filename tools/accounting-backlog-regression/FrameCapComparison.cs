using System;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

internal static class FrameCapComparison
{
    private sealed class NoRoutes : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => throw new Exception("Unexpected route in midnight home fixture");
    }
    public static void Run()
    {
        var state = Population.Create(Enumerable.Range(0, 960).Select(n => new CitizenIdentity {
            Id = "cap" + n, Name = "Cap " + n, Age = 30, Persona = Persona.Foodie }).ToArray(),
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 960 } }, "frame-cap", "0.40.4", 7, 0);
        foreach (var citizen in state.Citizens) { citizen.NextEventMinute = 0; citizen.ActivityUntilMinute = 1440; }
        string initial = SaveCodec.Write(state);
        (int Frames, string Final) Drain(int citizens)
        {
            var city = new CitySimulator(SaveCodec.Read(initial).State!, new NoRoutes()); int frames = 0;
            if (!city.IsAccountingEpochDrained(0)) throw new Exception("Minute-zero work belongs to the first epoch");
            while (!city.IsCaughtUpTo(0))
            {
                frames++;
                for (int i = 0; i < citizens; i++) if (city.AdvanceFrameSlice(0).CaughtUp) break;
                city.SynchronizeSnapshot();
            }
            return (frames, SaveCodec.Write(city.State));
        }
        var before = Drain(16); var after = Drain(new PopulationFrameBudget(1.0 / 30, false).CitizenEvents);
        if (before.Final != after.Final) throw new Exception("Frame cap change altered the city");
        Console.WriteLine(JsonSerializer.Serialize(new { frameCapResidents = 960, fps = 30, beforeFrames = before.Frames, afterFrames = after.Frames,
            beforeSimulatedSeconds = before.Frames / 30.0, afterSimulatedSeconds = after.Frames / 30.0 }));
    }
}
