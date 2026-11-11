using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5b identity and ownership core (docs/plans/f5-population.md section 3). Pure Core, inactive.</summary>
public sealed class PersonIdentityTests
{
    // ---- PersonId formats and ordering (contract B1) ----

    [Theory]
    [InlineData("c0", PersonIdFormat.SourceResident)]
    [InlineData("c399", PersonIdFormat.SourceResident)]
    [InlineData("pc:1", PersonIdFormat.Resident)]
    [InlineData("pc:2147483647", PersonIdFormat.Resident)]
    [InlineData("pv:1", PersonIdFormat.Visitor)]
    [InlineData("pv:42", PersonIdFormat.Visitor)]
    [InlineData("consumer:c12:1", PersonIdFormat.LegacyConsumer)]
    [InlineData("consumer:pc%3A7:2", PersonIdFormat.LegacyConsumer)]
    [InlineData("", PersonIdFormat.Invalid)]
    [InlineData("c", PersonIdFormat.Invalid)]
    [InlineData("c01", PersonIdFormat.Invalid)]
    [InlineData("c-1", PersonIdFormat.Invalid)]
    [InlineData("C1", PersonIdFormat.Invalid)]
    [InlineData("pc:0", PersonIdFormat.Invalid)]
    [InlineData("pc:01", PersonIdFormat.Invalid)]
    [InlineData("pc:2147483648", PersonIdFormat.Invalid)]
    [InlineData("pc:", PersonIdFormat.Invalid)]
    [InlineData("pv:0", PersonIdFormat.Invalid)]
    [InlineData("pv:x", PersonIdFormat.Invalid)]
    [InlineData("consumer:c12:0", PersonIdFormat.Invalid)]
    [InlineData("consumer::1", PersonIdFormat.Invalid)]
    [InlineData("consumer:pc:7:2", PersonIdFormat.Invalid)]
    [InlineData("consumer:c 1:1", PersonIdFormat.Invalid)]
    [InlineData("consumer:c%3a1:1", PersonIdFormat.Invalid)]
    [InlineData("consumer:c%3:1", PersonIdFormat.Invalid)]
    [InlineData("r1", PersonIdFormat.Invalid)]
    public void PersonIdFormatsAreClassifiedStrictly(string id, PersonIdFormat expected)
        => Assert.Equal(expected, PersonIds.Classify(id));

    [Fact]
    public void LegacyConsumerIdsAreKeptVerbatimAndFitOnlyVisitors()
    {
        foreach (var anchor in new[] { "c0", "c957", "pc:1", "pc:600" })
            for (int slot = 1; slot <= 2; slot++)
            {
                string id = FoodRepresentation.ConsumerId(anchor, slot);
                Assert.Equal(PersonIdFormat.LegacyConsumer, PersonIds.Classify(id));
                Assert.True(PersonIds.FitsKind(PersonIds.Classify(id), PersonKind.Visitor));
                Assert.False(PersonIds.FitsKind(PersonIds.Classify(id), PersonKind.Resident));
            }
        // Slot zero is the resident itself, with the resident's own ID.
        Assert.Equal("pc:3", FoodRepresentation.ConsumerId("pc:3", 0));
        Assert.True(PersonIds.FitsKind(PersonIds.Classify("pc:3"), PersonKind.Resident));
        Assert.True(PersonIds.FitsKind(PersonIds.Classify("c3"), PersonKind.Resident));
        Assert.False(PersonIds.FitsKind(PersonIds.Classify("pv:3"), PersonKind.Resident));
    }

    [Fact]
    public void VisitorIdsAreSequentialAndNeverReuseASequence()
    {
        Assert.Equal("pv:1", PersonIds.Visitor(1));
        Assert.Equal("pv:2147483647", PersonIds.Visitor(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => PersonIds.Visitor(0));
        var ids = Enumerable.Range(1, 2000).Select(PersonIds.Visitor).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Equal(PersonIdFormat.Visitor, PersonIds.Classify(id)));
    }

    [Fact]
    public void PersonOrderIsOrdinalNotCultureOrNumeric()
    {
        var ids = new[] { "pv:10", "pc:9", "consumer:c1:1", "c10", "pv:2", "c9", "pc:10", "consumer:pc%3A1:1", "c100" };
        var expected = new[] { "c10", "c100", "c9", "consumer:c1:1", "consumer:pc%3A1:1", "pc:10", "pc:9", "pv:10", "pv:2" };
        Assert.Equal(expected, ids.OrderBy(id => id, PersonIds.Order).ToArray());
        var shuffled = ids.Reverse().ToList();
        shuffled.Sort(PersonIds.Compare);
        Assert.Equal(expected, shuffled);
        Assert.True(PersonIds.Compare("pc:10", "pc:9") < 0);
    }

