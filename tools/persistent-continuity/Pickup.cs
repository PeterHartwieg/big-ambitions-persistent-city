using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    // Disposable selected-itinerary diagnostic. Production has no caller or fixture API.
    public sealed partial class Probe
    {
        private sealed class PickupQuery
        {
            public string Resident = "", Original = "", Origin = "", Destination = "";
            public Route? Route;
        }
        private readonly List<PickupQuery> pickupQueries = new List<PickupQuery>();
        private readonly JArray pickupRows = new JArray();
        private int pickupCursor;
        private double pickupMinute;
        private CitySimulator? pickupSimulation;
        private object? pickupSave;
        private string pickupState = "not-started", pickupError = "";
        private string pickupBranch = "";
        private bool pickupCommitted;
        private const string PickupPrefix = "FT Food Routines Continuity Pickup";

        private static T PickupClone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
        private static string PickupJson(object value) => JsonConvert.SerializeObject(value);
        private static string PickupFingerprint(object value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(PickupJson(value)))).Replace("-", "").ToLowerInvariant();
        }
        private ICitizenRoutePlanner PickupPlanner => ObserveField(Runtime, "routes") as ICitizenRoutePlanner
            ?? throw new InvalidOperationException("Native citizen planner absent.");
        private void PickupPaused()
        {
            if (!Allowed() || Time.timeScale != 0 || !SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines", StringComparison.Ordinal))
                throw new InvalidOperationException("Paused isolated FT Food Routines city required.");
            double now = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0 + SaveGameManager.Current.Minute;
            if (!Simulation.IsCaughtUpTo(now) || Math.Abs(Simulation.State.Minute - now) > .001)
                throw new InvalidOperationException("Population must be caught up to the paused native clock.");
            var routes = ObserveField(Runtime, "routes")!;
            if (!(bool)ObserveProperty(routes, "ReadyForPopulation")!) throw new InvalidOperationException("Native routes not ready.");
        }
        private void PickupResidentIdle(CitizenState person)
        {
            if (person.Car == null || !person.Preferences.OwnsCar || person.Phase != CitizenPhase.AtPlace || person.Journey != null
                || person.MealOuting != null) throw new InvalidOperationException("Existing idle car owner without an outing required.");
            foreach (string field in new[] { "physicalJourneys", "trafficCitizens" })
                if (((IDictionary)ObserveField(Simulation, field)!).Contains(person.Identity.Id))
                    throw new InvalidOperationException("Target already has physical/traffic journey ownership.");
            if (Simulation.State.Food?.Residents.Any(m => m.ResidentId == person.Identity.Id
                && (m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving || m.Status == MealStatus.Paid || m.Status == MealStatus.Uncertain)) == true)
                throw new InvalidOperationException("Target has an active food intention.");
        }
        private HashSet<string> PickupExcludedShops() => new HashSet<string>(SaveGameManager.Current.BuildingRegistrations
            .Where(r => r.RentedByPlayer).Select(NativeFoodMarket.PlaceId), StringComparer.Ordinal);

        private object PickupCommand(JObject request)
        {
            string op = (string?)request["op"] ?? "";
            if (op == "pickup-status") return PickupReport();
            if (op == "pickup-preflight")
            {
                PickupPaused();
                if (pickupState == "querying" || pickupCommitted) throw new InvalidOperationException("Existing pickup run is active or committed.");
                string evidenceText;
                using (var embedded = typeof(Probe).Assembly.GetManifestResourceStream("PersistentCity.PickupSubwayEvidence.json"))
                {
                    if (embedded != null)
                    {
                        if (embedded.Length > 256 * 1024) throw new InvalidOperationException("Subway evidence bound exceeded.");
                        using var reader = new StreamReader(embedded); evidenceText = reader.ReadToEnd();
                    }
                    else
                    {
                        string evidencePath = Path.Combine(Root, "pickup-subway-evidence.json");
                        if (!File.Exists(evidencePath) || new FileInfo(evidencePath).Length > 256 * 1024)
                            throw new InvalidOperationException("Bounded captured subway evidence required.");
                        evidenceText = File.ReadAllText(evidencePath);
                    }
                }
                var evidence = JObject.Parse(evidenceText);
                var observations = (JObject?)evidence["subwayFirstSamples"] ?? throw new InvalidOperationException("Subway evidence shape missing.");
                var ids = request["owners"]?.Values<string>().Select(v => v ?? "").ToArray() ?? Array.Empty<string>();
                if (ids.Length < 1 || ids.Length > 3 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                    throw new InvalidOperationException("Provide one to three distinct observed subway owner IDs.");
                var excluded = PickupExcludedShops();
                var places = Simulation.State.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
                pickupMinute = Simulation.State.Minute; pickupSimulation = Simulation; pickupSave = SaveGameManager.Current;
                pickupBranch = SaveGameManager.Current.SaveGameName; pickupCursor = 0; pickupError = "";
                pickupQueries.Clear(); pickupRows.Clear();
                foreach (string id in ids)
                {
                    var person = Simulation.State.Citizens.Single(c => c.Identity.Id == id); PickupResidentIdle(person);
                    var proof = observations[id] ?? throw new InvalidOperationException("Owner lacks captured actual Subway sample: " + id);
                    var before = proof["car"]!.ToObject<CarState>()!;
                    if (before.Id != person.Car!.Id || !before.ParkedPosition.Equals(person.Car.ParkedPosition)
                        || before.ModelId.Length > 0 && (before.ModelId != person.Car.ModelId || before.PaintSeed != person.Car.PaintSeed))
                        throw new InvalidOperationException("Car changed since captured subway sample: " + id);
                    var origin = places[person.PlaceId];
                    // Actual public destinations only. The fixture never visits a player-owned shop.
                    var candidates = places.Values.Where(p => p.Id != origin.Id && (p.Kind == PlaceKind.Shop || p.Kind == PlaceKind.Leisure)
                        && !excluded.Contains(p.Id) && DailyRoutine.Open(p, pickupMinute))
                        .Select(p => new { Place = p, Metres = Position.Distance(origin.Position, p.Position) })
                        .Where(p => p.Metres >= 150 && p.Metres <= 1200).OrderBy(p => p.Metres).ThenBy(p => p.Place.Id, StringComparer.Ordinal)
                        .Take(8).ToArray();
                    foreach (var destination in candidates)
                        pickupQueries.Add(new PickupQuery { Resident = id, Original = PickupJson(person), Origin = origin.Id, Destination = destination.Place.Id });
                    pickupRows.Add(new JObject { ["owner"] = id, ["actualSubwayEvidence"] = proof.DeepClone(), ["candidateCount"] = candidates.Length });
                }
                pickupState = pickupQueries.Count == 0 ? "no-candidates" : "querying";
                return PickupReport();
            }
            if (op == "pickup-commit") return CommitPickup((int?)request["query"] ?? -1);
            throw new InvalidOperationException("Unknown pickup diagnostic operation.");
        }
        private object PickupReport() => new { status = pickupState, queryError = pickupError, minute = pickupMinute, branch = pickupBranch,
            completedQueries = pickupCursor, totalQueries = pickupQueries.Count, committed = pickupCommitted, rows = pickupRows,
            note = "Preflight changes no resident/car state. Commit is one explicitly selected diagnostic itinerary, not ordinary demand or mode-share evidence." };

        private void TickPickup()
        {
            if (pickupState != "querying") return;
            try
            {
                PickupPaused();
                if (!ReferenceEquals(pickupSimulation, Simulation) || !ReferenceEquals(pickupSave, SaveGameManager.Current)
                    || pickupMinute != Simulation.State.Minute) throw new InvalidOperationException("Preflight context changed.");
                var q = pickupQueries[pickupCursor];
                var person = Simulation.State.Citizens.Single(c => c.Identity.Id == q.Resident);
                if (PickupJson(person) != q.Original) throw new InvalidOperationException("Resident changed during preflight.");
                var from = Simulation.State.Places.Single(p => p.Id == q.Origin);
                var to = Simulation.State.Places.Single(p => p.Id == q.Destination);
                // Detached person, existing exact car and preferences; native graph/traffic remain real.
                var route = PickupPlanner.Plan(from, to, pickupMinute, PickupClone(person));
                bool valid = route != null && route.Legs.Any(l => l.Mode == TravelMode.Car)
                    && TransportComposition.Valid(route, from.Position, to.Position, person.Preferences, person.Car!.ParkedPosition)
                    && (PickupPlanner as ITrafficRoutePlanner)?.TrafficNetwork?.TryMapRemaining(route, 0, out _) == true
                    && route.Minutes <= 360 && DailyRoutine.Open(to, pickupMinute + route.Minutes)
                    && DailyRoutine.NextClose(to, pickupMinute + route.Minutes) >= pickupMinute + route.Minutes + 10;
                if (valid) { CitySimulator.ValidateRoute(route!, from.Position, to.Position); CitySimulator.ValidateCarRoute(route!, person); q.Route = PickupClone(route!); }
                pickupRows.Add(new JObject { ["query"] = pickupCursor, ["resident"] = q.Resident, ["origin"] = q.Origin,
                    ["destination"] = q.Destination, ["eligibleCommit"] = valid, ["route"] = Value(route),
                    ["reason"] = route == null ? "No feasible native route" : !route.Legs.Any(l => l.Mode == TravelMode.Car) ? "Native fastest route is not car"
                        : !valid ? "Route exceeds bounded duration or destination opening" : "Validated native car route" });
                pickupCursor++;
                if (pickupCursor == pickupQueries.Count) pickupState = pickupQueries.Any(x => x.Route != null) ? "ready" : "no-car-candidate";
            }
            catch (Exception e) { pickupState = "failed"; pickupError = e.ToString(); }
        }

        private object CommitPickup(int index)
        {
            PickupPaused();
            if (pickupCommitted || pickupState != "ready" || index < 0 || index >= pickupQueries.Count
                || !SaveGameManager.Current.SaveGameName.StartsWith(PickupPrefix, StringComparison.Ordinal)
                || !ReferenceEquals(pickupSimulation, Simulation) || !ReferenceEquals(pickupSave, SaveGameManager.Current)
                || pickupMinute != Simulation.State.Minute || pickupBranch != SaveGameManager.Current.SaveGameName)
                throw new InvalidOperationException("Ready unchanged preflight on the named disposable Pickup branch required.");
            var q = pickupQueries[index];
            var person = Simulation.State.Citizens.Single(c => c.Identity.Id == q.Resident); PickupResidentIdle(person);
            if (q.Route == null || PickupJson(person) != q.Original) throw new InvalidOperationException("Selected route/resident changed.");
            var from = Simulation.State.Places.Single(p => p.Id == q.Origin);
            var to = Simulation.State.Places.Single(p => p.Id == q.Destination);
            if (PickupExcludedShops().Contains(to.Id)) throw new InvalidOperationException("Destination became player owned.");
            var fresh = PickupPlanner.Plan(from, to, pickupMinute, PickupClone(person));
            string quoteFailure = "";
            if (fresh == null) quoteFailure = "Fresh native route unavailable.";
            else
            {
                try
                {
                    CitySimulator.ValidateRoute(fresh, from.Position, to.Position); CitySimulator.ValidateCarRoute(fresh, person);
                    if (!fresh.Legs.Any(l => l.Mode == TravelMode.Car)
                        || !TransportComposition.Valid(fresh, from.Position, to.Position, person.Preferences, person.Car!.ParkedPosition))
                        quoteFailure = "Fresh native route no longer valid for the owned car.";
                    else if ((PickupPlanner as ITrafficRoutePlanner)?.TrafficNetwork?.TryMapRemaining(fresh, 0, out _) != true)
                        quoteFailure = "Fresh native route does not map to current traffic ownership.";
                    else if (fresh.Minutes > 360 || !DailyRoutine.Open(to, pickupMinute)
                        || !DailyRoutine.Open(to, pickupMinute + fresh.Minutes)
                        || DailyRoutine.NextClose(to, pickupMinute + fresh.Minutes) < pickupMinute + fresh.Minutes + 10)
                        quoteFailure = "Fresh route exceeds bounded duration or destination opening.";
                }
                catch (Exception e) { quoteFailure = e.Message; }
            }
            if (quoteFailure.Length > 0) return new { error = quoteFailure, preflightQuote = q.Route, freshQuote = fresh,
                preflightFingerprint = PickupFingerprint(q.Route), freshFingerprint = fresh == null ? null : PickupFingerprint(fresh) };
            var dictionary = (IDictionary)ObserveField(Simulation, "citizenEvents")!;
            object item = dictionary[person.Identity.Id]!;
            if (!ReferenceEquals(ObserveField(item, "Citizen"), person)) throw new InvalidOperationException("Scheduled identity changed.");
            object queue = ObserveField(Simulation, "due")!;
            var itemType = item.GetType();
            var remove = queue.GetType().GetMethod("Remove", new[] { itemType })!;
            var add = queue.GetType().GetMethod("Add", new[] { itemType })!;
            var minuteField = itemType.GetField("Minute")!;
            double oldMinute = (double)minuteField.GetValue(item)!;
            var scheduled = typeof(CitySimulator).GetMethod("ScheduledMinute", ContinuityFields)
                ?? throw new InvalidOperationException("Native scheduled journey seam absent.");
            string before = SaveCodec.Write(Simulation.State);
            string prefix = Path.Combine(Root, "pickup-" + pendingId);
            if (File.Exists(prefix + "-original-state.json")) throw new InvalidOperationException("Refuse overwrite of original fixture evidence.");
            File.WriteAllText(prefix + "-original-state.json", before);
            var original = PickupClone(person);
            if (!(bool)remove.Invoke(queue, new[] { item })!) throw new InvalidOperationException("Resident due event absent.");
            try
            {
                person.DestinationId = to.Id; person.DestinationActivity = to.Kind == PlaceKind.Shop ? Activity.Shop : Activity.Leisure;
                person.Phase = CitizenPhase.Travelling; person.Journey = PickupClone(fresh!); person.DepartureMinute = pickupMinute;
                person.NextEventMinute = pickupMinute + fresh!.Minutes; person.ActivityUntilMinute = person.NextEventMinute + 10;
                minuteField.SetValue(item, (double)scheduled.Invoke(Simulation, new object[] { person })!);
                if (!(bool)add.Invoke(queue, new[] { item })!) throw new InvalidOperationException("Could not restore selected due event.");
                Simulation.SynchronizeSnapshot(); SaveCodec.Write(Simulation.State);
            }
            catch
            {
                remove.Invoke(queue, new[] { item });
                person.DestinationId = original.DestinationId; person.DestinationActivity = original.DestinationActivity;
                person.Phase = original.Phase; person.Journey = original.Journey; person.DepartureMinute = original.DepartureMinute;
                person.NextEventMinute = original.NextEventMinute; person.ActivityUntilMinute = original.ActivityUntilMinute;
                minuteField.SetValue(item, oldMinute); add.Invoke(queue, new[] { item }); throw;
            }
            pickupCommitted = true; pickupState = "committed";
            var receipt = new { diagnosticSelectedItinerary = true, branch = pickupBranch, minute = pickupMinute, query = index,
                originalState = Path.GetFileName(prefix + "-original-state.json"), before = original, after = person,
                route = fresh, preflightQuote = q.Route, preflightFingerprint = PickupFingerprint(q.Route), freshFingerprint = PickupFingerprint(fresh!),
                routeQuoteChanged = PickupJson(q.Route) != PickupJson(fresh!), scheduledAt = minuteField.GetValue(item), carUnchanged = PickupJson(original.Car!) == PickupJson(person.Car!),
                note = "Same resident, owned car, position and scheduler event. Exact native planner route; traffic admission remains normal. No cars, identities or funds created/moved." };
            File.WriteAllText(prefix + "-change.json", JsonConvert.SerializeObject(receipt, Formatting.Indented));
            return receipt;
        }
    }
}
