using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Tests;

/// <summary>
/// A saved traffic vehicle whose progress floor (NominalMinutes) sits inside its current section, as a clock retime
/// leaves it. The kernel has no physical control since X6, so the fixture writes the floor into the captured state the
/// way a release rebased it: motion restarts at the floor at the capture clock and readiness covers the rest of the
/// section. Retiming, save round trips and reloads then run on that state.
/// </summary>
internal static class TrafficFloorFixture
{
    /// <summary>Advance <paramref name="simulation"/> to <paramref name="minute"/>, capture <paramref name="state"/> and
    /// raise the saved floor of <paramref name="id"/> to <paramref name="progress"/>, capped by the kernel's permission.
    /// Returns the saved floor. The state is detached from the simulator afterwards.</summary>
    internal static double Write(CityState state, CitySimulator simulation, string id, double minute, double progress)
    {
        simulation.AdvanceTo(minute);
        double allowed = simulation.Traffic!.GetPermission(id).MaximumNominalMinutes;
        state.PrepareForSave();
        TrafficVehicleState vehicle = state.Traffic!.Vehicles.Single(v => v.VehicleId == id);
        vehicle.NominalMinutes = System.Math.Max(vehicle.NominalMinutes, System.Math.Min(progress, allowed));
        if (vehicle.RouteIndex >= 0)
        {
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            double fraction = (vehicle.NominalMinutes - leg.StartNominalMinutes) / (leg.EndNominalMinutes - leg.StartNominalMinutes);
            vehicle.MotionStartFraction = fraction;
            vehicle.MotionStartedAt = state.Traffic.Clock;
            vehicle.ReadyAt = state.Traffic.Clock + (1 - fraction) * leg.FreeFlowMinutes;
        }
        // Later captures would overwrite the floor from the live kernel; a new simulator over the state reattaches.
        state.SnapshotSynchronizer = null;
        return vehicle.NominalMinutes;
    }
}