    [Fact]
    public void HouseholdIdIsDerivedFromTheSavedHome()
    {
        Assert.Equal("h:home:12", HouseholdIds.For("home:12"));
        Assert.Equal(HouseholdIds.For("home:12"), HouseholdIds.For("home:" + 12));
        Assert.NotEqual(HouseholdIds.For("home:12"), HouseholdIds.For("home:13"));
        Assert.Throws<ArgumentException>(() => HouseholdIds.For(""));
    }

    [Fact]
    public void QuestionDefaultsLiveInNamedConstants()
    {
        Assert.False(PersonPolicy.VisitorsOwnCars);
        Assert.Equal(VisitorPattern.Commuter, PersonPolicy.MigratedConsumerPattern);
    }

    // ---- IdentityPolicy v1 (contract B6) ----

    private static CitizenIdentity[] GoldenDonors() => new[]
    {
        new CitizenIdentity { Id = "c0", Name = "Ava Stone", Age = 34, Female = true, Persona = Persona.Commuter, AppearanceSeed = 101, District = "Midtown" },
        new CitizenIdentity { Id = "c1", Name = "Ben Ortiz", Age = 19, Female = false, Persona = Persona.Student, AppearanceSeed = 102, District = "Garment" },
        new CitizenIdentity { Id = "c2", Name = "Cleo Marsh", Age = 70, Female = true, Persona = Persona.Retiree, AppearanceSeed = 103, District = "Midtown" },
        new CitizenIdentity { Id = "c3", Name = "Dan Hale", Age = 41, Female = false, Persona = Persona.Parent, AppearanceSeed = 104, District = "Kitchen" },
        new CitizenIdentity { Id = "c4", Name = "Eve Park", Age = 28, Female = true, Persona = Persona.Foodie, AppearanceSeed = 105, District = "Murray Hill" },
        new CitizenIdentity { Id = "c5", Name = "Finn Lowe Jr", Age = 52, Female = false, Persona = Persona.NightOwl, AppearanceSeed = 106, District = "Midtown" },
        new CitizenIdentity { Id = "c6", Name = "Gia Russo", Age = 23, Female = true, Persona = Persona.Sporty, AppearanceSeed = 107, District = "Garment" },
        new CitizenIdentity { Id = "c7", Name = "Hal Brook", Age = 66, Female = false, Persona = Persona.Grumpy, AppearanceSeed = 108, District = "Murray Hill" },
    };

    // Frozen version-1 outputs. A change here is a new IdentityPolicy version, never an edit of these rows.
    public static IEnumerable<object[]> GoldenVectors() => new (int Seed, string Id, string Name, int Age, bool Female, Persona Persona, int Appearance)[]
    {
        (0, "consumer:c0:1", "Dan Brook", 41, false, Persona.NightOwl, 104),
        (0, "consumer:pc%3A12:2", "Finn Lowe Hale", 52, false, Persona.Commuter, 106),
        (0, "pv:1", "Ava Marsh", 34, true, Persona.Sporty, 101),
        (0, "pv:42", "Finn Lowe Ortiz", 52, false, Persona.Sporty, 106),
        (12345, "consumer:c0:1", "Ava Stone", 34, true, Persona.NightOwl, 101),
        (12345, "consumer:pc%3A12:2", "Finn Lowe Brook", 52, false, Persona.Commuter, 106),
        (12345, "pv:1", "Eve Marsh", 28, true, Persona.Sporty, 105),
        (12345, "pv:42", "Gia Hale", 23, true, Persona.Commuter, 107),
        (-7, "consumer:c0:1", "Gia Russo", 23, true, Persona.Commuter, 107),
        (-7, "consumer:pc%3A12:2", "Cleo Russo", 70, true, Persona.Retiree, 103),
        (-7, "pv:1", "Cleo Ortiz", 70, true, Persona.Retiree, 103),
        (-7, "pv:42", "Ben Jr", 19, false, Persona.Student, 102),
    }.Select(v => new object[] { v.Seed, v.Id, v.Name, v.Age, v.Female, v.Persona, v.Appearance });

    [Theory]
    [MemberData(nameof(GoldenVectors))]
    public void DerivedIdentityMatchesFrozenGoldenVectors(int seed, string id, string name, int age, bool female, Persona persona, int appearance)
    {
        var identity = IdentityPolicy.V1(GoldenDonors()).Derive(seed, id);
        Assert.Equal(id, identity.Id);
        Assert.Equal(name, identity.Name);
        Assert.Equal(age, identity.Age);
        Assert.Equal(female, identity.Female);
        Assert.Equal(persona, identity.Persona);
        Assert.Equal(appearance, identity.AppearanceSeed);
        Assert.Equal("", identity.District);
    }

