using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

// Base merge of exact integer-unit money into optional stops: schemas 15/16 carry money exactly as
// schema 14 does, and every earlier city schema reaches 16 under the same strict money rules.
public sealed partial class FoodCommerceTests
{
    // Written verbatim by the optional-stop branch before the merge (4aebca2): schema 16 with decimal
    // "...Cents" money. A 25-cent purchase is exact in units; 10.2528f was stored as twelve-digit cents.
    private const string BaseMerge_EraSchema16Exact = """{"Citizens":[{"Activity":2,"ActivityUntilMinute":2870.0,"Car":null,"CommutePreparation":{"PlaceId":"","PreparedAt":0.0,"TravelMinutes":0.0,"WorkEndsAt":0.0,"WorkStartsAt":0.0},"CompletedJourneys":0,"DayPlaceId":"work","DepartureMinute":0.0,"DestinationActivity":0,"DestinationId":"","HomeId":"home","Identity":{"Age":30,"AppearanceSeed":0,"District":"","Female":false,"Id":"r0","Name":"Resident 0","Persona":1},"JobChanges":0,"JoinedMinute":1440.0,"Journey":null,"LastEmploymentCheckDay":1,"LeisureId":"","MealOuting":null,"MissedActivities":0,"NextEventMinute":2870.0,"OptionalStop":null,"OptionalStopChoice":null,"Phase":0,"PlaceId":"shop","Preferences":{"AllowTransit":true,"FareWeightMinutes":1.8599999999999999,"OwnsCar":true,"WalkingToleranceMetres":1188.0},"RecentVisits":[],"ShopId":"shop"}],"Food":{"Districts":[],"History":[{"AccessRejections":0,"BusinessId":"b","BuyerDistricts":[{"DistrictId":"","Purchases":1}],"ChosenOffers":1,"ClosedRejections":0,"Day":1,"ExistingStopPurchases":1,"OffersConsidered":1,"PlaceId":"shop","PreferredAlternatives":0,"PriceRejections":0,"ProductSales":[{"ProductId":"pastry","PurchaseMeals":[1,0,0,0],"Purchases":1,"RevenueCents":25.0}],"PurchaseHours":[0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0],"PurchaseMeals":[1,0,0,0],"Purchases":1,"RevenueCents":25.0,"ServiceRejections":0,"StaffRejections":0,"StockRejections":0,"UnknownEvidence":0}],"HouseholdFinanceVersion":1,"MealRhythmVersion":0,"RepresentationFactor":1,"Residents":[{"AllowanceCents":207520.0,"AnchorId":"r0","Budget":{"ArrearsCents":0.0,"BalanceCents":216883.0,"BaseIncomeCents":28287,"DailyEssentialsCents":2026,"DailyHomeFoodCents":1209,"DailyHousingCents":6153,"DailyIncomeCents":28287,"DaysWithoutHomeFood":0,"EssentialsPaidCents":8179.0,"HeldPurchaseCents":0.0,"HomeFoodFundedDay":1,"HomeFoodPaidCents":1209.0,"IncomeReceivedCents":28287.0,"IncomeSource":0,"LastPostedDay":1,"NextPostingMinute":2880.0,"OpeningBalanceCents":198009.0,"PreparedFoodSpentCents":25.0},"BusinessId":"b","Day":1,"DayAllowanceUsedCents":0.0,"Deadline":2379.0,"DesiredAt":2139.0,"DueAt":3593.0,"Excursion":null,"ExistingStop":true,"ExistingStopPurchases":1,"ExpiresAt":2379.0,"Kind":0,"NeedsHomeResync":false,"PlaceId":"shop","PreferenceBonusCents":0,"PreferredProductId":"","PriceCents":25.0,"ProductId":"pastry","Purchases":1,"Reason":"Native fake confirmed","ResidentId":"r0","ReturnMinutes":0.0,"ServiceAt":2143.0,"ServiceMinutes":4.0,"Slot":0,"SpentCents":25.0,"Status":3}],"RoutineStops":[],"Shops":[{"AdditionalProductIds":[],"BusinessId":"b","NextServiceAt":2143.0,"PlaceId":"shop","ProductId":"pastry"}]},"InitialImportedResidents":1,"Minute":2169.0,"NextResidentSequence":1,"Places":[{"AttractionWeight":1.0,"Capacity":1,"CloseMinute":1440,"District":"","EvidenceSource":"legacy","Id":"home","Kind":0,"Name":"Home","NativeTrafficIndex":-1,"OpenMinute":0,"Position":{"X":0.0,"Y":0.0,"Z":0.0},"ResidentialAreaSquareMetres":0.0,"WeeklyHours":[]},{"AttractionWeight":1.0,"Capacity":1,"CloseMinute":1440,"District":"","EvidenceSource":"legacy","Id":"shop","Kind":2,"Name":"Bakery","NativeTrafficIndex":-1,"OpenMinute":0,"Position":{"X":2.0,"Y":0.0,"Z":0.0},"ResidentialAreaSquareMetres":0.0,"WeeklyHours":[]},{"AttractionWeight":1.0,"Capacity":1,"CloseMinute":1440,"District":"","EvidenceSource":"legacy","Id":"work","Kind":1,"Name":"Work","NativeTrafficIndex":-1,"OpenMinute":0,"Position":{"X":1.0,"Y":0.0,"Z":0.0},"ResidentialAreaSquareMetres":0.0,"WeeklyHours":[]}],"PopulationKey":"food-live","PopulationTarget":1,"RloVersion":"0.40.4","Schema":16,"Seed":7,"SourceIdentities":[{"Age":30,"AppearanceSeed":0,"District":"","Female":false,"Id":"r0","Name":"Resident 0","Persona":1}],"Traffic":null,"TrafficTopologyId":"","TravelClockRate":1.0,"TravelTimingVersion":1}""";
    private const string BaseMerge_EraSchema16TwelveDigit = """{"Citizens":[{"Activity":2,"ActivityUntilMinute":2870.0,"Car":null,"CommutePreparation":{"PlaceId":"","PreparedAt":0.0,"TravelMinutes":0.0,"WorkEndsAt":0.0,"WorkStartsAt":0.0},"CompletedJourneys":0,"DayPlaceId":"work","DepartureMinute":0.0,"DestinationActivity":0,"DestinationId":"","HomeId":"home","Identity":{"Age":30,"AppearanceSeed":0,"District":"","Female":false,"Id":"r0","Name":"Resident 0","Persona":1},"JobChanges":0,"JoinedMinute":1440.0,"Journey":null,"LastEmploymentCheckDay":1,"LeisureId":"","MealOuting":null,"MissedActivities":0,"NextEventMinute":2870.0,"OptionalStop":null,"OptionalStopChoice":null,"Phase":0,"PlaceId":"shop","Preferences":{"AllowTransit":true,"FareWeightMinutes":1.8599999999999999,"OwnsCar":true,"WalkingToleranceMetres":1188.0},"RecentVisits":[],"ShopId":"shop"}],"Food":{"Districts":[],"History":[{"AccessRejections":0,"BusinessId":"b","BuyerDistricts":[{"DistrictId":"","Purchases":1}],"ChosenOffers":1,"ClosedRejections":0,"Day":1,"ExistingStopPurchases":1,"OffersConsidered":1,"PlaceId":"shop","PreferredAlternatives":0,"PriceRejections":0,"ProductSales":[{"ProductId":"pastry","PurchaseMeals":[1,0,0,0],"Purchases":1,"RevenueCents":1025.279998779297}],"PurchaseHours":[0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0],"PurchaseMeals":[1,0,0,0],"Purchases":1,"RevenueCents":1025.279998779297,"ServiceRejections":0,"StaffRejections":0,"StockRejections":0,"UnknownEvidence":0}],"HouseholdFinanceVersion":1,"MealRhythmVersion":0,"RepresentationFactor":1,"Residents":[{"AllowanceCents":207520.0,"AnchorId":"r0","Budget":{"ArrearsCents":0.0,"BalanceCents":215882.720001220703,"BaseIncomeCents":28287,"DailyEssentialsCents":2026,"DailyHomeFoodCents":1209,"DailyHousingCents":6153,"DailyIncomeCents":28287,"DaysWithoutHomeFood":0,"EssentialsPaidCents":8179.0,"HeldPurchaseCents":0.0,"HomeFoodFundedDay":1,"HomeFoodPaidCents":1209.0,"IncomeReceivedCents":28287.0,"IncomeSource":0,"LastPostedDay":1,"NextPostingMinute":2880.0,"OpeningBalanceCents":198009.0,"PreparedFoodSpentCents":1025.279998779297},"BusinessId":"b","Day":1,"DayAllowanceUsedCents":0.0,"Deadline":2379.0,"DesiredAt":2139.0,"DueAt":3593.0,"Excursion":null,"ExistingStop":true,"ExistingStopPurchases":1,"ExpiresAt":2379.0,"Kind":0,"NeedsHomeResync":false,"PlaceId":"shop","PreferenceBonusCents":0,"PreferredProductId":"","PriceCents":1025.279998779297,"ProductId":"pastry","Purchases":1,"Reason":"Native fake confirmed","ResidentId":"r0","ReturnMinutes":0.0,"ServiceAt":2143.0,"ServiceMinutes":4.0,"Slot":0,"SpentCents":1025.279998779297,"Status":3}],"RoutineStops":[],"Shops":[{"AdditionalProductIds":[],"BusinessId":"b","NextServiceAt":2143.0,"PlaceId":"shop","ProductId":"pastry"}]},"InitialImportedResidents":1,"Minute":2169.0,"NextResidentSequence":1,"Places":[{"AttractionWeight":1.0,"Capacity":1,"CloseMinute":1440,"District":"","EvidenceSource":"legacy","Id":"home","Kind":0,"Name":"Home","NativeTrafficIndex":-1,"OpenMinute":0,"Position":{"X":0.0,"Y":0.0,"Z":0.0},"ResidentialAreaSquareMetres":0.0,"WeeklyHours":[]},{"AttractionWeight":1.0,"Capacity":1,"CloseMinute":1440,"District":"","EvidenceSource":"legacy","Id":"shop","Kind":2,"Name":"Bakery","NativeTrafficIndex":-1,"OpenMinute":0,"Position":{"X":2.0,"Y":0.0,"Z":0.0},"ResidentialAreaSquareMetres":0.0,"WeeklyHours":[]},{"AttractionWeight":1.0,"Capacity":1,"CloseMinute":1440,"District":"","EvidenceSource":"legacy","Id":"work","Kind":1,"Name":"Work","NativeTrafficIndex":-1,"OpenMinute":0,"Position":{"X":1.0,"Y":0.0,"Z":0.0},"ResidentialAreaSquareMetres":0.0,"WeeklyHours":[]}],"PopulationKey":"food-live","PopulationTarget":1,"RloVersion":"0.40.4","Schema":16,"Seed":7,"SourceIdentities":[{"Age":30,"AppearanceSeed":0,"District":"","Female":false,"Id":"r0","Name":"Resident 0","Persona":1}],"Traffic":null,"TrafficTopologyId":"","TravelClockRate":1.0,"TravelTimingVersion":1}""";

