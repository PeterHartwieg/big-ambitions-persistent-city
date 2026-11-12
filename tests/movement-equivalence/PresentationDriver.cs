using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.MovementEquivalence;

public enum LagModel { Ideal, Native, SlowHeadCar }

public enum CarBodies { Borrowed, Own }

public sealed class DriverSettings
{
    public int Pedestrians = 48, Vehicles = 24;
    public double RadiusMetres = 120;
    public int AcquisitionFailurePercent;
    public LagModel Lag = LagModel.Native;
    public int Seed;
    public bool PoseAim;
    public CarBodies Cars = CarBodies.Borrowed;
}

public sealed class PresentationDriver
{
    private sealed class Body
    {
        public bool Car;
        public Position Position;
        public double Pace = 1;
        public double BlockPhase;
        public bool HasReached;
        public Position Reached;
        public Position Target;
        public bool HasTarget;
        public double Speed;
    }
    private sealed class Binding
    {
        public CitizenState Citizen = null!;
        public Body Body = null!;
        public int ReachedCarLeg = -1;
        public Position ReachedCarMarker;
        public readonly PhysicalCornerHandoff CornerHandoff = new();
        public Route? Journey;
        public double BodyMinutes, PaceFactor, TailUntil, LagMetres;
        public bool Tail, Handoff;
        public int TargetLeg = -1;
        public bool TargetIsPose;
        public Position TargetPoint;
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

    public long Acquired, Released, FailedAcquisitions, Observations;
    public long BodyFrames, WalkerFrames, CarFrames, ArrivalTails, HiddenFrames, HiddenReleases;
    public readonly WalkerMovementTelemetry Walking = new();
    public int MaxWalkers, MaxCars;
    public long VisitorAcquired, VisitorFrames;
    public int MaxVisitorBodies;
    public readonly SortedSet<string> AcquiredVisitors = new(StringComparer.Ordinal);
    public int VisitorBodies { get; private set; }
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

    public void MoveBodies(double realSeconds, double gameMinutes)
    {
        realTime += realSeconds;
        foreach (var binding in bindings.Values)
        {
            var body = binding.Body;
            if (!body.Car)
            {
                if (binding.Journey == null || !body.HasTarget || Blocked(body)) continue;
                Step(body, body.Target, Math.Min(binding.LagMetres, GridPlanner.WalkMetresPerMinute * body.Pace * binding.PaceFactor * gameMinutes));
                continue;
            }
            if (!CarCommand(binding, out var own, out double cap) || own.Stationary || binding.CornerHandoff.MustHold(own, cap)) continue;
            var leg = own;
            if (binding.ReachedCarLeg != own.LegIndex && body.HasReached && Position.Distance(body.Reached, own.To) < 1e-9)
            {
                var next = WalkerFollow.Sample(binding.Journey!, own.ElapsedMinutes + own.RemainingLegMinutes);
                if (next.Mode != TravelMode.Car || next.Stationary || next.LegIndex == own.LegIndex) continue;
                leg = next;
            }
            double limit = PhysicalTrafficMotion.MaximumAdvance(leg, body.Position, cap);
            double metres;
            if (settings.Cars == CarBodies.Own)
            {
                var step = OwnCarMotion.Step(0, Position.Distance(body.Position, leg.To), body.Speed,
                    new PhysicalTrafficCommandLimit(leg.Direction, limit), realSeconds * body.Pace);
                body.Speed = step.Speed; metres = step.Moved;
            }
            else metres = Math.Min(limit, GridPlanner.CarMetresPerMinute * body.Pace * gameMinutes);
            if (Step(body, leg.To, metres)) { body.HasReached = true; body.Reached = leg.To; }
        }
    }

