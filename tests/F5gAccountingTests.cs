using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class F5gAccountingTests
{
    internal sealed class AdmittedPlanner : IResumableRoutePlanner, ICommuteTimingPlanner
    {
        public const int PathsPerFrame = 16;
        private int used;
        public long Paths, Jobs, Deferred, FramesWithDeferral;
        public int MaxPathsInFrame, MaxFramesWaited;
        private bool deferredThisFrame;
        public void NewFrame()
        {
            MaxPathsInFrame = Math.Max(MaxPathsInFrame, used);
            if (deferredThisFrame) FramesWithDeferral++;
            used = 0; deferredThisFrame = false;
        }
        public IRoutePlanningJob Begin(RouteRequest request) { Jobs++; return new Job(this, request); }
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => Walk(from, to);
        private static Route Walk(Place from, Place to) => new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position,
            Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };
        private sealed class Job : IRoutePlanningJob
        {
            private readonly AdmittedPlanner owner; private readonly RouteRequest request;
            private int need, frames;
            public Job(AdmittedPlanner owner, RouteRequest request)
            { this.owner = owner; this.request = request; need = 1 + (int)(StableRandom.Hash(request.From.Id + ">" + request.To.Id, 5) % 2); }
            public Route? Result { get; private set; }
            public bool Continue(double budgetMilliseconds)
            {
                while (need > 0 && owner.used < PathsPerFrame) { owner.used++; owner.Paths++; need--; }
                if (need > 0) { owner.Deferred++; owner.deferredThisFrame = true; frames++; owner.MaxFramesWaited = Math.Max(owner.MaxFramesWaited, frames); return false; }
                Result = Walk(request.From, request.To);
                return true;
            }
            public void Dispose() { }
        }
    }

    internal sealed class ClockMarket : IFoodMarket, IFoodSaleEvidence
    {
        public double Clock;
        public FoodBusiness[] Shops = Array.Empty<FoodBusiness>();
        public readonly List<string> Orders = new();
        private readonly HashSet<string> paid = new(StringComparer.Ordinal);
        public double EarliestMinute => Math.Floor(Clock / 60) * 60;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Shops;
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            Assert.True(at >= EarliestMinute && at < EarliestMinute + 60, "historical native checkout at " + at + " with clock " + Clock);
            var offer = Shops.Single(b => b.Id == meal.BusinessId).Offers.Single(o => o.ProductId == meal.ProductId);
            if (offer.Price != meal.Price) return new FoodSale(false, "Price changed");
            Assert.True(paid.Add(meal.IntentId), "duplicate native order " + meal.IntentId);
            Orders.Add(meal.IntentId + "@" + at.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (Orders.Count % 29 == 0) throw new InvalidOperationException("Lost native acknowledgement");
            return new FoodSale(true, "Confirmed");
        }
        public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
    }

    internal sealed class Result
    {
        public string Snapshot = "";
        public string[] Orders = Array.Empty<string>();
        public long Frames, Held, HeldAtBoundary, FillFrames, Units, FillUnits, FillCreated, Skipped, PlanningRequests;
        public double MaxLag;
        public int Reloads, FillReloads, MaxPathsInFrame, MaxFramesWaited;
        public long Paths, Jobs, Deferred, FramesWithDeferral;
        public long HistoricalClockMisses;
        public double FrameP95, FrameMax; public int FramesAtLeast30Ms;
    }

    internal static Result Run(int homes, int fps, int eventBudget, double milliseconds, bool reload, bool skip, int seed = 4242, int since = 3)
    {
        var (state, _) = F5gPopulationFillTests.City(homes, 1, seed: seed);
        var market = new ClockMarket { Shops = F5gPopulationFillTests.Shops(state), Clock = state.Minute };
        var planner = new AdmittedPlanner();
        PopulationFillCatalogue Fill() => F5gPopulationFillTests.Catalogue(homes, F5gPopulationFillTests.Base, F5gPopulationFillTests.Since(since));
        CitySimulator Start(CityState s) { var c = new CitySimulator(s, planner); c.AttachFoodMarket(market); c.AttachPopulationFill(Fill()); return c; }
        var city = Start(state);
        var result = new Result();
        var costs = new List<double>();
        const double boundary = 2880, end = 3240;
        double native = state.Minute;
        bool midnightReload = false, fillReload = false;
        long creditedUnits = 0, creditedCreated = 0, creditedSkipped = 0, creditedRequests = 0;
        void Credit() { result.FillUnits += city.PopulationFillUnits - creditedUnits; result.FillCreated += city.PopulationFillCreated - creditedCreated;
            result.Skipped += city.PopulationFillSkipped - creditedSkipped; result.PlanningRequests += city.PlanningRequests - creditedRequests; }
        CitySimulator Reload()
        {
            Credit();
            string payload = SaveCodec.Write(city.State);
            var read = SaveCodec.Read(payload);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(payload, SaveCodec.Write(read.State!));
            result.Reloads++;
            var next = Start(read.State!);
            creditedUnits = 0; creditedCreated = 0; creditedSkipped = 0; creditedRequests = 0;
            return next;
        }
        while (!city.IsCaughtUpTo(end))
        {
            Assert.True(++result.Frames < 2_000_000, "nonterminating fill scenario");
            planner.NewFrame();
            long started = Stopwatch.GetTimestamp();
            Assert.True(AccountingClock.Compatible(city.State.Minute, native), "input epoch escaped");
            Assert.True(city.IsAccountingEpochDrained(native), "unfinished ending epoch escaped");
            double target = AccountingClock.Target(native);
            bool ready = city.IsCaughtUpTo(target);
            float requested = (float)Math.Min(skip ? 120 : 30.0 / fps, end - native);
            float allowed = AccountingClock.Limit(ready, native, requested);
            if (allowed == 0 && requested > 0)
            {
                result.Held++;
                if (city.PopulationFillPending) result.HeldAtBoundary++;
            }
            int nativeHour = (int)Math.Floor(native / 60); float nativeMinute = (float)(native - nativeHour * 60.0) + allowed;
            while (nativeMinute >= 60) { nativeMinute -= 60; nativeHour++; }
            native = nativeHour * 60.0 + nativeMinute;
            market.Clock = native;
            Assert.True(city.BeginAccountingObservations(native), "observation epoch not published");
            target = Math.Max(city.State.Minute, AccountingClock.Target(native));
            long saveTicks = 0;
            if (reload && !midnightReload && city.State.Minute == boundary)
            { long saveStarted = Stopwatch.GetTimestamp(); midnightReload = true; city = Reload(); saveTicks = Stopwatch.GetTimestamp() - saveStarted; }
            result.MaxLag = Math.Max(result.MaxLag, target - city.State.Minute);
            long unitsBefore = city.PopulationFillUnits;
            for (int unit = 0; unit < eventBudget; unit++)
            {
                if (city.PlanningPending) { if (!city.ContinuePlanning(milliseconds)) break; continue; }
                var advanced = city.AdvanceFrameSlice(target);
                result.Units += advanced.Events;
                if (advanced.CaughtUp || (Stopwatch.GetTimestamp() - started - saveTicks) * 1000.0 / Stopwatch.Frequency >= milliseconds) break;
            }
            city.SynchronizeSnapshot();
            if (city.PopulationFillUnits != unitsBefore) result.FillFrames++;
            costs.Add((Stopwatch.GetTimestamp() - started - saveTicks) * 1000.0 / Stopwatch.Frequency);
            if (reload && !fillReload && city.PopulationFillPending && city.PopulationFillUnits > 0)
            {
                fillReload = true; result.FillReloads++; city = Reload();
            }
        }
        Credit();
        result.HistoricalClockMisses = city.HistoricalClockMisses;
        result.Snapshot = SaveCodec.Write(city.State); result.Orders = market.Orders.ToArray();
        result.Paths = planner.Paths; result.Jobs = planner.Jobs; result.Deferred = planner.Deferred; result.FramesWithDeferral = planner.FramesWithDeferral;
        planner.NewFrame();
        result.MaxPathsInFrame = planner.MaxPathsInFrame; result.MaxFramesWaited = planner.MaxFramesWaited;
        F5gPopulationFillTests.Invariants(city.State, "accounting"); F5gPopulationFillTests.Conserved(city.State, "accounting");
        costs.Sort();
        result.FrameP95 = costs[(int)((costs.Count - 1) * .95)]; result.FrameMax = costs[^1]; result.FramesAtLeast30Ms = costs.Count(c => c >= 30);
        return result;
    }

    [Theory]
    [InlineData(30, 1, .01, false)] [InlineData(60, 16, .5, false)] [InlineData(120, 128, 2, false)] [InlineData(60, 1, .01, true)]
    public void MidnightFillHoldsTheClockAdmitsRoutesAndEqualsTheReferenceAcrossCadencesAndSaves(int fps, int budget, double milliseconds, bool skip)
    {
        var reference = Run(12, 60, 128, 2, reload: false, skip: false);
        var actual = Run(12, fps, budget, milliseconds, reload: true, skip: skip);
        Assert.True(reference.FillCreated > 0);
        Assert.Equal(reference.Snapshot, actual.Snapshot);
        Assert.Equal(reference.Orders, actual.Orders);
        Assert.True(actual.Orders.Length > 0);
        Assert.Equal(0, actual.HistoricalClockMisses); Assert.Equal(0, reference.HistoricalClockMisses);
        Assert.InRange(actual.MaxLag, 0, AccountingClock.MaximumLagMinutes);
        Assert.InRange(actual.MaxPathsInFrame, 1, AdmittedPlanner.PathsPerFrame);
        Assert.True(actual.Reloads >= 1);
        Assert.True(actual.Held > 0);
        Assert.Equal(reference.FillCreated, actual.FillCreated);
        if (budget == 1) Assert.True(actual.FillReloads > 0);
    }

    [Fact]
    public void Measure()
    {
        string? path = PerfGate.Variable("PC_F5G_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        foreach (var (name, homes, since) in new[] { ("accounting-small", 12, 3), ("accounting-reference", 152, 45) })
            foreach (var (fps, budget, ms, skip) in new[] { (30, 1, .01, false), (60, 16, .5, false), (120, 128, 2.0, false), (60, 1, .01, true) })
                foreach (bool reload in new[] { false, true })
                {
                    var r = Run(homes, fps, budget, ms, reload, skip, since: since);
                    File.AppendAllText(path, JsonConvert.SerializeObject(new { arm = name, fps, budget, ms, skip, reload, r.Frames, r.Held, r.HeldAtBoundary,
                        r.FillFrames, r.FillUnits, r.FillCreated, r.Skipped, r.MaxLag, r.HistoricalClockMisses, r.Reloads, r.FillReloads, r.Jobs, r.Paths,
                        r.Deferred, r.FramesWithDeferral, r.MaxPathsInFrame, r.MaxFramesWaited, r.FrameP95, r.FrameMax, r.FramesAtLeast30Ms,
                        orders = r.Orders.Length, hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(r.Snapshot + string.Join(",", r.Orders))))[..16] }) + Environment.NewLine);
                }
    }
}
