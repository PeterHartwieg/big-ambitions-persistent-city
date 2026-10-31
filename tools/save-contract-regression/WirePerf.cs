using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Persistence;

static partial class Program
{
    private static object WirePerf(string previousPath, int residents)
    {
        previousPath = System.IO.Path.GetFullPath(previousPath);
        var context = new BaselineContext(previousPath); var assembly = context.LoadFromAssemblyPath(previousPath);
        var program = assembly.GetType("Program")!; var codec = assembly.GetType("PersistentCity.Persistence.SaveCodec")!;
        var create = program.GetMethod("FoodCity", BindingFlags.Static | BindingFlags.NonPublic)!;
        object previous = create.Invoke(null, new object[] { residents, true, 2 })!;
        program.GetMethod("PopulateDecisions", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { previous });
        var current = FoodCity(residents, true); PopulateDecisions(current);
        string oldJson = (string)codec.GetMethod("Write")!.Invoke(null, new[] { previous })!;
        string newJson = SaveCodec.Write(current);
        if (oldJson != newJson) throw new Exception("Wire optimization changed bytes.");
        Action[] work = {
            () => codec.GetMethod("Write")!.Invoke(null, new[] {previous}),
            () => SaveCodec.Write(current),
            () => codec.GetMethod("Read")!.Invoke(null, new object[] {oldJson}),
            () => SaveCodec.Read(newJson)
        };
        var times = work.Select(_ => new List<double>()).ToArray(); var allocations = work.Select(_ => new List<long>()).ToArray();
        for (int round = -2; round < 7; round++)
            for (int j = 0; j < work.Length; j++)
            {
                int i = (round & 1) == 0 ? j : work.Length - 1 - j;
                long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); work[i]();
                double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (round >= 0) { times[i].Add(ms); allocations[i].Add(GC.GetAllocatedBytesForCurrentThread() - before); }
            }
        // Compare the exact former shape path (Decode) to the new nonmaterializing shape precheck.
        const string wire = "100003440000000ZG10010000ZG10020000ZG1FFJ0000ZG1";
        var oldDecode = (Func<string, object>)Delegate.CreateDelegate(typeof(Func<string, object>), assembly.GetType("PersistentCity.Core.MemoryWire")!.GetMethod("Decode")!);
        var currentCheck = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), typeof(SaveCodec).Assembly.GetType("PersistentCity.Core.MemoryWire")!.GetMethod("CheckWire")!);
        object Micro(Action action)
        {
            for (int i = 0; i < 10000; i++) action();
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 500000; i++) action();
            return new { ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds / 500000, bytesPerCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 500000.0 };
        }
        var outcomeType = assembly.GetType("PersistentCity.Core.MealOutcomeRecord")!;
        var outcomeParameter = Expression.Parameter(typeof(string)); var outcomeResult = Expression.Variable(outcomeType);
        var oldShape = Expression.Lambda<Func<string, bool>>(Expression.Block(new[] { outcomeResult },
            Expression.Call(outcomeType.GetMethod("TryParse")!, outcomeParameter, outcomeResult)), outcomeParameter).Compile();
        var newShape = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), typeof(SaveCodec).Assembly.GetType("PersistentCity.Core.MealOutcomeRecord")!.GetMethod("IsCanonicalWire")!);
        const string outcomeWire = "1|0|1|0|0|0|487|487|pastry|address:ba%3Astreet_thirdstreet:128:shop";
        object CountMicro(Type type)
        {
            using var counter = (System.IO.TextWriter)Activator.CreateInstance(type, true)!;
            string block = new string('a', 65536); long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 4096; i++) counter.Write(block);
            return new { ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds, chars = type.GetProperty("Count")!.GetValue(counter), bytes = type.GetProperty("Bytes")!.GetValue(counter) };
        }
        return new { residents, chars = newJson.Length, unchangedBytes = true, samples = 7,
            saves = Enumerable.Range(0, 4).Select(i => new { mode = new[] { "beforeWrite", "afterWrite", "beforeRead", "afterRead" }[i],
                medianMs = times[i].OrderBy(t => t).ElementAt(3), maxMs = times[i].Max(), allocationBytes = allocations[i].OrderBy(t => t).ElementAt(3) }),
            outcomeShapeBefore = Micro(() => { if (!oldShape(outcomeWire)) throw new Exception("Bad former shape"); }),
            outcomeShapeAfter = Micro(() => { if (!newShape(outcomeWire)) throw new Exception("Bad current shape"); }),
            countBefore = CountMicro(codec.GetNestedType("CharacterCountingWriter", BindingFlags.NonPublic)!),
            countAfter = CountMicro(typeof(SaveCodec).GetNestedType("CharacterCountingWriter", BindingFlags.NonPublic)!),
            memoryShapeBefore = Micro(() => GC.KeepAlive(oldDecode(wire))), memoryShapeAfter = Micro(() => currentCheck(wire)) };
    }
}
