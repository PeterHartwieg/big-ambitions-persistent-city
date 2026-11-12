using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class PresentationGrid
    {
        public const double CellMetres = 64;
        private const double Pad = 0.01;
        private const int CellLimit = 1 << 28;
        private const int MaxLegSpan = 4096;

        private enum Anchor { None, Route, Place, Unplaced }

        private sealed class Entry
        {
            public CitizenState Citizen = null!;
            public Anchor Kind;
            public Route? Journey;
            public Position At;
            public readonly List<long> Cells = new List<long>();
            public readonly List<List<Slot>> Lists = new List<List<Slot>>();
            public readonly List<int> Slots = new List<int>();
            public int UnplacedAt = -1;
            public int Stamp;
        }

        private readonly struct Slot
        {
            public Slot(Entry entry, int k) { Entry = entry; K = k; }
            public Entry Entry { get; }
            public int K { get; }
        }

        private readonly Func<string, Position?>? placePosition;
        private readonly Dictionary<CitizenState, Entry> entries = new Dictionary<CitizenState, Entry>();
        private readonly Dictionary<long, List<Slot>> cells = new Dictionary<long, List<Slot>>();
        private readonly List<Entry> unplaced = new List<Entry>();
        private readonly Stack<Entry> pool = new Stack<Entry>();
        private readonly HashSet<long> scratch = new HashSet<long>();
        private readonly List<long> expected = new List<long>();
        private int generation;
        private int slotCount;

        public PresentationGrid(Func<string, Position?>? placePosition = null) => this.placePosition = placePosition;

        public int Count => entries.Count;
        public int Unplaced => unplaced.Count;
        public int Registrations => slotCount;
        public long Filings { get; private set; }
        public bool Contains(CitizenState c) => entries.ContainsKey(c);

        internal void Clear()
        {
            foreach (var entry in entries.Values) Recycle(entry);
            entries.Clear(); unplaced.Clear(); slotCount = 0;
            foreach (var list in cells.Values) list.Clear();
        }

        internal void File(CitizenState c)
        {
            Anchor kind = AnchorOf(c, out Position at);
            if (entries.TryGetValue(c, out var entry))
            {
                if (Same(entry, kind, c, at)) return;
                Unregister(entry);
            }
            else
            {
                entry = pool.Count != 0 ? pool.Pop() : new Entry();
                entry.Citizen = c;
                entries.Add(c, entry);
            }
            Register(entry, kind, c.Journey, at);
            Filings++;
        }

        internal void Remove(CitizenState c)
        {
            if (!entries.TryGetValue(c, out var entry)) return;
            Unregister(entry);
            entries.Remove(c);
            Recycle(entry);
        }

        public IReadOnlyList<IPersonView> Query(Position focus, double radius, List<IPersonView> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            for (int i = 0; i < unplaced.Count; i++) output.Add(unplaced[i].Citizen);
            if (!(radius >= 0) || !focus.IsFinite || slotCount == 0) return output;
            int stamp = NextStamp();
            if (double.IsInfinity(radius)) { QueryAll(stamp, output); return output; }
            int x0 = CellOf(focus.X - radius - Pad), x1 = CellOf(focus.X + radius + Pad);
            int z0 = CellOf(focus.Z - radius - Pad), z1 = CellOf(focus.Z + radius + Pad);
            if ((double)(x1 - x0 + 1) * (z1 - z0 + 1) > cells.Count) { QueryAll(stamp, output); return output; }
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if (cells.TryGetValue(Key(x, z), out var list)) Collect(list, stamp, output);
            return output;
        }

        private void QueryAll(int stamp, List<IPersonView> output)
        {
            foreach (var list in cells.Values) Collect(list, stamp, output);
        }

        private static void Collect(List<Slot> list, int stamp, List<IPersonView> output)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i].Entry;
                if (entry.Stamp == stamp) continue;
                entry.Stamp = stamp;
                output.Add(entry.Citizen);
            }
        }

        private int NextStamp()
        {
            if (++generation == int.MaxValue)
            {
                foreach (var entry in entries.Values) entry.Stamp = 0;
                foreach (var entry in pool) entry.Stamp = 0;
                generation = 1;
            }
            return generation;
        }

        public bool Matches(IReadOnlyList<CitizenState> members) => Matches(members, false);

        internal bool Matches(IReadOnlyList<CitizenState> members, bool recompute)
        {
            if (members.Count != entries.Count) return false;
            int registered = 0, unplacedSeen = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var c = members[i];
                if (!entries.TryGetValue(c, out var entry) || !ReferenceEquals(entry.Citizen, c)) return false;
                Anchor kind = AnchorOf(c, out Position at);
                if (!Same(entry, kind, c, at) || entry.Cells.Count != entry.Slots.Count || entry.Cells.Count != entry.Lists.Count) return false;
                bool placed = entry.UnplacedAt < 0;
                if (recompute && placed != (kind != Anchor.Unplaced && CellsOf(kind, c.Journey, at, expected))) return false;
                if (!placed)
                {
                    if (entry.Cells.Count != 0 || entry.UnplacedAt < 0 || entry.UnplacedAt >= unplaced.Count
                        || !ReferenceEquals(unplaced[entry.UnplacedAt], entry)) return false;
                    unplacedSeen++;
                    continue;
                }
                if (kind == Anchor.Unplaced || entry.Cells.Count == 0 || recompute && expected.Count != entry.Cells.Count) return false;
                for (int k = 0; k < entry.Cells.Count; k++)
                {
                    if (recompute && (expected[k] != entry.Cells[k] || !cells.TryGetValue(expected[k], out var keyed)
                        || !ReferenceEquals(keyed, entry.Lists[k]))) return false;
                    var list = entry.Lists[k];
                    int slot = entry.Slots[k];
                    if (slot < 0 || slot >= list.Count || !ReferenceEquals(list[slot].Entry, entry) || list[slot].K != k) return false;
                }
                registered += entry.Cells.Count;
            }
            int stored = 0;
            foreach (var list in cells.Values) stored += list.Count;
            return unplacedSeen == unplaced.Count && registered == slotCount && stored == slotCount;
        }

        private Anchor AnchorOf(CitizenState c, out Position at)
        {
            at = default;
            if (c.Phase == CitizenPhase.Travelling)
                return c.Journey != null && c.Journey.Legs.Count != 0 ? Anchor.Route : Anchor.Unplaced;
            var position = placePosition?.Invoke(c.PlaceId);
            if (!position.HasValue || !position.Value.IsFinite) return Anchor.Unplaced;
            at = position.Value;
            return Anchor.Place;
        }

        private static bool Same(Entry entry, Anchor kind, CitizenState c, Position at)
        {
            if (entry.Kind != kind) return false;
            if (kind == Anchor.Route) return ReferenceEquals(entry.Journey, c.Journey);
            if (kind == Anchor.Place) return entry.At.X.Equals(at.X) && entry.At.Z.Equals(at.Z);
            return true;
        }

        private void Register(Entry entry, Anchor kind, Route? journey, Position at)
        {
            entry.Kind = kind;
            entry.Journey = kind == Anchor.Route ? journey : null;
            entry.At = at;
            if (kind == Anchor.Unplaced || !CellsOf(kind, journey, at, entry.Cells))
            { entry.Cells.Clear(); entry.UnplacedAt = unplaced.Count; unplaced.Add(entry); return; }
            for (int k = 0; k < entry.Cells.Count; k++)
            {
                long key = entry.Cells[k];
                if (!cells.TryGetValue(key, out var list)) cells.Add(key, list = new List<Slot>());
                entry.Lists.Add(list);
                entry.Slots.Add(list.Count);
                list.Add(new Slot(entry, k));
            }
            slotCount += entry.Cells.Count;
        }

        private void Unregister(Entry entry)
        {
            if (entry.UnplacedAt >= 0)
            {
                int at = entry.UnplacedAt, last = unplaced.Count - 1;
                if (at != last) { var moved = unplaced[last]; unplaced[at] = moved; moved.UnplacedAt = at; }
                unplaced.RemoveAt(last);
            }
            for (int k = 0; k < entry.Cells.Count; k++)
            {
                var list = entry.Lists[k];
                int at = entry.Slots[k], last = list.Count - 1;
                if (at != last)
                {
                    var moved = list[last];
                    list[at] = moved;
                    moved.Entry.Slots[moved.K] = at;
                }
                list.RemoveAt(last);
            }
            slotCount -= entry.Cells.Count;
            entry.Cells.Clear(); entry.Slots.Clear(); entry.Lists.Clear();
            entry.Kind = Anchor.None; entry.Journey = null; entry.UnplacedAt = -1;
        }

        private void Recycle(Entry entry)
        {
            entry.Cells.Clear(); entry.Slots.Clear(); entry.Lists.Clear();
            entry.Citizen = null!; entry.Kind = Anchor.None; entry.Journey = null; entry.UnplacedAt = -1;
            pool.Push(entry);
        }

        private bool CellsOf(Anchor kind, Route? journey, Position at, List<long> output)
        {
            output.Clear(); scratch.Clear();
            if (kind == Anchor.Place) { output.Add(Key(CellOf(at.X), CellOf(at.Z))); return true; }
            var legs = journey!.Legs;
            for (int i = 0; i < legs.Count; i++)
            {
                Position a = legs[i].From, b = legs[i].To;
                if (!a.IsFinite || !b.IsFinite || Math.Abs(a.X - b.X) > MaxLegSpan * CellMetres
                    || Math.Abs(a.Z - b.Z) > MaxLegSpan * CellMetres) { output.Clear(); return false; }
                AddSegment(a, b, output);
            }
            return true;
        }

        private void AddSegment(Position a, Position b, List<long> output)
        {
            double minX = Math.Min(a.X, b.X) - Pad, maxX = Math.Max(a.X, b.X) + Pad;
            double dx = b.X - a.X;
            bool vertical = Math.Abs(dx) <= 1e-9;
            int c0 = CellOf(minX), c1 = CellOf(maxX);
            for (int column = c0; column <= c1; column++)
            {
                double z0, z1;
                if (vertical) { z0 = Math.Min(a.Z, b.Z); z1 = Math.Max(a.Z, b.Z); }
                else
                {
                    double lo = Math.Max(minX, column * CellMetres - Pad), hi = Math.Min(maxX, (column + 1) * CellMetres + Pad);
                    double ta = Clamp01((lo - a.X) / dx), tb = Clamp01((hi - a.X) / dx);
                    double za = a.Z + (b.Z - a.Z) * ta, zb = a.Z + (b.Z - a.Z) * tb;
                    z0 = Math.Min(za, zb); z1 = Math.Max(za, zb);
                }
                int r0 = CellOf(z0 - Pad), r1 = CellOf(z1 + Pad);
                for (int row = r0; row <= r1; row++)
                {
                    long key = Key(column, row);
                    if (scratch.Add(key)) output.Add(key);
                }
            }
        }

        private static double Clamp01(double t) => t < 0 ? 0 : t > 1 ? 1 : t;

        internal static int CellOf(double metres)
        {
            double cell = Math.Floor(metres / CellMetres);
            return !(cell >= -CellLimit) ? -CellLimit : cell > CellLimit ? CellLimit : (int)cell;
        }

        internal static long Key(int x, int z) => ((long)x << 32) | (uint)z;
    }
}
