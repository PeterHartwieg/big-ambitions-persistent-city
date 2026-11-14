namespace PersistentCity.Runtime
{
    internal sealed partial class PhysicalPopulation
    {
        public void CatchNativeLoads()
        {
            if (!active || disposed) return;
            vehicles.CatchNewLoads();
        }
    }

    public sealed partial class PopulationRuntime
    {
        private void LateUpdate()
        {
            if (physical != null) RunPhysical(physicalCatchLoads ??= () => physical?.CatchNativeLoads());
        }
    }
}