    private bool CarCommand(Binding binding, out PhysicalJourneySample own, out double cap)
    {
        own = default; cap = 0;
        var c = binding.Citizen;
        if (binding.Journey == null || c.Phase != CitizenPhase.Travelling || !ReferenceEquals(c.Journey, binding.Journey)) return false;
        own = WalkerFollow.Sample(binding.Journey, binding.BodyMinutes);
        if (own.Mode != TravelMode.Car || !own.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal)) return false;
        cap = CarFollow.Cap(binding.Journey, binding.BodyMinutes, LogicalCarMinutes(c));
        return true;
    }
    private double LogicalCarMinutes(CitizenState c)
        => simulation.TryGetTrafficPermission(c.Identity.Id, out var p) && p.LinkId.HasValue && !p.IsCompleted
            ? p.MaximumNominalMinutes : simulation.SampleJourney(c).ElapsedMinutes;

    private bool Blocked(Body body) => settings.Lag != LagModel.Ideal && (realTime + body.BlockPhase) % 20 < 3;

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
            var binding = new Binding { Citizen = citizen, Journey = citizen.Journey, BodyMinutes = sample.ElapsedMinutes };
            if (settings.AcquisitionFailurePercent > 0
                && Hash(citizen.Identity.Id, (uint)(settings.Seed * 7919 + ++acquisitionDraws)) % 100 < (uint)settings.AcquisitionFailurePercent)
            { FailedAcquisitions++; continue; }
            binding.Body = NewBody(citizen, car, car ? sample.Position : candidate.Position);
            bindings.Add(citizen.Identity.Id, binding); Acquired++;
            if (citizen.IsVisitor) { VisitorAcquired++; AcquiredVisitors.Add(citizen.Identity.Id); }
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
        int walkers = 0, cars = 0, visitors = 0;
        foreach (var b in bindings.Values) { if (b.Body.Car) cars++; else walkers++; if (b.Citizen.IsVisitor) visitors++; }
        Walkers = walkers; Cars = cars; VisitorBodies = visitors;
    }
    private void Tally()
    {
        MaxWalkers = Math.Max(MaxWalkers, Walkers); MaxCars = Math.Max(MaxCars, Cars); MaxVisitorBodies = Math.Max(MaxVisitorBodies, VisitorBodies);
        VisitorFrames += VisitorBodies;
        BodyFrames += Walkers + Cars; WalkerFrames += Walkers; CarFrames += Cars;
    }

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
        var own = WalkerFollow.Sample(route, binding.BodyMinutes);
        if (binding.LagFrame != frame)
        {
            binding.LagFrame = frame; Walking.RecordLag(lag);
            if (following) Walking.RecordAhead(WalkerFollow.AheadMetres(own, logical, body.Position));
        }
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
            body.Position = logical.Position; binding.BodyMinutes = logical.ElapsedMinutes; binding.TargetLeg = -1;
            Walking.Record(frame, citizen.Identity.Id, WalkerCorrectionKind.Move, before, body.Position, false);
            own = WalkerFollow.Sample(route, binding.BodyMinutes); lag = 0;
        }
        Aim(binding, own, lag, logical);
        return true;
    }

    private void Aim(Binding binding, PhysicalJourneySample own, double lag, PhysicalJourneySample logical)
    {
        binding.PaceFactor = own.Stationary || own.AtEnd ? 0 : WalkerFollow.PaceFactor(lag);
        binding.LagMetres = lag;
        if (own.Stationary || own.AtEnd) return;
        bool pose = false;
        if (settings.PoseAim)
        {
            pose = WalkerFollow.PoseOnLeg(own, logical);
            Position goal = pose ? logical.Position : own.To;
            bool corner = Position.Distance(goal, own.To) <= 1e-6;
            bool retarget = binding.TargetLeg != own.LegIndex || binding.TargetIsPose != pose
                || pose && (Position.Distance(goal, binding.TargetPoint) >= WalkerFollow.RetargetMetres
                    || corner && Position.Distance(goal, binding.TargetPoint) > 1e-6);
            if (!retarget) return;
            if (pose && lag <= WalkerFollow.StopMetres) { binding.PaceFactor = 0; return; }
        }
        else if (!WalkerFollow.NeedsPath(own, logical, lag, binding.TargetLeg, binding.TargetIsPose, binding.TargetPoint, true, out _)) return;
        Walking.PathRequests++;
        binding.TargetLeg = own.LegIndex; binding.TargetIsPose = pose; binding.TargetPoint = pose ? logical.Position : own.To;
        binding.Body.Target = binding.TargetPoint; binding.Body.HasTarget = true;
    }

    private bool Observe(Binding binding)
    {
        Observations++;
        if (!binding.Body.Car) return binding.Journey != null && ObserveWalker(binding);
        return binding.Journey != null && ObserveCar(binding);
    }

    private bool ObserveCar(Binding binding)
    {
        var c = binding.Citizen; var route = binding.Journey!; var body = binding.Body;
        if (c.Phase != CitizenPhase.Travelling || !ReferenceEquals(c.Journey, route))
        {
            Walking.Record(frame, c.Identity.Id, WalkerCorrectionKind.LagRelease, body.Position, route.Legs[route.Legs.Count - 1].To, false, car: true);
            return false;
        }
        var logical = simulation.SampleJourney(c);
        bool admitted = simulation.TryGetTrafficPermission(c.Identity.Id, out var permission) && permission.LinkId.HasValue && !permission.IsCompleted;
        double logicalMinutes = admitted ? permission.MaximumNominalMinutes : logical.ElapsedMinutes;
        var own = WalkerFollow.Sample(route, binding.BodyMinutes);
        if (own.Mode == TravelMode.Car && !own.Stationary && binding.ReachedCarLeg != own.LegIndex
            && body.HasReached && Position.Distance(body.Reached, own.To) < 1e-9)
        { binding.ReachedCarLeg = own.LegIndex; binding.ReachedCarMarker = body.Reached; body.HasReached = false; }
        binding.BodyMinutes = CarFollow.Advance(route, binding.BodyMinutes, CarFollow.Cap(route, binding.BodyMinutes, logicalMinutes),
            body.Position, binding.ReachedCarLeg);
        var after = WalkerFollow.Sample(route, binding.BodyMinutes);
        if (after.LegIndex == own.LegIndex + 1 && binding.ReachedCarLeg == own.LegIndex)
            binding.CornerHandoff.Begin(own, after, binding.ReachedCarMarker);
        double lag = WalkerFollow.RouteMetres(route, binding.BodyMinutes, logical.ElapsedMinutes);
        if (binding.LagFrame != frame)
        {
            binding.LagFrame = frame; Walking.RecordCarLag(lag);
            if (lag > CarFollow.CatchUpMetres) Walking.CarCatchUpFrames++;
        }
        if (after.AtEnd || !CarFollow.IsRoad(after)) { binding.Handoff = true; Walking.Handoffs++; Walking.CarHandoffs++; return false; }
        if (!CarFollow.NeedsCorrection(lag, CarFollow.SameCarRun(route, binding.BodyMinutes, logical.ElapsedMinutes))) return true;
        binding.Handoff = true;
        if (logical.Mode != TravelMode.Car && lag <= CarFollow.CorrectionMetres) { Walking.Handoffs++; Walking.CarHandoffs++; return false; }
        Walking.Record(frame, c.Identity.Id, WalkerCorrectionKind.LagRelease, body.Position, logical.Position, false, car: true);
        return false;
    }

    public List<string> BoundVisitors()
    {
        var ids = bindings.Values.Where(b => b.Citizen.IsVisitor).Select(b => b.Citizen.Identity.Id).ToList();
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    public void Followed(List<(CitizenState Citizen, bool Car)> into)
    {
        foreach (var b in bindings.Values)
            if (b.Journey != null && !b.Tail && b.Citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(b.Citizen.Journey, b.Journey))
                into.Add((b.Citizen, b.Body.Car));
        into.Sort((x, y) => StringComparer.Ordinal.Compare(x.Citizen.Identity.Id, y.Citizen.Identity.Id));
    }

    public void BeforeSave()
    {
        foreach (var binding in bindings.Values.ToArray()) if (!Observe(binding)) Release(binding);
    }

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
        bindings.Remove(binding.Citizen.Identity.Id);
        if (binding.Handoff) attempted.Remove(binding.Citizen.Identity.Id);
        else attempted[binding.Citizen.Identity.Id] = realTime;
        Released++;
    }
}
