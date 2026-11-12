using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct CarRectangle
    {
        public CarRectangle(Position centre, double yawDegrees, double halfLength, double halfWidth)
        { Centre = centre; YawDegrees = yawDegrees; HalfLength = halfLength; HalfWidth = halfWidth; }
        public Position Centre { get; }
        public double YawDegrees { get; }
        public double HalfLength { get; }
        public double HalfWidth { get; }
    }

    public static class OwnCarFootprint
    {
        public static Position Forward(double yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180;
            return new Position(Math.Sin(r), 0, Math.Cos(r));
        }

        public static CarRectangle At(Position front, double yawDegrees, Position markerOffset, double length, double width)
            => new CarRectangle(PhysicalVehicleReference.Root(front, markerOffset, Forward(yawDegrees)), yawDegrees,
                Math.Max(0.5, length / 2), Math.Max(0.25, width / 2));

        public const int MaximumSubsteps = 16;

        public static List<CarRectangle> Sweep(Position fromFront, double fromYaw, Position toFront, double toYaw,
            Position markerOffset, double length, double width)
        {
            var boxes = new List<CarRectangle>(MaximumSubsteps + 1);
            Sweep(fromFront, fromYaw, toFront, toYaw, markerOffset, length, width, boxes);
            return boxes;
        }

        public static void Sweep(Position fromFront, double fromYaw, Position toFront, double toYaw,
            Position markerOffset, double length, double width, List<CarRectangle> boxes)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            boxes.Clear();
            double turn = ((toYaw - fromYaw) % 360 + 540) % 360 - 180;
            double move = Position.Distance(fromFront, toFront);
            int steps = (int)Math.Ceiling(Math.Max(move / 0.25, Math.Abs(turn) / 2));
            steps = Math.Max(1, Math.Min(MaximumSubsteps, steps));
            var start = At(fromFront, fromYaw, markerOffset, length, width);
            double reach = Math.Sqrt(Math.Pow(Math.Abs(markerOffset.Z) + start.HalfLength, 2) + Math.Pow(Math.Abs(markerOffset.X) + start.HalfWidth, 2));
            double margin = (reach * Math.Abs(turn) * Math.PI / 180 + move) / (2 * steps);
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                var pose = At(Position.Lerp(fromFront, toFront, t), fromYaw + turn * t, markerOffset, length, width);
                boxes.Add(new CarRectangle(pose.Centre, pose.YawDegrees, pose.HalfLength + margin, pose.HalfWidth + margin));
            }
        }

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
