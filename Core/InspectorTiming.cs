using System;

namespace PersistentCity.Core
{
    public sealed class InspectorTiming
    {
        public const int Modes = 3;
        private readonly long[] updateTicks = new long[Modes], updateCalls = new long[Modes], updateMaxTicks = new long[Modes];
        private readonly long[] drawTicks = new long[Modes], drawCalls = new long[Modes], drawMaxTicks = new long[Modes];
        private readonly double ticksPerMillisecond;
        private long builds, buildTicks, buildMaxTicks;

        public InspectorTiming(double ticksPerSecond)
        {
            if (!(ticksPerSecond > 0) || double.IsInfinity(ticksPerSecond)) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            ticksPerMillisecond = ticksPerSecond / 1000.0;
        }

        public void Update(int mode, long elapsed) => Add(updateTicks, updateCalls, updateMaxTicks, mode, elapsed);
        public void Draw(int mode, long elapsed) => Add(drawTicks, drawCalls, drawMaxTicks, mode, elapsed);

        public void Build(long elapsed)
        {
            if (elapsed < 0) return;
            builds++; buildTicks += elapsed; if (elapsed > buildMaxTicks) buildMaxTicks = elapsed;
        }

        private static void Add(long[] ticks, long[] calls, long[] max, int mode, long elapsed)
        {
            if (mode < 0 || mode >= Modes || elapsed < 0) return;
            ticks[mode] += elapsed; calls[mode]++; if (elapsed > max[mode]) max[mode] = elapsed;
        }

        public double UpdateMaxMilliseconds(int mode) => updateMaxTicks[mode] / ticksPerMillisecond;
        public double BuildMaxMilliseconds => buildMaxTicks / ticksPerMillisecond;

        public void Reset()
        {
            Array.Clear(updateTicks, 0, Modes); Array.Clear(updateCalls, 0, Modes); Array.Clear(updateMaxTicks, 0, Modes);
            Array.Clear(drawTicks, 0, Modes); Array.Clear(drawCalls, 0, Modes); Array.Clear(drawMaxTicks, 0, Modes);
            builds = buildTicks = buildMaxTicks = 0;
        }

        private double Milliseconds(long ticks) => ticks / ticksPerMillisecond;

        private object ModeRows(long[] ticks, long[] calls, long[] max) => new
        {
            closed = new { calls = calls[0], totalMilliseconds = Milliseconds(ticks[0]), maxMilliseconds = Milliseconds(max[0]) },
            hover = new { calls = calls[1], totalMilliseconds = Milliseconds(ticks[1]), maxMilliseconds = Milliseconds(max[1]) },
            open = new { calls = calls[2], totalMilliseconds = Milliseconds(ticks[2]), maxMilliseconds = Milliseconds(max[2]) }
        };

        public object Projection(long picks, string? hovered, string? selected) => new
        {
            update = ModeRows(updateTicks, updateCalls, updateMaxTicks),
            draw = ModeRows(drawTicks, drawCalls, drawMaxTicks),
            updateMaxMilliseconds = Math.Max(UpdateMaxMilliseconds(0), Math.Max(UpdateMaxMilliseconds(1), UpdateMaxMilliseconds(2))),
            picks,
            builds, buildMilliseconds = Milliseconds(buildTicks), buildMaxMilliseconds = BuildMaxMilliseconds,
            hovered, selected
        };
    }
}
