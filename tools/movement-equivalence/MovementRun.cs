using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.MovementEquivalence;

/// <summary>Where the player stands. None: no finite focus, so the driver holds no bodies.</summary>
public enum FocusPath { None, Stationary, Sweep, AwayReturn }

/// <summary>Injected harness faults; each must fail the run loudly.</summary>
public enum Fault { None, DuplicateArrival, SkippedEvent }

public sealed class MovementOptions
{
    public string Arm = "bodies-off";
    public int Seed;
    public int Residents = MovementFixture.Residents;
    public int Days = MovementRun.Days;
    /// <summary>False: plain simulation without any presentation driver.</summary>
    public bool Driver = true;
    public FocusPath Focus = FocusPath.Stationary;
    public DriverSettings Bodies = new();
    public int FramesPerSecond = 60;
    /// <summary>Save and reload into a new simulator (and a new driver) mid-journey on the first day.</summary>
    public bool ReloadMidJourney;
    /// <summary>A hidden-travel window: bodies are released and native time runs at 10x.</summary>
    public bool HiddenTravel;
    /// <summary>F2c: nobody owns a car, so every journey walks. Walker follow makes these arms equal bodies-off.</summary>
    public bool WalkingOnly;
    public Fault Fault;
    /// <summary>Optional directory for the arm's journey records and ordered sales (one text file), for diffing arms.</summary>
    public string? DumpDirectory;
}

public sealed class Boundary
{
    public double Minute;
    public string SaveSha256 = "", SalesSha256 = "", JourneysSha256 = "", ParkedSha256 = "", WalletsSha256 = "", FifoSha256 = "";
    public int Sales, Arrivals, MissedActivities;
    /// <summary>Skipped meal outcomes of the day that ended before this 03:00 boundary.</summary>
    public int SkippedMeals;
}

public sealed class MovementResult
{
    public string Arm = "";
    public List<Boundary> Boundaries = new();
    public string FinalSaveSha256 = "", SalesSha256 = "", JourneysSha256 = "", FifoSha256 = "";
    public SortedDictionary<string, double> Metrics = new(StringComparer.Ordinal);
    public long Frames, Events, FifoSamples, QueuedFifoSamples;
    public int Reloads, InFlightAtReload, BodiesAtReload;
    public double Seconds, SimulatedDays;
    public long AllocatedBytes;
}

/// <summary>Deterministic frame-driven run. Each frame: native clock step under the F1 accounting fence,
/// publish the epoch, native body movement, budgeted simulation slices, snapshot publication, then the
/// presentation driver's Update (Runtime order: AdvancePopulation, then PhysicalPopulation.Update).
/// The saved city is hashed at every 03:00 boundary at the moment the native clock enters it: every
/// earlier event drained, none at or after it run, bodies observed for save first.</summary>
public static class MovementRun
{
    public const string Schema = "persistentcity-f2a-movement-1";
    public const int Days = 2;
    public const int SampleMinute = 180;
    public const double GameMinutesPerRealSecond = GridPlanner.ClockRate;
    public const double HiddenTravelSpeedup = 10;
    public const double HiddenFrom = 12 * 60, HiddenTo = 12 * 60 + 30;
    public const double AwayFrom = 9 * 60, AwayTo = 15 * 60;
    public const double SweepDwellMinutes = 10;
    public const double FifoSampleMinutes = 5;
    public const int EventUnitsPerFrame = 4096;
    public static readonly Position Away = new Position(1300, 0, 1300);
    public static readonly Position[] SweepPoints = {
        MovementFixture.Door(2, 2), MovementFixture.Door(12, 2), MovementFixture.Door(22, 2),
        MovementFixture.Door(22, 22), MovementFixture.Door(12, 22), MovementFixture.Door(2, 22) };

