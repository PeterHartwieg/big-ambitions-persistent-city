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

/// <summary>F5d: writer 26 is now a frozen development writer (the product refuses it as an older development
/// city, see Schema27Tests). Its codec stays compiling and reachable only through <see cref="DevelopmentChain"/>, so
/// these tests check the frozen writer-26 shape. F6i updates only simulated-fixture values for anchored choice.
/// F5c-lite (#92): writer 26, the person shape without migration. Golden bytes, the factor-1 roster rule,
/// the strict per-kind reader, the named bounds at and one over, fabricated arrival tuples, and the refusal of older
/// development cities with their food authority left native.</summary>
public sealed class Schema26Tests
{
    // ---- Fixtures ----

    /// <summary>A fresh seeded city of the trace test, run for 30 hours (meals, budgets, journeys, visitors).</summary>
    internal static CitySimulator Running(int factor, bool food = true, double hours = 30)
    {
        var (state, market) = FreshCityTraceTests.Fresh(factor, food, schema: DevelopmentChain.Represented);
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        if (food) city.AttachFoodMarket(market);
        double until = state.Minute + hours * 60;
        while (!city.AdvanceTo(until, 64).CaughtUp) { }
        return city;
    }

    private static string GoldenPath => Path.Combine(AppContext.BaseDirectory, "Writer26", "golden.json");

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static IEnumerable<(string Name, Func<CityState> Build)> GoldenFixtures() => new (string, Func<CityState>)[]
    {
        ("fresh-trace-factor2-30h", () => Running(2).State),
        ("current-schema-minimal", () => CurrentSchemaOnlyTests.City().Also(c => c.Schema = DevelopmentChain.Represented)),
    };

    private static void RefusedAndPreserved(string payload, CityState replacement, SaveReadStatus expected, string? error = null)
    {
        var read = DevelopmentChain.Read(payload);
        Assert.True(read.Status == expected, read.Status + ": " + read.Error); Assert.Null(read.State);
        if (error != null) Assert.Contains(error, read.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "kept", [FoodAuthorityStore.Key] = "manifest" };
        // The product refuses every writer-26 payload as an older development city before reading its shape.
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
        var root = JObject.Parse(payload);
        var parts = ((string)root["Visitors"]![index]!).Split('|');
        root["Visitors"]![index] = change(parts);
        return root.ToString(Formatting.None);
    }

    // ---- Golden current writer ----

    /// <summary>Two seeded fixtures pinned for writer 26. A later shape change without a schema bump fails here;
    /// with a bump, pin the new writer in a new file and keep this one as a record.</summary>
    [Fact]
    public void GoldenWriter26BytesArePinned()
    {
        Assert.Equal(26, SaveCodec.PersonSchema); Assert.Equal(CityState.CurrentSchema - 1, SaveCodec.LatestDevelopmentSchema);
        var pins = JObject.Parse(File.ReadAllText(GoldenPath));
        Assert.Equal(26, (int)pins["writer"]!);
        foreach (var (name, build) in GoldenFixtures())
        {
            var state = build();
            string payload = DevelopmentChain.Write(state);
            Assert.Equal((string)pins["fixtures"]![name]!["sha256"]!, Sha(payload));
            Assert.Equal((int)pins["fixtures"]![name]!["characters"]!, payload.Length);
            var read = DevelopmentChain.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(payload, DevelopmentChain.Write(read.State!));
        }
    }

    [Fact]
    public void GoldenFixturesAreDeterministicAcrossBuilds()
    {
        foreach (var (_, build) in GoldenFixtures()) Assert.Equal(DevelopmentChain.Write(build()), DevelopmentChain.Write(build()));
    }

    // ---- Fresh cities in the new shape: factor-1 roster rule ----

