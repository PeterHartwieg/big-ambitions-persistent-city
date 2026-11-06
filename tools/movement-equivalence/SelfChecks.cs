using System;
using System.Collections.Generic;

namespace PersistentCity.MovementEquivalence;

/// <summary>Harness self-checks (F2a row): same seed byte-identical; the bodies-off arms (no focus, zero
/// caps) equal the plain simulation; injected faults fail loudly; the reload arm resumes and, without
/// bodies, equals the uninterrupted run.</summary>
public static class SelfChecks
{
    public const int Residents = 96;

    public static MovementOptions Small(string arm, int residents = Residents) => new MovementOptions { Arm = arm, Residents = residents, Days = 1,
        Bodies = new DriverSettings { Pedestrians = 48, Vehicles = 24, Lag = LagModel.Native } };

    public static List<(string Check, bool Pass, string Detail)> Run(int residents = Residents)
    {
        var checks = new List<(string, bool, string)>();
        var plain = Small("plain", residents); plain.Driver = false;
        var plainResult = MovementRun.Run(plain);
        var again = MovementRun.Run(Copy(plain));
        checks.Add(("plain same seed byte-identical", Same(plainResult, again), plainResult.FinalSaveSha256));
        var bodies = Small("bodies", residents);
        var bodiesResult = MovementRun.Run(bodies);
        var bodiesAgain = MovementRun.Run(Small("bodies", residents));
        checks.Add(("bodies same seed byte-identical", Same(bodiesResult, bodiesAgain) && bodiesResult.Metrics["bodies.acquired"] > 0,
            "acquired " + bodiesResult.Metrics["bodies.acquired"]));
        var other = Small("seed1", residents); other.Driver = false; other.Seed = 1;
        checks.Add(("another seed differs", MovementRun.Run(other).FinalSaveSha256 != plainResult.FinalSaveSha256, ""));
        var none = Small("focus-none", residents); none.Focus = FocusPath.None;
        checks.Add(("no-focus driver equals plain", Same(MovementRun.Run(none), plainResult), ""));
        var zero = Small("caps-0", residents); zero.Bodies.Pedestrians = 0; zero.Bodies.Vehicles = 0;
        var zeroResult = MovementRun.Run(zero);
        checks.Add(("zero-cap driver equals plain", Same(zeroResult, plainResult) && zeroResult.Metrics["bodies.acquired"] == 0, ""));
        var reload = Small("plain-reload", residents); reload.Driver = false; reload.ReloadMidJourney = true;
        var reloadResult = MovementRun.Run(reload);
        checks.Add(("bodies-off mid-journey reload resumes byte-identical", reloadResult.Reloads == 1 && reloadResult.InFlightAtReload > 0
            && Same(reloadResult, plainResult), "in flight " + reloadResult.InFlightAtReload));
        var bodyReload = Small("bodies-reload", residents); bodyReload.ReloadMidJourney = true;
        var bodyReloadResult = MovementRun.Run(bodyReload);
        checks.Add(("bodies mid-journey reload resumes", bodyReloadResult.Reloads == 1 && bodyReloadResult.BodiesAtReload > 0,
            "bodies " + bodyReloadResult.BodiesAtReload + ", in flight " + bodyReloadResult.InFlightAtReload));
        foreach (var fault in new[] { Fault.DuplicateArrival, Fault.SkippedEvent })
        {
            var faulty = Small("fault-" + fault, residents); faulty.Fault = fault;
            string detail = "";
            bool failed = false;
            try { MovementRun.Run(faulty); }
            catch (HarnessViolation error) { failed = true; detail = error.Message; }
            checks.Add(("fault " + fault + " fails loudly", failed, detail));
        }
        return checks;
    }

    private static MovementOptions Copy(MovementOptions o) => new MovementOptions { Arm = o.Arm, Residents = o.Residents, Days = o.Days,
        Driver = o.Driver, Focus = o.Focus, Seed = o.Seed, FramesPerSecond = o.FramesPerSecond, Bodies = new DriverSettings {
            Pedestrians = o.Bodies.Pedestrians, Vehicles = o.Bodies.Vehicles, Lag = o.Bodies.Lag, AcquisitionFailurePercent = o.Bodies.AcquisitionFailurePercent } };

    public static bool Same(MovementResult a, MovementResult b) => Equivalence.Compare(a, b).Identical;
}
