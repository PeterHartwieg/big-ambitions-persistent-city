using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>The ground rectangle of an own car's body: its root (the model pivot) and half extents, turned by yaw.</summary>
    public readonly struct CarRectangle
    {
        public CarRectangle(Position centre, double yawDegrees, double halfLength, double halfWidth)
        { Centre = centre; YawDegrees = yawDegrees; HalfLength = halfLength; HalfWidth = halfWidth; }
        public Position Centre { get; }
        public double YawDegrees { get; }
        public double HalfLength { get; }
        public double HalfWidth { get; }
    }

    /// <summary>
    /// GR-9 (G7): the whole body of an own car at a pose. The car pivots on its front axle, so a turn swings the rear and
    /// sides even while the axle barely moves; a move is admitted only when the body at the proposed pose overlaps
    /// nothing. Per-frame moves are small (at most a few degrees and centimetres), so checking each proposed pose covers
    /// the swept area.
    /// </summary>
    public static class OwnCarFootprint
    {
        public static Position Forward(double yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180;
            return new Position(Math.Sin(r), 0, Math.Cos(r));
        }

        /// <summary>The body at a pose: the root placed so the front axle stands on <paramref name="front"/>.</summary>
        public static CarRectangle At(Position front, double yawDegrees, Position markerOffset, double length, double width)
            => new CarRectangle(PhysicalVehicleReference.Root(front, markerOffset, Forward(yawDegrees)), yawDegrees,
                Math.Max(0.5, length / 2), Math.Max(0.25, width / 2));

        /// <summary>Most substeps one frame's move is split into.</summary>
        public const int MaximumSubsteps = 16;

        /// <summary>
        /// The body over one frame's move, from one pose to the next: substeps of at most 0.25 m and 2 degrees (front axle
        /// and yaw interpolated, the short way round), each widened by a margin that covers how far any body point can
        /// travel between two substeps. Together the rectangles contain the whole swept area, also through a turn.
        /// </summary>
        public static List<CarRectangle> Sweep(Position fromFront, double fromYaw, Position toFront, double toYaw,
            Position markerOffset, double length, double width)
        {
            double turn = ((toYaw - fromYaw) % 360 + 540) % 360 - 180;
            double move = Position.Distance(fromFront, toFront);
            int steps = (int)Math.Ceiling(Math.Max(move / 0.25, Math.Abs(turn) / 2));
            steps = Math.Max(1, Math.Min(MaximumSubsteps, steps));
            var start = At(fromFront, fromYaw, markerOffset, length, width);
            // Farthest body point from the front axle (the pivot): it moves the most in a turn.
            double reach = Math.Sqrt(Math.Pow(Math.Abs(markerOffset.Z) + start.HalfLength, 2) + Math.Pow(Math.Abs(markerOffset.X) + start.HalfWidth, 2));
            double margin = (reach * Math.Abs(turn) * Math.PI / 180 + move) / (2 * steps);
            var boxes = new List<CarRectangle>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                var pose = At(Position.Lerp(fromFront, toFront, t), fromYaw + turn * t, markerOffset, length, width);
                boxes.Add(new CarRectangle(pose.Centre, pose.YawDegrees, pose.HalfLength + margin, pose.HalfWidth + margin));
            }
            return boxes;
        }

        /// <summary>Whether two rectangles overlap on the ground (separating-axis test).</summary>
        public static bool Overlaps(CarRectangle a, CarRectangle b)
        {
            foreach (var axis in new[] { Forward(a.YawDegrees), Right(a.YawDegrees), Forward(b.YawDegrees), Right(b.YawDegrees) })
            {
                double dx = b.Centre.X - a.Centre.X, dz = b.Centre.Z - a.Centre.Z;
                double distance = Math.Abs(dx * axis.X + dz * axis.Z);
                if (distance > Radius(a, axis) + Radius(b, axis)) return false;
            }
            return true;
        }

        private static Position Right(double yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180;
            return new Position(Math.Cos(r), 0, -Math.Sin(r));
        }

        private static double Radius(CarRectangle box, Position axis)
        {
            var f = Forward(box.YawDegrees); var r = Right(box.YawDegrees);
            return box.HalfLength * Math.Abs(f.X * axis.X + f.Z * axis.Z) + box.HalfWidth * Math.Abs(r.X * axis.X + r.Z * axis.Z);
        }
    }
}
