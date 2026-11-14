using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct PersonDressing
    {
        public PersonDressing(CitizenIdentity identity, int daysPerYear)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            Id = identity.Id; Name = identity.Name; Age = identity.Age; Female = identity.Female; AppearanceSeed = identity.AppearanceSeed; DaysPerYear = daysPerYear;
        }

        public string? Id { get; }
        public string? Name { get; }
        public int Age { get; }
        public bool Female { get; }
        public int AppearanceSeed { get; }
        public int DaysPerYear { get; }
        public bool Dressed => Id != null;

        public bool Matches(PersonDressing other) => Id != null && string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal) && Age == other.Age && Female == other.Female
            && AppearanceSeed == other.AppearanceSeed && DaysPerYear == other.DaysPerYear;
    }

    public enum BodyChoice { Hit, Grow, Redress, Deferred, Full }

    public sealed class DressedBodyPool<T> where T : class
    {
        public const int DefaultSpare = 16;
        public const int StreetSpare = 32;
        public const double TrimEverySeconds = 5, TrimIdleSeconds = 120;
        public const double StaleMinutes = 15;
        public static int Ceiling(int activeMaximum) => checked(activeMaximum + StreetSpare);
        private readonly List<T> items = new List<T>();
        private readonly List<PersonDressing> dressings = new List<PersonDressing>();
        private readonly List<double> returned = new List<double>();
        private readonly List<double> wanted = new List<double>();
        private readonly int spare;

        public DressedBodyPool(int spare = DefaultSpare)
        {
            if (spare < 0) throw new ArgumentOutOfRangeException(nameof(spare));
            this.spare = spare;
        }

        public int Count => items.Count;
        public int Spare => spare;
        public double Minute { get; set; } = double.NaN;

        public T this[int index] => items[index];
        public PersonDressing DressingAt(int index) => dressings[index];
        public double WantedAt(int index) => wanted[index];

        public void Return(T item, PersonDressing dressing) => Return(item, dressing, double.NegativeInfinity);

        public void Return(T item, PersonDressing dressing, double seconds) => Return(item, dressing, seconds, double.NaN);

        public void Return(T item, PersonDressing dressing, double seconds, double wantedMinute)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            items.Add(item); dressings.Add(dressing); returned.Add(double.IsNaN(seconds) ? double.NegativeInfinity : seconds);
            wanted.Add(dressing.Dressed ? wantedMinute : double.NaN);
        }

        public bool Expendable(int index)
        {
            double at = wanted[index];
            if (double.IsNaN(at)) return false;
            return double.IsPositiveInfinity(at) || !double.IsNaN(Minute) && at + StaleMinutes < Minute;
        }

        private double Score(int index)
        {
            if (Expendable(index)) return double.PositiveInfinity;
            double at = wanted[index];
            return double.IsNaN(at) ? double.PositiveInfinity : at;
        }

        private int Victim()
        {
            int best = -1;
            double score = double.NegativeInfinity;
            for (int i = 0; i < items.Count; i++)
            {
                double s = Score(i);
                if (best < 0 || s > score) { best = i; score = s; }
            }
            return best;
        }

        private int OldestExpendable()
        {
            for (int i = 0; i < items.Count; i++) if (Expendable(i)) return i;
            return -1;
        }

        private T TakeAt(int index)
        {
            T item = items[index];
            items.RemoveAt(index); dressings.RemoveAt(index); returned.RemoveAt(index); wanted.RemoveAt(index);
            return item;
        }

        public bool TryTakeDressed(in PersonDressing wanted, out T item)
        {
            item = null!;
            for (int i = items.Count - 1; i >= 0; i--)
                if (dressings[i].Matches(wanted)) { item = TakeAt(i); return true; }
            return false;
        }

        public bool TryTakeUndressed(out T item)
        {
            item = null!;
            for (int i = 0; i < items.Count; i++)
                if (!dressings[i].Dressed) { item = TakeAt(i); return true; }
            return false;
        }

        public bool TryTakeOldest(out T item)
        {
            item = null!;
            if (items.Count == 0) return false;
            item = TakeAt(0);
            return true;
        }

        public bool TryTakeVictim(out T item)
        {
            item = null!;
            int at = Victim();
            if (at < 0) return false;
            item = TakeAt(at);
            return true;
        }

        public bool TryTakeAny(out T item)
        {
            item = null!;
            if (items.Count == 0) return false;
            item = TakeAt(items.Count - 1);
            return true;
        }

        public bool HasDressed(in PersonDressing wanted)
        {
            for (int i = dressings.Count - 1; i >= 0; i--) if (dressings[i].Matches(wanted)) return true;
            return false;
        }

        public bool WouldDefer(in PersonDressing wanted, int owned, int maximum, DressBuildBudget? budget, int frame, double seconds) =>
            budget != null && !HasDressed(wanted) && !(owned >= maximum && items.Count == 0) && !budget.CanTake(frame, seconds);

        public BodyChoice Choose(in PersonDressing wanted, int owned, int maximum, DressBuildBudget? budget, int frame, double seconds, out T item)
        {
            if (TryTakeDressed(wanted, out item)) return BodyChoice.Hit;
            if (owned >= maximum && items.Count == 0) return BodyChoice.Full;
            if (budget != null && !budget.TryTake(frame, seconds)) return BodyChoice.Deferred;
            if (TryTakeUndressed(out item)) return BodyChoice.Redress;
            int expendable = OldestExpendable();
            if (expendable >= 0) { item = TakeAt(expendable); return BodyChoice.Redress; }
            if (owned < maximum && items.Count < spare) return BodyChoice.Grow;
            if (TryTakeVictim(out item)) return BodyChoice.Redress;
            return BodyChoice.Grow;
        }

        public bool TryTrim(double seconds, double idleSeconds, out T item)
        {
            item = null!;
            if (items.Count <= spare) return false;
            double before = seconds - idleSeconds;
            for (int i = 0; i < items.Count; i++)
                if (returned[i] <= before && (Expendable(i) || !dressings[i].Dressed)) { item = TakeAt(i); return true; }
            if (returned[0] > before) return false;
            item = TakeAt(0);
            return true;
        }

        public void Clear() { items.Clear(); dressings.Clear(); returned.Clear(); wanted.Clear(); }
    }

    public interface IBodyLease
    {
        void Renew(string identityId, long generation, PersonDressing dressing, bool sameLook);
        void Invalidate();
    }

    public sealed class BodyLeases<TBody, TLease> where TBody : class where TLease : class, IBodyLease
    {
        private readonly Dictionary<TBody, TLease> leases = new Dictionary<TBody, TLease>();
        private readonly Func<TBody, TLease> create;
        private long generation;

        public BodyLeases(Func<TBody, TLease> create) { this.create = create ?? throw new ArgumentNullException(nameof(create)); }

        public int Count => leases.Count;
        public long Generation => generation;

        public TLease Lease(TBody body, string identityId, in PersonDressing dressing, bool sameLook)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (!leases.TryGetValue(body, out var lease)) { leases.Add(body, lease = create(body)); sameLook = false; }
            lease.Renew(identityId, checked(++generation), dressing, sameLook);
            return lease;
        }

        public void Forget(TBody body)
        {
            if (body != null && leases.TryGetValue(body, out var lease)) { lease.Invalidate(); leases.Remove(body); }
        }

        public void Clear()
        {
            foreach (var lease in leases.Values) lease.Invalidate();
            leases.Clear();
        }
    }

    public sealed class DressBuildBudget
    {
        public const int DefaultPerFrame = 1;
        public const double DefaultPerSecond = 4;
        private readonly int perFrame;
        private readonly double perSecond, burst;
        private double tokens, lastSeconds = double.NaN;
        private int frame = int.MinValue, builtThisFrame;

        public DressBuildBudget(int perFrame = DefaultPerFrame, double perSecond = DefaultPerSecond, double burst = DefaultPerSecond, DressFrameGate? gate = null)
        {
            if (perFrame < 1) throw new ArgumentOutOfRangeException(nameof(perFrame));
            if (!(perSecond > 0) || double.IsInfinity(perSecond)) throw new ArgumentOutOfRangeException(nameof(perSecond));
            if (!(burst >= 1) || double.IsInfinity(burst)) throw new ArgumentOutOfRangeException(nameof(burst));
            this.perFrame = perFrame; this.perSecond = perSecond; this.burst = burst; tokens = burst; Gate = gate;
        }

        public DressFrameGate? Gate { get; }
        public long Granted { get; private set; }
        public long Deferred { get; private set; }

        public bool CanTake(int frame, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            double available = tokens;
            if (!double.IsNaN(lastSeconds) && seconds > lastSeconds) available = Math.Min(burst, tokens + (seconds - lastSeconds) * perSecond);
            int built = frame != this.frame ? 0 : builtThisFrame;
            return built < perFrame && available >= 1 && (Gate == null || Gate.Allows(frame, seconds));
        }

        public bool TryTake(int frame, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) { Deferred++; return false; }
            if (!double.IsNaN(lastSeconds) && seconds > lastSeconds) tokens = Math.Min(burst, tokens + (seconds - lastSeconds) * perSecond);
            if (double.IsNaN(lastSeconds) || seconds > lastSeconds) lastSeconds = seconds;
            if (frame != this.frame) { this.frame = frame; builtThisFrame = 0; }
            if (builtThisFrame >= perFrame || tokens < 1 || Gate != null && !Gate.Allows(frame, seconds)) { Deferred++; return false; }
            builtThisFrame++; tokens -= 1; Granted++;
            Gate?.Built(frame, seconds);
            return true;
        }
    }

    public sealed class DressFrameGate
    {
        public const int DefaultQuietFrames = 8;
        public const int DefaultMaximumFrames = 30;
        public const double DefaultTargetMilliseconds = 4;
        public const double DefaultEstimateMilliseconds = 4;
        public const double DefaultMaximumSeconds = 1.25;
        private readonly Func<double> elapsed;
        private readonly int quietFrames, maximumFrames;
        private readonly double target, maximumSeconds;
        private int lastBuild;
        private double lastSeconds = double.NaN;
        private bool built;

        public DressFrameGate(Func<double> elapsedMilliseconds, int quietFrames = DefaultQuietFrames, int maximumFrames = DefaultMaximumFrames,
            double targetMilliseconds = DefaultTargetMilliseconds, double estimateMilliseconds = DefaultEstimateMilliseconds,
            double maximumSeconds = DefaultMaximumSeconds)
        {
            if (!(maximumSeconds > 0) || double.IsInfinity(maximumSeconds)) throw new ArgumentOutOfRangeException(nameof(maximumSeconds));
            this.maximumSeconds = maximumSeconds;
            elapsed = elapsedMilliseconds ?? throw new ArgumentNullException(nameof(elapsedMilliseconds));
            if (quietFrames < 1) throw new ArgumentOutOfRangeException(nameof(quietFrames));
            if (maximumFrames < quietFrames) throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            if (!(targetMilliseconds > 0) || double.IsInfinity(targetMilliseconds)) throw new ArgumentOutOfRangeException(nameof(targetMilliseconds));
            if (!(estimateMilliseconds >= 0) || double.IsInfinity(estimateMilliseconds)) throw new ArgumentOutOfRangeException(nameof(estimateMilliseconds));
            this.quietFrames = quietFrames; this.maximumFrames = maximumFrames; target = targetMilliseconds; EstimateMilliseconds = estimateMilliseconds;
        }

        public double EstimateMilliseconds { get; private set; }
        public long Waited { get; private set; }
        public long Fitted { get; private set; }

        public bool Allows(int frame, double seconds = double.NaN)
        {
            if (!built) return true;
            long gap = (long)frame - lastBuild;
            if (gap < 0 || gap >= maximumFrames || Overdue(seconds)) return true;
            if (gap < quietFrames) return false;
            double now = elapsed();
            return !double.IsNaN(now) && !double.IsInfinity(now) && now + EstimateMilliseconds <= target;
        }

        private bool Overdue(double seconds) => !double.IsNaN(seconds) && !double.IsInfinity(seconds) && !double.IsNaN(lastSeconds) && seconds - lastSeconds >= maximumSeconds;

        internal void Built(int frame, double seconds = double.NaN)
        {
            if (built)
            {
                long gap = (long)frame - lastBuild;
                if (gap >= 0 && gap < maximumFrames && !Overdue(seconds)) Fitted++; else Waited++;
            }
            built = true; lastBuild = frame;
            lastSeconds = double.IsInfinity(seconds) ? double.NaN : seconds;
        }

        public void Observe(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0) return;
            EstimateMilliseconds = milliseconds >= EstimateMilliseconds ? milliseconds : EstimateMilliseconds + (milliseconds - EstimateMilliseconds) * .125;
        }
    }
}
