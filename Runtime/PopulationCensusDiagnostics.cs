using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Extensions;
using IngameDebugConsole;
using PersistentCity.Core;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    /// <summary>F5a read-only world census behind the debug console line `pc population census`
    /// (optionally followed by `workplaces`). It runs only when requested: no frame, event, save or
    /// BusinessAllocation work. Its inputs are the place catalogue and business facts from the
    /// load-time GamePlaces read (not the persisted catalogue, which can predate businesses
    /// established since), the residents' saved assignments joined by place ID, and the stations
    /// already discovered by RouteObjectDiscovery. A request copies those managed facts on the main
    /// thread, reduces them on a detached worker, and prints the latest result finished for this
    /// save. No native member is called or written.</summary>
    public sealed partial class PopulationRuntime
    {
        private List<Place> censusPlaces = new List<Place>();
        private List<CensusWorkplace> censusWorkplaces = new List<CensusWorkplace>();
        private bool censusCommandRegistered;
        private sealed class CensusResult
        {
            public object Save = null!;
            public int Sequence;
            public double Minute, CaptureMilliseconds, ComputeMilliseconds;
            public string Summary = "", Detail = "";
            public bool Refused;
        }
        private Task<CensusResult>? censusTask;
        private CensusResult? lastCensus;
        private int censusSequence;

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

        /// <summary>Called by TryStart with the fresh load-time catalogue of the save being started.</summary>
        private void CaptureCensusCatalogue(List<Place> places, List<CensusWorkplace> workplaces)
        {
            ResetCensus();
            censusPlaces = PopulationCensusInput.CapturePlaces(places);
            censusWorkplaces = workplaces;
        }

        private void ResetCensus()
        {
            // A worker that finishes after a save change is observed and dropped, never published.
            censusTask?.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            censusTask = null; lastCensus = null;
            censusPlaces = new List<Place>(); censusWorkplaces = new List<CensusWorkplace>();
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
            var save = SaveGameManager.Current;
            if (failed || current == null || save == null || !ReferenceEquals(saveIdentity, save))
            {
                Debug.LogWarning("[Persistent City] census unavailable: no active Persistent City population in this save.");
                return;
            }
            var pending = censusTask;
            if (pending != null && pending.IsCompleted)
            {
                censusTask = null;
                if (pending.Status != TaskStatus.RanToCompletion)
                    Debug.LogWarning("[Persistent City] census computation failed: " + (pending.Exception?.GetBaseException().Message ?? "cancelled"));
                else if (ReferenceEquals(pending.Result.Save, save)) lastCensus = pending.Result;
            }
            if (censusTask == null) StartCensus(current, save);
            var result = lastCensus != null && ReferenceEquals(lastCensus.Save, save) ? lastCensus : null;
            if (result == null)
            {
                Debug.Log("[Persistent City] population census (read-only)\nstatus=pending\nRun the command again for the result.");
                return;
            }
            Debug.Log("[Persistent City] population census (read-only)\n" + result.Summary
                + "sequence=" + result.Sequence.ToString(CultureInfo.InvariantCulture)
                + "\ncaptured_minute=" + result.Minute.ToString("R", CultureInfo.InvariantCulture)
                + "\ncapture_ms=" + result.CaptureMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
                + "\ndetached_compute_ms=" + result.ComputeMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
                + "\nnext=" + (censusTask == null ? "none" : "pending"));
            if (details && !result.Refused) Debug.Log("[Persistent City] population census workplaces\n" + result.Detail);
        }

        // Main thread: copy only managed facts, then reduce them detached from the frame.
        private void StartCensus(CitySimulator current, object save)
        {
            long started = Stopwatch.GetTimestamp();
            var input = new PopulationCensusInput
            {
                Places = censusPlaces, Workplaces = censusWorkplaces,
                Residents = PopulationCensusInput.CaptureResidents(current.State.Citizens), StationIds = DiscoveredStationIds()
            };
            var result = new CensusResult { Save = save, Sequence = ++censusSequence, Minute = current.State.Minute,
                CaptureMilliseconds = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp };
            censusTask = Task.Run(() =>
            {
                long computeStarted = Stopwatch.GetTimestamp();
                var census = PopulationCensus.Compute(input);
                result.Summary = census.Summary();
                result.Detail = census.Refused ? "" : census.WorkplaceDetail();
                result.Refused = census.Refused;
                result.ComputeMilliseconds = (Stopwatch.GetTimestamp() - computeStarted) * MillisecondsPerTimestamp;
                return result;
            });
        }

        // Null until a complete discovery sweep exists; the census then reports stations as pending.
        // Same activity and DLC visibility filter as MultimodalRoutes.EnsureStations. The planner
        // also drops stations without a finite navmesh entrance or near a gate, so its usable
        // portal count can be smaller than this discovered count.
        private List<string>? DiscoveredStationIds()
        {
            var discovery = routeObjects;
            if (discovery == null || !discovery.Ready) return null;
            var ids = new List<string>(discovery.Stations.Length);
            foreach (var station in discovery.Stations)
            {
                if (station == null || !station.gameObject.activeInHierarchy
                    || station.GetComponentsInParent<ToggleObjectDLC>(true).Any(t => t.showWhenOwned != t.dlc.DlcIsOwned())) continue;
                ids.Add("subway:" + station.stationName);
            }
            return ids;
        }
    }
}
