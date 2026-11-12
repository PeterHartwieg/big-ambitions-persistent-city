using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class BusinessTypesWalletTests
{
    private static string Corpus(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ReaderCorpus", file));


    [Fact]
    public void MoneyIsConservedAcrossTheMoveForEveryPerson()
    {
        var before = JObject.Parse(Corpus("retired-pre-wallet-full.json"));
        var after = JObject.Parse(Corpus(Path.Combine("schema-01", "full.json")));
        var oldRecords = before["Food"]!["Residents"]!.Cast<JArray>().ToDictionary(r => (string)r[26]!, StringComparer.Ordinal);
        var newRecords = after["Food"]!["Residents"]!.Cast<JArray>().ToDictionary(r => (string)r[26]!, StringComparer.Ordinal);
        var wallets = after["Citizens"]!.ToDictionary(c => (string)c["Identity"]!["Id"]!, c => c["Wallet"]!, StringComparer.Ordinal);
        var visitors = (JArray)after["Visitors"]!; var routines = (JArray)after["VisitorRoutines"]!;
        for (int i = 0; i < visitors.Count; i++) wallets.Add(((string)visitors[i]!).Split('|')[1], routines[i]!["Wallet"]!);

        Assert.Equal(oldRecords.Keys.OrderBy(k => k, StringComparer.Ordinal), wallets.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.True(oldRecords.Count > 200, "too few persons: " + oldRecords.Count);
        foreach (var (id, old) in oldRecords)
        {
            Assert.True(JToken.DeepEquals(old[2], wallets[id]), id + ": wallet differs from the budget it replaced");
            var moved = newRecords[id];
            Assert.Equal(0L, (long)moved[2]!);
            for (int slot = 0; slot < old.Count; slot++)
                if (slot != 2) Assert.True(JToken.DeepEquals(old[slot], moved[slot]), id + ": roster slot " + slot + " changed");
        }
        var read = SaveCodec.Read(after.ToString(Newtonsoft.Json.Formatting.None));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        AssertLinked(read.State!);
    }

    [Fact]
    public void ReloadKeepsEveryWalletAndTheCityContinuesAsIfUninterrupted()
    {
        var walk = new FreshCityTraceTests.Walk();
        var (stateA, marketA) = WorkplaceCapacityTests.Fresh(3, 9217);
        var (stateB, marketB) = WorkplaceCapacityTests.Fresh(3, 9217);
        var a = new CitySimulator(stateA, walk); a.AttachFoodMarket(marketA);
        var b = new CitySimulator(stateB, walk); b.AttachFoodMarket(marketB);
        double middle = stateA.Minute + 1440 + 7 * 60, end = middle + 2 * 1440;
        AttendanceWageTests.Advance(a, middle); AttendanceWageTests.Advance(b, middle);
        AssertLinked(b.State);
        var wallets = Wallets(b.State);
        Assert.True(wallets.Values.Any(w => w.LastPostedDay >= 0), "no wallet posted before the save");

        string payload = SaveCodec.Write(b.State);
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        AssertLinked(read.State!);
        var reloaded = Wallets(read.State!);
        Assert.Equal(wallets.Keys, reloaded.Keys);
        foreach (var (id, wallet) in wallets) Assert.Equal(Text(wallet), Text(reloaded[id]));

        var resumed = new CitySimulator(read.State!, walk); resumed.AttachFoodMarket(marketB);
        AssertLinked(resumed.State);
        AttendanceWageTests.Advance(a, end); AttendanceWageTests.Advance(resumed, end);
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(resumed.State));
        AssertLinked(resumed.State);
    }

    [Fact]
    public void AFoodRecordHoldingAnotherWalletThanItsPersonsRefusesTheSaveAndAWalletWithoutFinancesIsInvalid()
    {
        var state = TestCity.Banded();
        var meal = state.Food!.Residents[0];
        var person = state.Citizens.Single(c => c.Identity.Id == meal.ResidentId);
        meal.Budget = new ResidentBudget { IncomeSource = person.Wallet!.IncomeSource, BaseIncomeCents = 1, DailyHomeFoodCents = 1 };
        Assert.Contains("wallet is not its person's", Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state)).Message);
        meal.Budget = person.Wallet;
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);

        var plain = CurrentSchemaOnlyTests.City();
        plain.Citizens[0].Wallet = new ResidentBudget { BaseIncomeCents = 1, DailyHomeFoodCents = 1 };
        Assert.Contains("Wallet without household finances", Assert.Throws<SaveValidationException>(() => SaveCodec.Write(plain)).Message);
    }

    [Fact]
    public void ACityWrittenBeforeTheWalletMoveIsRefusedWithStartANewCityAndKept()
    {
        string old = Corpus("retired-pre-wallet-full.json");
        Assert.StartsWith(SaveCodec.BodyPrefix, old);
        var read = SaveCodec.Read(old);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Null(read.State);
        Assert.Contains(SaveCodec.DevelopmentSchemaMessage, read.Error);
        Assert.Contains("start a new city", read.Error);
        Assert.Equal(CityState.CurrentSchema, SaveCodec.DevelopmentSchemaOf(old));
        Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(old)).Message);

        var data = new Dictionary<string, string>(StringComparer.Ordinal) { [SaveStore.StateKey] = old, ["rlo:state"] = "owned by RLO" };
        var keys = new Dictionary<string, string>(data, StringComparer.Ordinal);
        var fresh = CurrentSchemaOnlyTests.City(); fresh.PopulationKey = JObject.Parse(old)["PopulationKey"]!.ToString();
        var session = SaveSession.Open(data, fresh.PopulationKey);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, session.InitialReadResult.Status);
        Assert.False(session.WriteTo(data, fresh).Success); Assert.False(SaveStore.WriteTo(data, fresh).Success);
        Assert.Equal(keys.Count, data.Count);
        foreach (var (key, value) in keys) Assert.Same(value, data[key]);
        Assert.Null(SaveCodec.DevelopmentSchemaOf(Corpus(Path.Combine("schema-01", "full.json"))));
    }


    [Fact]
    public void EssentialSharesSumExactlyAndANeedIsNotPaidTwice()
    {
        foreach (int total in new[] { 0, 1, 99, 1500, 1999, 2500, 1000000 })
        {
            Assert.Equal(total, HouseholdEssentials.Categories.Sum(c => HouseholdEssentials.Share(total, c)));
            Assert.Equal(total, HouseholdEssentials.Payable(total, EssentialNeeds.None));
            var clothing = EssentialNeeds.Of(HouseholdEssentials.Clothing);
            Assert.Equal(total - HouseholdEssentials.Share(total, HouseholdEssentials.Clothing), HouseholdEssentials.Payable(total, clothing));
        }
        Assert.Throws<ArgumentException>(() => EssentialNeeds.Of(HouseholdEssentials.Utilities));
        Assert.Throws<ArgumentException>(() => EssentialNeeds.Of("mod:category"));

        Money Posted(EssentialNeeds? needs)
        {
            var b = new ResidentBudget { IncomeSource = ResidentIncomeSource.ExternalSupport, BaseIncomeCents = 10000, DailyIncomeCents = 10000,
                DailyHousingCents = 3000, DailyEssentialsCents = 2000, DailyHomeFoodCents = 1000,
                OpeningBalance = Money.FromCents(5000), Balance = Money.FromCents(5000), NextPostingMinute = 1440 };
            HouseholdFinance.PostDay(b, 1440, needs: needs);
            Assert.True(HouseholdFinance.CashConserved(b));
            return b.EssentialsPaid;
        }
        Assert.Equal(Money.FromCents(3000 + 2000), Posted(null));
        Assert.Equal(Money.FromCents(3000 + 2000 - 400), Posted(EssentialNeeds.Of(HouseholdEssentials.Clothing)));
        Assert.Equal(Money.FromCents(3000 + 2000 - 400 - 300), Posted(EssentialNeeds.Of(HouseholdEssentials.Clothing, HouseholdEssentials.PersonalCare)));
    }

    [Fact]
    public void BusinessTypesNameTheirEssentialCategoryAndOnlyThose()
    {
        Assert.Equal(HouseholdEssentials.Clothing, BusinessTypes.For("ba:businesstype_clothingstore").EssentialCategory);
        Assert.Equal(HouseholdEssentials.HouseholdGoods, BusinessTypes.For("ba:businesstype_furniturestore").EssentialCategory);
        foreach (var type in new[] { "ba:businesstype_coffeeshop", "ba:businesstype_gym", "ba:businesstype_giftshop" })
            Assert.Equal("", BusinessTypes.For(type).EssentialCategory);
        foreach (var type in new[] { "ba:businesstype_clothingstore", "ba:businesstype_furniturestore", "ba:businesstype_appliancestore", "ba:businesstype_electronicsstore" })
            Assert.True(HouseholdEssentials.CanBecomeNeed(BusinessTypes.For(type).EssentialCategory), type);
    }


    [Theory]
    [InlineData("modded:businesstype_spaceport")] [InlineData("ba:businesstype_not_in_this_table")] [InlineData("")] [InlineData(null)]
    public void UnknownTypesGetTheNeutralDefault(string? type)
    {
        var profile = BusinessTypes.For(type);
        Assert.Same(BusinessTypes.Neutral, profile);
        Assert.True(profile.IsNeutral);
        Assert.Equal("", profile.EssentialCategory);
        Assert.Equal(7, profile.NeedIntervalDays); Assert.Equal(3, profile.NeedSpreadDays);
        // A known row uses the same neutral need numbers until GR-3 tunes them.
        Assert.Equal(7, BusinessTypes.For("ba:businesstype_clothingstore").NeedIntervalDays);
    }

    private static List<Place> Street() => new()
    {
        new Place { Id = "address:Main%20St:1:home", Kind = PlaceKind.Home },
        new Place { Id = "address:Main%20St:2:work", Kind = PlaceKind.Work },
        new Place { Id = "address:Main%20St:2:shop", Kind = PlaceKind.Shop },
        new Place { Id = "address:Main%20St:3:work", Kind = PlaceKind.Work },
        new Place { Id = "address:Main%20St:3:leisure", Kind = PlaceKind.Leisure },
        new Place { Id = "address:Main%20St:4:shop", Kind = PlaceKind.Shop },
        new Place { Id = "outdoor:park:1", Kind = PlaceKind.Leisure },
    };

    [Fact]
    public void RefreshOpensReplacesAndClosesBusinessesAndKeepsModdedNamesVerbatim()
    {
        var places = Street();
        var live = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["address:Main%20St:1"] = "ba:businesstype_coffeeshop",
            ["address:Main%20St:2"] = "ba:businesstype_clothingstore",
            ["address:Main%20St:3"] = "modded:businesstype_arcade",
            ["address:Main%20St:4"] = BusinessTypes.Empty,
        };
        var opened = BusinessTypeRefresh.Apply(places, live);
        Assert.Equal(new BusinessTypeChanges(4, 0, 0).ToString(), opened.ToString());
        Assert.Equal(new[] { "", "ba:businesstype_clothingstore", "ba:businesstype_clothingstore", "modded:businesstype_arcade", "modded:businesstype_arcade", "", "" },
            places.Select(p => p.BusinessType));
        Assert.False(BusinessTypeRefresh.Apply(places, live).Any);

        live["address:Main%20St:2"] = "ba:businesstype_giftshop";
        live["address:Main%20St:4"] = "ba:businesstype_bakery";
        var replaced = BusinessTypeRefresh.Apply(places, live);
        Assert.Equal(new BusinessTypeChanges(1, 0, 2).ToString(), replaced.ToString());
        Assert.Equal("ba:businesstype_bakery", places[5].BusinessType);
    }

    [Fact]
    public void AClosedBusinessLeavesNoDanglingReference()
    {
        var places = Street();
        var live = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["address:Main%20St:2"] = "ba:businesstype_clothingstore", ["address:Main%20St:3"] = "modded:businesstype_arcade" };
        BusinessTypeRefresh.Apply(places, live);
        var index = new BusinessTypeIndex(); index.Rebuild(places);
        Assert.Equal(new[] { "address:Main%20St:2:shop", "address:Main%20St:2:work" }, index.PlacesOf("ba:businesstype_clothingstore"));
        Assert.Equal(2, index.TypeCount);

        live.Remove("address:Main%20St:2");
        var closed = BusinessTypeRefresh.Apply(places, live);
        Assert.Equal(new BusinessTypeChanges(0, 2, 0).ToString(), closed.ToString());
        index.Rebuild(places);
        Assert.Empty(index.PlacesOf("ba:businesstype_clothingstore"));
        Assert.DoesNotContain("ba:businesstype_clothingstore", index.Types);
        Assert.Equal(1, index.TypeCount);
        Assert.All(places.Where(p => p.Id.StartsWith("address:Main%20St:2", StringComparison.Ordinal)), p => Assert.Equal("", p.BusinessType));
        Assert.Equal(7, places.Count);
    }

    [Fact]
    public void TypesRoundTripAndAClosedTypeIsSavedAsNone()
    {
        var state = TestCity.Banded();
        var shop = state.Places.First(p => p.Kind == PlaceKind.Shop);
        var work = state.Places.First(p => p.Kind == PlaceKind.Work);
        shop.BusinessType = "modded:businesstype_arcade"; work.BusinessType = "ba:businesstype_clothingstore";
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal("modded:businesstype_arcade", read.State!.Places.Single(p => p.Id == shop.Id).BusinessType);
        Assert.Equal("ba:businesstype_clothingstore", read.State.Places.Single(p => p.Id == work.Id).BusinessType);

        shop.BusinessType = BusinessTypes.None;
        read = SaveCodec.Read(SaveCodec.Write(state)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal("", read.State!.Places.Single(p => p.Id == shop.Id).BusinessType);

        state.Places.First(p => p.Kind == PlaceKind.Home).BusinessType = "ba:businesstype_coffeeshop";
        Assert.Contains("business type", Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state)).Message);
    }


    private static IEnumerable<CitizenState> Persons(CityState state) => state.Citizens.Concat<CitizenState>(state.VisitorRoutines);

    private static Dictionary<string, ResidentBudget> Wallets(CityState state)
        => Persons(state).ToDictionary(p => p.Identity.Id, p => p.Wallet!, StringComparer.Ordinal);

    private static string Text(ResidentBudget b) => Newtonsoft.Json.JsonConvert.SerializeObject(b);

    private static void AssertLinked(CityState state)
    {
        var persons = Persons(state).ToDictionary(p => p.Identity.Id, StringComparer.Ordinal);
        Assert.Equal(persons.Count, state.Food!.Residents.Count);
        foreach (var meal in state.Food.Residents)
        {
            var wallet = persons[meal.ResidentId].Wallet;
            Assert.NotNull(wallet);
            Assert.Same(wallet, meal.Budget);
            Assert.True(HouseholdFinance.CashConserved(wallet!));
        }
        Assert.Equal(persons.Count, persons.Values.Select(p => p.Wallet!).Distinct(ReferenceEqualityComparer.Instance).Count());
    }
}
