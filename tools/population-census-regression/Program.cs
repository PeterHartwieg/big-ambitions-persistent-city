using System.Diagnostics;
using System.Globalization;
using PersistentCity.Core;

// F5a census cost and invariants on synthetic catalogues. No Unity, game, save or rig input.
// The reference shape copies the rig city's recorded place, home, work-role and resident counts
// (1,059 / 335 / 383 / 957); bed bands, areas, owners, stations and assignments are synthetic.
int failures = 0;
void Check(bool condition, string message) { if (!condition) { failures++; Console.WriteLine("FAIL " + message); } }

foreach (var (name, places, homes, works, residents, stations) in new[]
{
    ("reference-shape", 1059, 335, 383, 957, 20),
    ("places-20000", 20000, 6330, 7240, 50000, 256)
})
{
    var input = Fixture(places, homes, works, residents, stations, out var citizens);
    var census = PopulationCensus.Compute(input);
    Check(!census.Refused, name + " refused: " + census.RefusalReason);
    Check(census.Places == places && census.HomeInteriors == homes && census.Residents == residents, name + " counts");
    Check(census.HouseholdsWithinBeds && census.Households <= census.HomeInteriors, name + " households within beds");
    Check(census.CommuterTarget + census.DayVisitorPoolTarget <= census.VisitorCapacity, name + " visitor capacity");
    Check(census.PersistedPersons <= PopulationCensusPolicy.MaximumPersons, name + " persons bound");
    Check(census.WorkplaceLines.Count == works, name + " workplace lines");
    var shuffled = new PopulationCensusInput
    {
        Places = input.Places.Reverse().ToList(), Workplaces = input.Workplaces.Reverse().ToList(),
        Residents = input.Residents.Reverse().ToList(), StationIds = input.StationIds!.Reverse().ToList()
    };
    Check(PopulationCensus.Compute(shuffled).Summary() == census.Summary(), name + " order independence");
    if (name == "reference-shape") Console.Write(census.Summary());

    for (int i = 0; i < 20; i++) PopulationCensus.Compute(input).Summary();
    var times = new List<double>(); var bytes = new List<long>();
    for (int i = 0; i < 200; i++)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        PopulationCensus.Compute(input).Summary();
        times.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
        bytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocated);
    }
    // Main-thread capture (resident facts and place copies) is measured apart from the detached reduction.
    var source = citizens;
    var captureTimes = new List<double>(); var captureBytes = new List<long>();
    for (int i = 0; i < 220; i++)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        PopulationCensusInput.CaptureResidents(source);
        double elapsed = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        long used = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (i >= 20) { captureTimes.Add(elapsed); captureBytes.Add(used); }
    }
    captureTimes.Sort(); captureBytes.Sort();
    times.Sort(); bytes.Sort();
    string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    Console.WriteLine(name + ": places=" + places + " residents=" + residents + " runs=200 compute+summary ms p50=" + F(times[100])
        + " p95=" + F(times[189]) + " max=" + F(times[199]) + " allocated_bytes median=" + bytes[100] + " max=" + bytes[199]
        + " | main-thread resident capture ms p50=" + F(captureTimes[100]) + " p95=" + F(captureTimes[189]) + " max=" + F(captureTimes[199])
        + " bytes=" + captureBytes[100]);
}
Console.WriteLine(failures == 0 ? "population census regression: PASS" : "population census regression: FAIL " + failures);
return failures == 0 ? 0 : 1;

static PopulationCensusInput Fixture(int placeCount, int homes, int works, int residents, int stations, out List<CitizenState> citizens)
{
    // Home bands 1/2/4/5/7 in a fixed cycle (the real reference city has 1,064 beds; this has 1,106).
    int[] beds = { 2, 4, 2, 4, 5, 1, 2, 4, 7, 2 };
    double[] homeAreas = { 60, 120, 60, 100, 200, 30, 50, 140, 320, 70 };
    double[] workAreas = { 35, 60, 90, 140, 180, 250, 420, 650, 0, 110 };
    var places = new List<Place>(placeCount);
    for (int i = 0; i < homes; i++)
        places.Add(new Place { Id = "address:h" + i.ToString("D5", CultureInfo.InvariantCulture) + ":home", Kind = PlaceKind.Home,
            Capacity = beds[i % beds.Length], ResidentialAreaSquareMetres = homeAreas[i % homeAreas.Length] });
    var workplaces = new List<CensusWorkplace>(works);
    for (int i = 0; i < works; i++)
    {
        string id = "address:b" + i.ToString("D5", CultureInfo.InvariantCulture) + ":work";
        places.Add(new Place { Id = id, Kind = PlaceKind.Work, Capacity = 16 });
        bool school = i == 0;
        if (school) places.Add(new Place { Id = "address:b00000:study", Kind = PlaceKind.Study, Capacity = 16 });
        workplaces.Add(new CensusWorkplace { WorkPlaceId = id, AreaSquareMetres = workAreas[i % workAreas.Length], PlayerOwned = i % 50 == 7,
            NativeStaff = i % 3 == 0 ? 4 + i % 7 : -1, StudyPlaceId = school ? "address:b00000:study" : "" });
    }
    for (int i = places.Count; i < placeCount; i++) places.Add(new Place { Id = "address:s" + i.ToString("D5", CultureInfo.InvariantCulture) + ":shop", Kind = PlaceKind.Shop, Capacity = 16 });
    var people = new List<CitizenState>(residents);
    var personas = (Persona[])Enum.GetValues(typeof(Persona));
    for (int i = 0; i < residents; i++)
    {
        var persona = personas[i % personas.Length];
        string home = places[i % homes].Id;
        string day = persona == Persona.Retiree ? "" : persona == Persona.Student ? "address:b00000:study" : places[homes + i % works].Id;
        people.Add(new CitizenState { Identity = new CitizenIdentity { Id = "c" + i.ToString(CultureInfo.InvariantCulture), Persona = persona },
            HomeId = home, PlaceId = home, DayPlaceId = day });
    }
    var stationIds = Enumerable.Range(0, stations).Select(i => "subway:Station" + i.ToString(CultureInfo.InvariantCulture)).ToList();
    citizens = people;
    return new PopulationCensusInput { Places = places, Workplaces = workplaces, Residents = PopulationCensusInput.CaptureResidents(people), StationIds = stationIds };
}
