using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5PopulationTableTests
{
    private static CitizenIdentity[] Donors(int count) => Enumerable.Range(0, count).Select(i => new CitizenIdentity
    {
        Id = "source:" + i.ToString("D5"), Name = "Given" + i + " Family" + i,
        District = "district", Age = 25 + i % 40, Persona = Persona.Commuter, AppearanceSeed = 100 + i
    }).ToArray();

    private static List<Place> Catalogue(int unrentableBlocks, double footprint, double floors)
    {
        var tally = new UnrentableHomeTally();
        var places = new List<Place>
        {
            new Place { Id = "home-rentable", Kind = PlaceKind.Home, Capacity = HousingPolicy.Capacity(54), ResidentialAreaSquareMetres = 54, District = "a" },
            new Place { Id = "work-a", Kind = PlaceKind.Work, Capacity = 100000, Position = new(100, 0, 0) },
            new Place { Id = "shop-a", Kind = PlaceKind.Shop, Capacity = 16, Position = new(50, 0, 0), BusinessType = "ba:businesstype_clothingstore" },
            new Place { Id = "shop-b", Kind = PlaceKind.Shop, Capacity = 16, Position = new(60, 0, 0), BusinessType = "ba:businesstype_coffeeshop" },
            new Place { Id = "shop-c", Kind = PlaceKind.Shop, Capacity = 16, Position = new(70, 0, 0), BusinessType = "ba:businesstype_furniturestore" },
        };
        for (int i = 0; i < unrentableBlocks; i++)
        {
            int beds = tally.Add(footprint, floors);
            if (beds > 0) places.Add(new Place { Id = "home-block-" + i.ToString("D4"), Kind = PlaceKind.Home, Capacity = beds, District = "a",
                Position = new(i, 0, 10), EvidenceSource = HousingPolicy.UnrentablePolicyId });
        }
        return places;
    }

    [Theory]
    [InlineData(400, 5, 40)]
    [InlineData(400, 5.9, 40)]
    [InlineData(100, 1, 2)]
    [InlineData(49.9, 1, 0)]
    [InlineData(1000, 12, 240)]
    public void SecondCapacityRuleFloorsFootprintTimesFloorsTimesShareOverSpacePerPerson(double footprint, double floors, int expected)
    {
        Assert.True(HousingPolicy.TryUnrentableCapacity(footprint, floors, out int capacity));
        Assert.Equal(expected, capacity);
        Assert.Equal((int)Math.Floor(footprint * Math.Floor(floors) * CityBalance.UnrentableHomeUsableShare / CityBalance.UnrentableHomeSquareMetresPerPerson), capacity);
    }

    [Fact]
    public void MissingOrNonFiniteInputHousesNobodyAndIsCounted()
    {
        var bad = new (double Footprint, double Floors)[]
        {
            (double.NaN, 3), (400, double.NaN), (double.PositiveInfinity, 3), (400, double.NegativeInfinity),
            (0, 3), (-10, 3), (400, -1), (double.MaxValue, double.MaxValue)
        };
        var tally = new UnrentableHomeTally();
        foreach (var (footprint, floors) in bad)
        {
            Assert.False(HousingPolicy.TryUnrentableCapacity(footprint, floors, out int capacity));
            Assert.Equal(0, capacity);
            Assert.Equal(0, tally.Add(footprint, floors));
        }
        Assert.Equal(bad.Length, tally.Unreadable);
        Assert.Equal(0, tally.Housed); Assert.Equal(0, tally.Beds);
        Assert.Equal(0, tally.Add(10, 1));
        Assert.Equal(0, tally.Add(400, 0));
        Assert.Equal(0, tally.Add(400, 0.5));
        Assert.Equal(3, tally.Empty);
        Assert.Equal(bad.Length, tally.Unreadable);
        Assert.Equal(40, tally.Add(400, 5));
        Assert.Equal(1, tally.Housed); Assert.Equal(40, tally.Beds);
        Assert.Contains("unreadable=" + bad.Length, tally.Summary());
    }

    [Fact]
    public void FillKeepsNinetyPercentAndTheCityTotalAndMaximumResidents()
    {
        Assert.True(CityBalance.CityResidentTotal <= HousingPolicy.MaximumResidents);
        Assert.True(CityBalance.CityResidentTotal + 2L * CityBalance.CityResidentTotal <= PopulationCensusPolicy.MaximumPersons);
        Assert.Equal(9, HousingPolicy.CityTarget(10));
        Assert.Equal(HousingPolicy.Target(1064), HousingPolicy.CityTarget(1064));
        Assert.Equal(CityBalance.CityResidentTotal, HousingPolicy.CityTarget(1_000_000));
        Assert.Equal(CityBalance.CityResidentTotal, HousingPolicy.CityTarget(long.MaxValue));
        Assert.Throws<InvalidOperationException>(() => HousingPolicy.CityTarget(0));
        Assert.Throws<InvalidOperationException>(() => HousingPolicy.CityTarget(1));

        var small = Population.CreateFromHousing(Donors(50), Catalogue(5, 400, 5), "source", "0.40.4", 7, 1440);
        long beds = small.Places.Where(p => p.Kind == PlaceKind.Home).Sum(p => (long)p.Capacity);
        Assert.Equal(202, beds);
        Assert.Equal(HousingPolicy.Target(beds), small.Citizens.Count);

        var large = Population.CreateFromHousing(Donors(50), Catalogue(200, 1000, 12), "source", "0.40.4", 7, 1440);
        Assert.Equal(CityBalance.CityResidentTotal, large.Citizens.Count);
        Assert.Equal(CityBalance.CityResidentTotal, large.PopulationTarget);
    }

    [Fact]
    public void EveryResidentHasAHomeAndAPlaceToInspect()
    {
        var city = Population.CreateFromHousing(Donors(30), Catalogue(40, 600, 6), "source", "0.40.4", 7, 1440);
        var byId = city.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        Assert.Contains(city.Citizens, c => c.HomeId.StartsWith("home-block-", StringComparison.Ordinal));
        foreach (var person in city.Citizens)
        {
            Assert.False(string.IsNullOrWhiteSpace(person.Identity.Name));
            Assert.True(byId.TryGetValue(person.HomeId, out var home) && home.Kind == PlaceKind.Home, person.Identity.Id);
            Assert.True(byId.ContainsKey(person.PlaceId), person.Identity.Id);
            used[person.HomeId] = used.TryGetValue(person.HomeId, out int n) ? n + 1 : 1;
        }
        foreach (var (home, count) in used) Assert.True(count <= byId[home].Capacity, home);
    }

    [Fact]
    public void CensusIsReproducibleFromTheSameInputs()
    {
        var places = Catalogue(25, 500, 4);
        var city = Population.CreateFromHousing(Donors(40), places, "source", "0.40.4", 7, 1440);
        PopulationCensus Run() => PopulationCensus.Compute(new PopulationCensusInput
        {
            Places = PopulationCensusInput.CapturePlaces(city.Places),
            Residents = PopulationCensusInput.CaptureResidents(city.Citizens),
            StationIds = new[] { "subway:A", "subway:B" }
        });
        var first = Run(); var second = Run();
        Assert.False(first.Refused);
        Assert.Equal(first.Summary(), second.Summary());
        Assert.Contains("visitor_groups=" + VisitorTable.Describe() + "\n", first.Summary());
        var again = Population.CreateFromHousing(Donors(40), Catalogue(25, 500, 4), "source", "0.40.4", 7, 1440);
        Assert.Equal(JsonConvert.SerializeObject(city.Citizens), JsonConvert.SerializeObject(again.Citizens));
    }

    [Fact]
    public void VisitorTableSharesAndWeekdaysAreComplete()
    {
        Assert.Equal(10000, VisitorTable.Groups.Sum(g => g.ShareBasisPoints));
        Assert.Equal(VisitorTable.Groups.Count, VisitorTable.Groups.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (var group in VisitorTable.Groups)
            Assert.All(group.Leaning.Values, w => Assert.True(w >= 1 && w <= 4));
        for (int day = 0; day < 7; day++)
        {
            Assert.InRange(VisitorTable.BasisPoints(VisitorPattern.DayVisit, day), 0, 10000);
            Assert.InRange(VisitorTable.BasisPoints(VisitorPattern.Commuter, day), 0, 10000);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => VisitorTable.BasisPoints(VisitorPattern.DayVisit, 7));
        var counts = VisitorTable.Groups.ToDictionary(g => g.Id, _ => 0);
        for (int n = 1; n <= 20000; n++) counts[VisitorTable.GroupOf(PersonIds.Visitor(n), 7).Id]++;
        foreach (var group in VisitorTable.Groups) Assert.InRange(counts[group.Id], group.ShareBasisPoints * 2 - 400, group.ShareBasisPoints * 2 + 400);
        Assert.Same(VisitorTable.GroupOf("pc:v:12", 7), VisitorTable.GroupOf("pc:v:12", 7));
    }

    [Fact]
    public void AGroupChangesWhoComesButNeverAShopsPriceOrPayment()
    {
        Assert.Equal(new[] { "Id", "Leaning", "ShareBasisPoints" },
            typeof(VisitorGroup).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        var city = Population.CreateFromHousing(Donors(30), Catalogue(10, 400, 5), "source", "0.40.4", 7, 1440);
        city.Food = EconomyScenario.Food();
        PortalPlaces.Add(city, VisitorRoutineTests.Stations);
        string places = JsonConvert.SerializeObject(city.Places), food = JsonConvert.SerializeObject(city.Food);
        var visitors = VisitorSchedules.Create(city, 600);
        Assert.Equal(places, JsonConvert.SerializeObject(city.Places));
        Assert.Equal(food, JsonConvert.SerializeObject(city.Food));
        var shopType = city.Places.Where(p => p.Kind == PlaceKind.Shop).ToDictionary(p => p.Id, p => p.BusinessType, StringComparer.Ordinal);
        foreach (var group in VisitorTable.Groups)
        {
            var members = visitors.Where(v => VisitorTable.GroupOf(v.Identity.Id, city.Seed) == group).ToList();
            Assert.NotEmpty(members);
            var favoured = group.Leaning.Keys.ToHashSet(StringComparer.Ordinal);
            double share = members.Count(v => favoured.Contains(shopType[v.ShopId])) / (double)members.Count;
            Assert.True(share > 1 / 3.0, group.Id + " " + share);
        }
    }
}

public sealed class Gr5SaveMeasure
{
    [Fact]
    public void MeasureSaveEncodeAtCitySizes()
    {
        string? path = PerfGate.Variable("PC_GR5_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        foreach (int residents in new[] { 957, CityBalance.CityResidentTotal, 1074, 3500 })
        {
            var state = EconomyScenario.Create(residents, RepresentativeFixture.Seed);
            PortalPlaces.Add(state, RepresentativeFixture.Portals);
            VisitorSchedules.Create(state, 2 * residents);
            state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
                Shops = Enumerable.Range(1, 3).Select(i => new FoodScope { PlaceId = "shop-" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
            new CitySimulator(state, new FreshCityTraceTests.Walk()).AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            string payload = SaveCodec.Write(state);
            var times = new List<double>();
            for (int i = 0; i < 21; i++)
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                payload = SaveCodec.Write(state);
                times.Add((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            }
            times.Sort();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            SaveCodec.Write(state);
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(new
            {
                residents, visitors = state.Visitors.Count, persons = state.Citizens.Count + state.Visitors.Count,
                payloadChars = payload.Length, encodeMedianMs = times[10], encodeP95Ms = times[19], encodeMaxMs = times[20], allocatedBytes = allocated
            }) + Environment.NewLine);
        }
    }

    [Fact]
    public void MeasureBackgroundSaveAtCitySizes()
    {
        string? path = PerfGate.Variable("PC_GR5_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        foreach (int residents in new[] { CityBalance.CityResidentTotal, 1074 })
        {
            var state = EconomyScenario.Create(residents, RepresentativeFixture.Seed);
            PortalPlaces.Add(state, RepresentativeFixture.Portals);
            VisitorSchedules.Create(state, 2 * residents);
            state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
                Shops = Enumerable.Range(1, 3).Select(i => new FoodScope { PlaceId = "shop-" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
            new CitySimulator(state, new FreshCityTraceTests.Walk()).AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            string expected = SaveCodec.Write(state);
            var owner = new object();
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            var session = SaveSession.Open(data, state.PopulationKey);
            var binding = new SaveEpochBinding(owner, data, 0);
            var writer = new SaveHandOffWriter("measure save writer") { Allocation = GC.GetAllocatedBytesForCurrentThread };
            var patchCheck = SaveCompressionPatches.Check(SaveHandOffTests.StreetFoodPatched, new[] { SaveCompressionPatches.HandOffOwner, "foodtrucks.mod" });
            Assert.Null(patchCheck.Conflict);
            int observed = 0;
            var main = new List<double>(); var written = new List<double>(); var spliced = new List<double>(); var plain = new List<double>();
            var mainCost = new SaveCostRecorder();
            var phases = new double[SaveCostRecorder.PhaseCount];
            var mainBytes = new List<long>(); var writerBytes = new List<long>(); var spliceBytes = new List<long>();
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pc-gr5-handoff-" + Guid.NewGuid().ToString("N"));
            var filler = new byte[8 << 20];
            new Random(5).NextBytes(filler);
            for (int i = 0; i < 22; i++)
            {
                PendingSave? queued = null;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                var result = SaveEpochCallback.Run(binding, owner, data, false, ticket =>
                {
                    mainCost.Begin();
                    var begun = session.BeginWrite(data, state, writer, mainCost, null, out queued);
                    mainCost.End(begun.Success, begun.Error);
                    return begun.Success ? SaveEpochCapture.Queued(queued!) : SaveEpochCapture.Refused(begun.Error);
                }, _ => { }, _ => { });
                double mainMs = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                long mainAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.Equal(SaveEpochResult.Queued, result);
                var pending = queued!;
                using (var file = System.IO.File.Create(temp))
                {
                    file.Write(filler, 0, filler.Length / 2);
                    file.Write(SaveHandOffTests.Odin(("foodtrucks:state", "{\"v\":3}"), (SaveStore.StateKey, pending.PayloadToken), (SaveEpochStore.Key, pending.MarkerToken)));
                    file.Write(filler, filler.Length / 2, filler.Length / 2);
                }
                allocated = GC.GetAllocatedBytesForCurrentThread();
                SaveHandOff.Expect(pending, temp);
                SaveHandOff.MarkSerialized();
                Assert.True(SaveHandOffTests.StreetFoodPatchedCompress(temp, temp + ".gz", ok => { if (ok) observed++; }));
                long compressAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.Equal(SaveSpliceOutcome.Spliced, pending.Splice);
                Assert.True(session.Settle(pending).Success);
                Assert.True(binding.Settle(pending));
                Assert.Equal(expected, data[SaveStore.StateKey]);
                long plainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                using (var input = System.IO.File.OpenRead(temp))
                using (var output = new System.IO.Compression.GZipStream(System.IO.File.Create(temp + ".plain"), System.IO.Compression.CompressionLevel.Optimal))
                    input.CopyTo(output);
                double plainMs = (System.Diagnostics.Stopwatch.GetTimestamp() - plainStarted) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (i == 0) continue;
                plain.Add(plainMs);
                for (int phase = 0; phase < phases.Length; phase++) phases[phase] += mainCost.Milliseconds((SaveCostPhase)phase) / 21;
                main.Add(mainMs); mainBytes.Add(mainAllocated);
                written.Add(pending.WriteMilliseconds); writerBytes.Add(pending.WriterAllocatedBytes);
                spliced.Add(pending.CompressMilliseconds); spliceBytes.Add(compressAllocated);
            }
            System.IO.File.Delete(temp); System.IO.File.Delete(temp + ".gz"); System.IO.File.Delete(temp + ".plain");
            Assert.Equal(22, observed);
            var fallbackData = new Dictionary<string, string>(StringComparer.Ordinal);
            var fallbackSession = SaveSession.Open(fallbackData, state.PopulationKey);
            var fallback = new List<double>(); var fallbackBytes = new List<long>();
            for (int i = 0; i < 22; i++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                Assert.True(fallbackSession.WriteTo(fallbackData, state).Success);
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (i == 0) continue;
                fallback.Add(ms); fallbackBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocated);
            }
            fallback.Sort(); fallbackBytes.Sort();
            main.Sort(); written.Sort(); spliced.Sort(); plain.Sort();
            System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(new
            {
                kind = "background-save", residents, visitors = state.Visitors.Count, payloadChars = expected.Length, saves = main.Count,
                streetFoodPatch = "accepted", streetFoodObserverCalls = observed,
                fallbackMainThreadMedianMs = fallback[10], fallbackMainThreadP95Ms = fallback[19], fallbackMainThreadAllocatedBytesMedian = fallbackBytes[10],
                mainThreadMedianMs = main[10], mainThreadP95Ms = main[19], mainThreadMaxMs = main[20],
                writerMedianMs = written[10], writerP95Ms = written[19], writerMaxMs = written[20],
                spliceCompressMedianMs = spliced[10], spliceCompressP95Ms = spliced[19], plainCompressMedianMs = plain[10], plainCompressP95Ms = plain[19],
                spliceFillerBytes = filler.Length,
                meanMainPhaseMs = Enumerable.Range(0, phases.Length).ToDictionary(i => ((SaveCostPhase)i).ToString(), i => Math.Round(phases[i], 3)),
                mainThreadAllocatedBytesMedian = mainBytes.OrderBy(b => b).ElementAt(10),
                writerAllocatedBytesMedian = writerBytes.OrderBy(b => b).ElementAt(10),
                spliceAllocatedBytesMedian = spliceBytes.OrderBy(b => b).ElementAt(10),
                totalAllocatedBytesMedian = mainBytes.OrderBy(b => b).ElementAt(10) + writerBytes.OrderBy(b => b).ElementAt(10) + spliceBytes.OrderBy(b => b).ElementAt(10),
            }) + Environment.NewLine);
        }
    }

    [Fact]
    public void MeasureSimulationAllocationOfCapturedPayload()
    {
        string? path = PerfGate.Variable("PC_GR5_MEASURE");
        string? source = PerfGate.Variable("PC_GR5_PAYLOAD");
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(source)) return;
        var read = SaveCodec.Read(CapturedPayload.Load(source));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var state = read.State!;
        var simulator = new CitySimulator(state, new FreshCityTraceTests.Walk());
        simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        double day = Math.Floor(state.Minute / 1440) * 1440;
        foreach (double start in new[] { state.Minute, day + 1440 + 60, day + 1440 + 8 * 60, day + 1440 + 12 * 60 })
        {
            simulator.AdvanceTo(start, int.MaxValue);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            int frames = 0;
            for (double minute = start; minute < start + 60; minute += 1.0 / 30) { simulator.AdvanceFrameSlice(minute, 32); frames++; }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(new
            {
                source = System.IO.Path.GetFileName(source), kind = "simulation-allocation", startHour = Math.Round(start % 1440 / 60, 2),
                residents = state.Citizens.Count, visitors = state.Visitors.Count, gameMinutes = 60, frames,
                allocatedBytes = allocated, bytesPerGameMinute = allocated / 60, bytesPerFrame = allocated / frames,
            }) + Environment.NewLine);
        }
    }

    [Fact]
    public void MeasureSaveEncodeOfCapturedPayload()
    {
        string? path = PerfGate.Variable("PC_GR5_MEASURE");
        string? source = PerfGate.Variable("PC_GR5_PAYLOAD");
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(source)) return;
        var read = SaveCodec.Read(CapturedPayload.Load(source));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var state = read.State!;
        string payload = SaveCodec.Write(state);
        var times = new List<double>();
        var phases = new double[SaveCostRecorder.PhaseCount];
        var encoded = new double[SaveCostRecorder.SectionCount];
        var validated = new double[SaveCostRecorder.SectionCount];
        var cost = new SaveCostRecorder();
        for (int i = 0; i < 21; i++)
        {
            cost.Begin();
            payload = SaveCodec.Write(state, cost);
            cost.End(true, "");
            times.Add(cost.TotalMilliseconds);
            for (int phase = 0; phase < phases.Length; phase++) phases[phase] += cost.Milliseconds((SaveCostPhase)phase) / 21;
            for (int section = 0; section < encoded.Length; section++)
            {
                encoded[section] += cost.SectionMilliseconds((SaveCostSection)section) / 21;
                validated[section] += cost.ValidateSectionMilliseconds((SaveCostSection)section) / 21;
            }
        }
        times.Sort();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        SaveCodec.Write(state);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(new
        {
            source = System.IO.Path.GetFileName(source), residents = state.Citizens.Count, visitors = state.Visitors.Count, places = state.Places.Count,
            trafficLinks = state.Traffic?.Links.Count ?? 0, payloadChars = payload.Length,
            encodeMedianMs = times[10], encodeP95Ms = times[19], encodeMaxMs = times[20], allocatedBytes = allocated,
            meanPhaseMs = Enumerable.Range(0, phases.Length).ToDictionary(i => ((SaveCostPhase)i).ToString(), i => Math.Round(phases[i], 3)),
            meanEncodeSectionMs = Enumerable.Range(0, encoded.Length).ToDictionary(i => ((SaveCostSection)i).ToString(), i => Math.Round(encoded[i], 3)),
            meanValidateSectionMs = Enumerable.Range(0, validated.Length).ToDictionary(i => ((SaveCostSection)i).ToString(), i => Math.Round(validated[i], 3)),
        }) + Environment.NewLine);
    }
}
