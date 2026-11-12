using System;

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
