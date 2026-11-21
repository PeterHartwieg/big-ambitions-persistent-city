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
            if (physical == null) return;
            var sampler = gcSampler;
            long started = sampler != null && sampler.Running ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            RunPhysical(physicalCatchLoads ??= () => physical?.CatchNativeLoads());
            if (started != 0) sampler!.LateModWork((System.Diagnostics.Stopwatch.GetTimestamp() - started) * GcMillisecondsPerTimestamp);
        }
    }
}
