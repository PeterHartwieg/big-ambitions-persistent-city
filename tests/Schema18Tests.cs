using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    private static void B0_Preserve(string payload, CityState replacement)
    {
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["foreign"] = "unchanged" };
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(payload).Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Same(payload, data[SaveStore.StateKey]); Assert.Equal("unchanged", data["foreign"]);
    }

    [Theory]
    [InlineData(13)] [InlineData(14)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    public void B0_AllSourceContractsPreserveEveryFactAndInitializeNoExperience(int schema)
    {
        var (city, full) = S17_Full();
        var person = city.State.Citizens[0];
        person.RecentVisits.Add(new VisitRecord { AtMinute = BitConverter.Int64BitsToDouble(0x3ff0000000000001), PlaceId = person.HomeId, Activity = Activity.Home });
        person.RecentVisits.Add(new VisitRecord { AtMinute = 123.123456789, PlaceId = "shop", Activity = Activity.Shop });
        string source = schema == 17 ? LegacyWire.Write(city.State) : S17_Old(LegacyWire.Write(city.State), schema);
        var original = LegacyPopulationCodec.Read(source); Assert.True(original.Status == SaveReadStatus.Loaded, original.Error);
        var read = SaveCodec.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(schema, read.SourceSchema); Assert.Equal(CityState.CurrentSchema, read.State!.Schema);
        Assert.Equal(LegacyWire.Write(original.State!), LegacyWire.Write(read.State));
        Assert.All(read.State.Food!.Residents, m => { Assert.Empty(m.Memory.Entries); Assert.Null(m.Memory.Watermark); });
        Assert.Empty(read.State.Food.BusinessReferences); Assert.False(read.State.Capabilities.Active);
        string next = SaveCodec.Write(read.State);
        Assert.Equal(CityState.CurrentSchema, SaveCodec.Read(next).SourceSchema);
        Assert.Equal(next, SaveCodec.Write(SaveCodec.Read(next).State!));
        for (int i = 0; i < 2; i++) Assert.Equal(BitConverter.DoubleToInt64Bits(person.RecentVisits[i].AtMinute), BitConverter.DoubleToInt64Bits(read.State.Citizens[0].RecentVisits[i].AtMinute));
        if (schema == 17) Assert.NotNull(read.State.Food.Residents.Single(m => m.Decision != null).Decision);
    }

    [Fact]
    public void B0_VisitReferencesIgnoreRootAndPlaceOrderAndPreserveLongEscapedIdsAndNegativeZero()
    {
        var (city, _) = S17_Full(); var place = city.State.Places.First(p => p.Kind == PlaceKind.Shop);
        string previous = place.Id; place.Id = new string('界', 80) + "|\"\\";
        // Keep the opaque food facts on their existing place; use a separate saved historical place.
        place.Id = previous;
        city.State.Places.Add(new Place { Id = new string('界', 80) + "|\"\\", Name = "historical", Kind = PlaceKind.Shop });
        var visits = city.State.Citizens[0].RecentVisits;
        visits.Add(new VisitRecord { AtMinute = -0.0, PlaceId = city.State.Places.Last().Id, Activity = Activity.Shop });
        visits.Add(new VisitRecord { AtMinute = Math.PI, PlaceId = city.State.Places.Last().Id, Activity = Activity.Shop });
        var wire = JObject.Parse(SaveCodec.Write(city.State));
        var places = (JArray)wire["Places"]!; var reversed = places.Reverse().Select(p => p.DeepClone()).ToArray(); wire["Places"] = new JArray(reversed);
        var reordered = new JObject(wire.Properties().Reverse().Select(p => new JProperty(p.Name, p.Value.DeepClone())));
        var read = SaveCodec.Read(reordered.ToString(Formatting.None)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(read.State!.Citizens[0].RecentVisits[0].AtMinute));
        Assert.Equal(visits.Last().PlaceId, read.State.Citizens[0].RecentVisits.Last().PlaceId);
        Assert.Equal(BitConverter.DoubleToInt64Bits(Math.PI), BitConverter.DoubleToInt64Bits(read.State.Citizens[0].RecentVisits.Last().AtMinute));
    }

    private static ConsumerMemory B0_Memory() => new ConsumerMemory { Watermark = new ExperienceWatermark { Day = 0, Meal = MealKind.Dinner, Context = 4, Stage = ExperienceStage.Served }, Entries = {
        new ShopMemory { BusinessRef = 0, Day = 0, Served = 3, Affinity = 1, Outcome = ShopExperience.Served },
        new ShopMemory { BusinessRef = 1, Day = 0, Served = 35, Affinity = 8, Outcome = ShopExperience.Served },
        new ShopMemory { BusinessRef = 2, Day = 0, Served = 0, Affinity = -8, Outcome = ShopExperience.Closed },
        new ShopMemory { BusinessRef = 19999, Day = 0, Served = 1, Affinity = 0, Outcome = ShopExperience.Queue } } };

    [Fact]
    public void B0_OutcomeShapePrecheckMatchesTheOriginalGrammarWithoutCreatingChildren()
    {
        string[] tokens = { "0", "1", "19", "2147483647", "2147483648", "01", "+1", "-1", "1.5", "1e2", "0x10", "", "界" };
        for (int field = 0; field < 8; field++)
        foreach (string token in tokens)
        {
            string[] fields = { "1", "0", "1", "0", "0", "0", "487", "487", "pastry", "historical|shop" };
            fields[field] = token; string wire = string.Join("|", fields);
            Assert.Equal(MealOutcomeRecord.TryParse(wire, out _), MealOutcomeRecord.IsCanonicalWire(wire));
        }
        Assert.False(MealOutcomeRecord.IsCanonicalWire("1|0|1|0|0|0|487|487|" + new string('p', 257) + "|shop"));
        Assert.True(MealOutcomeRecord.IsCanonicalWire("1|0|1|0|0|0|487|487|" + new string('p', 256) + "|" + new string('q', 256)));
        foreach (int affinity in new[] { -9, 9 })
        {
            var memory = B0_Memory(); memory.Entries[0].Affinity = affinity;
            Assert.Throws<FormatException>(() => MemoryWire.Encode(memory));
        }
    }

    [Theory]
    [InlineData("tuple-octal")] [InlineData("tuple-hex")] [InlineData("schema-hex")]
    [InlineData("revision-octal")] [InlineData("memory-escaped")] [InlineData("visit-escaped")]
    public void B0_NoncanonicalRawTokensRefuseBeforeTypedMaterialization(string damage)
    {
        var (city, _) = S17_Full();
        city.State.Citizens[0].RecentVisits.Add(new VisitRecord { PlaceId = city.State.Citizens[0].HomeId });
        string source = SaveCodec.Write(city.State), broken = source;
        switch (damage)
        {
            case "tuple-octal": broken = source.Replace("\"Residents\":[[1,", "\"Residents\":[[01,"); break;
            case "tuple-hex": broken = source.Replace("\"Residents\":[[1,", "\"Residents\":[[0x1,"); break;
            case "schema-hex": broken = source.Replace("\"Schema\":19,", "\"Schema\":0x13,"); break;
            case "revision-octal": broken = source.Replace("\"Revision\":1", "\"Revision\":01"); break;
            case "memory-escaped": broken = source.Replace("\"1-------\"", "\"\\u0031-------\""); break;
            case "visit-escaped": broken = source.Replace("\"RecentVisits\":[\"1", "\"RecentVisits\":[\"\\u0031"); break;
        }
        Assert.NotEqual(source, broken); B0_Preserve(broken, city.State);
    }

    [Fact]
    public void B0_FullMemoryRoundTripsCanonicallyWithinItsInclusiveBudgetAndCountsAsOne()
    {
        var (city, _) = S17_Full(); var state = city.State; var food = state.Food!;
        var meal = food.Residents[0]; string empty = SaveCodec.Write(state); var before = SaveCodec.CountRecords(state, empty);
        foreach (int slot in new[] { 0, 1, 2, 19999 }) food.BusinessReferences.Add(new BusinessReference { Slot = slot, BusinessId = "generation:" + slot, PlaceId = "shop" });
        meal.Memory = B0_Memory(); string wire = MemoryWire.Encode(meal.Memory);
        Assert.Equal(48, wire.Length); Assert.True(wire.Length + 3 <= 64); Assert.All(wire, c => Assert.InRange((int)c, 0, 127));
        Assert.Equal(wire, MemoryWire.Encode(MemoryWire.Decode(wire)));
        string json = SaveCodec.Write(state); var after = SaveCodec.CountRecords(state, json);
        Assert.Equal(before.Streaming + 4, after.Streaming); Assert.Equal(before.Semantic + 4, after.Semantic);
        var read = SaveCodec.Read(json); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(json, SaveCodec.Write(read.State!));
        // Empty extension already charges one; adding all four entries and watermark adds zero records.
        meal.Memory = new ConsumerMemory(); Assert.Equal(after, SaveCodec.CountRecords(state, SaveCodec.Write(state)));
        var threeOutcomes = meal.Today;
        meal.Today = null; var noToday = SaveCodec.CountRecords(state, SaveCodec.Write(state));
        meal.Today = new ResidentFoodToday { Day = 0 }; var emptyToday = SaveCodec.CountRecords(state, SaveCodec.Write(state));
        Assert.Equal(noToday, emptyToday); // absence, empty and saturated rollups all charge one consumer unit
        meal.Today = threeOutcomes;
        Assert.Equal(emptyToday, SaveCodec.CountRecords(state, SaveCodec.Write(state)));
    }

    [Theory]
    [InlineData("version")] [InlineData("meal-short")] [InlineData("meal-extra")] [InlineData("budget-short")] [InlineData("budget-extra")]
    [InlineData("money-fraction")] [InlineData("money-string")] [InlineData("rollup-extra")] [InlineData("rollup-version")]
    [InlineData("memory-null")] [InlineData("memory-extra")] [InlineData("memory-alphabet")] [InlineData("memory-day")]
    [InlineData("memory-ref")] [InlineData("memory-affinity")] [InlineData("memory-outcome")] [InlineData("memory-watermark")]
    [InlineData("table-extra")] [InlineData("table-place")] [InlineData("table-duplicate")] [InlineData("table-reused")]
    [InlineData("capability")] [InlineData("revision")] [InlineData("unknown")]
    [InlineData("visit-length")] [InlineData("visit-alphabet")] [InlineData("visit-nonfinite")] [InlineData("visit-place")] [InlineData("visit-activity")]
    public void B0_BadTokensSlotsReferencesAndCapabilitiesRefuseAndPreserve(string damage)
    {
        var (city, _) = S17_Full(); var state = city.State; city.EnableHouseholdFinances();
        state.Citizens[0].RecentVisits.Add(new VisitRecord { PlaceId = state.Citizens[0].HomeId });
        state.Food!.BusinessReferences.Add(new BusinessReference { BusinessId = "retained", PlaceId = "shop" });
        var root = JObject.Parse(SaveCodec.Write(state)); var meal = (JArray)root["Food"]!["Residents"]![0]!;
        var budget = (JArray)meal[3]!; var table = (JArray)root["Food"]!["BusinessReferences"]!;
        var visits = (JArray)root["Citizens"]![0]!["RecentVisits"]!;
        switch (damage)
        {
            case "version": meal[0] = 2; break;
            case "meal-short": meal.RemoveAt(35); break;
            case "meal-extra": meal.Add(0); break;
            case "budget-short": budget.RemoveAt(18); break;
            case "budget-extra": budget.Add(0); break;
            case "money-fraction": meal[1] = 0.5; break;
            case "money-string": meal[1] = "0"; break;
            case "rollup-extra": ((JArray)meal[34]!).Add("0|0|0|0|0|0|0|0||"); break;
            case "rollup-version": meal[34]![0] = 2; break;
            case "memory-null": meal[35] = JValue.CreateNull(); break;
            case "memory-extra": meal[35] = MemoryWire.Encode(B0_Memory()) + "00000003I1"; break;
            case "memory-alphabet": meal[35] = "1-------00000003i1"; break;
            case "memory-day": meal[35] = "1-------000ZZZZ3I1"; break;
            case "memory-ref": meal[35] = "1-------00100003I1"; break;
            case "memory-affinity": meal[35] = "1-------00000003Z1"; break;
            case "memory-outcome": meal[35] = "1-------00000003I5"; break;
            case "memory-watermark": meal[35] = "10000350"; break;
            case "table-extra": table[0]!.Last!.AddAfterSelf(new JValue(0)); break;
            case "table-place": table[0]![3] = "FFF"; break;
            case "table-duplicate": table.Add(table[0]!.DeepClone()); break;
            case "table-reused": table.Add(new JArray(1, 1, "retained", "000")); break;
            case "capability": root["Capabilities"]!["Code"] = "C"; break;
            case "revision": root["Capabilities"]!["Revision"] = 2; break;
            case "active": root["Capabilities"]!["Active"] = true; break;
            case "unknown": root["Extra"] = 0; break;
            case "visit-length": visits[0] = ((string)visits[0]!) + "0"; break;
            case "visit-alphabet": visits[0] = "1a00000000000000000000"; break;
            case "visit-nonfinite": visits[0] = "17FF000000000000000000"; break;
            case "visit-place": visits[0] = "10000000000000000FFF0"; break;
            case "visit-activity": visits[0] = "100000000000000000005"; break;
        }
        B0_Preserve(root.ToString(Formatting.None), state);
    }

    [Fact]
    public void B1_ActiveCapabilityExecutesAtEarlyBootstrapAndSimulationConstruction()
    {
        var (city, _) = S17_Full();
        city.State.Capabilities.Active = true;
        string active = SaveCodec.Write(city.State);
        SaveCodec.RequireExecutableCapabilities(active);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(active).Status);
        _ = new CitySimulator(city.State, new Walking());
        city.State.Capabilities.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => new CitySimulator(city.State, new Walking()));
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city.State));
    }

    [Theory]
    [InlineData(13)] [InlineData(14)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    public void B0_LegacySchemaCannotSmuggleAnInactiveOrActiveCapabilityHeader(int schema)
    {
        var (city, source) = S17_Full();
        var old = JObject.Parse(S17_Old(source, schema));
        foreach (bool active in new[] { false, true })
        {
            old["Capabilities"] = new JObject { ["Code"] = "B", ["Revision"] = 1, ["Active"] = active };
            string wire = old.ToString(Formatting.None);
            Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(wire));
            B0_Preserve(wire, city.State);
        }
    }

    [Fact]
    public void B0_FrozenReaderRefuses18AndBothContractsKeepFourCapturedOffers()
    {
        var (city, legacy) = S17_Full();
        Assert.Equal(SaveReadStatus.Future, LegacyPopulationCodec.Read(SaveCodec.Write(city.State)).Status);
        var chosen = city.State.Food!.Residents.Single(m => m.Decision != null).Decision!.Shops[0];
        while (chosen.Offers.Count < 5) chosen.Offers.Add(new FoodDecisionOffer { ProductId = "extra:" + chosen.Offers.Count });
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city.State));
        var old = JObject.Parse(legacy); var shop = old["Food"]!["Residents"]!.First(m => m["Decision"]!.Type != JTokenType.Null)!["Decision"]!["Shops"]![0]!;
        var offers = (JArray)shop["Offers"]!;
        while (offers.Count < 5) offers.Add(offers[0]!.DeepClone());
        B0_Preserve(old.ToString(Formatting.None), S17_Full().city.State);
    }

    [Fact]
    public void B0_RegularsAreUniqueRetainedConsumersWithPositiveEffectiveAffinityAndRecentServedEvidence()
    {
        var food = new FoodCommerceState { Shops = { new FoodScope { BusinessId = "own" } }, BusinessReferences = {
            new BusinessReference { Slot = 0, BusinessId = "own" }, new BusinessReference { Slot = 1, BusinessId = "rival" } } };
        foreach (var (served, affinity, day) in new[] { (3, 1, 6), (2, 1, 20), (35, 0, 20), (3, -1, 20), (3, 1, 5), (3, 1, 20) })
            food.Residents.Add(new ResidentMeal { ResidentId = "r" + food.Residents.Count, Memory = new ConsumerMemory { Entries = { new ShopMemory { BusinessRef = 0, Served = served, Affinity = affinity, Day = day } } } });
        int calls = 0;
        var index = new RegularsProjection(food, 20, (m, d) => { calls++; return m.Affinity; });
        Assert.Equal(0, calls); Assert.Equal(0, index.CountOwnShop("rival")); Assert.Equal(0, calls);
        Assert.Equal(2, index.CountOwnShop("own")); int indexed = calls;
        for (int i = 0; i < 100; i++) Assert.Equal(2, index.CountOwnShop("own")); Assert.Equal(indexed, calls);
        Assert.Equal(0, new RegularsProjection(food, 20, (m, d) => 0).CountOwnShop("own"));
        Assert.Equal(1, new RegularsProjection(food, 21, (m, d) => m.Affinity).CountOwnShop("own"));
    }
}
