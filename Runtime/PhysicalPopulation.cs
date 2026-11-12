using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        internal StreetFoodVendorMarket? PhysicalVendorMarket => vendorMarket;
    }

    internal sealed partial class PhysicalPopulation : IDisposable
    {
        private sealed class Binding
        {
            public CitizenState Citizen = null!;
            public Route? Journey;
            public double BodyMinutes;
            public float HurrySince = float.NaN;
            public bool Fading;
            public int FadeLeg = -1;
            public float FadeOutSince = float.NaN;
            public bool FadeOnScreen;
            public Position FadeFrom;
            public bool Tail;
            public float TailSince;
            public bool Handoff;
            public int LagFrame = -1;
            public float FadeInSince = float.NaN;
            public int CarSyncFrame = int.MinValue;
            public PhysicalPedestrianBody? Person;
            public PhysicalVehicleBody? Car;
            public int TargetLeg = -1;
            public Position TargetPoint;
            public bool TargetIsPose;
            public double BoundPace, BoundMinutes;
            public int ReachedCarLeg = -1;
            public Position ReachedCarMarker;
            public readonly PhysicalCornerHandoff CornerHandoff = new PhysicalCornerHandoff();
            public CarPhaseMismatch Phase;
            public float NextTargetAttempt;
            public Dwell? Queue;
            public long VendorOpening;
            public double VendorDepartureUntil;
            public OptionalStopState? Optional;
            public bool OptionalReturning;
            public bool OptionalStandsShort;
            public DoorQueue? Line;
            public int Slot = -1;
            public QueueStep Step;
            public float StepWalked;
            public int EntryAttempts;
            public bool AwaitingArrival;
            public double AwaitMinute;
            public Route? AwaitJourney;
        }
        private enum QueueStep { Approaching, Standing, Entering }
        private sealed class QueuePlanEntry
        {
            public Dwell? Dwell; public DoorQueue Line = null!; public int Slot; public long VendorOpening;
        }
        private readonly CitySimulator simulation;
        private readonly MultimodalRoutes routes;
        private readonly PhysicalPedestrians pedestrians;
        private readonly PhysicalVehicles vehicles;
        private readonly AmbientPedestrians ambient;
        private readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> attempted = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, Place> places;
        private readonly DwellSpots spots;
        private readonly PopulationRuntime? owner;
        private readonly Dictionary<string, long> vendorGaveUp = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, IPersonView> vendorWaiters = new Dictionary<string, IPersonView>(StringComparer.Ordinal);
        private FoodCommerceState? vendorFood;
        private int vendorMealCount;
        private readonly Dictionary<string, ResidentMeal> vendorMeals = new Dictionary<string, ResidentMeal>(StringComparer.Ordinal);
        private readonly Dictionary<string, QueuePlanEntry> queuers = new Dictionary<string, QueuePlanEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> gaveUp = new Dictionary<string, double>(StringComparer.Ordinal);
        private const float QueueStepSeconds = 20f;
        private const int EntryAttemptFrames = 30;
        private readonly IModLogger log;
        private readonly PlayerContact contact = new PlayerContact();
        private readonly int pedestrianLimit, vehicleLimit;
        private readonly double radius;
        private float nextSelection;
        private bool active, disposed;
        private long acquired, released, blockedSamples, failedAcquisitions, queued;
        internal static readonly WalkerMovementTelemetry Telemetry = new WalkerMovementTelemetry();
        private readonly int faultAcquirePercent;
        private readonly System.Random faultDice = new System.Random(0x2F2C);
        private float followClock, followDelta;
        private double followMinute = double.NaN;
        private readonly Dictionary<string, (CitizenState Citizen, Route? Journey, string? ArrivedAt)> fadedUntilHidden
            = new Dictionary<string, (CitizenState Citizen, Route? Journey, string? ArrivedAt)>(StringComparer.Ordinal);
        private int personBindings, carBindings, queueBindings;
        private List<Binding>? bindingSnapshot = new List<Binding>();
        private const float JourneyIndexCheckSeconds = 30f;
        private float nextJourneyIndexCheck;
        private bool journeyIndexFallback;
        private long journeyIndexChecks, journeyIndexMismatches;

        public PhysicalPopulation(Transform parent, CitySimulator simulation, MultimodalRoutes routes, Assembly dependency,
            IModLogger log, int pedestrianLimit, int vehicleLimit, double radius, DwellSpots spots, int faultAcquirePercent = 0)
        {
            this.spots = spots;
            this.faultAcquirePercent = Math.Max(0, Math.Min(100, faultAcquirePercent));
            owner = parent.GetComponent<PopulationRuntime>();
            var view = simulation.View;
            for (int i = 0; i < view.ResidentList.Count; i++) TrackVendorWaiter(view.ResidentList[i]);
            for (int i = 0; i < view.VisitorRoutineList.Count; i++) TrackVendorWaiter(view.VisitorRoutineList[i]);
            this.simulation = simulation; this.routes = routes; this.log = log;
            RefreshVendorMeals();
            this.pedestrianLimit = pedestrianLimit; this.vehicleLimit = vehicleLimit; this.radius = radius;
            places = simulation.State.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
            pedestrians = new PhysicalPedestrians(parent, pedestrianLimit) { BeforeNavigation = BoundWalkers };
            vehicles = new PhysicalVehicles(routes.ResolveWaypoint, vehicleLimit);
            vehicles.CommandLimit = CommandLimit;
            vehicles.NativeTargetPermission = NativeTargetPermission;
            ambient = new AmbientPedestrians(dependency, simulation.State.SourceIdentities);
            try
            {
                if (!vehicles.Ready) throw new InvalidOperationException("Own cars: " + vehicles.LastError);
                if (!ambient.Ready) throw new InvalidOperationException("Pedestrian hooks: " + ambient.LastError);
                bool tested = false;
                foreach (var resident in view.ResidentList.Take(64))
                {
                    if (!pedestrians.TryAcquire(Entity(resident).Identity, places[resident.HomeId].Position, out var body)) continue;
                    pedestrians.Release(body!); tested = true; break;
                }
                if (!tested) throw new InvalidOperationException("No native pedestrian body passed appearance/NavMesh preflight.");
                if (!vehicles.Activate()) throw new InvalidOperationException("Native traffic is not ready or has no ordinary car model.");
                ambient.Activate();
                if (!ambient.Active) throw new InvalidOperationException("Pedestrian activation: " + ambient.LastError);
                active = true;
                simulation.ActivityObserved += OnOptionalDeparture;
                log.Info("Physical population active: ordinary pedestrian and vehicle spawns replaced by own bodies and own cars; RLO scenes, customers, workers, taxis and service vehicles retained.");
            }
            catch { Dispose(); throw; }
        }

        public string Diagnostics => "physical=" + active + ", pedestrians=" + personBindings
            + ", cars=" + carBindings + ", acquired=" + acquired + ", released=" + released
            + ", blockedSamples=" + blockedSamples + ", unavailable=" + failedAcquisitions
            + ", queueing=" + queueBindings + ", queued=" + queued + ", walkersResumed=" + ambient.Resumed
            + ", journeyIndex=" + simulation.ActiveJourneys.Count + ", journeyIndexWithheldVisitors=" + simulation.ActiveJourneys.WithheldVisitors
            + ", journeyIndexChecks=" + journeyIndexChecks + ", journeyIndexMismatches=" + journeyIndexMismatches + ", journeyIndexFallback=" + journeyIndexFallback
            + ", leaving=" + leaving.Count + ", " + vehicles.OwnershipDiagnostics
            + ", carAdmission=" + vehicles.LastAdmissionResident + ":" + vehicles.LastAdmissionStage + ":" + vehicles.LastAdmissionError;
        public void ObserveForSave()
        {
            vehicles.AssertHealthy();
            simulation.RefreshOptionalStops();
            foreach (var binding in bindings.Values.ToArray()) if (!Observe(binding)) Release(binding);
        }
        private static CitizenState Entity(IPersonView person) => (CitizenState)person;
        private bool Bound(Binding binding) => bindings.TryGetValue(binding.Citizen.Identity.Id, out var registered) && ReferenceEquals(registered, binding);
        private void Bind(Binding binding)
        {
            bindings.Add(binding.Citizen.Identity.Id, binding);
            CountBinding(binding, 1);
        }
        private void Unbind(string citizenId)
        {
            if (!bindings.TryGetValue(citizenId, out var registered)) return;
            bindings.Remove(citizenId);
            CountBinding(registered, -1);
        }
        private void CountBinding(Binding binding, int delta)
        {
            if (binding.Person != null) personBindings += delta;
            if (binding.Car != null) carBindings += delta;
            if (binding.Queue.HasValue || binding.VendorOpening > 0) queueBindings += delta;
        }
        private void ReleaseBodies(Binding binding)
        {
            if (binding.Handoff && float.IsNaN(binding.FadeOutSince) && Leaves(binding)) return;
            try { if (binding.Person != null) pedestrians.Release(binding.Person); }
            finally { if (binding.Car != null) vehicles.Release(binding.Car); }
        }
        private bool Leaves(Binding binding)
        {
            if (binding.Person != null ? !(binding.Person.Valid && binding.Person.OnScreen && binding.Person.CanFade)
                : binding.Car == null || !(binding.Car.Valid && binding.Car.OnScreen && binding.Car.CanFade)) return false;
            binding.Person?.Pace(0);
            leaving.Add(new Leaving(binding.Person, binding.Car, followClock));
            return true;
        }
        private readonly List<Leaving> leaving = new List<Leaving>();
        private readonly struct Leaving
        {
            public Leaving(PhysicalPedestrianBody? person, PhysicalVehicleBody? car, float since) { Person = person; Car = car; Since = since; }
            public PhysicalPedestrianBody? Person { get; }
            public PhysicalVehicleBody? Car { get; }
            public float Since { get; }
        }
        private void FadeLeaving(bool all)
        {
            Exception? failure = null;
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                var body = leaving[i];
                float alpha = all ? 0 : BodyFade.Out(followClock - body.Since);
                bool valid = body.Person != null ? body.Person.Valid : body.Car != null && body.Car.Valid;
                if (alpha > 0 && valid)
                {
                    if (body.Person != null) { body.Person.Pace(0); body.Person.SetFade(alpha); } else body.Car!.SetFade(alpha);
                    continue;
                }
                leaving.RemoveAt(i);
                try { if (body.Person != null) pedestrians.Release(body.Person); else if (body.Car != null) vehicles.Release(body.Car); }
                catch (Exception error) { failure = failure ?? error; }
            }
            if (failure != null) throw new InvalidOperationException("A leaving body could not be released.", failure);
        }
        private void Release(Binding binding)
        {
            try { ReleaseBodies(binding); }
            finally
            {
                Unbind(binding.Citizen.Identity.Id);
                if (binding.Handoff) attempted.Remove(binding.Citizen.Identity.Id);
                else attempted[binding.Citizen.Identity.Id] = Time.unscaledTime;
                released++;
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; active = false;
            simulation.ActivityObserved -= OnOptionalDeparture;
            vehicles.CommandLimit = null;
            vehicles.NativeTargetPermission = null;
            try { Suspend(); }
            finally
            {
                try { ambient.Dispose(); }
                finally { try { vehicles.Dispose(); } finally { pedestrians.Dispose(); } }
            }
        }
    }
}
