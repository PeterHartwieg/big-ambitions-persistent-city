using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private readonly List<WalkerCorrection> movementCorrections = new List<WalkerCorrection>();
        private readonly List<PlayerHoldRecord> movementHolds = new List<PlayerHoldRecord>();
        private PlayerHoldReplay? holdReplay;
        private CitySimulator? holdReplayCity;
        private const int MaxHoldReplayCharacters = 8 * 1024 * 1024;
        private GcPauseSampler? gcSampler;
        private static readonly double GcMillisecondsPerTimestamp = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        private long GcFrameBegin()
        {
            var sampler = gcSampler;
            if (sampler == null || !sampler.Running) return 0;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            sampler.CounterFrame(GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            sampler.Observe(now * GcMillisecondsPerTimestamp, GcCount(0), GcCount(1), GcCount(2), GC.GetTotalMemory(false));
            return now;
        }

        private void GcFrameEnd(long started)
        {
            if (started != 0) gcSampler?.ModWork((System.Diagnostics.Stopwatch.GetTimestamp() - started) * GcMillisecondsPerTimestamp);
        }

        private readonly struct GcHeapMark
        {
            public GcHeapMark(long heap, int collections) { Heap = heap; Collections = collections; }
            public long Heap { get; }
            public int Collections { get; }
        }

        private GcHeapMark GcAllocationBegin()
        {
            var sampler = gcSampler;
            if (sampler == null || !sampler.Running) return new GcHeapMark(-1, 0);
            return new GcHeapMark(GcAllocationCounter.Read(), GcAllocationCounter.Collections);
        }

        private void GcAllocationEnd(GcAllocationSegment segment, GcHeapMark mark)
        {
            if (mark.Heap < 0) return;
            gcSampler?.Allocated(segment, mark.Heap, GcAllocationCounter.Read(), GcAllocationCounter.Collections != mark.Collections);
        }

        private static int GcCount(int generation) => generation > GC.MaxGeneration ? 0 : GC.CollectionCount(generation);

        private void GcCommand(string action, string detail)
        {
            if (action == "start" && detail.Length == 0)
            {
                gcSampler ??= new GcPauseSampler();
                string counter = GcAllocationCounter.Probe();
                gcSampler.Start(GC.GetTotalMemory(false), counter);
                Debug.Log("[Persistent City] gc measurement started (diagnostic only, " + GcPauseSampler.PolicyId + ", " + gcSampler.Capacity + " stored frames, allocation counter " + counter + "). Use pc gc report or pc gc stop.");
                return;
            }
            if ((action == "stop" || action == "report") && detail.Length == 0)
            {
                if (gcSampler == null) { Debug.LogWarning("[Persistent City] gc measurement not started. Use pc gc start."); return; }
                if (action == "stop") gcSampler.Stop(GC.GetTotalMemory(false));
                var context = GcContext();
                Debug.Log(GcPauseSampler.ReportPrefix + gcSampler.ReportJson(context));
                Debug.Log(gcSampler.ReportHuman(context));
                gcSampler.Rebaseline();
                return;
            }
            if (action == "population") { GcPopulationCommand(detail); return; }
            Debug.LogWarning("[Persistent City] Unknown command. Use: pc gc start | pc gc report | pc gc stop | pc gc population [<persons>|off] (FT cities only)");
        }

        private GcReportContext GcContext()
        {
            bool incremental = false;
            try { incremental = UnityEngine.Scripting.GarbageCollector.isIncremental; } catch (Exception) { }
            long heap = -1, used = -1;
            try { heap = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong(); used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(); } catch (Exception) { }
            var state = simulation?.State;
            return new GcReportContext(state?.Citizens.Count ?? 0, state?.Visitors.Count ?? 0, simulation?.MeasurementPersonTarget, GC.MaxGeneration, incremental, heap, used);
        }

        private void GcPopulationCommand(string detail)
        {
            var save = SaveGameManager.Current;
            if (!VendorDiagnosticCity(save?.SaveGameName)) { Debug.LogWarning("[Persistent City] gc population refused: measurement mode needs a disposable FT city."); return; }
            if (failed || simulation == null || save == null || !ReferenceEquals(saveIdentity, save))
            { Debug.LogWarning("[Persistent City] gc population refused: no active city."); return; }
            var city = simulation;
            if (detail == "off") city.SetMeasurementPersonTarget(null);
            else if (detail.Length > 0)
            {
                if (!int.TryParse(detail, NumberStyles.None, CultureInfo.InvariantCulture, out int persons) || persons < 1 || persons > PersonTable.MaximumPersons)
                { Debug.LogWarning("[Persistent City] gc population refused: give 1 to " + PersonTable.MaximumPersons + " persons or off."); return; }
                city.SetMeasurementPersonTarget(persons);
            }
            var state = city.State;
            var targets = city.PopulationFillTargets;
            Debug.Log("[Persistent City] MEASUREMENT MODE gc population (diagnostic only; not saved, cleared on reload; existing persons are never removed) "
                + "target=" + (city.MeasurementPersonTarget?.ToString(CultureInfo.InvariantCulture) ?? "off")
                + " reached=" + PersonTable.PersonCount(state).ToString(CultureInfo.InvariantCulture)
                + " residents=" + state.Citizens.Count.ToString(CultureInfo.InvariantCulture) + " visitors=" + state.Visitors.Count.ToString(CultureInfo.InvariantCulture)
                + " portals=" + state.Portals.Count.ToString(CultureInfo.InvariantCulture)
                + " fill=" + (city.PopulationFillAttached ? "attached" : "detached") + " refusals=" + city.PopulationFillRefusals.ToString(CultureInfo.InvariantCulture)
                + " visitor_capacity=" + (targets?.VisitorCapacity.ToString(CultureInfo.InvariantCulture) ?? "unplanned")
                + ". Visitors are added at the next midnight boundary; the fill stops at its own limits and reports the reached count.");
        }

        private void MovementCommand(string action, string detail, string value)
        {
            var telemetry = PhysicalPopulation.Telemetry;
            if (action == "status" && detail.Length == 0)
            {
                Debug.Log("[Persistent City] movement status (read-only)\n" + telemetry.Status(physical?.Followers ?? 0, physical?.CarFollowers ?? 0));
                return;
            }
            if (action == "corrections" && detail == "since"
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sequence) && sequence >= 0)
            {
                movementCorrections.Clear();
                telemetry.Since(sequence, movementCorrections, out bool gap);
                var text = new StringBuilder("[Persistent City] movement corrections since ")
                    .Append(sequence.ToString(CultureInfo.InvariantCulture))
                    .Append(" (read-only)\ngap=").Append(gap ? "true" : "false")
                    .Append("\noldest_sequence=").Append(telemetry.OldestSequence.ToString(CultureInfo.InvariantCulture))
                    .Append("\nlast_sequence=").Append(telemetry.LastSequence.ToString(CultureInfo.InvariantCulture))
                    .Append("\ncount=").Append(movementCorrections.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var correction in movementCorrections) text.Append('\n').Append(WalkerMovementTelemetry.Format(correction));
                movementCorrections.Clear();
                Debug.Log(text.ToString());
                return;
            }
            if (action == "holds" && detail == "since"
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long holdSequence) && holdSequence >= 0)
            {
                movementHolds.Clear();
                bool gap = false;
                if (simulation != null) PlayerContact.Since(simulation.State, holdSequence, movementHolds, out gap);
                var text = new StringBuilder("[Persistent City] movement holds since ")
                    .Append(holdSequence.ToString(CultureInfo.InvariantCulture))
                    .Append(" (read-only)\ngap=").Append(gap ? "true" : "false")
                    .Append("\noldest_sequence=").Append(PlayerContact.Ring.OldestSequence.ToString(CultureInfo.InvariantCulture))
                    .Append("\nlast_sequence=").Append(PlayerContact.Ring.LastSequence.ToString(CultureInfo.InvariantCulture))
                    .Append("\ncity_sequence=").Append(simulation == null ? "none" : simulation.State.PlayerHoldSequence.ToString(CultureInfo.InvariantCulture))
                    .Append("\ncontacts=").Append(PlayerContact.Contacts.ToString(CultureInfo.InvariantCulture))
                    .Append(" reports=").Append(PlayerContact.Reports.ToString(CultureInfo.InvariantCulture))
                    .Append(" applied=").Append(PlayerContact.Applied.ToString(CultureInfo.InvariantCulture))
                    .Append(" refused=").Append(PlayerContact.Refused.ToString(CultureInfo.InvariantCulture))
                    .Append("\ncount=").Append(movementHolds.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var hold in movementHolds) text.Append('\n').Append(PlayerHolds.Format(hold, PlayerHolds.ReadbackLinePrefix));
                movementHolds.Clear();
                Debug.Log(text.ToString());
                return;
            }
            if (action == "replay-holds" && detail.Length > 0 && value.Length == 0) { StartHoldReplay(detail); return; }
            if (action == "replay-holds") { Debug.LogWarning("[Persistent City] hold replay refused: give one file path without spaces, e.g. /tmp/pc-holds.txt"); return; }
            Debug.LogWarning("[Persistent City] Unknown command. Use: pc movement status | pc movement corrections since <seq> | pc movement holds since <seq> | pc movement replay-holds <file>");
        }

        private void StartHoldReplay(string path)
        {
            if (failed || simulation == null) { Debug.LogWarning("[Persistent City] hold replay refused: no active city."); return; }
            if (settings.PhysicalEnabled || physical != null) { Debug.LogWarning("[Persistent City] hold replay refused: set PhysicalEnabled = false and load the starting checkpoint."); return; }
            if (holdReplay != null && ReferenceEquals(holdReplayCity, simulation)) { Debug.LogWarning("[Persistent City] hold replay refused: a replay is already running."); return; }
            List<PlayerHoldRecord> records;
            long start, end;
            string error;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > MaxHoldReplayCharacters) { Debug.LogWarning("[Persistent City] hold replay refused: missing or oversized file " + path); return; }
                if (!PlayerHolds.ReadSegment(File.ReadAllLines(path), out records, out start, out end, out error))
                { Debug.LogWarning("[Persistent City] hold replay refused: " + error); return; }
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is ArgumentException || failure is NotSupportedException)
            { Debug.LogWarning("[Persistent City] hold replay refused: " + failure.Message); return; }
            long city = simulation.State.PlayerHoldSequence;
            if (start != city)
            { Debug.LogWarning("[Persistent City] hold replay refused: the segment starts at " + start + " but the loaded city is at " + city + "; load the starting checkpoint."); return; }
            if (records.Count != 0 && records[0].Minute < simulation.State.Minute)
            { Debug.LogWarning("[Persistent City] hold replay refused: the first hold is stamped before the loaded city's clock."); return; }
            try { holdReplay = new PlayerHoldReplay(records, start, end); }
            catch (ArgumentException failure) { Debug.LogWarning("[Persistent City] hold replay refused: " + failure.Message); return; }
            holdReplayCity = simulation;
            Debug.Log("[Persistent City] hold replay started count=" + records.Count + " start=" + start + " end=" + end);
        }

        private double HoldReplayLimit(double target)
        {
            if (holdReplay == null || simulation == null) return target;
            if (!ReferenceEquals(holdReplayCity, simulation)) { holdReplay = null; holdReplayCity = null; return target; }
            return Math.Max(simulation.State.Minute, holdReplay.Limit(target));
        }

        private void ApplyHoldReplay()
        {
            var replay = holdReplay;
            if (replay == null || simulation == null || !ReferenceEquals(holdReplayCity, simulation)) return;
            int before = replay.Applied;
            bool ok = replay.ApplyDue(simulation);
            for (int i = before; i < replay.Applied; i++) PlayerContact.Publish(replay[i], simulation.State, replay: true);
            if (!ok) { Debug.LogWarning("[Persistent City] hold replay refused: " + replay.Failure); holdReplay = null; holdReplayCity = null; return; }
            if (replay.Done)
            {
                Debug.Log("[Persistent City] hold replay complete applied=" + replay.Applied + " city_sequence=" + simulation.State.PlayerHoldSequence);
                holdReplay = null; holdReplayCity = null;
            }
        }
    }
}
