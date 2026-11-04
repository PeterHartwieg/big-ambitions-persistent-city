using PersistentCity.Core;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using Activity = PersistentCity.Core.Activity;

// The audit's minimal two-consumer fixture, now measuring every call needed to finish a day.
// Construction, assertions and sorting are outside timed calls. No Unity, routes or native market.
var field = typeof(CitySimulator).GetField("food", BindingFlags.NonPublic | BindingFlags.Instance)!;
var method = typeof(FoodCommerce).GetMethod("StepBudget", BindingFlags.NonPublic | BindingFlags.Instance)!;
if (args.Contains("--integrated")) { Integrated.Run(); return; }
foreach (int residents in new[] { 957, 5000, 50000 })
{
    var state = new CityState { Seed = 7, Minute = 1440, PopulationKey = "audit",
        Food = new FoodCommerceState { RepresentationFactor = 2, HouseholdFinanceVersion = 1 } };
    state.Places.Add(new Place { Id = "home", Kind = PlaceKind.Home, Capacity = residents });
    for (int i = 0; i < residents; i++)
    {
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "r" + i, Persona = Persona.Student, Age = 22 },
            HomeId = "home", PlaceId = "home", Activity = Activity.Home, Phase = CitizenPhase.AtPlace,
            NextEventMinute = 1e8, ActivityUntilMinute = 1e8 };
        state.Citizens.Add(person);
        for (int slot = 0; slot < 2; slot++)
        {
            string id = FoodRepresentation.ConsumerId(person.Identity.Id, slot);
            state.Food.Residents.Add(new ResidentMeal { ResidentId = id, AnchorId = person.Identity.Id, Slot = slot,
                DueAt = 1e8, Budget = HouseholdFinance.Create(person, 7, 1440, null, id) });
        }
    }
    var city = new CitySimulator(state, new Routes()); city.AttachFoodMarket(new Market());
    var food = field.GetValue(city)!;
    var step = (Func<bool>)method.CreateDelegate(typeof(Func<bool>), food);
    var counter = typeof(FoodCommerce).GetProperty("LastBudgetPostings");
    var last = counter == null ? null : (Func<int>)counter.GetMethod!.CreateDelegate(typeof(Func<int>), food);
    // Direct delegate excludes reflection's per-invocation overhead/boxing. Same delegate on both heads.
    var times = new List<double>(); var allocations = new List<long>(); var work = new List<int>();
    var frames = new List<double>(); int peakBacklog = 0, totalFrames = 0;
    for (int day = 1; day <= 8; day++)
    {
        state.Minute = day * 1440;
        int remaining = residents * 2, calls = 0;
        while (remaining > 0)
        {
            // One synthetic frame admits <=16 units under a soft 2ms budget, excluding observer cost.
            // F1 owns real runtime admission. These are offline coordinator-work samples, not whole frames.
            long frameStart = Stopwatch.GetTimestamp(); int frameUnits = 0;
            do
            {
                int before = residents * 2 - remaining;
                long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                if (!step()) throw new Exception("No posting progress");
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                // Do not include the O(N) completeness probe in the synthetic frame either.
                long probeStart = Stopwatch.GetTimestamp();
                int after = last != null ? before + last() : state.Food.Residents.Count(m => m.Budget!.LastPostedDay == day);
                frameStart += Stopwatch.GetTimestamp() - probeStart;
                remaining = residents * 2 - after;
                if (day > 1) { times.Add(elapsed); allocations.Add(allocated); work.Add(after - before); }
                calls++; frameUnits++;
            } while (remaining > 0 && frameUnits < 16 && Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds < 2);
            if (day > 1) { frames.Add(Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds); totalFrames++; peakBacklog = Math.Max(peakBacklog, remaining); }
        }
        if (state.Food.Residents.Any(m => !HouseholdFinance.CashConserved(m.Budget!) || m.Budget!.LastPostedDay != day))
            throw new Exception("Incomplete or unconserved posting");
    }
    times.Sort(); allocations.Sort(); frames.Sort();
    double P(List<double> values, double p) => values[(int)Math.Ceiling(values.Count * p) - 1];
    Console.WriteLine(JsonSerializer.Serialize(new { consumers = residents * 2, calls = times.Count,
        callMs = new { median = P(times, .5), p95 = P(times, .95), max = times[^1], spikesAtLeast30 = times.Count(t => t >= 30) },
        workPerCall = new { min = work.Min(), max = work.Max(), total = work.Sum() },
        allocationBytes = new { median = allocations[allocations.Count / 2], max = allocations[^1], total = allocations.Sum() },
        syntheticFrameWorkMs = new { median = P(frames, .5), p95 = P(frames, .95), max = frames[^1], spikesAtLeast30 = frames.Count(t => t >= 30) },
        totalFrames, peakBacklog, dayWorkMs = frames.Sum() / 7,
        evidence = "Windows .NET 10 synthetic posting/coordinator work only; seven days after one discarded warm day; no native FPS or whole-frame evidence" }));
}

sealed class Routes : IRoutePlanner
{
    public Route? Plan(Place a, Place b, double t, TravelPreferences p) => throw new Exception("No route work admitted");
}
sealed class Market : IFoodMarket
{
    public double EarliestMinute => 0;
    public IReadOnlyList<FoodBusiness> Read(double t) => throw new Exception("No market work admitted");
    public FoodSale Buy(ResidentMeal m, double t) => throw new Exception("No sales admitted");
}

