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
        private readonly List<T> items = new List<T>();
        private readonly List<PersonDressing> dressings = new List<PersonDressing>();

        public int Count => items.Count;

        public void Return(T item, PersonDressing dressing)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            items.Add(item); dressings.Add(dressing);
        }

        public bool TryTakeDressed(in PersonDressing wanted, out T item)
        {
            item = null!;
            for (int i = items.Count - 1; i >= 0; i--)
                if (dressings[i].Matches(wanted)) { item = items[i]; items.RemoveAt(i); dressings.RemoveAt(i); return true; }
            return false;
        }

        public bool TryTakeOldest(out T item)
        {
            item = null!;
            if (items.Count == 0) return false;
            item = items[0];
            items.RemoveAt(0); dressings.RemoveAt(0);
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
            if (owned < maximum) return BodyChoice.Grow;
            TryTakeOldest(out item);
            return BodyChoice.Redress;
        }

        public void Clear() { items.Clear(); dressings.Clear(); }
    }

    public sealed class DressBuildBudget
    {
        public const int DefaultPerFrame = 1;
        public const double DefaultPerSecond = 4;
        private readonly int perFrame;
        private readonly double perSecond, burst;
        private double tokens, lastSeconds = double.NaN;
        private int frame = int.MinValue, builtThisFrame;

        public DressBuildBudget(int perFrame = DefaultPerFrame, double perSecond = DefaultPerSecond, double burst = DefaultPerSecond)
        {
            if (perFrame < 1) throw new ArgumentOutOfRangeException(nameof(perFrame));
            if (!(perSecond > 0) || double.IsInfinity(perSecond)) throw new ArgumentOutOfRangeException(nameof(perSecond));
            if (!(burst >= 1) || double.IsInfinity(burst)) throw new ArgumentOutOfRangeException(nameof(burst));
            this.perFrame = perFrame; this.perSecond = perSecond; this.burst = burst; tokens = burst;
        }

        public long Granted { get; private set; }
        public long Deferred { get; private set; }

        public bool CanTake(int frame, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            double available = tokens;
            if (!double.IsNaN(lastSeconds) && seconds > lastSeconds) available = Math.Min(burst, tokens + (seconds - lastSeconds) * perSecond);
            int built = frame != this.frame ? 0 : builtThisFrame;
            return built < perFrame && available >= 1;
        }

        public bool TryTake(int frame, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) { Deferred++; return false; }
            if (!double.IsNaN(lastSeconds) && seconds > lastSeconds) tokens = Math.Min(burst, tokens + (seconds - lastSeconds) * perSecond);
            if (double.IsNaN(lastSeconds) || seconds > lastSeconds) lastSeconds = seconds;
            if (frame != this.frame) { this.frame = frame; builtThisFrame = 0; }
            if (builtThisFrame >= perFrame || tokens < 1) { Deferred++; return false; }
            builtThisFrame++; tokens -= 1; Granted++;
            return true;
        }
    }
}
