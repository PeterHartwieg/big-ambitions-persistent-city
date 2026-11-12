using System;

namespace PersistentCity.Core
{
    /// <summary>One frame of an own car on its current road segment: where it is along the segment, its speed, how far it
    /// moved and whether it stands on the segment's end corner.</summary>
    public readonly struct OwnCarStep
    {
        public OwnCarStep(double along, double speed, double moved, bool atCorner)
        { Along = along; Speed = speed; Moved = moved; AtCorner = atCorner; }
        public double Along { get; }
        public double Speed { get; }
        public double Moved { get; }
        public bool AtCorner { get; }
    }

    /// <summary>
    /// GR-9 (G7): the mod's own kinematic car. It drives along the directed road segment between two native waypoints
    /// under the follower's command limit (<see cref="PhysicalTrafficCommandLimit"/>): never past the kernel's coordinate,
    /// never past the segment's end corner, standing while the command holds. Speed rises at a fixed acceleration up to a
    /// cruise speed and falls so the car can stop at its limit; it never moves further than the limit allows in one frame.
    /// A body writes nothing back, so the journey, its times and its arrival do not depend on this motion.
    /// </summary>
    public static class OwnCarMotion
    {
        /// <summary>Top speed, 50 km/h: above the kernel's 40 km/h cap so a car can catch up a short lag.</summary>
        public const double CruiseMetresPerSecond = 50 / 3.6;
        public const double AccelerationMetresPerSecondSquared = 3;
        public const double BrakingMetresPerSecondSquared = 6;
        /// <summary>Turn rate of the visible body, degrees per second; the position is on the segment either way.</summary>
        public const double TurnDegreesPerSecond = 180;
        private const double Epsilon = 1e-9;

        /// <summary>Gap kept to an obstacle ahead (another car, the player, a walker), metres.</summary>
        public const double ObstacleGapMetres = 1.5;

        /// <summary>The distance to look ahead for obstacles: the braking distance at the current speed plus the gap.</summary>
        public static double LookAheadMetres(double speed)
            => (Position.Finite(speed) && speed > 0 ? speed * speed / (2 * BrakingMetresPerSecondSquared) : 0) + ObstacleGapMetres + 1;

        /// <summary>
        /// The command limit after the own car's own safety: never closer than <see cref="ObstacleGapMetres"/> to an obstacle
        /// <paramref name="obstacleMetres"/> ahead of its front, and not past its corner while the native light there is red.
        /// Only shortens the limit; a held car lags, and the follower's catch-up and fade handle lag.
        /// </summary>
        public static PhysicalTrafficCommandLimit Safe(PhysicalTrafficCommandLimit limit, double? obstacleMetres, bool redAhead, double toCorner)
        {
            if (limit.HoldPosition) return limit;
            double advance = limit.MaximumAdvance;
            if (obstacleMetres.HasValue) advance = Math.Min(advance, Math.Max(0, obstacleMetres.Value - ObstacleGapMetres));
            if (redAhead) advance = Math.Min(advance, Math.Max(0, toCorner));
            return new PhysicalTrafficCommandLimit(limit.Direction, advance, false);
        }

        /// <summary>The visible yaw (degrees) after one frame: turned towards the segment heading at
        /// <see cref="TurnDegreesPerSecond"/>, unchanged while held so the root (which pivots on the front axle) stands.</summary>
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
            // The fastest speed from which the car still stops within its room.
            double target = Math.Min(CruiseMetresPerSecond, Math.Sqrt(2 * BrakingMetresPerSecondSquared * room));
            double next = speed < target ? Math.Min(target, speed + AccelerationMetresPerSecondSquared * seconds) : target;
            double moved = Math.Min(room, next * seconds);
            // Within one braking step of the limit the car lands on it instead of creeping towards it.
            if (room - moved <= BrakingMetresPerSecondSquared * seconds * seconds) moved = room;
            if (room <= Epsilon) next = 0;
            double after = Math.Min(length, along + moved);
            return new OwnCarStep(after, next, moved, after >= length - Epsilon);
        }
    }
}