    // Text edits only: reparsing would turn decimal-cent tokens into doubles and change the amounts.
    private static string BaseMerge_Label(string payload, int schema)
    {
        Assert.Contains("\"Schema\":16,", payload);
        string labelled = payload.Replace("\"Schema\":16,", "\"Schema\":" + schema + ",");
        if (schema < 16) labelled = labelled.Replace("\"OptionalStopChoice\":null,", "");
        if (schema < 15) labelled = labelled.Replace("\"OptionalStop\":null,", "");
        Assert.Equal(schema < 16, labelled.Contains("\"Schema\":" + schema + ",") && !labelled.Contains("OptionalStopChoice"));
        Assert.Equal(schema < 15, !labelled.Contains("\"OptionalStop\""));
        return labelled;
    }

    private static void BaseMerge_RefusedAndPreserved(string payload, string error)
    {
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Contains(error, read.Error);
        var (city, _, _) = Ready();
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload };
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(payload, data[SaveStore.StateKey]);
    }

    [Theory]
    [InlineData(.25f)] [InlineData(12.5f)] [InlineData(3f)]
    public void BaseMerge_Schema13MoneyMigratesToSchema16Exactly(float native)
    {
        var (city, current) = R8_Paid(native);
        Assert.Contains("\"Schema\":16,", current);
        string old = LegacyMoneyWire.AsSchema(LegacyMoneyWire.ToLegacy(JObject.Parse(current), wholeCents: true), 13).ToString(Formatting.None);
        var price = city.State.Food!.Residents.Single().Price;
        Assert.Contains("\"PriceCents\":" + price.Units / Money.UnitsPerCent + ",", old);
        var read = SaveCodec.Read(old);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(13, read.SourceSchema);
        Assert.Equal(current, SaveCodec.Write(read.State!));
    }

    [Theory]
    [InlineData(.01f)] [InlineData(.3f)] [InlineData(1.37f)] [InlineData(10.2528f)] [InlineData(12.49f)]
    public void BaseMerge_CurrentSchema14MoneyMigratesToSchema16Exactly(float native)
    {
        var (city, current) = R8_Paid(native);
        Assert.Contains("\"Price\":" + city.State.Food!.Residents.Single().Price.Units + ",", current);
        var read = SaveCodec.Read(BaseMerge_Label(current, 14));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(14, read.SourceSchema);
        Assert.Equal(current, SaveCodec.Write(read.State!));
    }

    // A development save from the optional-stop branch follows the rule used for earlier schema-14
    // saves: exact amounts migrate to the same units, field by field, and nothing is rounded.
    [Theory]
    [InlineData(14)] [InlineData(15)] [InlineData(16)]
    public void BaseMerge_OptionalStopEraCentsSavesMigrateExactly(int schema)
    {
        string old = BaseMerge_Label(BaseMerge_EraSchema16Exact, schema);
        var read = SaveCodec.Read(old);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(schema, read.SourceSchema);
        Assert.Equal(Money.FromCents(25), read.State!.Food!.Residents.Single().Price);
        string written = SaveCodec.Write(read.State);
        Assert.Contains("\"Schema\":16,", written);
        int compared = 0;
        foreach (var field in LegacyMoneyWire.Names)
        {
            var cents = Regex.Matches(old, "\"" + field.Value + "\":([0-9.]+)").Select(m => m.Groups[1].Value).ToList();
            var units = Regex.Matches(written, "\"" + field.Key + "\":(-?[0-9]+)[,}]").Select(m => long.Parse(m.Groups[1].Value)).ToList();
            Assert.DoesNotContain("\"" + field.Value + "\":", written);
            Assert.Equal(cents.Count, units.Count);
            for (int i = 0; i < cents.Count; i++)
            {
                Assert.True(Money.TryParseCents(cents[i], 28 /* LegacyMoneyFields.MaximumFractionDigits */, out var expected));
                Assert.Equal(expected.Units, units[i]); compared++;
            }
        }
        Assert.True(compared >= 14, "Compared " + compared + " monetary fields.");
        Assert.Equal(written, SaveCodec.Write(SaveCodec.Read(written).State!));
    }

    [Theory]
    [InlineData(14)] [InlineData(15)] [InlineData(16)]
    public void BaseMerge_OptionalStopEraNonRepresentableCentsAreRefusedAndPreserved(int schema)
    {
        string old = BaseMerge_Label(BaseMerge_EraSchema16TwelveDigit, schema);
        Assert.Contains("\"PriceCents\":1025.279998779297,", old);
        BaseMerge_RefusedAndPreserved(old, "not an exact whole number of units");
    }

    [Theory]
    [InlineData(15)] [InlineData(16)]
    public void BaseMerge_OptionalStopEraMixedMoneyGenerationsAreRefused(int schema)
    {
        string old = BaseMerge_Label(BaseMerge_EraSchema16Exact, schema);
        string mixed = old.Replace("\"ArrearsCents\":0.0", "\"Arrears\":0");
        Assert.NotEqual(old, mixed);
        BaseMerge_RefusedAndPreserved(mixed, "mixes money field generations");
    }

    [Theory]
    [InlineData(15, "Allowance", "0x10")] [InlineData(16, "Allowance", "0X10")] [InlineData(16, "Arrears", "0x0")]
    [InlineData(15, "Allowance", "05")] [InlineData(16, "Allowance", "05")] [InlineData(16, "Allowance", "5e2")]
    [InlineData(15, "Allowance", "1.0")] [InlineData(16, "Allowance", "-1")] [InlineData(16, "Allowance", "9223372036854775808")]
    [InlineData(15, "AllowanceCents", "0x1")] [InlineData(16, "ArrearsCents", "0x10")] [InlineData(16, "AllowanceCents", "1e2")]
    [InlineData(15, "AllowanceCents", "0.1")] [InlineData(16, "AllowanceCents", "-2")]
    public void BaseMerge_NonCanonicalMoneyTokensAreRefusedInSchemas15And16(int schema, string field, string token)
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        city.State.Food!.Residents.Single().Allowance = Money.FromCents(2);
        string current = SaveCodec.Write(city.State);
        var wire = JObject.Parse(current);
        if (field.EndsWith("Cents", StringComparison.Ordinal)) LegacyMoneyWire.ToLegacy(wire, wholeCents: false);
        string encoded = LegacyMoneyWire.AsSchema(wire, schema).ToString(Formatting.None);
        var control = SaveCodec.Read(encoded);
        Assert.True(control.Status == SaveReadStatus.Loaded, control.Error); Assert.Equal(current, SaveCodec.Write(control.State!));
        string damaged = Regex.Replace(encoded, "\"" + field + "\":[0-9.]+", "\"" + field + "\":" + token);
        Assert.NotEqual(encoded, damaged);
        BaseMerge_RefusedAndPreserved(damaged, "Invalid state");
    }

    [Theory]
    [InlineData(.01f)] [InlineData(.3f)] [InlineData(10.2528f)] [InlineData(12.49f)]
    public void BaseMerge_Schema16MoneyRoundTripsByteIdentically(float native)
    {
        var (city, current) = R8_Paid(native);
        Assert.Contains("\"Schema\":16,", current);
        Assert.Contains("\"OptionalStop\":null", current); Assert.Contains("\"OptionalStopChoice\":null", current);
        Assert.Contains("\"Price\":" + city.State.Food!.Residents.Single().Price.Units + ",", current);
        foreach (var field in LegacyMoneyWire.Names)
        {
            Assert.DoesNotContain("\"" + field.Value + "\":", current);
            Assert.DoesNotMatch("\"" + field.Key + "\":-?[0-9]*[.eE]", current);
        }
        var read = SaveCodec.Read(current);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(16, read.SourceSchema);
        Assert.Equal(current, SaveCodec.Write(read.State!));
        var data = new Dictionary<string, string>();
        Assert.True(SaveStore.WriteTo(data, read.State!).Success); Assert.Equal(current, data[SaveStore.StateKey]);
    }
}
