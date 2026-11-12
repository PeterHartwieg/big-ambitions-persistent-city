using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.MovementEquivalence;

public sealed class Arm
{
    public string Name = "", Family = "", Reference = MovementArms.Plain;
    public Func<MovementOptions> Options = () => new MovementOptions();
}

public static class MovementArms
{
    public const string Plain = "bodies-off/plain-60";
    public const string WalkingPlain = "walking/plain-60";
    public const string VisitorsPlain = "visitors/plain-60";
    public static readonly (int Walkers, int Cars)[] Caps = { (0, 0), (1, 1), (24, 12), (48, 24), (256, 64) };
    public static readonly int[] FailurePercents = { 0, 10, 50 };
    public static readonly int[] Cadences = { 30, 60, 120 };

    private static MovementOptions Default(string name) => new MovementOptions { Arm = name, Focus = FocusPath.Stationary,
        Bodies = new DriverSettings { Pedestrians = 48, Vehicles = 24, Lag = LagModel.Native } };

    public static List<Arm> All(int residents, int days)
    {
        var arms = new List<Arm>();
        void Add(string family, string name, Func<MovementOptions> make, string reference = Plain)
            => arms.Add(new Arm { Family = family, Name = family + "/" + name, Reference = reference, Options = () =>
            { var o = make(); o.Arm = family + "/" + name; o.Residents = residents; o.Days = days; return o; } });

        foreach (int fps in Cadences)
            Add("bodies-off", "plain-" + fps, () => new MovementOptions { Driver = false, FramesPerSecond = fps }, fps == 60 ? "" : Plain);
        Add("bodies-off", "focus-none", () => { var o = Default(""); o.Focus = FocusPath.None; return o; });
        Add("bodies-off", "plain-reload", () => new MovementOptions { Driver = false, ReloadMidJourney = true });
        Add("bodies-off", "plain-hidden-travel", () => new MovementOptions { Driver = false, HiddenTravel = true });
        foreach (var focus in new[] { FocusPath.Stationary, FocusPath.Sweep, FocusPath.AwayReturn })
            Add("focus", focus switch { FocusPath.Sweep => "sweep", FocusPath.AwayReturn => "away-return", _ => "stationary" },
                () => { var o = Default(""); o.Focus = focus; return o; });
        foreach (var (walkers, cars) in Caps)
        {
            if (walkers == 48) continue;
            Add("caps", walkers + "-" + cars, () => { var o = Default(""); o.Bodies.Pedestrians = walkers; o.Bodies.Vehicles = cars; return o; });
        }
        Add("lag", "ideal", () => { var o = Default(""); o.Bodies.Lag = LagModel.Ideal; return o; });
        Add("lag", "slow-head-car", () => { var o = Default(""); o.Bodies.Lag = LagModel.SlowHeadCar; return o; });
        foreach (int percent in FailurePercents.Where(p => p != 0))
            Add("failures", percent + "pct", () => { var o = Default(""); o.Bodies.AcquisitionFailurePercent = percent; return o; });
        foreach (int fps in Cadences.Where(f => f != 60))
            Add("cadence", fps + "fps", () => { var o = Default(""); o.FramesPerSecond = fps; return o; }, "bodies-off/plain-" + fps);
        Add("transitions", "reload-mid-journey", () => { var o = Default(""); o.ReloadMidJourney = true; return o; });
        Add("transitions", "hidden-travel", () => { var o = Default(""); o.HiddenTravel = true; return o; }, "bodies-off/plain-hidden-travel");
        foreach (int fps in new[] { 60, 30 })
            Add("walking", "plain-" + fps, () => new MovementOptions { Driver = false, WalkingOnly = true, FramesPerSecond = fps }, "");
        Add("walking", "plain-hidden-travel", () => new MovementOptions { Driver = false, WalkingOnly = true, HiddenTravel = true }, "");
        void Walking(string name, Action<MovementOptions> change, string reference = WalkingPlain)
            => Add("walking", name, () => { var o = Default(""); o.WalkingOnly = true; change(o); return o; }, reference);
        Walking("stationary", o => { });
        Walking("sweep", o => o.Focus = FocusPath.Sweep);
        Walking("away-return", o => o.Focus = FocusPath.AwayReturn);
        Walking("caps-1", o => { o.Bodies.Pedestrians = 1; o.Bodies.Vehicles = 1; });
        Walking("caps-256", o => { o.Bodies.Pedestrians = 256; o.Bodies.Vehicles = 64; });
        Walking("lag-ideal", o => o.Bodies.Lag = LagModel.Ideal);
        Walking("failures-50pct", o => o.Bodies.AcquisitionFailurePercent = 50);
        Walking("cadence-30fps", o => o.FramesPerSecond = 30, "walking/plain-30");
        Walking("reload-mid-journey", o => o.ReloadMidJourney = true);
        Walking("hidden-travel", o => o.HiddenTravel = true, "walking/plain-hidden-travel");
        foreach (int fps in new[] { 60, 30 })
            Add("visitors", "plain-" + fps, () => new MovementOptions { Driver = false, Visitors = true, FramesPerSecond = fps }, "");
        Add("visitors", "plain-hidden-travel", () => new MovementOptions { Driver = false, Visitors = true, HiddenTravel = true }, "");
        void Visitors(string name, Action<MovementOptions> change, string reference = VisitorsPlain)
            => Add("visitors", name, () => { var o = Default(""); o.Visitors = true; change(o); return o; }, reference);
        Visitors("stationary", o => { });
        Visitors("sweep", o => o.Focus = FocusPath.Sweep);
        Visitors("away-return", o => o.Focus = FocusPath.AwayReturn);
        Visitors("caps-1", o => { o.Bodies.Pedestrians = 1; o.Bodies.Vehicles = 1; });
        Visitors("caps-256", o => { o.Bodies.Pedestrians = 256; o.Bodies.Vehicles = 64; });
        Visitors("lag-ideal", o => o.Bodies.Lag = LagModel.Ideal);
        Visitors("failures-50pct", o => o.Bodies.AcquisitionFailurePercent = 50);
        Visitors("cadence-30fps", o => o.FramesPerSecond = 30, "visitors/plain-30");
        Visitors("reload-mid-journey", o => o.ReloadMidJourney = true);
        Visitors("hidden-travel", o => o.HiddenTravel = true, "visitors/plain-hidden-travel");
        Add("visitors", "plain-120", () => new MovementOptions { Driver = false, Visitors = true, FramesPerSecond = 120 }, "");
        Visitors("focus-none", o => o.Focus = FocusPath.None);
        Visitors("caps-0", o => { o.Bodies.Pedestrians = 0; o.Bodies.Vehicles = 0; });
        Visitors("caps-24", o => { o.Bodies.Pedestrians = 24; o.Bodies.Vehicles = 12; });
        Visitors("failures-10pct", o => o.Bodies.AcquisitionFailurePercent = 10);
        Visitors("lag-slow-head-car", o => o.Bodies.Lag = LagModel.SlowHeadCar);
        Visitors("cadence-120fps", o => o.FramesPerSecond = 120, "visitors/plain-120");
        void Combined(string name, bool visitors, FocusPath focus, int walkers, int cars, int failures, int fps, LagModel lag,
            bool reload, bool hidden, string reference)
            => Add("combined", name, () =>
            {
                var o = Default(""); o.Visitors = visitors; o.Focus = focus; o.Bodies.Pedestrians = walkers; o.Bodies.Vehicles = cars;
                o.Bodies.AcquisitionFailurePercent = failures; o.FramesPerSecond = fps; o.Bodies.Lag = lag;
                o.ReloadMidJourney = reload; o.HiddenTravel = hidden; return o;
            }, reference);
        Combined("sweep-256-10pct-120fps", false, FocusPath.Sweep, 256, 64, 10, 120, LagModel.Native, false, false, "bodies-off/plain-120");
        Combined("away-return-1-50pct-30fps-reload", false, FocusPath.AwayReturn, 1, 1, 50, 30, LagModel.Native, true, false, "bodies-off/plain-30");
        Combined("stationary-24-50pct-slow-head-hidden", false, FocusPath.Stationary, 24, 12, 50, 60, LagModel.SlowHeadCar, false, true,
            "bodies-off/plain-hidden-travel");
        Combined("visitors-sweep-256-50pct-30fps-reload", true, FocusPath.Sweep, 256, 64, 50, 30, LagModel.Native, true, false, "visitors/plain-30");
        Combined("visitors-away-return-24-10pct-slow-head-hidden", true, FocusPath.AwayReturn, 24, 12, 10, 60, LagModel.SlowHeadCar, false, true,
            "visitors/plain-hidden-travel");
        Combined("visitors-stationary-1-10pct-120fps", true, FocusPath.Stationary, 1, 1, 10, 120, LagModel.Native, false, false, "visitors/plain-120");
        foreach (int fps in Cadences.Where(f => f != 60))
        {
            Add("bodies-off", "plain-hidden-" + fps, () => new MovementOptions { Driver = false, HiddenTravel = true, FramesPerSecond = fps }, "");
            Add("visitors", "plain-hidden-" + fps, () => new MovementOptions { Driver = false, Visitors = true, HiddenTravel = true, FramesPerSecond = fps }, "");
        }
        foreach (var t in Pairwise())
            Add("pairwise", t.Name, () =>
            {
                var o = Default(""); o.Visitors = t.Visitors; o.Focus = t.Focus; o.Bodies.Pedestrians = t.Caps.Walkers; o.Bodies.Vehicles = t.Caps.Cars;
                o.Bodies.AcquisitionFailurePercent = t.Failures; o.FramesPerSecond = t.Fps; o.Bodies.Lag = t.Lag;
                o.ReloadMidJourney = t.Reload; o.HiddenTravel = t.Hidden; return o;
            }, PlainReference(t.Visitors, t.Fps, t.Hidden));
        return arms;
    }

