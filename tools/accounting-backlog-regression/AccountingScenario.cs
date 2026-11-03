using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

// Detached native-clock/market double. Executes the real simulator, held decisions,
// wallets and save codec. No Unity, Harmony, native stock or presented frames.
public static class AccountingScenario
{
    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences preferences) => new Route {
            Legs = { new RouteLeg { From = from.Position, To = to.Position, Minutes = 1, Mode = TravelMode.Walk } } };
    }
    private sealed class Market : IFoodMarket, IFoodSaleEvidence
    {
        public double Clock;
        public double EarliestMinute => Math.Floor(Clock / 60) * 60;
        public readonly List<string> Orders = new List<string>();
        private readonly HashSet<string> paid = new HashSet<string>(StringComparer.Ordinal);
        private Money Price => Money.FromCents((long)Math.Floor(Clock) % 120 < 60 ? 100 : 120);
        public IReadOnlyList<FoodBusiness> Read(double minute) => new[] { new FoodBusiness {
            Id = "b", PlaceId = "shop", Name = "Bakery", MealsPerHour = 240,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new List<FoodOffer> { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Price, Units = Math.Max(0, 10000 - paid.Count) } }
        } };
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            Require(at >= EarliestMinute && at < EarliestMinute + 60, "historical native checkout");
            if (meal.Price != Price) return new FoodSale(false, "Timestamped price changed");
            Require(paid.Add(meal.IntentId), "duplicate native order");
            Orders.Add(meal.IntentId + "@" + at.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + meal.Price.Units);
            if (Orders.Count % 31 == 0) throw new InvalidOperationException("Lost native acknowledgement");
            return new FoodSale(true, "Native double confirmed");
        }
        public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
    }
    public sealed class Result
    {
        public string Snapshot = "";
        public string[] Orders = Array.Empty<string>();
        public long Frames, Units, Held, Spikes;
        public double MaxLag, FrameP50, FrameP95, FrameMax, RecoverySeconds, Seconds;
        public int Reloads;
        public long Purchases;
        public long SpentUnits;
        public long BalanceUnits, HeldUnits;
        public int MissedMeals;
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static Result Run(int residents, int fps, int eventBudget, double milliseconds,
        bool reload = false, int stallFrames = 0, bool skip = false, bool coordinated = true)
    {
        var state = Population.Create(Enumerable.Range(0, residents).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Persona = Persona.Foodie, Age = 30 }).ToArray(), new[] {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = residents, Position = new Position(1,0,0) },
            new Place { Id = "shop", Name = "Bakery", Kind = PlaceKind.Shop, Position = new Position(2,0,0) }
        }, "accounting-backlog", "0.40.4", 7, 1440);
        foreach (var person in state.Citizens)
        {
            person.Phase = CitizenPhase.AtPlace; person.PlaceId = "home"; person.Activity = Activity.Home;
            person.NextEventMinute = person.ActivityUntilMinute = 4320;
        }
        state.Food = new FoodCommerceState { RepresentationFactor = 2, MealRhythmVersion = MealRhythm.PolicyVersion,
            Shops = new List<FoodScope> { new FoodScope { PlaceId = "shop", BusinessId = "b", ProductId = FoodProducts.Croissant } } };
        var market = new Market { Clock = state.Minute }; var routes = new Walking();
        var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        // Exercise the current held-context policy throughout this fixture day, rather
        // than measuring only the legacy day that normally precedes its activation.
        state.Food.ChoiceStartsDay = state.Food.AwarenessStartsDay = 1;
        var result = new Result(); var costs = new List<double>(); var timer = Stopwatch.StartNew();
        int hour = 24; float minute = 0;
        int stalled = 0; bool injected = false, recovered = false, firstReload = false, midnightReload = false;
        long recoveryStart = 0;
        const double end = 2880;
        while (!city.IsCaughtUpTo(end))
        {
            Require(++result.Frames < 2000000, "nonterminating accounting recovery");
            double native = hour * 60.0 + minute;
            double target = coordinated ? AccountingClock.Target(native) : native;
            long started = Stopwatch.GetTimestamp();
            long saveTicks = 0;
            if (coordinated) Require(AccountingClock.Compatible(city.State.Minute, native), "input epoch escaped");
            bool ready = city.IsCaughtUpTo(target);
            float requested = (float)Math.Min(skip ? 120 : 30.0 / fps, end - native);
            float allowed = coordinated ? AccountingClock.Limit(ready, native, requested) : requested;
            if (allowed == 0 && requested > 0) result.Held++;
            minute += allowed;
            while (minute >= 60) { minute -= 60; hour++; }
            native = market.Clock = hour * 60.0 + minute;
            target = Math.Max(city.State.Minute, coordinated ? AccountingClock.Target(native) : native);
            if (reload && !midnightReload && native == end)
            {
                long saveStarted = Stopwatch.GetTimestamp();
                midnightReload = true;
                string before = SaveCodec.Write(city.State); var read = SaveCodec.Read(before);
                Require(read.Status == SaveReadStatus.Loaded && SaveCodec.Write(read.State!) == before, "midnight in-flight writer-25 round trip");
                city = new CitySimulator(read.State!, routes); city.AttachFoodMarket(market); result.Reloads++;
                saveTicks = Stopwatch.GetTimestamp() - saveStarted;
            }
            result.MaxLag = Math.Max(result.MaxLag, target - city.State.Minute);
            if (!injected && native >= 1800 && stallFrames > 0) { injected = true; recoveryStart = result.Frames; }
            bool stall = injected && stalled < stallFrames;
            if (stall) stalled++;
            if (!stall)
            {
                for (int unit = 0; unit < eventBudget; unit++)
                {
                    var advanced = city.AdvanceFrameSlice(target);
                    result.Units += advanced.Events;
                    if (advanced.CaughtUp || (Stopwatch.GetTimestamp() - started - saveTicks) * 1000.0 / Stopwatch.Frequency >= milliseconds) break;
                }
                city.SynchronizeSnapshot();
            }
            if (injected && !stall && !recovered && city.IsCaughtUpTo(target))
            { result.RecoverySeconds = (result.Frames - recoveryStart) / (double)fps; recovered = true; }
            double cost = (Stopwatch.GetTimestamp() - started - saveTicks) * 1000.0 / Stopwatch.Frequency;
            costs.Add(cost); if (cost >= 30) result.Spikes++;
            if (reload && !firstReload && city.State.Food!.Residents.Any(m => m.Decision != null || m.Status == MealStatus.Travelling))
            {
                firstReload = true;
                string before = SaveCodec.Write(city.State); var read = SaveCodec.Read(before);
                Require(read.Status == SaveReadStatus.Loaded && SaveCodec.Write(read.State!) == before, "in-flight writer-25 round trip");
                city = new CitySimulator(read.State!, routes); city.AttachFoodMarket(market); result.Reloads++;
            }
        }
        Require(!coordinated || city.HistoricalClockMisses == 0, "unreported workload-caused lost demand");
        result.Seconds = timer.Elapsed.TotalSeconds;
        result.Snapshot = SaveCodec.Write(city.State); result.Orders = market.Orders.ToArray();
        result.Purchases = city.State.Food!.Residents.Sum(m => m.Purchases);
        result.SpentUnits = city.State.Food.Residents.Sum(m => m.Spent.Units);
        result.BalanceUnits = city.State.Food.Residents.Sum(m => m.Budget!.Balance.Units);
        result.HeldUnits = city.State.Food.Residents.Sum(m => m.Budget!.HeldPurchase.Units);
        result.MissedMeals = city.State.Food.Residents.Sum(m => m.Today?.Meals.Count(o => o.Status == MealOutcomeStatus.Skipped) ?? 0);
        costs.Sort(); result.FrameP50 = costs[costs.Count / 2]; result.FrameP95 = costs[(int)((costs.Count - 1) * .95)]; result.FrameMax = costs[costs.Count - 1];
        return result;
    }
}
