using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

// F7b save-epoch overhead (plan F7 section 4 row: callback overhead p50/p95/max and allocation) and
// writer-25 byte equality. Offline .NET component timing; not native save pause or frame evidence.
internal static class Program
{
    private const int Warm = 2_000, Samples = 20_000;

    private static void Main()
    {
        var city = Fixture(957);
        string payload = SaveCodec.Write(city);
        // Writer 25 is unchanged: the epoch path publishes the SaveSession string byte for byte.
        var save = new object();
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { [SaveStore.StateKey] = payload };
        var session = SaveSession.Open(data, city.PopulationKey);
        var binding = new SaveEpochBinding(save, data, 0);
        var result = SaveEpochCallback.Run(binding, save, data, false, ticket =>
        {
            var written = session.WriteTo(data, city);
            return written.Success ? SaveEpochCapture.Written(city.Minute, city.PopulationKey) : SaveEpochCapture.Refused(written.Error);
        }, _ => throw new Exception("stop"), _ => { });
        Require(result == SaveEpochResult.Published, "fixture save did not publish");
        Require(Hash(data[SaveStore.StateKey]) == Hash(payload) && data[SaveStore.StateKey] == SaveCodec.Write(city), "writer-25 bytes changed");
        Require(SaveEpochStore.CheckForStart(data, out var marker) == null, "fixture marker refused");
        Require(SaveEpochStore.DescribesState(marker, CityState.CurrentSchema, city) == null, "fixture marker mismatch");

        // Before: the base callback's publication step (assign the encoded string). After: the same
        // assignment inside the F7b callback (ticket, binding checks, marker format and write).
        string[] payloads = { payload, new string(payload.AsSpan()) }; // Alternate references like successive saves.
        var before = Measure(i => { data[SaveStore.StateKey] = payloads[i & 1]; });

        int call = 0;
        Func<SaveEpochTicket, SaveEpochCapture> capture = ticket => { data[SaveStore.StateKey] = payloads[call & 1]; return SaveEpochCapture.Written(city.Minute, city.PopulationKey); };
        var after = Measure(i =>
        {
            call = i;
            if (SaveEpochCallback.Run(binding, save, data, false, capture, _ => throw new Exception("stop"), _ => { }) != SaveEpochResult.Published)
                throw new Exception("publication refused");
        });
        var load = Measure(_ =>
        {
            if (SaveEpochStore.CheckForStart(data, out var m) != null || SaveEpochStore.DescribesState(m, CityState.CurrentSchema, city) != null)
                throw new Exception("load check refused");
        });
        long started = Stopwatch.GetTimestamp();
        for (int i = 0; i < 5; i++) SaveCodec.Write(city);
        double encode = Ms(Stopwatch.GetTimestamp() - started) / 5;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            residents = 957, payloadChars = payload.Length, markerChars = data[SaveEpochStore.Key].Length, samples = Samples,
            beforePublishMs = before.Summary, afterCallbackMs = after.Summary, loadCheckMs = load.Summary,
            beforeBytesPerCall = before.Bytes, afterBytesPerCall = after.Bytes, loadBytesPerCall = load.Bytes,
            spikesAtLeast30Ms = before.Spikes + after.Spikes + load.Spikes, writer25EncodeMeanMs = encode,
            writer25Sha256 = Hash(payload)
        }));
        Console.WriteLine("PASS F7b save-epoch overhead and writer-25 byte equality (offline component timing)");
    }

    private static (string Summary, double Bytes, int Spikes) Measure(Action<int> action)
    {
        for (int i = 0; i < Warm; i++) action(i);
        var times = new double[Samples];
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Samples; i++)
        {
            long t = Stopwatch.GetTimestamp(); action(i); times[i] = Ms(Stopwatch.GetTimestamp() - t);
        }
        double bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)Samples;
        Array.Sort(times);
        string F(double v) => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        return (F(times[Samples / 2]) + " / " + F(times[(int)(Samples * .95)]) + " / " + F(times[^1]), bytes, times.Count(t => t >= 30));
    }

    private static CityState Fixture(int residents)
    {
        var identities = Enumerable.Range(0, residents).Select(i => new CitizenIdentity
            { Id = "resident-" + i.ToString("D4"), Name = "Resident " + i, Age = 20 + i % 50, Persona = (Persona)(i % 3) }).ToArray();
        var places = Enumerable.Range(0, 64).Select(i => new Place
            { Id = "home-" + i.ToString("D2"), Name = "Home " + i, Kind = PlaceKind.Home, Capacity = 32 }).ToArray();
        return Population.Create(identities, places, "rlo:0.40.4:f7b", "0.40.4", 42, 600);
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
