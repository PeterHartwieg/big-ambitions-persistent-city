using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using PersistentCity.Tests.Acceptance;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>F9e (plan f9-city-acceptance.md, Decision 8): observers read the city through <see cref="CitySimulator.View"/>.
/// A source test finds every <c>.State</c> access in Runtime by the type of its receiver (no Roslyn: each file's and its
/// partial siblings' declarations), allows <see cref="CitySimulator"/> receivers only in the owner files and three named
/// observer sinks, and fails on a receiver it cannot resolve. A reflection test walks every type reachable from
/// <see cref="ICityView"/>. The output test compares what the panel, inspector and diagnostics read through the view with
/// the same reads through the state, on the F5h and F9c cities.</summary>
[Collection(StreetFoodHooks.Collection)]
public sealed class OwnershipBoundaryTests
{
    private readonly ITestOutputHelper output;
    public OwnershipBoundaryTests(ITestOutputHelper output) { this.output = output; }

    // ---- The source scan ----

    /// <summary>Files that may read the simulator's mutable state: load and save, accounting recovery, the E6 market that
    /// owns vendor-row writes, presentation until F9g, F2e's hold publication and replay, and weather diagnostics (an
    /// observer outside F9e's file list, named in docs/checks/f9e.md as still reading the state).</summary>
    internal static readonly Dictionary<string, string> AllowedFiles = new(StringComparer.Ordinal)
    {
        ["PersistentCityMod.cs"] = "load and save",
        ["AccountingRecovery.cs"] = "accounting epoch recovery",
        ["StreetFoodVendorMarket.cs"] = "E6 market: vendor-row writes and the food state it serves",
        ["PhysicalPopulation.cs"] = "presentation, until F9g",
        ["MovementDiagnostics.cs"] = "F2e player-hold publication and replay (writes)",
        ["WeatherDiagnostics.cs"] = "observer outside F9e's Owns; listed in docs/checks/f9e.md",
    };

    /// <summary>The F9e observer files. Each simulator <c>.State</c> read in them must be one of <see cref="NamedSinks"/>.</summary>
    internal static readonly string[] ObserverFiles =
    {
        "FoodEconomyPanel.cs", "AccountingStatus.cs", "ResearchDiagnostics.cs", "SaveCostStatus.cs", "VendorDiagnostics.cs", "PopulationCensusDiagnostics.cs",
    };

    /// <summary>Observer reads that hand the mutable object to a sink whose signature lives outside F9e: the line must contain
    /// the fragment, and each fragment is used exactly once.</summary>
    internal static readonly (string File, string Fragment, string Reason)[] NamedSinks =
    {
        ("SaveCostStatus.cs", "SaveCodec.CountRecords(simulation.State, payload)", "Persistence's record counter takes the state it wrote"),
        ("ResearchDiagnostics.cs", "? null : simulation.State.Research;", "the research window and ResearchReportText take the ledger"),
        ("VendorDiagnostics.cs", "simulation.State.Food!.Residents.Where(", "meals and FoodSaleReceipts.ReadStatus are not in the view"),
    };

    /// <summary>Types that own a <c>.State</c> member other than the simulator's, each with a cited example.</summary>
    internal static readonly Dictionary<string, string> OtherStateOwners = new(StringComparer.Ordinal)
    {
        ["VendorRow"] = "row.State, Runtime/StreetFoodVendorMarket.cs",
        ["IVendorRowView"] = "the F9e vendor-row view",
        ["ResearchOrder"] = "open[i].State, Runtime/ResearchReportText.cs",
        ["IResearchOrderView"] = "the F9e research-order view",
        ["NativeInteriorCityView"] = "NativeInteriorCityView.State, Runtime/InteriorGuardDiagnostics.cs",
        ["SaveReadResult"] = "stored.State, Runtime/PersistentCityMod.cs",
        ["PreparedCity"] = "prepared.State, Runtime/PersistentCityMod.cs (the city read off the main thread)",
        ["UnityEngine.Random"] = "the type UnityEngine.Random.State, Runtime/PhysicalPedestrians.cs",
    };

    /// <summary>Receivers whose type the scan cannot read from a declaration (lambda parameters, <c>var</c> of a call), with
    /// the type of the expression before <c>.State</c>. An entry applies to every use of that name in its file.</summary>
    internal static readonly (string File, string Receiver, string Type)[] KnownReceivers =
    {
        ("VendorDiagnostics.cs", "r", "VendorRow"), // market.Coordinator.Table.Rows.Select(r => ...)
        ("StreetFoodVendorMarket.cs", "row", "VendorRow"), // Coordinator.Table.Find, table.FindPlace, table.Rows
        ("PhysicalPopulation.cs", "row", "VendorRow"), // simulation.State.Vendors.FindPlace(id)
        ("ResearchReportText.cs", "open", "ResearchOrder"), // open[i], open = ledger.Open
        ("ResearchReportText.cs", "order", "ResearchOrder"), // ResearchOrder parameters and ledger.Open/Kept/Closed items
        ("PersistentCityMod.cs", "stored", "SaveReadResult"), // stored = session.InitialReadResult
        ("PersistentCityMod.cs", "done", "PreparedCity"), // done = pending.Result (Task<PreparedCity>)
        ("PersistentCityMod.cs", "prepared", "PreparedCity"), // prepared = pending.Result
    };

    private static readonly string[] EntityTypes =
        { "CityState", "CitizenState", "VisitorRoutine", "Place", "FoodScope", "FoodCommerceState", "ResearchOrder", "ResearchLedger", "VendorRow", "VendorTable", "EconomicLedger" };

