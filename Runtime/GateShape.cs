using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal enum GateShapeSource { Colliders, Renderers, Building, Fallback }

    internal readonly struct GateExtent
    {
        public GateExtent(Position min, Position max) { Min = min; Max = max; }
        public Position Min { get; }
        public Position Max { get; }
        public bool Usable => Min.IsFinite && Max.IsFinite && Max.X >= Min.X && Max.Y >= Min.Y && Max.Z >= Min.Z
            && (Max.X - Min.X) * (Max.X - Min.X) + (Max.Y - Min.Y) * (Max.Y - Min.Y) + (Max.Z - Min.Z) * (Max.Z - Min.Z) >= 0.0001;
        public GateExtent Expanded(Position total) => new GateExtent(
            new Position(Min.X - total.X / 2, Min.Y - total.Y / 2, Min.Z - total.Z / 2),
            new Position(Max.X + total.X / 2, Max.Y + total.Y / 2, Max.Z + total.Z / 2));
        public static GateExtent Around(Position centre, Position size) => new GateExtent(
            new Position(centre.X - size.X / 2, centre.Y - size.Y / 2, centre.Z - size.Z / 2),
            new Position(centre.X + size.X / 2, centre.Y + size.Y / 2, centre.Z + size.Z / 2));
    }

    internal static class GateShape
    {
        public static readonly Position Margin = new Position(1, 2, 1);
        public static readonly Position FallbackSize = new Position(40, 20, 40);

        public static GateShapeSource Choose(Position door, GateExtent? colliders, Func<GateExtent?> renderers,
            Func<GateExtent?> building, out GateExtent gate)
        {
            if (colliders is GateExtent c && c.Usable) { gate = c.Expanded(Margin); return GateShapeSource.Colliders; }
            if (renderers() is GateExtent r && r.Usable) { gate = r.Expanded(Margin); return GateShapeSource.Renderers; }
            if (building() is GateExtent b && b.Usable) { gate = b.Expanded(Margin); return GateShapeSource.Building; }
            gate = GateExtent.Around(door, FallbackSize);
            return GateShapeSource.Fallback;
        }
    }

    internal sealed class GateShapeTally
    {
        private readonly int[] counts = new int[4];
        private readonly List<string> fallbackDoors = new List<string>();
        public int Count(GateShapeSource source) => counts[(int)source];
        public int FallbackGates => counts[(int)GateShapeSource.Fallback];
        public IReadOnlyList<string> FallbackDoors => fallbackDoors;
        public void Add(GateShapeSource source, string door)
        {
            counts[(int)source]++;
            if (source == GateShapeSource.Fallback) fallbackDoors.Add(door);
        }
        public bool SameAs(GateShapeTally? other)
        {
            if (other == null) return false;
            for (int i = 0; i < counts.Length; i++) if (counts[i] != other.counts[i]) return false;
            var left = new List<string>(fallbackDoors); left.Sort(StringComparer.Ordinal);
            var right = new List<string>(other.fallbackDoors); right.Sort(StringComparer.Ordinal);
            for (int i = 0; i < left.Count; i++) if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            return true;
        }
        public string? Report(GateShapeTally? previous)
        {
            if (FallbackGates == 0 && Count(GateShapeSource.Renderers) == 0 && Count(GateShapeSource.Building) == 0) return null;
            if (SameAs(previous)) return null;
            return "[Persistent City] Vehicle gate shapes: " + Count(GateShapeSource.Colliders) + " from colliders, "
                + Count(GateShapeSource.Renderers) + " from renderers, " + Count(GateShapeSource.Building) + " from building footprint, "
                + FallbackGates + " without a shape using the " + GateShape.FallbackSize.X + "x" + GateShape.FallbackSize.Y + "x" + GateShape.FallbackSize.Z + " fallback"
                + (FallbackGates == 0 ? "" : ": " + string.Join(", ", fallbackDoors));
        }
    }
}