    [Fact]
    public void DerivedIdentityIsIndependentOfDonorAndQueryOrder()
    {
        var ids = new[] { "pv:1", "pv:2", "pv:300", "consumer:c3:1", "consumer:c3:2", "consumer:pc%3A9:1" };
        var forward = IdentityPolicy.V1(GoldenDonors());
        var reversed = IdentityPolicy.V1(GoldenDonors().Reverse());
        var rotated = IdentityPolicy.V1(GoldenDonors().Skip(3).Concat(GoldenDonors().Take(3)));
        string Key(CitizenIdentity c) => c.Id + "|" + c.Name + "|" + c.Age + "|" + c.Female + "|" + c.Persona + "|" + c.AppearanceSeed;
        var expected = ids.Select(id => Key(forward.Derive(5, id))).ToArray();
        Assert.Equal(expected, ids.Select(id => Key(reversed.Derive(5, id))).ToArray());
        Assert.Equal(expected.Reverse(), ids.Reverse().Select(id => Key(rotated.Derive(5, id))).ToArray());
        // Repeated lookups are pure: no state carries between them.
        Assert.Equal(expected, ids.Select(id => Key(forward.Derive(5, id))).ToArray());
    }

    [Fact]
    public void DerivedIdentityUsesPlatformIndependentIntegerHashing()
    {
        // Independent FNV-1a reference with the StableRandom finaliser: pure 32-bit integer arithmetic over UTF-16
        // code units, no culture, floating point or runtime string hashing.
        static uint Reference(string text, int seed)
        {
            uint value = 2166136261u ^ unchecked((uint)seed);
            foreach (char c in text) { value ^= c; value = unchecked(value * 16777619u); }
            value ^= value >> 16; value = unchecked(value * 0x7feb352du); value ^= value >> 15;
            return value;
        }
        var donors = GoldenDonors();
        var policy = IdentityPolicy.V1(donors);
        foreach (int seed in new[] { 0, 1, -7, int.MaxValue, int.MinValue })
            foreach (var id in new[] { "pv:1", "pv:99", "consumer:c0:1", "consumer:pc%3A12:2" })
            {
                var draw = policy.Draw(seed, id);
                Assert.Equal(IdentityPolicy.Version1, draw.PolicyVersion);
                Assert.Equal((int)(Reference(id + ":identity:v1:appearance", seed) % (uint)donors.Length), draw.Appearance);
                Assert.Equal((int)(Reference(id + ":identity:v1:surname", seed) % (uint)donors.Length), draw.Surname);
                Assert.Equal(StableRandom.Hash(id + ":identity:v1:appearance", seed), Reference(id + ":identity:v1:appearance", seed));
            }
    }

    [Fact]
    public void IdentityPolicyIsVersionedAndRefusesUnusableCatalogues()
    {
        Assert.Equal(1, IdentityPolicy.CurrentVersion);
        Assert.Equal(IdentityPolicy.Version1, IdentityPolicy.V1(GoldenDonors()).PolicyVersion);
        Assert.Throws<InvalidOperationException>(() => IdentityPolicy.V1(Array.Empty<CitizenIdentity>()));
        Assert.Throws<InvalidOperationException>(() => IdentityPolicy.V1(GoldenDonors().Concat(new[] { new CitizenIdentity { Id = "c3", Name = "X Y", Age = 30 } })));
        Assert.Throws<ArgumentException>(() => IdentityPolicy.V1(GoldenDonors()).Derive(1, ""));
    }

    [Fact]
    public void DerivedPersonaKeepsTheAppearanceDonorsAgeBand()
    {
        var policy = IdentityPolicy.V1(GoldenDonors());
        for (int i = 1; i <= 500; i++)
        {
            var identity = policy.Derive(77, PersonIds.Visitor(i));
            if (identity.Persona == Persona.Student) Assert.InRange(identity.Age, 18, 25);
            if (identity.Persona == Persona.Retiree) Assert.InRange(identity.Age, 65, 82);
            if (identity.Persona == Persona.Parent) Assert.InRange(identity.Age, 27, 50);
        }
    }

