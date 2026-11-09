// Rig-only F4b union probe: the Unity-free rules, compiled by Probe and by logic-check.
// No Unity, Harmony, game or Persistent City reference may appear in this file.
using System;
using System.Collections.Generic;

namespace PerfUnionProbe
{
    /// <summary>Reference-mode pace shim (F4 plan Decision 3). Same rule as Persistent City's
    /// NativeCityClock.TickPrefix: an ordinary tick (not loading, time machine present and not running)
    /// with a positive, finite delta is multiplied by the declared factor. Everything else is unchanged.</summary>
    public static class PaceShim
    {
        public const double MinimumFactor = 0.1, MaximumFactor = 1;

        public static bool ValidFactor(double factor)
            => !double.IsNaN(factor) && !double.IsInfinity(factor) && factor >= MinimumFactor && factor <= MaximumFactor;

        public static bool Ordinary(bool loading, bool timeMachinePresent, bool timeMachineRunning)
            => !loading && timeMachinePresent && !timeMachineRunning;

        public static float Apply(float delta, bool loading, bool timeMachinePresent, bool timeMachineRunning, double factor)
        {
            if (!ValidFactor(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0) return delta;
            return Ordinary(loading, timeMachinePresent, timeMachineRunning) ? delta * (float)factor : delta;
        }
    }

    /// <summary>NativeCityClock counters read immediately before and after one AccountingPrefix call.</summary>
    public struct ClockCounters
    {
        public long HeldOrdinaryTicks, HeldEconomySkipTicks, BoundaryClips;
        public double WithheldMinutes;
        public ClockCounters(long heldOrdinary, long heldSkip, long clips, double withheld)
        { HeldOrdinaryTicks = heldOrdinary; HeldEconomySkipTicks = heldSkip; BoundaryClips = clips; WithheldMinutes = withheld; }
    }

    public struct TickRecord
    {
        public int Frame;
        public long Qpc;
        public bool Held, BoundaryClip, AccountingLimited, CountersReset;
        public double WithheldMinutes, NativeMinute, LogicalMinute;
    }

    public enum HoldEventKind { Hold, Resume }

    public struct HoldEvent
    {
        public HoldEventKind Kind;
        public int Frame;
        public long Qpc;
        /// <summary>A hold already in progress when the window started; opened at the window's first frame.</summary>
        public bool CarriedIn;
        /// <summary>A hold still open when the window ended; closed at the window's last frame.</summary>
        public bool Ongoing;
    }

    /// <summary>Per-tick hold state machine (F4 plan Decision 5). A tick is held only when HeldOrdinaryTicks or
    /// HeldEconomySkipTicks increased across the AccountingPrefix call. AccountingLimited without such an
    /// increase is a boundary clip, never a hold. Transitions append hold/resume events while a window is open.</summary>
    public sealed class HoldTracker
    {
        private readonly List<HoldEvent> events = new List<HoldEvent>();
        private bool windowOpen;
        public bool Held { get; private set; }
        public long Ticks { get; private set; }
        public long HeldTicks { get; private set; }
        public long BoundaryClipTicks { get; private set; }
        public long CounterResets { get; private set; }
        public IReadOnlyList<HoldEvent> Events => events;
        public bool WindowOpen => windowOpen;

        public TickRecord Observe(in ClockCounters before, in ClockCounters after, bool accountingLimited,
            int frame, long qpc, double nativeMinute, double logicalMinute)
        {
            // NativeCityClock.Start/Stop reset every counter; a decrease is a reset, not a hold or a clip.
            bool reset = after.HeldOrdinaryTicks < before.HeldOrdinaryTicks || after.HeldEconomySkipTicks < before.HeldEconomySkipTicks
                || after.BoundaryClips < before.BoundaryClips || after.WithheldMinutes < before.WithheldMinutes;
            bool held = !reset && (after.HeldOrdinaryTicks > before.HeldOrdinaryTicks || after.HeldEconomySkipTicks > before.HeldEconomySkipTicks);
            bool clip = !reset && !held && accountingLimited;
            Ticks++;
            if (reset) CounterResets++;
            if (held) HeldTicks++;
            if (clip) BoundaryClipTicks++;
            if (held != Held && windowOpen)
                events.Add(new HoldEvent { Kind = held ? HoldEventKind.Hold : HoldEventKind.Resume, Frame = frame, Qpc = qpc });
            Held = held;
            return new TickRecord { Frame = frame, Qpc = qpc, Held = held, BoundaryClip = clip, AccountingLimited = accountingLimited,
                CountersReset = reset, WithheldMinutes = reset ? 0 : Math.Max(0, after.WithheldMinutes - before.WithheldMinutes),
                NativeMinute = nativeMinute, LogicalMinute = logicalMinute };
        }