    internal sealed record Access(string File, int Line, string Receiver, string? Type, string LineText);

    internal static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    internal static Dictionary<string, string> RuntimeSources()
        => Directory.GetFiles(Path.Combine(Root(), "Runtime"), "*.cs").ToDictionary(f => Path.GetFileName(f), File.ReadAllText, StringComparer.Ordinal);

    /// <summary>Blanks comments and string or char literals (keeping interpolation holes and line breaks), so only code is
    /// scanned and line numbers stay.</summary>
    internal static string Code(string source)
    {
        var code = new StringBuilder(source.Length);
        int i = 0;
        void Blank(int to) { for (; i < to && i < source.Length; i++) code.Append(source[i] == '\n' ? '\n' : ' '); }
        while (i < source.Length)
        {
            char c = source[i], next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/') { int end = source.IndexOf('\n', i); Blank(end < 0 ? source.Length : end); }
            else if (c == '/' && next == '*') { int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal); Blank(end < 0 ? source.Length : end + 2); }
            else if (c == '\'') { int j = i + 1; while (j < source.Length && source[j] != '\'') j += source[j] == '\\' ? 2 : 1; Blank(j + 1); }
            else if (c == '"' || (c == '@' || c == '$') && (next == '"' || next == '@' || next == '$'))
            {
                bool verbatim = false, interpolated = false; int j = i;
                while (source[j] != '"') { verbatim |= source[j] == '@'; interpolated |= source[j] == '$'; j++; }
                Blank(j + 1);
                while (i < source.Length && source[i] != '"')
                {
                    if (interpolated && source[i] == '{' && i + 1 < source.Length && source[i + 1] == '{') { Blank(i + 2); continue; }
                    if (interpolated && source[i] == '{')
                    {
                        // A hole is code; nested braces count, nested literals inside a hole are not expected in Runtime.
                        int depth = 0;
                        for (; i < source.Length; i++)
                        {
                            code.Append(source[i]);
                            if (source[i] == '{') depth++;
                            else if (source[i] == '}' && --depth == 0) { i++; break; }
                        }
                        continue;
                    }
                    if (!verbatim && source[i] == '\\') { Blank(i + 2); continue; }
                    if (verbatim && source[i] == '"' && i + 1 < source.Length && source[i + 1] == '"') { Blank(i + 2); continue; }
                    Blank(i + 1);
                }
                Blank(i + 1);
            }
            else { code.Append(c); i++; }
        }
        return code.ToString();
    }

    private static readonly Regex Declaration = new(
        @"(?<![\w.])(?<type>[A-Z]\w*(?:\.[A-Z]\w*)*(?:<[^<>;(){}=]*(?:<[^<>;(){}=]*>)?[^<>;(){}=]*>)?(?:\[\])?\??)\s+(?<name>[a-z_]\w*)\s*(?=[=;,)&|:]|\bin\b)",
        RegexOptions.Compiled);
    private static readonly Regex LambdaParameters = new(@"(?:\b(?<name>[a-z_]\w*)|\((?<name>[a-z_]\w*)(?:\s*,\s*(?<name>[a-z_]\w*))*\))\s*=>", RegexOptions.Compiled);
    private static readonly Regex VarDeclaration = new(@"\bvar\s+(?<name>[a-z_]\w*)\s*=\s*(?<expr>[^;]+);", RegexOptions.Compiled);
    private static readonly Regex StateAccess = new(@"\.\s*State\b(?!\s*\()", RegexOptions.Compiled);
    private static readonly Regex PartialClass = new(@"\bpartial\s+class\s+(?<name>\w+)", RegexOptions.Compiled);

    private static void Declare(Dictionary<string, HashSet<string>> table, string name, string type)
    {
        if (!table.TryGetValue(name, out var types)) table.Add(name, types = new HashSet<string>(StringComparer.Ordinal));
        types.Add(type);
    }

    /// <summary>Every declared name in one file and the types it is declared with; <c>var x = y</c> copies y's types.</summary>
    private static Dictionary<string, HashSet<string>> Declarations(string code)
    {
        var table = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (Match m in Declaration.Matches(code))
            if (m.Groups["type"].Value != "var") Declare(table, m.Groups["name"].Value, m.Groups["type"].Value.TrimEnd('?'));
        // An untyped lambda parameter has no declared type: it makes its name unresolved unless a candidate is CitySimulator.
        foreach (Match m in LambdaParameters.Matches(code))
            foreach (Capture name in m.Groups["name"].Captures) Declare(table, name.Value, "?");
        return table;
    }

    private static void DeclareVars(string code, Dictionary<string, HashSet<string>> own, Func<string, IReadOnlyCollection<string>?> resolve)
    {
        foreach (Match m in VarDeclaration.Matches(code))
        {
            // A bare name, or a name before ?? (var active = simulation ?? throw ...), copies that name's types.
            var copy = Regex.Match(m.Groups["expr"].Value.Trim(), @"^(?<name>[A-Za-z_]\w*)\s*[!?]?\s*(?:\?\?.*)?$");
            var types = copy.Success ? resolve(copy.Groups["name"].Value) : null;
            if (types == null || types.Count == 0) Declare(own, m.Groups["name"].Value, "?");
            else foreach (var t in types) Declare(own, m.Groups["name"].Value, t);
        }
    }

    /// <summary>The single type a name resolves to: CitySimulator wins over any other candidate (conservative); several
    /// other candidates or an unknown <c>var</c> leave it unresolved.</summary>
    private static string? Single(IReadOnlyCollection<string>? types)
    {
        if (types == null || types.Count == 0) return null;
        if (types.Contains("CitySimulator")) return "CitySimulator";
        return types.Count == 1 && !types.Contains("?") ? types.First() : null;
    }

    /// <summary>The element type of an indexed receiver declared as a list or array.</summary>
    private static string Element(string type)
    {
        var list = Regex.Match(type, @"^(?:I?ReadOnlyList|List|IList)<(?<t>[\w.]+)\??>$");
        if (list.Success) return list.Groups["t"].Value;
        return type.EndsWith("[]", StringComparison.Ordinal) ? type[..^2].TrimEnd('?') : "?";
    }

    /// <summary>Every <c>.State</c> access in the given files with its receiver and resolved type (null when unresolved).</summary>
    internal static List<Access> Scan(IReadOnlyDictionary<string, string> files)
    {
        var code = files.ToDictionary(f => f.Key, f => Code(f.Value), StringComparer.Ordinal);
        var own = code.ToDictionary(f => f.Key, f => Declarations(f.Value), StringComparer.Ordinal);
        var classes = code.ToDictionary(f => f.Key, f => PartialClass.Matches(f.Value).Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        IReadOnlyCollection<string>? Lookup(string file, string name)
        {
            if (own[file].TryGetValue(name, out var mine)) return mine;
            var siblings = new HashSet<string>(StringComparer.Ordinal);
            foreach (var other in code.Keys)
                if (other != file && classes[other].Overlaps(classes[file]) && own[other].TryGetValue(name, out var theirs)) siblings.UnionWith(theirs);
            return siblings.Count == 0 ? null : siblings;
        }
        foreach (var file in code.Keys) DeclareVars(code[file], own[file], name => Lookup(file, name));
        var found = new List<Access>();
        foreach (var (file, text) in code)
        {
            var lines = files[file].Split('\n');
            foreach (Match m in StateAccess.Matches(text))
            {
                int line = text.Take(m.Index).Count(ch => ch == '\n');
                string receiver = Receiver(text, m.Index, out bool indexed);
                string? type = KnownReceivers.Where(k => k.File == file && k.Receiver == receiver).Select(k => k.Type).FirstOrDefault();
                if (type != null) { }
                else if (OtherStateOwners.ContainsKey(receiver)) type = receiver;
                else if (receiver.Contains('.') || receiver.StartsWith("(", StringComparison.Ordinal)) type = null;
                else
                {
                    type = Single(Lookup(file, receiver));
                    if (type != null && indexed) type = Element(type);
                    if (type == "?") type = null;
                }
                found.Add(new Access(file, line + 1, receiver, type, lines[line].Trim()));
            }
        }
        return found;
    }

    /// <summary>The receiver before the dot at <paramref name="dot"/>: a name, a dotted name (minus <c>this.</c>), or
    /// "(...)" for a call or parenthesised expression. Null-forgiving, null-conditional and index suffixes are skipped.</summary>
    private static string Receiver(string code, int dot, out bool indexed)
    {
        indexed = false;
        int i = dot - 1;
        void Space() { while (i >= 0 && char.IsWhiteSpace(code[i])) i--; }
        Space();
        while (i >= 0 && (code[i] == '!' || code[i] == '?')) { i--; Space(); }
        var parts = new List<string>();
        while (true)
        {
            if (i >= 0 && code[i] == ']')
            {
                int depth = 0;
                for (; i >= 0; i--) { if (code[i] == ']') depth++; else if (code[i] == '[' && --depth == 0) { i--; break; } }
                indexed = parts.Count == 0; Space();
            }
            if (i >= 0 && code[i] == ')') return "(" + string.Join(".", parts) + ")";
            int end = i;
            while (i >= 0 && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i--;
            if (end == i) return "(" + string.Join(".", parts) + ")";
            parts.Insert(0, code.Substring(i + 1, end - i));
            Space();
            while (i >= 0 && (code[i] == '!' || code[i] == '?') && i > 0 && code[i - 1] != '?') { i--; Space(); }
            if (i < 0 || code[i] != '.') break;
            i--; Space();
            while (i >= 0 && (code[i] == '!' || code[i] == '?')) { i--; Space(); }
        }
        if (parts.Count > 1 && parts[0] == "this") parts.RemoveAt(0);
        return string.Join(".", parts);
    }

    /// <summary>Every rule broken by the given Runtime files, one line per access.</summary>
    internal static List<string> Violations(IReadOnlyDictionary<string, string> files)
    {
        var violations = new List<string>();
        var used = new Dictionary<(string, string), int>();
        foreach (var a in Scan(files))
        {
            string where = a.File + ":" + a.Line + " '" + a.LineText + "'";
            if (a.Type == null) { violations.Add(where + ": receiver '" + a.Receiver + "' is not a declared or known .State owner"); continue; }
            if (a.Type != "CitySimulator")
            {
                if (!OtherStateOwners.ContainsKey(a.Type)) violations.Add(where + ": receiver type " + a.Type + " is not a known .State owner");
                continue;
            }
            if (AllowedFiles.ContainsKey(a.File)) continue;
            var sink = NamedSinks.FirstOrDefault(s => s.File == a.File && a.LineText.Contains(s.Fragment, StringComparison.Ordinal));
            if (sink.File == null) { violations.Add(where + ": reads CitySimulator.State outside the allowed files; observers read .View"); continue; }
            used[(sink.File, sink.Fragment)] = used.TryGetValue((sink.File, sink.Fragment), out var n) ? n + 1 : 1;
        }
        foreach (var ((file, fragment), count) in used)
            if (count > 1) violations.Add(file + ": named sink '" + fragment + "' used " + count + " times");
        foreach (var (file, text) in files)
        {
            if (!ObserverFiles.Contains(file)) continue;
            string code = Code(text);
            foreach (string entity in EntityTypes.Append(@"(?:I?ReadOnly)?List<[^>]*>"))
            {
                string pattern = entity.Contains('<') ? entity : Regex.Escape(entity);
                var cast = new Regex(@"\(\s*" + pattern + @"\??\s*\)\s*[\w(]|\b(?:as|is)\s+" + pattern + @"(?!\w)");
                foreach (Match m in cast.Matches(code))
                    violations.Add(file + ":" + (code.Take(m.Index).Count(ch => ch == '\n') + 1) + ": casts to " + m.Value.Trim() + "; observers keep the view type");
            }
        }
        return violations;
    }

    [Fact]
    public void RuntimeStateReadsAreOnTheAllowedListAndObserversUseTheView()
    {
        var files = RuntimeSources();
        var accesses = Scan(files);
        foreach (var a in accesses) output.WriteLine(a.File + ":" + a.Line + " " + a.Receiver + " -> " + (a.Type ?? "unresolved"));
        Assert.Empty(Violations(files));
        // Every listed file and sink exists, so the lists cannot go stale silently.
        foreach (var file in AllowedFiles.Keys.Concat(ObserverFiles)) Assert.True(files.ContainsKey(file), file + " is listed but missing");
        foreach (var (file, fragment, _) in NamedSinks)
            Assert.Single(accesses, a => a.File == file && a.Type == "CitySimulator" && a.LineText.Contains(fragment, StringComparison.Ordinal));
        // The observers that read the city at all read it through the view.
        foreach (var file in new[] { "FoodEconomyPanel.cs", "AccountingStatus.cs", "ResearchDiagnostics.cs", "PopulationCensusDiagnostics.cs" })
            Assert.Contains(".View", Code(files[file]));
        // The simulator reads found are the receivers the plan names, under every handle name.
        Assert.Contains(accesses, a => a.Type == "CitySimulator" && a.Receiver == "city" && a.File == "StreetFoodVendorMarket.cs");
        Assert.Contains(accesses, a => a.Type == "CitySimulator" && a.Receiver == "simulation" && a.File == "PersistentCityMod.cs");
    }

    [Fact]
    public void APlantedStateReadInADiagnosticsFixtureIsCaught()
    {
        var files = RuntimeSources();
        var clean = Violations(files);
        Assert.Empty(clean);
        string census = files["PopulationCensusDiagnostics.cs"];
        int anchor = census.LastIndexOf('}'); anchor = census.LastIndexOf('}', anchor - 1);
        string Plant(string body) => census.Insert(anchor, "\n        private void Planted() { " + body + " }\n    ");
        string Caught(string body)
        {
            var planted = new Dictionary<string, string>(files, StringComparer.Ordinal) { ["PopulationCensusDiagnostics.cs"] = Plant(body) };
            return Assert.Single(Violations(planted));
        }
        // The plan's case: a var copy of the simulator field under another name.
        Assert.Contains("observers read .View", Caught("var current = simulation; var m = current.State.Minute;"));
        Assert.Contains("observers read .View", Caught("var m = simulation!.State.Minute;"));
        Assert.Contains("observers read .View", Caught("var m = this.simulation?.State.Minute;"));
        Assert.Contains("observers read .View", Caught("var m = $\"{simulation.State.Minute}\";"));
        Assert.Contains("observers read .View", Caught("Read(simulation); } private void Read(CitySimulator sim) { var m = sim.State;"));
        // A receiver the scan cannot type fails rather than passing.
        Assert.Contains("is not a declared or known .State owner", Caught("var m = mystery.State;"));
        Assert.Contains("is not a declared or known .State owner", Caught("var m = Pick().State;"));
        Assert.Contains("is not a known .State owner", Caught("Read(null!); } private void Read(Unknown thing) { var m = thing.State;"));
        // A view cast back to an entity is refused in an observer file.
        Assert.Contains("casts to", Caught("var p = (CitizenState)simulation!.View.ResidentList[0];"));
        Assert.Contains("casts to", Caught("var p = simulation!.View.ResidentList as List<CitizenState>;"));
        // Text that only mentions the property is not a read.
        var quiet = new Dictionary<string, string>(files, StringComparer.Ordinal)
            { ["PopulationCensusDiagnostics.cs"] = Plant("var m = \"simulation.State\"; // current.State\n /* simulation.State */") };
        Assert.Empty(Violations(quiet));
        // A second use of a named sink fails.
        var twice = new Dictionary<string, string>(files, StringComparer.Ordinal);
        twice["SaveCostStatus.cs"] = files["SaveCostStatus.cs"].Replace("var counts = SaveCodec.CountRecords(simulation.State, payload);",
            "var counts = SaveCodec.CountRecords(simulation.State, payload); counts = SaveCodec.CountRecords(simulation.State, payload);");
        Assert.Contains(Violations(twice), v => v.Contains("used 2 times", StringComparison.Ordinal));
    }

    // ---- Reflection ----

    private static readonly Type[] Values = { typeof(string), typeof(bool), typeof(int), typeof(long), typeof(double), typeof(float), typeof(Position), typeof(Money) };

    private static bool Allowed(Type type, Queue<Type> views, out string why)
    {
        why = "";
        if (Nullable.GetUnderlyingType(type) is Type inner) type = inner;
        if (type.IsEnum || Values.Contains(type)) return true;
        if (type.IsInterface && type.Namespace == "PersistentCity.Core" && type.Name.EndsWith("View", StringComparison.Ordinal)) { views.Enqueue(type); return true; }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) return Allowed(type.GetGenericArguments()[0], views, out why);
        why = type.FullName ?? type.Name;
        return false;
    }

    [Fact]
    public void NoTypeReachableFromTheCityViewExposesASetterAnEntityOrAMutableCollection()
    {
        var seen = new HashSet<Type>(); var queue = new Queue<Type>(); queue.Enqueue(typeof(ICityView));
        var problems = new List<string>();
        while (queue.Count > 0)
        {
            var view = queue.Dequeue();
            if (!seen.Add(view)) continue;
            Assert.True(view.IsInterface, view.Name + " is not an interface");
            foreach (var type in view.GetInterfaces().Prepend(view))
            {
                foreach (var p in type.GetProperties())
                {
                    if (p.CanWrite || p.SetMethod != null) problems.Add(type.Name + "." + p.Name + " has a setter");
                    if (!Allowed(p.PropertyType, queue, out var why)) problems.Add(type.Name + "." + p.Name + " returns " + why);
                }
                foreach (var m in type.GetMethods().Where(m => !m.IsSpecialName))
                {
                    if (!Allowed(m.ReturnType, queue, out var why)) problems.Add(type.Name + "." + m.Name + "() returns " + why);
                    foreach (var parameter in m.GetParameters())
                        if (parameter.ParameterType.IsByRef || !Allowed(parameter.ParameterType, new Queue<Type>(), out _))
                            problems.Add(type.Name + "." + m.Name + " takes " + parameter.ParameterType.Name);
                }
                foreach (var e in type.GetEvents()) problems.Add(type.Name + "." + e.Name + " is an event");
            }
        }
        Assert.Empty(problems);
        // The walk reached every entity view, and each entity implements its view without a public member change.
        var expected = new[] { typeof(ICityView), typeof(IPersonView), typeof(IPlaceView), typeof(IFoodView), typeof(IFoodScopeView),
            typeof(IEconomicLedgerView), typeof(IResearchView), typeof(IResearchOrderView), typeof(IVendorRowView) };
        Assert.Equal(expected.OrderBy(t => t.Name), seen.OrderBy(t => t.Name));
        foreach (var (entity, view) in new[] { (typeof(CitizenState), typeof(IPersonView)), (typeof(Place), typeof(IPlaceView)),
            (typeof(FoodScope), typeof(IFoodScopeView)), (typeof(ResearchOrder), typeof(IResearchOrderView)),
            (typeof(VendorRow), typeof(IVendorRowView)), (typeof(EconomicLedger), typeof(IEconomicLedgerView)) })
        {
            Assert.True(view.IsAssignableFrom(entity), entity.Name + " does not implement " + view.Name);
            // Explicit implementations only: the view adds no public member, so the JSON and compact writers see the same shape.
            var map = entity.GetInterfaceMap(view);
            Assert.All(map.TargetMethods, m => Assert.True(m.IsPrivate && m.Name.Contains(view.Name, StringComparison.Ordinal), entity.Name + "." + m.Name));
        }
        // No view collection shares a name with a public collection anywhere else in Core or Persistence: the compact reader
        // takes each saved field's bound as the largest over every property of that name (CompactWire34.BuildFieldLimits),
        // so a shared name would loosen a saved bound. The specialized bounds stay as F7d pinned them.
        var viewCollections = seen.SelectMany(t => t.GetProperties()).Where(p => p.PropertyType != typeof(string)
            && typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("ExtraProductIds", viewCollections);
        foreach (var type in typeof(CityState).Assembly.GetTypes().Concat(typeof(SaveCodec).Assembly.GetTypes()).Where(t => !seen.Any(v => v.IsAssignableFrom(t))))
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.False(viewCollections.Contains(p.Name), type.Name + "." + p.Name + " shares its name with a view collection");
        Assert.Equal(FoodMenuPolicy.MaximumOffers - 1, SaveCodec.CompactCollectionLimit("AdditionalProductIds"));
        Assert.Equal(SaveCodec.MaxPortals, SaveCodec.CompactCollectionLimit("Portals"));
        // The city view implementation is not public, so an observer cannot name it to reach the state.
        Assert.False(typeof(CitySimulator).Assembly.GetType("PersistentCity.Core.CityView")!.IsPublic);
    }

    // ---- Outputs before and after ----

    /// <summary>What the panel (BuildEconomyText and the F6c overview), the economy overview, the accounting line, the research
    /// status, the census capture, the person command and the inspector read, through the state (the base code's reads).</summary>
    internal static string ThroughState(CitySimulator sim)
    {
        var city = sim.State; var food = city.Food!; var text = new StringBuilder();
        int day = (int)(city.Minute / 1440);
        text.AppendLine(PopulationWords.Headline(day, city.Citizens.Count, city.Visitors.Count, food.Residents.Count));
        text.AppendLine(food.MealRhythmVersion + "|" + PopulationWords.Lines(city.Visitors.Count, city.Portals.Count) + "|" + food.Shops.Count);
        var history = FoodFeedback.IndexHistory(food.History); int since = FoodFeedback.RecordingSince(food), since2 = FoodFeedbackV2.RecordingSince(food);
        foreach (var scope in food.Shops)
        {
            var place = city.Places.FirstOrDefault(p => p.Id == scope.PlaceId);
            text.AppendLine(scope.PlaceId + "|" + scope.BusinessId + "|" + place?.Name);
            var rows = history.TryGetValue(FoodFeedback.Key(scope.PlaceId, scope.BusinessId), out var r) ? r : new();
            text.AppendLine(FoodFeedback.Weeks(rows, scope.PlaceId, scope.BusinessId, day, since).Text());
            FoodFeedbackV2.AppendPeriods(text, rows, day, since2);
        }
        var money = FoodMoneyCounters.Rescan(food);
        text.AppendLine(money.ToString());
        FoodFeedbackV2.AppendCity(text, money, city.Economy, day);
        text.AppendLine(JsonConvert.SerializeObject(FoodEconomyInsight.Capture(city)));
        text.AppendLine("minute " + city.Minute.ToString("R"));
        var ledger = city.Research;
        text.AppendLine(ledger.Open.Count + "|" + ledger.Kept.Count + "|" + ledger.Closed.Count + "|" + ledger.NextSequence);
        foreach (var order in ResearchReportText.Orders(ledger)) text.AppendLine(ResearchReportText.Line(order, order.BusinessAddress));
        foreach (var r in city.Vendors.Rows) text.AppendLine(r.VehicleId + "|" + r.BusinessId + "|" + r.PlaceId + "|" + r.State + "|" + r.Reason);
        foreach (var c in PopulationCensusInput.CaptureResidents(city.Citizens)) text.Append(c.HomeId).Append(c.DayPlaceId).Append(c.Persona).Append(',');
        text.AppendLine();
        foreach (var id in city.Citizens.Select(c => c.Identity.Id).Concat(city.Visitors.Select(v => v.Id)))
            text.AppendLine(VisitorSchedules.DescribePerson(city, id, sim.FoodAnchorReads));
        foreach (var c in city.Citizens) Inspected(text, sim, c.Identity.Id);
        return text.ToString();
    }

    /// <summary>The same reads as the observer files now make them: through <see cref="CitySimulator.View"/>.</summary>
    internal static string ThroughView(CitySimulator sim)
    {
        var city = sim.View; var food = city.Food!; var text = new StringBuilder();
        int day = (int)(city.Minute / 1440);
        text.AppendLine(PopulationWords.Headline(day, city.ResidentList.Count, city.VisitorCount, food.ConsumerCount));
        text.AppendLine(food.MealRhythmVersion + "|" + PopulationWords.Lines(city.VisitorCount, city.PortalCount) + "|" + food.ScopeList.Count);
        var history = CityViewReports.IndexFoodHistory(city); int since = CityViewReports.FoodRecordingSince(city), since2 = CityViewReports.FoodRecordingSinceV2(city);
        foreach (var scope in food.ScopeList)
        {
            var place = city.PlaceList.FirstOrDefault(p => p.Id == scope.PlaceId);
            text.AppendLine(scope.PlaceId + "|" + scope.BusinessId + "|" + place?.Name);
            var rows = history.TryGetValue(FoodFeedback.Key(scope.PlaceId, scope.BusinessId), out var r) ? r : new();
            text.AppendLine(FoodFeedback.Weeks(rows, scope.PlaceId, scope.BusinessId, day, since).Text());
            FoodFeedbackV2.AppendPeriods(text, rows, day, since2);
        }
        var money = CityViewReports.RescanFoodMoney(city);
        text.AppendLine(money.ToString());
        CityViewReports.AppendCityEconomy(text, money, city, day);
        text.AppendLine(JsonConvert.SerializeObject(CityViewReports.EconomyOverview(city)));
        text.AppendLine("minute " + city.Minute.ToString("R"));
        var research = city.Research;
        text.AppendLine(research.OpenOrders.Count + "|" + research.KeptOrders.Count + "|" + research.ClosedOrders.Count + "|" + research.NextSequence);
        // The orders command still hands the ledger to ResearchReportText (a named sink); its order ids are the view's.
        var ids = ResearchReportText.Orders(sim.State.Research).Select(o => o.Id).ToList();
        Assert.Equal(ids.OrderBy(i => i, StringComparer.Ordinal), research.OpenOrders.Concat(research.KeptOrders).Concat(research.ClosedOrders).Select(o => o.Id).OrderBy(i => i, StringComparer.Ordinal));
        foreach (var order in ResearchReportText.Orders(sim.State.Research)) text.AppendLine(ResearchReportText.Line(order, order.BusinessAddress));
        foreach (var r in city.VendorRowList) text.AppendLine(r.VehicleId + "|" + r.BusinessId + "|" + r.PlaceId + "|" + r.State + "|" + r.Reason);
        foreach (var c in CityViewReports.CaptureCensusResidents(city)) text.Append(c.HomeId).Append(c.DayPlaceId).Append(c.Persona).Append(',');
        text.AppendLine();
        foreach (var id in city.ResidentList.Select(c => c.Id).Concat(sim.State.Visitors.Select(v => v.Id)))
            text.AppendLine(CityViewReports.DescribePerson(city, id, sim.FoodAnchorReads));
        foreach (var c in city.ResidentList) Inspected(text, sim, c.Id);
        return text.ToString();
    }

    private static void Inspected(StringBuilder text, CitySimulator sim, string id)
    {
        var insight = sim.Inspect(id)!;
        text.AppendLine(insight.Text()).AppendLine(insight.Home?.Label + "|" + insight.Work?.Label + "|" + insight.WorkRole + "|" + insight.HouseholdSize
            + "|" + string.Join(",", insight.Personality) + "|" + insight.PersonWords + "|" + string.Join(",", insight.MemoryWords));
    }

    /// <summary>The F5h reference city one day and seven hours in (meals, journeys and the first fill under way).</summary>
    internal static CitySimulator F5hCity()
    {
        var shape = F5hIntegratedAcceptanceTests.ReferenceShape();
        var (state, market) = shape.City();
        var city = F5gPopulationFillTests.Start(state, market, shape.Catalogue, new F5gPopulationFillTests.CachedPortalPlanner());
        double target = city.State.Minute + 1440 + 7 * 60;
        while (!city.IsCaughtUpTo(target)) city.AdvanceFrameSlice(target, 4096);
        return city;
    }

    /// <summary>The F9c city-s reference run's final city (three days, research reports and a vendor row), loaded.</summary>
    internal static CitySimulator F9cCity()
    {
        var run = CityScenario.Run(CityFixture.Create(CitySize.Small()), CityArm.ReferenceArm);
        return new CitySimulator(SaveCodec.Read(run.Final).State!, new CityFixture.SyncWalk());
    }

    [Fact]
    public void PanelInspectorAndDiagnosticOutputsAreIdenticalThroughTheViewOnTheF5hAndF9cCities()
    {
        foreach (var (name, city) in new[] { ("f5h", F5hCity()), ("f9c", F9cCity()) })
        {
            string before = ThroughState(city), after = ThroughView(city);
            output.WriteLine(name + " outputs sha256 " + CityOracles.Sha(after) + ", " + after.Length + " characters");
            Assert.Equal(before, after);
            // Non-trivial city: the reads cover residents, places, shops and (in F9c) research and vendor rows.
            Assert.True(city.View.ResidentList.Count > 100);
            Assert.NotEmpty(city.View.Food!.ScopeList);
            if (name == "f9c") { Assert.NotEmpty(city.View.Research.KeptOrders); Assert.NotEmpty(city.View.VendorRowList); }
            // Every view member reads the live entity: compare each with the state field it names.
            var s = city.State; var v = city.View;
            Assert.Same(s.Citizens, v.ResidentList); Assert.Same(s.Places, v.PlaceList); Assert.Same(s.Food!.Shops, v.Food!.ScopeList);
            Assert.Same(s.VisitorRoutines, v.VisitorRoutineList); Assert.Same(s.Vendors.Rows, v.VendorRowList); Assert.Same(s.Economy, v.Economy);
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                var c = s.Citizens[i]; IPersonView p = c;
                Assert.Equal((c.Identity.Id, c.Identity.Name, c.Identity.District, c.Identity.Age, c.Identity.Female, c.Identity.Persona, c.HomeId, c.DayPlaceId),
                    (p.Id, p.Name, p.District, p.Age, p.Female, p.Persona, p.HomeId, p.DayPlaceId));
                Assert.Equal((c.ShopId, c.LeisureId, c.Phase, c.Activity, c.PlaceId, c.DestinationId, c.DestinationActivity, c.DepartureMinute),
                    (p.ShopId, p.LeisureId, p.Phase, p.Activity, p.PlaceId, p.DestinationId, p.DestinationActivity, p.DepartureMinute));
                Assert.Equal((c.NextEventMinute, c.ActivityUntilMinute, c.CompletedJourneys, c.MissedActivities, c.JoinedMinute, c.EmploymentStartDay, c.JobChanges, c.PlayerHoldMinutes, c.IsVisitor),
                    (p.NextEventMinute, p.ActivityUntilMinute, p.CompletedJourneys, p.MissedActivities, p.JoinedMinute, p.EmploymentStartDay, p.JobChanges, p.PlayerHoldMinutes, p.IsVisitor));
            }
            foreach (var place in s.Places)
            {
                IPlaceView p = place;
                Assert.Equal((place.Id, place.Name, place.District, place.Kind, place.Position, place.Capacity, place.OpenMinute, place.CloseMinute),
                    (p.Id, p.Name, p.District, p.Kind, p.Position, p.Capacity, p.OpenMinute, p.CloseMinute));
                Assert.Equal((place.AttractionWeight, place.NativeTrafficIndex, place.EvidenceSource, place.ResidentialAreaSquareMetres),
                    (p.AttractionWeight, p.NativeTrafficIndex, p.EvidenceSource, p.ResidentialAreaSquareMetres));
            }
            foreach (var scope in s.Food.Shops)
            {
                IFoodScopeView f = scope;
                Assert.Equal((scope.PlaceId, scope.BusinessId, scope.ProductId, scope.Channel, scope.AlternativeKind, scope.NextServiceAt),
                    (f.PlaceId, f.BusinessId, f.ProductId, f.Channel, f.AlternativeKind, f.NextServiceAt));
                Assert.Equal(scope.AdditionalProductIds, f.ExtraProductIds);
            }
            foreach (var order in s.Research.Open.Concat(s.Research.Kept).Concat(s.Research.Closed))
            {
                IResearchOrderView o = order;
                Assert.Same(o, v.Research.Find(order.Id));
                Assert.Equal((order.Id, order.Sequence, order.ProductCode, order.Product.PriceDollars, order.Price, order.BusinessAddress, order.AgencyAddress, order.TaxDeductible),
                    (o.Id, o.Sequence, o.ProductCode, o.PriceDollars, o.Price, o.BusinessAddress, o.AgencyAddress, o.TaxDeductible));
                Assert.Equal((order.RadiusMetres, order.OrderMinute, order.SurveyAtMinute, order.State, order.DeclineReason, order.ReportId, order.Report != null, order.IsOpen),
                    (o.RadiusMetres, o.OrderMinute, o.SurveyAtMinute, o.State, o.DeclineReason, o.ReportId, o.HasReport, o.IsOpen));
            }
            foreach (var row in s.Vendors.Rows)
            {
                IVendorRowView r = row;
                Assert.Same(r, v.FindVendor(row.VehicleId));
                Assert.Equal((row.VehicleId, row.Kind, row.Generation, row.State, row.PlaceId, row.Signature, row.BusinessId, row.PlacePublished, row.Reason),
                    (r.VehicleId, r.Kind, r.Generation, r.State, r.PlaceId, r.Signature, r.BusinessId, r.PlacePublished, r.Reason));
                Assert.Equal((row.Authority.Desired, row.Authority.Managed, row.Authority.Suspended, row.Authority.HandbackPending, row.Authority.OpeningId, row.Stand != null),
                    (r.AuthorityDesired, r.AuthorityManaged, r.Suspended, r.HandbackPending, r.OpeningId, r.HasStand));
            }
            Assert.Equal((s.Economy.Outside.Count, s.Economy.Shops.Count), (v.Economy.OutsideDays, v.Economy.ObservedShops));
            Assert.Equal((s.Visitors.Count, s.Portals.Count, s.Food.Residents.Count, s.Food.MealRhythmVersion, s.Food.ChoiceStartsDay),
                (v.VisitorCount, v.PortalCount, v.Food.ConsumerCount, v.Food.MealRhythmVersion, v.Food.ChoiceStartsDay));
        }
    }

    [Fact]
    public void TheViewIsLiveAndAStateWithoutFoodHasNoFoodView()
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "a", Name = "A", Age = 30 } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } }, "f9e", "0", 1, 1440);
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        var view = city.View;
        Assert.Same(view, city.View);
        Assert.Null(view.Food);
        Assert.Equal(1440, view.Minute);
        // A replaced list and a changed value are seen at once: the view is the state, not a copy.
        state.Places = new List<Place> { new() { Id = "other", Name = "Other" } };
        Assert.Equal("other", Assert.Single(view.PlaceList).Id);
        state.Minute = 2000;
        Assert.Equal(2000, view.Minute);
        state.Food = new FoodCommerceState();
        Assert.NotNull(view.Food);
        Assert.Empty(view.Food!.ScopeList);
        // A view from elsewhere cannot reach the reports.
        Assert.Throws<ArgumentException>(() => CityViewReports.DescribePerson(new ForeignView(), "a", 0));
    }

    private sealed class ForeignView : ICityView
    {
        public double Minute => 0;
        public IReadOnlyList<IPersonView> ResidentList => Array.Empty<IPersonView>();
        public IReadOnlyList<IPersonView> VisitorRoutineList => Array.Empty<IPersonView>();
        public int VisitorCount => 0;
        public int PortalCount => 0;
        public IReadOnlyList<IPlaceView> PlaceList => Array.Empty<IPlaceView>();
        public IFoodView? Food => null;
        public IEconomicLedgerView Economy => new EconomicLedger();
        public IResearchView Research => throw new NotSupportedException();
        public IReadOnlyList<IVendorRowView> VendorRowList => Array.Empty<IVendorRowView>();
        public IVendorRowView? FindVendor(string vehicleId) => null;
    }

    // ---- Allocation ----

    /// <summary>The panel's view reads in BuildEconomyText and DrawEconomy, as index loops (no boxed enumerator).</summary>
    private static long PanelReads(ICityView city)
    {
        long sum = (long)city.Minute / 1440 + city.ResidentList.Count + city.VisitorCount + city.PortalCount;
        var food = city.Food!;
        sum += food.ConsumerCount + food.MealRhythmVersion;
        var scopes = food.ScopeList;
        for (int i = 0; i < scopes.Count; i++) sum += scopes[i].PlaceId.Length + scopes[i].BusinessId.Length;
        var places = city.PlaceList;
        for (int i = 0; i < places.Count; i++) sum += places[i].Id.Length + places[i].Name.Length;
        return sum;
    }

    [Fact]
    public void PanelRefreshReadsThroughTheViewAllocateNothing()
    {
        var city = F5hCity();
        long expected = PanelReads(city.View);
        for (int i = 0; i < 100; i++) PanelReads(city.View);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long total = 0;
        const int refreshes = 1000;
        for (int i = 0; i < refreshes; i++) total += PanelReads(city.View);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine("panel view reads: " + bytes + " bytes over " + refreshes + " refreshes of " + city.View.PlaceList.Count + " places, "
            + city.View.Food!.ScopeList.Count + " shops");
        Assert.Equal(expected * refreshes, total);
        Assert.Equal(0, bytes);
    }

    // ---- Golden pins ----

    /// <summary>F9e changes no wire: the writer-36 golden pins (E5b4, HeldPolicyExecutionTests) are unchanged with the view
    /// interfaces on the entity classes.</summary>
    [Theory]
    [InlineData(5150, "12447cdf8cff62a2926452fb51550054bbcf681d89d6c310738988878d7226e5", 73332)]
    [InlineData(9217, "590df8dcc45df126fe97385fedcc5fcbe8ced21be6d57747246e9e8f1e3fc89e", 72970)]
    public void CurrentWriterGoldenHashIsUnchanged(int seed, string pin, int characters)
    {
        Assert.Equal(36, CityState.CurrentSchema);
        string payload = SaveCodec.Write(ChangeJournalTests.Fixture(seed));
        Assert.Equal(pin + " " + characters, EconomicLedgerTests.Sha(payload) + " " + payload.Length);
    }
}
