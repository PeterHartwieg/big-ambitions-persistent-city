using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;

int checks = 0;
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
var fixture = new GameInstance();
SaveGameManager.Current = fixture;
CityManager.Registered = new CityManager { cityBuildingControllers = new[] {
    new CityBuildingController { building = new Building { SpecialService = new Buildings.SpecialService { settings = new Buildings.MarketingAgencySettings() } } },
    new CityBuildingController() } };
DialogController.current = new DialogController { contact = new Contact() };
for (int i = 0; i < 4096; i++) fixture.Transactions.Enqueue(new Transaction { amount = -i - 0.125f });
fixture.Transactions.Enqueue(new Transaction { transactionType = "other", transactionCategories = new() });
fixture.financialSummaries.Add(new Summary { businessIncomeStatements = new() { new Statement() } });
string Fingerprint() => JsonSerializer.Serialize(new { fixture, city = CityManager.Registered, dialog = DialogController.current }, new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true });
string before = Fingerprint();
long startBytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
string dump = AgencyBindingDiagnostic.Capture();
double cold = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
long coldBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
Check(dump.Contains("money(binary32 dollars)=1000.125"), "raw money");
Check(dump.Contains("options=SmallInternet;SmallBillboard;"), "native option list");
Check(dump.Contains("amount=-4095.125") && !dump.Contains("amount=-4079.125"), "last sixteen only");
Check(dump.Contains("day=8 hour=23 minute=59.5") && dump.Contains("ba:transactioncategory_marketing"), "transaction facts");
Check(dump.Contains("MarketingExpenses=100.125") && dump.Contains("provider=Broadway:1"), "expense and provider");
for (int i = 0; i < 100; i++) Check(dump == AgencyBindingDiagnostic.Capture(), "repeat dump");
Check(before == Fingerprint(), "native-double fingerprint unchanged after 101 reads");
var port = new AgencyPaymentPort();
var quote = new AgencyQuote(new AgencyRequestId(Guid.NewGuid()), new AgencyIdentity(Guid.NewGuid(), "fixture"), new ResearchScopePlaceholder("unresolved"), Money.FromUnits(1));
bool entered = false;
Check(port.Validate(quote) == AgencyPaymentRefusal.Unavailable, "Runtime unavailable");
Check(port.Charge(quote, () => entered = true, out var evidence) == AgencyNativeOutcome.Refused && !entered && evidence == null, "Runtime never enters native");

// Inspect emitted actual-source IL, not just method names in a mock. Any field write or native
// method call in the diagnostic/refusing port fails. Collection enumeration is allowed, mutation is not.
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
    .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
var native = new HashSet<Type> { typeof(GameInstance), typeof(SaveGameManager), typeof(CityManager), typeof(CityBuildingController), typeof(Building),
    typeof(Address), typeof(DialogController), typeof(Contact), typeof(Transaction), typeof(Timestamp), typeof(Summary), typeof(Statement),
    typeof(Buildings.SpecialService), typeof(Buildings.MarketingAgencySettings) };
foreach (var type in new[] { typeof(AgencyBindingDiagnostic), typeof(AgencyPaymentPort) })
foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
{
    var il = method.GetMethodBody()?.GetILAsByteArray(); if (il == null) continue;
    for (int p = 0; p < il.Length;)
    {
        ushort value = il[p++]; if (value == 0xfe) value = (ushort)(0xfe00 | il[p++]);
        var op = opcodes[value];
        Check(op != OpCodes.Stfld && op != OpCodes.Stsfld, $"field write in {method.Name}");
        int size = op.OperandType switch
        {
            OperandType.InlineNone => 0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2, OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, p), _ => 4
        };
        if (op.OperandType == OperandType.InlineMethod)
        {
            var target = method.Module.ResolveMethod(BitConverter.ToInt32(il, p))!;
            Check(!native.Contains(target.DeclaringType!), "native method call: " + target);
            Check(target.Name != "set_Item" && target.Name != "Add" && target.Name != "Enqueue" && target.Name != "Remove" && target.Name != "Clear", "collection mutation: " + target);
            Check(target.DeclaringType != typeof(IAgencyPaymentPort), "diagnostic called payment port");
        }
        p += size;
    }
}
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
foreach (string folder in new[] { "Runtime", "Core", "Persistence", "Compatibility" })
foreach (string path in Directory.GetFiles(Path.Combine(root, folder), "*.cs"))
{
    if (Path.GetFileName(path) is "AgencyOrders.cs" or "AgencyPaymentPort.cs" or "AgencyBindingDiagnostic.cs") continue;
    string source = File.ReadAllText(path);
    foreach (string symbol in new[] { "AgencyOrderBook", "IAgencyPaymentPort", "AgencyPaymentPort", "AgencyBindingDiagnostic" })
        Check(!source.Contains(symbol, StringComparison.Ordinal), "gameplay/persistence consumer: " + path);
}
// Warm individual-call timings; fixture setup, fingerprinting and JIT excluded.
var timings = new double[300];
startBytes = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < timings.Length; i++)
{
    start = Stopwatch.GetTimestamp(); _ = AgencyBindingDiagnostic.Capture();
    timings[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
}
long warmBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
Array.Sort(timings);
Console.WriteLine($"C3 {checks} actual-source/double/IL/isolation checks passed; no native proof.");
Console.WriteLine($"4096 marketing records, 2 buildings, 1 statement: cold incl first Capture JIT {cold:F4} ms / {coldBytes} B; warm p50/p95/max {timings[149]:F4}/{timings[284]:F4}/{timings[299]:F4} ms; {warmBytes / timings.Length} B/call.");
Console.WriteLine($"Offline dump spikes >=30 ms: {timings.Count(t => t >= 30) + (cold >= 30 ? 1 : 0)}; closed: zero Capture calls, hooks, retained state or per-frame allocations. Full mod-work and whole-frame unmeasured.");
SaveGameManager.Current = null; CityManager.Registered = null;
Check(AgencyBindingDiagnostic.Capture().Contains("unavailable"), "unloaded refusal");
