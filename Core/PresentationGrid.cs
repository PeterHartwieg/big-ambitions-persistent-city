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
            public readonly List<Registration> Registrations = new List<Registration>();
            public int Index;
            public int UnplacedAt = -1;
            public int Stamp;
        }

        private readonly struct Registration
        {
            public Registration(int cell, int slot) { Cell = cell; Slot = slot; }
            public int Cell { get; }
            public int Slot { get; }
        }

        private readonly struct Slot
        {
            public Slot(int entry, int registration) { Entry = entry; Registration = registration; }
            public int Entry { get; }
            public int Registration { get; }
        }

        private readonly Func<string, Position?>? placePosition;
        private readonly Dictionary<CitizenState, Entry> entries = new Dictionary<CitizenState, Entry>();
        private readonly Dictionary<long, int> cells = new Dictionary<long, int>();
        private readonly List<List<Slot>> cellSlots = new List<List<Slot>>();
        private readonly List<Entry> entrySlots = new List<Entry>();
        private readonly List<Entry> unplaced = new List<Entry>();
        private readonly Stack<Entry> pool = new Stack<Entry>();
        private readonly HashSet<long> scratch = new HashSet<long>();
        private readonly List<long> expected = new List<long>();
        private readonly List<long> routeCells = new List<long>();
        private int generation;
        private int slotCount;

        public PresentationGrid(Func<string, Position?>? placePosition = null) => this.placePosition = placePosition;

        internal void TakeSpareCells(PresentationGrid retired)
        {
            if (ReferenceEquals(retired, this)) return;
            int peak = retired.entries.Count + retired.pool.Count;
            foreach (var pair in retired.cells)
            {
                var slots = retired.cellSlots[pair.Value];
                if (slots.Count == 0 || cells.ContainsKey(pair.Key)) continue;
                cells.Add(pair.Key, cellSlots.Count);
                cellSlots.Add(slots);
            }
            retired.Clear();
            while (retired.pool.Count > 0 && entries.Count + pool.Count < peak)
            {
                var entry = retired.pool.Pop();
                entry.Stamp = 0;
                entry.Index = entrySlots.Count;
                entrySlots.Add(entry);
                pool.Push(entry);
            }
            retired.cells.Clear();
            retired.cellSlots.Clear();
            retired.entrySlots.Clear();
            retired.pool.Clear();
        }

        internal int Pooled => pool.Count;

        public int Count => entries.Count;
        public int Unplaced => unplaced.Count;
        public int Registrations => slotCount;
        public long Filings { get; private set; }
        public bool Contains(CitizenState c) => entries.ContainsKey(c);

        internal void Clear()
        {
            foreach (var entry in entries.Values) Recycle(entry);
            entries.Clear(); unplaced.Clear(); slotCount = 0;
            foreach (var list in cellSlots) list.Clear();
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
                if (pool.Count != 0) entry = pool.Pop();
                else
                {
                    entry = new Entry { Index = entrySlots.Count };
                    entrySlots.Add(entry);
                }
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
                    if (cells.TryGetValue(Key(x, z), out int cell)) Collect(cellSlots[cell], stamp, output);
            return output;
        }

        private void QueryAll(int stamp, List<IPersonView> output)
        {
            foreach (int cell in cells.Values) Collect(cellSlots[cell], stamp, output);
        }

        private void Collect(List<Slot> list, int stamp, List<IPersonView> output)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var entry = entrySlots[list[i].Entry];
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
                if (!Same(entry, kind, c, at) || !ReferenceEquals(entrySlots[entry.Index], entry)) return false;
                bool placed = entry.UnplacedAt < 0;
                if (recompute && placed != (kind != Anchor.Unplaced && CellsOf(kind, c.Journey, at, expected))) return false;
                if (!placed)
                {
                    if (entry.Registrations.Count != 0 || entry.UnplacedAt < 0 || entry.UnplacedAt >= unplaced.Count
                        || !ReferenceEquals(unplaced[entry.UnplacedAt], entry)) return false;
                    unplacedSeen++;
                    continue;
                }
                if (kind == Anchor.Unplaced || entry.Registrations.Count == 0 || recompute && expected.Count != entry.Registrations.Count) return false;
                for (int k = 0; k < entry.Registrations.Count; k++)
                {
                    var registration = entry.Registrations[k];
                    if (recompute && (!cells.TryGetValue(expected[k], out int cell) || cell != registration.Cell)) return false;
                    var list = cellSlots[registration.Cell];
                    int slot = registration.Slot;
                    if (slot < 0 || slot >= list.Count || list[slot].Entry != entry.Index || list[slot].Registration != k) return false;
                }
                registered += entry.Registrations.Count;
            }
            int stored = 0;
            foreach (var list in cellSlots) stored += list.Count;
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
            if (kind == Anchor.Unplaced || !CellsOf(kind, journey, at, routeCells))
            { entry.UnplacedAt = unplaced.Count; unplaced.Add(entry); return; }
            for (int k = 0; k < routeCells.Count; k++)
            {
                long key = routeCells[k];
                if (!cells.TryGetValue(key, out int cell))
                {
                    cell = cellSlots.Count;
                    cells.Add(key, cell);
                    cellSlots.Add(new List<Slot>());
                }
                var list = cellSlots[cell];
                entry.Registrations.Add(new Registration(cell, list.Count));
                list.Add(new Slot(entry.Index, k));
            }
            slotCount += entry.Registrations.Count;
        }

        private void Unregister(Entry entry)
        {
            if (entry.UnplacedAt >= 0)
            {
                int at = entry.UnplacedAt, last = unplaced.Count - 1;
                if (at != last) { var moved = unplaced[last]; unplaced[at] = moved; moved.UnplacedAt = at; }
                unplaced.RemoveAt(last);
            }
            for (int k = 0; k < entry.Registrations.Count; k++)
            {
                var registration = entry.Registrations[k];
                var list = cellSlots[registration.Cell];
                int at = registration.Slot, last = list.Count - 1;
                if (at != last)
                {
                    var moved = list[last];
                    list[at] = moved;
                    entrySlots[moved.Entry].Registrations[moved.Registration] = new Registration(registration.Cell, at);
                }
                list.RemoveAt(last);
            }
            slotCount -= entry.Registrations.Count;
            entry.Registrations.Clear();
            entry.Kind = Anchor.None; entry.Journey = null; entry.UnplacedAt = -1;
        }

        private void Recycle(Entry entry)
        {
            entry.Registrations.Clear();
            entry.Citizen = null!; entry.Kind = Anchor.None; entry.Journey = null; entry.UnplacedAt = -1;
            pool.Push(entry);
        }

        private static readonly long[] Unplaceable = new long[0];
        [ThreadStatic] private static List<long>? precomputeCells;
        [ThreadStatic] private static HashSet<long>? precomputeScratch;

        internal static void Precompute(Route journey)
        {
            if (journey == null || !journey.IsCommitted || journey.PresentationCells != null || journey.Legs.Count == 0) return;
            var output = precomputeCells ??= new List<long>();
            var seen = precomputeScratch ??= new HashSet<long>();
            journey.PresentationCells = RouteCells(journey, output, seen) ? output.ToArray() : Unplaceable;
            output.Clear(); seen.Clear();
        }

        private bool CellsOf(Anchor kind, Route? journey, Position at, List<long> output)
        {
            output.Clear(); scratch.Clear();
            if (kind == Anchor.Place) { output.Add(Key(CellOf(at.X), CellOf(at.Z))); return true; }
            var cached = journey!.IsCommitted ? journey.PresentationCells : null;
            if (cached != null)
            {
                journey.PresentationCells = null;
                if (ReferenceEquals(cached, Unplaceable)) return false;
                output.AddRange(cached);
                return true;
            }
            return RouteCells(journey, output, scratch);
        }

        private static bool RouteCells(Route journey, List<long> output, HashSet<long> scratch)
        {
            var legs = journey.Legs;
            for (int i = 0; i < legs.Count; i++)
            {
                Position a = legs[i].From, b = legs[i].To;
                if (!a.IsFinite || !b.IsFinite || Math.Abs(a.X - b.X) > MaxLegSpan * CellMetres
                    || Math.Abs(a.Z - b.Z) > MaxLegSpan * CellMetres) { output.Clear(); return false; }
                AddSegment(a, b, output, scratch);
            }
            return true;
        }

        private static void AddSegment(Position a, Position b, List<long> output, HashSet<long> scratch)
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
