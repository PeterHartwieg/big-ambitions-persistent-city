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

/// <summary>Presentation driver double. It follows the rules that Runtime/PhysicalPopulation.cs uses for
/// travelling persons: 0.25 s selection over the active-journey index with the same filters and priority
/// order, at most four attempts per tick, a two-second retry, traffic admission before a car body, and for
/// cars the lease then body acquisition (failed bodies release the lease), per-frame Observe with the
/// 16-corner loop, Gley-style reached evidence before a car crosses a road node, the traffic permission
/// clamp and the overshoot release. F2c walkers take no lease: they follow the logical pose with
/// WalkerFollow (catch-up pace, never ahead), are moved to the logical pose beyond 8 m (the double has no
/// camera, so every body is off screen), hand over at a mode boundary without the retry cooldown, and walk
/// a 10 s arrival tail. Then the 1.4x release radius, release before hidden travel and the
/// observe-before-save pass. Bodies are a position on the committed geometry that moves at a lag-model
/// pace toward its native target; nothing here reads Unity.
/// Not modelled (no lease involved, or F2b's write paths): door queues, optional stops and their
/// departure tail, outdoor scenes, ambient/RLO reservations and the interior view.</summary>
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
        // F2c walker follow.
        public Route? Journey;
        public double BodyMinutes, PaceFactor, TailUntil, LagMetres;
        public bool Tail, Handoff;
        public long LagFrame = -1;
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
    private long bodyGeneration, acquisitionDraws, frame;

    public long Acquired, Released, FailedAcquisitions, BlockedSamples, TrafficOvershoots, Observations, LeaseObservations;
    public long BodyFrames, WalkerFrames, CarFrames, ArrivalTails, HiddenFrames, HiddenReleases;
    /// <summary>F2c walker lag, corrections and handoffs, as Runtime's pc movement status reports them.</summary>
    public readonly WalkerMovementTelemetry Walking = new();
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
            if (!body.Car)
            {
                if (binding.Journey == null || !body.HasTarget || Blocked(body)) continue;
                // Runtime WalkerFollow.CommandSpeed: never more than the lag in one frame.
                Step(body, body.Target, Math.Min(binding.LagMetres, GridPlanner.WalkMetresPerMinute * body.Pace * binding.PaceFactor * gameMinutes));
                continue;
            }
            if (binding.Lease == null || !body.HasTarget) continue;
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
        frame++;
        if (hiddenTravel) { HiddenFrames++; Suspend(); return; }
        if (!focus.IsFinite) { Suspend(); return; }
        double radius = settings.RadiusMetres, releaseRadius = settings.RadiusMetres * 1.4;
        foreach (var binding in bindings.Values.ToArray())
        {
            if (!Observe(binding)) { Release(binding); continue; }
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
            // F2c: only a traffic leg is leased; a walker follows its journey from the logical pose.
            if (car && !simulation.TryAcquirePhysicalJourney(citizen.Identity.Id, out binding.Lease)) continue;
            if (!car) { binding.Journey = citizen.Journey; binding.BodyMinutes = sample.ElapsedMinutes; }
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

    /// <summary>Runtime ObserveWalker for travelling persons: follow the logical pose, correct beyond 8 m or on a
    /// mode change (always off screen here), hand over at a mode boundary, then the arrival tail.</summary>
    private bool ObserveWalker(Binding binding)
    {
        var citizen = binding.Citizen; var route = binding.Journey!; var body = binding.Body;
        bool following = citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, route);
        if (!following && !binding.Tail)
        {
            if (citizen.Phase == CitizenPhase.Travelling || citizen.Phase == CitizenPhase.WaitingForRoute) return false;
            binding.Tail = true; binding.TailUntil = realTime + WalkerFollow.ArrivalTailSeconds; ArrivalTails++;
        }
        var logical = following ? simulation.SampleJourney(citizen) : WalkerFollow.Sample(route, route.Minutes);
        binding.BodyMinutes = WalkerFollow.Advance(route, binding.BodyMinutes, logical.ElapsedMinutes, body.Position);
        double lag = WalkerFollow.RouteMetres(route, binding.BodyMinutes, logical.ElapsedMinutes);
        if (binding.LagFrame != frame) { binding.LagFrame = frame; Walking.RecordLag(lag); }
        var own = WalkerFollow.Sample(route, binding.BodyMinutes);
        if (binding.Tail)
        {
            if (own.AtEnd || realTime >= binding.TailUntil || own.Mode != TravelMode.Walk) return false;
            Aim(binding, own, Math.Max(lag, 2 * WalkerFollow.StopMetres), WalkerFollow.Sample(route, route.Minutes));
            return true;
        }
        if (own.Mode != TravelMode.Walk) { binding.Handoff = true; Walking.Handoffs++; return false; }
        if (WalkerFollow.NeedsCorrection(lag, logical.Mode))
        {
            Position before = body.Position;
            if (logical.Mode != TravelMode.Walk)
            {
                binding.Handoff = true;
                if (lag <= WalkerFollow.CorrectionMetres) { Walking.Handoffs++; return false; }
                Walking.Record(frame, citizen.Identity.Id, WalkerCorrectionKind.LagRelease, before, logical.Position, false);
                return false;
            }
            body.Position = logical.Position; binding.BodyMinutes = logical.ElapsedMinutes;
            Walking.Record(frame, citizen.Identity.Id, WalkerCorrectionKind.Move, before, body.Position, false);
            own = WalkerFollow.Sample(route, binding.BodyMinutes); lag = 0;
        }
        Aim(binding, own, lag, logical);
        return true;
    }

    // Runtime WalkLeg: the destination is the logical pose on its own leg, the leg's corner on an earlier one.
    private static void Aim(Binding binding, PhysicalJourneySample own, double lag, PhysicalJourneySample logical)
    {
        binding.PaceFactor = own.Stationary || own.AtEnd ? 0 : WalkerFollow.PaceFactor(lag);
        binding.LagMetres = lag;
        bool pose = logical.Mode == TravelMode.Walk && !logical.Stationary && own.LegIndex == logical.LegIndex;
        binding.Body.Target = pose ? logical.Position : own.To; binding.Body.HasTarget = true;
    }

    private bool Observe(Binding binding)
    {
        Observations++;
        if (!binding.Body.Car) return binding.Journey != null && ObserveWalker(binding);
        if (binding.Lease == null) return false;
        if (!simulation.TrySamplePhysicalJourney(binding.Lease, out var sample))
        {
            // Traffic completion ended the lease; a walk after parking re-acquires a walker at once.
            var c = binding.Citizen;
            if (c.Phase == CitizenPhase.Travelling && c.Journey != null && simulation.SampleJourney(c).Mode == TravelMode.Walk)
            { binding.Handoff = true; Walking.Handoffs++; }
            return false;
        }
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

    /// <summary>Runtime ObserveForSave: observe every body and release those whose follow ended.</summary>
    public void BeforeSave()
    {
        foreach (var binding in bindings.Values.ToArray()) if (!Observe(binding)) Release(binding);
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
        if (binding.Handoff) attempted.Remove(binding.Citizen.Identity.Id);
        else attempted[binding.Citizen.Identity.Id] = realTime;
        Released++;
    }
}