    [Fact]
    public void DerivedIdentityIsNeverPersistedOrWired()
    {
        // B6, and F5c-lite's boundary: schema 26 persists the person shape (Core/PersonTable.cs, Core/Model.cs,
        // Persistence/PersonWire26.cs) and the inspector shows kind/household/workplace words (Core/ResidentInsight.cs).
        // No routine, choice, money, Runtime or Compatibility path reads these types. F5f's active-journey index
        // (Core/PhysicalJourneys.cs) classifies person IDs to withhold visitors from bodies. Derived identity is never
        // drawn outside the Core identity files, so no saved field can carry it.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Core", "IdentityPolicy.cs"))) root = root.Parent;
        Assert.NotNull(root);
        var names = new[] { "IdentityPolicy", "OwnershipValidator", "OwnershipInput", "PersonIncome", "VisitorStay", "PersonKind",
            "HouseholdIds", "PersonIds", "PersonFacts", "NativeEmployment", "new Employment", "EmploymentKind" };
        var allowed = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Core/PersonTable.cs"] = names,
            ["Core/Model.cs"] = new[] { "IdentityPolicy" },
            ["Core/PhysicalJourneys.cs"] = new[] { "PersonKind", "PersonIds" },
            ["Core/ResidentInsight.cs"] = new[] { "PersonKind", "HouseholdIds", "EmploymentKind" },
            // The codec checks the saved person table against the F5b ownership validator (rules 1-5).
            ["Persistence/PersonWire26.cs"] = new[] { "IdentityPolicy", "VisitorStay", "PersonKind", "PersonIds", "NativeEmployment", "new Employment", "EmploymentKind",
                "OwnershipValidator", "OwnershipInput", "PersonFacts" },
            // F5d: writer 27 checks the visitor routines against the same contract; money reads each person's own
            // income source (section 3 Resources).
            ["Persistence/PersonWire27.cs"] = new[] { "IdentityPolicy", "PersonKind", "PersonIds", "NativeEmployment", "EmploymentKind",
                "OwnershipValidator", "OwnershipInput", "PersonFacts" },
            ["Core/HouseholdFinance.cs"] = new[] { "PersonIncome", "new Employment", "EmploymentKind" },
            // F7c: the save snapshot copies the saved policy version and each visitor's stored stay and employment; it derives nothing.
            ["Persistence/SaveSnapshot.cs"] = new[] { "IdentityPolicy", "VisitorStay", "NativeEmployment", "new Employment" },
        };
        var files = new[] { "Runtime", "Persistence", "Compatibility" }
            .SelectMany(d => Directory.GetFiles(Path.Combine(root!.FullName, d), "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Path.Combine(root!.FullName, "Core"), "*.cs")
                // F5d: Core/VisitorSchedules.cs owns the visitor routines and derives their identity (contract B6).
                .Where(f => !new[] { "PersonIdentity.cs", "IdentityPolicy.cs", "OwnershipValidator.cs", "VisitorSchedules.cs" }.Contains(Path.GetFileName(f))));
        foreach (var file in files)
        {
            string text = File.ReadAllText(file);
            string relative = Path.GetRelativePath(root!.FullName, file).Replace(Path.DirectorySeparatorChar, '/');
            var permitted = allowed.TryGetValue(relative, out var list) ? list : Array.Empty<string>();
            foreach (var name in names)
                if (Array.IndexOf(permitted, name) < 0)
                    Assert.False(name == "new Employment" ? System.Text.RegularExpressions.Regex.IsMatch(text, @"new Employment\s*[({]")
                        : text.Contains(name, StringComparison.Ordinal), relative + " references " + name);
            foreach (var draw in new[] { "IdentityPolicy.V1", ".Derive(", "IdentityPolicy.Draw" })
                Assert.False(text.Contains(draw, StringComparison.Ordinal), relative + " draws derived identity: " + draw);
        }
    }

    // ---- Per-person income source ----

    [Fact]
    public void IncomeSelectionEqualsTodaysRuleForResidents()
    {
        foreach (Persona persona in Enum.GetValues(typeof(Persona)))
            foreach (int age in new[] { 16, 17, 18, 30, 70 })
                foreach (var dayPlace in new[] { "", "work:1" })
                {
                    var person = new CitizenState { Identity = new CitizenIdentity { Id = "pc:1", Persona = persona, Age = age }, DayPlaceId = dayPlace };
                    var today = HouseholdFinance.Create(person, 3, 0, null).IncomeSource;
                    var employment = dayPlace.Length == 0 ? null : new Employment { PersonId = "pc:1", WorkplaceId = dayPlace,
                        Kind = persona == Persona.Student ? EmploymentKind.Study : EmploymentKind.Work };
                    Assert.Equal(today, PersonIncome.Select(persona, age, employment, null));
                }
    }

    [Fact]
    public void IncomeSelectionUsesOwnEmploymentThenCarriedSource()
    {
        var work = new Employment { PersonId = "consumer:c1:1", WorkplaceId = "work:1", Kind = EmploymentKind.Work };
        var ended = new Employment { PersonId = "consumer:c1:1", WorkplaceId = "work:1", Kind = EmploymentKind.Work, StartDay = 1, EndDay = 4 };
        var study = new Employment { PersonId = "consumer:c1:1", WorkplaceId = "school:1", Kind = EmploymentKind.Study };
        Assert.Equal(ResidentIncomeSource.ExternalEmployment, PersonIncome.Select(Persona.Commuter, 30, work, ResidentIncomeSource.ExternalSupport));
        // A carried outside source survives until the person's own employment outcome replaces it.
        Assert.Equal(ResidentIncomeSource.ExternalEmployment, PersonIncome.Select(Persona.Commuter, 30, null, ResidentIncomeSource.ExternalEmployment));
        Assert.Equal(ResidentIncomeSource.ExternalEmployment, PersonIncome.Select(Persona.Retiree, 70, null, ResidentIncomeSource.ExternalEmployment));
        Assert.Equal(ResidentIncomeSource.ExternalSupport, PersonIncome.Select(Persona.Commuter, 30, ended, null));
        Assert.Equal(ResidentIncomeSource.ExternalSupport, PersonIncome.Select(Persona.Commuter, 30, study, null));
        Assert.Equal(ResidentIncomeSource.ExternalPension, PersonIncome.Select(Persona.Retiree, 70, work, null));
        Assert.Equal(ResidentIncomeSource.ExternalSupport, PersonIncome.Select(Persona.Commuter, 17, work, null));
    }

