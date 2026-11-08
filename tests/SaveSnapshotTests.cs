using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// F7c (issue #94, plan section 3.2 and section 4 row F7c): the current writer encodes a detached save snapshot captured
/// on the calling thread, validation runs on that snapshot, and committed journey routes are immutable and shared. The
/// bytes equal the live writer's on the same fixtures; the reader is unchanged.
/// </summary>
public sealed class SaveSnapshotTests
{
    /// <summary>SHA-256 and length of the pre-F7c writer (base 57607b2, live-graph encode) on the same fresh fixtures,
    /// produced by tests/F7cMeasure.cs on that commit (docs/perf/f7c.md lists every fixture, including the larger ones).
    /// F6i repins only rush-957x2: it simulates food choices, so anchored prices change its meals/balances.
    /// The other four inputs and all codec shape assertions remain unchanged.</summary>
    public static IEnumerable<object[]> BasePins() => new[]
    {
        new object[] { "representative-957x2", "D1CC4FB4EE3F3135AECC0A032FD63AA5257B9BC1739BB76FC98B807113EEE1AF", 2311290 },
        new object[] { "fresh-957x3", "A6DB1093D09D7B0540B56220C5F51F64027DAED008CE0BAB995829B071BAF0E9", 2900486 },
        new object[] { "saturated-957x3", "F32DC000A40718334FFACE8F5174E9004161DF7F07D7319A91DF15B18D7A901C", 4211558 },
        new object[] { "encoding-max", "D222381479282F55F8B0D26A78760E915A676A0BA43593D70367B67A8424696C", 2445781 },
        new object[] { "rush-957x2", "F48B69FD42262E6589C8A385E3AC1C7B3A8B0B8626552B80A85D0E2DD9B1C5F8", 2240373 },
    };