    public static string PlainReference(bool visitors, int fps, bool hidden)
        => (visitors ? "visitors/plain-" : "bodies-off/plain-") + (hidden ? fps == 60 ? "hidden-travel" : "hidden-" + fps : fps.ToString());

    public static readonly FocusPath[] FocusPaths = { FocusPath.None, FocusPath.Stationary, FocusPath.Sweep, FocusPath.AwayReturn };
    public static readonly LagModel[] Lags = { LagModel.Ideal, LagModel.Native, LagModel.SlowHeadCar };
    public static readonly (bool Reload, bool Hidden)[] Transitions = { (false, false), (true, false), (false, true), (true, true) };

    public sealed record Factors(bool Visitors, FocusPath Focus, (int Walkers, int Cars) Caps, int Failures, int Fps, LagModel Lag,
        bool Reload, bool Hidden)
    {
        public int[] Levels => new[] { Visitors ? 1 : 0, Array.IndexOf(FocusPaths, Focus), Array.IndexOf(MovementArms.Caps, Caps),
            Array.IndexOf(FailurePercents, Failures), Array.IndexOf(Cadences, Fps), Array.IndexOf(Lags, Lag),
            Array.IndexOf(Transitions, (Reload, Hidden)) };
        public string Name => (Visitors ? "visitors-" : "") + Focus switch { FocusPath.None => "none", FocusPath.Sweep => "sweep",
            FocusPath.AwayReturn => "away-return", _ => "stationary" } + "-" + Caps.Walkers + "-" + Caps.Cars + "-" + Failures + "pct-"
            + Fps + "fps-" + Lag switch { LagModel.Ideal => "ideal", LagModel.SlowHeadCar => "slow-head", _ => "native" }
            + (Reload ? "-reload" : "") + (Hidden ? "-hidden" : "");
    }