    // ---- Ownership validator: rules 1-5 and duplication guards ----

    private static VisitorStay Stay(VisitorPattern pattern = VisitorPattern.Commuter)
        => new VisitorStay { OriginPortalId = "station:1", Pattern = pattern, Presence = VisitorPresence.Dormant, FirstDay = 2, NextStayStart = 4300 };

    private sealed class City
    {
        public List<PersonFacts> Persons = new()
        {
            new PersonFacts("c0", PersonKind.Resident, "home:1", hasCar: true),
            new PersonFacts("consumer:c0:1", PersonKind.Visitor, "", Stay()),
            new PersonFacts("consumer:c0:2", PersonKind.Visitor, "", Stay(VisitorPattern.DayVisit)),
            new PersonFacts("pc:1", PersonKind.Resident, "home:1"),
            new PersonFacts("pv:1", PersonKind.Visitor, "", Stay()),
        };
        public List<WalletFacts> Wallets = new();
        public List<RosterFacts> Roster = new();
        public List<DemandUnitFacts> Intents = new();
        public List<Employment> Employments = new()
        {
            new Employment { PersonId = "c0", WorkplaceId = "work:1", Kind = EmploymentKind.Work, StartDay = 1 },
            new Employment { PersonId = "consumer:c0:1", WorkplaceId = "work:2", Kind = EmploymentKind.Work, StartDay = 3 },
            new Employment { PersonId = "pc:1", WorkplaceId = "school:1", Kind = EmploymentKind.Study, StartDay = 0 },
            new Employment { PersonId = "pv:1", WorkplaceId = "work:1", Kind = EmploymentKind.Work, StartDay = 0, EndDay = 2 },
        };
        public List<WagePosting> Wages = new() { new WagePosting("c0", 5, "work:1"), new WagePosting("consumer:c0:1", 5, "work:2"), new WagePosting("pv:1", 2, "work:1") };
        public List<WorkplaceFacts> Workplaces = new()
        {
            new WorkplaceFacts("school:1", PlaceKind.Study), new WorkplaceFacts("shop:own", PlaceKind.Work, playerOwned: true),
            new WorkplaceFacts("work:1", PlaceKind.Work), new WorkplaceFacts("work:2", PlaceKind.Work),
        };
        public List<string> Homes = new() { "home:1", "home:2" };
        public List<string> Portals = new() { "station:1", "station:2" };
        public bool Finances = true, Food = true;

        public City()
        {
            foreach (var p in Persons)
            {
                Wallets.Add(new WalletFacts(MoneyOwnerKind.Person, p.Id));
                Roster.Add(new RosterFacts(p.Id, p.Id, 0));
                Intents.Add(new DemandUnitFacts(MealRhythm.IntentId(p.Id, 3, MealKind.Lunch), p.Id));
                Intents.Add(new DemandUnitFacts(MealRhythm.IntentId(p.Id, 3, MealKind.Legacy), p.Id));
            }
        }

        public OwnershipInput Input() => new()
        {
            Persons = Persons, Wallets = Wallets, Roster = Roster, Intents = Intents, Employments = Employments, Wages = Wages,
            Workplaces = Workplaces, HomePlaceIds = Homes, PortalIds = Portals, FinancesEnabled = Finances, FoodEnabled = Food
        };
    }

    private static List<OwnershipRule> Rules(City city)
    {
        var validator = new OwnershipValidator();
        bool valid = validator.Validate(city.Input());
        Assert.Equal(valid, validator.ViolationCount == 0);
        return validator.Violations.Select(v => v.Rule).ToList();
    }

    private static void AssertOnly(City city, OwnershipRule rule, string subject)
    {
        var validator = new OwnershipValidator();
        Assert.False(validator.Validate(city.Input()));
        var violation = Assert.Single(validator.Violations);
        Assert.Equal(rule, violation.Rule);
        Assert.Equal(subject, violation.Subject);
    }

    [Fact]
    public void ValidCityPassesAllRules() => Assert.Empty(Rules(new City()));

    [Fact]
    public void FinancesAndFoodOffRequireNoWalletsOrRoster()
    {
        var city = new City { Finances = false, Food = false };
        city.Wallets.Clear(); city.Roster.Clear();
        Assert.Empty(Rules(city));
    }

    [Fact]
    public void SecondBudgetIsRefused()
    {
        var city = new City();
        city.Wallets.Add(new WalletFacts(MoneyOwnerKind.Person, "pc:1"));
        AssertOnly(city, OwnershipRule.DuplicateWallet, "pc:1");
    }

