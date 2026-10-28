using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FoodDemandAuditProbe;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

static class Program
{
    private static CityState City(int count, int seed = 11)
    {
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity
        {
            Id = "rlo:test:c" + i.ToString("D3"), Name = "Citizen " + i, Age = 20 + i % 50, District = i % 2 == 0 ? "a" : "b",
            Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var places = new[]
        {
            new Place { Id = "home-a", Name = "Home A", District = "a", Kind = PlaceKind.Home, Capacity = count, Position = new(0, 0, 0) },
            new Place { Id = "home-b", Name = "Home B", District = "b", Kind = PlaceKind.Home, Capacity = count, Position = new(900, 0, 0) },
            new Place { Id = "work-1", Name = "Work 1", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(300, 0, 400) },
            new Place { Id = "work-2", Name = "Work 2", District = "b", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(1500, 0, -200) },
            new Place { Id = "work-3", Name = "Work 3", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(-600, 0, 900) },
            new Place { Id = "school", Name = "School", District = "a", Kind = PlaceKind.Study, Capacity = count, Position = new(200, 0, -700) },
            new Place { Id = "shop-1", Name = "Shop 1", District = "a", Kind = PlaceKind.Shop, Position = new(150, 0, 120), OpenMinute = 480, CloseMinute = 1200 },
            new Place { Id = "shop-2", Name = "Shop 2", District = "b", Kind = PlaceKind.Shop, Position = new(1000, 0, 300), OpenMinute = 540, CloseMinute = 1260 },
            new Place { Id = "shop-3", Name = "Shop 3", District = "a", Kind = PlaceKind.Shop, Position = new(-400, 0, -300), OpenMinute = 600, CloseMinute = 1080 },
            new Place { Id = "leisure-1", Name = "Leisure 1", District = "a", Kind = PlaceKind.Leisure, Position = new(60, 0, 250), OpenMinute = 600, CloseMinute = 1380 },
            new Place { Id = "leisure-2", Name = "Leisure 2", District = "b", Kind = PlaceKind.Leisure, Position = new(1200, 0, 600), OpenMinute = 660, CloseMinute = 1320 },
            new Place { Id = "leisure-3", Name = "Leisure 3", District = "a", Kind = PlaceKind.Leisure, Position = new(-200, 0, 800), OpenMinute = 720, CloseMinute = 1440 }
        };
        var city = Population.Create(identities, places, "resumable-test", "0.40.4", seed, 1440);
        // Some workers start unemployed so the daily employment check probes workplaces during the run.
        for (int i = 0; i < city.Citizens.Count; i += 5) city.Citizens[i].DayPlaceId = "";
        return city;
    }


    static (string save, int observed, Probe probe) Run(bool measured, bool fail, string? observerFault = null)
    {
        var planner = new MultimodalRoutes();
        var sim = new CitySimulator(City(36), planner);
        int observed = 0;
        sim.ActivityObserved += (_, _, _) => observed++;
        var probe = new Probe();
        if (measured) probe.Start();
        bool injected = false, observerInjected = false;
        for (int frame = 0; !sim.IsCaughtUpTo(4320); frame++)
        {
            UnityEngine.Time.frameCount++;
            if (observerFault != null && !observerInjected)
            { probe.InjectObserverFault(observerFault); observerInjected = true; }
            if (sim.PlanningPending)
            {
                if (fail && !injected) { planner.FailNext = true; injected = true; }
                try { sim.ContinuePlanning(0); }
                catch (InvalidOperationException error) when (error.Message == "injected") { }
            }
            else sim.AdvanceFrameSlice(4320, 1);
        }
        // No pending job is a legitimate no-op and must not dereference a null PendingEvent.
        sim.ContinuePlanning(0);
        sim.SynchronizeSnapshot();
        if (measured) probe.Stop();
        return (SaveCodec.Write(sim.State), observed, probe);
    }
    static void Main()
    {
        foreach (bool fail in new[] { false, true })
        {
            var plain = Run(false, fail); var measured = Run(true, fail);
            if (plain.save != measured.save || plain.observed != measured.observed) throw new Exception("Diagnostics changed outputs");
            if (measured.probe.Samples < 100 || measured.probe.Plans == 0 || measured.probe.NavTicks == 0 || measured.probe.RoadTicks == 0
                || measured.probe.AStarTicks == 0 || measured.probe.NavMeshPathMilliseconds <= 0)
                throw new Exception("Missing measurement hooks");
            if (fail && measured.probe.Failures == 0) throw new Exception("Missing failure observation");
            Console.WriteLine($"fail={fail};events={measured.observed};save={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(measured.save)))}");
        }
        foreach (string seam in new[] { "event-prefix", "event-finalizer", "query-prefix", "query-finalizer" })
        foreach (bool fail in new[] { false, true })
        {
            var plain = Run(false, fail); var broken = Run(true, fail, seam);
            if (plain.save != broken.save || plain.observed != broken.observed
                || broken.probe.DiagnosticFailures != 1 || !broken.probe.EventCaptureDisabled)
                throw new Exception("Observer fault changed simulation or was not reported: " + seam);
            Console.WriteLine($"observerFault={seam};nativeFailure={fail};events={broken.observed};outputs=identical");
        }
        foreach (bool query in new[] { false, true })
        {
            var probe = new Probe(); probe.Start();
            probe.VerifyOriginalExceptionSurvivesObserverFault(query); probe.Stop();
        }
        Console.WriteLine("Both diagnostic finalizers preserve the exact original exception on observer failure.");
    }
}
