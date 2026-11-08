using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>An exclusive, simulator-local authority over one committed journey. Never persist this handle.</summary>
    public sealed class PhysicalJourneyLease
    {
        internal PhysicalJourneyLease(CitySimulator owner, long generation, CitizenState citizen)
        { Owner = owner; Generation = generation; Citizen = citizen; Identity = citizen.Identity; Route = citizen.Journey!; CitizenId = citizen.Identity.Id; }
        internal CitySimulator Owner { get; }
        internal CitizenState Citizen { get; }
        internal CitizenIdentity Identity { get; }
        internal Route Route { get; }
        internal double Progress;
        /// <summary>Exact credited route minutes, before geometric boundary normalization in a sample.</summary>
        public double ObservedMinutes => Progress;
        public string CitizenId { get; }
        public long Generation { get; }
    }

    public readonly struct PhysicalJourneySample
    {
        internal PhysicalJourneySample(double elapsed, double total, int leg, double fraction, RouteLeg segment, Position position, Position? car, TravelMode? nextMode)
        { ElapsedMinutes = elapsed; TotalMinutes = total; LegIndex = leg; LegFraction = fraction; Mode = segment.Mode;
          From = segment.From; To = segment.To; Position = position; CarPosition = car; ServiceId = segment.ServiceId; LegMinutes = segment.Minutes; NextMode = nextMode; }
        public double ElapsedMinutes { get; }
        public double TotalMinutes { get; }
        public int LegIndex { get; }
        public double LegFraction { get; }
        public TravelMode Mode { get; }
        public string ServiceId { get; }
        public Position From { get; }
        public Position To { get; }
        public Position Position { get; }
        public Position? CarPosition { get; }
        public double LegMinutes { get; }
        public double RemainingMinutes => Math.Max(0, TotalMinutes - ElapsedMinutes);
        public double RemainingLegMinutes => LegMinutes * (1 - LegFraction);
        public bool AtEnd => ElapsedMinutes >= TotalMinutes;
        public TravelMode? NextMode { get; }
        public Position Direction
        {
            get
            {
                double distance = PersistentCity.Core.Position.Distance(From, To);
                return distance <= 0.000001 ? new Position(0, 0, 0)
                    : new Position((To.X - From.X) / distance, (To.Y - From.Y) / distance, (To.Z - From.Z) / distance);
            }
        }
        public bool Stationary => PersistentCity.Core.Position.Distance(From, To) <= 0.000001;
    }

    public sealed partial class CitySimulator
    {
        private readonly Dictionary<string, PhysicalJourneyLease> physicalJourneys = new Dictionary<string, PhysicalJourneyLease>(StringComparer.Ordinal);
        private long physicalGeneration;

        /// <summary>Acquire after advancing the city, for a journey on a traffic leg only (F2c: walk, wait and transit
        /// legs are never leased; their bodies follow the logical pose). Traffic progress then follows the body's
        /// reports until observed or released (F2d removes this).</summary>
        public bool TryAcquirePhysicalJourney(string citizenId, out PhysicalJourneyLease? lease)
        {
            lease = null;
            if (citizenId == null) throw new ArgumentNullException(nameof(citizenId));
            if (physicalJourneys.ContainsKey(citizenId) || !citizenEvents.TryGetValue(citizenId, out var item) || InProgress(item)) return false;
            var c = item.Citizen;
            // Visitors receive no body or lease before F5i (plan B5): their journeys stay purely logical.
            if (!ActiveJourneyIndex.ReceivesBody(c)) return false;
            if (c.Phase != CitizenPhase.Travelling || c.Journey == null || c.Identity.Id != citizenId || !CurrentCitizenSlot(item, c)) return false;
            if (!trafficCitizens.ContainsKey(citizenId)) return false;
            double progress = JourneyProgress(c);
            if (progress < 0 || progress >= c.Journey.Minutes) return false;
            if (!traffic!.TryGetVehicle(citizenId, out var trafficPosition) || !trafficPosition.LinkId.HasValue) return false;
            ValidateRoute(c.Journey, places[c.PlaceId].Position, places[c.DestinationId].Position);
            ValidateCarRoute(c.Journey, c);
            lease = new PhysicalJourneyLease(this, checked(++physicalGeneration), c) { Progress = progress };
            traffic.SetPhysicalControl(citizenId, true);
            physicalJourneys.Add(citizenId, lease);
            return true;
        }

        public bool IsPhysicalJourneyValid(PhysicalJourneyLease lease) => ValidPhysicalLease(lease);

        private bool ValidPhysicalLease(PhysicalJourneyLease? lease) => lease != null && ReferenceEquals(lease.Owner, this)
            && physicalJourneys.TryGetValue(lease.CitizenId, out var current) && ReferenceEquals(current, lease)
            && ReferenceEquals(lease.Citizen.Identity, lease.Identity) && lease.Citizen.Identity.Id == lease.CitizenId
            && ReferenceEquals(lease.Citizen.Journey, lease.Route) && lease.Citizen.Phase == CitizenPhase.Travelling
            && citizenEvents.TryGetValue(lease.CitizenId, out var item) && ReferenceEquals(item.Citizen, lease.Citizen)
            && CurrentCitizenSlot(item, lease.Citizen);

        private bool CurrentCitizenSlot(Due item, CitizenState citizen) => item.ListIndex >= 0
            ? item.ListIndex < State.Citizens.Count && ReferenceEquals(State.Citizens[item.ListIndex], citizen)
            : ~item.ListIndex < State.VisitorRoutines.Count && ReferenceEquals(State.VisitorRoutines[~item.ListIndex], citizen);

        /// <summary>
        /// Report measured progress along the persisted legs, expressed in their nominal route minutes.
        /// Equal progress means blocked/waiting. Call after AdvanceTo; progress cannot exceed the city clock
        /// because schema 4 represents it as a nonnegative departure time. No admission happens here.
        /// </summary>
        public bool ObservePhysicalJourney(PhysicalJourneyLease lease, double routeElapsedMinutes)
        {
            if (!Position.Finite(routeElapsedMinutes) || routeElapsedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(routeElapsedMinutes));
            if (!ValidPhysicalLease(lease)) return false;
            if (routeElapsedMinutes < lease.Progress || routeElapsedMinutes > lease.Route.Minutes || routeElapsedMinutes > State.Minute)
                throw new ArgumentOutOfRangeException(nameof(routeElapsedMinutes), "Progress must be monotonic and inside the route and city clock.");
            // Leases exist only on traffic legs, and traffic completion ends them (TrafficCompleted).
            if (!trafficCitizens.ContainsKey(lease.CitizenId)) return false;
            double accepted = traffic!.ReportPhysicalProgress(lease.CitizenId, routeElapsedMinutes);
            // The report can complete the traffic leg, which ends the lease and resumes the logical walk.
            if (!ValidPhysicalLease(lease)) return false;
            // The adapter must constrain commanded movement before this boundary. Refuse a
            // material physical overshoot instead of crediting movement through a queue.
            if (routeElapsedMinutes - accepted > 1e-7)
            { lease.Progress = accepted; RebasePhysicalJourney(lease); return false; }
            routeElapsedMinutes = accepted;
            lease.Progress = routeElapsedMinutes;
            RebasePhysicalJourney(lease);
            // Arrival runs through the ordinary due queue (including its budget and opening checks).
            if (routeElapsedMinutes == lease.Route.Minutes) ReturnPhysicalEvent(lease);
            return true;
        }

        /// <summary>Resume background travel at the last observed point with the remaining route duration.</summary>
        public bool ReleasePhysicalJourney(PhysicalJourneyLease lease)
        {
            if (!ValidPhysicalLease(lease)) return false;
            RebasePhysicalJourney(lease);
            if (trafficCitizens.ContainsKey(lease.CitizenId)) traffic!.SetPhysicalControl(lease.CitizenId, false);
            ReturnPhysicalEvent(lease);
            return true;
        }

        public bool TrySamplePhysicalJourney(PhysicalJourneyLease lease, out PhysicalJourneySample sample)
        {
            sample = default;
            if (!ValidPhysicalLease(lease)) return false;
            sample = SampleJourney(lease.Citizen, lease.Progress);
            return true;
        }

        /// <summary>Sample committed geometry, including zero-distance preparation/wait legs and the parked/driven car.</summary>
        public PhysicalJourneySample SampleJourney(CitizenState citizen)
        {
            if (citizen == null) throw new ArgumentNullException(nameof(citizen));
            double elapsed = JourneyProgress(citizen);
            if (citizen.Journey != null) elapsed = NormalizeJourneyElapsed(elapsed, citizen.Journey.Minutes, State.Minute);
            return SampleJourney(citizen, elapsed);
        }

        // A completed lease stores departure = clock - duration. Reversing that subtraction can
        // exceed duration by an ulp at a large city clock; this is not evidence of extra travel.
        internal static double JourneyClockTolerance(double clock, double total) =>
            4 * 2.2204460492503131e-16 * Math.Max(1, Math.Max(Math.Abs(clock), Math.Abs(total)));

        internal static double NormalizeJourneyElapsed(double elapsed, double total, double clock)
        {
            if (Math.Abs(elapsed - total) <= JourneyClockTolerance(clock, total)) return total;
            return elapsed;
        }

        public PhysicalJourneySample SampleJourney(CitizenState citizen, double routeElapsedMinutes)
        {
            if (citizen == null) throw new ArgumentNullException(nameof(citizen));
            var route = citizen.Journey;
            if (citizen.Phase != CitizenPhase.Travelling || route == null || route.Legs.Count == 0)
                throw new InvalidOperationException("Citizen has no committed journey.");
            return SampleRoute(route, routeElapsedMinutes, citizen.Car?.ParkedPosition);
        }

        // Shared pure sampling keeps clock-retime validation on the exact physical pose contract.
        internal static PhysicalJourneySample SampleRoute(Route route, double routeElapsedMinutes, Position? parkedCar)
        {
            double totalMinutes = route.Minutes;
            if (!Position.Finite(routeElapsedMinutes) || routeElapsedMinutes < 0 || routeElapsedMinutes > totalMinutes)
                throw new ArgumentOutOfRangeException(nameof(routeElapsedMinutes));
            double remaining = routeElapsedMinutes;
            double sampledElapsed = routeElapsedMinutes;
            Position? car = parkedCar;
            for (int i = 0; i < route.Legs.Count; i++)
            {
                var leg = route.Legs[i];
                // Summing fractional legs and then subtracting them can leave an endpoint a few
                // ulps short. Snap only a negligible fraction of this leg, so a reached corner
                // cannot remain leased forever while the native body has already stopped there.
                double boundaryTolerance = Math.Min(leg.Minutes * 1e-9, Math.Max(1e-12, Math.Abs(routeElapsedMinutes) * 1e-14));
                if (Math.Abs(remaining - leg.Minutes) <= boundaryTolerance)
                {
                    // Endpoint normalization may move the geometry back by a few ulps when the
                    // measured progress was just beyond the corner. Never move its clock back.
                    sampledElapsed = Math.Max(routeElapsedMinutes, sampledElapsed + leg.Minutes - remaining);
                    remaining = leg.Minutes;
                }
                // At a boundary, expose the next leg (important for walk/car/transit handoffs).
                if (remaining < leg.Minutes || i == route.Legs.Count - 1)
                {
                    double fraction = Math.Min(1, remaining / leg.Minutes);
                    var position = Position.Lerp(leg.From, leg.To, fraction);
                    if (leg.Mode == TravelMode.Car) car = position;
                    return new PhysicalJourneySample(i == route.Legs.Count - 1 && fraction == 1 ? totalMinutes : Math.Min(totalMinutes, Math.Max(routeElapsedMinutes, sampledElapsed)), totalMinutes, i, fraction, leg, position, car, i + 1 < route.Legs.Count ? route.Legs[i + 1].Mode : (TravelMode?)null);
                }
                if (leg.Mode == TravelMode.Car) car = leg.To;
                remaining -= leg.Minutes;
            }
            throw new InvalidOperationException("Invalid route.");
        }

        private void ReturnPhysicalEvent(PhysicalJourneyLease lease)
        {
            physicalJourneys.Remove(lease.CitizenId);
            if (trafficCitizens.ContainsKey(lease.CitizenId)) return;
            var item = citizenEvents[lease.CitizenId];
            item.Minute = ScheduledMinute(item.Citizen);
            if (!due.Add(item)) throw new InvalidOperationException("Duplicate physical journey event.");
        }

        private void RebasePhysicalJourney(PhysicalJourneyLease lease)
        {
            double totalMinutes = lease.Route.Minutes;
            lease.Citizen.DepartureMinute = State.Minute - lease.Progress;
            lease.Citizen.NextEventMinute = lease.Progress == totalMinutes
                ? State.Minute : lease.Citizen.DepartureMinute + totalMinutes;
        }

        private void ValidatePhysicalClock(double target)
        {
            foreach (var lease in physicalJourneys.Values)
            {
                if (!ValidPhysicalLease(lease)) throw new InvalidOperationException("Physical journey ownership changed outside the simulator.");
                if (target + (lease.Route.Minutes - lease.Progress) > 1e9)
                    throw new ArgumentOutOfRangeException(nameof(target), "Delayed arrival exceeds the supported clock.");
            }
        }

        private void SynchronizePhysicalJourneys()
        {
            foreach (var lease in physicalJourneys.Values) RebasePhysicalJourney(lease);
        }
    }

    /// <summary>
    /// F2c walker follower (docs/plans/f2-movement.md Decision 2). Pure presentation arithmetic: a walker body tracks its own
    /// progress along the committed route, never ahead of the logical pose, and Runtime turns the lag into a pace or a
    /// correction. Nothing here writes simulator state. The thresholds are technical starting values, reported by
    /// pc movement status and tuned from F2c measurements.
    /// </summary>
    public static class WalkerFollow
    {
        /// <summary>Lag up to which the body walks at native pace.</summary>
        public const double CatchUpMetres = 1.5;
        /// <summary>Lag above which the body is corrected (moved off screen, or hurried and faded on screen).</summary>
        public const double CorrectionMetres = 8;
        /// <summary>Fastest catch-up pace, as a multiple of native walking pace.</summary>
        public const double MaximumPaceFactor = 1.5;
        /// <summary>Catch-up pace reaches its maximum at this lag.</summary>
        public const double FullPaceMetres = 3;
        /// <summary>At or below this lag the body is at the logical pose and stops, so it is never ahead of it.</summary>
        public const double StopMetres = 0.05;
        /// <summary>Q3: an on-screen body hurries this long before it fades at the next corner or doorway.</summary>
        public const double HurrySeconds = 10;
        /// <summary>A walker on the logical pose's leg is re-aimed at that pose once it has moved this far.</summary>
        public const double RetargetMetres = 0.5;
        /// <summary>After the logical arrival, a visible walker keeps walking to the door this long.</summary>
        public const double ArrivalTailSeconds = 10;
        /// <summary>Q3 fade: a corrected on-screen body stands and fades out over this long before it is released.</summary>
        public const double FadeOutSeconds = 1;
        private const int MaximumCorners = 16;

        /// <summary>Pace multiple for a lag: 0 at the logical pose, 1 up to <see cref="CatchUpMetres"/>, rising
        /// linearly to <see cref="MaximumPaceFactor"/> at <see cref="FullPaceMetres"/>.</summary>
        public static double PaceFactor(double lagMetres)
        {
            if (!Position.Finite(lagMetres) || lagMetres <= StopMetres) return 0;
            if (lagMetres <= CatchUpMetres) return 1;
            double rise = (lagMetres - CatchUpMetres) / (FullPaceMetres - CatchUpMetres);
            return Math.Min(MaximumPaceFactor, 1 + (MaximumPaceFactor - 1) * rise);
        }

        /// <summary>Unity BlendMode.SrcAlpha and BlendMode.OneMinusSrcAlpha, the alpha blend Unity's built-in and
        /// URP shaders configure for their Fade/Transparent surface through the _SrcBlend/_DstBlend properties.</summary>
        public const int SourceAlphaBlend = 5, OneMinusSourceAlphaBlend = 10;

        /// <summary>Whether a material shows the fade: it has the colour property the fade scales, renders in a
        /// transparent queue (above 2500), and declares alpha blending (_SrcBlend SrcAlpha, _DstBlend
        /// OneMinusSrcAlpha). A queue override alone does not make an opaque shader blend, and a shader without those
        /// blend properties is not recognized; either way the body is kept until it is out of view instead.</summary>
        public static bool MaterialCanFade(int renderQueue, bool hasColourProperty, float? sourceBlend, float? destinationBlend)
            => hasColourProperty && renderQueue > 2500
            && sourceBlend.HasValue && (int)sourceBlend.Value == SourceAlphaBlend
            && destinationBlend.HasValue && (int)destinationBlend.Value == OneMinusSourceAlphaBlend;

        /// <summary>Commanded native speed: the pace's speed, but never more than the lag in one frame of
        /// <paramref name="deltaSeconds"/>, so the body is never commanded past the logical pose, even while the
        /// logical clock stands still. 0 at or below <see cref="StopMetres"/>.</summary>
        public static double CommandSpeed(double paceMetresPerSecond, double lagMetres, double deltaSeconds)
        {
            if (!(paceMetresPerSecond > 0) || !Position.Finite(lagMetres) || lagMetres <= StopMetres) return 0;
            double frame = Position.Finite(deltaSeconds) && deltaSeconds > 1e-3 ? deltaSeconds : 1e-3;
            return Math.Min(paceMetresPerSecond, lagMetres / frame);
        }

        /// <summary>True when the follow must be corrected: too far behind, or the logical leg is not a walk
        /// (the person is already in the car, bus or subway).</summary>
        public static bool NeedsCorrection(double lagMetres, TravelMode logicalMode)
            => logicalMode != TravelMode.Walk || !(lagMetres <= CorrectionMetres);

        /// <summary>Committed geometry at a route progress, without the simulator's clock or traffic state.</summary>
        public static PhysicalJourneySample Sample(Route route, double routeElapsedMinutes)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            double total = route.Minutes;
            double elapsed = Position.Finite(routeElapsedMinutes) ? Math.Max(0, Math.Min(total, routeElapsedMinutes)) : 0;
            return CitySimulator.SampleRoute(route, elapsed, null);
        }

        /// <summary>Along-route metres from <paramref name="fromMinutes"/> to <paramref name="toMinutes"/>; 0 if not ahead.</summary>
        public static double RouteMetres(Route route, double fromMinutes, double toMinutes)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (!(toMinutes > fromMinutes)) return 0;
            double offset = 0, metres = 0;
            foreach (var leg in route.Legs)
            {
                double start = offset, end = offset + leg.Minutes;
                offset = end;
                if (end <= fromMinutes || leg.Minutes <= 0) continue;
                if (start >= toMinutes) break;
                double share = (Math.Min(end, toMinutes) - Math.Max(start, fromMinutes)) / leg.Minutes;
                metres += share * Position.Distance(leg.From, leg.To);
            }
            return metres;
        }

        /// <summary>
        /// The body's route progress after this frame. It starts from the body's previous progress, projects the body
        /// onto that walk leg with <see cref="PhysicalProgress.TryProject"/> (passing reached corners, up to 16), waits
        /// out a zero-distance leg with the logical clock, stops at the start of a non-walk leg, and is capped at the
        /// logical progress. A body pushed off the leg keeps its previous progress.
        /// </summary>
        public static double Advance(Route route, double bodyMinutes, double logicalMinutes, Position actual)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            double total = route.Minutes;
            double logical = Position.Finite(logicalMinutes) ? Math.Max(0, Math.Min(total, logicalMinutes)) : 0;
            double progress = Position.Finite(bodyMinutes) ? Math.Max(0, Math.Min(logical, bodyMinutes)) : 0;
            for (int corner = 0; corner < MaximumCorners; corner++)
            {
                var sample = Sample(route, progress);
                if (sample.AtEnd || sample.Mode != TravelMode.Walk) break;
                double legEnd = Math.Min(total, sample.ElapsedMinutes + sample.RemainingLegMinutes);
                double next;
                if (sample.Stationary) next = Math.Min(logical, legEnd);
                else if (!PhysicalProgress.TryProject(sample, actual, false, out next)) break;
                next = Math.Min(next, logical);
                if (!(next > progress)) break;
                progress = next;
                if (progress < legEnd) break;
            }
            return progress;
        }
    }

    public enum WalkerCorrectionKind
    {
        /// <summary>An off-screen body moved to the logical pose.</summary>
        Move,
        /// <summary>Q3: an on-screen body that hurried for the bound faded out at a corner; it reappears at the logical pose once that is out of view.</summary>
        Fade,
        /// <summary>An off-screen body released because of lag: the person is no longer walking, or the arrival tail ended short of the door.</summary>
        LagRelease,
    }

    public readonly struct WalkerCorrection
    {
        public WalkerCorrection(long sequence, long frame, string personId, WalkerCorrectionKind kind, Position before, Position after, bool onScreen)
        { Sequence = sequence; Frame = frame; PersonId = personId; Kind = kind; Before = before; After = after; OnScreen = onScreen; }
        public long Sequence { get; }
        public long Frame { get; }
        public string PersonId { get; }
        public WalkerCorrectionKind Kind { get; }
        /// <summary>Body pose when corrected.</summary>
        public Position Before { get; }
        /// <summary>Body pose afterwards: the logical pose it was moved to, or where a released body reappears.</summary>
        public Position After { get; }
        /// <summary>Inside the camera frustum with a visible renderer at that frame.</summary>
        public bool OnScreen { get; }
    }

    /// <summary>
    /// F2c walker movement telemetry, held in Runtime memory and never saved: the bounded correction ring (last 512, with
    /// sequence numbers and gap detection), counters and a lag histogram for pc movement status.
    /// </summary>
    public sealed class WalkerMovementTelemetry
    {
        public const int Capacity = 512;
        // 0.05 m bins up to 64 m; larger lags share the last bin, and the exact maximum is kept separately.
        private const double BinMetres = 0.05;
        private const int Bins = 1281;
        private readonly WalkerCorrection[] ring = new WalkerCorrection[Capacity];
        private readonly long[] histogram = new long[Bins];
        private long nextSequence = 1;

        public long Corrections { get; private set; }
        public long Moves { get; private set; }
        public long OnScreenMoves { get; private set; }
        public long Fades { get; private set; }
        public long LagReleases { get; private set; }
        /// <summary>Mode boundaries where a walker reached the car, bus or subway and a body of the new kind is re-acquired.</summary>
        public long Handoffs { get; set; }
        /// <summary>Walker path requests (one NavMesh CalculatePath each). Their rate scales with game speed.</summary>
        public long PathRequests { get; set; }
        /// <summary>Acquisitions refused by the test-profile key MovementFaultAcquirePercent.</summary>
        public long FaultedAcquisitions { get; set; }
        public long LagSamples { get; private set; }
        public double MaximumLagMetres { get; private set; }
        /// <summary>Sequence of the latest correction; 0 before the first.</summary>
        public long LastSequence => nextSequence - 1;
        /// <summary>Oldest sequence still in the ring (1 while nothing was evicted).</summary>
        public long OldestSequence => Math.Max(1, nextSequence - Capacity);

        public long Record(long frame, string personId, WalkerCorrectionKind kind, Position before, Position after, bool onScreen)
        {
            if (personId == null) throw new ArgumentNullException(nameof(personId));
            long sequence = nextSequence++;
            ring[(int)((sequence - 1) % Capacity)] = new WalkerCorrection(sequence, frame, personId, kind, before, after, onScreen);
            Corrections++;
            if (kind == WalkerCorrectionKind.Move) { Moves++; if (onScreen) OnScreenMoves++; }
            else if (kind == WalkerCorrectionKind.Fade) Fades++;
            else LagReleases++;
            return sequence;
        }

        /// <summary>Entries with a sequence after <paramref name="sequence"/>, oldest first. <paramref name="gap"/> is true
        /// when some of those entries were already evicted from the ring.</summary>
        public void Since(long sequence, List<WalkerCorrection> into, out bool gap)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            gap = sequence + 1 < OldestSequence;
            for (long s = Math.Max(Math.Max(1, sequence + 1), OldestSequence); s <= LastSequence; s++)
                into.Add(ring[(int)((s - 1) % Capacity)]);
        }

        public void RecordLag(double metres)
        {
            if (!Position.Finite(metres) || metres < 0) return;
            LagSamples++;
            if (metres > MaximumLagMetres) MaximumLagMetres = metres;
            histogram[Math.Min(Bins - 1, (int)(metres / BinMetres))]++;
        }

        /// <summary>Upper edge of the histogram bin holding the quantile (at most the exact maximum); 0 without samples.</summary>
        public double LagQuantile(double quantile)
        {
            if (LagSamples == 0) return 0;
            long rank = Math.Max(1, (long)Math.Ceiling(Math.Max(0, Math.Min(1, quantile)) * LagSamples));
            long seen = 0;
            for (int bin = 0; bin < Bins; bin++)
            {
                seen += histogram[bin];
                if (seen >= rank) return bin == Bins - 1 ? MaximumLagMetres : Math.Min(MaximumLagMetres, (bin + 1) * BinMetres);
            }
            return MaximumLagMetres;
        }

        public string Status(int followers) => string.Format(CultureInfo.InvariantCulture,
            "followers={0}\nlag_samples={1}\nlag_p50_m={2:F2}\nlag_p95_m={3:F2}\nlag_max_m={4:F2}\ncorrections={5}\nmoves={6}\n"
            + "on_screen_moves={7}\nfades={8}\nlag_releases={9}\nhandoffs={10}\nfaulted_acquisitions={11}\nlast_sequence={12}\n"
            + "path_requests={20}\n"
            + "thresholds=catch_up_m:{13};correct_m:{14};max_pace:{15};hurry_s:{16};arrival_tail_s:{17};fade_out_s:{18};retarget_m:{19}",
            followers, LagSamples, LagQuantile(0.5), LagQuantile(0.95), MaximumLagMetres, Corrections, Moves, OnScreenMoves, Fades,
            LagReleases, Handoffs, FaultedAcquisitions, LastSequence, WalkerFollow.CatchUpMetres, WalkerFollow.CorrectionMetres,
            WalkerFollow.MaximumPaceFactor, WalkerFollow.HurrySeconds, WalkerFollow.ArrivalTailSeconds,
            WalkerFollow.FadeOutSeconds, WalkerFollow.RetargetMetres, PathRequests);

        public static string Format(WalkerCorrection c) => string.Format(CultureInfo.InvariantCulture,
            "seq={0} frame={1} person={2} kind={3} on_screen={4} before={5:R},{6:R},{7:R} after={8:R},{9:R},{10:R}",
            c.Sequence, c.Frame, c.PersonId, c.Kind, c.OnScreen ? "true" : "false",
            c.Before.X, c.Before.Y, c.Before.Z, c.After.X, c.After.Y, c.After.Z);
    }

    /// <summary>
    /// F5f active-journey candidate index (city audit F5 #92, docs/plans/f5-population.md section 3). The derived,
    /// in-memory set of persons whose phase can make them a physical-body candidate: travelling, waiting at a door
    /// (queue planning), holding an optional stop, or at an outdoor scene. Physical selection iterates this set instead
    /// of every citizen. It holds no saved state and decides nothing: caps, leases, the candidate filters and their
    /// priority order stay with Runtime's selection, and movement authority stays with F2 #89.
    /// Visitors are indexed with their kind but withheld from <see cref="BodyCandidates"/> until F5i.
    /// F2f files every body candidate into a <see cref="PresentationGrid"/> from inside File and Rebuild, so selection
    /// can read only the cells near the focus (<see cref="Grid"/>).
    /// </summary>
    public sealed class ActiveJourneyIndex
    {
        /// <summary>F5i switch. Visitors receive no body or lease before F2's movement contract lands (plan B5).</summary>
        public const bool VisitorsReceiveBodies = false;
        /// <summary>Prefix of native outdoor scene places (Runtime OutdoorPlaces: parks, beaches, party piers).</summary>
        public const string OutdoorScenePrefix = "scene-";

        // Swap-removal lists keep membership changes O(1) and enumeration allocation-free. Their order is not
        // roster order; selection sorts by SelectionCandidate.ByPriority, whose ordinal-ID key is total.
        private readonly List<CitizenState> eligible = new List<CitizenState>();
        private readonly List<CitizenState> withheld = new List<CitizenState>();
        // Position in its list; withheld entries are stored as ~index (negative). CitizenState has reference equality.
        private readonly Dictionary<CitizenState, int> positions = new Dictionary<CitizenState, int>();
        private readonly PresentationGrid grid;

        /// <param name="placePosition">Place position by ID (the simulator's place table). Without it, stationary
        /// candidates are unplaced in the grid and returned by every grid query.</param>
        public ActiveJourneyIndex(Func<string, Position?>? placePosition = null) => grid = new PresentationGrid(placePosition);

        /// <summary>Phase-level membership. A superset of every stateful physical-selection candidate
        /// (Runtime PhysicalPopulation: moving journeys, planned queuers, Outdoor); time, distance, cooldown,
        /// reservation and binding filters stay in the selection.</summary>
        public static bool Member(CitizenState c)
            => c.Phase == CitizenPhase.Travelling || c.Phase == CitizenPhase.WaitingForOpening
            || c.Phase == CitizenPhase.AtPlace && (c.OptionalStop != null
                || c.Activity == Activity.Leisure && c.PlaceId.StartsWith(OutdoorScenePrefix, StringComparison.Ordinal));

        /// <summary>Kind from the stable person ID until F5c saves an explicit kind. IDs of no person format
        /// (older fixtures and tests) keep today's resident behaviour.</summary>
        public static PersonKind KindOf(CitizenState c)
            => PersonIds.FitsKind(PersonIds.Classify(c.Identity.Id), PersonKind.Visitor) ? PersonKind.Visitor : PersonKind.Resident;

        public static bool ReceivesBody(CitizenState c) => VisitorsReceiveBodies || !c.IsVisitor && KindOf(c) != PersonKind.Visitor;

        /// <summary>Indexed persons that may receive a body, in no particular order.</summary>
        public IReadOnlyList<CitizenState> BodyCandidates => eligible;
        /// <summary>Every indexed person, visitors included.</summary>
        public int Count => eligible.Count + withheld.Count;
        /// <summary>Indexed visitors withheld from body selection (zero while VisitorsReceiveBodies).</summary>
        public int WithheldVisitors => withheld.Count;
        public bool Contains(CitizenState c) => positions.ContainsKey(c);
        /// <summary>F2f spatial view of <see cref="BodyCandidates"/>: <see cref="PresentationGrid.Query"/> returns each body
        /// candidate filed near a focus once, a superset of those within the radius. Read-only outside Core.</summary>
        public PresentationGrid Grid => grid;

        internal void Rebuild(IReadOnlyList<CitizenState> citizens) => Rebuild(citizens, Array.Empty<CitizenState>());

        /// <summary>Residents and schema-27 visitor routines; visitors are filed with their kind and withheld.</summary>
        internal void Rebuild(IReadOnlyList<CitizenState> citizens, IReadOnlyList<CitizenState> visitors)
        {
            eligible.Clear(); withheld.Clear(); positions.Clear(); grid.Clear();
            for (int i = 0; i < citizens.Count; i++) File(citizens[i]);
            for (int i = 0; i < visitors.Count; i++) File(visitors[i]);
        }

        /// <summary>Called wherever a live citizen's phase, activity, place or optional stop may have changed.
        /// A body candidate that stays a member is re-filed in the grid when its phase anchor or journey changed.</summary>
        internal void File(CitizenState c)
        {
            bool member = Member(c);
            bool present = positions.TryGetValue(c, out int at);
            if (member == present) { if (member && at >= 0) grid.File(c); return; }
            if (member)
            {
                if (ReceivesBody(c)) { positions.Add(c, eligible.Count); eligible.Add(c); grid.File(c); }
                else { positions.Add(c, ~withheld.Count); withheld.Add(c); }
                return;
            }
            positions.Remove(c);
            if (at >= 0) { RemoveAt(eligible, at, false); grid.Remove(c); } else RemoveAt(withheld, ~at, true);
        }

        private void RemoveAt(List<CitizenState> list, int index, bool negative)
        {
            int last = list.Count - 1;
            if (index != last)
            {
                var moved = list[last];
                list[index] = moved;
                positions[moved] = negative ? ~index : index;
            }
            list.RemoveAt(last);
        }

        /// <summary>Shadow comparison against a full scan: exactly the members of <paramref name="citizens"/>,
        /// each in the list of its kind, with consistent positions, and the grid holds exactly the body candidates
        /// at their current journey or place cells (F2f). Allocation-free; for tests and the
        /// low-cadence runtime check, never per frame.</summary>
        public bool MatchesScan(IReadOnlyList<CitizenState> citizens) => MatchesScan(citizens, Array.Empty<CitizenState>());

        /// <summary>As above over residents and visitor routines together.</summary>
        public bool MatchesScan(IReadOnlyList<CitizenState> citizens, IReadOnlyList<CitizenState> visitors)
        {
            int members = 0;
            for (int i = 0; i < citizens.Count + visitors.Count; i++)
            {
                var c = i < citizens.Count ? citizens[i] : visitors[i - citizens.Count];
                bool present = positions.TryGetValue(c, out int at);
                if (Member(c) != present) return false;
                if (!present) continue;
                members++;
                bool body = ReceivesBody(c);
                if (body ? at < 0 || at >= eligible.Count || !ReferenceEquals(eligible[at], c)
                    : at >= 0 || ~at >= withheld.Count || !ReferenceEquals(withheld[~at], c)) return false;
            }
            return members == positions.Count && members == Count && grid.Matches(eligible);
        }
    }
}
