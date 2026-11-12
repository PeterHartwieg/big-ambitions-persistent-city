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

        public PhysicalJourneySample SampleJourney(CitizenState citizen)
        {
            if (citizen == null) throw new ArgumentNullException(nameof(citizen));
            double elapsed = JourneyProgress(citizen);
            if (citizen.Journey != null) elapsed = NormalizeJourneyElapsed(elapsed, citizen.Journey.Minutes, State.Minute);
            return SampleJourney(citizen, elapsed);
        }

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
                double boundaryTolerance = Math.Min(leg.Minutes * 1e-9, Math.Max(1e-12, Math.Abs(routeElapsedMinutes) * 1e-14));
                if (Math.Abs(remaining - leg.Minutes) <= boundaryTolerance)
                {
                    sampledElapsed = Math.Max(routeElapsedMinutes, sampledElapsed + leg.Minutes - remaining);
                    remaining = leg.Minutes;
                }
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

    public static class WalkerFollow
    {
        public const double CatchUpMetres = 1.5;
        public const double CorrectionMetres = 8;
        public const double MaximumPaceFactor = 1.5;
        public const double FullPaceMetres = 3;
        public const double StopMetres = 0.05;
        public const double HurrySeconds = 10;
        public const double RetargetMetres = 0.5;
        public const double AheadToleranceMetres = 0.01;
        public const double ArrivalTailSeconds = 10;
        public const double FadeOutSeconds = 1;
        private const int MaximumCorners = 16;

        public static double PaceFactor(double lagMetres)
        {
            if (!Position.Finite(lagMetres) || lagMetres <= StopMetres) return 0;
            if (lagMetres <= CatchUpMetres) return 1;
            double rise = (lagMetres - CatchUpMetres) / (FullPaceMetres - CatchUpMetres);
            return Math.Min(MaximumPaceFactor, 1 + (MaximumPaceFactor - 1) * rise);
        }

        public const int SourceAlphaBlend = 5, OneMinusSourceAlphaBlend = 10;

        public static readonly string[] UrpFadeShaders = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit" };
        public static readonly string[] StandardFadeShaders = { "Standard", "Standard (Specular setup)" };

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

        public const double ProjectionAheadMetres = 0.01;
        public static bool NotAheadOnLeg(Position from, Position to, Position pose, Position projected)
        {
            double dx = to.X - from.X, dz = to.Z - from.Z, square = dx * dx + dz * dz;
            if (!projected.IsFinite || !Position.Finite(square)) return false;
            if (square <= 1e-12) return true;
            double alongPose = ((pose.X - from.X) * dx + (pose.Z - from.Z) * dz) / square;
            double alongProjected = ((projected.X - from.X) * dx + (projected.Z - from.Z) * dz) / square;
            return (alongProjected - alongPose) * Math.Sqrt(square) <= ProjectionAheadMetres;
        }

        public static double CommandSpeed(double paceMetresPerSecond, double lagMetres, double deltaSeconds)
        {
            if (!(paceMetresPerSecond > 0) || !Position.Finite(lagMetres) || lagMetres <= StopMetres) return 0;
            double frame = Position.Finite(deltaSeconds) && deltaSeconds > 1e-3 ? deltaSeconds : 1e-3;
            return Math.Min(paceMetresPerSecond, lagMetres / frame);
        }

        public static bool PoseOnLeg(PhysicalJourneySample own, PhysicalJourneySample logical)
            => logical.Mode == TravelMode.Walk && !logical.Stationary && own.LegIndex == logical.LegIndex;

        public static bool NeedsPath(PhysicalJourneySample own, PhysicalJourneySample logical, double lagMetres,
            int targetLeg, bool targetIsPose, Position targetPoint, bool pathHeld, out bool atPose)
        {
            atPose = targetIsPose && targetLeg == own.LegIndex && PoseOnLeg(own, logical)
                && Position.Distance(logical.Position, own.To) > 1e-6;
            if (!atPose) return targetLeg != own.LegIndex || targetIsPose || !pathHeld && lagMetres > StopMetres;
            return lagMetres > StopMetres && Position.Distance(logical.Position, targetPoint) >= RetargetMetres;
        }

        public static double AheadMetres(PhysicalJourneySample own, PhysicalJourneySample logical, Position actual)
        {
            if (own.LegIndex != logical.LegIndex || logical.Mode != TravelMode.Walk || logical.Stationary || !actual.IsFinite
                || Position.Distance(actual, logical.From) <= 0.08) return 0;
            double dx = logical.To.X - logical.From.X, dz = logical.To.Z - logical.From.Z, square = dx * dx + dz * dz;
            if (!Position.Finite(square) || square <= 1e-12) return 0;
            double length = Math.Sqrt(square);
            double along = ((actual.X - logical.From.X) * dx + (actual.Z - logical.From.Z) * dz) / length;
            double beside = Math.Abs((actual.X - logical.From.X) * dz - (actual.Z - logical.From.Z) * dx) / length;
            if (!Position.Finite(along) || !Position.Finite(beside) || beside > 0.35) return 0;
            double ahead = Math.Min(along, length) - logical.LegFraction * length;
            return ahead > 0 ? ahead : 0;
        }

        public static bool NeedsCorrection(double lagMetres, TravelMode logicalMode)
            => logicalMode != TravelMode.Walk || !(lagMetres <= CorrectionMetres);

        public static PhysicalJourneySample Sample(Route route, double routeElapsedMinutes)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            double total = route.Minutes;
            double elapsed = Position.Finite(routeElapsedMinutes) ? Math.Max(0, Math.Min(total, routeElapsedMinutes)) : 0;
            return CitySimulator.SampleRoute(route, elapsed, null);
        }

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
        Move,
        Fade,
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
        public Position Before { get; }
        public Position After { get; }
        public bool OnScreen { get; }
        public bool Car { get; }
    }

    public sealed class WalkerMovementTelemetry
    {
        public const int Capacity = 512;
        private sealed class LagHistogram
        {
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

        public long Corrections { get; private set; }
        public long Moves { get; private set; }
        public long OnScreenMoves { get; private set; }
        public long Fades { get; private set; }
        public long LagReleases { get; private set; }
        public long Handoffs { get; set; }
        public long PathRequests { get; set; }
        public long AheadFrames { get; private set; }
        public double MaximumAheadMetres { get; private set; }
        public void RecordAhead(double metres)
        {
            if (!(metres > WalkerFollow.AheadToleranceMetres)) return;
            AheadFrames++; MaximumAheadMetres = Math.Max(MaximumAheadMetres, metres);
        }
        public long FaultedAcquisitions { get; set; }
        public long LagSamples => walkers.Samples;
        public double MaximumLagMetres => walkers.Maximum;
        public long CarCorrections { get; private set; }
        public long CarFades { get; private set; }
        public long CarLagReleases { get; private set; }
        public long CarHandoffs { get; set; }
        public long CarCatchUpFrames { get; set; }
        public long CarLagSamples => cars.Samples;
        public double CarMaximumLagMetres => cars.Maximum;
        public long KernelHeldAtNativeGreen { get; private set; }
        public long NativeStopAtKernelGreen { get; private set; }
        public double KernelHeldAtNativeGreenSeconds { get; private set; }
        public double NativeStopAtKernelGreenSeconds { get; private set; }
        public long LastSequence => nextSequence - 1;
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

        public void Since(long sequence, List<WalkerCorrection> into, out bool gap)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            gap = sequence + 1 < OldestSequence;
            for (long s = Math.Max(Math.Max(1, sequence + 1), OldestSequence); s <= LastSequence; s++)
                into.Add(ring[(int)((s - 1) % Capacity)]);
        }

        public void RecordLag(double metres) => walkers.Record(metres);
        public void RecordCarLag(double metres) => cars.Record(metres);

        public double LagQuantile(double quantile) => walkers.Quantile(quantile);
        public double CarLagQuantile(double quantile) => cars.Quantile(quantile);

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
            + "path_requests={20}\nahead_frames={21}\nahead_max_m={22:F3}\n"
            + "thresholds=catch_up_m:{13};correct_m:{14};max_pace:{15};hurry_s:{16};arrival_tail_s:{17};fade_out_s:{18};retarget_m:{19}",
            followers, LagSamples, LagQuantile(0.5), LagQuantile(0.95), MaximumLagMetres, Corrections, Moves, OnScreenMoves, Fades,
            LagReleases, Handoffs, FaultedAcquisitions, LastSequence, WalkerFollow.CatchUpMetres, WalkerFollow.CorrectionMetres,
            WalkerFollow.MaximumPaceFactor, WalkerFollow.HurrySeconds, WalkerFollow.ArrivalTailSeconds,
            WalkerFollow.FadeOutSeconds, WalkerFollow.RetargetMetres, PathRequests, AheadFrames, MaximumAheadMetres)
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

    public sealed class ActiveJourneyIndex
    {
        public const bool VisitorsReceiveBodies = true;
        public const string OutdoorScenePrefix = "scene-";

        private readonly List<CitizenState> eligible = new List<CitizenState>();
        private readonly List<CitizenState> withheld = new List<CitizenState>();
        private readonly Dictionary<CitizenState, int> positions = new Dictionary<CitizenState, int>();
        private readonly PresentationGrid grid;

        public ActiveJourneyIndex(Func<string, Position?>? placePosition = null) => grid = new PresentationGrid(placePosition);

        public static bool Member(CitizenState c)
            => c.Phase == CitizenPhase.Travelling || c.Phase == CitizenPhase.WaitingForOpening
            || c.Phase == CitizenPhase.AtPlace && (c.OptionalStop != null
                || c.Activity == Activity.Leisure && c.PlaceId.StartsWith(OutdoorScenePrefix, StringComparison.Ordinal));

        public static PersonKind KindOf(CitizenState c)
            => PersonIds.FitsKind(PersonIds.Classify(c.Identity.Id), PersonKind.Visitor) ? PersonKind.Visitor : PersonKind.Resident;

        public static bool ReceivesBody(CitizenState c) => VisitorsReceiveBodies || !c.IsVisitor && KindOf(c) != PersonKind.Visitor;

        public IReadOnlyList<CitizenState> BodyCandidates => eligible;
        public int Count => eligible.Count + withheld.Count;
        public int WithheldVisitors => withheld.Count;
        public bool Contains(IPersonView person) => person is CitizenState c && positions.ContainsKey(c);
        public PresentationGrid Grid => grid;

        internal void Rebuild(IReadOnlyList<CitizenState> citizens) => Rebuild(citizens, Array.Empty<CitizenState>());

        internal void Rebuild(IReadOnlyList<CitizenState> citizens, IReadOnlyList<CitizenState> visitors)
        {
            eligible.Clear(); withheld.Clear(); positions.Clear(); grid.Clear();
            for (int i = 0; i < citizens.Count; i++) File(citizens[i]);
            for (int i = 0; i < visitors.Count; i++) File(visitors[i]);
        }

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

        public bool MatchesScan(IReadOnlyList<CitizenState> citizens) => MatchesScan(citizens, Array.Empty<CitizenState>());

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
