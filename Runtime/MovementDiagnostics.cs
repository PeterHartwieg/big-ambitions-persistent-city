using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>F2c read-only movement console lines: `pc movement status` and `pc movement corrections since &lt;seq&gt;`,
    /// for walkers and (F2d) cars. They read Runtime telemetry only (PhysicalPopulation.Telemetry) and write nothing.
    /// F2e adds `pc movement holds since &lt;seq&gt;` (read-only, the last 256 applied player holds) and
    /// `pc movement replay-holds &lt;file&gt;`, the in-game E2 replay of a recorded hold segment into a city loaded from the
    /// segment's starting checkpoint with bodies off.</summary>
    public sealed partial class PopulationRuntime
    {
        private readonly List<WalkerCorrection> movementCorrections = new List<WalkerCorrection>();
        private readonly List<PlayerHoldRecord> movementHolds = new List<PlayerHoldRecord>();
        private PlayerHoldReplay? holdReplay;
        private CitySimulator? holdReplayCity;
        private const int MaxHoldReplayCharacters = 8 * 1024 * 1024;

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
                // Readback copies carry their own prefix, so they never enter the original hold stream.
                foreach (var hold in movementHolds) text.Append('\n').Append(PlayerHolds.Format(hold, PlayerHolds.ReadbackLinePrefix));
                movementHolds.Clear();
                Debug.Log(text.ToString());
                return;
            }
            if (action == "replay-holds" && detail.Length > 0 && value.Length == 0) { StartHoldReplay(detail); return; }
            if (action == "replay-holds") { Debug.LogWarning("[Persistent City] hold replay refused: give one file path without spaces, e.g. /tmp/pc-holds.txt"); return; }
            Debug.LogWarning("[Persistent City] Unknown command. Use: pc movement status | pc movement corrections since <seq> | pc movement holds since <seq> | pc movement replay-holds <file>");
        }

        /// <summary>
        /// Starts the in-game E2 replay (rig check 8). The file holds Player.log hold lines and exactly one required line
        /// <c>segment start=&lt;seq&gt; end=&lt;seq&gt;</c> with the two checkpoints' saved sequences. The path cannot contain spaces
        /// (the console splits arguments on them). The loaded city must be the
        /// starting checkpoint (its saved PlayerHoldSequence is the start) with bodies off. A gap, duplicate, reordering or
        /// wrong start/end refuses the file before anything is applied.
        /// </summary>
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

        /// <summary>The advance target while a replay runs: never past the next stamped hold minute.</summary>
        private double HoldReplayLimit(double target)
        {
            if (holdReplay == null || simulation == null) return target;
            if (!ReferenceEquals(holdReplayCity, simulation)) { holdReplay = null; holdReplayCity = null; return target; }
            return Math.Max(simulation.State.Minute, holdReplay.Limit(target));
        }

        /// <summary>After the frame's advance: apply the holds stamped at the drained minute, compared bit for bit.</summary>
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
