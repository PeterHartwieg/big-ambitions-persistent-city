using System;

namespace PersistentCity.Core
{
    public enum CarPhaseMismatch
    {
        None,
        KernelHeldAtNativeGreen,
        NativeStopAtKernelGreen,
    }

    public static class CarFollow
    {
        public const double CatchUpMetres = 5;
        public const double CorrectionMetres = 30;
        public const double HurrySeconds = WalkerFollow.HurrySeconds;
        public const double FadeOutSeconds = WalkerFollow.FadeOutSeconds;
        public const double StoppedMetresPerSecond = 0.5;
        public const double CornerMetres = 1.5;
        private const int MaximumCorners = 16;

        public static bool IsRoad(RouteLeg leg) => leg.Mode == TravelMode.Car && leg.ServiceId != null
            && leg.ServiceId.StartsWith(RoadServicePrefix, StringComparison.Ordinal);
        public static bool IsRoad(PhysicalJourneySample sample) => sample.Mode == TravelMode.Car && sample.ServiceId != null
            && sample.ServiceId.StartsWith(RoadServicePrefix, StringComparison.Ordinal);
        public const string RoadServicePrefix = "native:road:v1:";

        public static double CarLegsEnd(Route route, double fromMinutes)
            => RunEnd(route, fromMinutes, IsRoad);

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

        public static double Cap(Route route, double bodyMinutes, double logicalMinutes)
        {
            double logical = Position.Finite(logicalMinutes) ? Math.Max(0, Math.Min(route.Minutes, logicalMinutes)) : 0;
            return Math.Min(logical, CarLegsEnd(route, bodyMinutes));
        }

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
                    if (!reached && next >= legEnd) next = Approach(sample, actual);
                }
                next = Math.Min(next, cap);
                if (!(next > progress)) break;
                progress = next;
                if (progress < legEnd) break;
            }
            return progress;
        }

        private static double Approach(PhysicalJourneySample sample, Position actual)
        {
            double dx = sample.To.X - sample.From.X, dz = sample.To.Z - sample.From.Z, square = dx * dx + dz * dz;
            double along = square > 0 ? ((actual.X - sample.From.X) * dx + (actual.Z - sample.From.Z) * dz) / square : 0;
            double fraction = Math.Max(sample.LegFraction, Math.Min(1 - 1e-9, Position.Finite(along) ? along : 0));
            return sample.ElapsedMinutes + (fraction - sample.LegFraction) * sample.LegMinutes;
        }

        public static bool NeedsCorrection(double lagMetres, bool logicalOnCar)
            => !logicalOnCar || !(lagMetres <= CorrectionMetres);

        public static CarPhaseMismatch Classify(bool bodyStopped, bool? nativeRed, double lagMetres, bool kernelHeldBySignal, bool kernelGreenAtBody)
        {
            if (!bodyStopped || !nativeRed.HasValue) return CarPhaseMismatch.None;
            if (!nativeRed.Value && kernelHeldBySignal && lagMetres <= CatchUpMetres) return CarPhaseMismatch.KernelHeldAtNativeGreen;
            if (nativeRed.Value && kernelGreenAtBody) return CarPhaseMismatch.NativeStopAtKernelGreen;
            return CarPhaseMismatch.None;
        }
    }
}
