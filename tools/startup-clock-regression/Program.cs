using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private sealed class Routes : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
            => throw new Exception("No route request should precede the retained future action.");
    }
    private sealed class EmptyMarket : IFoodMarket
    {
        public double EarliestMinute => 0;
        public System.Collections.Generic.IReadOnlyList<FoodBusiness> Read(double minute) => Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal meal, double minute) => throw new Exception("No checkout in startup fixture.");
    }
    private static CityState SavedCity()
    {
        var state = Population.Create(new[] {
            new CitizenIdentity { Id = "worker", Name = "Worker", Age = 35, Persona = Persona.Commuter },
            new CitizenIdentity { Id = "traveller", Name = "Traveller", Age = 35, Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 2, Position = new Position(120, 0, 0) } },
            "startup-clock-test", "test", 17, 0);
        state.Minute = 1440; state.TravelClockRate = 1.0 / 3;
        var worker = state.Citizens.Single(p => p.Identity.Id == "worker");
        worker.NextEventMinute = 1600; worker.ActivityUntilMinute = 1700;
        worker.Preferences.OwnsCar = true;
        worker.Car = new CarState { Id = "worker:car", ParkedPosition = new Position(3, 0, 0), ModelId = "saved", PaintSeed = 12 };
        worker.CommutePreparation = new CommutePreparation { PlaceId = "work", PreparedAt = 1400,
            TravelMinutes = 20, WorkStartsAt = 1700, WorkEndsAt = 1800 };
        var traveller = state.Citizens.Single(p => p.Identity.Id == "traveller");
        traveller.Phase = CitizenPhase.Travelling; traveller.PlaceId = "home";
        traveller.DestinationId = "work"; traveller.DestinationActivity = Activity.Work;
        traveller.DepartureMinute = 1400; traveller.NextEventMinute = 1460; traveller.ActivityUntilMinute = 1800;
        traveller.Journey = new Route { Legs = new System.Collections.Generic.List<RouteLeg> {
            new RouteLeg { From = new Position(0, 0, 0), To = new Position(120, 0, 0), Mode = TravelMode.Walk, Minutes = 60 } } };
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion,
            HouseholdFinanceVersion = HouseholdFinance.PolicyVersion };
        new CitySimulator(state, new Routes()).AttachFoodMarket(new EmptyMarket());
        return SaveCodec.Read(SaveCodec.Write(state)).State!;
    }
    private static void Main(string[] args)
    {
        InteriorViewRegression.Run();
        NativeCityClock.Active = false; GameManager.SetMultiplier(1); NativeTravelPace.ResetReading();
        var tracker = (ClockRateTracker)typeof(NativeTravelPace).GetField("tracker", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        for (int i = 0; i < 4; i++) tracker.Observe(i, 1, 1);
        Check(NativeTravelPace.ClockRate == 1, "fixture did not reproduce inactive-policy observation");
        double initial = NativeTravelPace.InitialClockRate(1.0 / 3);
        Check(initial == 1.0 / 3, "fresh configured rate used inactive observer rate");
        var loaded = SavedCity(); string before = SaveCodec.Write(loaded);
        NativeTravelPace.PrepareInitialTiming(loaded, false, initial);
        Check(SaveCodec.Write(loaded) == before, "pre-activation reload changed supported saved state");
        NativeCityClock.Active = true; NativeCityClock.Factor = 1.0 / 3; NativeTravelPace.ResetReading();
        Check(NativeTravelPace.ClockRate == loaded.TravelClockRate, "same configured policy requires another retiming");
        var worker = loaded.Citizens.Single(p => p.Identity.Id == "worker");
        var journey = loaded.Citizens.Single(p => p.Identity.Id == "traveller").Journey;
        var simulation = new CitySimulator(loaded, new Routes());
        simulation.AttachFoodMarket(new EmptyMarket());
        simulation.AdvanceTo(1440 + 1.0 / 3);
        Check(worker.NextEventMinute == 1600 && worker.LastEmploymentCheckDay == 0 && worker.JobChanges == 0,
            "same-policy reload woke future employment check");
        Check(worker.CommutePreparation.PlaceId == "work" && worker.Car!.ParkedPosition.X == 3,
            "same-policy reload lost saved commute preparation or parking");
        Check(ReferenceEquals(journey, loaded.Citizens.Single(p => p.Identity.Id == "traveller").Journey)
            && journey!.Minutes == 60, "same-policy reload retimed in-flight route");

        var changed = SavedCity(); before = SaveCodec.Write(changed);
        NativeCityClock.Active = false; NativeTravelPace.PrepareInitialTiming(changed, false, .5);
        Check(SaveCodec.Write(changed) == before, "genuine changed policy applied before startup policy activation");
        NativeCityClock.Active = true; NativeCityClock.Factor = .5; NativeTravelPace.ResetReading();
        Check(TravelTiming.Apply(changed, 2, NativeTravelPace.ClockRate), "true activated policy change was ignored");
        Check(changed.TravelClockRate == .5 && changed.Citizens.Single(p => p.Identity.Id == "worker").NextEventMinute == changed.Minute,
            "genuine rate change no longer wakes safe home routine");
        Check(changed.Citizens.Single(p => p.Identity.Id == "traveller").Journey!.Minutes == 70,
            "genuine rate change did not retime only remaining in-flight movement");
        Check(!TravelTiming.Apply(changed, 2, NativeTravelPace.ClockRate), "unchanged policy repeated retiming");

        var fresh = SavedCity(); fresh.TravelClockRate = 1; fresh.Food = null;
        NativeCityClock.Active = false; NativeTravelPace.PrepareInitialTiming(fresh, true, initial);
        Check(fresh.TravelClockRate == initial, "fresh population did not use configured initial rate");
        Check(fresh.Food == null, "fresh timing preparation unexpectedly initialized household economy");
        var missingFood = SavedCity(); missingFood.Food = null; before = SaveCodec.Write(missingFood);
        Check(SaveCodec.Read(before).Status == SaveReadStatus.Loaded, "pure food-less diagnostic codec contract changed");
        bool missingFoodRefused = false;
        try { NativeTravelPace.PrepareInitialTiming(missingFood, false, initial); }
        catch (InvalidOperationException) { missingFoodRefused = true; }
        Check(missingFoodRefused && SaveCodec.Write(missingFood) == before,
            "loaded population without household economy was accepted or changed");
        var unsupported = SavedCity(); unsupported.TravelTimingVersion = 0; before = SaveCodec.Write(unsupported);
        bool refused = false;
        try { NativeTravelPace.PrepareInitialTiming(unsupported, false, initial); } catch (InvalidOperationException) { refused = true; }
        Check(refused && SaveCodec.Write(unsupported) == before, "unsupported saved timing was migrated or overwritten");
        if (args.Length > 0)
        {
            var read = SaveCodec.Read(File.ReadAllText(args[0]));
            Check(read.Status == SaveReadStatus.Loaded, "native checkpoint did not validate");
            before = SaveCodec.Write(read.State!);
            NativeTravelPace.PrepareInitialTiming(read.State!, false, initial);
            Check(SaveCodec.Write(read.State!) == before, "native saved checkpoint changed during startup timing");
        }
        Console.WriteLine("PASS " + checks + " actual startup-clock source checks; native clock/UI doubles, no Unity execution.");
    }
}
