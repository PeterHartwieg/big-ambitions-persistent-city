using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// 10 Nov (b) Mac rig, run mac-b-save-reload: S1 was saved at logical minute 1921.9687 (08:01, 0.03 game minutes
/// before 08:02). After the native load, native time ticked into 08:02 before TryStart installed the clock hold,
/// and activation refused with "Accounting input epoch was lost". H1 (1983.011, 0.99 minutes of slack) reloaded.
/// These tests drive the real save-epoch binding, save session, codec and simulator through that sequence.
/// </summary>
public sealed partial class FoodCommerceTests
{
    private const double RigS1Minute = 1921.9687246084213; // The S1 success marker's minute.

    /// <summary>Native time is Day*1440 + Hour*60 + a float minute of the hour.</summary>
    private static double Native(int hourStart, float minuteOfHour) => hourStart + (double)minuteOfHour;

    private sealed class SaveObject { public readonly Dictionary<string, string> modData = new(StringComparer.Ordinal); }

    private static Dictionary<string, string> OrdinarySave(SaveEpochBinding binding, SaveObject save, SaveSession session, CitySimulator city, double native)
    {
        var result = SaveEpochCallback.Run(binding, save, save.modData, false, ticket =>
        {
            // CaptureSteps: observations first, then the payload write inside the ticket.
            Assert.True(city.BeginAccountingObservations(native));
            Assert.True(binding.CanPublish(ticket, save, save.modData));
            var written = session.WriteTo(save.modData, city.State);
            return written.Success ? SaveEpochCapture.Written(city.State.Minute, city.State.PopulationKey) : SaveEpochCapture.Refused(written.Error);
        }, reason => throw new Exception("stopped: " + reason), _ => { });
        Assert.Equal(SaveEpochResult.Published, result);
        return new Dictionary<string, string>(save.modData, StringComparer.Ordinal);
    }

    /// <summary>TryStart's worker on a loaded file: epoch marker, strict read, marker-describes-state.</summary>
    private static (CityState State, SaveEpochMarker Marker) LoadFile(Dictionary<string, string> file, string populationKey)
    {
        Assert.False(AccountingStopStore.IsStopped(file));
        Assert.Null(SaveEpochStore.CheckForStart(file, out var marker));
        var read = SaveSession.Open(new Dictionary<string, string> { [SaveStore.StateKey] = file[SaveStore.StateKey] }, populationKey).InitialReadResult;
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Null(SaveEpochStore.DescribesState(marker, read.SourceSchema, read.State));
        return (read.State!, marker!);
    }

    /// <summary>The activation order in CompleteInitialization: classify, then the drained check before the market.</summary>
    private static LoadClockStart Activate(CityState state, double native, Walking routes, Market market, out CitySimulator city)
    {
        var clock = AccountingClock.ClassifyLoad(state.Minute, native);
        city = new CitySimulator(state, routes);
        if (clock == LoadClockStart.Lost) return clock;
        if (!city.IsAccountingEpochDrained(clock == LoadClockStart.CatchUp ? state.Minute : native)) return LoadClockStart.Lost;
        city.AttachFoodMarket(market);
        return clock;
    }

