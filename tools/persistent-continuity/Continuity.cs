using System;
using System.Collections;
using System.Collections.Generic;
using GleyTrafficSystem;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    // Read-only main-thread observation. No population, route, body or camera mutation.
    public sealed partial class Probe
    {
        private static readonly BindingFlags ContinuityFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object? ObserveField(object? value, string name)
        {
            if (value == null) throw new InvalidOperationException("Continuity object missing for field " + name);
            var field = value.GetType().GetField(name, ContinuityFields)
                ?? throw new InvalidOperationException("Continuity contract field missing: " + value.GetType().Name + "." + name);
            return field.GetValue(value);
        }
        private static object? ObserveProperty(object? value, string name)
        {
            if (value == null) throw new InvalidOperationException("Continuity object missing for property " + name);
            var property = value.GetType().GetProperty(name, ContinuityFields)
                ?? throw new InvalidOperationException("Continuity contract property missing: " + value.GetType().Name + "." + name);
            return property.GetValue(value);
        }
        private static object StaticObservation(Type type, string name)
            => (type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Continuity contract static property missing: " + type.Name + "." + name)).GetValue(null)
                ?? throw new InvalidOperationException("Continuity static observation null: " + name);
        private static JToken Value(object? value) => value == null ? JValue.CreateNull() : JToken.FromObject(value);
        private static JObject PositionValue(Position p) => new JObject { ["x"] = p.X, ["y"] = p.Y, ["z"] = p.Z };
        private static JObject PositionValue(Vector3 p) => new JObject { ["x"] = p.x, ["y"] = p.y, ["z"] = p.z };

        private object Continuity(JObject request)
        {
            var simulation = Simulation;
            var runtime = Runtime;
            if (simulation.State.Citizens.Count > 5000) throw new InvalidOperationException("Continuity capture bound is 5000 residents.");
            var tracking = request["track"] == null ? Array.Empty<string>() : request["track"]!.Values<string>().ToArray();
            if (tracking.Length > 12 || tracking.Any(s => string.IsNullOrEmpty(s) || s!.Length > 100))
                throw new InvalidOperationException("Track at most twelve bounded resident IDs.");
            var physical = ObserveField(runtime, "physical") ?? throw new InvalidOperationException("Physical adapter absent; enable physical mode and wait for initialization.");
            var bindings = ObserveField(physical, "bindings") as IDictionary ?? throw new InvalidOperationException("Continuity bindings absent or incompatible.");
            var camera = Camera.main;
            var nativePace = runtime.GetType().Assembly.GetType("PersistentCity.Runtime.NativeTravelPace") ?? throw new InvalidOperationException("NativeTravelPace absent.");
            var focus = StaticObservation(nativePace, "Focus");
            if (!(focus is Position)) throw new InvalidOperationException("NativeTravelPace.Focus has incompatible type.");
            var hiddenTravel = StaticObservation(nativePace, "HiddenTravel");
            if (!(hiddenTravel is bool)) throw new InvalidOperationException("NativeTravelPace.HiddenTravel has incompatible type.");
            var bodies = new JArray();
            if (bindings != null)
                foreach (DictionaryEntry pair in bindings)
                {
                    if (bodies.Count >= 128) throw new InvalidOperationException("Physical binding capture bound exceeded.");
                    var binding = pair.Value;
                    var car = ObserveField(binding, "Car");
                    if (car == null) continue;
                    var citizen = (CitizenState)ObserveField(binding, "Citizen")!;
                    var vehicle = ObserveField(car, "Vehicle") as Component;
                    var prefab = ObserveField(car, "NativePrefab") as GameObject;
                    var row = new JObject {
                        ["residentId"] = citizen.Identity.Id, ["carId"] = citizen.Car?.Id,
                        ["modelId"] = citizen.Car?.ModelId, ["paintSeed"] = citizen.Car?.PaintSeed,
                        ["bodyId"] = Value(ObserveProperty(car, "Id")), ["bodyGeneration"] = Value(ObserveProperty(car, "Generation")),
                        ["leaseGeneration"] = Value(ObserveProperty(ObserveField(binding, "Lease"), "Generation")),
                        ["valid"] = Value(ObserveProperty(car, "Valid")), ["ownershipLossReason"] = Value(ObserveProperty(car, "OwnershipLossReason")),
                        ["position"] = ObserveProperty(car, "Position") is Position actual ? PositionValue(actual) : null,
                        ["nativeObjectId"] = vehicle == null ? null : (JToken)vehicle.GetInstanceID(),
                        ["nativePrefab"] = prefab == null ? null : prefab.name,
                        ["nativeIndex"] = Value(ObserveField(car, "NativeIndex")),
                        ["routeFinished"] = Value(ObserveProperty(car, "RouteFinished"))
                    };
                    if (vehicle != null)
                    {
                        var paint = vehicle.GetComponentInChildren<CarFeatures>(true);
                        row["nativePaint"] = paint?.VehicleColor == null ? null : paint.VehicleColor.name;
                        row["pivot"] = PositionValue(vehicle.transform.position);
                        row["eulerAngles"] = PositionValue(vehicle.transform.eulerAngles);
                        row["active"] = vehicle.gameObject.activeInHierarchy;
                        if (camera != null)
                        {
                            Vector3 viewport = camera.WorldToViewportPoint(vehicle.transform.position);
                            row["viewport"] = PositionValue(viewport);
                            row["inMainCameraFrustum"] = viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
                        }
                    }
                    if (ObserveField(binding, "Lease") is PhysicalJourneyLease lease && simulation.TrySamplePhysicalJourney(lease, out var sample))
                        row["sample"] = SampleValue(sample);
                    bodies.Add(row);
                }
            var residents = new JArray();
            foreach (var citizen in simulation.State.Citizens.Where(c => c.Car != null || tracking.Contains(c.Identity.Id)))
            {
                var row = new JObject {
                    ["id"] = citizen.Identity.Id, ["name"] = citizen.Identity.Name,
                    ["phase"] = citizen.Phase.ToString(), ["activity"] = citizen.Activity.ToString(),
                    ["homeId"] = citizen.HomeId, ["dayPlaceId"] = citizen.DayPlaceId, ["placeId"] = citizen.PlaceId,
                    ["destinationId"] = citizen.DestinationId, ["departureMinute"] = citizen.DepartureMinute,
                    ["nextEventMinute"] = citizen.NextEventMinute, ["completedJourneys"] = citizen.CompletedJourneys,
                    ["logicalPosition"] = PositionValue(simulation.PositionOf(citizen)),
                    ["car"] = Value(citizen.Car), ["modes"] = citizen.Journey == null ? new JArray() : JArray.FromObject(citizen.Journey.Legs.Select(l => l.Mode.ToString()).Distinct())
                };
                if (citizen.Phase == CitizenPhase.Travelling && citizen.Journey != null)
                    row["sample"] = SampleValue(simulation.SampleJourney(citizen));
                if (tracking.Contains(citizen.Identity.Id))
                {
                    row["journey"] = Value(citizen.Journey);
                    row["recentVisits"] = Value(citizen.RecentVisits);
                    row["roadAdmission"] = RoadAdmissionValue(simulation, runtime, physical, citizen);
                }
                residents.Add(row);
            }
            return new JObject {
                ["readOnly"] = true, ["saveName"] = SaveGameManager.Current.SaveGameName,
                ["nativeMinute"] = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0 + SaveGameManager.Current.Minute,
                ["stateMinute"] = simulation.State.Minute, ["frame"] = Time.frameCount, ["realSeconds"] = Time.realtimeSinceStartup,
                ["timeScale"] = Time.timeScale, ["hiddenTravel"] = Value(hiddenTravel),
                ["focused"] = Application.isFocused, ["focus"] = focus is Position at ? PositionValue(at) : null,
                ["camera"] = camera == null ? null : new JObject { ["name"] = camera.name, ["position"] = PositionValue(camera.transform.position), ["eulerAngles"] = PositionValue(camera.transform.eulerAngles) },
                ["runtimeFailed"] = Value(ObserveField(runtime, "failed")), ["physicalFailed"] = Value(ObserveField(runtime, "physicalFailed")),
                ["physicalDiagnostics"] = Value(ObserveProperty(physical, "Diagnostics")),
                ["residents"] = residents, ["bodies"] = bodies,
                ["note"] = "Logical parked cars have no parked native mesh. Native body IDs/generations may change after offscreen retirement; resident/car ID, model, paint, route and progress establish continuity. Frustum is a projection test, not occlusion proof. This read-only capture neither creates trips nor moves the observer."
            };
        }

        private static JObject RoadAdmissionValue(CitySimulator simulation, object runtime, object physical, CitizenState citizen)
        {
            var result = new JObject { ["readOnly"] = true };
            try
            {
                var vehicles = ObserveField(physical, "vehicles")!;
                result["lastAttemptResident"] = Value(ObserveProperty(vehicles, "LastAdmissionResident"));
                result["lastAttemptStage"] = Value(ObserveProperty(vehicles, "LastAdmissionStage"));
                result["lastAttemptError"] = Value(ObserveProperty(vehicles, "LastAdmissionError"));
                result["hasTrafficPermission"] = simulation.TryGetTrafficPermission(citizen.Identity.Id, out var permission);
                if (permission.VehicleId != null) result["trafficPermission"] = Value(permission);
                if (citizen.Car == null || citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null)
                { result["sampleReason"] = "No active car journey"; return result; }
                var sample = simulation.SampleJourney(citizen);
                result["sample"] = SampleValue(sample);
                var routes = ObserveField(runtime, "routes")!;
                result["routesReady"] = Value(ObserveProperty(routes, "ReadyForPopulation"));
                result["roadRebuildReason"] = Value(ObserveProperty(routes, "LastRoadRebuildReason"));
                var method = routes.GetType().GetMethod("TryPhysicalRoadSample", ContinuityFields)
                    ?? throw new InvalidOperationException("TryPhysicalRoadSample contract absent");
                object?[] arguments = { citizen, sample, null };
                bool valid = (bool)method.Invoke(routes, arguments)!;
                result["validRoadSample"] = valid;
                if (!valid) return result;
                var road = arguments[2]!;
                var boundaries = (IReadOnlyList<RoadBoundary>)ObserveProperty(road, "AdmissionBoundaries")!;
                var progress = (double)ObserveProperty(road, "RouteProgress")!;
                var remaining = (IReadOnlyList<int>)ObserveProperty(road, "RemainingRoadNodes")!;
                if (boundaries.Count > 512 || remaining.Count > 512) throw new InvalidOperationException("Road diagnostic bound exceeded");
                result["topologyId"] = Value(ObserveProperty(road, "TopologyId"));
                result["spawnNode"] = Value(ObserveProperty(road, "SpawnNode"));
                result["position"] = Value(ObserveProperty(road, "Position"));
                result["directionTarget"] = Value(ObserveProperty(road, "DirectionTarget"));
                result["remainingRoadNodes"] = Value(remaining);
                result["routeProgressMetres"] = progress;
                result["boundaries"] = Value(boundaries);
                var ordinary = vehicles.GetType().GetMethod("Ordinary", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Ordinary native pool eligibility contract absent");
                var pool = Manager.GetVehicleList();
                if (pool.Count > 4096) throw new InvalidOperationException("Native pool diagnostic bound exceeded");
                var matching = pool.Where(v => v != null && v.prefab != null && v.prefab.name == citizen.Car.ModelId).ToArray();
                var eligible = matching.Where(v => (bool)ordinary.Invoke(null, new object[] { v })!).ToArray();
                var selected = eligible.FirstOrDefault(v => !v.gameObject.activeInHierarchy);
                result["savedModelId"] = citizen.Car.ModelId;
                result["matchingModelSlots"] = new JArray(matching.Take(32).Select(v => new JObject {
                    ["nativeIndex"] = v.GetIndex(), ["nativeLength"] = v.length, ["active"] = v.gameObject.activeInHierarchy,
                    ["ordinary"] = eligible.Contains(v), ["selectedInactive"] = ReferenceEquals(v, selected)
                }));
                if (selected == null)
                { result["lengthReason"] = "No current inactive ordinary slot for the saved model; admission length unavailable"; return result; }
                result["selectedNativeLength"] = selected.length;
                result["selectedNativeIndex"] = selected.GetIndex();
                result["lengthEvidence"] = "Current first inactive ordinary exact-model pool slot; read only, not an acquisition or a saved last-attempt value";
                result["admissionAllows"] = PhysicalRoadAdmission.Allows(boundaries, progress, selected.length);
                result["admissionTrace"] = AdmissionTrace(boundaries, progress, selected.length);
            }
            catch (Exception error)
            {
                var cause = error is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : error;
                result["diagnosticError"] = cause.GetType().Name + ": " + cause.Message;
            }
            return result;
        }

        // Explain the current predicate without changing it. Structural refusals anywhere in
        // the committed block are distinguished from clearance at the resident's actual point.
        private static JObject AdmissionTrace(IReadOnlyList<RoadBoundary> boundaries, double progress, double length)
        {
            var steps = new JArray(); int inside = -1; double previous = -1; bool blocked = false;
            string refusal = boundaries.Count == 0 ? "empty boundary list" : ""; int? refusedAt = null;
            if (!Position.Finite(progress) || progress < 0 || !Position.Finite(length) || length <= 0) refusal = "invalid progress or length";
            for (int index = 0; index < boundaries.Count && refusal == ""; index++)
            {
                var boundary = boundaries[index]; int before = inside; bool wasBlocked = blocked;
                if (!Position.Finite(boundary.Distance) || boundary.Distance < 0 || boundary.Distance <= previous
                    || boundary.Enter == boundary.Exit || boundary.Junction < 0) refusal = "invalid or unordered boundary";
                else if (boundary.Enter)
                {
                    if (inside != -1) refusal = "entry while already inside a junction";
                    else
                    {
                        inside = boundary.Junction;
                        if (progress >= boundary.Distance || boundary.Distance - progress < length + 1) blocked = true;
                    }
                }
                else if (inside != boundary.Junction && index != 0) refusal = "exit does not match an earlier entry";
                else
                {
                    if (inside != boundary.Junction) blocked = true; // Leading explicit exit; retain rear clearance.
                    inside = -1;
                    if (progress >= boundary.Distance + length + 1) blocked = false;
                    else if (progress >= boundary.Distance) blocked = true;
                }
                steps.Add(new JObject { ["index"] = index, ["distance"] = boundary.Distance,
                    ["relativeToProgress"] = boundary.Distance - progress, ["enter"] = boundary.Enter, ["exit"] = boundary.Exit,
                    ["junction"] = boundary.Junction, ["leadingExitEvidence"] = index == 0 && boundary.Exit,
                    ["insideBefore"] = before, ["insideAfter"] = inside,
                    ["blockedBefore"] = wasBlocked, ["blockedAfter"] = blocked, ["refusal"] = refusal });
                if (refusal != "") refusedAt = index;
                previous = boundary.Distance;
            }
            if (refusal == "" && inside != -1) refusal = "final entry has no matching exit anywhere in committed road block";
            if (refusal == "" && blocked) refusal = "current body envelope overlaps junction or stop-line clearance";
            return new JObject { ["marginMetres"] = 1, ["bodyAndMarginMetres"] = length + 1,
                ["reason"] = refusal == "" ? "allowed" : refusal, ["firstRefusalIndex"] = Value(refusedAt),
                ["finalInsideJunction"] = inside, ["finalBlocked"] = blocked, ["steps"] = steps };
        }

        private static JObject SampleValue(PhysicalJourneySample sample) => new JObject {
            ["elapsedMinutes"] = sample.ElapsedMinutes, ["totalMinutes"] = sample.TotalMinutes,
            ["legIndex"] = sample.LegIndex, ["legFraction"] = sample.LegFraction,
            ["mode"] = sample.Mode.ToString(), ["stationary"] = sample.Stationary, ["atEnd"] = sample.AtEnd,
            ["from"] = PositionValue(sample.From), ["to"] = PositionValue(sample.To), ["position"] = PositionValue(sample.Position)
        };
    }
}
