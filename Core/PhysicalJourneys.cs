using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
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
        // F5g: a visitor's slot is found by its ID in the ordinal visitor list, not by the queued index, because fill units
        // merge new visitors into that list and shift later positions. Before F5g both gave the same answer.
        private bool CurrentCitizenSlot(Due item, CitizenState citizen)
        {
            if (item.ListIndex >= 0) return item.ListIndex < State.Citizens.Count && ReferenceEquals(State.Citizens[item.ListIndex], citizen);
            int index = VisitorSlot(citizen.Identity.Id);
            return index >= 0 && ReferenceEquals(State.VisitorRoutines[index], citizen);
        }

        private int VisitorSlot(string id)
        {
            int low = 0, high = State.Visitors.Count - 1;
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                int order = string.CompareOrdinal(State.Visitors[mid].Id, id);
                if (order == 0) return mid < State.VisitorRoutines.Count ? mid : -1;
                if (order < 0) low = mid + 1; else high = mid - 1;
            }
            return -1;
        }

        /// <summary>Sample committed geometry, including zero-distance preparation/wait legs and the parked/driven car.</summary>
        public PhysicalJourneySample SampleJourney(CitizenState citizen)
        {
            if (citizen == null) throw new ArgumentNullException(nameof(citizen));
            double elapsed = JourneyProgress(citizen);
            if (citizen.Journey != null) elapsed = NormalizeJourneyElapsed(elapsed, citizen.Journey.Minutes, State.Minute);
            return SampleJourney(citizen, elapsed);
        }

        // A completed traffic leg (or, before F2c/F2d, a completed lease) stores departure = clock - duration. Reversing
        // that subtraction can exceed duration by an ulp at a large city clock; this is not evidence of extra travel.
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
                // cannot stay unreached forever while a body has already stopped there.
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

        /// <summary>Shaders whose fade configuration is verified: the URP Lit, Simple Lit and Unlit shaders, which output
        /// _BaseColor's alpha on a transparent surface, and the built-in Standard shaders, which output _Color's alpha in
        /// their alpha-blend (Fade) mode. Any other shader is opaque for the fade, whatever properties it exposes.</summary>
        public static readonly string[] UrpFadeShaders = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit" };
        public static readonly string[] StandardFadeShaders = { "Standard", "Standard (Specular setup)" };

        /// <summary>Whether a material shows the fade the colour alpha applies: a verified shader (above) in its
        /// alpha-blended configuration, using that shader's own colour property, in a transparent queue (above 2500),
        /// with _SrcBlend SrcAlpha and _DstBlend OneMinusSrcAlpha, and without alpha clipping (_ALPHATEST_ON would cut
        /// the body out halfway through instead of fading it). URP needs _Surface 1 with _SURFACE_TYPE_TRANSPARENT and
        /// _BaseColor; Standard needs _ALPHABLEND_ON and _Color. Everything else counts as opaque, and the body is kept
        /// until it is out of view instead.</summary>
        public static bool MaterialCanFade(MaterialFadeFacts m)
        {
            if (m.AlphaTestKeyword || !(m.RenderQueue > 2500)
                || !(m.SourceBlend.HasValue && (int)m.SourceBlend.Value == SourceAlphaBlend)
                || !(m.DestinationBlend.HasValue && (int)m.DestinationBlend.Value == OneMinusSourceAlphaBlend)) return false;
            if (Array.IndexOf(UrpFadeShaders, m.ShaderName) >= 0)
                return m.HasBaseColour && m.Surface == 1f && m.TransparentSurfaceKeyword;
            if (Array.IndexOf(StandardFadeShaders, m.ShaderName) >= 0)
                return m.HasColour && !m.HasBaseColour && m.AlphaBlendKeyword;
            return false;
        }

        /// <summary>A NavMesh projection of a pose may shift it along the leg (kerbs, ramps). It is usable only if it is
        /// not ahead of the pose along the leg <paramref name="from"/>→<paramref name="to"/> (horizontal), so the
        /// destination stays behind the logical progress.</summary>
        public const double ProjectionAheadMetres = 0.01;
        public static bool NotAheadOnLeg(Position from, Position to, Position pose, Position projected)
        {
            double dx = to.X - from.X, dz = to.Z - from.Z, square = dx * dx + dz * dz;
            if (!projected.IsFinite || !Position.Finite(square)) return false;
            if (square <= 1e-12) return true;
            double alongPose = ((pose.X - from.X) * dx + (pose.Z - from.Z) * dz) / square;
            double alongProjected = ((projected.X - from.X) * dx + (projected.Z - from.Z) * dz) / square;
            // Compared in metres along the leg: a pose passed through float coordinates rounds by up to a few
            // hundredths of a millimetre, which must not count as ahead; a real forward shift does.
            return (alongProjected - alongPose) * Math.Sqrt(square) <= ProjectionAheadMetres;
        }

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

    /// <summary>What the fade probe reads from one material (Runtime PhysicalPedestrianBody.CanFade fills it).</summary>
    public readonly struct MaterialFadeFacts
    {
        public MaterialFadeFacts(string shaderName, int renderQueue, bool hasBaseColour, bool hasColour, float? sourceBlend, float? destinationBlend,
            float? surface, bool transparentSurfaceKeyword, bool alphaBlendKeyword, bool alphaTestKeyword)
        {
            ShaderName = shaderName ?? ""; RenderQueue = renderQueue; HasBaseColour = hasBaseColour; HasColour = hasColour;
            SourceBlend = sourceBlend; DestinationBlend = destinationBlend; Surface = surface;
            TransparentSurfaceKeyword = transparentSurfaceKeyword; AlphaBlendKeyword = alphaBlendKeyword; AlphaTestKeyword = alphaTestKeyword;
        }
        public string ShaderName { get; }
        public int RenderQueue { get; }
        public bool HasBaseColour { get; }
        public bool HasColour { get; }
        public float? SourceBlend { get; }
        public float? DestinationBlend { get; }
        public float? Surface { get; }
        public bool TransparentSurfaceKeyword { get; }
        public bool AlphaBlendKeyword { get; }
        public bool AlphaTestKeyword { get; }
    }

    public enum WalkerCorrectionKind
    {
        /// <summary>An off-screen body moved to the logical pose.</summary>
        Move,
        /// <summary>Q3: an on-screen body that hurried for the bound faded out at a corner; it reappears at the logical pose once that is out of view.</summary>
        Fade,
        /// <summary>An off-screen body released because of lag: the person is no longer walking, the arrival tail ended short of the door,
        /// or (F2d) a car fell behind the kernel; it reappears at the logical pose.</summary>
        LagRelease,
    }

    public readonly struct WalkerCorrection
    {
        public WalkerCorrection(long sequence, long frame, string personId, WalkerCorrectionKind kind, Position before, Position after, bool onScreen, bool car = false)
        { Sequence = sequence; Frame = frame; PersonId = personId; Kind = kind; Before = before; After = after; OnScreen = onScreen; Car = car; }
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
        /// <summary>F2d: the corrected body was a car (false: a walker).</summary>
        public bool Car { get; }
    }

    /// <summary>
    /// F2c walker and (F2d) car movement telemetry, held in Runtime memory and never saved: one bounded correction ring
    /// (last 512, with sequence numbers and gap detection) for both kinds of body, counters, a lag histogram per kind and
    /// the visible cars' signal phase-mismatch counts for pc movement status.
    /// </summary>
    public sealed class WalkerMovementTelemetry
    {
        public const int Capacity = 512;
        private sealed class LagHistogram
        {
            // 0.05 m bins up to 64 m; larger lags share the last bin, and the exact maximum is kept separately.
            private const double BinMetres = 0.05;
            private const int Bins = 1281;
            private readonly long[] histogram = new long[Bins];
            internal long Samples;
            internal double Maximum;
            internal void Record(double metres)
            {
                if (!Position.Finite(metres) || metres < 0) return;
                Samples++;
                if (metres > Maximum) Maximum = metres;
                histogram[Math.Min(Bins - 1, (int)(metres / BinMetres))]++;
            }
            internal double Quantile(double quantile)
            {
                if (Samples == 0) return 0;
                long rank = Math.Max(1, (long)Math.Ceiling(Math.Max(0, Math.Min(1, quantile)) * Samples));
                long seen = 0;
                for (int bin = 0; bin < Bins; bin++)
                {
                    seen += histogram[bin];
                    if (seen >= rank) return bin == Bins - 1 ? Maximum : Math.Min(Maximum, (bin + 1) * BinMetres);
                }
                return Maximum;
            }
        }
        private readonly WalkerCorrection[] ring = new WalkerCorrection[Capacity];
        private readonly LagHistogram walkers = new LagHistogram(), cars = new LagHistogram();
        private long nextSequence = 1;

        /// <summary>All corrections, walkers and cars.</summary>
        public long Corrections { get; private set; }
        public long Moves { get; private set; }
        public long OnScreenMoves { get; private set; }
        public long Fades { get; private set; }
        public long LagReleases { get; private set; }
        /// <summary>Mode boundaries where a walker reached the car, bus or subway, or a car its parking point, and a body
        /// of the new kind is re-acquired.</summary>
        public long Handoffs { get; set; }
        /// <summary>Walker path calculations: every SetTarget a follower issues (one NavMesh CalculatePath each, the
        /// projected and the raw attempt counted separately). Their rate scales with game speed.</summary>
        public long PathRequests { get; set; }
        /// <summary>Acquisitions refused by the test-profile key MovementFaultAcquirePercent.</summary>
        public long FaultedAcquisitions { get; set; }
        public long LagSamples => walkers.Samples;
        public double MaximumLagMetres => walkers.Maximum;
        /// <summary>F2d car counters: corrections, fades and lag releases among the totals above, car handoffs to a walker,
        /// and frames a visible car spent catching up (lag beyond <see cref="CarFollow.CatchUpMetres"/>).</summary>
        public long CarCorrections { get; private set; }
        public long CarFades { get; private set; }
        public long CarLagReleases { get; private set; }
        public long CarHandoffs { get; set; }
        public long CarCatchUpFrames { get; set; }
        public long CarLagSamples => cars.Samples;
        public double CarMaximumLagMetres => cars.Maximum;
        /// <summary>F2d Decision 6: episodes in which a standing visible car was held by the kernel at a native green, or
        /// stopped by the native light at a kernel green, and the follow-clock seconds spent in each.</summary>
        public long KernelHeldAtNativeGreen { get; private set; }
        public long NativeStopAtKernelGreen { get; private set; }
        public double KernelHeldAtNativeGreenSeconds { get; private set; }
        public double NativeStopAtKernelGreenSeconds { get; private set; }
        /// <summary>Sequence of the latest correction; 0 before the first.</summary>
        public long LastSequence => nextSequence - 1;
        /// <summary>Oldest sequence still in the ring (1 while nothing was evicted).</summary>
        public long OldestSequence => Math.Max(1, nextSequence - Capacity);

        public long Record(long frame, string personId, WalkerCorrectionKind kind, Position before, Position after, bool onScreen, bool car = false)
        {
            if (personId == null) throw new ArgumentNullException(nameof(personId));
            long sequence = nextSequence++;
            ring[(int)((sequence - 1) % Capacity)] = new WalkerCorrection(sequence, frame, personId, kind, before, after, onScreen, car);
            Corrections++;
            if (car) CarCorrections++;
            if (kind == WalkerCorrectionKind.Move) { Moves++; if (onScreen) OnScreenMoves++; }
            else if (kind == WalkerCorrectionKind.Fade) { Fades++; if (car) CarFades++; }
            else { LagReleases++; if (car) CarLagReleases++; }
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

        public void RecordLag(double metres) => walkers.Record(metres);
        public void RecordCarLag(double metres) => cars.Record(metres);

        /// <summary>Upper edge of the histogram bin holding the quantile (at most the exact maximum); 0 without samples.</summary>
        public double LagQuantile(double quantile) => walkers.Quantile(quantile);
        public double CarLagQuantile(double quantile) => cars.Quantile(quantile);

        /// <summary>One frame of a visible car's phase state: a new episode when the kind differs from the car's previous
        /// frame, and the frame's seconds added to the kind's total.</summary>
        public void RecordPhase(CarPhaseMismatch kind, CarPhaseMismatch previous, double seconds)
        {
            if (kind == CarPhaseMismatch.None) return;
            double add = Position.Finite(seconds) && seconds > 0 ? seconds : 0;
            if (kind == CarPhaseMismatch.KernelHeldAtNativeGreen)
            { if (previous != kind) KernelHeldAtNativeGreen++; KernelHeldAtNativeGreenSeconds += add; }
            else { if (previous != kind) NativeStopAtKernelGreen++; NativeStopAtKernelGreenSeconds += add; }
        }

        public string Status(int followers) => Status(followers, 0);

        public string Status(int followers, int carFollowers) => string.Format(CultureInfo.InvariantCulture,
            "followers={0}\nlag_samples={1}\nlag_p50_m={2:F2}\nlag_p95_m={3:F2}\nlag_max_m={4:F2}\ncorrections={5}\nmoves={6}\n"
            + "on_screen_moves={7}\nfades={8}\nlag_releases={9}\nhandoffs={10}\nfaulted_acquisitions={11}\nlast_sequence={12}\n"
            + "path_requests={20}\n"
            + "thresholds=catch_up_m:{13};correct_m:{14};max_pace:{15};hurry_s:{16};arrival_tail_s:{17};fade_out_s:{18};retarget_m:{19}",
            followers, LagSamples, LagQuantile(0.5), LagQuantile(0.95), MaximumLagMetres, Corrections, Moves, OnScreenMoves, Fades,
            LagReleases, Handoffs, FaultedAcquisitions, LastSequence, WalkerFollow.CatchUpMetres, WalkerFollow.CorrectionMetres,
            WalkerFollow.MaximumPaceFactor, WalkerFollow.HurrySeconds, WalkerFollow.ArrivalTailSeconds,
            WalkerFollow.FadeOutSeconds, WalkerFollow.RetargetMetres, PathRequests)
            + string.Format(CultureInfo.InvariantCulture,
            "\ncar_followers={0}\ncar_lag_samples={1}\ncar_lag_p50_m={2:F2}\ncar_lag_p95_m={3:F2}\ncar_lag_max_m={4:F2}\n"
            + "car_catch_up_frames={5}\ncar_corrections={6}\ncar_fades={7}\ncar_lag_releases={8}\ncar_handoffs={9}\n"
            + "phase_kernel_held_native_green={10}\nphase_kernel_held_native_green_s={11:F1}\n"
            + "phase_native_stop_kernel_green={12}\nphase_native_stop_kernel_green_s={13:F1}\n"
            + "car_thresholds=catch_up_m:{14};correct_m:{15};hurry_s:{16};fade_out_s:{17};stopped_mps:{18}",
            carFollowers, CarLagSamples, CarLagQuantile(0.5), CarLagQuantile(0.95), CarMaximumLagMetres, CarCatchUpFrames,
            CarCorrections, CarFades, CarLagReleases, CarHandoffs, KernelHeldAtNativeGreen, KernelHeldAtNativeGreenSeconds,
            NativeStopAtKernelGreen, NativeStopAtKernelGreenSeconds, CarFollow.CatchUpMetres, CarFollow.CorrectionMetres,
            CarFollow.HurrySeconds, CarFollow.FadeOutSeconds, CarFollow.StoppedMetresPerSecond);

        public static string Format(WalkerCorrection c) => string.Format(CultureInfo.InvariantCulture,
            "seq={0} frame={1} person={2} kind={3} on_screen={4} before={5:R},{6:R},{7:R} after={8:R},{9:R},{10:R} body={11}",
            c.Sequence, c.Frame, c.PersonId, c.Kind, c.OnScreen ? "true" : "false",
            c.Before.X, c.Before.Y, c.Before.Z, c.After.X, c.After.Y, c.After.Z, c.Car ? "car" : "walker");
    }

    /// <summary>
    /// F5f active-journey candidate index (city audit F5 #92, docs/plans/f5-population.md section 3). The derived,
    /// in-memory set of persons whose phase can make them a physical-body candidate: travelling, waiting at a door
    /// (queue planning), holding an optional stop, or at an outdoor scene. Physical selection iterates this set instead
    /// of every citizen. It holds no saved state and decides nothing: caps, the candidate filters and their
    /// priority order stay with Runtime's selection, and movement authority stays with F2 #89.
    /// Since F5i visitors are body candidates like residents: F2's movement contract makes every body follow the
    /// logical pose, so a body cannot change a visitor's outcome. The withheld list stays empty.
    /// F2f files every body candidate into a <see cref="PresentationGrid"/> from inside File and Rebuild, so selection
    /// can read only the cells near the focus (<see cref="Grid"/>).
    /// </summary>
    public sealed class ActiveJourneyIndex
    {
        /// <summary>F5i switch (plan B5): visitors receive bodies now that F2's movement contract (F2c walkers, F2d cars,
        /// F2e player holds) makes physical progress follow logical permissions.</summary>
        public const bool VisitorsReceiveBodies = true;
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

        /// <summary>Indexed persons that may receive a body, in no particular order. F9g named exception: still entity-typed,
        /// because the F2 movement-equivalence driver (tests/movement-equivalence/PresentationDriver.cs, owned by F2g) follows
        /// each candidate's journey through it. Runtime presentation reads <see cref="Grid"/> instead.</summary>
        public IReadOnlyList<CitizenState> BodyCandidates => eligible;
        /// <summary>Every indexed person, visitors included.</summary>
        public int Count => eligible.Count + withheld.Count;
        /// <summary>Indexed visitors withheld from body selection (zero while VisitorsReceiveBodies).</summary>
        public int WithheldVisitors => withheld.Count;
        /// <summary>F9g: takes the read-only view, so presentation can test a grid query result; only the entity is ever filed.</summary>
        public bool Contains(IPersonView person) => person is CitizenState c && positions.ContainsKey(c);
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
