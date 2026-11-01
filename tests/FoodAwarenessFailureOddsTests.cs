using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodAwarenessFailureOddsTests
{
    private sealed record Result(string[] Aware, string[] Shortlist, string Chosen, long Protected, string Memory);

    private static Result Decide(int seed, string situation, ShopExperience cause, bool failed, bool reload)
    {
        int day = situation == "repeat" ? 57 : situation is "expiry" or "eviction-expired" ? 30 : situation == "live-habit" ? 29 : situation == "expired-insertion" ? 31 : 2;
        var (bootstrap, market, paths) = WeightedFoodChoiceTests.Ready(seed, 12);
        var state = bootstrap.State;
        // Move the isolated fixture's unstarted meal to the comparison day; no history/event is replayed.
        double offset = (day - 1) * 1440;
        state.Minute += offset;
        var person = state.Citizens.Single();
        person.NextEventMinute += offset; person.ActivityUntilMinute += offset;
        person.Traits = new ResidentTraits { Bands = "2222202222" }; // curious: a remembered competitor need not be boosted
        state.Capabilities.Active = true;
        var food = state.Food!;
        food.ChoiceStartsDay = food.AwarenessStartsDay = day;
        var meal = food.Residents.Single();
        meal.Day = day; meal.DesiredAt += offset; meal.DueAt += offset; meal.ExpiresAt += offset;
        var settings = new FoodMemorySettings { FoodMemoryCapacity = 4,
            FoodMemoryWeight = situation == "eviction-zero-weight" ? 0 : 1,
            FoodMemoryServedGain = 4 };
        var store = new FoodMemoryStore(food);
        void Visit(string business, int atDay, int context, ShopExperience outcome)
        {
            var history = new ResidentMeal { ResidentId = meal.ResidentId, AnchorId = meal.AnchorId, Day = atDay,
                Kind = business == "b0" ? MealKind.Dinner : MealKind.Lunch, ContextAttempts = context, BusinessId = business,
                PlaceId = "s" + business.Substring(1), Memory = meal.Memory };
            Assert.True(store.CommitStage(history, ExperienceStage.Committed));
            Assert.True(store.Experience(history, outcome, atDay, person.Traits, settings));
        }
        bool eviction = situation.StartsWith("eviction", StringComparison.Ordinal);
        if (eviction)
        {
            int count = situation == "eviction-lowered" ? 4 : 1;
            // Curious consumers value novelty above even these positive, known competitors.
            // B1's scalar-only eviction guard therefore permits their eviction.
            for (int i = 1; i <= count; i++) Visit("b" + i, 1, i, ShopExperience.Served);
            settings.FoodMemoryCapacity = 1;
        }
        if (situation is "expiry" or "repeat" or "expired-insertion" or "live-habit") Visit("b0", 1, 1, ShopExperience.Served);
        if (situation == "live-habit")
        {
            settings.FoodMemoryDecayDaysPerPoint = 14;
            person.Traits = new ResidentTraits { Bands = "2222242222" };
            Visit("b0", 1, 2, ShopExperience.Served);
        }
        if (situation == "repeat") Visit("b0", 28, 1, cause);
        if (failed) Visit("b0", situation == "expired-insertion" ? 30 : situation is "repeat" or "eviction-expired" ? 29 : situation is "expiry" or "live-habit" ? 28 : 1,
            eviction ? 4 : 2, cause);
        string memory = JsonConvert.SerializeObject(meal.Memory.Entries);
        var awareness = new FoodAwarenessSettings { DrawsPerAnchor = situation == "eviction-lowered" ? 4 : eviction ? 1 : 0, DiscoveryProbability = .001 };
        CitySimulator Attach(CityState saved)
        {
            var city = new CitySimulator(saved, paths);
            city.AttachFoodMarket(market, memorySettings: settings, awarenessSettings: awareness);
            return city;
        }
        CityState Load(CityState saved)
        {
            var read = SaveCodec.Read(SaveCodec.Write(saved));
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            return read.State!;
        }
        var simulation = Attach(reload ? Load(state) : state);
        meal = simulation.State.Food!.Residents.Single();
        var dump = simulation.InspectFoodAwareness(meal.ResidentId)!;
        string[] aware = dump.Image.Union.Where(e => e.Eligible).Select(e => e.BusinessId).ToArray();
        Assert.InRange(aware.Length, 0, 24);
        simulation.AdvanceTo(meal.DesiredAt, 1);
        Assert.NotNull(meal.Decision);
        string[] shortlist = meal.Decision!.Shops.Select(s => s.BusinessId).ToArray();
        Assert.InRange(shortlist.Length, 0, 8);
        if (reload)
        {
            simulation = Attach(Load(simulation.State));
            meal = simulation.State.Food!.Residents.Single();
        }
        while (meal.Decision != null) simulation.AdvanceTo(meal.DesiredAt, 1);
        Assert.InRange(paths.Out + paths.Back, 0, 16);
        return new(aware, shortlist, meal.BusinessId, store.ProtectedInsertions, memory);
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (string situation in new[] { "insertion", "eviction", "eviction-zero-weight", "eviction-lowered", "eviction-expired", "expiry", "repeat", "expired-insertion", "live-habit" })
            foreach (var cause in new[] { ShopExperience.Stockout, ShopExperience.Closed, ShopExperience.Queue })
                foreach (bool reload in new[] { false, true }) yield return new object[] { situation, cause, reload };
    }

    [Theory] [MemberData(nameof(Cases))]
    public void BadVisitCannotGainAwarenessShortlistOrFinalChoice(string situation, ShopExperience cause, bool reload)
    {
        int before = 0, after = 0, gained = 0, newlyAware = 0, displaced = 0, unprotected = 0, maximumShortlist = 0;
        for (int seed = 0; seed < 128; seed++)
        {
            var control = Decide(seed, situation, cause, false, reload);
            var failure = Decide(seed, situation, cause, true, reload);
            maximumShortlist = Math.Max(maximumShortlist, Math.Max(control.Shortlist.Length, failure.Shortlist.Length));
            bool a = control.Chosen == "b0", b = failure.Chosen == "b0";
            if (a) before++; if (b) after++; if (b && !a) gained++;
            if (failure.Aware.Contains("b0") && !control.Aware.Contains("b0")) newlyAware++;
            if (b) { Assert.Contains("b0", failure.Aware); Assert.Contains("b0", failure.Shortlist); }
            if (situation.StartsWith("eviction", StringComparison.Ordinal))
            {
                if (!control.Aware.SequenceEqual(failure.Aware) || situation != "eviction-expired" && control.Memory != failure.Memory) displaced++;
                if (failure.Protected != (situation == "eviction-expired" ? 0 : 1)) unprotected++;
            }
        }
        Assert.True(gained == 0 && after <= before && newlyAware == 0,
            $"{situation}/{cause}/reload={reload}: control choices={before}, failure choices={after}, gained seeds={gained}, newly aware={newlyAware}");
        if (situation.StartsWith("eviction", StringComparison.Ordinal))
        {
            Assert.True(before > 0, "Comparison must include feasible target choices.");
            Assert.Equal(0, displaced); Assert.Equal(0, unprotected);
        }
        if (situation == "eviction-lowered") Assert.Equal(8, maximumShortlist);
        if (situation == "live-habit") Assert.True(before > 0 && after > 0, "Previously served habits, including negative ones, must remain usable before expiry.");
    }
}
