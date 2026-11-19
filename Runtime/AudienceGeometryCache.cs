using System;
using System.Collections.Generic;
using System.Diagnostics;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed class AudienceTarget
    {
        public AudienceTarget(object zone, object performer, string id, Position position)
        { Zone = zone; Performer = performer; Id = id; Position = position; }
        public object Zone { get; }
        public object Performer { get; }
        public string Id { get; }
        public Position Position { get; }
        public bool Matches(AudienceTarget other) => ReferenceEquals(Zone, other.Zone)
            && ReferenceEquals(Performer, other.Performer) && Id == other.Id && Position.Equals(other.Position);
    }

    internal sealed class GeometryFrameBudget
    {
        public const double DefaultFrameMilliseconds = 1;
        private readonly double limit;
        private int frame = int.MinValue;
        private double spent;

        public GeometryFrameBudget(double frameMilliseconds = DefaultFrameMilliseconds)
        {
            if (!(frameMilliseconds > 0) || double.IsInfinity(frameMilliseconds)) throw new ArgumentOutOfRangeException(nameof(frameMilliseconds));
            limit = frameMilliseconds;
        }

        public long Deferrals { get; private set; }

        public bool TryDeadline(int frameNumber, long now, out long deadline)
        {
            if (frameNumber != frame) { frame = frameNumber; spent = 0; }
            double remaining = limit - spent;
            if (remaining <= 0) { Deferrals++; deadline = 0; return false; }
            deadline = now + (long)(remaining * Stopwatch.Frequency / 1000);
            return true;
        }

        public void Spend(double milliseconds) { if (milliseconds > 0) spent += milliseconds; }
    }

    internal sealed class ResumableSamples<TInput>
    {
        private readonly TInput[] inputs;
        private int next;
        private readonly List<OptionalStopSpot> spots = new List<OptionalStopSpot>();

        public ResumableSamples(TInput[] inputs) { this.inputs = inputs ?? throw new ArgumentNullException(nameof(inputs)); }

        public bool Complete => next >= inputs.Length;
        public int Sampled => next;
        public IReadOnlyList<OptionalStopSpot> Spots => spots;

        public bool Continue<TState>(Func<TState, TInput, int, OptionalStopSpot?> sample, TState state, long deadlineTimestamp)
        {
            while (next < inputs.Length)
            {
                var found = sample(state, inputs[next++], spots.Count);
                if (found != null) spots.Add(found);
                if (next < inputs.Length && Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
            }
            return true;
        }

        public OptionalStopSpot[] Take() => spots.ToArray();
    }

    internal interface IGeometryWarmTarget
    {
        int Count { get; }
        bool Warm(int index);
        bool Started(int index);
        void Start(int index);
        bool Continue(int index, long deadlineTimestamp);
    }

    internal sealed class GeometryWarmup
    {
        private int cursor;
        public bool Idle { get; private set; }
        public long Setups { get; private set; }
        public long Slices { get; private set; }
        public long Failures { get; private set; }
        public double MaximumMilliseconds { get; private set; }
        public void Reset() { Idle = false; }
        public void ResetDiagnostics() { Setups = Slices = Failures = 0; MaximumMilliseconds = 0; }

        public void Run(GeometryFrameBudget budget, int frameNumber, IGeometryWarmTarget target)
        {
            int count = target.Count;
            if (Idle || count == 0) return;
            long started = Stopwatch.GetTimestamp();
            if (!budget.TryDeadline(frameNumber, started, out long deadline)) return;
            bool worked = false;
            try
            {
                for (int visited = 0; visited < count; visited++)
                {
                    int index = cursor % count;
                    if (target.Warm(index)) { cursor = index + 1; if (Stopwatch.GetTimestamp() >= deadline) return; continue; }
                    worked = true;
                    if (!target.Started(index))
                    {
                        if (Stopwatch.GetTimestamp() >= deadline) return;
                        Setups++;
                        target.Start(index);
                    }
                    if (!target.Continue(index, deadline)) return;
                    cursor = index + 1;
                    if (Stopwatch.GetTimestamp() >= deadline) return;
                }
                if (!worked) Idle = true;
            }
            catch (Exception) { Failures++; cursor++; }
            finally
            {
                double elapsed = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                budget.Spend(elapsed); Slices++;
                if (elapsed > MaximumMilliseconds) MaximumMilliseconds = elapsed;
            }
        }
    }

    internal sealed class AudienceGeometryCache
    {
        public const int Angles = 8;
        private static readonly Func<(Func<Place, AudienceTarget, int, OptionalStopSpot?> Sample, Place Place), (AudienceTarget Member, int Angle), int, OptionalStopSpot?> SampleMember
            = (state, input, _) => state.Sample(state.Place, input.Member, input.Angle);
        private sealed class Entry
        {
            public Position Origin;
            public AudienceTarget[] Targets = Array.Empty<AudienceTarget>();
            public ResumableSamples<(AudienceTarget Member, int Angle)>? Run;
            public IReadOnlyList<OptionalStopSpot> Spots = Array.Empty<OptionalStopSpot>();
            public bool Matches(Position origin, IReadOnlyList<AudienceTarget> targets)
            {
                if (!Origin.Equals(origin) || Targets.Length != targets.Count) return false;
                for (int i = 0; i < Targets.Length; i++) if (!Targets[i].Matches(targets[i])) return false;
                return true;
            }
        }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        public int Builds { get; private set; }

        public IReadOnlyList<OptionalStopSpot> Get(Place place, IReadOnlyList<AudienceTarget> targets,
            Func<Place, AudienceTarget, int, OptionalStopSpot?> sample)
        {
            if (!TryGet(place, targets, sample, long.MaxValue, out var spots)) throw new InvalidOperationException("An unbounded audience sample did not finish.");
            return spots;
        }

        public bool Pending(Place place, IReadOnlyList<AudienceTarget> targets)
            => entries.TryGetValue(place.Id, out var entry) && entry.Run != null && entry.Matches(place.Position, targets);

        public bool Cached(Place place, IReadOnlyList<AudienceTarget> targets, out IReadOnlyList<OptionalStopSpot> spots)
        {
            if (entries.TryGetValue(place.Id, out var entry) && entry.Run == null && entry.Matches(place.Position, targets))
            { spots = entry.Spots; return true; }
            spots = Array.Empty<OptionalStopSpot>();
            return false;
        }

        public bool TryGet(Place place, IReadOnlyList<AudienceTarget> targets, Func<Place, AudienceTarget, int, OptionalStopSpot?> sample,
            long deadlineTimestamp, out IReadOnlyList<OptionalStopSpot> spots)
        {
            if (!entries.TryGetValue(place.Id, out var entry) || !entry.Matches(place.Position, targets))
            {
                var image = new AudienceTarget[targets.Count];
                for (int i = 0; i < image.Length; i++) image[i] = targets[i];
                var inputs = new (AudienceTarget, int)[image.Length * Angles];
                for (int i = 0; i < image.Length; i++)
                    for (int angle = 0; angle < Angles; angle++) inputs[i * Angles + angle] = (image[i], angle);
                entry = new Entry { Origin = place.Position, Targets = image, Run = new ResumableSamples<(AudienceTarget, int)>(inputs) };
                entries[place.Id] = entry;
                Builds++;
            }
            if (entry.Run != null)
            {
                bool done;
                try { done = entry.Run.Continue(SampleMember, (sample, place), deadlineTimestamp); }
                catch (Exception) { entries.Remove(place.Id); throw; }
                if (!done) { spots = Array.Empty<OptionalStopSpot>(); return false; }
                entry.Spots = entry.Run.Take(); entry.Run = null;
            }
            spots = entry.Spots;
            return true;
        }
    }
}
