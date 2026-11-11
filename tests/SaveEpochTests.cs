using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// F7b exact save-epoch contract (plan F7 section 3.3 rules 1-10 and the section 3.8 rows owned by F7b).
/// Native Save, GameInstance and the runtime lifecycle are doubles that call the actual Persistence
/// binding, callback, marker reader and save session; <see cref="RuntimeWiringKeepsCaptureOrderAndDetachMarkersFirst"/>
/// pins the real Runtime wiring to the same order. Offline only; no native evidence.
/// </summary>
public sealed class SaveEpochTests
{
    private const double Minute = 600;
    private const string Receipt = "persistentcity:food-sale-v1:intent-1";
    private const string Authority = "persistentcity:food-authority-v1";

    private static CityState City(string key = "source") => Population.Create(
        new[] { new CitizenIdentity { Id = "resident", Name = "Resident", Age = 35, Persona = Persona.Commuter } },
        new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } }, key, "0.40.4", 42, Minute);

    /// <summary>A native GameInstance double: one object owning one modData dictionary.</summary>
    private sealed class GameInstance { public readonly Dictionary<string, string> modData = new(StringComparer.Ordinal); }

    /// <summary>Mirrors PopulationRuntime's F7b wiring (SaveEpochRuntime.cs, AccountingRecovery.StopAccounting, Detach).</summary>
    private sealed class Runtime
    {
        public GameInstance? Current;
        public SaveEpochBinding? Epoch;
        public SaveSession? Session;
        public CityState State = City();
        public bool Failed, StopPending, ReleaseThrows, CaptureThrowsThroughStop;
        public int Units, Stops, Captures;
        public readonly List<string> Reports = new();
        public Func<string?>? BeforeWrite;
        public Action? InsideCapture;

        /// <summary>Mirrors TryStart (bind and subscribe while initializing) and CompleteInitialization.</summary>
        public void Attach(GameInstance save, long sequence = 0, bool initializing = false)
        {
            Current = save;
            Session = SaveSession.Open(save.modData, State.PopulationKey);
            Assert.True(Session.BindTo(save.modData).Success);
            Epoch = new SaveEpochBinding(save, save.modData, sequence, initializing: true);
            if (!initializing) Epoch.CompleteInitialization();
        }

        public SaveEpochResult Save()
        {
            var result = SaveEpochCallback.Run(Epoch, Current, Current?.modData, Units > 0, Capture, StopAfterFailure, Reports.Add);
            if (result == SaveEpochResult.Reentrant) StopPending = true;
            if (StopPending && Units == 0 && Epoch?.Saving != true) { StopPending = false; StopAfterFailure("deferred"); }
            return result;
        }

        private SaveEpochCapture Capture(SaveEpochTicket ticket)
        {
            Captures++;
            InsideCapture?.Invoke();
            if (CaptureThrowsThroughStop) StopAccounting(); // BeginAccountingObservations failing inside the callback
            string? refusal = BeforeWrite?.Invoke();
            if (refusal != null) return SaveEpochCapture.Refused(refusal);
            if (Epoch?.CanPublish(ticket, Current, Current?.modData) != true) return SaveEpochCapture.Refused("stale");
            var written = Session!.WriteTo(Current!.modData, State);
            return written.Success ? SaveEpochCapture.Written(State.Minute, State.PopulationKey) : SaveEpochCapture.Refused(written.Error);
        }

        private void StopAfterFailure(string reason) { if (!Failed) StopAccounting(); }

        public void StopAccounting()
        {
            Stops++;
            AccountingStopStore.Stop(Current!.modData);
            try { if (ReleaseThrows) throw new InvalidOperationException("ownership release failed"); }
            finally { Fail(); }
        }

        public void Fail() { Failed = true; Detach(); }

        public void Detach()
        {
            var epoch = Epoch; Epoch = null;
            epoch?.Detach(Current);
        }

        public void Unit(Action inside) { Units++; try { inside(); } finally { Units--; if (StopPending && Units == 0) { StopPending = false; StopAfterFailure("deferred"); } } }
    }

    /// <summary>Native SaveGameManager.Save double: runs onSaveGame, then serializes modData (the "file").</summary>
    private static Dictionary<string, string> NativeSave(GameInstance save, Runtime? subscriber)
    {
        subscriber?.Save(); // A plain Action: any exception here would abort native Save.
        return new Dictionary<string, string>(save.modData, StringComparer.Ordinal);
    }

    private static GameInstance Load(Dictionary<string, string> file)
    {
        var save = new GameInstance();
        foreach (var pair in file) save.modData.Add(pair.Key, pair.Value);
        return save;
    }

    /// <summary>F7b start decision, composed of the functions TryStart, the worker and CompleteInitialization call.</summary>
    private static string? F7bStart(IDictionary<string, string> data, string populationKey, double nativeMinute)
    {
        if (AccountingStopStore.IsStopped(data)) return "accounting stop";
        string? refusal = SaveEpochStore.CheckForStart(data, out var marker);
        if (refusal != null) return refusal;
        var read = ReadPopulation(data, populationKey);
        if (read.Status != SaveReadStatus.Loaded && read.Status != SaveReadStatus.Missing) return read.Error;
        refusal = SaveEpochStore.DescribesState(marker, read.SourceSchema, read.State);
        if (refusal != null) return refusal;
        return read.State != null && !AccountingClock.Compatible(read.State.Minute, nativeMinute) ? "clock" : null;
    }

    /// <summary>Frozen copy of the base (writer-25, pre-F7b) startup refusal: stop key presence, strict read, minute check.
    /// The base ignores persistentcity:save-epoch_v1.</summary>
    private static string? BaseWriter25Start(IDictionary<string, string> data, string populationKey, double nativeMinute)
    {
        if (data.ContainsKey("persistentcity:accounting-stop_v1")) return "accounting stop";
        var read = ReadPopulation(data, populationKey);
        if (read.Status != SaveReadStatus.Loaded && read.Status != SaveReadStatus.Missing) return read.Error;
        return read.State != null && !AccountingClock.Compatible(read.State.Minute, nativeMinute) ? "clock" : null;
    }

    private static SaveReadResult ReadPopulation(IDictionary<string, string> data, string populationKey)
    {
        var snapshot = new Dictionary<string, string>();
        if (data.TryGetValue(SaveStore.StateKey, out var payload)) snapshot.Add(SaveStore.StateKey, payload);
        return SaveSession.Open(snapshot, populationKey).InitialReadResult;
    }

    private static (GameInstance Save, Runtime Runtime) Attached(bool withLegacyPayload = true)
    {
        var save = new GameInstance(); var runtime = new Runtime();
        if (withLegacyPayload) save.modData[SaveStore.StateKey] = SaveCodec.Write(runtime.State);
        save.modData[Receipt] = "Confirmed";
        save.modData[Authority] = "manifest";
        runtime.Attach(save);
        return (save, runtime);
    }

    private static void AssertRefusedByEveryBuild(Dictionary<string, string> file, string populationKey)
    {
        var before = new Dictionary<string, string>(file);
        Assert.NotNull(F7bStart(file, populationKey, Minute + .5));
        Assert.NotNull(BaseWriter25Start(file, populationKey, Minute + .5));
        Assert.Equal(before, file); // Refusal preserves every key.
    }

    // Rules 1 and 2: capture inside the callback after the observations; payload first, marker after it.
    [Fact]
    public void NormalSavePublishesPayloadThenMarkerDescribingIt()
    {
        var (save, runtime) = Attached();
        var file = NativeSave(save, runtime);
        Assert.Equal(1, runtime.Captures);
        Assert.True(SaveEpochStore.TryRead(file[SaveEpochStore.Key], out var marker, out _));
        Assert.Equal(SaveEpochMarkerKind.Success, marker!.Kind);
        Assert.Equal(1, marker.Sequence);
        Assert.Equal(CityState.CurrentSchema, marker.Schema);
        Assert.Equal(Minute, marker.Minute);
        Assert.Equal(runtime.State.PopulationKey, marker.PopulationKey);
        Assert.Equal(new KeyValuePair<string, int>(SaveStore.StateKey, file[SaveStore.StateKey].Length), Assert.Single(marker.Payloads));
        Assert.False(file.ContainsKey(AccountingStopStore.Key));
        Assert.Null(F7bStart(file, runtime.State.PopulationKey, Minute + .5));
        Assert.Null(BaseWriter25Start(file, runtime.State.PopulationKey, Minute + .5)); // Cross-build: success loads in base.
        // Rule 8: receipts and manifest are untouched and not copied into the payload or marker.
        Assert.Equal("Confirmed", file[Receipt]); Assert.Equal("manifest", file[Authority]);
        Assert.DoesNotContain("intent-1", file[SaveStore.StateKey]); Assert.DoesNotContain("intent-1", file[SaveEpochStore.Key]);
    }

    // Writer-25 bytes unchanged: the epoch path publishes exactly the base SaveSession/SaveCodec string.
    [Fact]
    public void Writer25PayloadBytesAreUnchangedByTheEpochPath()
    {
        var (save, runtime) = Attached();
        runtime.State.Citizens[0].CompletedJourneys = 7;
        var file = NativeSave(save, runtime);
        var baseData = new Dictionary<string, string> { [SaveStore.StateKey] = SaveCodec.Write(City()) };
        Assert.True(SaveSession.Open(baseData, runtime.State.PopulationKey).WriteTo(baseData, runtime.State).Success);
        Assert.Equal(baseData[SaveStore.StateKey], file[SaveStore.StateKey]);
        Assert.Equal(SaveCodec.Write(runtime.State), file[SaveStore.StateKey]);
        Assert.Equal(CityState.CurrentSchema, ReadPopulation(file, runtime.State.PopulationKey).SourceSchema);
    }

    // Rule 7 / plan row: same-minute double save.
    [Fact]
    public void SameMinuteDoubleSaveKeepsEachFileCoherentAndMinuteCheckAloneCannotSeeStalePairing()
    {
        var (save, runtime) = Attached();
        var first = NativeSave(save, runtime);
        save.modData[Receipt] = "Confirmed-after-second-sale"; // Native progress inside the same minute.
        runtime.State.Citizens[0].CompletedJourneys = 3;
        var second = NativeSave(save, runtime);
        Assert.True(SaveEpochStore.TryRead(first[SaveEpochStore.Key], out var one, out _));
        Assert.True(SaveEpochStore.TryRead(second[SaveEpochStore.Key], out var two, out _));
        Assert.Equal(1, one!.Sequence); Assert.Equal(2, two!.Sequence);
        Assert.NotEqual(first[SaveStore.StateKey], second[SaveStore.StateKey]);
        Assert.Null(F7bStart(first, runtime.State.PopulationKey, Minute + .5));
        Assert.Null(F7bStart(second, runtime.State.PopulationKey, Minute + .5));
        Assert.Equal(3, ReadPopulation(second, runtime.State.PopulationKey).State!.Citizens[0].CompletedJourneys);
        // The measured gap: the minute-granular check alone accepts a stale same-minute payload.
        Assert.True(AccountingClock.Compatible(ReadPopulation(first, runtime.State.PopulationKey).State!.Minute, Minute + .5));
    }

    // Rule 6 / section 3.8: failed write followed by native progress; refused on load in F7b and in the base build.
    [Fact]
    public void FailedWriteThenNativeProgressIsRefusedByEveryBuildAndKeepsThePreviousPayload()
    {
        var (save, runtime) = Attached();
        var good = NativeSave(save, runtime);
        string previous = save.modData[SaveStore.StateKey];
        runtime.BeforeWrite = () => "encode failed";
        var failedFile = NativeSave(save, runtime);
        Assert.True(runtime.Failed); Assert.Null(runtime.Epoch); Assert.Equal(1, runtime.Stops);
        Assert.Same(previous, failedFile[SaveStore.StateKey]); // Never deleted or replaced.
        Assert.Equal(AccountingStopStore.SaveEpochFailed, failedFile[AccountingStopStore.Key]);
        Assert.Equal(SaveEpochStore.Failed(2), failedFile[SaveEpochStore.Key]);
        save.modData[Receipt] = "Confirmed-later"; save.modData["native-money"] = "1234";
        var later = NativeSave(save, null); // PC is detached; native saves continue.
        Assert.Equal(SaveEpochStore.Failed(2), later[SaveEpochStore.Key]);
        AssertRefusedByEveryBuild(later, runtime.State.PopulationKey);
        Assert.Null(F7bStart(good, runtime.State.PopulationKey, Minute + .5)); // Recovery: the earlier unmarked save.
    }

    // Rule 9 / section 3.8: runtime Fail outside a save, then a same-minute native save.
    [Fact]
    public void RuntimeFailOutsideSaveThenSameMinuteNativeSaveIsRefused()
    {
        var (save, runtime) = Attached();
        NativeSave(save, runtime);
        runtime.Fail();
        Assert.Equal(AccountingStopStore.DetachedAfterSave, save.modData[AccountingStopStore.Key]);
        save.modData[Receipt] = "Confirmed-after-failure";
        var file = NativeSave(save, runtime); // Unsubscribed in the runtime; the double proves nothing is published.
        Assert.Equal(SaveEpochStore.Failed(1), file[SaveEpochStore.Key]);
        AssertRefusedByEveryBuild(file, runtime.State.PopulationKey);
    }

    // Rule 9 / section 3.8: Shutdown (mod unload, component destroy, onGameUnloaded) while the same GameInstance stays current.
    [Fact]
    public void ShutdownWithSameGameInstanceThenSameMinuteSaveIsRefused()
    {
        var (save, runtime) = Attached();
        runtime.Detach(); // Shutdown -> Detach with SaveGameManager.Current unchanged.
        var file = NativeSave(save, runtime);
        Assert.Equal(SaveEpochStore.Failed(0), file[SaveEpochStore.Key]);
        AssertRefusedByEveryBuild(file, runtime.State.PopulationKey);
    }

    // Rule 9: a normal save, then quit to menu (onGameUnloaded), then reload of that save loads normally.
    [Fact]
    public void NormalSaveUnloadReloadLoadsNormallyWithoutStopMarker()
    {
        var (save, runtime) = Attached();
        var file = NativeSave(save, runtime);
        runtime.Detach(); // onGameUnloaded -> Shutdown writes into the in-memory modData only.
        Assert.True(AccountingStopStore.IsStopped(save.modData));
        Assert.False(file.ContainsKey(AccountingStopStore.Key));
        var reloaded = Load(file);
        Assert.Null(F7bStart(reloaded.modData, runtime.State.PopulationKey, Minute + .5));
        Assert.Null(BaseWriter25Start(reloaded.modData, runtime.State.PopulationKey, Minute + .5));
        // The reloaded session continues the sequence from the marker.
        Assert.Null(SaveEpochStore.CheckForStart(reloaded.modData, out var marker));
        var next = new Runtime(); next.Attach(reloaded, marker!.Sequence);
        var after = NativeSave(reloaded, next);
        Assert.True(SaveEpochStore.TryRead(after[SaveEpochStore.Key], out var continued, out _));
        Assert.Equal(2, continued!.Sequence);
    }

    // Rule 9 exemption: detach after a verified save-object replacement writes nothing to either object.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetachAfterVerifiedReplacementWritesNothing(bool currentNull)
    {
        var (save, runtime) = Attached();
        NativeSave(save, runtime);
        var oldBefore = new Dictionary<string, string>(save.modData);
        var replacement = new GameInstance(); replacement.modData["other"] = "city";
        runtime.Current = currentNull ? null : replacement;
        runtime.Detach();
        Assert.Equal(oldBefore, save.modData);
        Assert.Equal(new Dictionary<string, string> { ["other"] = "city" }, replacement.modData);
        // Nothing is written after detach, even if a stale subscriber is invoked.
        runtime.Current = save;
        Assert.Equal(SaveEpochResult.NotBound, runtime.Save());
        Assert.Equal(oldBefore, save.modData);
    }

    // Section 3.3 rule 6: ownership release throwing inside the save callback.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnershipReleaseThrowingInsideSaveCallbackIsContainedWithMarkersWritten(bool throughObservation)
    {
        var (save, runtime) = Attached();
        runtime.ReleaseThrows = true;
        if (throughObservation) runtime.CaptureThrowsThroughStop = true; // StopAccounting from BeginAccountingObservations
        else runtime.BeforeWrite = () => "validation failed";            // StopAccounting from the epoch failure path
        Dictionary<string, string>? file = null;
        var error = Record.Exception(() => file = NativeSave(save, runtime));
        Assert.Null(error); // Native Save completes.
        Assert.NotNull(file);
        Assert.True(runtime.Failed); Assert.Null(runtime.Epoch);
        Assert.True(AccountingStopStore.IsStopped(file!));
        Assert.StartsWith(SaveEpochStore.FailedPrefix, file![SaveEpochStore.Key]);
        Assert.Contains(runtime.Reports, r => r.Contains("contained") || r.Contains("ownership release failed"));
        AssertRefusedByEveryBuild(file, runtime.State.PopulationKey);
    }

    // Rule 5: re-entrant callback while a PC unit (event, checkout, initialization) is on the stack.
    [Fact]
    public void ReentrantCallbackDuringUnitRefusesAndStopsAfterTheUnit()
    {
        var (save, runtime) = Attached();
        Dictionary<string, string>? file = null;
        runtime.Unit(() =>
        {
            file = NativeSave(save, runtime);
            Assert.False(runtime.Failed); // Not torn down mid-unit.
            Assert.Equal(0, runtime.Captures); // No mid-unit snapshot.
        });
        Assert.True(runtime.Failed); Assert.Equal(1, runtime.Stops);
        Assert.Equal(AccountingStopStore.SaveEpochFailed, file![AccountingStopStore.Key]);
        AssertRefusedByEveryBuild(file, runtime.State.PopulationKey);
    }

    // Rule 5: a native save while initialization steps are on the stack (a PC unit) still refuses with both
    // markers, takes no snapshot and returns normally.
    [Fact]
    public void SaveDuringInitializationOnTheStackRefusesWithBothMarkersAndKeepsPayload()
    {
        var save = new GameInstance(); var runtime = new Runtime();
        save.modData[SaveStore.StateKey] = SaveCodec.Write(runtime.State);
        save.modData[Receipt] = "Confirmed";
        string previous = save.modData[SaveStore.StateKey];
        runtime.Attach(save, initializing: true);
        Dictionary<string, string>? file = null;
        Exception? error = null;
        runtime.Unit(() => error = Record.Exception(() => file = NativeSave(save, runtime)));
        Assert.Null(error); // Native Save completes normally.
        Assert.Equal(0, runtime.Captures); // No snapshot of a half-initialized city.
        Assert.Same(previous, file![SaveStore.StateKey]);
        Assert.Equal(AccountingStopStore.SaveEpochFailed, file[AccountingStopStore.Key]);
        Assert.Equal(SaveEpochStore.Failed(1), file[SaveEpochStore.Key]);
        Assert.Equal("Confirmed", file[Receipt]);
        Assert.True(runtime.Failed); Assert.Equal(1, runtime.Stops);
        AssertRefusedByEveryBuild(file, runtime.State.PopulationKey);
    }

    // Rig mac-creation-1 (10 Nov 2026): a new city's native creation save arrives while the initialization
    // worker is running. It is deferred: no key is written or removed, the epoch is not poisoned, nothing stops,
    // the sequence the worker read still matches, and the next ordinary save publishes the normal payload.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveDuringPendingInitializationIsDeferredAndTheCityStillAttaches(bool loadedPayload)
    {
        var save = new GameInstance(); var runtime = new Runtime();
        if (loadedPayload) save.modData[SaveStore.StateKey] = SaveCodec.Write(runtime.State);
        save.modData[Receipt] = "Confirmed";
        var before = new Dictionary<string, string>(save.modData, StringComparer.Ordinal);
        runtime.Attach(save, initializing: true);
        long workerSequence = runtime.Epoch!.Sequence;

        Dictionary<string, string>? file = null;
        var error = Record.Exception(() => file = NativeSave(save, runtime));
        Assert.Null(error);
        Assert.Equal(before, file); // Creation save carries no PC state, partial or otherwise.
        Assert.Equal(before, save.modData);
        Assert.Equal(0, runtime.Captures);
        Assert.False(runtime.Failed); Assert.Equal(0, runtime.Stops); Assert.False(runtime.StopPending);
        Assert.NotNull(runtime.Epoch);
        Assert.Equal(1, runtime.Epoch!.Deferred); Assert.Equal(0, runtime.Epoch.Refused);
        Assert.Equal(workerSequence, runtime.Epoch.Sequence); // CompleteInitialization's epoch check still passes.
        Assert.False(runtime.Epoch.Saving);
        Assert.Contains(runtime.Reports, r => r.StartsWith("Persistent City skipped the save during initialization", StringComparison.Ordinal));

        runtime.Epoch.CompleteInitialization();
        file = NativeSave(save, runtime);
        Assert.Equal(1, runtime.Captures);
        Assert.False(file.ContainsKey(AccountingStopStore.Key));
        Assert.True(SaveEpochStore.TryRead(file[SaveEpochStore.Key], out var marker, out _));
        Assert.Equal(SaveEpochMarkerKind.Success, marker!.Kind);
        Assert.Equal(workerSequence + 1, marker.Sequence);
        var loaded = SaveCodec.Read(file[SaveStore.StateKey]);
        Assert.Equal(runtime.State.PopulationKey, loaded.State!.PopulationKey);
    }

    // The other entry refusals keep refusing while initializing: replaced dictionary and an earlier failure.
    [Fact]
    public void OtherRefusalsStillRefuseWhileInitializing()
    {
        var save = new GameInstance();
        var replaced = new SaveEpochBinding(save, save.modData, 0, initializing: true);
        var ticket = replaced.Begin(save, new Dictionary<string, string>(), unitOnStack: false)!;
        Assert.Equal("The save object's mod data dictionary was replaced.", ticket.Refusal);
        Assert.Null(ticket.Deferral); Assert.Equal(0, replaced.Deferred);

        var save2 = new GameInstance();
        var nested = new SaveEpochBinding(save2, save2.modData, 0, initializing: true);
        var unit = nested.Begin(save2, save2.modData, unitOnStack: true)!; // poisons the epoch
        Assert.Equal("A Persistent City unit was executing when the native save started.", unit.Refusal);
        var later = nested.Begin(save2, save2.modData, unitOnStack: false)!;
        Assert.Equal("An earlier save epoch of this session failed.", later.Refusal);
        Assert.Null(later.Deferral); Assert.Equal(0, nested.Deferred);
        Assert.StartsWith(SaveEpochStore.FailedPrefix, save2.modData[SaveEpochStore.Key]);
    }

    // Rules 5 and 9: an initialization that fails or is abandoned writes nothing (load refusals preserve
    // every key); after completion the same session publishes normally.
    [Fact]
    public void InitializingBindingDetachWritesNothingAndCompletionAllowsPublication()
    {
        var save = new GameInstance(); save.modData[SaveStore.StateKey] = "payload";
        var before = new Dictionary<string, string>(save.modData);
        var abandoned = new SaveEpochBinding(save, save.modData, 3, initializing: true);
        Assert.False(abandoned.Detach(save));
        Assert.Equal(before, save.modData);
        Assert.Throws<InvalidOperationException>(() => abandoned.CompleteInitialization());

        var (live, runtime) = Attached();
        Assert.Equal(SaveEpochResult.Published, runtime.Save());
        Assert.False(live.modData.ContainsKey(AccountingStopStore.Key));
    }

    // Rules 3 and 5: a nested save inside a callback refuses, and the outer stale-sequence snapshot is never published.
    [Fact]
    public void NestedCallbackRefusesAndOuterStaleSequenceIsNotPublished()
    {
        var (save, runtime) = Attached();
        string previous = save.modData[SaveStore.StateKey];
        SaveEpochResult inner = SaveEpochResult.NotBound;
        runtime.InsideCapture = () => { runtime.InsideCapture = null; inner = runtime.Save(); };
        var outer = runtime.Save();
        Assert.Equal(SaveEpochResult.Reentrant, inner);
        Assert.Equal(SaveEpochResult.Refused, outer);
        Assert.Equal(1, runtime.Stops);
        Assert.StartsWith(SaveEpochStore.FailedPrefix, save.modData[SaveEpochStore.Key]);
        Assert.Same(previous, save.modData[SaveStore.StateKey]); // The stale outer capture never replaced the payload.
        AssertRefusedByEveryBuild(new Dictionary<string, string>(save.modData), runtime.State.PopulationKey);
    }

    // Rule 3: GameInstance binding and dictionary binding.
    [Fact]
    public void PublicationRefusesAnotherGameInstanceOrReplacedDictionary()
    {
        var save = new GameInstance(); save.modData[SaveStore.StateKey] = "payload";
        var binding = new SaveEpochBinding(save, save.modData, 4);
        Assert.Null(binding.Begin(new GameInstance(), save.modData, false));
        Assert.False(save.modData.ContainsKey(SaveEpochStore.Key));
        var ticket = binding.Begin(save, save.modData, false)!;
        Assert.Equal(5, ticket.Sequence);
        Assert.False(binding.Publish(ticket, new GameInstance(), save.modData, 25, Minute, "source"));
        Assert.Equal(SaveEpochStore.Failed(5), save.modData[SaveEpochStore.Key]);
        binding.End(ticket);
        // Poisoned: a later callback never publishes a success marker over the failure.
        var later = binding.Begin(save, save.modData, false)!;
        Assert.NotNull(later.Refusal); Assert.False(later.Reentrant);
        Assert.StartsWith(SaveEpochStore.FailedPrefix, save.modData[SaveEpochStore.Key]);

        var other = new GameInstance(); other.modData[SaveStore.StateKey] = "payload";
        var second = new SaveEpochBinding(other, other.modData, 0);
        var replaced = new Dictionary<string, string>(other.modData);
        var refused = second.Begin(other, replaced, false)!;
        Assert.Contains("dictionary", refused.Refusal);
        Assert.True(AccountingStopStore.IsStopped(other.modData));
    }

    // Rule 4: no concurrent traversal; the capture runs synchronously on the callback thread.
    [Fact]
    public void CaptureRunsSynchronouslyOnTheCallbackThread()
    {
        var (save, runtime) = Attached();
        int thread = Environment.CurrentManagedThreadId, seen = -1;
        runtime.InsideCapture = () => seen = Environment.CurrentManagedThreadId;
        Assert.Equal(SaveEpochResult.Published, runtime.Save());
        Assert.Equal(thread, seen);
    }

    // Rule 6: marker/payload mismatch refuses and preserves.
    [Theory]
    [InlineData("chars")]
    [InlineData("minute")]
    [InlineData("population")]
    [InlineData("schema")]
    [InlineData("missing-payload")]
    [InlineData("unlisted-extra-key")]
    public void MarkerPayloadMismatchRefusesAndPreservesEveryKey(string defect)
    {
        var (save, runtime) = Attached();
        var file = NativeSave(save, runtime);
        string marker = file[SaveEpochStore.Key];
        int chars = file[SaveStore.StateKey].Length;
        switch (defect)
        {
            case "chars": file[SaveEpochStore.Key] = marker.Replace("payload=" + chars + " ", "payload=" + (chars + 1) + " "); break;
            case "minute": file[SaveEpochStore.Key] = marker.Replace("minute=600", "minute=601"); break;
            case "population": file[SaveEpochStore.Key] = marker.Replace("population=source", "population=other"); break;
            case "schema": file[SaveEpochStore.Key] = marker.Replace("schema=" + CityState.CurrentSchema, "schema=" + (CityState.CurrentSchema - 1)); break;
            case "missing-payload": file.Remove(SaveStore.StateKey); break;
            case "unlisted-extra-key":
                file[SaveEpochStore.Key] = marker + "\npayload=3 persistentcity:zz-future_v1"; break;
        }
        Assert.NotEqual(defect == "missing-payload" ? "" : marker, file.GetValueOrDefault(SaveEpochStore.Key));
        var before = new Dictionary<string, string>(file);
        Assert.NotNull(F7bStart(file, runtime.State.PopulationKey, Minute + .5));
        Assert.Equal(before, file);
    }

    // Rule 6 strict reader: unknown versions and non-canonical values refuse.
    [Theory]
    [InlineData("v2\nsequence=1", "Unsupported")]
    [InlineData("v10", "Unsupported")]
    [InlineData("", "Malformed")]
    [InlineData("V1", "Malformed")]
    [InlineData("failed:", "Malformed")]
    [InlineData("failed:01", "Malformed")]
    [InlineData("failed:-1", "Malformed")]
    [InlineData("failed:1 ", "Malformed")]
    public void StrictReaderRefusesUnknownVersionsAndMalformedValues(string text, string expected)
    {
        Assert.False(SaveEpochStore.TryRead(text, out var marker, out string error));
        Assert.Null(marker);
        Assert.Contains(expected, error);
        var data = new Dictionary<string, string> { [SaveEpochStore.Key] = text, [SaveStore.StateKey] = "x" };
        var before = new Dictionary<string, string>(data);
        Assert.NotNull(SaveEpochStore.CheckForStart(data, out _));
        Assert.Equal(before, data);
    }

    [Theory]
    [InlineData("sequence=01")]
    [InlineData("sequence=0")]
    [InlineData("sequence=+1")]
    [InlineData("schema=0")]
    [InlineData("minute=600.0")]
    [InlineData("minute=NaN")]
    [InlineData("minute=-1")]
    [InlineData("population=")]
    [InlineData("payload=01 persistentcity:state_v1")]
    [InlineData("payload=5 persistentcity:other_v1")]
    [InlineData("trailing-newline")]
    [InlineData("duplicate-payload")]
    [InlineData("reordered")]
    public void StrictReaderRefusesNonCanonicalSuccessMarkers(string defect)
    {
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = "12345" };
        string good = SaveEpochStore.Success(1, 25, Minute, "source", data);
        Assert.True(SaveEpochStore.TryRead(good, out _, out _));
        string[] lines = good.Split('\n');
        string bad = defect switch
        {
            "trailing-newline" => good + "\n",
            "duplicate-payload" => good + "\n" + lines[5],
            "reordered" => string.Join("\n", lines[0], lines[2], lines[1], lines[3], lines[4], lines[5]),
            _ => string.Join("\n", lines.Select(line => line.Split('=')[0] == defect.Split('=')[0] ? defect : line)),
        };
        Assert.False(SaveEpochStore.TryRead(bad, out _, out _));
    }

    // Rule 6: a payload with no marker (every save before F7b) loads under today's rules, including the minute check.
    [Fact]
    public void LegacyNoMarkerPayloadLoadsUnderExistingRules()
    {
        var city = City();
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = SaveCodec.Write(city), [Receipt] = "Pending" };
        Assert.Null(SaveEpochStore.CheckForStart(data, out var marker));
        Assert.Null(marker);
        Assert.Null(F7bStart(data, city.PopulationKey, Minute + .5));
        Assert.Equal("clock", F7bStart(data, city.PopulationKey, Minute + 61)); // Existing Compatible refusal kept.
        Assert.Null(F7bStart(new Dictionary<string, string>(), city.PopulationKey, Minute)); // Fresh city.
        var orphan = new Dictionary<string, string> { [SaveEpochStore.Key] = SaveEpochStore.Success(1, 25, Minute, "source",
            new Dictionary<string, string> { [SaveStore.StateKey] = "x" }) };
        Assert.NotNull(F7bStart(orphan, city.PopulationKey, Minute)); // Marker without its payload.
    }

    // Rule 10: the native relocation path copies modData without onSaveGame; the pair stays coherent.
    [Fact]
    public void RelocationCopyCarriesTheMatchingPair()
    {
        var (save, runtime) = Attached();
        NativeSave(save, runtime);
        var copy = Load(new Dictionary<string, string>(save.modData)); // CreateSaveSnapshot(Current), no callback.
        Assert.Equal(save.modData[SaveEpochStore.Key], copy.modData[SaveEpochStore.Key]);
        Assert.Null(F7bStart(copy.modData, runtime.State.PopulationKey, Minute + .5));
    }

    // Section 3.8 receipt row: Pending/Conflict receipts are never retried, cleared or rewritten by any epoch path.
    [Theory]
    [InlineData("Pending")]
    [InlineData("Conflict")]
    public void ReceiptsAreUnchangedOnEverySaveEpochPath(string receiptState)
    {
        foreach (var arm in new[] { "publish", "fail", "reentrant", "detach" })
        {
            var (save, runtime) = Attached();
            save.modData[Receipt] = receiptState;
            string receipt = save.modData[Receipt], manifest = save.modData[Authority];
            if (arm == "fail") runtime.BeforeWrite = () => "fail";
            if (arm == "reentrant") runtime.Unit(() => NativeSave(save, runtime));
            else if (arm == "detach") runtime.Detach();
            else NativeSave(save, runtime);
            Assert.Same(receipt, save.modData[Receipt]);
            Assert.Same(manifest, save.modData[Authority]);
        }
    }

}