    [Theory]
    [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)] [InlineData(1, false)]
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
        Assert.Contains("\"Schema\":26,", payload);
        Assert.DoesNotContain("RepresentationFactor", payload);
        // Roster tuple v3: version plus 34 slots, no anchor or slot (routine stops still name their resident anchor).
        if (food) Assert.All(JObject.Parse(payload)["Food"]!["Residents"]!, row => { Assert.Equal(3, (int)row[0]!); Assert.Equal(35, ((JArray)row).Count); });
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
        var visitorMeal = state.Food!.Residents.First(m => m.HostSlot == 1);
        visitorMeal.AnchorId = visitorMeal.HostId; visitorMeal.Slot = 1; // the pre-26 hosted form
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
        var root = JObject.Parse(payload); root["Citizens"]![0]!["P"] = (string)root["Visitors"]![0]!;
        Invalid(root.ToString(Formatting.None), state, "visitor row cannot carry resident identity");
        root = JObject.Parse(payload); root["Citizens"]![0]!["P"] = "1V";
        Invalid(root.ToString(Formatting.None), state, "visitor row cannot carry resident identity");
        // A resident without identity: as a visitor tuple, or a citizen row whose identity is missing or null.
        Invalid(WithVisitor(payload, 0, p => "1R-"), state, "resident row requires identity");
        root = JObject.Parse(payload); ((JObject)root["Citizens"]![0]!).Remove("Identity");
        Invalid(root.ToString(Formatting.None), state);
        root = JObject.Parse(payload); root["Citizens"]![0]!["Identity"] = null;
        Invalid(root.ToString(Formatting.None), state);
        // Visitor tuples have no identity, home, car or visit slot: any extra field changes the arity.
        Invalid(WithVisitor(payload, 0, p => string.Join("|", p) + "|Ada"), state, "wrong arity");
        Invalid(WithVisitor(payload, 0, p => string.Join("|", p.Take(5))), state, "wrong arity");
        // Unknown kind and version.
        Invalid(WithVisitor(payload, 0, p => "1X|" + string.Join("|", p.Skip(1))), state, "Unknown person kind");
        Invalid(WithVisitor(payload, 0, p => "2V|" + string.Join("|", p.Skip(1))), state, "version");
        root = JObject.Parse(payload); root["Citizens"]![0]!["P"] = "1Q-";
        Invalid(root.ToString(Formatting.None), state, "Unknown person kind");
    }

    [Fact]
    public void ResidentTupleMustMatchItsDayPlaceAndFixedCodes()
    {
        var state = Personas(); string payload = DevelopmentChain.Write(state);
        int worker = state.Citizens.FindIndex(c => c.DayPlaceId.Length > 0), retiree = state.Citizens.FindIndex(c => c.DayPlaceId.Length == 0);
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(payload).Status);
        string Set(int row, string tuple) { var r = JObject.Parse(payload); r["Citizens"]![row]!["P"] = tuple; return r.ToString(Formatting.None); }
        int start = state.Citizens[worker].EmploymentStartDay;
        Invalid(Set(worker, "1R-"), state, "Employment differs");
        Invalid(Set(retiree, "1RE" + start + ".0.0"), state, "Employment differs");
        Invalid(Set(worker, "1RE" + start + ".1.0"), state, "Invalid resident employment");
        Invalid(Set(worker, "1RE" + start + ".0.1"), state, "Invalid resident employment");
        Invalid(Set(worker, "1RE0" + start + ".0.0"), state, "Invalid resident employment start");
        Invalid(Set(worker, "1RE" + (state.Citizens[worker].LastEmploymentCheckDay + 1) + ".0.0"), state, "Employment start is outside");
        Invalid(Set(worker, "1RE-1.0.0"), state);
        Invalid(Set(worker, "1R" + new string('-', 40)), state);
        var r = JObject.Parse(payload); ((JObject)r["Citizens"]![0]!).Remove("P");
        Invalid(r.ToString(Formatting.None), state);
        // A duplicate field cannot pass the token check even with equal values.
        string tuple = "\"P\":\"" + (string)JObject.Parse(payload)["Citizens"]![0]!["P"]! + "\"";
        int at = payload.IndexOf(tuple, StringComparison.Ordinal);
        Invalid(payload.Insert(at, tuple + ","), state, "Duplicate field: P");
    }

    [Fact]
    public void RootPersonFieldsAreStrict()
    {
        var state = Running(2, true, 2).State; string payload = DevelopmentChain.Write(state);
        string Root(Action<JObject> change) { var r = JObject.Parse(payload); change(r); return r.ToString(Formatting.None); }
        RefusedAndPreserved(Root(r => r["Schema"] = CityState.CurrentSchema + 1), state, SaveReadStatus.Future);
        Invalid(Root(r => r["Persons"] = new JArray()), state, "Unknown field: Persons");
        Invalid(Root(r => r["Food"]!["RepresentationFactor"] = 2), state, "Unknown field: RepresentationFactor");
        Invalid(Root(r => r.Remove("Visitors")), state, "Missing object fields");
        Invalid(Root(r => r["NextVisitorSequence"] = 2), state, "Visitor creation and active counts disagree");
        Invalid(Root(r => r["IdentityPolicyVersion"] = 2), state, "identity policy");
        Invalid(Root(r => r["Portals"] = new JArray("station:b", "station:a")), state, "Station table is unordered");
        Invalid(Root(r => r["Portals"] = new JArray("station:a", "station:a")), state, "Station table is unordered");
        Invalid(Root(r => r["Portals"] = new JArray(Enumerable.Range(0, 257).Select(i => "s" + i.ToString("000", CultureInfo.InvariantCulture)))), state, "Oversized station table");
        Invalid(Root(r => r["Portals"] = new JArray(new string('s', 129))), state, "Invalid station ID");
        // One roster record per person: tuple v2 (with anchor and slot) and a missing or extra visitor are refused.
        Invalid(Root(r => r["Food"]!["Residents"]![0]![0] = 2), state, "Invalid tuple version");
        Invalid(Root(r => ((JArray)r["Visitors"]!).RemoveAt(0)), state, "represented consumers");
        Invalid(Root(r => ((JArray)r["Visitors"]!).Add(r["Visitors"]![0]!.DeepClone())), state);
        Invalid(Root(r => { var v = (JArray)r["Visitors"]!; var first = v[0]!; v.RemoveAt(0); v.Add(first); }), state);
        // A visitor must be one resident's represented consumer in writer 26.
        string host = state.Citizens[0].Identity.Id;
        Invalid(WithVisitor(payload, 0, p => { p[1] = FoodRepresentation.ConsumerId(host, 2); return string.Join("|", p); }), state);
        Invalid(Root(r => { r["NextVisitorSequence"] = 2; ((JArray)r["Visitors"]!)[0] = "1V|pv:1|---CA.1.0000000000000000|-|-|-"; }), state);
        Invalid(WithVisitor(payload, 0, p => { p[1] = "visitor-without-format"; return string.Join("|", p); }), state, "Invalid visitor ID");
    }

    [Fact]
    public void VisitorStayEmploymentAndIncomeAreStructurallyChecked()
    {
        var state = Running(2, true, 2).State; string payload = DevelopmentChain.Write(state);
        string Stay(string stay) => WithVisitor(payload, 0, p => { p[2] = stay; return string.Join("|", p); });
        string Field(int index, string value) => WithVisitor(payload, 0, p => { p[index] = value; return string.Join("|", p); });
        double future = state.Minute + 90;
        Invalid(Stay("---XA.1.0000000000000000"), state, "pattern");
        Invalid(Stay("---CX.1.0000000000000000"), state, "presence");
        Invalid(Stay("---CA.9.0000000000000000"), state, "first day");
        Invalid(Stay("---CA.01.0000000000000000"), state, "first day");
        Invalid(Stay("---CA.1." + Bits(future)), state, "active visitor has no next stay");
        Invalid(Stay("---CD.1." + Bits(state.Minute - 1)), state, "next stay must be in the future");
        Invalid(Stay("---CD.1.7FF8000000000000"), state);
        Invalid(Stay("000CA.1.0000000000000000"), state, "outside the station table");
        Invalid(Stay("zz0CA.1.0000000000000000"), state, "portal");
        // A dormant visitor and one arriving through a saved portal are valid shapes.
        var dormant = DevelopmentChain.Read(Stay("---CD.1." + Bits(future))); Assert.True(dormant.Status == SaveReadStatus.Loaded, dormant.Error);
        var withPortal = JObject.Parse(Stay("000CA.1.0000000000000000")); withPortal["Portals"] = new JArray("station:central");
        var portal = DevelopmentChain.Read(withPortal.ToString(Formatting.None)); Assert.True(portal.Status == SaveReadStatus.Loaded, portal.Error);
        Assert.Equal("station:central", portal.State!.Visitors[0].Stay.OriginPortalId);
        Assert.Equal(withPortal.ToString(Formatting.None), DevelopmentChain.Write(portal.State));
        // Employment: commuters only, a saved Work/Study place of the right kind, native employment fixed at none.
        string work = MemoryWire.Number(PlaceIndex(state, "work:a"), 3), study = MemoryWire.Number(PlaceIndex(state, "study:a"), 3);
        var employed = DevelopmentChain.Read(Field(3, "W" + work + ".1.0.0")); Assert.True(employed.Status == SaveReadStatus.Loaded, employed.Error);
        Assert.Equal("work:a", employed.State!.Visitors[0].Employment!.WorkplaceId);
        Assert.Equal(Field(3, "W" + work + ".1.0.0"), DevelopmentChain.Write(employed.State));
        Invalid(Field(3, "W" + study + ".1.0.0"), state, "wrong role");
        Invalid(Field(3, "S" + work + ".1.0.0"), state, "wrong role");
        Invalid(Field(3, "W" + work + ".1.0.1"), state, "Invalid visitor employment");
        Invalid(Field(3, "W" + work + ".1.1.0"), state, "Invalid visitor employment");
        Invalid(Field(3, "W" + work + ".0.0.0"), state, "employment start");
        Invalid(Field(3, "WZZZ.1.0.0"), state, "workplace index");
        Invalid(WithVisitor(payload, 0, p => { p[2] = "---DA.1.0000000000000000"; p[3] = "W" + work + ".1.0.0"; return string.Join("|", p); }), state, "day visitor holds no employment");
        // Carried income: one defined source digit or none.
        var carried = DevelopmentChain.Read(Field(4, "1")); Assert.True(carried.Status == SaveReadStatus.Loaded, carried.Error);
        Assert.Equal(ResidentIncomeSource.ExternalPension, carried.State!.Visitors[0].CarriedIncomeSource);
        Invalid(Field(4, "3"), state, "carried income"); Invalid(Field(4, "11"), state, "carried income");
    }

    /// <summary>Plan section 3, Arrival evidence: the reader checks structure only (token length/alphabet, finite
    /// minute not after the city minute, place index in the saved table, activity code, none while dormant). Any
    /// historical arrival is valid; place, Shop activity and DesiredAt belong to admission, not to reading.</summary>
    [Fact]
    public void FabricatedArrivalTuplesAreRefusedAndHistoricalOnesRead()
    {
        var state = Running(2, true, 2).State; string payload = DevelopmentChain.Write(state);
        string Arrival(string token) => WithVisitor(payload, 0, p => { p[5] = token; return string.Join("|", p); });
        string work = MemoryWire.Number(PlaceIndex(state, "work:a"), 3), shop = MemoryWire.Number(PlaceIndex(state, "shop:1"), 3);
        foreach (string historical in new[] { "1" + Bits(state.Minute - 300) + work + "1", "1" + Bits(state.Minute) + shop + "2", "1" + Bits(0) + work + "0" })
        {
            var read = DevelopmentChain.Read(Arrival(historical)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(Arrival(historical), DevelopmentChain.Write(read.State!));
        }
        Invalid(Arrival("1" + Bits(state.Minute + 1) + shop + "2"), state, "Arrival is in the future");
        Invalid(Arrival("1" + Bits(-1) + shop + "2"), state);
        Invalid(Arrival("17FF8000000000000" + shop + "2"), state, "Invalid arrival minute");
        Invalid(Arrival("1" + Bits(state.Minute).ToLowerInvariant() + shop + "2"), state, "Invalid arrival minute");
        Invalid(Arrival("1" + Bits(state.Minute) + "ZZZ" + "2"), state, "Invalid arrival place index");
        Invalid(Arrival("1" + Bits(state.Minute) + "FFF" + "2"), state); // inside the codec bound, outside this Places table
        Invalid(Arrival("1" + Bits(state.Minute) + "ab0" + "2"), state, "Invalid arrival place index");
        Invalid(Arrival("1" + Bits(state.Minute) + shop + "5"), state, "Unknown arrival activity");
        Invalid(Arrival("1" + Bits(state.Minute) + shop), state, "length/version");
        Invalid(Arrival("2" + Bits(state.Minute) + shop + "2"), state, "length/version");
        Invalid(Arrival("1" + Bits(state.Minute) + shop + "22"), state, "length/version");
        // The departure commit clears it: a dormant visitor holding an arrival is refused.
        Invalid(WithVisitor(payload, 0, p => { p[2] = "---CD.1." + Bits(state.Minute + 90); p[5] = "1" + Bits(state.Minute - 5) + work + "1"; return string.Join("|", p); }),
            state, "dormant visitor holds no arrival");
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
        var root = JObject.Parse(payload); root[field] = new JArray("@@" + field + "@@");
        string text = root.ToString(Formatting.None);
        var rows = new StringBuilder(count * (element.Length + 1));
        for (int i = 0; i < count; i++) { if (i > 0) rows.Append(','); rows.Append(element); }
        return text.Replace("\"@@" + field + "@@\"", rows.ToString());
    }

    [Fact]
    public void PersonsAndResidentsAreRefusedOneOverTheirBoundsBeforeAllocation()
    {
        var small = Running(2, true, 1).State; string source = DevelopmentChain.Write(small);
        var root = JObject.Parse(source);
        string citizen = root["Citizens"]![0]!.ToString(Formatting.None);
        string visitor = JsonConvert.ToString((string)root["Visitors"]![0]!);
        string residents = Spliced(source, "Citizens", citizen, SaveCodec.MaxResidents);
        // At the resident bound: no bound refusal (identical rows fail later, as duplicates or a roster mismatch).
        var at = DevelopmentChain.Read(Spliced(residents, "Visitors", visitor, 0));
        Assert.Equal(SaveReadStatus.Invalid, at.Status); Assert.DoesNotContain("exceed the bound", at.Error);
        string overResidents = Spliced(source, "Citizens", citizen, SaveCodec.MaxResidents + 1);
        Assert.Contains("Residents exceed the bound of 50000", DevelopmentChain.Read(overResidents).Error);
        // 50,000 residents + 100,000 visitors = 150,000 persons: at the bound; one more visitor is one over.
        string atPersons = Spliced(residents, "Visitors", visitor, SaveCodec.MaxVisitors);
        var atRead = DevelopmentChain.Read(atPersons);
        Assert.Equal(SaveReadStatus.Invalid, atRead.Status); Assert.DoesNotContain("exceed the bound", atRead.Error);
        string overPersons = Spliced(residents, "Visitors", visitor, SaveCodec.MaxVisitors + 1);
        Assert.Contains("Persons exceed the bound of 150000", DevelopmentChain.Read(overPersons).Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = overPersons };
        Assert.False(SaveStore.WriteTo(data, small).Success); Assert.Same(overPersons, data[SaveStore.StateKey]);
        // The visitor bound itself, with few residents.
        Assert.Contains("Visitors exceed the bound of 100000", DevelopmentChain.Read(Spliced(source, "Visitors", visitor, SaveCodec.MaxVisitors + 1)).Error);
        Assert.DoesNotContain("exceed the bound", DevelopmentChain.Read(Spliced(source, "Visitors", visitor, SaveCodec.MaxVisitors)).Error);
    }

    [Fact]
    public void WritesCheckTheNamedBoundsBeforeAnyPersonWork()
    {
        var state = FreshCityTraceTests.Fresh(1, false, schema: DevelopmentChain.Represented).State; var person = state.Citizens[0];
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
        return (JsonSerializerSettings)typeof(SaveCodec).GetMethod("SettingsForSchema", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new[] { context, (object)SaveCodec.PersonSchema })!;
    }

    [Fact]
    public void TrafficVehiclesAreRefusedOneOverTheResidentBound()
    {
        var state = FreshCityTraceTests.Fresh(1, false, schema: DevelopmentChain.Represented).State; string source = DevelopmentChain.Write(state);
        string vehicle = JsonConvert.SerializeObject(new TrafficVehicleState { VehicleId = "v" }, Settings(state));
        string Traffic(int vehicles)
        {
            var kernel = JObject.Parse(JsonConvert.SerializeObject(new TrafficKernelState { Clock = state.Minute, RequestedTime = state.Minute }, Settings(state)));
            kernel["Vehicles"] = new JArray("@@V@@");
            var root = JObject.Parse(source); root["Traffic"] = kernel; root["TrafficTopologyId"] = new string('a', 64);
            var rows = string.Join(",", Enumerable.Repeat(vehicle, vehicles));
            return root.ToString(Formatting.None).Replace("\"@@V@@\"", rows);
        }
        var at = DevelopmentChain.Read(Traffic(SaveCodec.MaxTrafficVehicles));
        Assert.Equal(SaveReadStatus.Invalid, at.Status); Assert.DoesNotContain("Oversized", at.Error);
        Invalid(Traffic(SaveCodec.MaxTrafficVehicles + 1), state, "Oversized collection");
        // The write side refuses the same count before any per-vehicle check.
        state.TrafficTopologyId = new string('a', 64);
        state.Traffic = new TrafficKernelState { Clock = state.Minute, RequestedTime = state.Minute,
            Vehicles = Enumerable.Repeat(new TrafficVehicleState { VehicleId = "v" }, SaveCodec.MaxTrafficVehicles + 1).ToList() };
        Assert.Contains("Oversized traffic collection", Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(state)).Message);
        state.Traffic.Vehicles.RemoveAt(0);
        Assert.DoesNotContain("Oversized", Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(state)).Message);
    }

    // ---- Record budget: every new schema-26 record is charged by both counters ----

    [Theory]
    [InlineData(2, true)] [InlineData(1, false)]
    public void ResidentVisitorAndStationTuplesChargeOneRecordInBothCounters(int factor, bool food)
    {
        var state = Running(factor, food, 2).State; state.Portals.AddRange(new[] { "station:a", "station:b" });
        string p26 = DevelopmentChain.Write(state), p25 = DevelopmentChain.Write25(state);
        var c26 = DevelopmentChain.CountRecords(state, p26);
        var c25 = SaveCodec.CountRecordsForSchema(SaveCodec.ReadDevelopmentChain(p25).State!, p25, 25);
        int added = state.Citizens.Count + state.Visitors.Count + state.Portals.Count;
        Assert.Equal(c25.Streaming + added, c26.Streaming); Assert.Equal(c25.Semantic + added, c26.Semantic);
    }

    /// <summary>Section 5 record budget at the limit: a schema-26 city charging exactly MaxEmbeddedRecords (with one station
    /// entry) writes and round-trips; one more new record (a second station) is refused by the writer before
    /// publication and by the reader before allocation.</summary>
    [Fact]
    public void AtTheRecordLimitAPayloadRoundTripsAndOneMoreNewRecordIsRefusedBeforePublication()
    {
        var state = EconomyScenario.Create(SaveCodec.MaxResidents, 23); state.Schema = DevelopmentChain.Represented;
        var template = state.Places.Single(p => p.Id == "leisure-1");
        for (int i = state.Places.Count; i < SaveCodec.MaxPlaces; i++)
            state.Places.Add(new Place { Id = "pad:" + i.ToString("D5", CultureInfo.InvariantCulture), Name = "Pad", District = "a",
                Kind = PlaceKind.Leisure, Position = template.Position, Capacity = 0, EvidenceSource = template.EvidenceSource });
        var empty = DevelopmentChain.CountRecords(state, DevelopmentChain.Write(state));
        Assert.Equal(empty.Streaming, empty.Semantic);
        long remaining = SaveCodec.MaxEmbeddedRecords - 1 - empty.Semantic; // the last record is the station entry
        foreach (var place in state.Places.Where(p => p.Kind == PlaceKind.Leisure))
            for (int w = place.WeeklyHours.Count; w < SaveCodec.MaxOpeningWindows && remaining > state.Citizens.Count * (long)SaveCodec.MaxRecentVisits; w++, remaining--)
                place.WeeklyHours.Add(new OpeningWindow { Day = w % 7, FromMinute = 0, ToMinute = 60 });
        foreach (var person in state.Citizens)
        {
            person.RecentVisits.Clear();
            for (int v = 0; v < SaveCodec.MaxRecentVisits && remaining > 0; v++, remaining--)
                person.RecentVisits.Add(new VisitRecord { AtMinute = state.Minute - SaveCodec.MaxRecentVisits + v, PlaceId = person.HomeId, Activity = Activity.Home });
        }
        Assert.Equal(0, remaining);
        state.Portals.Add("station:a");
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = DevelopmentChain.Write(state) };
        string payload = data[SaveStore.StateKey];
        var count = DevelopmentChain.CountRecords(state, payload);
        Assert.Equal((SaveCodec.MaxEmbeddedRecords, SaveCodec.MaxEmbeddedRecords), (count.Streaming, count.Semantic));
        var read = DevelopmentChain.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, DevelopmentChain.Write(read.State!));
        read = null;
        // One more new record: the writer refuses before publication and the stored payload stays.
        state.Portals.Add("station:b");
        Assert.Contains("Too many", Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(state)).Message);
        Assert.Same(payload, data[SaveStore.StateKey]);
        // The reader charges the same record while streaming, before deserialization allocates.
        string over = payload.Replace("\"Portals\":[\"station:a\"]", "\"Portals\":[\"station:a\",\"station:b\"]");
        Assert.NotEqual(payload, over);
        var overRead = DevelopmentChain.Read(over);
        Assert.Equal(SaveReadStatus.Invalid, overRead.Status); Assert.Contains("Too many", overRead.Error);
    }

    // ---- Older development schemas: refused, preserved, food authority native ----

    [Fact]
    public void Schema25CityWithOwnedFoodBusinessesIsRefusedAndItsBusinessesReturnNative()
    {
        string payload = ReaderCorpus.Load(25).Single(f => f.File == "full.json").Text;
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
        string payload = ReaderCorpus.Load(25).Single(f => f.File == "full.json").Text;
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
