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

        public double Exclusive(double elapsedMilliseconds, double maximumMilliseconds)
        {
            if (double.IsNaN(maximumMilliseconds) || maximumMilliseconds < MinimumSliceMilliseconds) throw new ArgumentOutOfRangeException(nameof(maximumMilliseconds));
            if (double.IsNaN(elapsedMilliseconds) || double.IsInfinity(elapsedMilliseconds) || elapsedMilliseconds < 0) elapsedMilliseconds = 0;
            return Math.Min(maximumMilliseconds, Math.Max(MinimumSliceMilliseconds, target - elapsedMilliseconds));
        }

        public void ObservePhysical(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0) return;
            double sample = Math.Min(MaximumReserveMilliseconds, milliseconds);
            ReserveMilliseconds = sample >= ReserveMilliseconds ? sample : ReserveMilliseconds + (sample - ReserveMilliseconds) * .05;
        }
    }

    public sealed class UnitOvershoot
    {
        public const double Decay = .25;
        public double Milliseconds { get; private set; }

        public long Deadline(long startedTimestamp, double budgetMilliseconds)
        {
            double usable = double.IsNaN(budgetMilliseconds) ? 0 : Math.Max(0, budgetMilliseconds - Milliseconds);
            return startedTimestamp + (long)(usable * System.Diagnostics.Stopwatch.Frequency / 1000);
        }

        public void Observe(long deadlineTimestamp, long finishedTimestamp)
        {
            double over = Math.Max(0, (finishedTimestamp - deadlineTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            Milliseconds = over >= Milliseconds ? over : Milliseconds + (over - Milliseconds) * Decay;
        }

        public void Reset() => Milliseconds = 0;
    }

    public static class PathAdmission
    {
        public static bool Admits(bool frameRanUnit, int framePaths, int maximumFramePaths, double remainingMilliseconds, double reserveMilliseconds)
            => framePaths < maximumFramePaths && (!frameRanUnit || remainingMilliseconds > reserveMilliseconds);
    }

    public sealed class UnitForecast
    {
        public const double FrameDecay = .5;
        public const int MaximumIdleHold = 30;
        private readonly int idleHold;
        private int idleFrames;
        private double frameMaximum;

        public UnitForecast(int idleHoldFrames = 0) { idleHold = Math.Max(0, Math.Min(MaximumIdleHold, idleHoldFrames)); }

        public double Milliseconds { get; private set; }

        public bool Fits(double elapsedMilliseconds, double budgetMilliseconds)
            => elapsedMilliseconds < budgetMilliseconds && elapsedMilliseconds + Milliseconds <= budgetMilliseconds;

        public void Observe(double unitMilliseconds)
        {
            if (double.IsNaN(unitMilliseconds) || double.IsInfinity(unitMilliseconds) || unitMilliseconds <= 0) return;
            if (unitMilliseconds > frameMaximum) frameMaximum = unitMilliseconds;
            if (unitMilliseconds > Milliseconds) Milliseconds = unitMilliseconds;
        }

        public void EndFrame()
        {
            if (frameMaximum == 0 && ++idleFrames <= idleHold) return;
            if (frameMaximum > 0) idleFrames = 0;
            Milliseconds = Math.Max(frameMaximum, Milliseconds * FrameDecay);
            frameMaximum = 0;
        }

        public void Reset() { Milliseconds = 0; frameMaximum = 0; idleFrames = 0; }
    }

    public sealed class AdvanceAdmission
    {
        private bool ran;
        public UnitForecast Events { get; } = new UnitForecast();
        public const int PlanningIdleHoldFrames = 8;
        public UnitForecast Planning { get; } = new UnitForecast(PlanningIdleHoldFrames);
        public long SkippedSamples { get; private set; }

        public bool AdmitsEvent(double elapsedMilliseconds, double budgetMilliseconds) => Events.Fits(elapsedMilliseconds, budgetMilliseconds);

        public bool AdmitsPlanning(double elapsedMilliseconds, double budgetMilliseconds)
            => !ran || Planning.Fits(elapsedMilliseconds, budgetMilliseconds);

        public void ObserveEvent(double wallMilliseconds, double cpuMilliseconds, bool collected)
        {
            ran = true;
            if (Usable(wallMilliseconds, collected)) Events.Observe(cpuMilliseconds >= 0 ? Math.Min(wallMilliseconds, cpuMilliseconds) : wallMilliseconds);
        }

        public void ObservePlanning(double unitMilliseconds, double sliceCpuMilliseconds, bool collected)
        {
            ran = true;
            if (Usable(unitMilliseconds, collected)) Planning.Observe(sliceCpuMilliseconds >= 0 ? Math.Min(unitMilliseconds, sliceCpuMilliseconds) : unitMilliseconds);
        }

        private bool Usable(double milliseconds, bool collected)
        {
            if (!collected) return true;
            if (milliseconds > 0) SkippedSamples++;
            return false;
        }

        public void EndFrame() { ran = false; Events.EndFrame(); Planning.EndFrame(); }

        public void Reset() { ran = false; Events.Reset(); Planning.Reset(); SkippedSamples = 0; }
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
