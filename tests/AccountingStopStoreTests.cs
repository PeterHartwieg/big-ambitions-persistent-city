using System.Collections.Generic;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class AccountingStopStoreTests
{
    [Fact]
    public void FailureIntentSurvivesNativeSaveReloadWithoutChangingArchivedPopulationOrReceipts()
    {
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = "archived-population", ["original-receipt"] = "paid" };
        Assert.False(AccountingStopStore.IsStopped(data));
        AccountingStopStore.Stop(data); AccountingStopStore.Stop(data);
        var reloaded = new Dictionary<string, string>(data);
        Assert.True(AccountingStopStore.IsStopped(reloaded)); Assert.Equal(3, reloaded.Count);
        Assert.Equal("archived-population", reloaded[SaveStore.StateKey]); Assert.Equal("paid", reloaded["original-receipt"]);
    }
    [Theory]
    [InlineData("")] [InlineData("future-policy")]
    public void UnknownStopIntentIsPreservedAndNeverAutomaticallyResumed(string marker)
    {
        var data = new Dictionary<string, string> { [AccountingStopStore.Key] = marker };
        AccountingStopStore.Stop(data);
        Assert.True(AccountingStopStore.IsStopped(data)); Assert.Equal(marker, data[AccountingStopStore.Key]);
    }
    [Theory]
    [InlineData(AccountingStopStore.SaveEpochFailed, "never settles or replays")]
    [InlineData(AccountingStopStore.DetachedAfterSave, "already stopped")]
    [InlineData("input-epoch-lost", "Accounting input epoch was lost.")]
    [InlineData("future-policy", "Accounting input epoch was lost.")]
    public void TheLoadRefusalNamesTheRecordedStopCause(string marker, string text)
    {
        var data = new Dictionary<string, string> { [AccountingStopStore.Key] = marker };
        Assert.Contains(text, AccountingStopStore.Cause(data));
        Assert.True(AccountingStopStore.IsStopped(data));
    }
}
