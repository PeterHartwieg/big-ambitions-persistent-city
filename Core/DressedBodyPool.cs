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

        public bool TryTake(in PersonDressing wanted, out T item, out bool dressed)
        {
            item = null!; dressed = false;
            if (items.Count == 0) return false;
            int at = 0;
            for (int i = items.Count - 1; i >= 0; i--)
                if (dressings[i].Matches(wanted)) { at = i; dressed = true; break; }
            item = items[at];
            items.RemoveAt(at); dressings.RemoveAt(at);
            return true;
        }

        public void Clear() { items.Clear(); dressings.Clear(); }
    }
}
