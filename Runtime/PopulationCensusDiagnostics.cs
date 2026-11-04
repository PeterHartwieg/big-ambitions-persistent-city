using System;
using System.Collections.Generic;
using System.Globalization;
using IngameDebugConsole;
using PersistentCity.Core;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    /// <summary>F5a read-only world census behind the debug console line `pc population census`
    /// (optionally followed by `workplaces`). The census runs only when requested: no frame,
    /// event, save or BusinessAllocation work. Its inputs are the business facts recorded by the
    /// load-time GamePlaces read, the saved place catalogue and residents, and the station list
    /// already discovered by RouteObjectDiscovery. No native member is called or written.</summary>
    public sealed partial class PopulationRuntime
    {
        private List<CensusWorkplace> censusWorkplaces = new List<CensusWorkplace>();
        private bool censusCommandRegistered;

        private void RegisterCensusCommand()
        {
            try
            {
                CommandHelper.AddCommand<string, string>("pc", "Persistent City read-only diagnostics: pc population census", CensusCommand);
                CommandHelper.AddCommand<string, string, string>("pc", "Persistent City read-only diagnostics: pc population census workplaces", CensusDetailCommand);
                censusCommandRegistered = true;
            }
            catch (Exception error) { log?.Warn("Population census console command not registered: " + error.Message); }
        }

        private void UnregisterCensusCommand()
        {
            if (!censusCommandRegistered) return;
            censusCommandRegistered = false;
            try
            {
                CommandHelper.RemoveCommand<string, string>(CensusCommand);
                CommandHelper.RemoveCommand<string, string, string>(CensusDetailCommand);
            }
            catch (Exception error) { log?.Warn("Population census console command not removed: " + error.Message); }
        }

        private void CensusCommand(string area, string action) => RunCensusCommand(area, action, "");
        private void CensusDetailCommand(string area, string action, string detail) => RunCensusCommand(area, action, detail);

        private void RunCensusCommand(string area, string action, string detail)
        {
            bool details = string.Equals(detail, "workplaces", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(area, "population", StringComparison.OrdinalIgnoreCase) || !string.Equals(action, "census", StringComparison.OrdinalIgnoreCase)
                || detail.Length > 0 && !details)
            {
                Debug.LogWarning("[Persistent City] Unknown command. Use: pc population census [workplaces]");
                return;
            }
            var current = simulation;
            if (failed || current == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
            {
                Debug.LogWarning("[Persistent City] census unavailable: no active Persistent City population in this save.");
                return;
            }
            long started = Stopwatch.GetTimestamp();
            var census = PopulationCensus.Compute(new PopulationCensusInput
            {
                Places = current.State.Places, Residents = current.State.Citizens, Workplaces = censusWorkplaces, StationIds = DiscoveredStationIds()
            });
            string summary = census.Summary();
            double milliseconds = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
            Debug.Log("[Persistent City] population census (read-only)\n" + summary
                + "elapsed_ms=" + milliseconds.ToString("F3", CultureInfo.InvariantCulture));
            if (details && !census.Refused) Debug.Log("[Persistent City] population census workplaces\n" + census.WorkplaceDetail());
        }

        // Null until a complete discovery sweep exists; the census then reports stations as pending.
        private List<string>? DiscoveredStationIds()
        {
            var discovery = routeObjects;
            if (discovery == null || !discovery.Ready) return null;
            var ids = new List<string>(discovery.Stations.Length);
            foreach (var station in discovery.Stations)
                if (station != null && station.gameObject.activeInHierarchy) ids.Add("subway:" + station.stationName);
            return ids;
        }
    }
}