    [Fact]
    public void OrphanBudgetIsRefused()
    {
        var city = new City();
        city.Wallets.Add(new WalletFacts(MoneyOwnerKind.Person, "pc:2"));
        AssertOnly(city, OwnershipRule.OrphanWallet, "pc:2");
    }

    [Fact]
    public void MissingBudgetIsRefusedWithFinancesOn()
    {
        var city = new City();
        city.Wallets.RemoveAll(w => w.OwnerId == "pv:1");
        AssertOnly(city, OwnershipRule.MissingWallet, "pv:1");
    }

    [Fact]
    public void BudgetWithFinancesOffIsRefused()
    {
        var city = new City { Finances = false };
        city.Wallets.RemoveAll(w => w.OwnerId != "c0");
        AssertOnly(city, OwnershipRule.WalletWithoutFinances, "c0");
    }

    [Theory]
    [InlineData(MoneyOwnerKind.Household, "h:home:1", OwnershipRule.HouseholdMoney)]
    [InlineData(MoneyOwnerKind.Workplace, "work:1", OwnershipRule.WorkplaceMoney)]
    [InlineData(MoneyOwnerKind.VisitorStay, "pv:1", OwnershipRule.VisitorStayMoney)]
    public void OnlyPersonsHoldMoney(MoneyOwnerKind owner, string id, OwnershipRule rule)
    {
        var city = new City();
        city.Wallets.Add(new WalletFacts(owner, id));
        AssertOnly(city, rule, id);
    }

    [Fact]
    public void RosterIsABijectionOfOwnSlotZeroRecords()
    {
        var duplicate = new City(); duplicate.Roster.Add(new RosterFacts("pv:1", "pv:1", 0));
        AssertOnly(duplicate, OwnershipRule.DuplicateRoster, "pv:1");
        var orphan = new City(); orphan.Roster.Add(new RosterFacts("pv:9", "pv:9", 0));
        AssertOnly(orphan, OwnershipRule.OrphanRoster, "pv:9");
        var missing = new City(); missing.Roster.RemoveAll(r => r.ResidentId == "c0");
        AssertOnly(missing, OwnershipRule.MissingRoster, "c0");
        var anchored = new City(); anchored.Roster[1] = new RosterFacts("consumer:c0:1", "c0", 1);
        AssertOnly(anchored, OwnershipRule.RosterNotOwn, "consumer:c0:1");
        var noFood = new City { Food = false }; noFood.Roster.RemoveAll(r => r.ResidentId != "pc:1");
        AssertOnly(noFood, OwnershipRule.RosterWithoutFood, "pc:1");
    }

    [Fact]
    public void IntentWithTwoOwnersIsRefused()
    {
        var city = new City();
        // The former anchor claims an intent its migrated consumer already owns.
        string owned = MealRhythm.IntentId("consumer:c0:1", 3, MealKind.Lunch);
        city.Intents.Add(new DemandUnitFacts(owned, "c0"));
        AssertOnly(city, OwnershipRule.IntentWithTwoOwners, owned);
    }

    [Fact]
    public void IntentOwnershipIsExact()
    {
        var duplicate = new City();
        string id = MealRhythm.IntentId("pc:1", 3, MealKind.Lunch);
        duplicate.Intents.Add(new DemandUnitFacts(id, "pc:1"));
        AssertOnly(duplicate, OwnershipRule.DuplicateIntent, id);
        var orphan = new City();
        string missing = MealRhythm.IntentId("pv:7", 3, MealKind.Lunch);
        orphan.Intents.Add(new DemandUnitFacts(missing, "pv:7"));
        AssertOnly(orphan, OwnershipRule.IntentWithoutOwner, missing);
        var copied = new City();
        string anchors = MealRhythm.IntentId("c0", 4, MealKind.Dinner);
        copied.Intents.Add(new DemandUnitFacts(anchors, "consumer:c0:2"));
        AssertOnly(copied, OwnershipRule.IntentNotDerived, anchors);
    }

    [Fact]
    public void IntentDerivationMatchesEscapedOwnerIds()
    {
        var owners = new List<string> { "c0", "pc:12", "consumer:pc%3A12:2", "pv:4", "a b", "x~y_z-.", "été", "\U0001F600" };
        for (char c = ' '; c < '\u007f'; c++) owners.Add("p" + c + "q");
        foreach (var owner in owners)
        {
            string id = MealRhythm.IntentId(owner, 12, MealKind.Breakfast);
            Assert.True(OwnershipValidator.DerivedFrom(id, owner), owner);
            Assert.True(OwnershipValidator.DerivedFrom(FoodDemand.IntentId(owner, 12), owner), owner);
            Assert.False(OwnershipValidator.DerivedFrom(id, owner + "x"), owner);
            Assert.False(OwnershipValidator.DerivedFrom("food:" + Uri.EscapeDataString(owner) + ":", owner), owner);
            Assert.False(OwnershipValidator.DerivedFrom("food:" + Uri.EscapeDataString(owner), owner), owner);
        }
        Assert.False(OwnershipValidator.DerivedFrom("food:pc%3a12:lunch:3", "pc:12"));
        Assert.False(OwnershipValidator.DerivedFrom("meal:pc%3A12:lunch:3", "pc:12"));
    }

