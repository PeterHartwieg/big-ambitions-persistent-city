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
    internal static string E5d_Wire24(CityState state)
    {
        int schema = state.Schema; string policy = state.MenuPolicy;
        try { state.Schema = 24; state.MenuPolicy = FoodMenuPolicy.Wire; return PopulationSchema24.Write(state); }
        finally { state.Schema = schema; state.MenuPolicy = policy; }
    }
    static JObject E5d_Offer(JObject root) => (JObject)root["Food"]!["Residents"]!
        .Select(m => m[9]).First(d => d?.Type == JTokenType.Object)!["Shops"]![0]!["Offers"]![0]!;
    static void E5d_Refused(string source, CityState replacement)
    {
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(source).Status);
        var data = new Dictionary<string, string> { [SaveCodec.StateKey] = source };
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Same(source, data[SaveCodec.StateKey]);
    }
    static FoodShopDay E5d_Row(string product, string business = "history") => new()
    {
        Day = 0, PlaceId = "shop", BusinessId = business, Purchases = 1, Revenue = Money.FromCents(137),
        PurchaseHours = new long[] { 1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 },
        PurchaseMeals = new long[] { 1,0,0,0 }, BuyerDistricts = { new FoodBuyerDistrict { DistrictId = "a", Purchases = 1 } },
        ProductSales = { new FoodProductSales { ProductId = product, Purchases = 1, Revenue = Money.FromCents(137), PurchaseMeals = new long[] { 1,0,0,0 } } }
    };

    [Fact]
    public void E5d_Exact24MigrationKeepsEveryOriginalFieldAndOpaqueObjectAndRefusesDowngrade()
    {
        var (city, _) = S17_Full();
        foreach (string name in new[] { "foodtrucks:itemname_dumplings", "foodtrucks:itemname_choppedcheese", new string('x', 512) })
            city.State.Food!.History.Add(E5d_Row(name, "past:" + city.State.Food!.History.Count));
        string source = E5d_Wire24(city.State);
        var read = SaveCodec.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(24, read.SourceSchema); Assert.Equal(25, read.State!.Schema);
        Assert.Equal((int)(city.State.Minute / 1440) + 1, read.State.NamePolicyStartsDay);
        Assert.Equal(source, E5d_Wire24(read.State)); // all identities, money, routes, traits, histories and latches
        Assert.All(read.State.Food!.History.SelectMany(r => r.ProductSales), c => Assert.True(c.OpaqueIdentity));
        string current = SaveCodec.Write(read.State);
        Assert.Equal(current, SaveCodec.Write(SaveCodec.Read(current).State!));
        Assert.Equal(SaveReadStatus.Future, PopulationSchema24.Read(current).Status);
        Assert.Throws<SaveValidationException>(() => PopulationSchema24.RequireExecutableCapabilities(current));
        SaveCodec.RequireExecutableCapabilities(current);
    }

    [Fact]
    public void E5d_OpaqueHistoryDoesNotConsumeTableButIncompatiblePredecessorRefusesInsteadOfRepairing()
    {
        var (city, _) = S17_Full();
        city.State.Food!.Shops[0].ProductId = new string('x', 256);
        string valid = E5d_Wire24(city.State);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(valid).Status);
        // Valid schema-24 historical objects do not consume new table entries, even at 512 characters.
        for (int i = 0; i < 260; i++) city.State.Food.History.Add(E5d_Row("opaque:" + i, "generation:" + i));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(E5d_Wire24(city.State)).Status);
        // Old scopes may carry opaque product text that the new canonical live/table contract refuses.
        city.State.Food.Shops[0].ProductId = "old|opaque";
        string incompatible = E5d_Wire24(city.State);
        Assert.Equal(SaveReadStatus.Loaded, PopulationSchema24.Read(incompatible).Status);
        E5d_Refused(incompatible, S17_Full().city.State);
    }

    [Theory]
    [InlineData("missing-table")] [InlineData("null-table")] [InlineData("duplicate-name")]
    [InlineData("table-order")] [InlineData("unused-name")] [InlineData("257-names")]
    [InlineData("long-name")] [InlineData("control")] [InlineData("delimiter")]
    [InlineData("missing-metadata")] [InlineData("null-metadata")] [InlineData("metadata-object")]
    [InlineData("metadata-short")] [InlineData("metadata-long")] [InlineData("metadata-future")]
    [InlineData("metadata-class")] [InlineData("metadata-taste")] [InlineData("metadata-fit")]
    [InlineData("metadata-old-policy")] [InlineData("missing-start")] [InlineData("future-start")]
    public void E5d_BadNewShapesAndSemanticsPreserveExactDictionaryReference(string damage)
    {
        var (city, _) = S17_Full(); var root = JObject.Parse(SaveCodec.Write(city.State));
        var names = (JArray)root["ProductNames"]!; var offer = E5d_Offer(root);
        switch (damage)
        {
            case "missing-table": root.Remove("ProductNames"); break;
            case "null-table": root["ProductNames"] = null; break;
            case "duplicate-name": names.Add(names[0]!.DeepClone()); break;
            case "table-order": root["ProductNames"] = new JArray(names.Reverse()); break;
            case "unused-name": names.Add("zz:unused"); break;
            case "257-names": root["ProductNames"] = new JArray(Enumerable.Range(0, 257).Select(i => "p:" + i.ToString("D3"))); break;
            case "long-name": names[0] = new string('x', 257); break;
            case "control": names[0] = "bad\u0001name"; break;
            case "delimiter": names[0] = "bad|name"; break;
            case "missing-metadata": offer.Remove("Metadata"); break;
            case "null-metadata": offer["Metadata"] = null; break;
            case "metadata-object": offer["Metadata"] = new JObject(); break;
            case "metadata-short": ((JArray)offer["Metadata"]!).RemoveAt(5); break;
            case "metadata-long": ((JArray)offer["Metadata"]!).Add(0); break;
            case "metadata-future": offer["Metadata"]![0] = 3; break;
            case "metadata-class": offer["Metadata"]![1] = 5; break;
            case "metadata-taste": offer["Metadata"]![2] = -1; break;
            case "metadata-fit": offer["Metadata"]![3] = 5001; break;
            case "metadata-old-policy": offer["Metadata"]![3] = 4999; break;
            case "missing-start": root.Remove("NamePolicyStartsDay"); break;
            case "future-start": root["NamePolicyStartsDay"] = 999; break;
        }
        E5d_Refused(root.ToString(Formatting.None), city.State);
    }

    [Theory]
    [InlineData("foodtrucks:itemname_dumplings")]
    [InlineData("foodtrucks:itemname_choppedcheese")]
    [InlineData("a:\"\\雪😀")]
    public void E5d_NameReferenceCellsAreOrdinalAndDoNotRemapWhenDefinitionsAreAbsentOrReturn(string name)
    {
        var (city, _) = S17_Full(); city.State.Food!.History.Add(E5d_Row(name));
        string first = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(first); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(name, read.State!.Food!.History.Single().ProductSales.Single().ProductId);
        // No loaded catalogue API exists in the codec. A removed mod is still an exact historical fact.
        Assert.Equal(first, SaveCodec.Write(read.State));
        read.State.Food.History.Add(E5d_Row("A:new-before-existing", "other-generation"));
        string changed = SaveCodec.Write(read.State);
        var next = SaveCodec.Read(changed); Assert.True(next.Status == SaveReadStatus.Loaded, next.Error);
        Assert.Equal(name, next.State!.Food!.History.First().ProductSales.Single().ProductId);
        next.State.Food.History.RemoveAt(1);
        Assert.Equal(first, SaveCodec.Write(next.State));
        var table = JObject.Parse(changed)["ProductNames"]!.Values<string>().ToArray();
        Assert.Equal(table.OrderBy(n => n, StringComparer.Ordinal), table);
    }

    [Theory]
    [InlineData(0)] [InlineData(257)] [InlineData(256)] [InlineData(-1)]
    public void E5d_DanglingProductReferencesCannotFallBackToFixedE4Codes(int reference)
    {
        var (city, _) = S17_Full(); city.State.Food!.History.Add(E5d_Row("foodtrucks:itemname_dumplings"));
        var root = JObject.Parse(SaveCodec.Write(city.State));
        root["Food"]!["History"]![0]!["ProductSales"]![0]![0] = reference;
        E5d_Refused(root.ToString(Formatting.None), city.State);
    }

    [Fact]
    public void E5d_CountsChargeTableAndEachHeldTupleOnceAndEveryHistoricalCellOnce()
    {
        var (city, _) = S17_Full(); city.State.Food!.History.Add(E5d_Row(FoodProducts.Burger));
        string before = E5d_Wire24(city.State);
        var oldState = PopulationSchema24.Read(before).State!;
        var old = PopulationSchema24.CountRecordsForSchema(oldState, before, 24);
        string wire = SaveCodec.Write(city.State);
        var current = SaveCodec.CountRecords(city.State, wire);
        int added = city.State.ProductNames.Count + city.State.Food.Residents.Where(m => m.Decision != null)
            .Sum(m => m.Decision!.Shops.Sum(s => s.Offers.Count));
        Assert.Equal(old.Streaming + added, current.Streaming);
        Assert.Equal(old.Semantic + added, current.Semantic);
        Assert.True(current.Streaming >= current.Semantic);
    }

    [Fact]
    public void E5d_256NamesFitButValid24ScopesRequiring257RefuseMigrationAtomically()
    {
        var (city, _) = S17_Full();
        city.State.Food!.Residents.ForEach(m => { m.Decision = null; m.Today = null; m.PreferredProductId = ""; m.PreferenceBonusCents = 0; });
        city.State.Food.Shops.Clear();
        for (int i = 0; i < 32; i++)
        {
            city.State.Places.Add(new Place { Id = "named-shop:" + i, Name = "Shop", Kind = PlaceKind.Shop });
            city.State.Food.Shops.Add(new FoodScope { PlaceId = "named-shop:" + i, BusinessId = "named-business:" + i,
                ProductId = "product:" + (i * 8).ToString("D3"),
                AdditionalProductIds = Enumerable.Range(i * 8 + 1, 7).Select(n => "product:" + n.ToString("D3")).ToList() });
        }
        string before = E5d_Wire24(city.State);
        Assert.Equal(SaveReadStatus.Loaded, PopulationSchema24.Read(before).Status);
        var read = SaveCodec.Read(before); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(256, read.State!.ProductNames.Count);
        Assert.Equal(before, E5d_Wire24(read.State));
        city.State.Places.Add(new Place { Id = "named-shop:32", Name = "Shop", Kind = PlaceKind.Shop });
        city.State.Food.Shops.Add(new FoodScope { PlaceId = "named-shop:32", BusinessId = "named-business:32", ProductId = "product:256" });
        string incompatible = E5d_Wire24(city.State);
        Assert.Equal(SaveReadStatus.Loaded, PopulationSchema24.Read(incompatible).Status);
        Assert.Contains("more than 256", SaveCodec.Read(incompatible).Error);
        E5d_Refused(incompatible, read.State);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city.State));
    }

    [Theory]
    [InlineData("\\ud800")] [InlineData("\\udfff")] [InlineData("\\ud800x")]
    public void E5d_InvalidEscapedSurrogatesCannotBeRemappedToReplacementCharacters(string escape)
    {
        var (city, _) = S17_Full(); string source = SaveCodec.Write(city.State);
        string bad = source.Replace("\"ProductNames\":[", "\"ProductNames\":[\"" + escape + "\",");
        E5d_Refused(bad, city.State);
        Assert.False(FoodNameIdentity.IsCanonical("bad\ud800"));
        Assert.False(FoodNameIdentity.IsCanonical("bad\udfff"));
    }

    [Fact]
    public void E5d_MetadataIsBoundedImmutableAndReloadDoesNotUseCurrentConfiguration()
    {
        var (city, _) = S17_Full();
        var decision = city.State.Food!.Residents.Single(m => m.Decision != null).Decision!;
        var offer = decision.Shops[0].Offers[0]; offer.ProductId = "foodtrucks:itemname_choppedcheese";
        decision.ProductId = offer.ProductId;
        offer.Metadata = new FoodOfferMetadata(2, FoodMealClass.Unknown, 2, 900, 100, 450);
        city.State.MenuPolicy = FoodNameIdentity.DynamicWire;
        string wire = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(wire); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(wire, SaveCodec.Write(read.State!));
        Assert.All(typeof(FoodOfferMetadata).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(900, read.State!.Food!.Residents.Single(m => m.Decision != null).Decision!.Shops[0].Offers[0].Metadata!.BreakfastFitCents);
        Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(wire));
        Assert.Throws<SaveValidationException>(() => SaveCodec.ValidateForActivation(read.State));
        Assert.Throws<InvalidOperationException>(() => new CitySimulator(read.State, new Walking()));
        read.State.MenuPolicy = FoodNameIdentity.Wire;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(read.State));
    }

    [Theory]
    [InlineData(2, false)] [InlineData(6, false)] [InlineData(10, false)] [InlineData(10, true)] [InlineData(20, false)]
    public void E5d_InterruptedJourneyServicePaidAndUncertainMigrationNeverRepeatsCheckout(int elapsed, bool uncertain)
    {
        var (city, market, routes) = Ready(); market.ThrowAfterSale = uncertain;
        double at = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, at + elapsed);
        string source = E5d_Wire24(city.State);
        var read = SaveCodec.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(source, E5d_Wire24(read.State!));
        var twin = new Market { Units = market.Units, ThrowAfterSale = uncertain };
        twin.Orders.AddRange(market.Orders); twin.Times.AddRange(market.Times);
        var restored = new CitySimulator(read.State!, routes); restored.AttachFoodMarket(twin);
        Advance(city, at + 50); Advance(restored, at + 50);
        Assert.Equal(market.Orders, twin.Orders); Assert.Equal(market.Times, twin.Times);
        Assert.Equal(E5d_Wire24(city.State), E5d_Wire24(restored.State));
        Assert.Equal(uncertain ? MealStatus.Uncertain : MealStatus.Paid, restored.State.Food!.Residents.Single().Status);
        Assert.Single(twin.Orders);
    }

    [Theory]
    [InlineData("new-table")] [InlineData("new-start")] [InlineData("new-metadata")]
    [InlineData("new-policy")] [InlineData("missing-policy")] [InlineData("missing-outflow")]
    [InlineData("unknown-offer")] [InlineData("fixed-code-7")] [InlineData("missing-field")]
    public void E5d_Source24IsValidatedBeforeAnyNewShapeCanBeDefaulted(string damage)
    {
        var (city, _) = S17_Full(); city.State.Food!.History.Add(E5d_Row(FoodProducts.Burger));
        var root = JObject.Parse(E5d_Wire24(city.State));
        switch (damage)
        {
            case "new-table": root["ProductNames"] = new JArray(); break;
            case "new-start": root["NamePolicyStartsDay"] = 0; break;
            case "new-metadata": E5d_Offer(root)["Metadata"] = new JArray(1,1,2,0,0,0); break;
            case "new-policy": root["MenuPolicy"] = FoodNameIdentity.Wire; break;
            case "missing-policy": root.Remove("MenuPolicy"); break;
            case "missing-outflow": ((JObject)root["Food"]!).Remove("Outflow"); break;
            case "unknown-offer": E5d_Offer(root)["ProductId"] = "foodtrucks:itemname_dumplings"; break;
            case "fixed-code-7": root["Food"]!["History"]![0]!["ProductSales"]![0]![0] = 7; break;
            case "missing-field": root.Remove("Seed"); break;
        }
        E5d_Refused(root.ToString(Formatting.None), city.State);
    }

    [Fact]
    public void E5d_EscapedNamesAreLosslessAndBothTableAndMetadataByteBudgetsArePrechecked()
    {
        var (city, _) = S17_Full(); string source = SaveCodec.Write(city.State);
        string paddedTable = source.Replace("\"ProductNames\":[", "\"ProductNames\":[" + new string(' ', FoodNameIdentity.MaximumTableWire));
        E5d_Refused(paddedTable, city.State);
        Assert.Contains("character/UTF-8 budget", SaveCodec.Read(paddedTable).Error);
        var unicodeRoot = JObject.Parse(source);
        unicodeRoot["ProductNames"] = new JArray(Enumerable.Range(0, 256).Select(i => new string('\ue001', 253) + i.ToString("D3")));
        string unicodeTable = unicodeRoot.ToString(Formatting.None).Replace("\"ProductNames\":[", "\"ProductNames\":[" + new string(' ', 330000));
        Assert.True(unicodeTable.Length < FoodNameIdentity.MaximumTableWire);
        E5d_Refused(unicodeTable, city.State);
        Assert.Contains("character/UTF-8 budget", SaveCodec.Read(unicodeTable).Error);
        var root = JObject.Parse(source);
        var offer = E5d_Offer(root); offer["Metadata"] = new JArray(2,1,2,900,100,450);
        string tuple = offer["Metadata"]!.ToString(Formatting.None);
        string paddedTuple = root.ToString(Formatting.None).Replace("\"Metadata\":" + tuple,
            "\"Metadata\":[" + new string(' ', FoodNameIdentity.MaximumHeldWire) + tuple.Substring(1));
        E5d_Refused(paddedTuple, city.State);
        Assert.Contains("metadata character/UTF-8", SaveCodec.Read(paddedTuple).Error);
        string duplicate = source.Replace("\"ProductNames\":", "\"ProductNames\":[],\"ProductNames\":");
        E5d_Refused(duplicate, city.State);
        string duplicateMetadata = source.Replace("\"Metadata\":", "\"Metadata\":[1,1,2,0,0,0],\"Metadata\":");
        E5d_Refused(duplicateMetadata, city.State);
        string noncanonical = source.Replace("\"Metadata\":[1,", "\"Metadata\":[01,");
        E5d_Refused(noncanonical, city.State);
    }

    [Fact]
    public void E5d_64CapturedTuplesFitAnd65RefuseBeforeDeserializingTheImage()
    {
        var (city, _) = S17_Full();
        var decision = city.State.Food!.Residents.Single(m => m.Decision != null).Decision!;
        var first = decision.Shops[0];
        first.Offers.Clear();
        for (int i = 0; i < 8; i++) first.Offers.Add(new FoodDecisionOffer { ProductId = "product:" + i,
            Price = Money.FromCents(100), Units = 1, Metadata = new FoodOfferMetadata(2, FoodMealClass.Unknown, 2, 900, 100, 450) });
        decision.ProductId = first.Offers[0].ProductId;
        for (int i = 1; i < 8; i++) decision.Shops.Add(new FoodDecisionShop { BusinessId = "held:" + i,
            PlaceId = first.PlaceId, MealsPerHour = first.MealsPerHour, NextServiceAt = first.NextServiceAt,
            Hours = first.Hours, Offers = first.Offers, OutwardQuote = first.OutwardQuote, ReturnQuote = first.ReturnQuote,
            Outward = first.Outward, Return = first.Return });
        decision.QuotesDone = 16;
        city.State.MenuPolicy = FoodNameIdentity.DynamicWire;
        string source = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(64, read.State!.Food!.Residents.Sum(m => m.Decision?.Shops.Sum(s => s.Offers.Count) ?? 0));
        Assert.Equal(source, SaveCodec.Write(read.State));
        var root = JObject.Parse(source);
        var meals = (JArray)root["Food"]!["Residents"]!;
        var second = meals.First(m => m[9]!.Type == JTokenType.Null);
        second[9] = meals.First(m => m[9]!.Type == JTokenType.Object)[9]!.DeepClone();
        string bad = root.ToString(Formatting.None);
        Assert.Contains("Oversized held metadata image", SaveCodec.Read(bad).Error);
        E5d_Refused(bad, city.State);
        Assert.DoesNotContain("product:", string.Join(",", FoodProducts.Ids)); // no live definition was registered
    }
}
