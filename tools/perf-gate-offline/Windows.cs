using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.MovementEquivalence;
using PersistentCity.Persistence;
using PersistentCity.Tests;

namespace PersistentCity.PerfGateOffline;

/// <summary>Detached native checkout for the pre-gate: F5g's three player croissant shops with the F1 hour fence, one
/// order per intent, and the F6 change journal fed once per native hour from the same image (as Runtime's
/// NativeFoodMarket observes supply and hours). Never books anything; it only keeps the ordered sales list the run hashes.</summary>
internal sealed class OfflineMarket : IFoodMarket, IFoodSaleEvidence
{
    private readonly FoodBusiness[] shops;
    private readonly HashSet<string> paid;
    private readonly FoodChangeJournal journal;
    private long observedHour = -1;
    public double Clock;
    public readonly List<string> Orders = new();
    public long JournalObservations;

    public OfflineMarket(CityState state, IEnumerable<string> paid)
    {
        shops = F5gPopulationFillTests.Shops(state);
        this.paid = new HashSet<string>(paid, StringComparer.Ordinal);
        journal = new FoodChangeJournal(state.Food!, state.Economy);
        Clock = state.Minute;
    }
    public IEnumerable<string> Paid => paid;
    public double EarliestMinute => Math.Floor(Clock / 60) * 60;
    public IReadOnlyList<FoodBusiness> Read(double minute) => shops;

    /// <summary>The native clock entered <paramref name="nativeMinute"/>: the first minute of a new hour observes supply and hours.</summary>
    public void Enter(double nativeMinute)
    {
        Clock = nativeMinute;
        long hour = (long)Math.Floor(nativeMinute / 60);
        if (hour == observedHour) return;
        observedHour = hour;
        foreach (var shop in shops)
        {
            var offers = shop.Offers.Select(o => new ShopObservedOffer { ProductId = o.ProductId, PriceUnits = o.Price.Units, Stock = 1 }).ToArray();
            journal.ObserveSupply(shop.Id, shop.PlaceId, (int)nativeMinute, offers, offers.Length, ((shop.MealsPerHour ?? 0) + 29) / 30);
            journal.ObserveHours(shop.Id, shop.PlaceId, (int)nativeMinute, shop.Hours);
            JournalObservations++;
        }
    }

    public FoodSale Buy(ResidentMeal meal, double at)
    {
        if (!(at >= EarliestMinute && at < EarliestMinute + 60)) throw new InvalidOperationException("historical native checkout at " + at + " with clock " + Clock);
        var offer = shops.Single(b => b.Id == meal.BusinessId).Offers.Single(o => o.ProductId == meal.ProductId);
        if (offer.Price != meal.Price) return new FoodSale(false, "Price changed");
        if (!paid.Add(meal.IntentId)) throw new InvalidOperationException("duplicate native order " + meal.IntentId);
        Orders.Add(meal.IntentId + "@" + at.ToString("R", CultureInfo.InvariantCulture));
        return new FoodSale(true, "Confirmed");
    }
    public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
}

/// <summary>F6 ordinary visits: every saved shop and leisure place is a native non-player business, priced from the
/// current hour once the clock has entered an hour after load (the shape of F6h's ownership double).</summary>
internal sealed class OfflineOwnership : IOrdinaryVisitOwnership
{
    private readonly Dictionary<string, OrdinaryVisitClass> kinds;
    private readonly long loadedHour;
    private long observedHour = -1;
    public OfflineOwnership(CityState state)
    {
        loadedHour = (long)(state.Minute / 60);
        kinds = state.Places.ToDictionary(p => p.Id, p => p.Kind == PlaceKind.Shop ? OrdinaryVisitClass.Retail
            : p.Kind == PlaceKind.Leisure ? OrdinaryVisitClass.Leisure : OrdinaryVisitClass.ActivityOnly, StringComparer.Ordinal);
        foreach (var shop in state.Food!.Shops) kinds.Remove(shop.PlaceId);
    }
    public bool Pending => false;
    public void Continue() { }
    public void ObserveBoundary(double minute) { if ((long)(minute / 60) > loadedHour) observedHour = (long)(minute / 60); }
    public bool TryGet(string place, out OrdinaryVisitTerms terms)
    {
        terms = new OrdinaryVisitTerms(observedHour, false, kinds.TryGetValue(place, out var kind) ? kind : OrdinaryVisitClass.ActivityOnly);
        return observedHour >= 0;
    }
}

