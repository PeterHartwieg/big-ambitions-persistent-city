using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.MovementEquivalence;

/// <summary>Native body lag. Ideal bodies move at nominal pace; Native adds a seeded walker pace of
/// 0.8-1.0x and recurring blocking episodes; SlowHeadCar adds every fourth car body at 0.5x.</summary>
public enum LagModel { Ideal, Native, SlowHeadCar }

public sealed class DriverSettings
{
    public int Pedestrians = 48, Vehicles = 24;
    public double RadiusMetres = 120;
    /// <summary>Share of body acquisitions that fail after the lease was taken (native pool exhausted).</summary>
    public int AcquisitionFailurePercent;
    public LagModel Lag = LagModel.Native;
    public int Seed;
}

/// <summary>Presentation driver double. It calls today's public lease API in the order and under the
/// rules that Runtime/PhysicalPopulation.cs uses for travelling persons: 0.25 s selection over the
/// active-journey index with the same filters and priority order, at most four attempts per tick, a
/// two-second retry, traffic admission before a car body, lease then body acquisition (failed bodies
/// release the lease), per-frame Observe with the 16-corner loop, wait legs from elapsed simulation
/// time, Gley-style reached evidence before a car crosses a road node, the traffic permission clamp and
/// the overshoot release, the door-arrival hold, the 1.4x release radius, release before hidden travel
/// and the observe-before-save pass. Bodies are a position on the committed geometry that moves at a
/// lag-model pace toward its native target; nothing here reads Unity.
/// Not modelled (no lease involved, or F2b's write paths): door queues, optional stops and their
/// departure hold, outdoor scenes, ambient/RLO reservations and the interior view.</summary>
public sealed class PresentationDriver
{
    private sealed class Body
    {
        public bool Car;
        public Position Position;
        public double Pace = 1;
        public double BlockPhase;
        // The native car's reached-waypoint marker (Gley): set when it reaches its target, consumed by Observe.
        public bool HasReached;
        public Position Reached;
        public Position Target;
        public bool HasTarget;
    }
    private sealed class Binding
    {
        public CitizenState Citizen = null!;
        public PhysicalJourneyLease? Lease;
        public Body Body = null!;
        public double ObservedMinute;
        public int TargetLeg = -1;
        public int ReachedCarLeg = -1;
        public Position ReachedCarMarker;
        public readonly PhysicalCornerHandoff CornerHandoff = new();
        public bool AwaitingArrival;
        public double AwaitMinute;
        public Route? AwaitJourney;
    }
    private readonly struct Candidate
    {
        public Candidate(CitizenState citizen, Position position, double lastAttempt, double distance, int rosterIndex)
        { Citizen = citizen; Position = position; LastAttempt = lastAttempt; Distance = distance; RosterIndex = rosterIndex; }
        public CitizenState Citizen { get; }
        public Position Position { get; }
        public double LastAttempt { get; }
        public double Distance { get; }
        public int RosterIndex { get; }
        // Runtime SelectionCandidate.ByPriority: least recently attempted, nearest, ordinal id, roster order.
        public static int Compare(Candidate a, Candidate b)
        {
            int order = a.LastAttempt.CompareTo(b.LastAttempt);
            if (order == 0) order = a.Distance.CompareTo(b.Distance);
            if (order == 0) order = StringComparer.Ordinal.Compare(a.Citizen.Identity.Id, b.Citizen.Identity.Id);
            return order != 0 ? order : a.RosterIndex.CompareTo(b.RosterIndex);
        }
    }

    private readonly CitySimulator simulation;
    private readonly DriverSettings settings;
    private readonly Dictionary<string, Binding> bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> attempted = new(StringComparer.Ordinal);
    private readonly List<Candidate> candidates = new();
    private double realTime, nextSelection;
    private long bodyGeneration, acquisitionDraws;

    public long Acquired, Released, FailedAcquisitions, BlockedSamples, TrafficOvershoots, Observations, LeaseObservations;
    public long BodyFrames, WalkerFrames, CarFrames, ArrivalHolds, HiddenFrames, HiddenReleases;
    public int MaxWalkers, MaxCars;
    public int Walkers { get; private set; }
    public int Cars { get; private set; }
    public int Count => bindings.Count;

    public PresentationDriver(CitySimulator simulation, DriverSettings settings)
    { this.simulation = simulation; this.settings = settings; }

