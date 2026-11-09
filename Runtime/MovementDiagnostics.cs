using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>F2c read-only movement console lines: `pc movement status` and `pc movement corrections since &lt;seq&gt;`.
    /// They read Runtime telemetry only (PhysicalPopulation.Telemetry) and write nothing.</summary>
    public sealed partial class PopulationRuntime
    {
        private readonly List<WalkerCorrection> movementCorrections = new List<WalkerCorrection>();

        private void MovementCommand(string action, string detail, string value)
        {
            var telemetry = PhysicalPopulation.Telemetry;
            if (action == "status" && detail.Length == 0)
            {
                Debug.Log("[Persistent City] movement status (read-only)\n" + telemetry.Status(physical?.Followers ?? 0));
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
            Debug.LogWarning("[Persistent City] Unknown command. Use: pc movement status | pc movement corrections since <seq>");
        }
    }
}