    public static readonly int[] FactorSizes = { 2, 4, 5, 3, 3, 3, 4 };

    public static bool Valid(Factors t) => !t.Reload || (t.Focus != FocusPath.None && t.Caps.Walkers > 0 && !(t.Visitors && t.Caps.Walkers < 2));

    public static IEnumerable<Factors> Product()
    {
        foreach (bool visitors in new[] { false, true })
        foreach (var focus in FocusPaths)
        foreach (var caps in Caps)
        foreach (int failures in FailurePercents)
        foreach (int fps in Cadences)
        foreach (var lag in Lags)
        foreach (var (reload, hidden) in Transitions)
            yield return new Factors(visitors, focus, caps, failures, fps, lag, reload, hidden);
    }

    private static IEnumerable<(int A, int La, int B, int Lb)> Pairs(int[] levels)
    {
        for (int a = 0; a < levels.Length; a++)
            for (int b = a + 1; b < levels.Length; b++)
                yield return (a, levels[a], b, levels[b]);
    }

    public static HashSet<(int, int, int, int)> ValidPairs() => Product().Where(Valid).SelectMany(t => Pairs(t.Levels)).ToHashSet();

    public static List<Factors> Pairwise()
    {
        var candidates = Product().Where(Valid).Select(t => (Factors: t, Pairs: Pairs(t.Levels).ToArray())).ToList();
        var uncovered = candidates.SelectMany(c => c.Pairs).ToHashSet();
        var chosen = new List<Factors>();
        while (uncovered.Count > 0)
        {
            var best = candidates[0]; int bestCount = -1;
            foreach (var c in candidates)
            {
                int count = c.Pairs.Count(uncovered.Contains);
                if (count > bestCount) { best = c; bestCount = count; }
            }
            chosen.Add(best.Factors);
            uncovered.ExceptWith(best.Pairs);
        }
        return chosen;
    }
}
