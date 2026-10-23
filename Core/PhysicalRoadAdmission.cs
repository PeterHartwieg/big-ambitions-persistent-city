using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct RoadBoundary
    {
        public RoadBoundary(double distance, bool enter, bool exit, int junction)
        { Distance = distance; Enter = enter; Exit = exit; Junction = junction; }
        public double Distance { get; }
        public bool Enter { get; }
        public bool Exit { get; }
        public int Junction { get; }
    }

    /// <summary>Read-only admission evidence; never supplies signal permission or occupancy.</summary>
    public static class PhysicalRoadAdmission
    {
        public static bool Allows(IReadOnlyList<RoadBoundary> boundaries, double progress, double vehicleLength, double margin = 1)
        {
            if (boundaries == null || boundaries.Count == 0 || boundaries.Count > 512
                || double.IsNaN(progress) || double.IsInfinity(progress) || progress < 0
                || double.IsNaN(vehicleLength) || double.IsInfinity(vehicleLength) || vehicleLength <= 0
                || double.IsNaN(margin) || double.IsInfinity(margin) || margin < 0) return false;
            int inside = -1; double previous = -1; bool blocked = false;
            foreach (var boundary in boundaries)
            {
                if (double.IsNaN(boundary.Distance) || double.IsInfinity(boundary.Distance)
                    || boundary.Distance < 0 || boundary.Distance <= previous || boundary.Enter == boundary.Exit
                    || boundary.Junction < 0) return false;
                previous = boundary.Distance;
                if (boundary.Enter)
                {
                    if (inside != -1) return false;
                    inside = boundary.Junction;
                    if (progress >= boundary.Distance || boundary.Distance - progress < vehicleLength + margin) blocked = true;
                }
                else
                {
                    if (inside != boundary.Junction) return false;
                    inside = -1;
                    // Reset only after the whole body has cleared this exit. Later entries can block again.
                    if (progress >= boundary.Distance + vehicleLength + margin) blocked = false;
                    else if (progress >= boundary.Distance) blocked = true;
                }
            }
            return inside == -1 && !blocked;
        }
    }
}
