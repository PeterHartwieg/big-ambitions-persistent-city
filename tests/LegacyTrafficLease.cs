using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Tests;

/// <summary>
/// F2d: no traffic leg is leased any more. An older development city can still hold a vehicle whose saved progress a
/// body reported (the removed lease put the kernel vehicle under physical control, reported a coordinate and released
/// it). These fixtures rebuild that state through the kernel internals exactly as the lease drove them, so the
/// retiming, reload and release paths for such saves stay covered.
/// </summary>
internal static class LegacyTrafficLease
{
    /// <summary>Put <paramref name="id"/> under physical control, advance the city to <paramref name="minute"/>, report
    /// <paramref name="progress"/> (the kernel caps it at its permission) and, unless told otherwise, release it to
    /// background travel from there. Returns the kernel's accepted coordinate.</summary>
    internal static double Report(CitySimulator simulation, string id, double minute, double progress, bool release = true)
    {
        var kernel = simulation.Traffic!;
        kernel.SetPhysicalControl(id, true);
        simulation.AdvanceTo(minute);
        double accepted = kernel.ReportPhysicalProgress(id, progress);
        if (release) kernel.SetPhysicalControl(id, false);
        return accepted;
    }

    /// <summary>The bytes an older (pre-F2d) build wrote for this city while a vehicle was under physical control. The
    /// live simulator's own capture refuses that state since F2d, so the kernel snapshot is written the way the save
    /// synchronizer wrote it before. The simulator's synchronizer is restored afterwards.</summary>
    internal static string OlderSave(CitySimulator simulation)
    {
        var state = simulation.State; var synchronizer = state.SnapshotSynchronizer;
        simulation.SynchronizeSnapshot();
        state.Traffic = simulation.Traffic!.Capture();
        state.SnapshotSynchronizer = null;
        try { return SaveCodec.Write(state); }
        finally { state.SnapshotSynchronizer = synchronizer; }
    }
}
