using System;

namespace PersistentCity.Core
{
    /// <summary>Places a root so its native front marker retains the committed XZ route position.</summary>
    public static class PhysicalVehicleReference
    {
        public static Position Root(Position marker, Position localOffset, Position forward)
        {
            double length = Math.Sqrt(forward.X * forward.X + forward.Z * forward.Z);
            if (!marker.IsFinite || !localOffset.IsFinite || !forward.IsFinite || length < .000001)
                throw new ArgumentException("Invalid native marker geometry.");
            double x = forward.X / length, z = forward.Z / length;
            return new Position(marker.X - localOffset.X * z - localOffset.Z * x, marker.Y,
                marker.Z + localOffset.X * x - localOffset.Z * z);
        }
    }
}
