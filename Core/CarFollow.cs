using System;

namespace PersistentCity.Core
{
    /// <summary>F2d: what a visible car is doing against the traffic signal at the moment it stands still.</summary>
    public enum CarPhaseMismatch
    {
        None,
        /// <summary>The kernel holds the car at its signal while the native light at the car's target is green.</summary>
        KernelHeldAtNativeGreen,
        /// <summary>The native light stops the car while the kernel signal of the section it is in is green.</summary>
        NativeStopAtKernelGreen,
    }

    /// <summary>
    /// F2d car follower (docs/plans/f2-movement.md Decision 2). Pure presentation arithmetic: a car body tracks its own
    /// progress along the committed car legs and is commanded toward the traffic kernel's background coordinate, never
    /// past it. Runtime turns the lag into a follow, a catch-up or a correction. Nothing here writes simulator or kernel
    /// state, and the kernel never waits for a body. Thresholds are technical starting values, reported by
    /// pc movement status and tuned from F2d measurements.
    /// </summary>
    public static class CarFollow
    {
        /// <summary>Lag up to which the car simply follows. Beyond it the car is catching up (Gley drives at its
        /// native pace, still capped at the kernel's coordinate) and the lag is counted as catch-up.</summary>
        public const double CatchUpMetres = 5;
        /// <summary>Lag above which the car is corrected (released off screen, or hurried and faded on screen).</summary>
        public const double CorrectionMetres = 30;
        /// <summary>Q3: an on-screen car is given this long to catch up before it fades at the next corner.</summary>
        public const double HurrySeconds = WalkerFollow.HurrySeconds;
        /// <summary>Q3 fade: a corrected on-screen car stands and fades out over this long before it is released.</summary>
        public const double FadeOutSeconds = WalkerFollow.FadeOutSeconds;
        /// <summary>A car slower than this (horizontal metres per second) counts as standing for the phase count.</summary>
        public const double StoppedMetresPerSecond = 0.5;
        /// <summary>A car this close to its own corner counts as being at that corner for a fade.</summary>
        public const double CornerMetres = 1.5;
        private const int MaximumCorners = 16;

        /// <summary>A native road leg: the only car legs a native car body drives. A committed car route also has car-mode
        /// legs that are not roads (the preparation and parking pauses and the kerb connectors at both ends).</summary>
        public static bool IsRoad(RouteLeg leg) => leg.Mode == TravelMode.Car && leg.ServiceId != null
            && leg.ServiceId.StartsWith(RoadServicePrefix, StringComparison.Ordinal);
        public static bool IsRoad(PhysicalJourneySample sample) => sample.Mode == TravelMode.Car && sample.ServiceId != null
            && sample.ServiceId.StartsWith(RoadServicePrefix, StringComparison.Ordinal);
        public const string RoadServicePrefix = "native:road:v1:";

        /// <summary>Route minutes where the run of native road legs that contains (or starts at) <paramref name="fromMinutes"/>
        /// ends: the car body's parking point, where it hands over. Equal to <paramref name="fromMinutes"/> when that point
        /// is not on a road leg.</summary>
        public static double CarLegsEnd(Route route, double fromMinutes)
            => RunEnd(route, fromMinutes, IsRoad);

        /// <summary>F2d: whether <paramref name="logicalMinutes"/> lies in the run of car-mode legs (roads, kerb connectors,
        /// parking pause) that contains <paramref name="bodyMinutes"/>. A logical pose on the trailing connector or the
        /// parking pause is still the car's journey, not a reason to correct it.</summary>
        public static bool SameCarRun(Route route, double bodyMinutes, double logicalMinutes)
            => Position.Finite(logicalMinutes) && logicalMinutes <= RunEnd(route, bodyMinutes, leg => leg.Mode == TravelMode.Car)
            && WalkerFollow.Sample(route, Math.Max(0, Math.Min(route.Minutes, logicalMinutes))).Mode == TravelMode.Car;

        private static double RunEnd(Route route, double fromMinutes, Func<RouteLeg, bool> member)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            double offset = 0, end = Position.Finite(fromMinutes) ? Math.Max(0, fromMinutes) : 0;
            bool inside = false;
            foreach (var leg in route.Legs)
            {
                double start = offset;
                offset += leg.Minutes;
                if (!inside)
                {
                    if (offset <= end) continue;
                    if (!member(leg)) return end;
                    inside = true;
                }
                else if (!member(leg)) return start;
            }
            return inside ? Math.Min(offset, route.Minutes) : end;
        }

