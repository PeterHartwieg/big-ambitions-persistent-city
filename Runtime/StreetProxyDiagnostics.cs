using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        /// <summary>Main-thread, explicit one-shot bridge command. False returns null before any native read.
        /// Edges must be independently identified world-space pairs, never generated from the candidate matrix.</summary>
        public StreetProxyDoorSnapshot? ReadStreetProxyDoors(bool requested = false, IReadOnlyList<StreetEdgePair>? edges = null)
        {
            if (!requested) return null;
            RequireStreetProxyHost();
            return NativeStreetProxyDiagnostics.ReadDoors(edges);
        }

        /// <summary>Main-thread, explicit one-shot metadata command. No polling, retained cache or scene writes.</summary>
        public StreetProxyInventorySnapshot? ReadStreetProxyInventory(bool requested = false)
        {
            if (!requested) return null;
            RequireStreetProxyHost();
            return NativeStreetProxyDiagnostics.ReadInventory(physical);
        }

        private void RequireStreetProxyHost()
        {
            if (failed || simulation == null || SaveGameManager.Current == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
                throw new InvalidOperationException("A ready current population is required for street-proxy observation.");
        }
    }
}
