using System;
using System.Linq;

namespace PersistentCity.EconomyInterventions;

public static class SelfChecks
{
    public const int SmallResidents = 48;
    public const double MidEpochReload = 20 * 1440 + 750.5;
    public const double InputBoundaryReload = 20 * 1440 + 660;

    public static void Run()
    {
        var control = EconomyRun.Run(Small());
        Require(control.Json == EconomyRun.Run(Small()).Json, "same seed did not give byte-identical metrics");
        var reloaded = EconomyRun.Run(Small(reload: MidEpochReload));
        Require(reloaded.InFlightAtReload > 0, "mid-epoch reload had no meal in flight");
        Require(reloaded.Json == control.Json && reloaded.FinalSaveSha256 == control.FinalSaveSha256, "mid-run save/reload changed metrics");
        var staffing = InterventionArms.All().Single(a => a.Name == "lunch-counters-halved");
        var arm = EconomyRun.Run(Small(staffing));
        Require(arm.Json == EconomyRun.Run(Small(staffing, InputBoundaryReload)).Json, "reload at an input boundary changed metrics");
        Require(arm.Json != control.Json, "intervention inputs had no observable effect on metrics");
        Require(control.ConservationChecks == (long)EconomyRun.Days * SmallResidents * EconomyFixture.RepresentationFactor,
            "conservation identity was not checked for every owner every day");
        Console.WriteLine($"PASS determinism: {SmallResidents} residents x {EconomyRun.Days} days byte-identical, mid-epoch reload ({reloaded.InFlightAtReload} in flight) and input-boundary reload");

        Expect<InputViolation>(() => EconomyRun.Run(Small(new Arm("fault", "mid-epoch", "", new[] {
            new InterventionInput(15 * 1440 + 660.5, InputKind.MealsPerHour, EconomyFixture.Shop, "", 30) }))), "epoch boundary");
        var market = EconomyFixture.Build(0, 4).Market;
        var schedule = new InputSchedule(staffing.Inputs, EconomyFixture.StartMinute, 60 * 1440);
        long first = (long)staffing.Inputs[0].Minute;
        Expect<InputViolation>(() => schedule.PublishAt(first, false, first - 0.5, market), "drained");
        Expect<InputViolation>(() => schedule.PublishAt(first, true, first, market), "observed");
        Console.WriteLine("PASS inputs mutate only at drained, unobserved epoch boundaries; every native read is epoch-fenced");

        Expect<LedgerViolation>(() => EconomyRun.Run(Small(fault: Fault.DuplicatedDebit)), "watermarked debits");
        Expect<LedgerViolation>(() => EconomyRun.Run(Small(fault: Fault.UnbalancedDebit)), "conservation identity failed");
        Expect<LedgerViolation>(() => EconomyRun.Run(Small(fault: Fault.DuplicateNativeOrder)), "duplicate native receipt");
        Console.WriteLine("PASS fault injections fail loudly: duplicated debit, unbalanced debit, duplicate native order");
    }

    public static RunOptions Small(Arm? arm = null, double? reload = null, Fault fault = Fault.None) => new RunOptions {
        Family = arm?.Family ?? "control", Arm = arm?.Name ?? "control", Seed = 3, Residents = SmallResidents,
        Inputs = arm?.Inputs ?? Array.Empty<InterventionInput>(), ReloadAt = reload, Fault = fault };

    private static void Expect<T>(Action action, string fragment) where T : Exception
    {
        try { action(); }
        catch (T e) when (e.Message.Contains(fragment, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("expected " + typeof(T).Name + " containing '" + fragment + "'");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
