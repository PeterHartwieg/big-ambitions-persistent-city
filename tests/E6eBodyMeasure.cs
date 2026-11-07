using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

// Opt-in offline timing of the real selection/dwell hook with native scene/pool doubles.
[Collection(StreetFoodHooks.Collection)]
public sealed class E6eBodyMeasure
{
    private readonly ITestOutputHelper output;
    public E6eBodyMeasure(ITestOutputHelper output) { this.output = output; }
    private void Report(string line)
    {
        output.WriteLine(line);
        if (Environment.GetEnvironmentVariable("PC_E6E_MEASURE") is string path) File.AppendAllText(path, line + Environment.NewLine);
    }
    private void Measure(string label, Action action, int count = 1000)
    {
        for (int i = 0; i < 50; i++) action();
        var times = new List<double>(count); long bytes = 0;
        for (int i = 0; i < count; i++)
        {
            long allocation = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            action();
            times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            bytes += GC.GetAllocatedBytesForCurrentThread() - allocation;
        }
        times.Sort();
        Report(string.Format(CultureInfo.InvariantCulture,
            "{0}: n={1}, p50={2:F6} ms, p95={3:F6} ms, max={4:F6} ms, bytes/op={5:F1}, spikes>=30ms={6}",
            label, count, times[count / 2], times[(int)Math.Ceiling(count * .95) - 1], times[^1], bytes / (double)count, times.Count(t => t >= 30)));
        foreach (double spike in times.Where(t => t >= 30)) Report($"FLAG {label}: {spike:F6} ms");
        Assert.True(times[(int)Math.Ceiling(count * .95) - 1] < 5, label + " offline p95 exceeded 5 ms");
    }
    [Fact]
    public void QueueSamplingCachedDwellAndBodySelection()
    {
        if (Environment.GetEnvironmentVariable("PC_E6E_MEASURE") == null) return;
        using var f = new VendorBodyTests.Fixture(people: 8, cap: 8);
        var vehicle = f.Vendors.Market.Coordinator.Image!.Find("v00")!;
        Measure("cold eight-spot queue sampling", () => { Time.Next(); new DwellSpots().TryVendorQueue(f.Row, vehicle, out _); });
        var cache = new DwellSpots(); Time.Next(); Assert.True(cache.TryVendorQueue(f.Row, vehicle, out _));
        int samples = NavMesh.Samples;
        Measure("cached vendor queue lookup", () => cache.TryVendorQueue(f.Row, vehicle, out _));
        Assert.Equal(samples, NavMesh.Samples);
        Measure("eight-body selection and release (pool double)", () => {
            f.Bodies.Suspend(); Time.Next(3); f.Bodies.Update(); Time.Next(); f.Bodies.Update();
        });
        f.ReachTargets();
        Measure("eight-body dwell update including selection cadence", () => { Time.Next(); f.Bodies.Update(); });
        foreach (var meal in f.City.State.Food!.Residents) meal.Status = PersistentCity.Core.MealStatus.Paid;
        f.Tick(); f.ReachTargets();
        Assert.Equal(8, f.Pool.Active.Count);
        Measure("eight completed-meal bodies awaiting departure", () => { Time.Next(); f.Bodies.Update(); });
        Report("Whole-frame and native Instantiate/appearance/NavMeshAgent costs: unmeasured offline; batched rig required.");
        Report("No autosave or save in these headless windows. Four parked hotspots unchanged.");
    }
}
