using System;

namespace PersistentCity.Core
{
    public readonly struct OwnCarStep
    {
        public OwnCarStep(double along, double speed, double moved, bool atCorner)
        { Along = along; Speed = speed; Moved = moved; AtCorner = atCorner; }
        public double Along { get; }
        public double Speed { get; }
        public double Moved { get; }
        public bool AtCorner { get; }
    }

    public static class OwnCarMotion
    {
        public const double CruiseMetresPerSecond = 50 / 3.6;
        public const double AccelerationMetresPerSecondSquared = 3;
        public const double BrakingMetresPerSecondSquared = 6;
        public const double TurnDegreesPerSecond = 180;
        private const double Epsilon = 1e-9;

        public const double ObstacleGapMetres = 1.5;

        public static double LookAheadMetres(double speed)
            => (Position.Finite(speed) && speed > 0 ? speed * speed / (2 * BrakingMetresPerSecondSquared) : 0) + ObstacleGapMetres + 1;

        public static PhysicalTrafficCommandLimit Safe(PhysicalTrafficCommandLimit limit, double? obstacleMetres, bool redAhead, double toCorner)
        {
            if (limit.HoldPosition) return limit;
            double advance = limit.MaximumAdvance;
            if (obstacleMetres.HasValue) advance = Math.Min(advance, Math.Max(0, obstacleMetres.Value - ObstacleGapMetres));
            if (redAhead) advance = Math.Min(advance, Math.Max(0, toCorner));
            return new PhysicalTrafficCommandLimit(limit.Direction, advance, false);
        }

        public static double Yaw(double current, double target, double seconds, bool hold)
        {
            if (hold || !Position.Finite(current) || !Position.Finite(target) || !Position.Finite(seconds) || seconds <= 0) return current;
            double delta = ((target - current) % 360 + 540) % 360 - 180;
            double turn = TurnDegreesPerSecond * seconds;
            return Math.Abs(delta) <= turn ? target : current + Math.Sign(delta) * turn;
        }

        public static OwnCarStep Step(double along, double length, double speed, PhysicalTrafficCommandLimit limit, double seconds)
        {
            if (!Position.Finite(length) || length < 0) length = 0;
            if (!Position.Finite(along)) along = 0;
            along = Math.Max(0, Math.Min(length, along));
            if (!Position.Finite(speed) || speed < 0) speed = 0;
            bool corner = along >= length - Epsilon;
            if (limit.HoldPosition || !Position.Finite(seconds) || seconds <= 0 || !Position.Finite(limit.MaximumAdvance))
                return new OwnCarStep(along, limit.HoldPosition ? 0 : speed, 0, corner);
            double room = Math.Max(0, Math.Min(limit.MaximumAdvance, length - along));
            double target = Math.Min(CruiseMetresPerSecond, Math.Sqrt(2 * BrakingMetresPerSecondSquared * room));
            double next = speed < target ? Math.Min(target, speed + AccelerationMetresPerSecondSquared * seconds) : target;
            double moved = Math.Min(room, next * seconds);
            if (room - moved <= BrakingMetresPerSecondSquared * seconds * seconds) moved = room;
            if (room <= Epsilon) next = 0;
            double after = Math.Min(length, along + moved);
            return new OwnCarStep(after, next, moved, after >= length - Epsilon);
        }
    }
}