    [Fact]
    public void TwoActiveEmploymentsAreRefused()
    {
        var city = new City();
        city.Employments.Add(new Employment { PersonId = "c0", WorkplaceId = "work:2", Kind = EmploymentKind.Work, StartDay = 4 });
        AssertOnly(city, OwnershipRule.TwoActiveEmployments, "c0");
    }

    [Fact]
    public void EndedEmploymentsDoNotCountAsActive()
    {
        var city = new City();
        city.Employments.Add(new Employment { PersonId = "pv:1", WorkplaceId = "work:2", Kind = EmploymentKind.Work, StartDay = 3 });
        city.Employments.Add(new Employment { PersonId = "c0", WorkplaceId = "work:2", Kind = EmploymentKind.Work, StartDay = 0, EndDay = 0 });
        Assert.Empty(Rules(city));
    }

    [Fact]
    public void WageBearingStudyIsRefused()
    {
        var city = new City();
        city.Wages.Add(new WagePosting("pc:1", 5, "school:1"));
        AssertOnly(city, OwnershipRule.WageOnStudy, "pc:1");
    }

    [Fact]
    public void WagesAreKeyedByPersonAndDay()
    {
        var duplicate = new City();
        duplicate.Wages.Add(new WagePosting("c0", 5, "work:1"));
        AssertOnly(duplicate, OwnershipRule.DuplicateWage, "c0");
        var unemployed = new City();
        unemployed.Wages.Add(new WagePosting("pv:1", 3, "work:1"));
        AssertOnly(unemployed, OwnershipRule.WageWithoutEmployment, "pv:1");
        var elsewhere = new City();
        elsewhere.Wages.Add(new WagePosting("c0", 6, "work:2"));
        AssertOnly(elsewhere, OwnershipRule.WageWithoutEmployment, "c0");
        var nobody = new City();
        nobody.Wages.Add(new WagePosting("pv:5", 6, "work:2"));
        AssertOnly(nobody, OwnershipRule.WageWithoutEmployment, "pv:5");
    }

    [Fact]
    public void NativeEmploymentOtherThanNoneIsRefused()
    {
        var city = new City();
        city.Employments[0].NativeEmployment = (NativeEmployment)1;
        AssertOnly(city, OwnershipRule.NativeEmploymentNotNone, "c0");
    }

    [Fact]
    public void EmploymentAtAPlayerOwnedBusinessIsRefused()
    {
        var city = new City();
        city.Employments[0].WorkplaceId = "shop:own";
        city.Wages.RemoveAll(w => w.PersonId == "c0");
        AssertOnly(city, OwnershipRule.PlayerOwnedWorkplace, "c0");
    }

    [Fact]
    public void EmploymentNeedsAMatchingSavedWorkplaceAndAPerson()
    {
        var unknown = new City();
        unknown.Employments[0].WorkplaceId = "work:9"; unknown.Wages.RemoveAll(w => w.PersonId == "c0");
        AssertOnly(unknown, OwnershipRule.UnknownWorkplace, "c0");
        var mismatch = new City();
        mismatch.Employments[2].WorkplaceId = "work:2";
        AssertOnly(mismatch, OwnershipRule.EmploymentKindMismatch, "pc:1");
        var orphan = new City();
        orphan.Employments.Add(new Employment { PersonId = "pv:8", WorkplaceId = "work:1", Kind = EmploymentKind.Work });
        AssertOnly(orphan, OwnershipRule.EmploymentWithoutPerson, "pv:8");
        var backwards = new City();
        backwards.Employments[3].EndDay = -2;
        backwards.Wages.RemoveAll(w => w.PersonId == "pv:1");
        AssertOnly(backwards, OwnershipRule.InvalidEmployment, "pv:1");
    }

    [Fact]
    public void DayVisitorsNeverHoldEmploymentButCommutersMay()
    {
        var city = new City();
        city.Employments.Add(new Employment { PersonId = "consumer:c0:2", WorkplaceId = "work:2", Kind = EmploymentKind.Work });
        AssertOnly(city, OwnershipRule.DayVisitorEmployment, "consumer:c0:2");
        Assert.Empty(Rules(new City()));
    }

