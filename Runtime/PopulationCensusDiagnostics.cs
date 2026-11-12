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
                CommandHelper.AddCommand<string, string, string>("pc", "Persistent City read-only diagnostics: pc population census workplaces | pc population person <id> | pc population manifest", CensusDetailCommand);
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

        private void CaptureCensusCatalogue(List<Place> places, List<CensusWorkplace> workplaces)
        {
            ResetCensus();
            censusPlaces = PopulationCensusInput.CapturePlaces(places);
            censusWorkplaces = workplaces;
        }

        private void ResetCensus()
        {
            censusTask?.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            censusTask = null; lastCensus = null;
            censusPlaces = new List<Place>(); censusWorkplaces = new List<CensusWorkplace>();
        }

        private void CensusCommand(string area, string action) => RunCensusCommand(area, action, "");
        private void CensusDetailCommand(string area, string action, string detail) => RunCensusCommand(area, action, detail);

        private void RunCensusCommand(string area, string action, string detail)
        {
            if (area == "ordinary" && action == "status") { OrdinaryExchangeStatus(); return; }
            if (area == "capability") { CapabilityCommand(action, detail); return; }
            if (string.Equals(area, "population", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "person", StringComparison.OrdinalIgnoreCase))
            { PersonCommand(detail); return; }
            if (string.Equals(area, "population", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "manifest", StringComparison.OrdinalIgnoreCase))
            { ManifestCommand(); return; }
            bool details = string.Equals(detail, "workplaces", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(area, "population", StringComparison.OrdinalIgnoreCase) || !string.Equals(action, "census", StringComparison.OrdinalIgnoreCase)
                || detail.Length > 0 && !details)
            {
                Debug.LogWarning("[Persistent City] Unknown command. Use: pc population census [workplaces] | pc population person <id> | pc population manifest");
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

        private void ManifestCommand()
        {
            var current = simulation;
            if (failed || current == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
            { Debug.LogWarning("[Persistent City] manifest unavailable: no active Persistent City population in this save."); return; }
            Debug.Log("[Persistent City] population manifest (read-only)\n" + current.PopulationManifest());
        }

        private void PersonCommand(string id)
        {
            var current = simulation;
            if (failed || current == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
            { Debug.LogWarning("[Persistent City] person unavailable: no active Persistent City population in this save."); return; }
            string? line = id.Length == 0 ? null : CityViewReports.DescribePerson(current.View, id, current.FoodAnchorReads);
            if (line == null) Debug.LogWarning("[Persistent City] Unknown person: " + id);
            else Debug.Log("[Persistent City] population person (read-only)\n" + line);
        }

        private void StartCensus(CitySimulator current, object save)
        {
            long started = Stopwatch.GetTimestamp();
            var input = new PopulationCensusInput
            {
                Places = censusPlaces, Workplaces = censusWorkplaces,
                Residents = CityViewReports.CaptureCensusResidents(current.View), StationIds = DiscoveredStationIds()
            };
            var result = new CensusResult { Save = save, Sequence = ++censusSequence, Minute = current.View.Minute,
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
