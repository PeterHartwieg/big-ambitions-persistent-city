using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    private const double RigS1Minute = 1921.9687246084213;

    private static double Native(int hourStart, float minuteOfHour) => hourStart + (double)minuteOfHour;

    private sealed class SaveObject { public readonly Dictionary<string, string> modData = new(StringComparer.Ordinal); }

    private enum HoldFrom
    {
        RloReady,
        FirstUpdate,
        CityLoadCallback,
    }

    private sealed class NativeLoad
    {
        public int Hour; public float Minute;
        public double Now => Hour + (double)Minute;
        public NativeLoad(double saved) { Hour = (int)(Math.Floor(saved / 60) * 60); Minute = (float)(saved - Hour); }

        public double Run(HoldFrom from, int loadingFrames, int waitingFrames, bool currentAtCallback = true)
        {
            var loaded = new object();
            var startup = new StartupClockHold();
            bool held = false;
            if (from == HoldFrom.CityLoadCallback)
            {
                startup.ForSave(currentAtCallback ? loaded : null);
                if (startup.Begin()) held = true;
            }
            for (int frame = 0; frame < loadingFrames + waitingFrames; frame++)
            {
                bool loading = frame < loadingFrames;
                if (!held) Minute += 1f / 30;
                if (from == HoldFrom.CityLoadCallback)
                {
                    startup.ForSave(loaded);
                    if (startup.Begin()) held = true;
                }
                if (from == HoldFrom.FirstUpdate)
                {
                    startup.ForSave(loaded);
                    if (startup.Begin()) held = true;
                }
                if (startup.Wait(loading, 1.0 / 30)) held = false;
            }
            startup.Initializing();
            ReleasedAtEnd = startup.Released;
            return Now;
        }
        public bool ReleasedAtEnd { get; private set; }
    }

    private static Dictionary<string, string> OrdinarySave(SaveEpochBinding binding, SaveObject save, SaveSession session, CitySimulator city, double native)
    {
        var result = SaveEpochCallback.Run(binding, save, save.modData, false, ticket =>
        {
            Assert.True(city.BeginAccountingObservations(native));
            Assert.True(binding.CanPublish(ticket, save, save.modData));
            var written = session.WriteTo(save.modData, city.State);
            return written.Success ? SaveEpochCapture.Written(city.State.Minute, city.State.PopulationKey) : SaveEpochCapture.Refused(written.Error);
        }, reason => throw new Exception("stopped: " + reason), _ => { });
        Assert.Equal(SaveEpochResult.Published, result);
        return new Dictionary<string, string>(save.modData, StringComparer.Ordinal);
    }

    private static (CityState State, SaveEpochMarker Marker) LoadFile(Dictionary<string, string> file, string populationKey)
    {
        Assert.False(AccountingStopStore.IsStopped(file));
        Assert.Null(SaveEpochStore.CheckForStart(file, out var marker));
        var read = SaveSession.Open(new Dictionary<string, string> { [SaveStore.StateKey] = file[SaveStore.StateKey] }, populationKey).InitialReadResult;
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Null(SaveEpochStore.DescribesState(marker, read.SourceSchema, read.State));
        return (read.State!, marker!);
    }

    private static CitySimulator? Activate(CityState state, double native, Walking routes, Market market)
    {
        if (!AccountingClock.Compatible(state.Minute, native)) return null;
        var city = new CitySimulator(state, routes);
        if (!city.IsAccountingEpochDrained(native)) return null;
        city.AttachFoodMarket(market);
        return city;
    }

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

    private static (CitySimulator City, Dictionary<string, string> File, SaveEpochMarker Marker, CityState Loaded, Walking Routes) FreshCityFirstSave(double saveMinute)
    {
        var (city, _, routes) = Ready(60);
        var save = new SaveObject();
        var binding = new SaveEpochBinding(save, save.modData, 0, initializing: true);
        Assert.Equal(SaveEpochResult.Deferred, SaveEpochCallback.Run(binding, save, save.modData, false,
            _ => throw new Exception("captured while initializing"), reason => throw new Exception(reason), _ => { }));
        Assert.Empty(save.modData);
        binding.CompleteInitialization();
        var session = SaveSession.Open(save.modData, city.State.PopulationKey);
        Assert.True(session.BindTo(save.modData).Success);
        RunNative(city, 1440, saveMinute);
        Assert.Equal(saveMinute, city.State.Minute);
        var file = OrdinarySave(binding, save, session, city, saveMinute);
        var (loaded, marker) = LoadFile(file, city.State.PopulationKey);
        Assert.Equal(1, marker.Sequence);
        Assert.Equal(saveMinute, loaded.Minute);
        return (city, file, marker, loaded, routes);
    }

    [Fact]
    public void FirstOrdinarySaveAfterADeferredCreationSaveReloadsWithNativeHeldFromTheFirstStartupAttempt()
    {
        double native = Native(2100, 1.9687246f);
        Assert.Equal(RigS1Minute - 1920, native - 2100);
        var (city, file, marker, loaded, routes) = FreshCityFirstSave(native);
        Assert.Contains(city.State.Food!.Residents, m => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving);

        double before = new NativeLoad(loaded.Minute).Run(HoldFrom.RloReady, 3, 30);
        Assert.True(Math.Floor(before) > Math.Floor(native));
        Assert.Null(Activate(loaded, before, routes, new Market()));
        double firstUpdate = new NativeLoad(loaded.Minute).Run(HoldFrom.FirstUpdate, 3, 30);
        Assert.Equal(2102, Math.Floor(firstUpdate));
        Assert.Null(Activate(loaded, firstUpdate, routes, new Market()));

        Assert.Equal(native, new NativeLoad(loaded.Minute).Run(HoldFrom.CityLoadCallback, 3, 30, currentAtCallback: false));
        double after = new NativeLoad(loaded.Minute).Run(HoldFrom.CityLoadCallback, 3, 30);
        Assert.Equal(native, after);
        var restored = Activate(loaded, after, routes, new Market());
        Assert.NotNull(restored);
        Assert.True(restored!.BeginAccountingObservations(after));
        double later = Native(2100, 3.5f);
        RunNative(city, native, later); RunNative(restored, after, later);
        Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(restored.State));

        var reloadedSave = new SaveObject();
        foreach (var pair in file) reloadedSave.modData.Add(pair.Key, pair.Value);
        var next = new SaveEpochBinding(reloadedSave, reloadedSave.modData, marker.Sequence, initializing: true);
        next.CompleteInitialization();
        var nextSession = SaveSession.Open(new Dictionary<string, string> { [SaveStore.StateKey] = file[SaveStore.StateKey] }, loaded.PopulationKey);
        Assert.True(nextSession.BindTo(reloadedSave.modData).Success);
        double hourLater = Native(2160, 3.0113802f);
        RunNative(restored, later, hourLater);
        var h1 = OrdinarySave(next, reloadedSave, nextSession, restored, hourLater);
        var (h1State, h1Marker) = LoadFile(h1, loaded.PopulationKey);
        Assert.Equal(2, h1Marker.Sequence);
        double h1Native = new NativeLoad(h1State.Minute).Run(HoldFrom.CityLoadCallback, 3, 30);
        var h1City = Activate(h1State, h1Native, routes, new Market());
        Assert.NotNull(h1City);
        Assert.True(h1City!.BeginAccountingObservations(h1Native));
        Assert.Equal(restored.State.Citizens.Select(c => c.Identity.Id + "|" + c.Identity.Name + "|" + c.HomeId),
            h1State.Citizens.Select(c => c.Identity.Id + "|" + c.Identity.Name + "|" + c.HomeId));
    }

    [Fact]
    public void ReleasedStartupHoldStillRefusesALoadThatCrossedAMinute()
    {
        var (_, _, _, loaded, routes) = FreshCityFirstSave(Native(2100, 1.9687246f));
        int waiting = (int)(StartupClockHold.MaximumWaitSeconds * 30) + 2;
        var load = new NativeLoad(loaded.Minute);
        double native = load.Run(HoldFrom.CityLoadCallback, 3, waiting);
        Assert.True(load.ReleasedAtEnd);
        Assert.True(Math.Floor(native) > Math.Floor(loaded.Minute));
        Assert.Null(Activate(loaded, native, routes, new Market()));
    }

    [Fact]
    public void SaveWithUnfinishedWorkFromAnEarlierEpochStillRefuses()
    {
        var (city, _, routes) = Ready();
        var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 6); Assert.Equal(MealStatus.Serving, meal.Status);
        double saved = Math.Floor(city.State.Minute) + .5;
        meal.DueAt = meal.ServiceAt = Math.Floor(saved) - 1.5; meal.Deadline = saved + 30;
        city.State.Food.Shops.Single().NextServiceAt = meal.ServiceAt;
        city.State.Minute = saved;
        Assert.True(AccountingClock.Compatible(saved, saved));
        Assert.Null(Activate(city.State, saved, routes, new Market()));
    }
}