    [Fact]
    public void ResidentAndVisitorShapesAreStrict()
    {
        var homeless = new City(); homeless.Persons[3] = new PersonFacts("pc:1", PersonKind.Resident, "");
        AssertOnly(homeless, OwnershipRule.ResidentShape, "pc:1");
        var staying = new City(); staying.Persons[3] = new PersonFacts("pc:1", PersonKind.Resident, "home:1", Stay());
        AssertOnly(staying, OwnershipRule.ResidentShape, "pc:1");
        var housed = new City(); housed.Persons[4] = new PersonFacts("pv:1", PersonKind.Visitor, "home:2", Stay());
        AssertOnly(housed, OwnershipRule.VisitorShape, "pv:1");
        var stayless = new City(); stayless.Persons[4] = new PersonFacts("pv:1", PersonKind.Visitor);
        AssertOnly(stayless, OwnershipRule.VisitorShape, "pv:1");
        var driving = new City(); driving.Persons[4] = new PersonFacts("pv:1", PersonKind.Visitor, "", Stay(), hasCar: true);
        AssertOnly(driving, OwnershipRule.VisitorCar, "pv:1");
        var badStay = new City(); badStay.Employments.RemoveAt(3); badStay.Wages.RemoveAt(2); badStay.Persons[4] = new PersonFacts("pv:1", PersonKind.Visitor, "",
            new VisitorStay { OriginPortalId = "station:1", Pattern = (VisitorPattern)7, NextStayStart = 1 });
        AssertOnly(badStay, OwnershipRule.InvalidVisitorStay, "pv:1");
        var nanStay = new City(); nanStay.Persons[4] = new PersonFacts("pv:1", PersonKind.Visitor, "",
            new VisitorStay { OriginPortalId = "station:1", NextStayStart = double.NaN });
        AssertOnly(nanStay, OwnershipRule.InvalidVisitorStay, "pv:1");
    }

    [Fact]
    public void IdsMustFitTheirKind()
    {
        var city = new City();
        city.Persons[4] = new PersonFacts("pv:1", PersonKind.Resident, "home:2");
        city.Employments.RemoveAt(3); city.Wages.RemoveAt(2);
        AssertOnly(city, OwnershipRule.PersonIdFormat, "pv:1");
    }

    [Fact]
    public void ReferencesAreSavedIdsOnly()
    {
        var home = new City(); home.Persons[3] = new PersonFacts("pc:1", PersonKind.Resident, "home:9");
        AssertOnly(home, OwnershipRule.UnknownHome, "pc:1");
        var portal = new City(); portal.Persons[4] = new PersonFacts("pv:1", PersonKind.Visitor, "",
            new VisitorStay { OriginPortalId = "station:9", NextStayStart = 1 });
        AssertOnly(portal, OwnershipRule.UnknownPortal, "pv:1");
    }

    [Fact]
    public void UnorderedOrDuplicatePersonsStopValidation()
    {
        var swapped = new City();
        (swapped.Persons[0], swapped.Persons[1]) = (swapped.Persons[1], swapped.Persons[0]);
        AssertOnly(swapped, OwnershipRule.PersonOrder, "c0");
        var duplicate = new City(); duplicate.Persons.Insert(4, duplicate.Persons[3]);
        AssertOnly(duplicate, OwnershipRule.PersonOrder, "pc:1");
        var catalogue = new City(); catalogue.Portals.Reverse();
        AssertOnly(catalogue, OwnershipRule.CatalogueOrder, "");
    }

    [Fact]
    public void ResultIsIndependentOfFactListOrder()
    {
        var city = new City();
        city.Wallets.Add(new WalletFacts(MoneyOwnerKind.Person, "pc:1"));
        city.Intents.Add(new DemandUnitFacts(MealRhythm.IntentId("consumer:c0:1", 3, MealKind.Lunch), "pc:1"));
        var expected = Rules(city).OrderBy(r => r).ToList();
        city.Wallets.Reverse(); city.Roster.Reverse(); city.Intents.Reverse(); city.Employments.Reverse(); city.Wages.Reverse();
        Assert.Equal(expected, Rules(city).OrderBy(r => r).ToList());
        Assert.Equal(new[] { OwnershipRule.DuplicateWallet, OwnershipRule.IntentWithTwoOwners }, expected);
    }

    [Fact]
    public void ValidatorReuseCarriesNoStateBetweenCalls()
    {
        var validator = new OwnershipValidator();
        var bad = new City();
        bad.Wallets.Add(new WalletFacts(MoneyOwnerKind.Person, "c0"));
        bad.Employments.Add(new Employment { PersonId = "c0", WorkplaceId = "work:2", Kind = EmploymentKind.Work });
        bad.Wages.Add(new WagePosting("c0", 5, "work:1"));
        Assert.False(validator.Validate(bad.Input()));
        Assert.Equal(3, validator.ViolationCount);
        Assert.True(validator.Validate(new City().Input()));
        Assert.Empty(validator.Violations);
    }

    [Fact]
    public void StoredViolationsAreBoundedButCounted()
    {
        var city = new City();
        for (int i = 0; i < 100; i++) city.Wallets.Add(new WalletFacts(MoneyOwnerKind.Household, "h:home:" + i));
        var validator = new OwnershipValidator();
        Assert.False(validator.Validate(city.Input()));
        Assert.Equal(100, validator.ViolationCount);
        Assert.Equal(OwnershipValidator.StoredViolationLimit, validator.Violations.Count);
    }
}
