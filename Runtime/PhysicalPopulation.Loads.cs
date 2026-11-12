namespace PersistentCity.Runtime
{
    internal sealed partial class PhysicalPopulation
    {
        /// <summary>Review Sol-r3-1: the own-car adapter's per-frame check for native cars loaded directly into a held group
        /// (PhysicalVehicles.CatchNewLoads). Called from PopulationRuntime.LateUpdate, after every Update, before rendering.</summary>
        public void CatchNativeLoads()
        {
            if (!active || disposed) return;
            vehicles.CatchNewLoads();
        }
    }

    public sealed partial class PopulationRuntime
    {
        // Unity runs LateUpdate after every script's Update and coroutines and before rendering: a car RLO's rival pass-by
        // loads into a held group during the frame is retired before it is drawn once. A failure stops the view adapter
        // only, as every other physical call does.
        private void LateUpdate()
        {
            if (physical != null) RunPhysical(() => physical?.CatchNativeLoads());
        }
    }
}