        /// <summary>The furthest route minute a car body may be commanded to: the logical (kernel) coordinate, but never
        /// beyond the end of its own run of car legs, so a car whose person has parked drives on only to the parking
        /// point.</summary>
        public static double Cap(Route route, double bodyMinutes, double logicalMinutes)
        {
            double logical = Position.Finite(logicalMinutes) ? Math.Max(0, Math.Min(route.Minutes, logicalMinutes)) : 0;
            return Math.Min(logical, CarLegsEnd(route, bodyMinutes));
        }

        /// <summary>
        /// The body's route progress after this frame. It starts from the body's previous progress and projects the car
        /// onto its own car leg (<see cref="PhysicalProgress.TryProject"/>, car tolerances), up to 16 corners. A road
        /// corner is passed only with Gley's reached evidence for that leg (<paramref name="reachedLeg"/>), which keeps
        /// the visible turn in the order of the native waypoints; until then the car advances only up to that corner.
        /// A zero-distance road leg is waited out with the cap. Progress stops at the start of a leg that is not a native
        /// road (the kerb connector, the parking pause or the walk after parking), is capped at
        /// <paramref name="capMinutes"/>, and is kept when the car is pushed off its leg.
        /// </summary>
        public static double Advance(Route route, double bodyMinutes, double capMinutes, Position actual, int reachedLeg)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            double total = route.Minutes;
            double cap = Position.Finite(capMinutes) ? Math.Max(0, Math.Min(total, capMinutes)) : 0;
            double progress = Position.Finite(bodyMinutes) ? Math.Max(0, Math.Min(cap, bodyMinutes)) : 0;
            for (int corner = 0; corner < MaximumCorners; corner++)
            {
                var sample = WalkerFollow.Sample(route, progress);
                if (sample.AtEnd || !IsRoad(sample)) break;
                double legEnd = Math.Min(total, sample.ElapsedMinutes + sample.RemainingLegMinutes);
                bool reached = sample.LegIndex == reachedLeg;
                double next;
                if (sample.Stationary) next = Math.Min(cap, legEnd);
                else
                {
                    if (!PhysicalProgress.TryProject(sample, actual, reached, out next)) break;
                    // Without Gley's evidence the corner itself is not passed: credit the measured approach only.
                    if (!reached && next >= legEnd) next = Approach(sample, actual);
                }
                next = Math.Min(next, cap);
                if (!(next > progress)) break;
                progress = next;
                if (progress < legEnd) break;
            }
            return progress;
        }

        // Along-leg progress of a car short of its corner, strictly before the corner.
        private static double Approach(PhysicalJourneySample sample, Position actual)
        {
            double dx = sample.To.X - sample.From.X, dz = sample.To.Z - sample.From.Z, square = dx * dx + dz * dz;
            double along = square > 0 ? ((actual.X - sample.From.X) * dx + (actual.Z - sample.From.Z) * dz) / square : 0;
            double fraction = Math.Max(sample.LegFraction, Math.Min(1 - 1e-9, Position.Finite(along) ? along : 0));
            return sample.ElapsedMinutes + (fraction - sample.LegFraction) * sample.LegMinutes;
        }

        /// <summary>True when the follow must be corrected: too far behind, or the person is no longer on this car's
        /// traffic leg (the kernel completed it and the person walks on from the parked car).</summary>
        public static bool NeedsCorrection(double lagMetres, bool logicalOnCar)
            => !logicalOnCar || !(lagMetres <= CorrectionMetres);

        /// <summary>
        /// Phase mismatch of a standing visible car (Decision 6), from simultaneous phases at the same junction: the
        /// native light at the car's target waypoint (<paramref name="nativeRed"/>, null when unknown),
        /// <paramref name="kernelHeldBySignal"/> when the kernel vehicle stands at its link end behind its own red now,
        /// and <paramref name="kernelGreenAtBody"/> when the kernel signal of the section the car itself is in is green
        /// now. A past kernel crossing is not a green. A car that is still catching up is not held by the kernel. Changes
        /// no signal phase.
        /// </summary>
        public static CarPhaseMismatch Classify(bool bodyStopped, bool? nativeRed, double lagMetres, bool kernelHeldBySignal, bool kernelGreenAtBody)
        {
            if (!bodyStopped || !nativeRed.HasValue) return CarPhaseMismatch.None;
            if (!nativeRed.Value && kernelHeldBySignal && lagMetres <= CatchUpMetres) return CarPhaseMismatch.KernelHeldAtNativeGreen;
            if (nativeRed.Value && kernelGreenAtBody) return CarPhaseMismatch.NativeStopAtKernelGreen;
            return CarPhaseMismatch.None;
        }
    }
}
