using System.Diagnostics;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

static class Integrated
{
    sealed class Routes : IRoutePlanner
    {
        public long Calls;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
        {
            Calls++;
            return new Route { Legs = { new RouteLeg { From = from.Position, To = to.Position,
                Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };
        }
    }
    sealed class Market : IFoodMarket
    {
        public long Reads, Sales;
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute)
        {
            Reads++;
            return new[] { new FoodBusiness { Id = "b1", PlaceId = "shop-1", Name = "Shop 1", MealsPerHour = 10000,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 1000000 } } } };
        }
        public FoodSale Buy(ResidentMeal meal, double minute) { Sales++; return new FoodSale(true, "detached confirmed"); }
    }
    public static void Run()
    {
        foreach (int residents in new[] { 957, 5000, 50000 })
        {
            var state = EconomyScenario.Create(residents, 23); state.Food = EconomyScenario.Food();
            var routes = new Routes(); var market = new Market();
            var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
            double target = 2910;
            var times = new List<double>(); var allocated = new List<long>();
            long events = 0; double maximumLag = 0;
            // Cold, real scheduler replay from 05:00 through all meal peaks and next midnight.
            // Fixed target measures backlog drain, not real-time calendar overload/backpressure (F1).
            while (!city.IsCaughtUpTo(target))
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                int units = 0;
                do
                {
                    var result = city.AdvanceFrameSlice(target, 1); units += result.Events;
                    if (result.CaughtUp) break;
                } while (units < 16 && Stopwatch.GetElapsedTime(start).TotalMilliseconds < 2);
                times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                allocated.Add(GC.GetAllocatedBytesForCurrentThread() - bytes);
                events += units; maximumLag = Math.Max(maximumLag, target - state.Minute);
            }
            city.SynchronizeSnapshot();
            if (state.Food.Residents.Any(m => !HouseholdFinance.CashConserved(m.Budget!))) throw new Exception("Unconserved integrated cash");
            string final = SaveCodec.Write(state);
            var read = SaveCodec.Read(final);
            if (read.Status != SaveReadStatus.Loaded || SaveCodec.Write(read.State!) != final) throw new Exception("Integrated reload changed bytes: " + read.Error);
            times.Sort(); allocated.Sort();
            Console.WriteLine(JsonSerializer.Serialize(new { consumers = residents * 2, frames = times.Count, events,
                frameAdvanceMs = new { median = times[times.Count / 2], p95 = times[(int)(times.Count * .95)], max = times[^1],
                    spikesAtLeast30 = times.Count(t => t >= 30) },
                frameAllocationBytes = new { median = allocated[allocated.Count / 2], p95 = allocated[(int)(allocated.Count * .95)], max = allocated[^1], total = allocated.Sum() },
                maximumBacklogGameMinutes = maximumLag, finalBacklogGameMinutes = target - state.Minute,
                totalAdvanceMs = times.Sum(), eventsPerSecond = events / (times.Sum() / 1000), routes = routes.Calls, reads = market.Reads,
                sales = market.Sales, purchases = state.Food.Residents.Sum(m => m.Purchases),
                saveSha256 = EconomyScenario.Digest(final),
                evidence = "Cold Windows .NET 10 actual Core scheduler + detached routes/market; fixed backlog drain; excludes construction, per-frame snapshot publication and serialization; no Unity, traffic graph, native or whole-frame evidence" }));
        }
    }
}
