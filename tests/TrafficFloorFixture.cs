using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Tests;

internal static class TrafficFloorFixture
{
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
        state.SnapshotSynchronizer = null;
        return vehicle.NominalMinutes;
    }
}
