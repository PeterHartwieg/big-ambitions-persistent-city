using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;

internal static class Program
{
    private static void Main()
    {
        NativeClockContract.Run();
        AccountingStatusBenchmark.Run();
        FrameCapComparison.Run();
        ClockBaseline.Run();
        // Warm both policies before measuring. Timers include every detached frame operation;
        // save/reload is deliberately outside measured gameplay windows, as on the rig.
        AccountingScenario.Run(16, 60, 16, 2);
        var reference = AccountingScenario.Run(957, 60, 128, 2);
        var reloaded = AccountingScenario.Run(957, 30, 1, .01, reload: true);
        if (reference.Snapshot != reloaded.Snapshot || !reference.Orders.SequenceEqual(reloaded.Orders) || reloaded.Reloads != 2)
            throw new Exception("957-resident held/midnight reload diverged");
        Console.WriteLine("PASS 957-resident held/travelling and midnight reload; two writer-25 exact round trips (outside timing arms)");
        foreach (var arm in new[] { (30, 1, .01, false), (60, 16, .5, false), (120, 128, 2.0, false), (30, 1, .01, true) })
        {
            var result = AccountingScenario.Run(957, arm.Item1, arm.Item2, arm.Item3, stallFrames: arm.Item1 * 2, skip: arm.Item4);
            if (reference.Snapshot != result.Snapshot || !reference.Orders.SequenceEqual(result.Orders)) throw new Exception("957-resident economic replay diverged");
            Console.WriteLine(JsonSerializer.Serialize(new { residents = 957, consumers = 1914, fps = arm.Item1, units = arm.Item2,
                milliseconds = arm.Item3, skip = arm.Item4, result.Frames, result.Units, result.Held, result.MaxLag,
                result.RecoverySeconds, result.Reloads, result.Purchases, result.SpentUnits, result.MissedMeals, result.BalanceUnits, result.HeldUnits,
                nativeOrders = result.Orders.Length,
                result.FrameP50, result.FrameP95, result.FrameMax, result.Spikes, result.Seconds,
                unitsPerCpuSecond = result.Units / result.Seconds }));
        }
        var stress = AccountingScenario.Run(2000, 30, 1, .01, stallFrames: 300);
        Console.WriteLine(JsonSerializer.Serialize(new { stressResidents = 2000, stress.Frames, stress.Units, stress.Held, stress.MaxLag,
            stress.RecoverySeconds, stress.FrameP50, stress.FrameP95, stress.FrameMax, stress.Spikes, stress.Seconds }));
        // Cheap taken win: float predecessor without BitConverter.GetBytes' array allocation.
        for (int i = 0; i < 10000; i++) AccountingClock.Limit(true, 59, 1);
        long before = GC.GetAllocatedBytesForCurrentThread(); long started = Stopwatch.GetTimestamp(); double sink = 0;
        for (int i = 0; i < 100000; i++) sink += AccountingClock.Limit(true, i % 60, 1);
        double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine(JsonSerializer.Serialize(new { policyCalls = 100000, ms, bytes, sink }));
    }
}
