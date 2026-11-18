using System;

namespace PersistentCity.Core
{
    public sealed class UpdateCityBudget
    {
        public const double DefaultTargetMilliseconds = 4;
        public const double AdvanceFloorMilliseconds = 2;
        public const double MaximumReserveMilliseconds = 2;
        public const double InitialReserveMilliseconds = 1;
        private readonly double target;

        public UpdateCityBudget(double targetMilliseconds = DefaultTargetMilliseconds)
        {
            if (!(targetMilliseconds > 0) || double.IsInfinity(targetMilliseconds)) throw new ArgumentOutOfRangeException(nameof(targetMilliseconds));
            target = targetMilliseconds;
        }

        public double TargetMilliseconds => target;
        public double ReserveMilliseconds { get; private set; } = InitialReserveMilliseconds;

        public double Advance(double nativeMilliseconds, double elapsedMilliseconds)
        {
            if (double.IsNaN(nativeMilliseconds) || nativeMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nativeMilliseconds));
            if (nativeMilliseconds <= AdvanceFloorMilliseconds) return nativeMilliseconds;
            if (double.IsNaN(elapsedMilliseconds) || double.IsInfinity(elapsedMilliseconds) || elapsedMilliseconds < 0) elapsedMilliseconds = 0;
            return Math.Min(nativeMilliseconds, Math.Max(AdvanceFloorMilliseconds, target - elapsedMilliseconds - ReserveMilliseconds));
        }

        public const double MinimumSliceMilliseconds = 1;

        public double Slice(double elapsedMilliseconds, double maximumMilliseconds)
        {
            if (double.IsNaN(maximumMilliseconds) || maximumMilliseconds < MinimumSliceMilliseconds) throw new ArgumentOutOfRangeException(nameof(maximumMilliseconds));
            if (double.IsNaN(elapsedMilliseconds) || double.IsInfinity(elapsedMilliseconds) || elapsedMilliseconds < 0) elapsedMilliseconds = 0;
            return Math.Min(maximumMilliseconds, Math.Max(MinimumSliceMilliseconds, target - elapsedMilliseconds - ReserveMilliseconds));
        }

        public void ObservePhysical(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0) return;
            double sample = Math.Min(MaximumReserveMilliseconds, milliseconds);
            ReserveMilliseconds = sample >= ReserveMilliseconds ? sample : ReserveMilliseconds + (sample - ReserveMilliseconds) * .05;
        }
    }

    public sealed class FrameDeferral
    {
        public const double DefaultFrameMilliseconds = 8;
        private readonly double frameLimit;
        private double firstDeferred = double.NaN;

        public FrameDeferral(double frameMilliseconds = DefaultFrameMilliseconds)
        {
            if (!(frameMilliseconds > 0) || double.IsInfinity(frameMilliseconds)) throw new ArgumentOutOfRangeException(nameof(frameMilliseconds));
            frameLimit = frameMilliseconds;
        }

        public double FrameMilliseconds => frameLimit;
        public long Deferred { get; private set; }
        public double LongestWaitSeconds { get; private set; }

        public bool Spent(double frameMilliseconds) => !(frameMilliseconds < frameLimit);

        public bool Allows(double frameMilliseconds, double seconds)
        {
            if (Spent(frameMilliseconds))
            {
                if (double.IsNaN(firstDeferred)) firstDeferred = seconds;
                LongestWaitSeconds = Math.Max(LongestWaitSeconds, seconds - firstDeferred);
                Deferred++;
                return false;
            }
            firstDeferred = double.NaN;
            return true;
        }
    }
}