/// <summary>The synchronous planner of the uninterrupted reference and the checkpoints: F5g's cached walking routes with
/// the commute-timing role that Runtime's planner and the window's admitted planner both have, so commuters leave at the
/// same minutes in every run.</summary>
internal sealed class SynchronousCommutePlanner : ICommuteTimingPlanner
{
    private readonly F5gPopulationFillTests.CachedPortalPlanner cached = new();
    public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => cached.Plan(from, to, minute, preferences);
}

/// <summary>A saved city and the native checkout's receipts at one minute: what a rig checkpoint load restores.</summary>
internal sealed record Checkpoint(string Name, double Minute, string Payload, string[] Paid)
{
    public string Sha256 => Fixture.Sha(Payload);
}

/// <summary>One loaded city: the simulator with FT-F4-Declared's attachments, its market and planner.</summary>
internal sealed class Session
{
    public CitySimulator City = null!;
    public OfflineMarket Market = null!;
    public F5hIntegratedAcceptanceTests.AdmittedPortalPlanner? Admitted;

    /// <summary>Loads a checkpoint as Runtime would: F5g fill, the food market, F6 ordinary visits; household finances,
    /// attendance wages and the change journal come with the saved writer-35 city (F5g fixture, finances on).</summary>
    public static Session Load(Checkpoint checkpoint, PopulationFillCatalogue fill, bool resumable)
    {
        var read = SaveCodec.Read(checkpoint.Payload);
        if (read.Status != SaveReadStatus.Loaded) throw new InvalidOperationException("checkpoint refused: " + read.Error);
        var state = read.State!;
        var session = new Session { Market = new OfflineMarket(state, checkpoint.Paid) };
        IRoutePlanner planner = resumable ? session.Admitted = new F5hIntegratedAcceptanceTests.AdmittedPortalPlanner()
            : new SynchronousCommutePlanner();
        session.City = new CitySimulator(state, planner);
        session.City.AttachFoodMarket(session.Market);
        session.City.AttachPopulationFill(fill);
        session.City.AttachOrdinaryExchanges(new OfflineOwnership(state));
        return session;
    }

