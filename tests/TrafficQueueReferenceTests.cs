using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficQueueReferenceTests
{
    private static TrafficKernel City()
    {
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition { Id = 7, Capacity = 8, FreeFlowMinutes = 2 } });
        for (int i = 0; i < 6; i++) kernel.Enqueue("car" + i, new[] { new TrafficRouteLeg
            { LinkId = 7, StartNominalMinutes = 0, EndNominalMinutes = 2, EndLinkFraction = i == 2 ? .5 : 1 } });
        kernel.AdvanceTo(0, 100);
        return kernel;
    }
    private static string State(TrafficKernel kernel) => JsonConvert.SerializeObject(kernel.Capture());

    [Fact]
    public void ProjectedPhysicalQueuesNeverShareTheirVehiclesWithTheLiveKernel()
    {
        var live = City(); live.SetPhysicalControl("car0", true); live.AdvanceTo(1, 100);
        string before = State(live);
        var permissions = Enumerable.Range(0, 6).Select(i => live.GetPermission("car" + i)).ToArray();
        for (int i = 0; i < 20; i++) live.ForecastEntry(7, live.Clock, .25, .75);
        Assert.Equal(before, State(live));
        Assert.Equal(permissions, Enumerable.Range(0, 6).Select(i => live.GetPermission("car" + i)).ToArray());
    }

    [Fact]
    public void RestoredQueueSurvivesCancellationAndReuseOfAnExternalId()
    {
        var live = City(); var restored = TrafficKernel.Restore(live.Capture(), false);
        Assert.Equal(State(live), State(restored));
        Assert.True(restored.Cancel("car2"));
        restored.Enqueue("car2", new[] { new TrafficRouteLeg { LinkId = 7, StartNominalMinutes = 0, EndNominalMinutes = 2 } });
        restored.AdvanceTo(20, 100);
        Assert.All(Enumerable.Range(0, 6), i => Assert.True(restored.GetPermission("car" + i).IsCompleted));
        Assert.False(live.GetPermission("car2").IsCompleted);
        Assert.Equal(6, restored.Capture().Vehicles.Count);
    }
}