    public static Position Focus(FocusPath path, double minute)
    {
        double day = minute % 1440;
        switch (path)
        {
            case FocusPath.None: return new Position(double.NaN, 0, 0);
            case FocusPath.Sweep: return SweepPoints[(int)(Math.Floor(minute / SweepDwellMinutes) % SweepPoints.Length)];
            case FocusPath.AwayReturn: return day >= AwayFrom && day < AwayTo ? Away : MovementFixture.ShopPosition;
            default: return MovementFixture.ShopPosition;
        }
    }

    private static void Require(bool value, string message) { if (!value) throw new HarnessViolation(message); }
    public static string Sha(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>Per-journey ledger from ActivityObserved: commit minute, mode and arrival.</summary>
    private sealed class JourneyLedger
    {
        private sealed class Open { public Route Route = null!; public double Start; public int Completed; public string Mode = ""; public string Destination = ""; public Activity Activity; }
        private readonly Dictionary<string, Open> open = new(StringComparer.Ordinal);
        public readonly SortedDictionary<string, List<double>> Durations = new(StringComparer.Ordinal);
        public readonly StringBuilder Records = new();
        public int Arrivals, Replaced, ShopArrivals, AdmittedAtShop, WorkArrivals;
        public void Observe(string id, CitizenState c, double minute)
        {
            open.TryGetValue(id, out var o);
            if (c.Phase == CitizenPhase.Travelling && c.Journey != null)
            {
                if (o != null && ReferenceEquals(o.Route, c.Journey)) return;
                // One event can arrive and send the person straight on (for example a closed door).
                if (o != null && c.CompletedJourneys > o.Completed) Close(id, o, c, minute);
                else if (o != null) { Replaced++; open.Remove(id); }
                bool car = false;
                foreach (var leg in c.Journey.Legs) car |= leg.Mode == TravelMode.Car;
                open[id] = new Open { Route = c.Journey, Start = minute, Completed = c.CompletedJourneys, Mode = car ? "car" : "walk", Destination = c.DestinationId, Activity = c.DestinationActivity };
                return;
            }
            if (o != null) Close(id, o, c, minute);
        }
        private void Close(string id, Open o, CitizenState c, double minute)
        {
            open.Remove(id);
            Arrivals++;
            double duration = minute - o.Start;
            if (!Durations.TryGetValue(o.Mode, out var list)) Durations[o.Mode] = list = new List<double>();
            list.Add(duration);
            if (o.Destination == MovementFixture.ShopPlace)
            {
                ShopArrivals++;
                if (c.PlaceId == MovementFixture.ShopPlace && c.Phase == CitizenPhase.AtPlace) AdmittedAtShop++;
            }
            if (o.Activity == Activity.Work) WorkArrivals++;
            Records.Append(id).Append('@').Append(o.Start.ToString("R", CultureInfo.InvariantCulture)).Append('>')
                .Append(minute.ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(c.PlaceId).Append('\n');
        }
        /// <summary>After a reload every in-flight journey is a new object with the same saved route.</summary>
        public void Rebind(CityState state)
        {
            foreach (var c in state.Citizens)
                if (open.TryGetValue(c.Identity.Id, out var o))
                {
                    Require(c.Phase == CitizenPhase.Travelling && c.Journey != null, "reload lost an in-flight journey of " + c.Identity.Id);
                    o.Route = c.Journey!;
                }
        }
        public int InFlight => open.Count;
    }

    private sealed class FifoDigest
    {
        private readonly StringBuilder text = new();
        public long Samples, Queued;
        public void Sample(CitySimulator city, double minute)
        {
            if (city.Traffic == null) return;
            var capture = city.Traffic.Capture();
            Samples++;
            text.Append(minute.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            foreach (var link in capture.Links)
            {
                if (link.Vehicles.Count == 0 && link.WaitingSources.Count == 0) continue;
                if (link.Vehicles.Count > 1) Queued++;
                text.Append(link.LinkId.ToString(CultureInfo.InvariantCulture)).Append(':').Append(string.Join(",", link.Vehicles))
                    .Append('/').Append(string.Join(",", link.WaitingSources)).Append(';');
            }
            text.Append('\n');
        }
        public string Sha => MovementRun.Sha(text.ToString());
    }

    public static MovementResult Run(MovementOptions options)
    {
        long allocated = GC.GetTotalAllocatedBytes(true);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var planner = new GridPlanner();
        var market = new MovementMarket();
        var state = MovementFixture.Build(options.Seed, options.Residents, carOwners: !options.WalkingOnly);
        var city = MovementFixture.Attach(state, market, planner, fresh: true);
        var ledger = new JourneyLedger();
        city.ActivityObserved += ledger.Observe;
        var settings = options.Bodies;
        settings.Seed = options.Seed;
        PresentationDriver? driver = options.Driver ? new PresentationDriver(city, settings) : null;
        var drivers = new List<PresentationDriver>();
        if (driver != null) drivers.Add(driver);
        var fifo = new FifoDigest();
        var result = new MovementResult { Arm = options.Arm, SimulatedDays = options.Days };
        int initialCompleted = state.Citizens.Sum(c => c.CompletedJourneys);

        double start = MovementFixture.StartMinute;
        double end = start + options.Days * 1440.0 + SampleMinute;
        var boundaries = new Queue<double>(Enumerable.Range(0, options.Days + 1).Select(d => start + d * 1440.0 + SampleMinute));
        int hour = (int)(start / 60); float minute = 0;
        double native = start;
        market.Clock = native;
        Require(city.BeginAccountingObservations(native), "initial epoch not published");
        double nextFifo = start + FifoSampleMinutes;
        bool reloaded = false, faultInjected = false, hiddenChecked = false;
        long priorReconsiders = 0, priorTrafficEvents = 0;
        double frameSeconds = 1.0 / options.FramesPerSecond;
        while (boundaries.Count > 0)
        {
            Require(++result.Frames < 50_000_000, "nonterminating run");
            Require(AccountingClock.Compatible(city.State.Minute, native), "input epoch escaped");
            Require(city.IsAccountingEpochDrained(native), "unfinished ending epoch escaped");
            bool hidden = options.HiddenTravel && native % 1440 >= HiddenFrom && native % 1440 < HiddenTo;
            double target = AccountingClock.Target(native);
            bool ready = city.IsCaughtUpTo(target);
            float requested = (float)(frameSeconds * GameMinutesPerRealSecond * (hidden ? HiddenTravelSpeedup : 1));
            float allowed = AccountingClock.Limit(ready, native, requested);
            if (options.Fault == Fault.SkippedEvent && !faultInjected && !ready && native >= start + 8 * 60)
            {
                // The native clock moves on as if this frame's due events had run.
                faultInjected = true; allowed = (float)(Math.Floor(native) + 1.25 - native);
            }
            minute += allowed;
            while (minute >= 60) { minute -= 60; hour++; }
            double previous = native;
            native = market.Clock = hour * 60.0 + minute;
            Require(city.BeginAccountingObservations(native), "observation epoch not published");
            // Link order is sampled where the native clock enters each five-minute epoch, so the sample
            // minute does not depend on frame cadence.
            if (native >= nextFifo)
            {
                Require(city.State.Minute == nextFifo, "FIFO sample is not at its epoch minute");
                fifo.Sample(city, nextFifo);
                nextFifo += FifoSampleMinutes;
            }
            if (native >= boundaries.Peek() && previous < boundaries.Peek())
            {
                double at = boundaries.Dequeue();
                driver?.BeforeSave();
                result.Boundaries.Add(Sample(city, market, ledger, fifo, at));
                Require(city.State.Citizens.Sum(c => c.CompletedJourneys) - initialCompleted == ledger.Arrivals,
                    "journey ledger: completed journeys differ from observed arrivals (" + (city.State.Citizens.Sum(c => c.CompletedJourneys) - initialCompleted) + " vs " + ledger.Arrivals + ", replaced " + ledger.Replaced + ")");
                if (boundaries.Count == 0) break;
            }
            if (hidden && driver != null)
            {
                // Runtime BeforeAdvance: hidden travel observes and releases bodies before AdvancePopulation.
                driver.SuspendBeforeHiddenAdvance();
                Require(driver.Count == 0, "hidden travel advanced with bodies bound");
                if (!hiddenChecked && city.Traffic != null)
                {
                    hiddenChecked = true;
                    Require(city.Traffic.Capture().Vehicles.All(v => !v.PhysicalControl), "hidden travel advanced with physical traffic control");
                }
            }
            else hiddenChecked = false;
            driver?.MoveBodies(frameSeconds, frameSeconds * GameMinutesPerRealSecond);
            target = Math.Max(city.State.Minute, AccountingClock.Target(native));
            // Since F2d no body adds kernel work, so a frame's events always drain and no frame starts behind. The
            // skipped-event fault first starves one frame inside the current minute epoch, so the next frame starts
            // with due work, and then skips it.
            bool starve = options.Fault == Fault.SkippedEvent && !faultInjected && native >= start + 8 * 60
                && Math.Floor(city.State.Minute) == Math.Floor(native);
            for (int unit = 0; unit < (starve ? 0 : EventUnitsPerFrame); unit++)
            {
                var advanced = city.AdvanceFrameSlice(target, 1);
                result.Events += advanced.Events;
                if (advanced.CaughtUp) break;
            }
            city.SynchronizeSnapshot();
            driver?.Update(Focus(options.Focus, native), hidden);
            // F2d: no traffic leg is ever under physical control, whatever the bodies do.
            Require(city.Traffic == null || !city.Traffic.HasPhysicalControl, "a traffic vehicle is under physical control");

            if (options.Fault == Fault.DuplicateArrival && !faultInjected && ledger.Arrivals > 0 && native >= start + 8 * 60)
            {
                // A second arrival booked for an already arrived journey.
                faultInjected = true;
                city.State.Citizens.First(c => c.CompletedJourneys > 0).CompletedJourneys++;
            }
            if (options.ReloadMidJourney && !reloaded && native >= start + 7 * 60 + 30 && city.IsCaughtUpTo(target)
                && (driver == null ? city.State.Citizens.Count(c => c.Phase == CitizenPhase.Travelling) >= 3 && (city.Traffic?.VehicleCount ?? 0) >= 1
                    : driver.Walkers >= 1 && (driver.Cars >= 1 || options.WalkingOnly)))
            {
                reloaded = true;
                result.BodiesAtReload = driver?.Count ?? 0;
                driver?.BeforeSave();
                string json = SaveCodec.Write(city.State);
                var read = SaveCodec.Read(json);
                Require(read.Status == SaveReadStatus.Loaded, "mid-journey reload refused: " + read.Error);
                Require(SaveCodec.Write(read.State!) == json, "mid-journey save round trip changed bytes");
                city.ActivityObserved -= ledger.Observe;
                priorReconsiders += city.Traffic?.Reconsiders ?? 0; priorTrafficEvents += city.Traffic?.ProcessedEvents ?? 0;
                city = MovementFixture.Attach(read.State!, market, planner, fresh: false);
                city.ActivityObserved += ledger.Observe;
                ledger.Rebind(city.State);
                result.InFlightAtReload = ledger.InFlight;
                // Runtime disposes the physical population on load; the new one starts without bodies.
                if (driver != null) { driver = new PresentationDriver(city, settings); drivers.Add(driver); }
                result.Reloads++;
            }
        }
        Require(options.Fault == Fault.None || faultInjected, "fault was never injected");
        Require(city.HistoricalClockMisses == 0, "unreported workload-caused lost demand");
        result.FinalSaveSha256 = result.Boundaries[result.Boundaries.Count - 1].SaveSha256;
        result.SalesSha256 = Sha(string.Join("\n", market.Sales));
        result.JourneysSha256 = Sha(ledger.Records.ToString());
        if (options.DumpDirectory != null)
            System.IO.File.WriteAllText(System.IO.Path.Combine(options.DumpDirectory, options.Arm.Replace("/", "_") + ".txt"),
                ledger.Records + "--sales--\n" + string.Join("\n", market.Sales) + "\n");
        result.FifoSha256 = fifo.Sha; result.FifoSamples = fifo.Samples; result.QueuedFifoSamples = fifo.Queued;
        Summarize(result, city, market, ledger, drivers);
        // F2d perf: kernel work over the whole run (every kernel, across a reload).
        result.Metrics["traffic.reconsiders"] = priorReconsiders + (city.Traffic?.Reconsiders ?? 0);
        result.Metrics["traffic.events"] = priorTrafficEvents + (city.Traffic?.ProcessedEvents ?? 0);
        result.Metrics["run.frames"] = result.Frames;
        result.Seconds = timer.Elapsed.TotalSeconds;
        result.AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
        return result;
    }

    private static Boundary Sample(CitySimulator city, MovementMarket market, JourneyLedger ledger, FifoDigest fifo, double at)
    {
        var state = city.State;
        Require(state.Minute == at, "boundary sample is not at the drained boundary minute");
        var parked = new StringBuilder(); var wallets = new StringBuilder();
        foreach (var c in state.Citizens)
            if (c.Car != null) parked.Append(c.Identity.Id).Append(':').Append(c.Car.ParkedPosition.X.ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(c.Car.ParkedPosition.Z.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        // The daily outcome ledger keeps one day; read the finished day here, before the next day's meals replace it.
        int day = (int)Math.Floor(at / 1440) - 1, skipped = 0;
        foreach (var m in state.Food!.Residents)
        {
            wallets.Append(m.ResidentId).Append(':').Append(m.Budget?.Balance.Units ?? 0).Append('\n');
            if (m.Today == null) continue;
            Require(m.Today.Day <= day, "a next-day meal outcome exists at the 03:00 boundary");
            if (m.Today.Day == day) skipped += m.Today.Meals.Count(o => o.Status == MealOutcomeStatus.Skipped);
        }
        return new Boundary { Minute = at, SaveSha256 = Sha(SaveCodec.Write(state)), SalesSha256 = Sha(string.Join("\n", market.Sales)),
            JourneysSha256 = Sha(ledger.Records.ToString()), ParkedSha256 = Sha(parked.ToString()), WalletsSha256 = Sha(wallets.ToString()),
            FifoSha256 = fifo.Sha, Sales = market.Sales.Count, Arrivals = ledger.Arrivals,
            MissedActivities = state.Citizens.Sum(c => c.MissedActivities), SkippedMeals = skipped };
    }

    private static double Percentile(List<double> sorted, double p) => sorted.Count == 0 ? 0 : sorted[(int)Math.Round((sorted.Count - 1) * p)];

    private static void Summarize(MovementResult result, CitySimulator city, MovementMarket market, JourneyLedger ledger, List<PresentationDriver> drivers)
    {
        var m = result.Metrics; var state = city.State;
        foreach (var (mode, list) in ledger.Durations)
        {
            var sorted = list.OrderBy(v => v).ToList();
            m["journeys." + mode + ".count"] = sorted.Count;
            m["journeys." + mode + ".meanMinutes"] = sorted.Count == 0 ? 0 : sorted.Average();
            m["journeys." + mode + ".p50Minutes"] = Percentile(sorted, .5);
            m["journeys." + mode + ".p95Minutes"] = Percentile(sorted, .95);
            m["journeys." + mode + ".maxMinutes"] = sorted.Count == 0 ? 0 : sorted[sorted.Count - 1];
        }
        m["journeys.arrivals"] = ledger.Arrivals;
        m["journeys.replaced"] = ledger.Replaced;
        m["visits.shopArrivals"] = ledger.ShopArrivals;
        m["visits.admittedAtShop"] = ledger.AdmittedAtShop;
        m["visits.workArrivals"] = ledger.WorkArrivals;
        m["routines.missedActivities"] = state.Citizens.Sum(c => c.MissedActivities);
        var food = state.Food!.Residents;
        m["meals.bought"] = food.Sum(r => r.Purchases);
        m["meals.ownSales"] = market.Sales.Count;
        m["meals.refusedAtCheckout"] = market.Refusals;
        m["meals.skipped"] = result.Boundaries.Sum(b => b.SkippedMeals);
        m["revenue.ownUnits"] = market.RevenueUnits;
        m["wallets.balanceUnits"] = food.Sum(r => r.Budget?.Balance.Units ?? 0);
        m["traffic.fifoSamples"] = result.FifoSamples;
        m["traffic.queuedLinkSamples"] = result.QueuedFifoSamples;
        m["cars.parkedAway"] = state.Citizens.Count(c => c.Car != null && Position.Distance(c.Car.ParkedPosition,
            GridPlanner.Node(GridPlanner.Nearest(state.Places.First(p => p.Id == c.HomeId).Position).X, GridPlanner.Nearest(state.Places.First(p => p.Id == c.HomeId).Position).Z)) > 1e-9);
        m["bodies.acquired"] = drivers.Sum(d => d.Acquired);
        m["bodies.released"] = drivers.Sum(d => d.Released);
        m["bodies.failedAcquisitions"] = drivers.Sum(d => d.FailedAcquisitions);
        m["bodies.arrivalTails"] = drivers.Sum(d => d.ArrivalTails);
        m["bodies.walkerFrames"] = drivers.Sum(d => d.WalkerFrames);
        m["bodies.carFrames"] = drivers.Sum(d => d.CarFrames);
        m["bodies.maxWalkers"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.MaxWalkers);
        m["bodies.maxCars"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.MaxCars);
        m["bodies.hiddenTravelFrames"] = drivers.Sum(d => d.HiddenFrames);
        m["bodies.hiddenTravelReleases"] = drivers.Sum(d => d.HiddenReleases);
        // F2c walker follow (E3 lag; the double has no camera, so every correction is an off-screen move).
        m["walkers.lagSamples"] = drivers.Sum(d => d.Walking.LagSamples);
        m["walkers.lagP50Metres"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.Walking.LagQuantile(.5));
        m["walkers.lagP95Metres"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.Walking.LagQuantile(.95));
        m["walkers.lagMaxMetres"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.Walking.MaximumLagMetres);
        m["walkers.moves"] = drivers.Sum(d => d.Walking.Moves);
        m["walkers.lagReleases"] = drivers.Sum(d => d.Walking.LagReleases);
        m["walkers.handoffs"] = drivers.Sum(d => d.Walking.Handoffs - d.Walking.CarHandoffs);
        // F2d car follow (E3 car lag; every correction is an off-screen release and re-acquisition here).
        m["cars.lagSamples"] = drivers.Sum(d => d.Walking.CarLagSamples);
        m["cars.lagP50Metres"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.Walking.CarLagQuantile(.5));
        m["cars.lagP95Metres"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.Walking.CarLagQuantile(.95));
        m["cars.lagMaxMetres"] = drivers.Count == 0 ? 0 : drivers.Max(d => d.Walking.CarMaximumLagMetres);
        m["cars.catchUpFrames"] = drivers.Sum(d => d.Walking.CarCatchUpFrames);
        m["cars.corrections"] = drivers.Sum(d => d.Walking.CarCorrections);
        m["cars.handoffs"] = drivers.Sum(d => d.Walking.CarHandoffs);
    }
}
