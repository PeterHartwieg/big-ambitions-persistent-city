using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5c-lite (#92): the person table. The factor-1 roster rule, the strict per-kind reader, the named bounds at
/// and one over, fabricated arrival tuples, and the refusal of older development cities with their food authority left
/// native.</summary>
public sealed class PersonTableTests
{
    // ---- Fixtures ----

    /// <summary>A fresh seeded city of the trace test, run for 30 hours (meals, budgets, journeys, visitors).</summary>
    internal static CitySimulator Running(int factor, bool food = true, double hours = 30)
    {
        var (state, market) = FreshCityTraceTests.Fresh(factor, food);
        // Visitors (K-1 per resident) are persons with their own routines; they arrive through a portal.
        if (factor > 1) PortalPlaces.Add(state, VisitorRoutineTests.Stations);
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        if (food) city.AttachFoodMarket(market);
        double until = state.Minute + hours * 60;
        while (!city.AdvanceTo(until, 64).CaughtUp) { }
        return city;
    }

    private static string GoldenPath => Path.Combine(AppContext.BaseDirectory, "Writer26", "golden.json");

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void RefusedAndPreserved(string payload, CityState replacement, SaveReadStatus expected, string? error = null)
    {
        var read = DevelopmentChain.Read(payload);
        Assert.True(read.Status == expected, read.Status + ": " + read.Error); Assert.Null(read.State);
        if (error != null) Assert.Contains(error, read.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "kept", [FoodAuthorityStore.Key] = "manifest" };
        // The product refuses a payload with another schema number as an older development city before reading its shape.
        var product = SaveCodec.DevelopmentSchemaOf(payload) != null ? SaveReadStatus.DevelopmentSchema : expected;
        Assert.Equal(product, SaveSession.Open(data, replacement.PopulationKey).InitialReadResult.Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Same(payload, data[SaveStore.StateKey]); Assert.Equal("kept", data["rlo:state"]); Assert.Equal("manifest", data[FoodAuthorityStore.Key]);
    }

    private static void Invalid(string payload, CityState replacement, string? error = null) => RefusedAndPreserved(payload, replacement, SaveReadStatus.Invalid, error);

    private static string Bits(double minute) => unchecked((ulong)BitConverter.DoubleToInt64Bits(minute)).ToString("X16", CultureInfo.InvariantCulture);

    private static int PlaceIndex(CityState state, string id) => state.Places.Select(p => p.Id).OrderBy(p => p, StringComparer.Ordinal).ToList().IndexOf(id);

    private static string WithVisitor(string payload, int index, Func<string[], string> change)
    {
        var root = SaveWire.Parse(payload);
        var parts = ((string)root["Visitors"]![index]!).Split('|');
        root["Visitors"]![index] = change(parts);
        return root.ToString(Formatting.None);
    }

    // ---- Golden current writer ----

    // ---- Fresh cities in the new shape: factor-1 roster rule ----

    [Theory]
    [InlineData(1, true)] [InlineData(1, false)]
    public void FreshCitiesUseOneRosterRecordPerPersonAndRoundTrip(int factor, bool food)
    {
        var city = Running(factor, food, 26);
        var state = city.State;
        int residents = state.Citizens.Count;
        Assert.Equal(food ? (factor - 1) * residents : 0, state.Visitors.Count);
        Assert.Equal(state.Visitors.Select(v => v.Id).OrderBy(id => id, StringComparer.Ordinal), state.Visitors.Select(v => v.Id));
        foreach (var visitor in state.Visitors)
        {
            Assert.Equal(PersonIdFormat.LegacyConsumer, PersonIds.Classify(visitor.Id));
            Assert.Equal(PersonPolicy.MigratedConsumerPattern, visitor.Stay.Pattern);
            Assert.Equal(VisitorPresence.Active, visitor.Stay.Presence);
            Assert.Equal(1, visitor.Stay.FirstDay); Assert.Equal(0, BitConverter.DoubleToInt64Bits(visitor.Stay.NextStayStart));
            Assert.Null(visitor.Employment); Assert.Null(visitor.CarriedIncomeSource); Assert.Null(visitor.LastArrival);
        }
        if (food) Assert.Equal(PersonTable.PersonCount(state), state.Food!.Residents.Count);
        string payload = DevelopmentChain.Write(state);
        Assert.Contains("\"Schema\":" + CityState.CurrentSchema + ",", payload);
        Assert.DoesNotContain("RepresentationFactor", payload);
        // Roster tuple v3: version plus 34 slots, no anchor or slot (routine stops still name their resident anchor).
        if (food) Assert.All(SaveWire.Parse(payload)["Food"]!["Residents"]!, row => { Assert.Equal(3, (int)row[0]!); Assert.Equal(35, ((JArray)row).Count); });
        Assert.DoesNotContain("EmploymentStartDay", payload);
        Assert.Contains("\"P\":\"1R", payload);
        var read = DevelopmentChain.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, DevelopmentChain.Write(read.State!));
        if (food)
        {
            // Factor-1 roster rule, live and decoded: one own slot-0 record per person and a stored factor of 1. The host
            // and slot of today's represented excursion are the scheduling adapter, identical across the reload.
            foreach (var city2 in new[] { state, read.State! })
            {
                Assert.Equal(1, city2.Food!.RepresentationFactor);
                Assert.All(city2.Food.Residents, m => { Assert.Equal(m.ResidentId, m.AnchorId); Assert.Equal(0, m.Slot);
                    Assert.Equal(m.ResidentId, FoodRepresentation.ConsumerId(m.HostId, m.HostSlot)); });
                Assert.Equal(new int[0], OwnershipRules(city2));
            }
            var before = state.Food!.Residents.ToDictionary(m => m.ResidentId, m => (m.HostId, m.HostSlot), StringComparer.Ordinal);
            Assert.All(read.State!.Food!.Residents, m => Assert.Equal(before[m.ResidentId], (m.HostId, m.HostSlot)));
            Assert.Equal(factor - 1, read.State.Food.Residents.Max(m => m.HostSlot));
        }
        Assert.Equal(state.Citizens.Select(c => c.EmploymentStartDay), read.State!.Citizens.Select(c => c.EmploymentStartDay));
        // The reloaded city keeps running on today's schedules.
        var resumed = new CitySimulator(read.State, new FreshCityTraceTests.Walk());
        if (food) resumed.AttachFoodMarket(new WeightedFoodChoiceTests.Market());
        while (!resumed.AdvanceTo(read.State.Minute + 60, 64).CaughtUp) { }
    }

    /// <summary>F5b's OwnershipValidator over the saved person table; returns the violated rules other than the two the
    /// codec documents (opaque resident IDs, represented visitors without a portal).</summary>
    private static int[] OwnershipRules(CityState state)
    {
        var persons = state.Citizens.Select(c => new PersonFacts(c.Identity.Id, PersonKind.Resident, c.HomeId))
            .Concat(state.Visitors.Select(v => new PersonFacts(v.Id, PersonKind.Visitor, "", v.Stay))).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var food = state.Food!;
        var input = new OwnershipInput { Persons = persons, FoodEnabled = true, FinancesEnabled = food.HouseholdFinanceVersion != 0,
            HomePlaceIds = state.Places.Where(p => p.Kind == PlaceKind.Home).Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToList(),
            Roster = food.Residents.Select(m => new RosterFacts(m.ResidentId, m.AnchorId, m.Slot)).ToList(),
            Wallets = food.Residents.Where(m => m.Budget != null).Select(m => new WalletFacts(MoneyOwnerKind.Person, m.ResidentId)).ToList(),
            Intents = food.Residents.Select(m => new DemandUnitFacts(m.IntentId, m.ResidentId)).ToList() };
        var validator = new OwnershipValidator(); validator.Validate(input);
        return validator.Violations.Where(v => v.Rule != OwnershipRule.PersonIdFormat && v.Rule != OwnershipRule.UnknownPortal).Select(v => (int)v.Rule).ToArray();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void MarketDiagnosticReportsTheSchedulingFactorBesideTheStoredRosterFactor(int factor)
    {
        var state = Running(factor, true, 2).State;
        var capture = JObject.FromObject(FoodEconomyInsight.Capture(state));
        Assert.Equal(factor, (int)capture["representationFactor"]!);
        Assert.Equal(1, (int)capture["storedRosterFactor"]!);
        Assert.Equal(factor * state.Citizens.Count, (int)capture["representedConsumers"]!);
    }

    [Fact]
    public void WritesRefuseARosterRecordThatIsNotItsOwnPersons()
    {
        var state = Running(2, true, 2).State;
        var visitorMeal = state.Food!.Residents.First(m => state.Visitors.Any(v => v.Id == m.ResidentId));
        visitorMeal.AnchorId = state.Citizens[0].Identity.Id; visitorMeal.Slot = 1; // the old hosted form
        Assert.Contains("factor-1 roster rule", Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(state)).Message);
        visitorMeal.AnchorId = visitorMeal.ResidentId; visitorMeal.Slot = 0;
        state.Food.RepresentationFactor = 2;
        Assert.Contains("one own record per person", Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(state)).Message);
        state.Food.RepresentationFactor = 1;
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(DevelopmentChain.Write(state)).Status);
    }

    /// <summary>Every persona housed, plus one unassigned worker: Work, Study and no Employment all occur.</summary>
    private static CityState Personas()
    {
        var personas = new[] { Persona.Commuter, Persona.Student, Persona.Retiree, Persona.Foodie };
        var state = Population.Create(personas.Select((p, i) => new CitizenIdentity { Id = "c" + i, Name = "Person " + i, Age = p == Persona.Retiree ? 70 : 30, Persona = p }),
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 4 },
                new Place { Id = "work:a", Name = "Office", Kind = PlaceKind.Work, Capacity = 4, Position = new(50, 0, 0) },
                new Place { Id = "study:a", Name = "School", Kind = PlaceKind.Study, Capacity = 4, Position = new(-50, 0, 0) } }, "personas", "0.40.4", 9, 2 * 1440 + 30);
        state.Citizens[3].DayPlaceId = ""; state.Citizens[3].EmploymentStartDay = 0;
        return state;
    }

    [Fact]
    public void ResidentEmploymentStartFollowsJobChangesAndCreation()
    {
        var state = Personas();
        foreach (var person in state.Citizens)
            Assert.Equal(person.DayPlaceId.Length == 0 ? 0 : (int)(person.JoinedMinute / 1440), person.EmploymentStartDay);
        var worker = state.Citizens.First(c => c.DayPlaceId.Length > 0 && c.Identity.Persona != Persona.Student);
        var employment = PersonTable.ResidentEmployment(worker)!;
        Assert.Equal(EmploymentKind.Work, employment.Kind); Assert.Equal(worker.DayPlaceId, employment.WorkplaceId);
        Assert.Equal(PersonTable.PersonaScheduleCode, employment.ScheduleCode); Assert.Equal(NativeEmployment.None, employment.NativeEmployment);
        var student = state.Citizens.Single(c => c.Identity.Persona == Persona.Student);
        Assert.Equal(EmploymentKind.Study, PersonTable.ResidentEmployment(student)!.Kind);
        Assert.Null(PersonTable.ResidentEmployment(state.Citizens.Single(c => c.Identity.Persona == Persona.Retiree)));
        // A job found after a later employment check starts on that check's day; the tuple round-trips it.
        var unassigned = state.Citizens[3]; unassigned.LastEmploymentCheckDay = 2;
        ResidentLifecycle.AssignWorkplace(unassigned, state.Places.Single(p => p.Id == "work:a"));
        Assert.Equal(2, unassigned.EmploymentStartDay);
        var read = DevelopmentChain.Read(DevelopmentChain.Write(state)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(2, read.State!.Citizens[3].EmploymentStartDay);
    }

    // ---- Strict reader: per-kind shape ----

    [Fact]
    public void PerKindShapeRefusesVisitorRowsWithResidentFieldsAndResidentsWithoutIdentity()
    {
        var city = Running(2, true, 2); var state = city.State;
        string payload = DevelopmentChain.Write(state);
        // A visitor in the resident table would carry identity, home, car and recent visits: refused.
        var root = SaveWire.Parse(payload); root["Citizens"]![0]!["P"] = (string)root["Visitors"]![0]!;
        Invalid(root.ToString(Formatting.None), state, "visitor row cannot carry resident identity");
        root = SaveWire.Parse(payload); root["Citizens"]![0]!["P"] = "1V";
        Invalid(root.ToString(Formatting.None), state, "visitor row cannot carry resident identity");
        // A resident without identity: as a visitor tuple, or a citizen row whose identity is missing or null.
        Invalid(WithVisitor(payload, 0, p => "1R-"), state, "resident row requires identity");
        root = SaveWire.Parse(payload); ((JObject)root["Citizens"]![0]!).Remove("Identity");
        Invalid(root.ToString(Formatting.None), state);
        root = SaveWire.Parse(payload); root["Citizens"]![0]!["Identity"] = null;
        Invalid(root.ToString(Formatting.None), state);
        // Visitor tuples have no identity, home, car or visit slot: any extra field changes the arity.
        Invalid(WithVisitor(payload, 0, p => string.Join("|", p) + "|Ada"), state, "wrong arity");
        Invalid(WithVisitor(payload, 0, p => string.Join("|", p.Take(5))), state, "wrong arity");
        // Unknown kind and version.
        Invalid(WithVisitor(payload, 0, p => "1X|" + string.Join("|", p.Skip(1))), state, "Unknown person kind");
        Invalid(WithVisitor(payload, 0, p => "2V|" + string.Join("|", p.Skip(1))), state, "version");
        root = SaveWire.Parse(payload); root["Citizens"]![0]!["P"] = "1Q-";
        Invalid(root.ToString(Formatting.None), state, "Unknown person kind");
    }

    [Fact]
    public void ResidentTupleMustMatchItsDayPlaceAndFixedCodes()
    {
        var state = Personas(); string payload = DevelopmentChain.Write(state);
        int worker = state.Citizens.FindIndex(c => c.DayPlaceId.Length > 0), retiree = state.Citizens.FindIndex(c => c.DayPlaceId.Length == 0);
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(payload).Status);
        string Set(int row, string tuple) { var r = SaveWire.Parse(payload); r["Citizens"]![row]!["P"] = tuple; return r.ToString(Formatting.None); }
        int start = state.Citizens[worker].EmploymentStartDay;
        Invalid(Set(worker, "1R-"), state, "Employment differs");
        Invalid(Set(retiree, "1RE" + start + ".0.0"), state, "Employment differs");
        Invalid(Set(worker, "1RE" + start + ".1.0"), state, "Invalid resident employment");
        Invalid(Set(worker, "1RE" + start + ".0.1"), state, "Invalid resident employment");
        Invalid(Set(worker, "1RE0" + start + ".0.0"), state, "Invalid resident employment start");
        Invalid(Set(worker, "1RE" + (state.Citizens[worker].LastEmploymentCheckDay + 1) + ".0.0"), state, "Employment start is outside");
        Invalid(Set(worker, "1RE-1.0.0"), state);
        Invalid(Set(worker, "1R" + new string('-', 40)), state);
        var r = SaveWire.Parse(payload); ((JObject)r["Citizens"]![0]!).Remove("P");
        Invalid(r.ToString(Formatting.None), state);
        // A duplicate field cannot pass the token check even with equal values.
        string tuple = "\"P\":\"" + (string)SaveWire.Parse(payload)["Citizens"]![0]!["P"]! + "\"";
        int at = payload.IndexOf(tuple, StringComparison.Ordinal);
        Invalid(payload.Insert(at, tuple + ","), state, "Duplicate field" /* F7d: a duplicated body field becomes a duplicated compact shape field */);
    }

    // ---- Bounds: named, at the bound and one over, before allocation ----

    [Fact]
    public void NamedBoundsSplitTheFormerCitizenCap()
    {
        Assert.Equal(150000, SaveCodec.MaxPersons); Assert.Equal(50000, SaveCodec.MaxResidents); Assert.Equal(100000, SaveCodec.MaxVisitors);
        Assert.Equal(50000, SaveCodec.MaxTrafficVehicles); Assert.Equal(256, SaveCodec.MaxPortals);
        Assert.Equal(SaveCodec.MaxPersons, SaveCodec.MaxResidents + SaveCodec.MaxVisitors);
        Assert.Equal(PopulationCensusPolicy.MaximumPersons, SaveCodec.MaxPersons); Assert.Equal(HousingPolicy.MaximumResidents, SaveCodec.MaxResidents);
    }

    private static string Spliced(string payload, string field, string element, int count)
    {
        var root = SaveWire.Parse(payload); root[field] = new JArray("@@" + field + "@@");
        string text = root.ToString(Formatting.None);
        var rows = new StringBuilder(count * (element.Length + 1));
        for (int i = 0; i < count; i++) { if (i > 0) rows.Append(','); rows.Append(element); }
        return text.Replace("\"@@" + field + "@@\"", rows.ToString());
    }

    [Fact]
    public void WritesCheckTheNamedBoundsBeforeAnyPersonWork()
    {
        var state = FreshCityTraceTests.Fresh(1, false).State; var person = state.Citizens[0];
        var original = state.Citizens.ToList();
        void Fill(int residents, int visitors)
        {
            state.Citizens = Enumerable.Repeat(person, residents).ToList();
            state.Visitors = Enumerable.Range(0, visitors).Select(i => new VisitorPerson { Id = "consumer:r:" + (i + 1) }).ToList();
        }
        string Error(int residents, int visitors)
        {
            Fill(residents, visitors);
            try { DevelopmentChain.Write(state); return ""; } catch (SaveValidationException e) { return e.Message; }
        }
        Assert.DoesNotContain("exceed the bound", Error(SaveCodec.MaxResidents, SaveCodec.MaxVisitors));
        Assert.Contains("Persons exceed the bound", Error(SaveCodec.MaxResidents, SaveCodec.MaxVisitors + 1));
        Assert.Contains("Residents exceed the bound", Error(SaveCodec.MaxResidents + 1, 0));
        Assert.Contains("Visitors exceed the bound", Error(1, SaveCodec.MaxVisitors + 1));
        state.Citizens = original; state.Visitors.Clear();
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(DevelopmentChain.Write(state)).Status);
    }

    private static JsonSerializerSettings Settings(CityState state)
    {
        var context = typeof(SaveCodec).GetNestedType("WireContext", BindingFlags.NonPublic)!.GetMethod("FromState")!.Invoke(null, new object[] { state });
        return (JsonSerializerSettings)typeof(SaveCodec).GetMethod("Settings", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new[] { context })!;
    }

    // ---- Record budget: every new schema-26 record is charged by both counters ----

    // ---- Older development schemas: refused, preserved, food authority native ----

    /// <summary>The corpus's full city relabelled with a number an older development build wrote.</summary>
    private static string OlderPayload(int schema)
        => ReaderCorpus.Load().Single(f => f.File == "full.json").Text.Replace(SaveCodec.CompactPrefix, "{\"Schema\":" + schema + ",\"f\":");

    [Fact]
    public void Schema25CityWithOwnedFoodBusinessesIsRefusedAndItsBusinessesReturnNative()
    {
        string payload = OlderPayload(25);
        Assert.Equal(25, SaveCodec.DevelopmentSchemaOf(payload));
        var managed = new FoodAuthority { PlaceId = "address:test:1:shop" };
        managed.Observe("business:test:1:3:bakery:player", true, 8); Assert.True(managed.Boundary(9, true, false, true));
        var notOwned = new FoodAuthority { PlaceId = "address:test:2:shop" };
        notOwned.Observe("business:test:2:3:bakery:player", false, 8);
        var released = new FoodAuthority { PlaceId = "address:test:3:shop" };
        released.Observe("business:test:3:3:bakery:player", true, 2); released.Boundary(3, true, false, true);
        released.Observe(released.Signature, false, 6); released.Boundary(7, true, false, true);
        string manifest = FoodAuthorityStore.Write(new[] { managed, notOwned, released });
        const long loadedHour = 12;
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SaveStore.StateKey] = payload, [FoodAuthorityStore.Key] = manifest,
            ["persistentcity:food-sale-v2:food%3Ac0%3A1%3A0"] = "Pending",
            [AccountingStopStore.Key] = "save-epoch-failed", ["rlo:state"] = "owned by RLO",
        };
        data[SaveEpochStore.Key] = SaveEpochStore.Success(4, 25, loadedHour * 60, "food-live", data);
        var before = new Dictionary<string, string>(data, StringComparer.Ordinal);

        // The product refuses with its own status and message: neither corruption nor a missing city.
        var read = SaveStore.ReadFrom(data, "food-live");
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Null(read.State); Assert.Equal(25, read.SourceSchema);
        Assert.Contains(SaveCodec.DevelopmentSchemaMessage, read.Error);
        var session = SaveSession.Open(data, "food-live");
        Assert.Equal(SaveReadStatus.DevelopmentSchema, session.InitialReadResult.Status);
        var fresh = CurrentSchemaOnlyTests.City(); fresh.PopulationKey = "food-live";
        Assert.False(session.WriteTo(data, fresh).Success); Assert.False(SaveStore.WriteTo(data, fresh).Success);
        Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(payload)).Message);

        // Food authority: no PC authority is taken; the loaded hour stays protected, then native customers return.
        var standDown = new FoodAuthorityStandDown(FoodAuthorityStore.Read(data[FoodAuthorityStore.Key]), loadedHour);
        Assert.Equal(new[] { managed.PlaceId }, standDown.ProtectedPlaces);
        Assert.True(standDown.Suppresses(managed.PlaceId, loadedHour)); Assert.True(standDown.Suppresses(managed.PlaceId, loadedHour - 1));
        Assert.False(standDown.Suppresses(managed.PlaceId, 8)); Assert.False(standDown.Suppresses(managed.PlaceId, loadedHour + 1));
        foreach (var place in new[] { notOwned.PlaceId, released.PlaceId, "address:unknown:9:shop" })
            for (long hour = 0; hour <= loadedHour + 2; hour++) Assert.False(standDown.Suppresses(place, hour));
        Assert.False(standDown.HandBack(loadedHour)); Assert.True(standDown.HandBack(loadedHour + 1)); Assert.False(standDown.HandBack(loadedHour + 2));

        // Every key is byte-identical: payload, manifest, receipt, accounting stop, epoch marker and foreign keys.
        Assert.Equal(before.Count, data.Count);
        foreach (var (key, value) in before) Assert.Same(value, data[key]);
    }

    /// <summary>An unreadable manifest, or one ahead of the loaded clock, beside a schema-25 payload still stands the city
    /// down: nothing is protected, so every player business is native at once, and no key changes.</summary>
    [Theory]
    [InlineData("not json")] [InlineData("[]x")] [InlineData("ahead")]
    public void Schema25CityWithAnUnreadableOrAheadManifestStandsDownWithEveryBusinessNative(string damage)
    {
        string payload = OlderPayload(25);
        const long loadedHour = 12;
        string manifest = damage;
        if (damage == "ahead")
        {
            var row = new FoodAuthority { PlaceId = "address:test:1:shop" };
            row.Observe("business:test:1:3:bakery:player", true, loadedHour + 5); row.Boundary(loadedHour + 6, true, false, true);
            manifest = FoodAuthorityStore.Write(new[] { row });
        }
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
            { [SaveStore.StateKey] = payload, [FoodAuthorityStore.Key] = manifest, [AccountingStopStore.Key] = "save-epoch-failed" };
        var before = new Dictionary<string, string>(data, StringComparer.Ordinal);
        Assert.Equal(25, SaveCodec.DevelopmentSchemaOf(data[SaveStore.StateKey]));
        Assert.Equal(SaveReadStatus.DevelopmentSchema, SaveStore.ReadFrom(data, "food-live").Status);
        var standDown = FoodAuthorityStandDown.FromManifest(() => FoodAuthorityStore.Read(data[FoodAuthorityStore.Key]), loadedHour, out var problem);
        Assert.NotNull(problem);
        Assert.Empty(standDown.ProtectedPlaces);
        for (long hour = 0; hour <= loadedHour + 8; hour++) Assert.False(standDown.Suppresses("address:test:1:shop", hour));
        Assert.Equal(before.Count, data.Count);
        foreach (var (key, value) in before) Assert.Same(value, data[key]);
    }

    /// <summary>Source guard for the Runtime wiring offline tests cannot run: the development check precedes every
    /// accounting-stop, epoch and authority write, and a stood-down save persists nothing.</summary>
    [Fact]
    public void RuntimeRefusesDevelopmentCitiesBeforeAnyKeyIsWritten()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Runtime", "NativeFoodOwnership.cs"))) root = root.Parent;
        Assert.NotNull(root);
        string recovery = File.ReadAllText(Path.Combine(root!.FullName, "Runtime", "AccountingRecovery.cs"));
        int check = recovery.IndexOf("NativeFoodOwnership.StandDown != null", StringComparison.Ordinal);
        Assert.True(check > 0 && check < recovery.IndexOf("AccountingStopStore.IsStopped", StringComparison.Ordinal));
        string start = File.ReadAllText(Path.Combine(root.FullName, "Runtime", "PersistentCityMod.cs"));
        int tryStart = start.IndexOf("private void TryStart()", StringComparison.Ordinal);
        Assert.True(start.IndexOf("if (!CanStartAccountingSave()) return;", tryStart, StringComparison.Ordinal)
            < start.IndexOf("SaveEpochStore.CheckForStart", tryStart, StringComparison.Ordinal));
        string food = File.ReadAllText(Path.Combine(root.FullName, "Runtime", "NativeFoodOwnership.cs"));
        int ensure = food.IndexOf("internal static void EnsureSave()", StringComparison.Ordinal);
        int development = food.IndexOf("StandDown = FoodAuthorityStandDown.FromManifest(", ensure, StringComparison.Ordinal);
        Assert.True(development > ensure && food.IndexOf("return;", development, StringComparison.Ordinal) < food.IndexOf("Persist()", development, StringComparison.Ordinal));
        // The development check precedes the manifest read, the capability probe and every suspension (Error) path.
        int probe = food.IndexOf("SaveCodec.DevelopmentSchemaOf(", ensure, StringComparison.Ordinal);
        Assert.True(probe > ensure && probe < development && development < food.IndexOf("RequireExecutableCapabilities", ensure, StringComparison.Ordinal)
            && development < food.IndexOf("Error = \"Food ownership suspended", ensure, StringComparison.Ordinal));
        Assert.Contains("if (StandDown != null) return StandDown.Suppresses(", food);
        Assert.Contains("|| StandDown != null ||", food);
    }

    // ---- Inspector words ----

    [Fact]
    public void InspectorShowsKindHouseholdAndEmploymentWordsWithoutIds()
    {
        var city = Running(2, true, 1); var state = city.State;
        foreach (var person in state.Citizens)
        {
            var insight = city.Inspect(person.Identity.Id)!;
            int household = state.Citizens.Count(c => c.HomeId == person.HomeId);
            Assert.Equal(PersonKind.Resident, insight.Kind); Assert.Equal(household, insight.HouseholdSize);
            Assert.StartsWith("Resident", insight.PersonWords);
            Assert.Contains(household == 1 ? "lives alone" : "household of " + household, insight.PersonWords);
            if (person.DayPlaceId.Length == 0) Assert.DoesNotContain("since day", insight.PersonWords);
            else Assert.Contains((person.Identity.Persona == Persona.Student ? "studying" : "employed") + " since day " + person.EmploymentStartDay, insight.PersonWords);
            Assert.DoesNotContain(person.HomeId, insight.PersonWords); Assert.DoesNotContain("h:", insight.PersonWords);
        }
    }
}