    [Fact]
    public void FirstOrdinarySaveAfterADeferredCreationSaveReloadsAcrossOneUnheldNativeMinute()
    {
        var (city, _, routes) = Ready(60);
        var save = new SaveObject();
        // #202: the creation save during initialization is deferred and writes nothing.
        var binding = new SaveEpochBinding(save, save.modData, 0, initializing: true);
        Assert.Equal(SaveEpochResult.Deferred, SaveEpochCallback.Run(binding, save, save.modData, false,
            _ => throw new Exception("captured while initializing"), reason => throw new Exception(reason), _ => { }));
        Assert.Empty(save.modData);
        binding.CompleteInitialization();
        var session = SaveSession.Open(save.modData, city.State.PopulationKey);
        Assert.True(session.BindTo(save.modData).Success);

        // The rig's S1 minute of the hour (xx:01.97), at 11:01 so lunch is under way, then the first ordinary save.
        double native = Native(2100, 1.9687246f);
        Assert.Equal(RigS1Minute - 1920, native - 2100);
        RunNative(city, 1440, native);
        Assert.Equal(native, city.State.Minute);
        // The city has meals in flight at the save, so the catch-up drains real work.
        Assert.Contains(city.State.Food!.Residents, m => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving);
        var file = OrdinarySave(binding, save, session, city, native);
        var (loaded, marker) = LoadFile(file, city.State.PopulationKey);
        Assert.Equal(1, marker.Sequence);
        Assert.Equal(native, loaded.Minute);

        // The native load, then one unheld 30 FPS tick before TryStart's hold: 11:02.002.
        double crossed = Native(2100, 1.9687246f + 1f / 30);
        Assert.Equal(2102, Math.Floor(crossed));
        Assert.False(AccountingClock.Compatible(loaded.Minute, crossed)); // The rig refusal before this fix.
        Assert.Equal(LoadClockStart.CatchUp, Activate(loaded, crossed, routes, new Market(), out var restored));
        Assert.False(restored.BeginAccountingObservations(crossed)); // No observation before the crossed minute drains.
        Assert.True(AccountingClock.CompatibleDuringLoadCatchUp(restored.State.Minute, crossed, 2102));

        // Catch up to the end of 11:01 with native held, then the 11:02 epoch begins normally.
        Advance(restored, AccountingClock.PreviousEpochTarget(crossed), 1);
        Assert.True(restored.IsAccountingEpochDrained(crossed));
        Assert.True(restored.BeginAccountingObservations(crossed));
        // The same as a city that never saved: drain 11:01, then begin 11:02.
        Advance(city, AccountingClock.PreviousEpochTarget(crossed), 1);
        Assert.True(city.BeginAccountingObservations(crossed));
        Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(restored.State));

        // One in-game hour later (R2 -> H1 style): save again from the reloaded session and reload in the same minute.
        var reloadedSave = new SaveObject();
        foreach (var pair in file) reloadedSave.modData.Add(pair.Key, pair.Value);
        var next = new SaveEpochBinding(reloadedSave, reloadedSave.modData, marker.Sequence, initializing: true);
        next.CompleteInitialization(); // ContinueLoadCatchUp completes it once drained.
        var nextSession = SaveSession.Open(new Dictionary<string, string> { [SaveStore.StateKey] = file[SaveStore.StateKey] }, loaded.PopulationKey);
        Assert.True(nextSession.BindTo(reloadedSave.modData).Success);
        double hourLater = Native(2160, 3.0113802f);
        RunNative(restored, crossed, hourLater);
        var h1 = OrdinarySave(next, reloadedSave, nextSession, restored, hourLater);
        var (h1State, h1Marker) = LoadFile(h1, loaded.PopulationKey);
        Assert.Equal(2, h1Marker.Sequence);
        Assert.Equal(LoadClockStart.Current, Activate(h1State, hourLater, routes, new Market(), out var h1City));
        Assert.True(h1City.BeginAccountingObservations(hourLater));
        Assert.Equal(restored.State.Citizens.Select(c => c.Identity.Id + "|" + c.Identity.Name + "|" + c.HomeId), h1State.Citizens.Select(c => c.Identity.Id + "|" + c.Identity.Name + "|" + c.HomeId));
    }

    /// <summary>Native time held at each minute fence: drain the epoch, cross into the next, observe, up to <paramref name="to"/>.</summary>
    private static void RunNative(CitySimulator city, double from, double to)
    {
        double clock = from;
        while (true)
        {
            Assert.True(city.BeginAccountingObservations(clock));
            double next = Math.Min(to, Math.Floor(clock) + 1);
            if (next == to) { Advance(city, Math.Max(city.State.Minute, to), 1); return; }
            Advance(city, Math.Max(city.State.Minute, AccountingClock.PreviousEpochTarget(next)), 1);
            clock = next;
        }
    }

    [Fact]
    public void CatchUpLoadWithUnfinishedWorkFromTheSavesOwnEpochStillRefuses()
    {
        var (city, _, routes) = Ready();
        var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 6); Assert.Equal(MealStatus.Serving, meal.Status);
        // A save whose logical clock is late in a minute but whose previous epoch still owes a purchase:
        // the accounting input really is inconsistent, so the one-minute catch-up does not apply.
        double saved = Math.Floor(city.State.Minute) + .97;
        if (Math.Floor(saved) % 60 == 59) saved += 2; // Keep the crossing inside one hour, as in the rig.
        meal.DueAt = meal.ServiceAt = Math.Floor(saved) - 1.5; meal.Deadline = saved + 30;
        city.State.Food.Shops.Single().NextServiceAt = meal.ServiceAt;
        city.State.Minute = saved;
        double crossed = Math.Floor(saved) + 1.01;
        Assert.Equal(LoadClockStart.CatchUp, AccountingClock.ClassifyLoad(saved, crossed));
        Assert.Equal(LoadClockStart.Lost, Activate(city.State, crossed, routes, new Market(), out _));
    }
}

