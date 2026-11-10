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
    static JObject E5d_Offer(JObject root) => (JObject)root["Food"]!["Residents"]!
        .Select(m => m[DevelopmentChain.DecisionSlot(root)]).First(d => d?.Type == JTokenType.Object)!["Shops"]![0]!["Offers"]![0]!;
    static void E5d_Refused(string source, CityState replacement)
    {
        Assert.Equal(SaveReadStatus.Invalid, DevelopmentChain.Read(source).Status);
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
        var (city, _) = S17_Full(); var root = SaveWire.Parse(DevelopmentChain.Write(city.State));
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
        string first = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(first); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(name, read.State!.Food!.History.Single().ProductSales.Single().ProductId);
        // No loaded catalogue API exists in the codec. A removed mod is still an exact historical fact.
        Assert.Equal(first, DevelopmentChain.Write(read.State));
        read.State.Food.History.Add(E5d_Row("A:new-before-existing", "other-generation"));
        string changed = DevelopmentChain.Write(read.State);
        var next = DevelopmentChain.Read(changed); Assert.True(next.Status == SaveReadStatus.Loaded, next.Error);
        Assert.Equal(name, next.State!.Food!.History.First().ProductSales.Single().ProductId);
        next.State.Food.History.RemoveAt(1);
        Assert.Equal(first, DevelopmentChain.Write(next.State));
        var table = SaveWire.Parse(changed)["ProductNames"]!.Values<string>().ToArray();
        Assert.Equal(table.OrderBy(n => n, StringComparer.Ordinal), table);
    }

    [Theory]
    [InlineData(0)] [InlineData(257)] [InlineData(256)] [InlineData(-1)]
    public void E5d_DanglingProductReferencesCannotFallBackToFixedE4Codes(int reference)
    {
        var (city, _) = S17_Full(); city.State.Food!.History.Add(E5d_Row("foodtrucks:itemname_dumplings"));
        var root = SaveWire.Parse(DevelopmentChain.Write(city.State));
        root["Food"]!["History"]![0]!["ProductSales"]![0]![0] = reference;
        E5d_Refused(root.ToString(Formatting.None), city.State);
    }

    [Theory]
    [InlineData("\\ud800")] [InlineData("\\udfff")] [InlineData("\\ud800x")]
    public void E5d_InvalidEscapedSurrogatesCannotBeRemappedToReplacementCharacters(string escape)
    {
        var (city, _) = S17_Full(); string source = DevelopmentChain.Write(city.State);
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
        string wire = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(wire); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(wire, DevelopmentChain.Write(read.State!));
        Assert.All(typeof(FoodOfferMetadata).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(900, read.State!.Food!.Residents.Single(m => m.Decision != null).Decision!.Shops[0].Offers[0].Metadata!.BreakfastFitCents);
        Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(wire));
        Assert.Throws<SaveValidationException>(() => DevelopmentChain.ValidateForActivation(read.State));
        Assert.Throws<InvalidOperationException>(() => new CitySimulator(read.State, new Walking()));
        var data = new Dictionary<string, string> { [SaveCodec.StateKey] = wire };
        Assert.Equal(DevelopmentChain.ProductStatus(wire, SaveReadStatus.Invalid), SaveStore.ReadFrom(data, city.State.PopulationKey).Status);
        Assert.False(SaveStore.WriteTo(data, S17_Full().city.State).Success);
        Assert.False(SaveSession.Open(data, city.State.PopulationKey).WriteTo(data, S17_Full().city.State).Success);
        Assert.Same(wire, data[SaveCodec.StateKey]);
        var empty = new Dictionary<string, string>();
        Assert.False(SaveStore.WriteTo(empty, read.State).Success);
        Assert.False(SaveSession.Open(empty, city.State.PopulationKey).WriteTo(empty, read.State).Success);
        Assert.Empty(empty);
        read.State.MenuPolicy = FoodNameIdentity.Wire;
        Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(read.State));
    }

    [Fact]
    public void E5d_EscapedNamesAreLosslessAndBothTableAndMetadataByteBudgetsArePrechecked()
    {
        var (city, _) = S17_Full(); string source = DevelopmentChain.Write(city.State);
        string paddedTable = source.Replace("\"ProductNames\":[", "\"ProductNames\":[" + new string(' ', FoodNameIdentity.MaximumTableWire));
        E5d_Refused(paddedTable, city.State);
        Assert.Contains("character/UTF-8 budget", DevelopmentChain.Read(paddedTable).Error);
        var unicodeRoot = SaveWire.Parse(source);
        unicodeRoot["ProductNames"] = new JArray(Enumerable.Range(0, 256).Select(i => new string('\ue001', 253) + i.ToString("D3")));
        string unicodeTable = unicodeRoot.ToString(Formatting.None).Replace("\"ProductNames\":[", "\"ProductNames\":[" + new string(' ', 330000));
        Assert.True(unicodeTable.Length < FoodNameIdentity.MaximumTableWire);
        E5d_Refused(unicodeTable, city.State);
        Assert.Contains("character/UTF-8 budget", DevelopmentChain.Read(unicodeTable).Error);
        var root = SaveWire.Parse(source);
        var offer = E5d_Offer(root); offer["Metadata"] = new JArray(2,1,2,900,100,450);
        string tuple = offer["Metadata"]!.ToString(Formatting.None);
        string paddedTuple = root.ToString(Formatting.None).Replace("\"Metadata\":" + tuple,
            "\"Metadata\":[" + new string(' ', FoodNameIdentity.MaximumHeldWire) + tuple.Substring(1));
        E5d_Refused(paddedTuple, city.State);
        Assert.Contains("metadata character/UTF-8", DevelopmentChain.Read(paddedTuple).Error);
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
        string source = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(64, read.State!.Food!.Residents.Sum(m => m.Decision?.Shops.Sum(s => s.Offers.Count) ?? 0));
        Assert.Equal(source, DevelopmentChain.Write(read.State));
        var root = SaveWire.Parse(source);
        var meals = (JArray)root["Food"]!["Residents"]!;
        var second = meals.First(m => m[8]!.Type == JTokenType.Null);
        second[8] = meals.First(m => m[8]!.Type == JTokenType.Object)[8]!.DeepClone();
        string bad = root.ToString(Formatting.None);
        Assert.Contains("Oversized held metadata image", DevelopmentChain.Read(bad).Error);
        E5d_Refused(bad, city.State);
        Assert.DoesNotContain("product:", string.Join(",", FoodProducts.Ids)); // no live definition was registered
    }

    [Theory]
    [InlineData("ProductNames", false)] [InlineData("ProductNames", true)]
    [InlineData("Metadata", false)] [InlineData("Metadata", true)]
    public void E5d_RawImageBudgetsIncludeEscapedFieldNamesAndWhitespaceBeforeTheValue(string field, bool escaped)
    {
        var (city, _) = S17_Full(); string source = DevelopmentChain.Write(city.State);
        string key = "\"" + field + "\":";
        int cap = field == "ProductNames" ? FoodNameIdentity.MaximumTableWire : FoodNameIdentity.MaximumHeldWire;
        var root = SaveWire.Parse(source);
        int used = field == "ProductNames" ? root[field]!.ToString(Formatting.None).Length + 16
            : 2 + root["Food"]!["Residents"]!.Select(m => m[8]).OfType<JObject>()
                .SelectMany(d => d["Shops"]!).SelectMany(s => s["Offers"]!)
                .Sum(o => o["Metadata"]!.ToString(Formatting.None).Length + 12);
        if (escaped)
        {
            string raw = "\"" + string.Concat(field.Select(c => "\\u" + ((int)c).ToString("x4"))) + "\":";
            // Only one field changes spelling; the decoded DTO key remains identical.
            int first = source.IndexOf(key, StringComparison.Ordinal);
            source = source.Remove(first, key.Length).Insert(first, raw);
            used += raw.Length - key.Length; key = raw;
        }
        int at = source.IndexOf(key, StringComparison.Ordinal) + key.Length;
        string exact = source.Insert(at, new string(' ', cap - used));
        var read = DevelopmentChain.Read(exact); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(DevelopmentChain.Write(city.State), DevelopmentChain.Write(read.State!));
        string bad = exact.Insert(at, " ");
        E5d_Refused(bad, city.State);
        Assert.Contains("character/UTF-8", DevelopmentChain.Read(bad).Error);
    }
}
