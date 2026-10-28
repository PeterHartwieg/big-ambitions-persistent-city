using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;

static class Program
{
    static readonly StringBuilder evidence = new();
    static int observations;
    static void Record(object value) { evidence.AppendLine(JsonConvert.SerializeObject(value)); observations++; }
    static List<TrafficRouteLeg> Route(int index)
    {
        var ids = index % 3 == 0 ? new[] { 2 } : index % 3 == 1 ? new[] { 2, 19 } : new[] { 37, 19 };
        return ids.Select((id, i) => new TrafficRouteLeg { LinkId = id, StartNominalMinutes = i,
            EndNominalMinutes = i + 1, FreeFlowMinutes = 1,
            EndLinkFraction = ids.Length == 1 && index % 2 == 0 ? .4 : 1 }).ToList();
    }
    static string State(TrafficKernel kernel) => JsonConvert.SerializeObject(kernel.Capture());
    static void Main()
    {
        foreach (int budget in new[] { 1, 17, 10000 })
        {
            var kernel = new TrafficKernel(new[] { 2, 19, 37 }.Select(id => new TrafficLinkDefinition
                { Id = id, Capacity = 16, FreeFlowMinutes = 1, HeadwayMinutes = .125 }));
            void Subscribe() => kernel.VehicleCompleted += (id, at) => Record(new { completed = id, at });
            Subscribe();
            var ids = Enumerable.Range(0, 60).Select(i => "車:" + i).ToArray();
            for (int i = 0; i < ids.Length; i++) kernel.Enqueue(ids[i], Route(i));
            kernel.SetPhysicalControl(ids[1], true);
            for (int tick = 0; tick < 160; tick++)
            {
                double target = tick / 4.0;
                while (!kernel.AdvanceTo(target, budget).ReachedTarget) { }
                if (tick == 4) kernel.SetLinkClosed(19, true);
                if (tick == 20) kernel.SetLinkClosed(19, false);
                if (tick == 9)
                {
                    Record(kernel.Cancel(ids[12]));
                    kernel.Enqueue(ids[12], Route(12)); // Same external identity, newly owned queue object.
                }
                foreach (string id in ids)
                {
                    if (!kernel.TryGetVehicle(id, out var snapshot)) continue;
                    Record(snapshot);
                    if (snapshot.PhysicalControl && !snapshot.IsCompleted)
                        Record(kernel.ReportPhysicalProgress(id, snapshot.MaximumNominalMinutes));
                }
                if (tick == 24) kernel.SetPhysicalControl(ids[1], false);
                string before = State(kernel);
                foreach (int link in new[] { 2, 19, 37 })
                {
                    Record(kernel.Forecast(link, kernel.Clock));
                    Record(kernel.ForecastEntry(link, kernel.Clock, .3, .7));
                }
                if (before != State(kernel)) throw new Exception("Forecast mutated live state");
                if (tick % 7 == 0)
                {
                    Record(before);
                    kernel = TrafficKernel.Restore(kernel.Capture(), releasePhysicalControl: false);
                    if (before != State(kernel)) throw new Exception("Restore changed exact snapshot");
                    Subscribe();
                }
            }
            Record(State(kernel));
        }
        // A long queue exercises every predecessor and the curb-exit node reuse.
        var longQueue = new TrafficKernel(new[] { new TrafficLinkDefinition { Id = 9, Capacity = 512, FreeFlowMinutes = 10 } });
        for (int i = 0; i < 400; i++) longQueue.Enqueue("q" + i, new[] { new TrafficRouteLeg
            { LinkId = 9, StartNominalMinutes = 0, EndNominalMinutes = 10, EndLinkFraction = i % 3 == 0 ? .8 : 1 } });
        longQueue.AdvanceTo(0, 10000); longQueue.SetPhysicalControl("q0", true); longQueue.AdvanceTo(5, 10000);
        for (int i = 0; i < 400; i++) Record(longQueue.GetPermission("q" + i));
        Record(State(longQueue));
        Console.WriteLine($"{observations} observations; SHA256={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence.ToString())))}");
    }
}