public sealed class LoadClockStartTests
{
    private static double Native(int hourStart, float minuteOfHour) => hourStart + (double)minuteOfHour;

    [Fact]
    public void RigS1MinuteCatchesUpOnlyAcrossOneSameHourMinute()
    {
        double s1 = Native(1920, 1.9687246f);
        Assert.Equal(LoadClockStart.Current, AccountingClock.ClassifyLoad(s1, s1));
        Assert.Equal(LoadClockStart.Current, AccountingClock.ClassifyLoad(s1, Native(1920, 1.99f)));
        Assert.Equal(LoadClockStart.CatchUp, AccountingClock.ClassifyLoad(s1, Native(1920, 2.002f)));
        Assert.Equal(LoadClockStart.CatchUp, AccountingClock.ClassifyLoad(s1, Native(1920, 2.999f)));
        // Two minutes crossed unheld: the intermediate epoch was never observed.
        Assert.Equal(LoadClockStart.Lost, AccountingClock.ClassifyLoad(s1, Native(1920, 3.001f)));
        // H1 (1983.011) loaded inside its own minute.
        Assert.Equal(LoadClockStart.Current, AccountingClock.ClassifyLoad(Native(1980, 3.0113802f), Native(1980, 3.2f)));
    }

    [Fact]
    public void AnHourOrDayBoundaryCrossedUnheldIsStillLost()
    {
        // 08:59.97 -> 09:00.01: native hourly accounting ran without the last Persistent City work.
        Assert.Equal(LoadClockStart.Lost, AccountingClock.ClassifyLoad(Native(1920, 59.97f), 1980.01));
        // 23:59.97 -> next day 00:00.01: daily accounting too.
        Assert.Equal(LoadClockStart.Lost, AccountingClock.ClassifyLoad(2879.97, 2880.01));
        Assert.False(AccountingClock.CompatibleDuringLoadCatchUp(Native(1920, 59.97f), 1980.01, 1980));
    }

    [Fact]
    public void SnapshotsAheadOfOrFarBehindNativeAreStillLost()
    {
        Assert.Equal(LoadClockStart.Lost, AccountingClock.ClassifyLoad(1922.5, 1922.1)); // ahead of native
        Assert.Equal(LoadClockStart.Lost, AccountingClock.ClassifyLoad(1900.2, 1922.1)); // an old cross-epoch save
        Assert.Equal(LoadClockStart.Lost, AccountingClock.ClassifyLoad(double.NaN, 1922.1));
    }

