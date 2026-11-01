using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Activity = PersistentCity.Core.Activity;

static class Program
{
    sealed class NoRoutes : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => throw new Exception("Inspector requested route");
    }
    sealed class NoMarket : IFoodMarket
    {
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double at) => throw new Exception("Inspector read market");
        public FoodSale Buy(ResidentMeal meal, double at) => throw new Exception("Inspector booked sale");
    }
    static object Measure(Action action, int batch)
    {
        var samples = new List<double>(); long allocated = 0;
        for (int round = -8; round < 80; round++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            for (int i = 0; i < batch; i++) action();
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds / batch;
            if (round < 0) continue;
            allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
            samples.Add(ms);
        }
        samples.Sort();
        return new { p50Ms = samples[40], p95Ms = samples[76], maxMs = samples[^1],
            bytesPerCall = allocated / (80.0 * batch), batchSize = batch,
            batchSpikesAtLeast30Ms = samples.Count(ms => ms * batch >= 30) };
    }

    static object Run(int residents, bool memory = false)
    {
        var places = new[] {
            new Place { Id = "address:Maple%20Street:12:home", Name = "Maple Street 12", Kind = PlaceKind.Home, Capacity = residents },
            new Place { Id = "address:Harbor%20Road:4:work", Name = "Office", Kind = PlaceKind.Work, Capacity = residents, Position = new(1, 0, 0) } };
        var state = Population.Create(Enumerable.Range(0, residents).Select(i => new CitizenIdentity {
            Id = "r" + i, Name = "Resident", Age = 30, Persona = Persona.Commuter }), places, "a5-inspector", "0.40.4", 31, 1440);
        var person = state.Citizens[0];
        person.PlaceId = person.DayPlaceId = places[1].Id; person.Activity = Activity.Work;
        person.ActivityUntilMinute = person.NextEventMinute = 2400;
        ResidentMeal? meal = null;
        if (memory)
        {
            state.Capabilities.Active = true;
            state.Places.AddRange(Enumerable.Range(0, 16).Select(i => new Place { Id = "s" + i, Name = "Bakery " + i, Kind = PlaceKind.Shop, Position = new(i + 2, 0, 0) }));
            state.Food = new() { RepresentationFactor = 2, MealRhythmVersion = 1,
                Shops = Enumerable.Range(0, 16).Select(i => new FoodScope { BusinessId = "b" + i, PlaceId = "s" + i, ProductId = FoodProducts.Croissant }).ToList() };
            var simulation = new CitySimulator(state, new NoRoutes()); simulation.AttachFoodMarket(new NoMarket());
            state.Food.AwarenessStartsDay = 1;
            for (int i = 0; i < 16; i++) state.Food.BusinessReferences.Add(new() { Slot = i, BusinessId = "b" + i, PlaceId = "s" + i });
            foreach (var consumer in state.Food.Residents)
                for (int i = 0; i < 4; i++) consumer.Memory.Entries.Add(new() { BusinessRef = i, Day = 1, Served = 3, Affinity = 8, Outcome = ShopExperience.Served });
            meal = state.Food.Residents.Single(m => m.ResidentId == person.Identity.Id);
        }
        var index = state.Places.ToDictionary(p => p.Id);
        int formats = 0; string sink = "";
        Func<string, int, string> format = (street, number) => { formats++; return number + " " + street; };
        Action capture = () => sink = ResidentInsight.Capture(state, person, meal, id => index.GetValueOrDefault(id), formatAddress: format).Text();
        string before = SaveCodec.Write(state);
        var popup = Measure(capture, 100);
        formats = 0; capture(); int formatsPerCapture = formats;
        if (SaveCodec.Write(state) != before || SaveCodec.Write(SaveCodec.Read(before).State!) != before)
            throw new Exception("Inspector changed saved state");
        return new { residents, activeMemory = memory, consumers = state.Food?.Residents.Count ?? 0,
            popupCaptureAndText = popup, formatsPerCapture, textLength = sink.Length, unchangedPayload = true };
    }

    static void Main()
    {
        var throttle = new ResidentPickThrottle(); throttle.ShouldPick(0, 0, 0);
        int picks = 0; var closed = Measure(() => { if (throttle.ShouldPick(1, 0, 0)) picks++; }, 10000);
        if (picks != 0) throw new Exception("Stationary closed cursor picked");
        var ids = Enumerable.Range(0, 48).Select(i => "r" + i).ToArray();
        string? hit = null;
        var hover = Measure(() => {
            var best = new ResidentPickAccumulator();
            for (int i = 0; i < ids.Length; i++)
                if (ResidentPickAccumulator.Hit(100, 100, 100 + i, 120, 100 + i, 60, 7, out float score)) best.Consider(ids[i], 1, score, 5);
            hit = best.Id;
        }, 1000);
        if (hit == null) throw new Exception("No synthetic hit");
        Run(10);
        Console.WriteLine(JsonSerializer.Serialize(new {
            boundary = "Offline .NET 10 Release, 80 batch means after eight warmups. Closed = pure stationary throttle; hover = scoring 48 preprojected walkers. Unity projection, guards, GUI draw and native formatter excluded. Open = Core capture/text, with absent food or saturated four-entry B memory on two consumers/resident and 16 shops; C2a active header but inspector never samples awareness. Batch flags are not mod-work frames.",
            closedStationary = closed, hoverPicking = hover, closedPicks = picks,
            populations = new[] { Run(957), Run(5000), Run(957, true), Run(5000, true) }, nativeModWorkGate = "unverified on this head; requires matched 6 Nov windows" }));
    }
}
