using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F7a: explicit save-cost diagnostics never change bytes; representative fixtures are deterministic.</summary>
public sealed class SaveCostDiagnosticsTests
{
    private static readonly SaveCostPhase[] Phases = Enum.GetValues<SaveCostPhase>();

    [Fact]
    public void DiagnosticsOnAndOffPublishIdenticalPayloadBytes()
    {
        // Two independent replays: one saved only with diagnostics, the other only without.
        var timedCity = RepresentativeFixture.Build(120).State;
        var plainCity = RepresentativeFixture.Build(120).State;
        var timedData = new Dictionary<string, string>(StringComparer.Ordinal) { ["other:sentinel"] = "keep" };
        var plainData = new Dictionary<string, string>(StringComparer.Ordinal) { ["other:sentinel"] = "keep" };
        var timedSession = SaveSession.Open(timedData, timedCity.PopulationKey);
        var plainSession = SaveSession.Open(plainData, plainCity.PopulationKey);
        var cost = new SaveCostRecorder();
        for (int save = 0; save < 3; save++)
        {
            cost.Begin();
            var timed = timedSession.WriteTo(timedData, timedCity, cost);
            cost.End(timed.Success, timed.Error);
            Assert.True(timed.Success, timed.Error);
            Assert.True(plainSession.WriteTo(plainData, plainCity).Success);
            Assert.Equal(RepresentativeFixture.Sha256(plainData[SaveCodec.StateKey]), RepresentativeFixture.Sha256(timedData[SaveCodec.StateKey]));
            Assert.Equal("keep", timedData["other:sentinel"]);
        }
        Assert.Equal(SaveCodec.Write(plainCity), SaveCodec.Write(plainCity, new SaveCostRecorder()));
        Assert.Equal(SaveCodec.Write(timedCity, null), timedData[SaveCodec.StateKey]);
    }

    [Fact]
    public void TimersCoverEveryPhaseAndPartitionTheSaveSpan()
    {
        var city = RepresentativeFixture.Build(60).State;
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        var session = SaveSession.Open(data, city.PopulationKey);
        var cost = new SaveCostRecorder();
        Assert.Equal(0, cost.Sequence);
        for (int save = 1; save <= 2; save++)
        {
            cost.Begin();
            var result = session.WriteTo(data, city, cost);
            cost.End(result.Success, result.Error);
            Assert.True(result.Success, result.Error);
            Assert.Equal(save, cost.Sequence);
            Assert.All(Phases, phase => Assert.True(cost.Covered(phase), phase + " not covered"));
            Assert.Equal(cost.TotalTicks, Phases.Sum(phase => cost.PhaseTicks(phase)));
            Assert.True(cost.PhaseTicks(SaveCostPhase.Encode) > 0);
            Assert.Equal(SaveCostPhase.Publish, cost.LastPhase);
            string payload = data[SaveCodec.StateKey];
            Assert.Equal(payload.Length, cost.PayloadCharacters);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(payload), cost.PayloadUtf8Bytes);
            Assert.Equal(2L * payload.Length, cost.PayloadUtf16Bytes);
            Assert.Equal(-1, cost.StreamingRecords);
            var counts = SaveCodec.CountRecords(city, payload);
            cost.Counts(counts.Streaming, counts.Semantic, 1);
            Assert.True(cost.StreamingRecords > 0 && cost.SemanticRecords > 0);
        }
    }

    [Fact]
    public void RefusedSaveRecordsTheFailingPhaseAndKeepsTheOriginalPayload()
    {
        var city = RepresentativeFixture.Build(30).State;
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        var session = SaveSession.Open(data, city.PopulationKey);
        Assert.True(session.WriteTo(data, city).Success);
        string original = data[SaveCodec.StateKey];
        city.Citizens.Add(city.Citizens[0]); // Duplicate identity: validation must refuse.
        var cost = new SaveCostRecorder();
        cost.Begin();
        var result = session.WriteTo(data, city, cost);
        cost.End(result.Success, result.Error);
        Assert.False(result.Success);
        Assert.False(cost.Success);
        Assert.Equal(result.Error, cost.Error);
        Assert.Equal(SaveCostPhase.Validate, cost.LastPhase);
        Assert.False(cost.Covered(SaveCostPhase.Encode));
        Assert.False(cost.Covered(SaveCostPhase.Publish));
        Assert.Equal(0, cost.PayloadCharacters);
        Assert.Equal(cost.TotalTicks, Phases.Sum(phase => cost.PhaseTicks(phase)));
        Assert.Same(original, data[SaveCodec.StateKey]);
    }

    [Fact]
    public void RecorderBookkeepingAllocatesNothing()
    {
        var cost = new SaveCostRecorder();
        void Cycle() { cost.Begin(); foreach (var phase in Phases) cost.Enter(phase); cost.End(true, ""); cost.Counts(1, 1, 0); }
        Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Cycle();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void RepresentativeFixtureIsDeterministicAndCadenceIndependent()
    {
        string first = SaveCodec.Write(RepresentativeFixture.Build(120).State);
        Assert.Equal(first, SaveCodec.Write(RepresentativeFixture.Build(120).State));
        Assert.Equal(first, SaveCodec.Write(RepresentativeFixture.Build(120, eventsPerSlice: 64).State));
        Assert.NotEqual(first, SaveCodec.Write(RepresentativeFixture.Build(120, RepresentativeFixture.Seed + 1).State));
    }

    [Fact]
    public void RepresentativeFixtureCarriesNaturalStateAndRoundTrips()
    {
        var replay = RepresentativeFixture.Build(957);
        var state = replay.State;
        Assert.Equal(957, state.Citizens.Count);
        Assert.Equal(1914, state.Food!.Residents.Count);
        Assert.True(replay.Events > 0 && state.Food.Residents.Sum(m => m.Purchases) > 0);
        Assert.NotEmpty(state.Food.History);
        // The replay crossed midnight: every consumer budget posted for the new day.
        int day = (int)Math.Floor(RepresentativeFixture.TargetMinute / 1440);
        Assert.All(state.Food.Residents, meal => Assert.Equal(day, meal.Budget!.LastPostedDay));
        // Natural, not saturated, histories (E5d stress retains 32 visits/resident).
        Assert.InRange(state.Citizens.Max(c => c.RecentVisits.Count), 1, SaveCodec.MaxRecentVisits);
        Assert.True(state.Citizens.Average(c => c.RecentVisits.Count) < 32);
        // No stored visitor identity (F5 B6): every roster record is a saved resident's or a visitor person's own (F5d).
        var persons = state.Citizens.Select(c => c.Identity.Id).Concat(state.Visitors.Select(v => v.Id)).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(957, state.Visitors.Count);
        Assert.All(state.Food.Residents, meal => { Assert.Contains(meal.HostId, persons); Assert.Equal(meal.ResidentId, meal.HostId); });
        string payload = SaveCodec.Write(state);
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        Assert.Equal(SaveCodec.ResearchSchema, CityState.CurrentSchema);
    }
}