        /// <summary>Opens a window at its first frame. A hold already in progress opens there, flagged carried-in.</summary>
        public void BeginWindow(int firstFrame, long firstQpc)
        {
            events.Clear(); windowOpen = true;
            Ticks = HeldTicks = BoundaryClipTicks = CounterResets = 0;
            if (Held) events.Add(new HoldEvent { Kind = HoldEventKind.Hold, Frame = firstFrame, Qpc = firstQpc, CarriedIn = true });
        }

        /// <summary>Closes the window at its last frame. A hold still open closes there, flagged ongoing.</summary>
        public void EndWindow(int lastFrame, long lastQpc)
        {
            if (!windowOpen) return;
            if (Held) events.Add(new HoldEvent { Kind = HoldEventKind.Resume, Frame = lastFrame, Qpc = lastQpc, Ongoing = true });
            windowOpen = false;
        }
    }

    /// <summary>Decision 7: a per-call record for every AdvancePopulation call of at least 30 ms.</summary>
    public static class AdvanceCallRule
    {
        public const int ThresholdMilliseconds = 30;
        /// <summary>Exact integer comparison, so 30.0 ms is recorded and 29.9 ms is not, at any timer frequency.</summary>
        public static bool Record(long durationTicks, long frequency)
        {
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
            return durationTicks >= 0 && durationTicks * 1000 >= ThresholdMilliseconds * frequency;
        }
    }

    public struct AdvanceCallRecord
    {
        public int Frame;
        public long StartQpc, DurationTicks;
        public int CollectionsDelta;
        /// <summary>OptionalStopEnvironment.SeamMilliseconds after minus before; NaN when the seam is unbound.</summary>
        public double OptionalStopMilliseconds;
        public double TargetMinute;
    }

    /// <summary>Nested inclusive union of timed scopes for the current frame, plus per-scope and per-component totals.
    /// Components are nested attribution scopes only; their time is never added to the union.</summary>
    public sealed class UnionClock
    {
        private readonly long[] calls, ticks, maxTicks, spikes, frameComponentTicks;
        private readonly long spikeTicks;
        private int depth;
        private long unionStart;
        public UnionClock(int scopes, int components, long frequency)
        {
            calls = new long[scopes]; ticks = new long[scopes]; maxTicks = new long[scopes]; spikes = new long[scopes];
            frameComponentTicks = new long[components];
            spikeTicks = AdvanceCallRule.ThresholdMilliseconds * frequency / 1000;
        }
        public int Depth => depth;
        public long FrameTicks { get; private set; }
        public long Underflows { get; private set; }
        public long ComponentsOutsideUnion { get; private set; }
        public int Scopes => calls.Length;
        public int Components => frameComponentTicks.Length;
        public long Calls(int scope) => calls[scope];
        public long Ticks(int scope) => ticks[scope];
        public long MaxTicks(int scope) => maxTicks[scope];
        public long Spikes(int scope) => spikes[scope];
        public long FrameComponentTicks(int component) => frameComponentTicks[component];

        public void Enter(long now) { if (depth++ == 0) unionStart = now; }
        public void Exit(int scope, long start, long now)
        {
            long duration = Math.Max(0, now - start);
            calls[scope]++; ticks[scope] += duration;
            if (duration > maxTicks[scope]) maxTicks[scope] = duration;
            if (duration >= spikeTicks) spikes[scope]++;
            if (depth == 0) { Underflows++; return; }
            if (--depth == 0) FrameTicks += Math.Max(0, now - unionStart);
        }
        public void Component(int component, long start, long now, bool insideUnion)
        {
            if (!insideUnion) ComponentsOutsideUnion++;
            frameComponentTicks[component] += Math.Max(0, now - start);
        }
        public void ResetFrame() { FrameTicks = 0; Array.Clear(frameComponentTicks, 0, frameComponentTicks.Length); }
        public void ResetAll()
        {
            ResetFrame(); depth = 0; Underflows = ComponentsOutsideUnion = 0;
            Array.Clear(calls, 0, calls.Length); Array.Clear(ticks, 0, ticks.Length);
            Array.Clear(maxTicks, 0, maxTicks.Length); Array.Clear(spikes, 0, spikes.Length);
        }
        /// <summary>Abandons open scopes (cross-frame or error); the frame is flagged by the caller.</summary>
        public void Abandon() => depth = 0;
    }
}