    public Checkpoint Save(string name)
    {
        City.SynchronizeSnapshot();
        return new Checkpoint(name, City.State.Minute, SaveCodec.Write(City.State), Market.Paid.OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }

    /// <summary>The state hash of a run: the saved city plus the native orders it placed.</summary>
    public string StateHash()
    {
        City.SynchronizeSnapshot();
        return Fixture.Sha(SaveCodec.Write(City.State) + "\n" + string.Join(",", Market.Orders));
    }

    /// <summary>Uninterrupted advance, one native minute epoch at a time with no frame budget: drain the epoch, then let the
    /// clock enter the next minute (the F6h epoch loop). The reference every frame-sliced run must equal.</summary>
    public void AdvanceUninterrupted(double to)
    {
        if (to != Math.Floor(to)) throw new ArgumentException("whole minutes only", nameof(to));
        double start = Math.Floor(City.State.Minute);
        Market.Enter(start); Require(City.BeginAccountingObservations(start), "initial epoch not published");
        for (double epoch = start + 1; epoch <= to; epoch++)
        {
            Drain(AccountingClock.PreviousEpochTarget(epoch));
            Require(City.IsAccountingEpochDrained(epoch), "ending epoch did not drain");
            Market.Enter(epoch); Require(City.BeginAccountingObservations(epoch), "observation epoch not published");
        }
        Drain(to);
    }
    private void Drain(double target)
    {
        while (!City.IsCaughtUpTo(target)) City.AdvanceTo(target, 100_000);
    }
    internal static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

/// <summary>FT-F4-Declared's shape offline: the F5h reference city (152 homes, 957 residents, the declared-load workplaces,
/// three player food shops, F6 household finances) aged to 06:00 on day 3 so the F5g fill has reached its targets, then
/// advanced uninterrupted to every window's start.</summary>
internal static class Fixture
{
    public const string Name = "FT-F4-Declared (offline: F5h reference shape)";
    public const int DefaultSeed = F5hIntegratedAcceptanceTests.Seed;
    public const double Day3 = 3 * 1440 + 360;

    public static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static F5hIntegratedAcceptanceTests.Shape Shape() => F5hIntegratedAcceptanceTests.ReferenceShape();

    /// <summary>F5h's Prefilled (tests/F5hIntegratedAcceptanceTests.cs) with the seed as a parameter.</summary>
    public static Checkpoint Aged(F5hIntegratedAcceptanceTests.Shape shape, int seed)
    {
        var (state, market) = F5gPopulationFillTests.City(shape.Homes, 1, seed: seed, businesses: shape.Businesses);
        var city = F5gPopulationFillTests.Start(state, market, shape.Catalogue, new F5gPopulationFillTests.CachedPortalPlanner());
        F5gPopulationFillTests.Advance(city, Day3, 100_000);
        city.SynchronizeSnapshot();
        return new Checkpoint("day3-06:00", Day3, SaveCodec.Write(city.State), Array.Empty<string>());
    }

    /// <summary>Checkpoints at each requested minute, made in one uninterrupted pass from the aged city.</summary>
    public static Dictionary<double, Checkpoint> Checkpoints(Checkpoint aged, PopulationFillCatalogue fill, IEnumerable<double> minutes)
    {
        var result = new Dictionary<double, Checkpoint>();
        var session = Session.Load(aged, fill, resumable: false);
        foreach (double minute in minutes.Distinct().OrderBy(m => m))
        {
            if (minute == aged.Minute) { result[minute] = aged; continue; }
            session.AdvanceUninterrupted(minute);
            result[minute] = session.Save(Clock(minute));
        }
        return result;
    }

    public static string Clock(double minute)
        => "day" + ((int)(minute / 1440)).ToString(CultureInfo.InvariantCulture) + "-" + TimeSpan.FromMinutes(minute % 1440).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
}

/// <summary>How a window advances the native clock and its bodies.</summary>
internal enum Presentation { Bodies, Hidden, TimeMachine }

/// <summary>One offline window: condition and script as Decision 3 names them, the arm's pace and frame rate.</summary>
internal sealed record WindowSpec(string Condition, string Script, string Arm, double From, double To, int Fps,
    double GameMinutesPerRealSecond, Presentation Presentation, double ClockFactor, int NativeSpeed, bool SliceCheck = false)
{
    public string Id => Condition + "/" + Script + "/" + Arm + "/" + Fps.ToString(CultureInfo.InvariantCulture) + "fps";
}

/// <summary>Native pace. Decision 3: about 3.3 real seconds per game minute at speed 1 and factor 1/3 on the 6 November
/// rig; the #128 replay pins that as 98.6 frames per game minute at 30 FPS. Speeds 2, 3 and 6 are assumed to scale the
/// speed-1 rate linearly; G9 measures the real rates.</summary>
internal static class Pace
{
    public const double DeclaredFactor = 1.0 / 3, FastestFactor = 1;
    public static readonly double NativeSpeed1 = 30 / 98.6 / DeclaredFactor;
    public static double At(double factor, int speed) => NativeSpeed1 * factor * speed;
}

/// <summary>Per game hour of a window: frames, simulated real time against the unheld clock, holds and lag.</summary>
internal sealed class HourRow
{
    public int Day, Hour;
    public double GameMinutes;
    public long Frames, HeldFrames;
    public long LongestHoldFrames;
    public double MaxLag;
}

/// <summary>Everything one window measured. Times are offline .NET Stopwatch milliseconds; real time is frames/fps.</summary>
internal sealed class WindowResult
{
    public WindowSpec Spec = null!;
    public string CheckpointSha256 = "";
    public long Frames, Units, CitizenEvents, PlanningSlices, HeldFrames, Gc0, Gc1, Gc2;
    public long HistoricalClockMisses, Orders, JournalObservations;
    public List<double> CoreMs = new(), DriverMs = new(), ModWorkMs = new();
    public List<(long Frame, double Minute, double Ms, double CoreMs, double DriverMs, string Component, int Gc)> Spikes = new();
    public double UnitMax; public string UnitMaxKind = ""; public double UnitMaxMinute;
    public List<(double Minute, double Lag, bool Held)> Lag = new();
    public List<HourRow> Hours = new();
    public double MaxLag, LongestHoldSeconds, LongestRecoverySeconds;
    public long Holds;
    /// <summary>Held frames by why the frame before them ended: the frame budget part that left the epoch undrained.</summary>
    public SortedDictionary<string, long> HoldCauses = new(StringComparer.Ordinal);
    public int MaxWalkers, MaxCars; public double MedianBodies, MedianWalkers, MedianCars;
    public long BodyAcquisitions, BodyReleases;
    public double MaxPathsInFrame;
    public string StateHash = "", UninterruptedHash = "";
    public double ToolSeconds;
    public List<double> FrameMinute = new();
}

/// <summary>The F4e frame loop: Runtime's PopulationRuntime frame shape (PopulationFrameBudget units, citizen events and
/// soft milliseconds; the first unit always runs; planning gets the frame's remaining time) behind the F1 accounting clock
/// (AccountingClock.Limit holds native time until the epoch drains, the checkout fence stays in the native hour), with the
/// F2a presentation driver moving and selecting bodies around a fixed focus every frame.</summary>
internal static class WindowRunner
{
    public const int Pedestrians = 48, Vehicles = 24;

    public static WindowResult Run(WindowSpec spec, Checkpoint checkpoint, PopulationFillCatalogue fill, IReadOnlyList<Position> focusPath, bool uninterruptedCheck)
    {
        var wall = Stopwatch.StartNew();
        var session = Session.Load(checkpoint, fill, resumable: true);
        var city = session.City; var market = session.Market; var planner = session.Admitted!;
        var food = F5hIntegratedAcceptanceTests.Food(city);
        var r = new WindowResult { Spec = spec, CheckpointSha256 = checkpoint.Sha256 };
        var driver = spec.Presentation == Presentation.Bodies
            ? new PresentationDriver(city, new DriverSettings { Pedestrians = Pedestrians, Vehicles = Vehicles, Seed = 0 }) : null;
        bool skip = spec.Presentation == Presentation.TimeMachine;
        double native = checkpoint.Minute, end = spec.To;
        if (checkpoint.Minute != spec.From) throw new InvalidOperationException("checkpoint " + checkpoint.Minute + " is not the window start " + spec.From);
        long misses0 = city.HistoricalClockMisses;
        double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;
        long holdFrames = 0, recoveryFrames = -1;
        var bodies = new List<int>(); var walkers = new List<int>(); var cars = new List<int>();
        HourRow? hour = null;
        double focusMetres = 0;
        string previousStop = "start";
        // Collect the checkpoint load's garbage before timing, as the #128 replay does: a collection of memory the
        // window did not allocate must not land in one of its frames.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        while (!city.IsCaughtUpTo(end))
        {
            if (++r.Frames > 20_000_000) throw new InvalidOperationException("nonterminating window " + spec.Id);
            planner.NewFrame();
            Session.Require(AccountingClock.Compatible(city.State.Minute, native), "input epoch escaped");
            Session.Require(city.IsAccountingEpochDrained(native), "unfinished ending epoch escaped");
            bool ready = city.IsCaughtUpTo(AccountingClock.Target(native));
            float requested = (float)Math.Min(skip ? 120 : spec.GameMinutesPerRealSecond / spec.Fps, end - native);
            float allowed = AccountingClock.Limit(ready, native, requested);
            bool held = allowed == 0 && requested > 0;
            int nativeHour = (int)Math.Floor(native / 60); float nativeMinute = (float)(native - nativeHour * 60.0) + allowed;
            while (nativeMinute >= 60) { nativeMinute -= 60; nativeHour++; }
            native = nativeHour * 60.0 + nativeMinute;
            market.Enter(native);
            Session.Require(city.BeginAccountingObservations(native), "observation epoch not published");
            double target = Math.Max(city.State.Minute, AccountingClock.Target(native));
            double lag = target - city.State.Minute;

            // F2a: native bodies move in real time, before the simulation advances, also on frames that hold native time.
            long frameStarted = Stopwatch.GetTimestamp();
            driver?.MoveBodies(1.0 / spec.Fps, allowed);
            double driverMs = Ms(frameStarted, Stopwatch.GetTimestamp());

            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var budget = new PopulationFrameBudget(1.0 / spec.Fps, skip);
            long started = Stopwatch.GetTimestamp();
            double Elapsed() => Ms(started, Stopwatch.GetTimestamp());
            int citizens = 0; string component = "idle"; double slowest = 0;
            string stop = "units";
            for (int unit = 0; unit < budget.Units; unit++)
            {
                if (city.PlanningPending)
                {
                    long p = Stopwatch.GetTimestamp();
                    bool done = city.ContinuePlanning(Math.Max(0, budget.Milliseconds - Elapsed()));
                    double pm = Ms(p, Stopwatch.GetTimestamp()); r.PlanningSlices++;
                    if (pm > slowest) { slowest = pm; component = "planning"; }
                    if (!done)
                    {
                        if (Elapsed() >= budget.Milliseconds) { stop = "milliseconds"; break; }
                        if (planner.AdmissionExhausted) { stop = "path-admission"; break; }
                        continue;
                    }
                    r.CitizenEvents++; citizens++;
                    if (citizens >= budget.CitizenEvents) { stop = "citizen-events"; break; }
                    if (Elapsed() >= budget.Milliseconds) { stop = "milliseconds"; break; }
                    continue;
                }
                long fillBefore = city.PopulationFillUnits, postedBefore = food.BudgetPostings;
                long t = Stopwatch.GetTimestamp();
                var advanced = city.AdvanceFrameSlice(target);
                double ms = Ms(t, Stopwatch.GetTimestamp());
                string kind = city.PopulationFillUnits != fillBefore ? "fill" : food.BudgetPostings != postedBefore ? "posting"
                    : advanced.FoodContinuationEvents > 0 ? "food" : advanced.TrafficEvents > 0 ? "traffic" : advanced.Events > 0 ? "citizen" : "idle";
                if (ms > slowest) { slowest = ms; component = kind; }
                if (ms > r.UnitMax) { r.UnitMax = ms; r.UnitMaxKind = kind; r.UnitMaxMinute = city.State.Minute; }
                r.Units += advanced.Events; r.CitizenEvents += advanced.CitizenEvents; citizens += advanced.CitizenEvents;
                if (advanced.PlanningPending) { if (Elapsed() >= budget.Milliseconds) { stop = "milliseconds"; break; } continue; }
                if (advanced.CaughtUp) { stop = "caught-up"; break; }
                if (citizens >= budget.CitizenEvents) { stop = "citizen-events"; break; }
                if (Elapsed() >= budget.Milliseconds) { stop = "milliseconds"; break; }
            }
            city.SynchronizeSnapshot();
            double coreMs = Elapsed();

            long presented = Stopwatch.GetTimestamp();
            if (driver != null)
            {
                var focus = Along(focusPath, focusMetres);
                driver.Update(focus, false);
                bodies.Add(driver.Count); walkers.Add(driver.Walkers); cars.Add(driver.Cars);
            }
            // S-path walks the focus at 1.4 m/s of real time.
            if (focusPath.Count > 1) focusMetres += 1.4 / spec.Fps;
            driverMs += Ms(presented, Stopwatch.GetTimestamp());

            int gc = GC.CollectionCount(2) != g2 ? 2 : GC.CollectionCount(1) != g1 ? 1 : GC.CollectionCount(0) != g0 ? 0 : -1;
            double modWork = coreMs + driverMs;
            r.CoreMs.Add(coreMs); r.DriverMs.Add(driverMs); r.ModWorkMs.Add(modWork); r.FrameMinute.Add(native);
            if (modWork >= 30) r.Spikes.Add((r.Frames, city.State.Minute, modWork, coreMs, driverMs, coreMs >= driverMs ? component : "presentation-driver", gc));

            // S rows: holds (consecutive held frames), recovery to lag <= 0.1 after a hold, lag and pace per game hour.
            if (held)
            {
                r.HeldFrames++; holdFrames++; if (holdFrames == 1) r.Holds++; recoveryFrames = 0;
                r.HoldCauses[previousStop] = r.HoldCauses.TryGetValue(previousStop, out long n) ? n + 1 : 1;
            }
            else
            {
                r.LongestHoldSeconds = Math.Max(r.LongestHoldSeconds, holdFrames / (double)spec.Fps); holdFrames = 0;
                if (recoveryFrames >= 0)
                {
                    recoveryFrames++;
                    if (lag <= 0.1) { r.LongestRecoverySeconds = Math.Max(r.LongestRecoverySeconds, recoveryFrames / (double)spec.Fps); recoveryFrames = -1; }
                }
            }
            previousStop = stop;
            r.MaxLag = Math.Max(r.MaxLag, lag);
            int day = (int)(Math.Max(0, native - 1e-9) / 1440), hourOfDay = (int)(Math.Max(0, native - 1e-9) % 1440 / 60);
            if (hour == null || hour.Day != day || hour.Hour != hourOfDay)
            {
                hour = new HourRow { Day = day, Hour = hourOfDay };
                double hourStart = day * 1440.0 + hourOfDay * 60;
                hour.GameMinutes = Math.Min(end, hourStart + 60) - Math.Max(spec.From, hourStart);
                r.Hours.Add(hour);
            }
            hour.Frames++; if (held) { hour.HeldFrames++; } hour.LongestHoldFrames = Math.Max(hour.LongestHoldFrames, holdFrames);
            hour.MaxLag = Math.Max(hour.MaxLag, lag);
        }
        r.LongestHoldSeconds = Math.Max(r.LongestHoldSeconds, holdFrames / (double)spec.Fps);
        r.Gc0 = GC.CollectionCount(0) - gc0; r.Gc1 = GC.CollectionCount(1) - gc1; r.Gc2 = GC.CollectionCount(2) - gc2;
        r.HistoricalClockMisses = city.HistoricalClockMisses - misses0;
        r.Orders = market.Orders.Count; r.JournalObservations = market.JournalObservations;
        planner.NewFrame(); r.MaxPathsInFrame = planner.MaxPathsInFrame;
        if (driver != null)
        {
            driver.BeforeSave();
            r.MaxWalkers = driver.MaxWalkers; r.MaxCars = driver.MaxCars; r.BodyAcquisitions = driver.Acquired; r.BodyReleases = driver.Released;
            r.MedianBodies = Median(bodies); r.MedianWalkers = Median(walkers); r.MedianCars = Median(cars);
        }
        r.StateHash = session.StateHash();
        if (uninterruptedCheck)
        {
            var reference = Session.Load(checkpoint, fill, resumable: false);
            reference.AdvanceUninterrupted(end);
            r.UninterruptedHash = reference.StateHash();
        }
        r.ToolSeconds = wall.Elapsed.TotalSeconds;
        return r;
    }

    private static double Median(List<int> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted[sorted.Length / 2];
    }

    /// <summary>The point <paramref name="metres"/> along a closed polyline (a stationary focus for one point).</summary>
    public static Position Along(IReadOnlyList<Position> path, double metres)
    {
        if (path.Count == 1) return path[0];
        double total = 0;
        for (int i = 0; i < path.Count; i++) total += Position.Distance(path[i], path[(i + 1) % path.Count]);
        double at = total > 0 ? metres % total : 0;
        for (int i = 0; i < path.Count; i++)
        {
            var a = path[i]; var b = path[(i + 1) % path.Count];
            double d = Position.Distance(a, b);
            if (at <= d && d > 0) { double f = at / d; return new Position(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f); }
            at -= d;
        }
        return path[0];
    }
}
