using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class SaveSnapshotTests
{
    public static IEnumerable<object[]> BasePins() => new[]
    {
        new object[] { "representative-957x2", "5FC9DB6ED1844D2B99CE8EE3BA3B54B785FFC55A82A710FC16FEE4E58598A9EE", 2824643 },
        new object[] { "fresh-957x3", "1B3FD0E4DE7E9ED6C5676CAC23532F8F103D67C0A05A6C29D43A7E5A6FA9949B", 3627929 },
        new object[] { "saturated-957x3", "2FD351378B93AD40346FADBE9749D742B679F2698FCEBDDFD9228ADD887E28D5", 4916033 },
        new object[] { "encoding-max", "11FB50CE4161EF19303C0C813ED33915B504CB2ECA80276ED879C6B1EC726E56", 2959944 },
        new object[] { "rush-957x2", "3859D913FBD830EDF97DFB487E2629437241A1E408B82FC19A788F5A2EDA7115", 2755031 },
    };

    [Theory]
    [MemberData(nameof(BasePins))]
    public void SnapshotAndLiveWriterBytesMatchPinnedFixtures(string fixture, string sha256, int characters)
    {
        var state = F7cFixtures.Build(fixture).State;
        string current = SaveCodec.Encode(SaveCodec.Capture(state));
        string actual = RepresentativeFixture.Sha256(current) + " " + current.Length;
        Assert.True(sha256 + " " + characters == actual, "golden " + fixture + ": " + actual);
        Assert.Equal(current, SaveCodec.Write(state));
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["other:key"] = "kept" };
        var session = SaveSession.Open(data, state.PopulationKey);
        Assert.True(session.WriteTo(data, state).Success);
        Assert.Equal(current, data[SaveCodec.StateKey]); Assert.Equal("kept", data["other:key"]);
        var read = SaveWire.Read(current);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(current, SaveCodec.Write(read.State!));
    }

    private static CityState ResearchRush()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        state.Research = ResearchSaveTests.AllStates();
        return state;
    }

    [Fact]
    public void PermutedCaptureOrderGivesIdenticalBytes()
    {
        var state = ResearchRush();
        string expected = SaveCodec.Write(state);
        var sections = SaveSnapshot.DefaultOrder;
        var orders = new List<SnapshotSection[]> { sections.Reverse().ToArray() };
        for (int r = 1; r < sections.Length; r++) orders.Add(sections.Skip(r).Concat(sections.Take(r)).ToArray());
        var random = new Random(94);
        for (int i = 0; i < 16; i++) orders.Add(sections.OrderBy(_ => random.Next()).ToArray());
        foreach (var order in orders)
        {
            var snapshot = SaveCodec.Capture(state, null, order);
            Assert.Equal(expected, SaveCodec.Encode(snapshot));
        }
        Assert.Throws<ArgumentException>(() => SaveCodec.Capture(state, null, sections.Take(6).ToArray()));
        Assert.Throws<ArgumentException>(() => SaveCodec.Capture(state, null, sections.Append(SnapshotSection.Food).ToArray()));
    }

    [Fact]
    public void SnapshotEqualsTheLiveGraphAndSharesOnlyImmutableObjects()
    {
        var state = ResearchRush();
        var snapshot = SaveCodec.Capture(state);
        var audit = GraphAudit.Compare(state, snapshot.Wire);
        Assert.True(audit.Differences.Count == 0, string.Join("; ", audit.Differences));
        Assert.All(audit.Shared, shared => Assert.True(GraphAudit.Immutable(shared), shared.GetType().Name + " is shared with the live graph"));
        int travelling = PersonTable.Schedules(state).Count(c => c.Journey != null);
        Assert.True(travelling > 100, "too few journeys in flight: " + travelling);
        Assert.Equal(travelling, snapshot.SharedRoutes);
        Assert.Equal(travelling, audit.Shared.OfType<Route>().Count());
        Assert.NotSame(state.Research, snapshot.Wire.Research);
    }

    [Fact]
    public void EveryWritableMemberOfTheSavedGraphIsCaptured()
    {
        var fill = new GraphFill();
        var state = fill.City();
        Assert.True(fill.Unknown.Count == 0, "unknown saved member types: " + string.Join(", ", fill.Unknown));
        var committed = new Route { Legs = { new RouteLeg { From = new Position(1, 2, 3), To = new Position(4, 5, 6), Minutes = 7, ServiceId = "s" } } }.Commit();
        state.Citizens.Add(new CitizenState { Journey = committed });
        var snapshot = SaveSnapshot.Capture(state);
        var audit = GraphAudit.Compare(state, snapshot.Wire);
        Assert.True(audit.Differences.Count == 0, string.Join("; ", audit.Differences));
        Assert.All(audit.Shared, shared => Assert.True(GraphAudit.Immutable(shared), shared.GetType().Name + " is shared with the live graph"));
        Assert.Same(committed, snapshot.Wire.Citizens[^1].Journey);
        Assert.NotSame(state.Citizens[0].Journey, snapshot.Wire.Citizens[0].Journey);
    }

    [Fact]
    public void MutationAfterCaptureNeverReachesThePayload()
    {
        var built = F7cFixtures.Build("rush-957x2");
        var state = built.State; state.Research = ResearchSaveTests.AllStates();
        string before = SaveCodec.Write(state);
        var snapshot = SaveCodec.Capture(state);
        double later = state.Minute + 90;
        while (!built.City.IsCaughtUpTo(later)) built.City.AdvanceFrameSlice(later, 64);
        built.City.SynchronizeSnapshot();
        Assert.NotEqual(before, SaveCodec.Write(state));
        state.Places[0].Name = "renamed"; state.Places[0].WeeklyHours.Clear();
        state.Citizens[0].RecentVisits.Clear(); state.Citizens[1].Identity.Name = "renamed"; state.Citizens[2].CommutePreparation.TravelMinutes = 99;
        state.Visitors[0].Stay.FirstDay++; state.VisitorRoutines[0].NextEventMinute += 1;
        state.Food!.Residents[0].Budget!.Balance += Money.FromUnits(1); state.Food.Residents[1].Memory.Entries.Clear();
        state.Food.History.Clear(); state.Food.Shops[0].AdditionalProductIds.Add("renamed");
        state.ProductNames.Add("renamed"); state.Portals.Add("renamed");
        int open = state.Research.Open.Count;
        Assert.Contains(state.Research.Open.ToList(), order => state.Research.TryMarkPaid(order.Id) == ResearchMoveRefusal.None);
        Assert.Equal(open - 1, state.Research.Open.Count);
        Assert.Equal(before, SaveCodec.Encode(snapshot));
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        var session = SaveSession.Open(data, state.PopulationKey);
        Assert.True(session.WriteTo(data, snapshot).Success);
        string published = data[SaveCodec.StateKey];
        Assert.Equal(before, published);
        var again = session.WriteTo(data, snapshot);
        Assert.False(again.Success); Assert.Contains("at most once", again.Error);
        Assert.Same(published, data[SaveCodec.StateKey]);
    }

    [Fact]
    public void CommittedJourneysAreFrozenAndSharedWhileLoadedRoutesAreCopied()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        int index = state.Citizens.FindIndex(c => c.Journey != null && c.Journey.Legs.Count > 0);
        var journey = state.Citizens[index].Journey!;
        Assert.True(journey.IsCommitted);
        var leg = journey.Legs[0];
        Assert.Throws<InvalidOperationException>(() => leg.Minutes = 1);
        Assert.Throws<InvalidOperationException>(() => leg.From = default);
        Assert.Throws<InvalidOperationException>(() => leg.To = default);
        Assert.Throws<InvalidOperationException>(() => leg.Mode = TravelMode.Car);
        Assert.Throws<InvalidOperationException>(() => leg.ServiceId = "");
        Assert.Throws<InvalidOperationException>(() => journey.Legs = new List<RouteLeg>());
        double sum = 0; foreach (var l in journey.Legs) sum += l.Minutes;
        Assert.Equal(sum, journey.Minutes);

        var snapshot = SaveCodec.Capture(state);
        Assert.Same(journey, snapshot.Wire.Citizens[index].Journey);
        string payload = SaveCodec.Encode(snapshot);

        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = read.State!.Citizens[index].Journey!;
        Assert.False(loaded.IsCommitted);
        var reloaded = SaveCodec.Capture(read.State!);
        Assert.Equal(0, reloaded.SharedRoutes);
        Assert.Equal(snapshot.SharedRoutes, reloaded.CopiedRoutes);
        Assert.NotSame(loaded, reloaded.Wire.Citizens[index].Journey);
        Assert.Equal(payload, SaveCodec.Encode(reloaded));

        var legs = journey.Legs;
        var same = new RouteLeg { From = leg.From, To = leg.To, Mode = leg.Mode, Minutes = leg.Minutes + 1, ServiceId = leg.ServiceId };
        Assert.Throws<InvalidOperationException>(() => legs[0] = same);
        Assert.Throws<InvalidOperationException>(() => legs.Add(same));
        Assert.Throws<InvalidOperationException>(() => legs.Insert(0, same));
        Assert.Throws<InvalidOperationException>(() => legs.AddRange(new[] { same }));
        Assert.Throws<InvalidOperationException>(() => legs.RemoveAt(0));
        Assert.Throws<InvalidOperationException>(() => legs.Remove(leg));
        Assert.Throws<InvalidOperationException>(() => legs.Clear());
        Assert.True(legs.IsReadOnly);
        Assert.Same(leg, journey.Legs[0]);
        Assert.Equal(sum, journey.Minutes);
        Assert.Equal(payload, SaveCodec.Encode(snapshot));
    }

    [Fact]
    public void SameCountLegReplacementCannotChangeACommittedRouteBeforeOrAfterCapture()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        var journeys = PersonTable.Schedules(state).Where(c => c.Journey != null).Select(c => c.Journey!).ToList();
        Assert.True(journeys.Count > 100);
        Assert.All(journeys, j => Assert.True(j.IsCommitted && j.Legs.IsReadOnly));
        string before = SaveCodec.Write(state);
        var donor = journeys[1].Legs[0];
        Assert.Throws<InvalidOperationException>(() => journeys[0].Legs[0] = donor);
        Assert.Throws<InvalidOperationException>(() => journeys[0].Legs = new List<RouteLeg> { donor });
        var snapshot = SaveCodec.Capture(state);
        string payload = SaveCodec.Encode(snapshot);
        Assert.Equal(before, payload);
        foreach (var journey in journeys)
        {
            var first = journey.Legs[0];
            Assert.Throws<InvalidOperationException>(() => journey.Legs[0] = new RouteLeg
            { From = first.From, To = first.To, Mode = first.Mode, Minutes = first.Minutes + 1, ServiceId = first.ServiceId });
        }
        Assert.Equal(payload, SaveCodec.Encode(snapshot));
        foreach (string accepted in new[] { payload, SaveCodec.Write(state) })
        {
            var read = SaveWire.Read(accepted);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(accepted, SaveCodec.Write(read.State!));
        }
    }

    [Fact]
    public void ARouteNeverAliasesItsBuilderList()
    {
        var a = new RouteLeg { From = new Position(0, 0, 0), To = new Position(1, 0, 0), Minutes = 2, Mode = TravelMode.Walk };
        var b = new RouteLeg { From = new Position(1, 0, 0), To = new Position(2, 0, 0), Minutes = 3, Mode = TravelMode.Walk };
        var builder = new List<RouteLeg> { a };
        var route = new Route { Legs = builder };
        builder.Add(b); builder[0] = b;
        Assert.Single(route.Legs); Assert.Same(a, route.Legs[0]);
        route.Commit();
        builder.Clear();
        Assert.Single(route.Legs); Assert.Equal(2, route.Minutes);
        Assert.Throws<InvalidOperationException>(() => new Route { Legs = new List<RouteLeg> { a, null! } }.Commit());
    }

    [Fact]
    public void ValidationRunsOnTheSnapshotAndARefusedSaveKeepsThePayload()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        var session = SaveSession.Open(data, state.PopulationKey);
        Assert.True(session.WriteTo(data, state).Success);
        string previous = data[SaveCodec.StateKey];
        var invalid = SaveCodec.Capture(state);
        invalid.Wire.Citizens[0].Identity.Age = 999;
        var refused = session.WriteTo(data, invalid);
        Assert.False(refused.Success); Assert.Contains("age", refused.Error);
        Assert.Same(previous, data[SaveCodec.StateKey]);
        Assert.False(invalid.Published);
        Assert.True(session.WriteTo(data, SaveCodec.Capture(state)).Success);
        var other = SaveCodec.Capture(F7cFixtures.Build("rush-957x2").State);
        var foreign = SaveSession.Open(new Dictionary<string, string>(), "another-population");
        Assert.Contains("another population", foreign.WriteTo(new Dictionary<string, string>(), other).Error);
    }
}
