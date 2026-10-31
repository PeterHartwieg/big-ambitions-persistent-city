using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

// Readiness evidence for the published B0 contract, not a B1 implementation or acceptance test.
// Fails if its counterexample stops reproducing, so a prerequisite fix cannot silently leave stale evidence.
sealed class Paths : IRoutePlanner
{
    public int Calls;
    public Route Plan(Place a, Place b, double at, TravelPreferences preferences)
    {
        Calls++;
        return new Route { Legs = new() { new RouteLeg { From = a.Position, To = b.Position,
            Mode = TravelMode.Walk, Minutes = 1 } } };
    }
}

sealed class Market : IFoodMarket
{
    public double EarliestMinute => 0;
    public int Orders;
    public IReadOnlyList<FoodBusiness> Read(double at) => new[] { new FoodBusiness {
        Id = "generation:1", PlaceId = "shop", Name = "Shop", MealsPerHour = 60,
        Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
        Offers = new() {
            new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 100 },
            new FoodOffer { ProductId = FoodProducts.Burger, Price = Money.FromCents(100), Units = 100 } } } };
    public FoodSale Buy(ResidentMeal meal, double at)
    { Orders++; throw new InvalidOperationException("Contract probe must stop before checkout."); }
}

static class Program
{
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    static (CitySimulator city, Market market, Paths paths) Ready(int seed)
    {
        var places = new List<Place> {
            new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 },
            new() { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Position = new(10, 0, 0) } };
        var state = Population.Create(new[] { new CitizenIdentity { Id = "resident", Name = "Resident",
            Age = 30, Persona = Persona.Foodie } }, places, "b1-contract-probe", "0.40.4", seed, 1440);
        var resident = state.Citizens.Single();
        resident.NextEventMinute = resident.ActivityUntilMinute = 2870;
        resident.Traits = new ResidentTraits { Bands = "4022222222" };
        state.Food = new FoodCommerceState { Shops = new() { new FoodScope { PlaceId = "shop",
            BusinessId = "generation:1", ProductId = FoodProducts.Croissant,
            AdditionalProductIds = new() { FoodProducts.Burger } } } };
        var market = new Market(); var paths = new Paths(); var city = new CitySimulator(state, paths);
        city.AttachFoodMarket(market, new FoodChoiceSettings { FoodChoiceTasteWeight = 0 });
        state.Food.ChoiceStartsDay = 1;
        return (city, market, paths);
    }

    static (string product, int routes, int orders) Resume(string checkpoint, double tasteWeight)
    {
        var loaded = SaveCodec.Read(checkpoint);
        Require(loaded.Status == SaveReadStatus.Loaded, loaded.Error ?? "Checkpoint refused.");
        var paths = new Paths(); var market = new Market();
        var city = new CitySimulator(loaded.State!, paths);
        city.AttachFoodMarket(market, new FoodChoiceSettings { FoodChoiceTasteWeight = tasteWeight });
        var meal = city.State.Food!.Residents.Single();
        while (!city.AdvanceFrameSlice(meal.DesiredAt, 1).CaughtUp) { }
        Require(meal.Status == MealStatus.Travelling, "Probe did not commit its selected visit.");
        Require(market.Orders == 0, "Probe entered checkout.");
        return (meal.ProductId, paths.Calls, market.Orders);
    }

    static void Main()
    {
        object? mismatch = null; string? example = null;
        for (int seed = 0; seed < 128; seed++)
        {
            var (city, market, paths) = Ready(seed);
            var meal = city.State.Food!.Residents.Single();
            city.AdvanceFrameSlice(meal.DesiredAt, 1);
            Require(meal.Decision?.Result == FoodDecisionResult.Evaluating, "Missing held evaluating checkpoint.");
            Require(meal.Decision!.QuotesDone == 0 && paths.Calls == 0 && market.Orders == 0, "Checkpoint already progressed.");
            string checkpoint = SaveCodec.Write(city.State);
            var unchanged = Resume(checkpoint, 0); var repeated = Resume(checkpoint, 0); var edited = Resume(checkpoint, 3);
            Require(unchanged == repeated, "Unchanged-config control does not replay.");
            Require(unchanged.routes == edited.routes, "Config counterexample changed route counts.");
            if (unchanged.product != edited.product)
            {
                example = checkpoint;
                mismatch = new { seed, captureTasteWeight = 0, resumeTasteWeight = 3,
                    unchangedProduct = unchanged.product, editedProduct = edited.product,
                    routesPerResume = unchanged.routes, nativeCheckoutCalls = unchanged.orders,
                    unchangedConfigReplayMatches = true };
                break;
            }
        }
        Require(example != null, "No held-context config replay counterexample found.");

        object Refusal(string name, Action<JObject> edit)
        {
            var root = JObject.Parse(example!); edit(root);
            string wire = root.ToString(Newtonsoft.Json.Formatting.None);
            var data = new Dictionary<string, string> { [SaveStore.StateKey] = wire };
            var session = SaveSession.Open(data, "b1-contract-probe");
            Require(session.InitialReadResult.Status != SaveReadStatus.Loaded, "Unexpected schema-18 extension acceptance: " + name);
            Require(ReferenceEquals(wire, data[SaveStore.StateKey]), "Probe replaced refused source.");
            return new { name, status = session.InitialReadResult.Status.ToString(), error = session.InitialReadResult.Error,
                sourceReferencePreserved = true };
        }
        var extensionRefusals = new[] {
            Refusal("held memory/config input", root => ((JArray)root["Food"]!["Residents"]![0]!)[9]!["MemoryInputs"] = new JObject()),
            Refusal("B activation day", root => root["Capabilities"]!["StartsDay"] = 2) };

        Console.WriteLine(JsonSerializer.Serialize(new { baseline = "192037b", schema = CityState.CurrentSchema,
            b1Implemented = false, heldContextCounterexample = mismatch, extensionRefusals },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