    private static uint Hash(string text, uint salt)
    {
        unchecked
        {
            uint value = 2166136261u ^ salt;
            foreach (char c in text) { value ^= c; value *= 16777619u; }
            value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
            return value;
        }
    }
    private static double Unit(uint hash) => (hash >> 8) / 16777216.0;

    /// <summary>Native movement for this frame, before the simulation advances, under the limits the
    /// previous frame's observations left (Runtime: Gley and NavMesh move, CommandLimit gates cars).</summary>
    public void MoveBodies(double realSeconds, double gameMinutes)
    {
        realTime += realSeconds;
        foreach (var binding in bindings.Values)
        {
            var body = binding.Body;
            if (binding.Lease == null || !body.HasTarget) continue;
            if (!body.Car)
            {
                if (Blocked(body)) continue;
                Step(body, body.Target, GridPlanner.WalkMetresPerMinute * body.Pace * gameMinutes);
                continue;
            }
            // CommandLimit: hold at a reached corner until Observe consumes it, and never past the kernel's permission.
            if (!simulation.TrySamplePhysicalJourney(binding.Lease, out var sample) || sample.Mode != TravelMode.Car
                || !simulation.TryGetTrafficPermission(binding.Citizen.Identity.Id, out var permission)
                || !permission.LinkId.HasValue || permission.IsCompleted) continue;
            double maximum = permission.MaximumNominalMinutes;
            bool hold = binding.ReachedCarLeg == sample.LegIndex || body.HasReached && Position.Distance(body.Reached, sample.To) < 1e-9
                || binding.CornerHandoff.MustHold(sample, maximum);
            if (hold) continue;
            double limit = PhysicalTrafficMotion.MaximumAdvance(sample, body.Position, maximum);
            double metres = Math.Min(limit, GridPlanner.CarMetresPerMinute * body.Pace * gameMinutes);
            if (Step(body, sample.To, metres)) { body.HasReached = true; body.Reached = sample.To; }
        }
    }

    // Recurring blocking: 3 s of every 20 s of real time, at a seeded phase per body.
    private bool Blocked(Body body) => settings.Lag != LagModel.Ideal && (realTime + body.BlockPhase) % 20 < 3;

    // True when the body stands on its target: Gley raises its reached marker for a car there.
    private static bool Step(Body body, Position target, double metres)
    {
        double distance = Position.Distance(body.Position, target);
        if (metres > 0)
        {
            if (distance <= metres) body.Position = target;
            else body.Position = Position.Lerp(body.Position, target, metres / distance);
        }
        return Position.Distance(body.Position, target) <= ReachedMetres;
    }
    public const double ReachedMetres = 1e-6;

    /// <summary>Runtime PhysicalPopulation.Update after AdvancePopulation.</summary>
    public void Update(Position focus, bool hiddenTravel)
    {
        if (hiddenTravel) { HiddenFrames++; Suspend(); return; }
        if (!focus.IsFinite) { Suspend(); return; }
        double radius = settings.RadiusMetres, releaseRadius = settings.RadiusMetres * 1.4;
        foreach (var binding in bindings.Values.ToArray())
        {
            if (!Observe(binding))
            {
                if (!TryAwaitArrival(binding)) { Release(binding); continue; }
                continue;
            }
            if (Position.Distance(binding.Body.Position, focus) > releaseRadius) Release(binding);
        }
        Recount();
        if (realTime < nextSelection) { Tally(); return; }
        nextSelection = realTime + .25;
        int people = Walkers, cars = Cars, attempts = 0;
        candidates.Clear();
        var roster = simulation.ActiveJourneys.BodyCandidates;
        for (int index = 0; index < roster.Count; index++)
        {
            var c = roster[index];
            if (people >= settings.Pedestrians && cars >= settings.Vehicles) break;
            if (!(c.NextEventMinute > simulation.State.Minute) || bindings.ContainsKey(c.Identity.Id)) continue;
            bool tried = attempted.TryGetValue(c.Identity.Id, out double lastAttempt);
            if (tried && !(realTime - lastAttempt >= 2)) continue;
            if (!(c.Phase == CitizenPhase.Travelling && c.Journey != null)) continue;
            Position sampled = simulation.PositionOf(c);
            double distance = Position.Distance(sampled, focus);
            if (!(distance <= radius)) continue;
            candidates.Add(new Candidate(c, sampled, tried ? lastAttempt : double.NegativeInfinity, distance, candidates.Count));
        }
        candidates.Sort(Candidate.Compare);
        foreach (var candidate in candidates)
        {
            var citizen = candidate.Citizen;
            var sample = simulation.SampleJourney(citizen);
            bool car = sample.Mode == TravelMode.Car && !sample.Stationary;
            bool person = sample.Mode == TravelMode.Walk;
            if ((!car && !person) || (car ? cars >= settings.Vehicles : people >= settings.Pedestrians)) continue;
            if (++attempts > 4) break;
            attempted[citizen.Identity.Id] = realTime;
            if (car && (!simulation.TryGetTrafficPermission(citizen.Identity.Id, out var admission)
                || !admission.LinkId.HasValue || admission.IsCompleted)) continue;
            var binding = new Binding { Citizen = citizen, ObservedMinute = simulation.State.Minute };
            if (!simulation.TryAcquirePhysicalJourney(citizen.Identity.Id, out binding.Lease)) continue;
            bool bound = false;
            try
            {
                if (settings.AcquisitionFailurePercent > 0
                    && Hash(citizen.Identity.Id, (uint)(settings.Seed * 7919 + ++acquisitionDraws)) % 100 < (uint)settings.AcquisitionFailurePercent)
                { FailedAcquisitions++; continue; }
                // Runtime: a car spawns at the road sample of its journey pose, a walker at the selected position.
                binding.Body = NewBody(citizen, car, car ? sample.Position : candidate.Position);
                bindings.Add(citizen.Identity.Id, binding); bound = true; Acquired++;
            }
            finally { if (!bound && binding.Lease != null) simulation.ReleasePhysicalJourney(binding.Lease); }
            if (!Observe(binding)) { Release(binding); continue; }
            if (car) cars++; else people++;
        }
        candidates.Clear();
        Recount(); Tally();
    }

