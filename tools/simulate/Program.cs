using System.Diagnostics;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

int days = args.Length > 0 ? int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 28;
if (days < 1 || days > 365) throw new ArgumentOutOfRangeException(nameof(days), "Use 1 to 365 days.");
var places = new List<Place>();
var nodes = new List<GraphNode>();
var edges = new List<GraphEdge>();
for (int x = 0; x < 6; x++)
for (int z = 0; z < 6; z++)
{
    string id = $"home-{x}-{z}";
    var point = new Position(x * 180, 0, z * 180);
    places.Add(new Place { Id = id, Name = $"Sample home {x},{z}", District = x < 3 ? "West" : "East", Kind = PlaceKind.Home, Capacity = 12, Position = point });
    nodes.Add(new GraphNode(id, point));
}
void Connect(string a, string b, double minutes, TravelMode mode = TravelMode.Walk, string service = "")
{
    edges.Add(new GraphEdge($"{mode}:{a}>{b}", a, b, mode, minutes, mode == TravelMode.Bus ? 2 : 0, mode == TravelMode.Bus ? 10 : 0, service));
    edges.Add(new GraphEdge($"{mode}:{b}>{a}", b, a, mode, minutes, mode == TravelMode.Bus ? 2 : 0, mode == TravelMode.Bus ? 10 : 0, service));
}
for (int x = 0; x < 6; x++)
for (int z = 0; z < 6; z++)
{
    if (x < 5) Connect($"home-{x}-{z}", $"home-{x + 1}-{z}", 180.0 / 84);
    if (z < 5) Connect($"home-{x}-{z}", $"home-{x}-{z + 1}", 180.0 / 84);
}
for (int x = 0; x < 5; x++) Connect($"home-{x}-2", $"home-{x + 1}-2", 0.5, TravelMode.Bus, "sample-bus");
void Venue(string id, PlaceKind kind, int x, int z, int capacity)
{
    var point = new Position(x * 180 + 12, 0, z * 180 + 12);
    places.Add(new Place { Id = id, Name = "Sample " + id, District = x < 3 ? "West" : "East", Kind = kind, Capacity = capacity, Position = point });
    nodes.Add(new GraphNode(id, point));
    Connect($"home-{x}-{z}", id, 0.25);
}
for (int i = 0; i < 6; i++) Venue("work-" + i, PlaceKind.Work, i, 2, 60);
Venue("study-west", PlaceKind.Study, 1, 4, 32);
Venue("study-east", PlaceKind.Study, 4, 4, 32);
foreach (var (x, z) in new[] { (0, 1), (0, 5), (5, 1), (5, 5) })
{
    Venue($"shop-{x}-{z}", PlaceKind.Shop, x, z, 16);
    Venue($"leisure-{x}-{z}", PlaceKind.Leisure, x, z, 16);
}
var people = Enumerable.Range(0, 400).Select(i => new CitizenIdentity
{
    Id = $"sample:{i:D3}", Name = $"Sample Citizen {i:D3}", Age = i % 8 == 4 ? 70 : i % 8 == 3 ? 21 : 35,
    Female = i % 2 == 0, District = i % 2 == 0 ? "West" : "East", Persona = (Persona)(i % 8), AppearanceSeed = i + 1
});
var state = Population.Create(people, places, "synthetic-demonstration-only", "synthetic; no RLO or game loaded", 42, 1440);
var planner = new MeteredPlanner(new GraphRoutePlanner(nodes, edges));
var engine = new CitySimulator(state, planner);
var watch = Stopwatch.StartNew();
int events = 0, frames = 0;
while (true)
{
    var step = engine.AdvanceTo(1440 * (days + 1.0), 200);
    events += step.Events; frames++;
    if (step.CaughtUp) break;
}
watch.Stop();
var json = SaveCodec.Write(state);
var restored = SaveCodec.Read(json);
if (restored.Status != SaveReadStatus.Loaded || SaveCodec.Write(restored.State!) != json) throw new Exception("Snapshot round trip failed.");
var report = new
{
    Scenario = "Synthetic connected grid, not game performance or RLO population evidence",
    Citizens = state.Citizens.Count, Days = days, Events = events, BoundedBatches = frames,
    ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds, CompletedJourneys = state.Citizens.Sum(c => c.CompletedJourneys),
    MissedActivities = state.Citizens.Sum(c => c.MissedActivities), planner.RouteRequests, planner.Unreachable,
    PlannedModeCounts = planner.Modes, SnapshotBytes = System.Text.Encoding.UTF8.GetByteCount(json),
    SaveRoundTrip = "Exact", EconomyTransactions = "Not implemented in this project"
};
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
if (args.Length > 1)
{
    string output = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(output, "population.json"), json);
}

sealed class MeteredPlanner(IRoutePlanner inner) : IRoutePlanner
{
    public int RouteRequests { get; private set; }
    public int Unreachable { get; private set; }
    public Dictionary<string, int> Modes { get; } = new();
    public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
    {
        RouteRequests++;
        var route = inner.Plan(from, to, at, preferences);
        if (route == null) Unreachable++;
        else
        {
            string mode = route.Legs.Any(l => l.Mode == TravelMode.Bus) ? "Bus" : route.Legs.Any(l => l.Mode == TravelMode.Car) ? "Car" : "Walk";
            Modes[mode] = Modes.GetValueOrDefault(mode) + 1;
        }
        return route;
    }
}