    /// <summary>E6c: these pins are writer 29's, now the frozen C5c writer (the name is kept for docs/checks/f7c.md). The snapshot path for that schema still
    /// gives the pinned bytes; the session and the reader use the current writer.</summary>
    [Theory]
    [MemberData(nameof(BasePins))]
    public void SnapshotAndLiveWriterBytesMatchPinnedFixtures(string fixture, string sha256, int characters)
    {
        var state = F7cFixtures.Build(fixture, SaveCodec.ResearchSchema).State;
        const int frozen = SaveCodec.ResearchSchema;
        state.Schema = frozen;
        var snapshot = SaveCodec.CaptureForSchema(state, frozen, null);
        Assert.Equal(frozen, snapshot.Schema);
        string payload = SaveCodec.EncodeForSchema(snapshot, frozen, null);
        Assert.Equal(sha256, RepresentativeFixture.Sha256(payload));
        Assert.Equal(characters, payload.Length);
        Assert.Equal(payload, SaveCodec.WriteForSchema(state, frozen));
        var frozenRead = SaveCodec.ReadWriter29(payload);
        Assert.True(frozenRead.Status == SaveReadStatus.Loaded, frozenRead.Error);
        Assert.Equal(payload, SaveCodec.WriteForSchema(frozenRead.State!, frozen));
        state.Schema = CityState.CurrentSchema;
        string current = SaveCodec.Encode(SaveCodec.Capture(state));
        Assert.Equal(current, SaveCodec.Write(state));
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["other:key"] = "kept" };
        var session = SaveSession.Open(data, state.PopulationKey);
        Assert.True(session.WriteTo(data, state).Success);
        Assert.Equal(current, data[SaveCodec.StateKey]); Assert.Equal("kept", data["other:key"]);
        // The current reader loads the payload and the loaded city writes the same bytes again.
        var read = SaveCodec.Read(current);
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
            var snapshot = SaveCodec.CaptureForSchema(state, CityState.CurrentSchema, null, order);
            Assert.Equal(expected, SaveCodec.EncodeForSchema(snapshot, CityState.CurrentSchema, null));
        }
        // Every section exactly once.
        Assert.Throws<ArgumentException>(() => SaveCodec.CaptureForSchema(state, CityState.CurrentSchema, null, sections.Take(6).ToArray()));
        Assert.Throws<ArgumentException>(() => SaveCodec.CaptureForSchema(state, CityState.CurrentSchema, null, sections.Append(SnapshotSection.Food).ToArray()));
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
        // The section copy alone (no synchronize or validation): the filled graph is not a valid city.
        var snapshot = SaveSnapshot.Capture(state, CityState.CurrentSchema);
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
        // Live play goes on after the save callback returns ...
        double later = state.Minute + 90;
        while (!built.City.IsCaughtUpTo(later)) built.City.AdvanceFrameSlice(later, 64);
        built.City.SynchronizeSnapshot();
        Assert.NotEqual(before, SaveCodec.Write(state));
        // ... and every section of the live graph changes in place.
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
        // A snapshot is published once and never outlives its save.
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
        Assert.Equal(sum, journey.Minutes); // computed once at commit, in the same order

        var snapshot = SaveCodec.Capture(state);
        Assert.Same(journey, snapshot.Wire.Citizens[index].Journey);
        string payload = SaveCodec.Encode(snapshot);

        // A loaded journey has not been committed by this simulator yet, so a snapshot copies it.
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = read.State!.Citizens[index].Journey!;
        Assert.False(loaded.IsCommitted);
        var reloaded = SaveCodec.Capture(read.State!);
        Assert.Equal(0, reloaded.SharedRoutes);
        Assert.Equal(snapshot.SharedRoutes, reloaded.CopiedRoutes);
        Assert.NotSame(loaded, reloaded.Wire.Citizens[index].Journey);
        Assert.Equal(payload, SaveCodec.Encode(reloaded));

        // The leg list refuses every structural edit, through any reference to it (review Sol-r1-1).
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

    /// <summary>Review Sol-r1-1: a same-count leg replacement, before or after capture, never reaches an accepted payload,
    /// and every payload the writer accepts reads back.</summary>
    [Fact]
    public void SameCountLegReplacementCannotChangeACommittedRouteBeforeOrAfterCapture()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        var journeys = PersonTable.Schedules(state).Where(c => c.Journey != null).Select(c => c.Journey!).ToList();
        Assert.True(journeys.Count > 100);
        Assert.All(journeys, j => Assert.True(j.IsCommitted && j.Legs.IsReadOnly));
        string before = SaveCodec.Write(state);
        // Before capture: a frozen leg taken from another committed route cannot replace one of the same count.
        var donor = journeys[1].Legs[0];
        Assert.Throws<InvalidOperationException>(() => journeys[0].Legs[0] = donor);
        Assert.Throws<InvalidOperationException>(() => journeys[0].Legs = new List<RouteLeg> { donor });
        var snapshot = SaveCodec.Capture(state);
        string payload = SaveCodec.Encode(snapshot);
        Assert.Equal(before, payload);
        // After capture: the shared route still refuses the same replacement, so the snapshot's bytes stay.
        foreach (var journey in journeys)
        {
            var first = journey.Legs[0];
            Assert.Throws<InvalidOperationException>(() => journey.Legs[0] = new RouteLeg
            { From = first.From, To = first.To, Mode = first.Mode, Minutes = first.Minutes + 1, ServiceId = first.ServiceId });
        }
        Assert.Equal(payload, SaveCodec.Encode(snapshot));
        foreach (string accepted in new[] { payload, SaveCodec.Write(state) })
        {
            var read = SaveCodec.Read(accepted);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(accepted, SaveCodec.Write(read.State!));
        }
    }

    /// <summary>Review Sol-r1-1: a route owns its legs. Assigning a builder list copies it, so later edits of that list
    /// change neither the route nor, after commit, any snapshot that shares it.</summary>
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
        // Invalid in the snapshot: refused by validation of the snapshot, nothing published.
        var invalid = SaveCodec.Capture(state);
        invalid.Wire.Citizens[0].Identity.Age = 999;
        var refused = session.WriteTo(data, invalid);
        Assert.False(refused.Success); Assert.Contains("age", refused.Error);
        Assert.Same(previous, data[SaveCodec.StateKey]);
        Assert.False(invalid.Published);
        // The live city was never invalid; a fresh snapshot publishes.
        Assert.True(session.WriteTo(data, SaveCodec.Capture(state)).Success);
        // Another population's snapshot is refused before encoding.
        var other = SaveCodec.Capture(F7cFixtures.Build("rush-957x2").State);
        var foreign = SaveSession.Open(new Dictionary<string, string>(), "another-population");
        Assert.Contains("another population", foreign.WriteTo(new Dictionary<string, string>(), other).Error);
    }
}