public sealed class StartupClockHoldTests
{
    [Fact]
    public void HoldBeginsOnceAndLoadingScreenTimeIsNotCounted()
    {
        var hold = new StartupClockHold();
        Assert.True(hold.Begin()); Assert.False(hold.Begin());
        for (int i = 0; i < 10000; i++) Assert.False(hold.Wait(loading: true, 1));
        Assert.True(hold.Holding);
    }

    [Fact]
    public void WaitReleasesExactlyOnceAndNotAgainForThisSave()
    {
        var hold = new StartupClockHold();
        Assert.True(hold.Begin());
        Assert.False(hold.Wait(false, StartupClockHold.MaximumWaitSeconds - .5));
        Assert.True(hold.Wait(false, 1));
        Assert.False(hold.Holding); Assert.True(hold.Released);
        Assert.False(hold.Wait(false, 100));
        Assert.False(hold.Begin());
        hold.Reset();
        Assert.True(hold.Begin());
    }

    [Fact]
    public void InitializationTakesOverAndNeverTimesOut()
    {
        var hold = new StartupClockHold();
        Assert.True(hold.Begin());
        hold.Initializing();
        Assert.False(hold.Wait(false, 1000));
        Assert.False(hold.Released);
        Assert.Throws<ArgumentOutOfRangeException>(() => { var h = new StartupClockHold(); h.Begin(); h.Wait(false, double.NaN); });
    }

    [Fact]
    public void HoldBelongsToOneSaveAndASwitchStartsOver()
    {
        object s1 = new object(), s2 = new object();
        var hold = new StartupClockHold();
        Assert.True(hold.ForSave(s1)); Assert.True(hold.Begin());
        Assert.False(hold.ForSave(s1)); Assert.True(hold.Holding);
        Assert.True(hold.Wait(false, StartupClockHold.MaximumWaitSeconds));
        Assert.True(hold.Released);
        Assert.True(hold.ForSave(s2));
        Assert.False(hold.Released);
        Assert.True(hold.Begin());
        Assert.True(hold.ForSave(s1)); Assert.True(hold.Begin());
        Assert.False(hold.Wait(false, StartupClockHold.MaximumWaitSeconds - 1));
        Assert.True(hold.ForSave(s2)); Assert.True(hold.Begin());
        Assert.False(hold.Wait(false, StartupClockHold.MaximumWaitSeconds - 1));
        Assert.True(hold.Holding);
    }

}