    private Body NewBody(CitizenState citizen, bool car, Position at)
    {
        uint draw = Hash(citizen.Identity.Id, (uint)(settings.Seed * 104729 + ++bodyGeneration));
        var body = new Body { Car = car, Position = at, BlockPhase = Unit(Hash(citizen.Identity.Id, (uint)bodyGeneration)) * 20 };
        if (settings.Lag != LagModel.Ideal && !car) body.Pace = 0.8 + 0.2 * Unit(draw);
        if (settings.Lag == LagModel.SlowHeadCar && car && draw % 4 == 0) body.Pace = 0.5;
        return body;
    }

    private void Recount()
    {
        int walkers = 0, cars = 0;
        foreach (var b in bindings.Values) if (b.Body.Car) cars++; else walkers++;
        Walkers = walkers; Cars = cars;
    }
    private void Tally()
    {
        MaxWalkers = Math.Max(MaxWalkers, Walkers); MaxCars = Math.Max(MaxCars, Cars);
        BodyFrames += Walkers + Cars; WalkerFrames += Walkers; CarFrames += Cars;
    }

    /// <summary>Runtime TryAwaitArrival/ObserveArrival: a walker that reached a shop or venue door keeps its
    /// body (no lease) until the simulator processes the arrival, for at most half a game minute.</summary>
    private bool TryAwaitArrival(Binding binding)
    {
        var citizen = binding.Citizen; var lease = binding.Lease;
        if (binding.AwaitingArrival)
            return citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, binding.AwaitJourney)
                && simulation.State.Minute <= binding.AwaitMinute + 0.5;
        if (binding.Body.Car || lease == null || citizen.Phase != CitizenPhase.Travelling || simulation.IsPhysicalJourneyValid(lease)
            || (citizen.DestinationActivity != Activity.Shop && citizen.DestinationActivity != Activity.Leisure)) return false;
        var destination = simulation.State.Places.FirstOrDefault(p => p.Id == citizen.DestinationId);
        if (destination == null || Position.Distance(binding.Body.Position, destination.Position) > 3) return false;
        binding.Lease = null;
        simulation.ReleasePhysicalJourney(lease);
        binding.AwaitingArrival = true; binding.AwaitMinute = simulation.State.Minute; binding.AwaitJourney = citizen.Journey;
        binding.Body.HasTarget = false; ArrivalHolds++;
        return true;
    }

    private bool Observe(Binding binding)
    {
        Observations++;
        if (binding.Lease == null) return binding.AwaitingArrival && TryAwaitArrival(binding);
        if (!simulation.TrySamplePhysicalJourney(binding.Lease, out var sample)) return false;
        double elapsed = Math.Max(0, simulation.State.Minute - binding.ObservedMinute);
        binding.ObservedMinute = simulation.State.Minute;
        var body = binding.Body;
        Position actual = body.Position;
        for (int corner = 0; corner < 16; corner++)
        {
            if (!body.Car && sample.Mode != TravelMode.Walk || body.Car && sample.Mode != TravelMode.Car) return false;
            if (body.Car && !sample.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal)) return false;
            if (sample.Stationary)
            {
                body.HasTarget = false;
                double waitProgress = PhysicalProgress.AdvanceWait(sample, elapsed);
                double wait = waitProgress - sample.ElapsedMinutes;
                LeaseObservations++;
                if (!simulation.ObservePhysicalJourney(binding.Lease, waitProgress)
                    || !simulation.TrySamplePhysicalJourney(binding.Lease, out var afterWait)) return false;
                elapsed -= wait;
                if (afterWait.LegIndex == sample.LegIndex) return true;
                sample = afterWait;
                continue;
            }
            if (body.Car && binding.ReachedCarLeg != sample.LegIndex && body.HasReached && Position.Distance(body.Reached, sample.To) < 1e-9)
            { binding.ReachedCarLeg = sample.LegIndex; binding.ReachedCarMarker = body.Reached; body.HasReached = false; }
            bool nativeCorner = body.Car && binding.ReachedCarLeg == sample.LegIndex;
            if (!PhysicalProgress.TryProject(sample, actual, nativeCorner, out double progress))
            { BlockedSamples++; return true; }
            if (body.Car && !nativeCorner && progress >= sample.ElapsedMinutes + sample.RemainingLegMinutes - 1e-9)
                progress = sample.ElapsedMinutes;
            progress = Math.Min(progress, simulation.State.Minute);
            if (body.Car && simulation.TryGetTrafficPermission(binding.Citizen.Identity.Id, out var permission))
            {
                if (permission.MaximumNominalMinutes <= simulation.State.Minute
                    && (PhysicalTrafficCornerPermission.TryReconcileEndpoint(sample, nativeCorner, progress,
                        permission.MaximumNominalMinutes, out double reconciled)
                        || PhysicalTrafficCornerPermission.TryReconcileNormalizedBoundary(sample, binding.Lease.ObservedMinutes,
                            progress, permission.MaximumNominalMinutes, out reconciled)))
                    progress = reconciled;
                if (progress > permission.MaximumNominalMinutes)
                {
                    if (binding.CornerHandoff.TryHold(sample, actual, permission.MaximumNominalMinutes,
                        binding.Lease.ObservedMinutes, out double held))
                        progress = held;
                    else if (PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, progress,
                        permission.MaximumNominalMinutes, out double rounded)) progress = rounded;
                    else { TrafficOvershoots++; return false; }
                }
            }
            if (progress == sample.ElapsedMinutes) BlockedSamples++;
            LeaseObservations++;
            if (!simulation.ObservePhysicalJourney(binding.Lease, progress)) return false;
            if (!simulation.TrySamplePhysicalJourney(binding.Lease, out var updated)) return false;
            if (updated.LegIndex == sample.LegIndex)
            {
                // Runtime SetTarget(sample.To) once per leg; the double's path request never fails.
                if (!body.Car && binding.TargetLeg != sample.LegIndex) binding.TargetLeg = sample.LegIndex;
                body.Target = sample.To; body.HasTarget = true;
                return true;
            }
            if (body.Car && nativeCorner) binding.CornerHandoff.Begin(sample, updated, binding.ReachedCarMarker);
            sample = updated;
        }
        return true;
    }

    /// <summary>Runtime ObserveForSave: observe every body, keep door-arrival holds, release the rest.</summary>
    public void BeforeSave()
    {
        foreach (var binding in bindings.Values.ToArray()) if (!Observe(binding) && !TryAwaitArrival(binding)) Release(binding);
    }

    /// <summary>Runtime BeforeAdvance during hidden travel (SuspendBeforeNativeFastForward): observe every
    /// body for save, then release them all, before the simulation advances the hidden slice.</summary>
    public void SuspendBeforeHiddenAdvance()
    {
        HiddenReleases += bindings.Count;
        BeforeSave();
        Suspend();
    }

    public void Suspend()
    {
        foreach (var binding in bindings.Values.ToArray()) Release(binding);
        Recount();
    }

    private void Release(Binding binding)
    {
        if (binding.Lease != null) simulation.ReleasePhysicalJourney(binding.Lease);
        bindings.Remove(binding.Citizen.Identity.Id);
        attempted[binding.Citizen.Identity.Id] = realTime;
        Released++;
    }
}