    [Fact]
    public void CatchUpCompatibilityEndsWhenNativeLeavesTheCrossedMinute()
    {
        double s1 = Native(1920, 1.9687246f);
        Assert.True(AccountingClock.CompatibleDuringLoadCatchUp(s1, 1922.002, 1922));
        Assert.True(AccountingClock.CompatibleDuringLoadCatchUp(AccountingClock.PreviousEpochTarget(1922), 1922.5, 1922));
        // A bypassing clock writer moved native on while the catch-up was held: lost as before.
        Assert.False(AccountingClock.CompatibleDuringLoadCatchUp(s1, 1923.001, 1922));
        // Without a catch-up epoch the rule is the ordinary one.
        Assert.False(AccountingClock.CompatibleDuringLoadCatchUp(s1, 1922.002, double.NaN));
        Assert.Equal(AccountingClock.Compatible(1922.3, 1922.4), AccountingClock.CompatibleDuringLoadCatchUp(1922.3, 1922.4, double.NaN));
    }

    /// <summary>Pins the Runtime wiring (Unity-bound, not compiled here) to the tested Core decisions.</summary>
    [Fact]
    public void RuntimeActivationCatchesUpBeforeObservationsAndDefersSavesUntilDrained()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        string Read(string path) => File.ReadAllText(Path.Combine(root!.FullName, path)).Replace("\r\n", "\n");
        string mod = Read("Runtime/PersistentCityMod.cs");
        string complete = mod.Substring(mod.IndexOf("private void CompleteInitialization(Task<PreparedCity> pending)", StringComparison.Ordinal));
        int classify = complete.IndexOf("AccountingClock.ClassifyLoad(state.Minute, loadedNative)", StringComparison.Ordinal);
        int lost = complete.IndexOf("if (loadClock == LoadClockStart.Lost)\n            { StopAccounting(); return; }", StringComparison.Ordinal);
        int drained = complete.IndexOf("simulation.IsAccountingEpochDrained(loadClock == LoadClockStart.CatchUp ? state.Minute : loadedNative)", StringComparison.Ordinal);
        int begin = complete.IndexOf("NativeCityClock.BeginLoadCatchUp(Math.Floor(loadedNative))", StringComparison.Ordinal);
        int market = complete.IndexOf("simulation.AttachFoodMarket(", StringComparison.Ordinal);
        Assert.True(classify > 0 && classify < lost && lost < drained && drained < begin && begin < market);
        Assert.Contains("else saveEpoch.CompleteInitialization();", complete);
        string update = mod.Substring(mod.IndexOf("private void UpdateCity()", StringComparison.Ordinal));
        int catchUp = update.IndexOf("bool catchingUp = ContinueLoadCatchUp();", StringComparison.Ordinal);
        Assert.True(catchUp > 0 && catchUp < update.IndexOf("else if (!BeginAccountingObservations()) return;", StringComparison.Ordinal));
        Assert.True(update.IndexOf("if (catchingUp)\n                {", StringComparison.Ordinal) < update.IndexOf("simulation.RefreshTrafficClosures();", StringComparison.Ordinal));
        Assert.Contains("AdvancePopulation(HoldReplayLimit(Math.Max(AccountingClock.PreviousEpochTarget(target), simulation.State.Minute)));", update);
        string recovery = Read("Runtime/AccountingRecovery.cs");
        string finish = recovery.Substring(recovery.IndexOf("private bool ContinueLoadCatchUp()", StringComparison.Ordinal));
        Assert.True(finish.IndexOf("simulation.IsAccountingEpochDrained(Now())", StringComparison.Ordinal)
            < finish.IndexOf("saveEpoch.CompleteInitialization();", StringComparison.Ordinal));
        string clock = Read("Runtime/NativeCityClock.cs");
        Assert.Contains("AccountingClock.CompatibleDuringLoadCatchUp(logical, native, LoadCatchUpEpoch)", clock);
    }
}
