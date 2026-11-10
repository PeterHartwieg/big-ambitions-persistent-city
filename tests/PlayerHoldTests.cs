using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.MovementEquivalence;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests;

/// <summary>
/// F2e (docs/plans/f2-movement.md Decision 4, row F2e): player holds as world events. Core applies a retroactive, bounded
/// hold inside HoldForPlayer and keeps no hold state afterwards; TrafficKernel.HoldVehicle rebases a car's motion; the
/// consumed minutes and the city-wide sequence are saved (schema 35); the stream replays bit for bit (E2).
/// </summary>
[Collection(StreetFoodHooks.Collection)]
public sealed class PlayerHoldTests
{
    // ---- Fixtures: the F2a grid city without food (walkers and kernel cars), and small kernels ----

    private static CitySimulator City(int residents = 96)
    {
        var state = MovementFixture.Build(0, residents);
        state.Food = null;
        return new CitySimulator(state, new GridPlanner());
    }

    private static CitySimulator Load(string payload)
    {
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var city = new CitySimulator(read.State!, new GridPlanner());
        city.AdvanceTo(city.State.Minute); // binds the road network, as the first frame after a load does
        return city;
    }

    private static CitizenState Person(CitySimulator city, string id) => city.State.Citizens.Single(c => c.Identity.Id == id);

    /// <summary>Advance in quarter minutes until a walker (not in traffic) on a walk leg satisfies the condition, given its
    /// minutes into the current leg.</summary>
    private static CitizenState Walker(CitySimulator city, Func<CitizenState, double, bool> wanted, double until = MovementFixture.StartMinute + 12 * 60)
    {
        for (double t = city.State.Minute; t <= until; t += 0.25)
        {
            city.AdvanceTo(t);
            foreach (var c in city.State.Citizens)
            {
                if (c.Phase != CitizenPhase.Travelling || c.Journey == null || city.TryGetTrafficPermission(c.Identity.Id, out _)) continue;
                var s = city.SampleJourney(c);
                if (s.Mode == TravelMode.Walk && !s.Stationary && wanted(c, s.LegFraction * s.LegMinutes)) return c;
            }
        }
        throw new Xunit.Sdk.XunitException("no walker found");
    }

    private static TrafficLinkDefinition Link(int id, int capacity, double free = 1, double headway = 0, TrafficSignalSchedule? signal = null)
        => new() { Id = id, Capacity = capacity, FreeFlowMinutes = free, HeadwayMinutes = headway, Signal = signal };
    private static List<TrafficRouteLeg> Leg(double minutes, double end = 1)
        => new() { new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 0, EndNominalMinutes = minutes, StartLinkFraction = 0, EndLinkFraction = end } };
    private static string Json(TrafficKernel kernel) => JsonSerializer.Serialize(kernel.Capture());
    private static TrafficKernel Copy(TrafficKernel kernel) => TrafficKernel.Restore(kernel.Capture());
    private static void Enter(TrafficKernel kernel, string id, double at, List<TrafficRouteLeg> route)
    { kernel.AdvanceTo(at, 1000); kernel.Enqueue(id, route); kernel.AdvanceTo(at, 1000); }
    private static double Spatial(TrafficKernel kernel, string id, double end = 1)
    {
        var v = kernel.Capture().Vehicles.Single(x => x.VehicleId == id);
        var leg = v.Route[v.RouteIndex];
        return leg.StartLinkFraction + (kernel.GetPermission(id).NominalMinutes - leg.StartNominalMinutes) / (leg.EndNominalMinutes - leg.StartNominalMinutes) * (leg.EndLinkFraction - leg.StartLinkFraction);
    }
    private static int FirstRoad(Route route)
    {
        for (int i = 0; i < route.Legs.Count; i++) if (route.Legs[i].ServiceId.StartsWith("native:road:", StringComparison.Ordinal)) return i;
        return int.MaxValue;
    }
    private static void SameBits(double a, double b) => Assert.Equal(BitConverter.DoubleToInt64Bits(a), BitConverter.DoubleToInt64Bits(b));

    // ---- Walk legs: retroactive, instant, bounded ----

    [Fact]
    public void AWalkHoldMovesTheJourneyLaterAtOnceAndArrivalFollowsWithoutHoldState()
    {
        var city = City();
        var w = Walker(city, (c, inLeg) => inLeg >= 0.5 && c.NextEventMinute > city.State.Minute + 2);
        string id = w.Identity.Id;
        double departure = w.DepartureMinute, arrival = w.NextEventMinute, elapsed = city.State.Minute - departure;
        var back = w.Journey!.PositionAt(elapsed - 0.25);
        var result = city.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot);
        Assert.True(result.Applied, result.Refusal.ToString());
        Assert.Equal(1, result.Record.Sequence); Assert.Equal(1, city.State.PlayerHoldSequence);
        Assert.Equal(city.State.Minute, result.Record.Minute); Assert.False(result.Record.Vehicle);
        Assert.Equal(0.25, result.Record.Input); Assert.Equal(0.25, result.Record.Charge, 12);
        Assert.Equal(departure + result.Record.Charge, w.DepartureMinute);
        Assert.Equal(arrival + result.Record.Charge, w.NextEventMinute);
        // Progress is what it was at the start of the blocked interval.
        Assert.True(Position.Distance(back, city.PositionOf(w)) < 1e-6);
        Assert.Equal(result.Record.Charge, w.PlayerHoldMinutes);
        // The arrival happens at the delayed minute, and the allowance belongs to that journey only.
        int completed = w.CompletedJourneys;
        city.AdvanceTo(arrival + 0.01);
        Assert.Equal(completed, Person(city, id).CompletedJourneys);
        city.AdvanceTo(w.NextEventMinute);
        Assert.Equal(completed + 1, Person(city, id).CompletedJourneys);
        Assert.Equal(0, Person(city, id).PlayerHoldMinutes);
    }

    [Fact]
    public void HoldsClampAtTheLegStartAndRefuseAtIt()
    {
        var city = City();
        var w = Walker(city, (c, inLeg) => inLeg >= 0.3 && inLeg <= 0.9 && c.NextEventMinute > city.State.Minute + 2);
        var start = city.SampleJourney(w);
        double inLeg = start.LegFraction * start.LegMinutes, applied = 0;
        PlayerHoldResult result;
        while ((result = city.HoldForPlayer(w.Identity.Id, 0.25, PlayerHoldCause.PlayerFoot)).Applied) applied += result.Record.Charge;
        Assert.Equal(PlayerHoldRefusal.AtLegStart, result.Refusal);
        Assert.Equal(inLeg, applied, 9);
        var now = city.SampleJourney(w);
        Assert.Equal(start.LegIndex, now.LegIndex); Assert.Equal(0, now.LegFraction, 9);
        Assert.True(w.PlayerHoldMinutes < PlayerHolds.MaximumMinutesPerJourney);
    }

    [Fact]
    public void ArrivalTrafficHandoffAndUndrainedMinutesAreNeverHeld()
    {
        var city = City();
        // Arrived (or not yet left): no journey to hold.
        var home = city.State.Citizens.First(c => c.Phase == CitizenPhase.AtPlace);
        Assert.Equal(PlayerHoldRefusal.NotTravelling, city.HoldForPlayer(home.Identity.Id, 0.1, PlayerHoldCause.PlayerFoot).Refusal);
        Assert.Equal(PlayerHoldRefusal.UnknownPerson, city.HoldForPlayer("nobody", 0.1, PlayerHoldCause.PlayerFoot).Refusal);
        foreach (double bad in new[] { 0, -0.1, 0.2500001, double.NaN, double.PositiveInfinity })
            Assert.Equal(PlayerHoldRefusal.InvalidInput, city.HoldForPlayer(home.Identity.Id, bad, PlayerHoldCause.PlayerFoot).Refusal);
        // A car person after the kernel handed the journey back (kerb connector or parking pause): neither cause can hold it,
        // so the handoff already made is never undone.
        CitizenState? parked = null;
        for (double t = city.State.Minute; parked == null && t <= MovementFixture.StartMinute + 12 * 60; t += 0.05)
        {
            city.AdvanceTo(t);
            parked = city.State.Citizens.FirstOrDefault(c => c.Phase == CitizenPhase.Travelling && c.Journey != null
                && !city.TryGetTrafficPermission(c.Identity.Id, out _) && city.SampleJourney(c).Mode == TravelMode.Car
                && city.SampleJourney(c).LegIndex > FirstRoad(c.Journey));
        }
        Assert.NotNull(parked);
        Assert.Equal(PlayerHoldRefusal.WrongLeg, city.HoldForPlayer(parked!.Identity.Id, 0.1, PlayerHoldCause.PlayerFoot).Refusal);
        Assert.Equal(PlayerHoldRefusal.WrongLeg, city.HoldForPlayer(parked.Identity.Id, 0.1, PlayerHoldCause.PlayerVehicle).Refusal);
        // A walker cannot be held as a car, nor a car as a walker.
        var w = Walker(city, (c, inLeg) => inLeg > 0.1);
        Assert.Equal(PlayerHoldRefusal.WrongLeg, city.HoldForPlayer(w.Identity.Id, 0.1, PlayerHoldCause.PlayerVehicle).Refusal);
        // A minute with work still due is refused, so a replay that stops on the same minute applies it to the same state.
        long sequence = city.State.PlayerHoldSequence;
        bool refusedUndrained = false;
        for (double t = city.State.Minute + 0.5; !refusedUndrained && t < city.State.Minute + 120; t += 0.5)
        {
            if (city.AdvanceFrameSlice(t, 1).CaughtUp || city.IsCaughtUpTo(city.State.Minute)) continue;
            var walker = city.State.Citizens.FirstOrDefault(c => c.Phase == CitizenPhase.Travelling && c.Journey != null && !city.TryGetTrafficPermission(c.Identity.Id, out _));
            if (walker == null) continue;
            string before = SaveCodec.Write(city.State);
            Assert.Equal(PlayerHoldRefusal.NotDrained, city.HoldForPlayer(walker.Identity.Id, 0.1, PlayerHoldCause.PlayerFoot).Refusal);
            Assert.Equal(before, SaveCodec.Write(city.State));
            refusedUndrained = true;
        }
        Assert.True(refusedUndrained);
        Assert.Equal(sequence, city.State.PlayerHoldSequence);
    }

    [Fact]
    public void SaveInTheMiddleOfABlockageThenContinueOrReloadGivesEqualBytes()
    {
        var city = City();
        var w = Walker(city, (c, inLeg) => inLeg >= 0.3 && c.NextEventMinute > city.State.Minute + 4);
        string id = w.Identity.Id;
        // The player keeps standing in the way: a report every quarter minute.
        for (int i = 0; i < 2; i++)
        {
            Assert.True(city.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot).Applied);
            city.AdvanceTo(city.State.Minute + 0.25);
        }
        string middle = SaveCodec.Write(city.State);
        var reloaded = Load(middle);
        Assert.Equal(middle, SaveCodec.Write(reloaded.State));
        foreach (var c in new[] { city, reloaded })
        {
            Assert.True(c.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot).Applied);
            c.AdvanceTo(c.State.Minute + 0.25);
            Assert.True(c.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot).Applied);
            c.AdvanceTo(c.State.Minute + 30);
        }
        Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(reloaded.State));
        Assert.Equal(4, reloaded.State.PlayerHoldSequence);
    }

    [Fact]
    public void ReloadNearTheJourneyLimitWhileTheBlockageContinuesAppliesOnlyTheRemainingAllowance()
    {
        var city = City();
        var w = Walker(city, (c, inLeg) => inLeg >= 0.3 && c.NextEventMinute > city.State.Minute + 4);
        string id = w.Identity.Id;
        foreach (double minutes in new[] { 0.25, 0.25, 0.25, 0.15 })
        {
            Assert.True(city.HoldForPlayer(id, minutes, PlayerHoldCause.PlayerFoot).Applied);
            city.AdvanceTo(city.State.Minute + 0.25);
        }
        Assert.Equal(0.9, w.PlayerHoldMinutes, 9);
        var reloaded = Load(SaveCodec.Write(city.State));
        var after = Person(reloaded, id);
        Assert.Equal(w.PlayerHoldMinutes, after.PlayerHoldMinutes);
        double departure = after.DepartureMinute;
        var last = reloaded.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot);
        Assert.True(last.Applied);
        Assert.Equal(0.25, last.Record.Input);
        Assert.Equal(PlayerHolds.MaximumMinutesPerJourney - 0.9, last.Record.Charge, 9);
        Assert.Equal(departure + last.Record.Charge, after.DepartureMinute);
        Assert.Equal(PlayerHolds.MaximumMinutesPerJourney, after.PlayerHoldMinutes, 12);
        reloaded.AdvanceTo(reloaded.State.Minute + 0.25);
        Assert.Equal(PlayerHoldRefusal.AllowanceSpent, reloaded.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot).Refusal);
    }

    // ---- Traffic legs: TrafficKernel.HoldVehicle ----

    [Fact]
    public void AMidLinkCarHoldMovesItBackAndWithholdsItsFollowerIncludingAnInteriorCurbExitWithReplayAndReloadEquality()
    {
        // a crosses the whole link in 1 minute; b, twice as fast, leaves at a curb halfway along the link.
        var unheld = new TrafficKernel(new[] { Link(1, 4) });
        Enter(unheld, "a", 0, Leg(1));
        Enter(unheld, "b", 0.5, Leg(0.25, 0.5));
        unheld.AdvanceTo(0.6, 1000);
        string checkpoint = Json(unheld);
        var held = Copy(unheld);
        double followerBefore = held.GetPermission("b").NominalMinutes;
        var result = held.HoldVehicle("a", 0.25);
        Assert.True(result.Applied);
        // Moved back to its follower's spacing (u 0.45), not to the motion floor (0.35); readiness 0.15 later.
        Assert.Equal(0.6, unheld.GetPermission("a").NominalMinutes, 12);
        Assert.Equal(0.45, held.GetPermission("a").NominalMinutes, 12);
        Assert.Equal(0.45, result.MotionStartFraction, 12); Assert.Equal(0.6, result.MotionStartedAt); Assert.Equal(1.15, result.ReadyAt, 12);
        Assert.Equal(0.15, result.Charge, 12);
        Assert.Equal(followerBefore, held.GetPermission("b").NominalMinutes);
        // The follower gains no space and no curb-exit permission from the held car.
        unheld.AdvanceTo(0.7, 1000); held.AdvanceTo(0.7, 1000);
        Assert.True(held.GetPermission("b").MaximumNominalMinutes < unheld.GetPermission("b").MaximumNominalMinutes - 0.01);
        unheld.AdvanceTo(5, 1000); held.AdvanceTo(5, 1000);
        Assert.Equal(0.75, unheld.GetPermission("b").CompletedAt!.Value, 12);
        Assert.Equal(0.9, held.GetPermission("b").CompletedAt!.Value, 12);
        Assert.Equal(1.15, held.GetPermission("a").CompletedAt!.Value, 12);
        // Replay: the same input on the same checkpoint reproduces the mutation and charge bit for bit.
        var replay = TrafficKernel.Restore(JsonSerializer.Deserialize<TrafficKernelState>(checkpoint)!);
        var again = replay.HoldVehicle("a", 0.25);
        SameBits(result.Charge, again.Charge); SameBits(result.MotionStartFraction, again.MotionStartFraction);
        SameBits(result.MotionStartedAt, again.MotionStartedAt); SameBits(result.ReadyAt, again.ReadyAt);
        // Reload right after the hold continues exactly as the uninterrupted kernel.
        var reloaded = TrafficKernel.Restore(JsonSerializer.Deserialize<TrafficKernelState>(checkpoint)!);
        reloaded.HoldVehicle("a", 0.25);
        reloaded = Copy(reloaded);
        replay.AdvanceTo(5, 1000); reloaded.AdvanceTo(5, 1000);
        Assert.Equal(Json(held), Json(replay));
        Assert.Equal(Json(held), Json(reloaded));
    }

    [Fact]
    public void ATrafficHoldThatChangesNeitherMotionNorDischargeIsRefusedAndChangesNothing()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 1) });
        Enter(kernel, "a", 0, Leg(1));
        kernel.Enqueue("waiting", Leg(1)); kernel.AdvanceTo(0, 1000);
        string before = Json(kernel);
        // Just entered: a link transition already made is never undone.
        Assert.False(kernel.HoldVehicle("a", 0.25).Applied);
        // A waiting source has no link; an unknown vehicle is nobody.
        Assert.False(kernel.HoldVehicle("waiting", 0.25).Applied);
        Assert.False(kernel.HoldVehicle("nobody", 0.25).Applied);
        Assert.Equal(before, Json(kernel));
        kernel.AdvanceTo(1, 1000);
        Assert.True(kernel.GetPermission("a").IsCompleted);
        Assert.False(kernel.HoldVehicle("a", 0.25).Applied);
        // Standing at a red light for longer than the report: neither its motion nor its discharge moves.
        var red = new TrafficKernel(new[] { Link(1, 2, 0.5, 0, new TrafficSignalSchedule { CycleMinutes = 2, GreenStartMinutes = 0, GreenDurationMinutes = 1 }) });
        Enter(red, "c", 0.9, Leg(0.5));
        red.AdvanceTo(1.9, 1000);
        before = Json(red);
        Assert.False(red.HoldVehicle("c", 0.25).Applied);
        Assert.Equal(before, Json(red));
    }

    [Fact]
    public void HeadwayAndSignalsAbsorbPartOfATrafficHoldAndTheChargeIsWhatReplays()
    {
        // Headway: b reached the end at 1.1 and waits for a's 0.3 discharge headway (until 1.3).
        var kernel = new TrafficKernel(new[] { Link(1, 2, 1, 0.3) });
        Enter(kernel, "a", 0, Leg(1));
        Enter(kernel, "b", 0.1, Leg(1));
        kernel.AdvanceTo(1.2, 1000);
        string checkpoint = Json(kernel);
        Assert.Equal(1, kernel.GetPermission("b").NominalMinutes);
        var held = kernel.HoldVehicle("b", 0.25);
        Assert.True(held.Applied);
        Assert.Equal(0.85, kernel.GetPermission("b").NominalMinutes, 12); // sampled position moved back
        Assert.Equal(0.05, held.Charge, 12); // input 0.25 > charge: the headway absorbed the rest
        var replay = TrafficKernel.Restore(JsonSerializer.Deserialize<TrafficKernelState>(checkpoint)!).HoldVehicle("b", 0.25);
        SameBits(held.Charge, replay.Charge); SameBits(held.ReadyAt, replay.ReadyAt); SameBits(held.MotionStartFraction, replay.MotionStartFraction);
        kernel.AdvanceTo(5, 1000);
        Assert.Equal(1.35, kernel.GetPermission("b").CompletedAt!.Value, 12);
        // Signal: a car at the stop line before a green at 2.0 is moved back with a charge of 0, which is still applied.
        var red = new TrafficKernel(new[] { Link(1, 2, 0.5, 0, new TrafficSignalSchedule { CycleMinutes = 2, GreenStartMinutes = 0, GreenDurationMinutes = 1 }) });
        Enter(red, "c", 0.9, Leg(0.5));
        red.AdvanceTo(1.5, 1000);
        var signal = red.HoldVehicle("c", 0.25);
        Assert.True(signal.Applied);
        Assert.Equal(0, signal.Charge);
        Assert.Equal(0.7, signal.MotionStartFraction, 12);
        red.AdvanceTo(5, 1000);
        Assert.Equal(2.0, red.GetPermission("c").CompletedAt!.Value, 12);
    }

    [Fact]
    public void AHeldCarOnALegThatEndsPartwayAlongItsLinkKeepsItsFollowersPositionAndSpacing()
    {
        // a leaves at a curb 60 % along the link; b, twice as fast, crosses the whole link behind it.
        var kernel = new TrafficKernel(new[] { Link(1, 4) });
        Enter(kernel, "a", 0, Leg(0.6, 0.6));
        Enter(kernel, "b", 0.35, Leg(0.5));
        kernel.AdvanceTo(0.4, 1000);
        double follower = kernel.GetPermission("b").NominalMinutes, aBefore = Spatial(kernel, "a", 0.6);
        Assert.True(kernel.HoldVehicle("a", 0.25).Applied);
        Assert.Equal(follower, kernel.GetPermission("b").NominalMinutes);
        double a = Spatial(kernel, "a"), b = Spatial(kernel, "b");
        Assert.True(a < aBefore - 0.01, "the held car moved back");
        Assert.True(a - b >= 0.25 - 1e-12, "the held car stays one storage spacing ahead of its follower");
        Assert.Equal(0.35, a, 9);
    }

    [Fact]
    public void FollowersQueueBehindAHeldCarInFifoOrder()
    {
        // b and c drive twice as fast as a, so without the hold they still finish only behind it (headway 0.01).
        TrafficKernel Build()
        {
            var k = new TrafficKernel(new[] { Link(1, 6, 1, 0.01) });
            Enter(k, "a", 0, Leg(1));
            Enter(k, "b", 0.25, Leg(0.5));
            k.AdvanceTo(0.3, 1000);
            return k;
        }
        var unheld = Build(); var held = Build();
        Assert.True(held.HoldVehicle("a", 0.25).Applied);
        foreach (var k in new[] { unheld, held }) { Enter(k, "c", 0.4, Leg(0.5)); k.AdvanceTo(5, 1000); }
        string[] Order(TrafficKernel k) => k.Capture().Vehicles.OrderBy(v => v.CompletedAt).Select(v => v.VehicleId).ToArray();
        Assert.Equal(new[] { "a", "b", "c" }, Order(unheld));
        Assert.Equal(new[] { "a", "b", "c" }, Order(held));
        foreach (string id in new[] { "a", "b", "c" })
            Assert.True(held.GetPermission(id).CompletedAt > unheld.GetPermission(id).CompletedAt + 1e-9, id + " queued behind the held car");
    }

    [Fact]
    public void ACityCarHoldMovesTheSampledJourneyBackAndReloadsAndReplaysExactly()
    {
        var city = City();
        for (double t = city.State.Minute; t <= MovementFixture.StartMinute + 12 * 60; t += 0.05)
        {
            city.AdvanceTo(t);
            var cars = city.State.Citizens.Where(c => city.TryGetTrafficPermission(c.Identity.Id, out var p) && p.LinkId.HasValue && !p.IsCompleted).ToList();
            if (cars.Count == 0) continue;
            string checkpoint = SaveCodec.Write(city.State);
            foreach (var c in cars)
            {
                double before = city.SampleJourney(c).ElapsedMinutes;
                var hold = city.HoldForPlayer(c.Identity.Id, 0.2, PlayerHoldCause.PlayerVehicle);
                if (!hold.Applied) continue; // refused: nothing changed
                Assert.True(hold.Record.Vehicle);
                // A car its queue already holds back keeps its sampled pose; look for one in free motion.
                if (!(city.SampleJourney(c).ElapsedMinutes < before - 1e-6)) { city = Load(checkpoint); break; }
                Assert.Equal(city.State.Minute - city.SampleJourney(c).ElapsedMinutes, c.DepartureMinute, 9);
                // Replay from the checkpoint through the same call reproduces the record; a reload right after it continues equal.
                var replay = Load(checkpoint);
                var replayed = new PlayerHoldReplay(new[] { hold.Record }, 0, 1);
                Assert.True(replayed.ApplyDue(replay), replayed.Failure); Assert.True(replayed.Done);
                var reloaded = Load(SaveCodec.Write(city.State));
                foreach (var x in new[] { city, replay, reloaded }) x.AdvanceTo(x.State.Minute + 30);
                Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(replay.State));
                Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(reloaded.State));
                return;
            }
        }
        throw new Xunit.Sdk.XunitException("no car hold moved a sampled journey back");
    }

    // ---- E2: the complete stream replays from a checkpoint (harness arm) ----

    private static readonly Lazy<MovementResult> Contact = new(() => MovementRun.Run(new MovementOptions
    {
        Arm = "contact/test", Residents = 96, Days = 1, PlayerContact = true,
        Bodies = new DriverSettings { Pedestrians = 48, Vehicles = 24, Lag = LagModel.Native },
        Checkpoints = new[] { MovementFixture.StartMinute + 1, MovementFixture.StartMinute + 8 * 60, MovementFixture.StartMinute + 9 * 60, MovementFixture.StartMinute + 11 * 60 },
    }));

    [Fact]
    public void E2ReplayFromACheckpointPlusItsStreamEqualsTheContactRun()
    {
        var live = Contact.Value;
        Assert.True(live.Player!.WalkHolds > 0 && live.Player.CarHolds > 0, "the contact arm held walkers and cars");
        Assert.True(live.Player.Absorbed > 0, "some car holds were partly absorbed (input > charge)");
        Assert.Equal(live.Holds.Count, live.Player.Lines.Count);
        var end = live.Checkpoints[^1];
        Assert.Equal(live.Holds[^1].Sequence, end.Sequence);
        foreach (var from in live.Checkpoints.Take(live.Checkpoints.Count - 1))
        {
            var outcome = HoldReplay.Replay(from, end, HoldReplay.Segment(live.Holds, from, end));
            Assert.True(outcome.Equal, "from " + from.Minute + ": " + outcome.Failure);
        }
        // Without the stream the bodies-off run from the same checkpoint to the same minute differs: the holds are real outcomes.
        var bare = HoldReplay.Replay(live.Checkpoints[0], end, Array.Empty<PlayerHoldRecord>(), withoutHolds: true);
        Assert.Equal("", bare.Failure);
        Assert.NotEqual(end.Payload, bare.Payload);
        // Not only through the saved hold counters: inside the contact window the journeys themselves differ (review
        // Opus-r2-1). By 11:00 every held journey has ended, so only the counters still show the holds there.
        var mid = live.Checkpoints.Single(c => c.Minute == MovementFixture.StartMinute + 9 * 60);
        var midBare = HoldReplay.Replay(live.Checkpoints[0], mid, Array.Empty<PlayerHoldRecord>(), withoutHolds: true);
        Assert.Equal("", midBare.Failure);
        static string Journeys(string payload) => string.Join("\n", SaveCodec.Read(payload).State!.Citizens.Select(c =>
            c.Identity.Id + ":" + c.Phase + ":" + c.PlaceId + ":" + c.DepartureMinute.ToString("R", CultureInfo.InvariantCulture)
            + ":" + c.NextEventMinute.ToString("R", CultureInfo.InvariantCulture) + ":" + c.CompletedJourneys));
        Assert.NotEqual(Journeys(mid.Payload), Journeys(midBare.Payload));
        // A segment that starts and ends at one checkpoint is that checkpoint.
        Assert.True(HoldReplay.Replay(end, end, Array.Empty<PlayerHoldRecord>()).Equal);
    }

    [Fact]
    public void ReplayRefusesAGapDuplicateReorderingOrWrongStartOrEndSequence()
    {
        var live = Contact.Value;
        var from = live.Checkpoints[0]; var end = live.Checkpoints[^1];
        var segment = HoldReplay.Segment(live.Holds, from, end).ToList();
        Assert.True(segment.Count >= 3);
        string Refusal(IReadOnlyList<PlayerHoldRecord> edited, long start, long stop)
            => Assert.Throws<ArgumentException>(() => HoldReplay.Replay(new HoldCheckpoint(from.Minute, from.Payload, from.Market, from.Hour, from.MinuteOfHour, start, from.FramesPerSecond),
                new HoldCheckpoint(end.Minute, end.Payload, end.Market, end.Hour, end.MinuteOfHour, stop, end.FramesPerSecond), edited)).Message;
        Assert.Contains("gap", Refusal(segment.Where((_, i) => i != 1).ToList(), from.Sequence, end.Sequence));
        var duplicate = new List<PlayerHoldRecord>(segment); duplicate.Insert(2, segment[1]);
        Assert.Contains("duplicate", Refusal(duplicate, from.Sequence, end.Sequence));
        var swapped = new List<PlayerHoldRecord>(segment); (swapped[1], swapped[2]) = (swapped[2], swapped[1]);
        Assert.Contains("out of order", Refusal(swapped, from.Sequence, end.Sequence));
        Assert.Contains("does not start", Refusal(segment, from.Sequence + 1, end.Sequence));
        Assert.Contains("does not end", Refusal(segment, from.Sequence, end.Sequence + 1));
        Assert.Contains("does not end", Refusal(segment.Take(segment.Count - 1).ToList(), from.Sequence, end.Sequence));
        Assert.True(PlayerHolds.ValidateSegment(segment, from.Sequence, end.Sequence, out string none), none);
    }

    [Fact]
    public void ReplayRefusesARecordedMutationOrChargeThatDoesNotReproduce()
    {
        var live = Contact.Value;
        var from = live.Checkpoints[0]; var end = live.Checkpoints[^1];
        var segment = HoldReplay.Segment(live.Holds, from, end).ToList();
        static double Ulp(double v) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(v) + 1);
        int car = segment.FindIndex(h => h.Vehicle), walk = segment.FindIndex(h => !h.Vehicle);
        var r = segment[car];
        var tampered = new List<PlayerHoldRecord>(segment);
        tampered[car] = new PlayerHoldRecord(r.Sequence, r.Minute, r.PersonId, r.Cause, r.Input, r.Charge, r.MotionStartFraction, r.MotionStartedAt, Ulp(r.ReadyAt));
        var outcome = HoldReplay.Replay(from, end, tampered);
        Assert.False(outcome.Equal); Assert.Contains("did not reproduce", outcome.Failure); Assert.Equal(car, outcome.Applied);
        var w = segment[walk];
        tampered = new List<PlayerHoldRecord>(segment);
        tampered[walk] = new PlayerHoldRecord(w.Sequence, w.Minute, w.PersonId, w.Cause, w.Input, Ulp(w.Charge));
        outcome = HoldReplay.Replay(from, end, tampered);
        Assert.False(outcome.Equal); Assert.Contains("did not reproduce", outcome.Failure);
    }

    [Fact]
    public void ATrafficHoldThatWouldMissASignalWindowIsShortenedToTheRemainingAllowanceWithReplayAndReloadEquality()
    {
        // Review Sol-r1-1: readiness at minute 1, green [0, 1.05) of a 3-minute cycle. Unbounded, a 0.1 hold at 0.95 pushes
        // readiness to 1.10, past the green, and the discharge to 3.0 (charge 2). Bounded by a 0.1 allowance it is shortened.
        TrafficKernel Build()
        {
            var k = new TrafficKernel(new[] { Link(1, 2, 1, 0, new TrafficSignalSchedule { CycleMinutes = 3, GreenStartMinutes = 0, GreenDurationMinutes = 1.05 }) });
            Enter(k, "a", 0, Leg(1));
            k.AdvanceTo(0.95, 1000);
            return k;
        }
        var unbounded = Build();
        Assert.Equal(2, unbounded.HoldVehicle("a", 0.1).Charge, 9);
        var kernel = Build();
        string checkpoint = Json(kernel);
        var held = kernel.HoldVehicle("a", 0.1, maximumCharge: 0.1);
        Assert.True(held.Applied);
        Assert.True(held.Charge > 0 && held.Charge <= 0.1, held.Charge.ToString("R", CultureInfo.InvariantCulture));
        Assert.True(held.MotionStartFraction < 0.95, "the motion still moved back");
        Assert.True(held.ReadyAt < 1.05, "readiness stays inside the green");
        var replay = TrafficKernel.Restore(JsonSerializer.Deserialize<TrafficKernelState>(checkpoint)!);
        var again = replay.HoldVehicle("a", 0.1, maximumCharge: 0.1);
        SameBits(held.Charge, again.Charge); SameBits(held.MotionStartFraction, again.MotionStartFraction); SameBits(held.ReadyAt, again.ReadyAt);
        var reloaded = Copy(kernel);
        foreach (var k in new[] { kernel, replay, reloaded }) k.AdvanceTo(10, 1000);
        Assert.Equal(1 + held.Charge, kernel.GetPermission("a").CompletedAt!.Value, 12);
        Assert.Equal(Json(kernel), Json(replay)); Assert.Equal(Json(kernel), Json(reloaded));
        // The bound is a journey's remaining allowance, so it is always positive.
        Assert.Throws<ArgumentOutOfRangeException>(() => Build().HoldVehicle("a", 0.1, maximumCharge: 0));
    }

    [Fact]
    public void NearTheAllowanceACityCarHoldNeverChargesMoreThanIsLeftAndTheSavedMinutesEqualTheCharges()
    {
        var city = City();
        int applied = 0;
        for (double t = city.State.Minute; applied < 40 && t <= MovementFixture.StartMinute + 12 * 60; t += 0.1)
        {
            city.AdvanceTo(t);
            foreach (var c in city.State.Citizens)
            {
                if (!city.TryGetTrafficPermission(c.Identity.Id, out var p) || !p.LinkId.HasValue || p.IsCompleted) continue;
                if (c.PlayerHoldMinutes == 0) c.PlayerHoldMinutes = 0.95; // as if 0.95 was already charged on this journey
                double used = c.PlayerHoldMinutes;
                var hold = city.HoldForPlayer(c.Identity.Id, 0.25, PlayerHoldCause.PlayerVehicle);
                if (!hold.Applied) continue;
                applied++;
                Assert.True(hold.Record.Charge <= PlayerHolds.MaximumMinutesPerJourney - used + 1e-15, hold.Record.Charge + " after " + used);
                Assert.Equal(used + hold.Record.Charge, c.PlayerHoldMinutes, 15);
            }
        }
        Assert.True(applied > 0);
        var reloaded = Load(SaveCodec.Write(city.State));
        Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(reloaded.State));
    }

    // ---- Replay file and readback ----

    private static string[] SegmentFile(IEnumerable<PlayerHoldRecord> records, long start, long end, bool header = true)
    {
        var lines = new List<string> { "Player.log header line" };
        if (header) lines.Add("segment start=" + start + " end=" + end);
        lines.AddRange(records.Select(r => "12:00:00 " + PlayerHolds.Format(r)));
        return lines.ToArray();
    }

    [Fact]
    public void TheReplayFileNeedsCheckpointBoundsSoAMissingTailOrAnEmptyFileIsRefused()
    {
        var records = Enumerable.Range(1, 3).Select(i => new PlayerHoldRecord(i, 1000 + i, "r", PlayerHoldCause.PlayerFoot, 0.1, 0.1)).ToList();
        Assert.True(PlayerHolds.ReadSegment(SegmentFile(records, 0, 3), out var read, out long start, out long end, out string error), error);
        Assert.Equal(3, read.Count); Assert.Equal(0, start); Assert.Equal(3, end);
        // Review Sol-r1-3: the end comes from the checkpoint, never from the last remaining record.
        Assert.False(PlayerHolds.ReadSegment(SegmentFile(records.Take(2), 0, 3), out _, out _, out _, out error));
        Assert.Contains("does not end", error);
        Assert.False(PlayerHolds.ReadSegment(SegmentFile(records, 0, 3, header: false), out _, out _, out _, out error));
        Assert.Contains("no segment line", error);
        Assert.False(PlayerHolds.ReadSegment(Array.Empty<string>(), out _, out _, out _, out error));
        Assert.False(PlayerHolds.ReadSegment(SegmentFile(Array.Empty<PlayerHoldRecord>(), 0, 3), out _, out _, out _, out error));
        Assert.Contains("does not end", error);
        Assert.True(PlayerHolds.ReadSegment(SegmentFile(Array.Empty<PlayerHoldRecord>(), 3, 3), out read, out _, out _, out error), error);
        Assert.Empty(read);
        var twice = SegmentFile(records, 0, 3).ToList(); twice.Add("segment start=0 end=3");
        Assert.False(PlayerHolds.ReadSegment(twice, out _, out _, out _, out error)); Assert.Contains("more than one", error);
        Assert.False(PlayerHolds.ReadSegment(new[] { "segment start=4 end=3" }, out _, out _, out _, out error));
    }

    [Fact]
    public void AHoldsReadbackInTheSameLogIsNeverReadAsAnOriginalHold()
    {
        // Review Sol-r1-2: Player.log holds the original events and, after a checkpoint query, readback copies of them.
        var records = Enumerable.Range(1, 3).Select(i => new PlayerHoldRecord(i, 1000 + i, "r", PlayerHoldCause.PlayerFoot, 0.1, 0.1)).ToList();
        var log = SegmentFile(records, 0, 3).ToList();
        log.Add("[Persistent City] movement holds since 0 (read-only)");
        log.AddRange(records.Select(r => PlayerHolds.Format(r, PlayerHolds.ReadbackLinePrefix)));
        log.AddRange(records.Select(r => PlayerHolds.Format(r, replay: true)));
        Assert.All(records, r => Assert.False(PlayerHolds.TryParse(PlayerHolds.Format(r, PlayerHolds.ReadbackLinePrefix), out _)));
        Assert.True(PlayerHolds.ReadSegment(log, out var read, out _, out _, out string error), error);
        Assert.Equal(new long[] { 1, 2, 3 }, read.Select(r => r.Sequence));
        // A true duplicate original line is still refused.
        log.Add(PlayerHolds.Format(records[2]));
        Assert.False(PlayerHolds.ReadSegment(log, out _, out _, out _, out error));
        Assert.Contains("duplicate", error);
        Assert.Throws<ArgumentException>(() => PlayerHolds.Format(records[0], "[Persistent City] other seq="));
    }

    [Fact]
    public void TheHoldReadbackBelongsToTheLoadedCityAcrossReloadsAndPresentationRebuilds()
    {
        // Review Sol-r1-4: a city reloaded from an earlier checkpoint, or another city, never sees the discarded run's holds,
        // even without a new contact; a rebuild over the same state keeps them.
        var played = CurrentSchemaOnlyTests.City(); var loaded = CurrentSchemaOnlyTests.City();
        for (long s = 1; s <= 10; s++) PlayerContact.Publish(new PlayerHoldRecord(s, s, "r", PlayerHoldCause.PlayerFoot, 0.1, 0.1), played);
        var list = new List<PlayerHoldRecord>();
        PlayerContact.Since(played, 0, list, out bool gap);
        Assert.Equal(10, list.Count); Assert.False(gap);
        list.Clear(); PlayerContact.Since(played, 0, list, out _);
        Assert.Equal(10, list.Count); // same state: kept
        list.Clear(); PlayerContact.Since(loaded, 0, list, out gap);
        Assert.Empty(list); Assert.False(gap);
        // A replayed hold is the loaded city's own hold and enters its ring.
        PlayerContact.Publish(new PlayerHoldRecord(1, 1, "r", PlayerHoldCause.PlayerFoot, 0.1, 0.1), loaded, replay: true);
        list.Clear(); PlayerContact.Since(loaded, 0, list, out _);
        Assert.Equal(new long[] { 1 }, list.Select(h => h.Sequence));
        Assert.StartsWith(PlayerHolds.ReplayLinePrefix, Debug.Lines().Last());
    }

    // ---- Stream line and ring ----

    [Fact]
    public void TheStreamLineRoundTripsExactlyAndOnlyOriginalLinesParse()
    {
        var walk = new PlayerHoldRecord(7, 1440 + 480.123456789, "r 0001/ü", PlayerHoldCause.PlayerFoot, 0.1 + 0.2, 0.30000000000000004);
        var car = new PlayerHoldRecord(8, 1e6 / 3, "r0002", PlayerHoldCause.PlayerVehicle, 0.25, 0, 1.0 / 3, 1e6 / 3, Math.PI);
        foreach (var record in new[] { walk, car })
        {
            string line = PlayerHolds.Format(record);
            Assert.StartsWith("[Persistent City] hold seq=", line);
            Assert.True(PlayerHolds.TryParse("2026-11-09 12:00:00 " + line + "\r\n", out var parsed));
            Assert.Equal(record.Sequence, parsed.Sequence); Assert.Equal(record.PersonId, parsed.PersonId); Assert.Equal(record.Cause, parsed.Cause);
            SameBits(record.Minute, parsed.Minute); SameBits(record.Input, parsed.Input); SameBits(record.Charge, parsed.Charge);
            Assert.Equal(record.Vehicle, parsed.Vehicle);
            if (record.Vehicle) { SameBits(record.MotionStartFraction, parsed.MotionStartFraction); SameBits(record.MotionStartedAt, parsed.MotionStartedAt); SameBits(record.ReadyAt, parsed.ReadyAt); }
            Assert.False(PlayerHolds.TryParse(PlayerHolds.Format(record, replay: true), out _));
        }
        Assert.Contains(" cause=player-vehicle input=0.25 charge=0 motionStart=", PlayerHolds.Format(car));
        foreach (string bad in new[] { "[Persistent City] hold seq=0 minute=1 person=a cause=player-foot input=0.1 charge=0.1",
            "[Persistent City] hold seq=1 person=a minute=1 cause=player-foot input=0.1 charge=0.1",
            "[Persistent City] hold seq=1 minute=1 person=a cause=npc input=0.1 charge=0.1",
            "[Persistent City] hold seq=1 minute=NaN person=a cause=player-foot input=0.1 charge=0.1",
            "[Persistent City] hold seq=1 minute=1 person=a cause=player-vehicle input=0.1 charge=0.1 motionStart=0.5" })
            Assert.False(PlayerHolds.TryParse(bad, out _), bad);
    }

    [Fact]
    public void TheHoldRingKeepsTheLast256AndReportsAGapAfterEviction()
    {
        var ring = new PlayerHoldRing();
        var list = new List<PlayerHoldRecord>();
        ring.Since(0, list, out bool gap);
        Assert.False(gap); Assert.Empty(list);
        for (long s = 1; s <= 300; s++) ring.Add(new PlayerHoldRecord(s, s, "r", PlayerHoldCause.PlayerFoot, 0.1, 0.1));
        Assert.Equal(256, ring.Count); Assert.Equal(45, ring.OldestSequence); Assert.Equal(300, ring.LastSequence);
        ring.Since(0, list, out gap); Assert.True(gap); Assert.Equal(256, list.Count);
        list.Clear(); ring.Since(44, list, out gap); Assert.False(gap); Assert.Equal(256, list.Count);
        list.Clear(); ring.Since(298, list, out gap); Assert.False(gap); Assert.Equal(new long[] { 299, 300 }, list.Select(h => h.Sequence));
        // A loaded city that starts at another sequence restarts the ring.
        ring.Add(new PlayerHoldRecord(12, 1, "r", PlayerHoldCause.PlayerFoot, 0.1, 0.1));
        Assert.Equal(1, ring.Count); Assert.Equal(12, ring.OldestSequence);
    }

    // ---- Runtime: only the player causes a hold ----

    private static readonly Position Home = new(40, 0, 0), Work = new(100, 0, 0);

    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => PhysicalWalkerFollowTests.Legs(a.Position, b.Position);
    }

    private static CityState Walking()
    {
        const double now = 1440 + 600;
        var state = Population.Create(new List<CitizenIdentity> { new() { Id = "w", Name = "W", Age = 30, AppearanceSeed = 3 } }, new[] {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2, Position = Home },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 2, Position = Work } }, "f2e", "0.40.4", 3, now);
        var c = state.Citizens[0];
        c.HomeId = "home"; c.DayPlaceId = "work"; c.MealOuting = null;
        c.PlaceId = "home"; c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
        c.Journey = PhysicalWalkerFollowTests.Legs(Home, Work);
        c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 300;
        return state;
    }

    [Fact]
    public void OnlyThePlayerCausesAHoldNeverANativeBodyRloActorOrServiceVehicle()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrianBody.Unreachable = null;
        PhysicalPedestrians.VisiblePoint = null; GameManager.Driving = false;
        NativeTravelPace.Focus = Work; Time.Next(3);
        var city = new CitySimulator(Walking(), new Walk());
        var plain = new CitySimulator(Walking(), new Walk());
        var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(), typeof(PlayerHoldTests).Assembly,
            new VendorBodyTests.Log(), 8, 3, 100, new DwellSpots());
        var game = new GameManager { playerController = new PlayerControllerDouble(), selectedVehicle = new VehicleController() };
        try
        {
            InstanceBehavior<GameManager>.Instance = null;
            void Frame() { city.AdvanceTo(city.State.Minute + 0.02); plain.AdvanceTo(plain.State.Minute + 0.02); Time.Next(0.3f); bodies.Update(); }
            Frame();
            var pool = PhysicalPedestrians.Last!;
            Assert.True(pool.Active.ContainsKey("w"));
            var walker = pool.Active["w"];
            Vector3 Ahead(double metres) => PhysicalPedestrianBody.Vector(new Position(walker.Position.X + metres, 0, 0));
            // The walker stands still while its logical pose moves on (visibly blocked). A native pedestrian stands right in
            // front of it, an RLO-reserved actor beside it, and the player is nowhere (no controller), then 30 m away.
            var native = new Actor(); native.transform.position = Ahead(0.5);
            AmbientPedestrians.Reserved.Add("rlo-actor");
            for (int i = 0; i < 20; i++) Frame();
            InstanceBehavior<GameManager>.Instance = game;
            game.playerController!.transform.position = Ahead(30);
            for (int i = 0; i < 20; i++) Frame();
            // Driving: the player's own vehicle in front of a walker is not a foot contact either.
            GameManager.Driving = true; game.selectedVehicle!.transform.position = Ahead(0.5);
            for (int i = 0; i < 20; i++) Frame();
            Assert.Equal(0, city.State.PlayerHoldSequence);
            bodies.BeforeSave();
            Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(city.State));
            // The player's avatar 0.5 m ahead along the leg: now the blocked walker is held, and each hold is a stream line.
            GameManager.Driving = false; game.playerController.transform.position = Ahead(0.5);
            double departure = city.State.Citizens[0].DepartureMinute;
            for (int i = 0; i < 12; i++) Frame();
            Assert.True(city.State.PlayerHoldSequence >= 1);
            Assert.True(city.State.Citizens[0].DepartureMinute > departure);
            Assert.Equal(city.State.Citizens[0].PlayerHoldMinutes, city.State.Citizens[0].DepartureMinute - departure, 9);
            var line = Debug.Lines().Last(l => l.StartsWith(PlayerHolds.LinePrefix, StringComparison.Ordinal));
            Assert.True(PlayerHolds.TryParse(line, out var logged));
            Assert.Equal("w", logged.PersonId); Assert.Equal(PlayerHoldCause.PlayerFoot, logged.Cause);
            Assert.True(logged.Input <= PlayerHolds.MaximumReportMinutes);
            var ring = new List<PlayerHoldRecord>();
            PlayerContact.Ring.Since(0, ring, out _);
            Assert.Equal(city.State.PlayerHoldSequence, ring.Last().Sequence);
            // Bodies-off replay of exactly that stream gives the same city.
            var stream = ring.Where(h => h.Sequence >= 1 && h.Sequence <= city.State.PlayerHoldSequence).ToList();
            var replay = new CitySimulator(Walking(), new Walk());
            var replayer = new PlayerHoldReplay(stream, 0, city.State.PlayerHoldSequence);
            while (replay.State.Minute < city.State.Minute)
            {
                replay.AdvanceTo(replayer.Limit(city.State.Minute));
                Assert.True(replayer.ApplyDue(replay), replayer.Failure);
            }
            Assert.True(replayer.Done);
            bodies.BeforeSave();
            Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(replay.State));
        }
        finally
        {
            InstanceBehavior<GameManager>.Instance = null; GameManager.Driving = false; AmbientPedestrians.Reserved.Clear();
            bodies.Dispose();
        }
    }
}

// Opt-in offline measurements for docs/perf/f2e.md: set PC_F2E_MEASURE to an output file. The production PhysicalPopulation
// on the native doubles with 48 walkers, without a player (the contact pass is skipped, as before F2e), with the player
// far from every body, and with the player standing inside the crowd; then the contact rule alone per body.
[Collection(StreetFoodHooks.Collection)]
public sealed class PlayerHoldPerfTests
{
    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => Legs(a.Position, b.Position);
    }
    private static Route Legs(Position a, Position b)
    {
        var route = new Route { Legs = new() };
        for (int i = 0; i < 6; i++)
        {
            var from = Position.Lerp(a, b, i / 6.0); var to = Position.Lerp(a, b, (i + 1) / 6.0);
            route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Walk, Minutes = Position.Distance(from, to) / 2 });
        }
        return route;
    }

    private static string Update(string label, Func<PhysicalPedestrians, Position?> player)
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); GameManager.Driving = false;
        const double now = 1440 + 600;
        var ids = Enumerable.Range(0, 48).Select(i => new CitizenIdentity { Id = "w" + i.ToString("D2"), Name = "W" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place> { new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 64, Position = new Position(100, 0, 0) } };
        for (int i = 0; i < 48; i++)
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1,
                Position = new Position(100 + 90 * Math.Cos(i * 0.13), 0, 90 * Math.Sin(i * 0.13)) });
        var state = Population.Create(ids, places, "f2e-perf", "0.40.4", 3, now);
        for (int i = 0; i < 48; i++)
        {
            var c = state.Citizens[i];
            c.HomeId = c.PlaceId = "home" + i; c.DayPlaceId = "work"; c.MealOuting = null;
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.Journey = Legs(places[i + 1].Position, places[0].Position);
            c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 600;
        }
        var city = new CitySimulator(state, new Walk());
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        var game = new GameManager { playerController = new PlayerControllerDouble(), selectedVehicle = new VehicleController() };
        try
        {
            using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
                typeof(PlayerHoldPerfTests).Assembly, new VendorBodyTests.Log(), 64, 8, 120, new DwellSpots());
            var pool = PhysicalPedestrians.Last!;
            void Move(float seconds)
            {
                foreach (var body in pool.Active.Values)
                {
                    if (body.Agent.isStopped) continue;
                    double pace = NativeTravelPace.WalkingMetresPerSecond * (body.IdentityId.GetHashCode() % 3 == 0 ? 0.85 : 1);
                    var at = body.Position; double d = Position.Distance(at, body.Target), step = pace * seconds;
                    body.Character.transform.position = PhysicalPedestrianBody.Vector(d <= step ? body.Target : Position.Lerp(at, body.Target, step / d));
                }
            }
            double minute = now;
            const float frame = 1 / 60f;
            void Frame()
            {
                Move(frame); minute += frame / 3.0; city.AdvanceTo(minute); Time.Next(frame);
                var at = player(pool);
                InstanceBehavior<GameManager>.Instance = at.HasValue ? game : null;
                if (at.HasValue) game.playerController!.transform.position = PhysicalPedestrianBody.Vector(at.Value);
            }
            for (int i = 0; i < 120; i++) { Frame(); bodies.Update(); }
            int count = 2400;
            var times = new List<double>(count); int bound = 0;
            long holds = city.State.PlayerHoldSequence;
            for (int i = 0; i < count; i++)
            {
                Frame();
                long start = Stopwatch.GetTimestamp();
                bodies.Update();
                times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                bound = Math.Max(bound, pool.Active.Count);
            }
            times.Sort();
            return string.Format(CultureInfo.InvariantCulture,
                "Update, 48 walkers, {0}: bound max={1}, n={2}, p50={3:F4} ms, p95={4:F4} ms, max={5:F4} ms, spikes>=30ms={6}, holds={7}",
                label, bound, count, times[count / 2], times[(int)Math.Ceiling(count * .95) - 1], times[^1], times.Count(t => t >= 30),
                city.State.PlayerHoldSequence - holds);
        }
        finally { InstanceBehavior<GameManager>.Instance = null; }
    }

    [Fact]
    public void MeasureContactDetectionAndHoldCost()
    {
        string? path = System.Environment.GetEnvironmentVariable("PC_F2E_MEASURE");
        if (path == null) return;
        var lines = new List<string>();
        for (int run = 0; run < 2; run++)
        {
            lines.Add(Update("no player (contact pass skipped, as before F2e)", _ => null));
            lines.Add(Update("player 1 km away", _ => new Position(1100, 0, 0)));
            // The player stands 0.5 m ahead of the first walker on its own leg every frame: the worst case, a held body.
            lines.Add(Update("player 0.5 m ahead of one walker", pool =>
            {
                if (!pool.Active.TryGetValue("w00", out var w)) return new Position(100, 0, 0);
                var d = new Position(100 - w.Position.X, 0, 0 - w.Position.Z); double n = Math.Sqrt(d.X * d.X + d.Z * d.Z);
                return n < 1e-6 ? w.Position : new Position(w.Position.X + d.X / n * 0.5, 0, w.Position.Z + d.Z / n * 0.5);
            }));
        }
        // The contact rule alone: one leg sample and Observe per body, 72 bodies (48 walkers and 24 cars), 20,000 frames.
        var route = Legs(new Position(0, 0, 0), new Position(120, 0, 0));
        var contact = new PlayerContact();
        var far = new PlayerPose(new Position(1000, 0, 0), false); var near = new PlayerPose(new Position(30.5, 0, 0), false);
        foreach (var (label, pose) in new[] { ("far", far), ("in contact", near) })
        {
            const int frames = 20000, body = 72;
            var ids = Enumerable.Range(0, body).Select(b => "b" + b.ToString(CultureInfo.InvariantCulture)).ToArray();
            long start = Stopwatch.GetTimestamp();
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < body; b++)
                {
                    var own = WalkerFollow.Sample(route, 15);
                    contact.Observe(ids[b], route, b >= 48, 15 + f * 0.0001, 15, new Position(30, 0, 0), own.From, own.To, pose, 1000 + f * 0.0001, f);
                }
            double ns = (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / (frames * (double)body);
            lines.Add(string.Format(CultureInfo.InvariantCulture, "Contact rule per body ({0}): {1:F1} ns; per frame at 72 bodies: {2:F4} ms", label, ns, ns * 72 / 1e6));
        }
        // The pre-check PhysicalPopulation runs first (a body out of reach with no open track stops there).
        {
            const int frames = 20000, body = 72;
            var ids = Enumerable.Range(0, body).Select(b => "far" + b.ToString(CultureInfo.InvariantCulture)).ToArray();
            int reached = 0;
            long start = Stopwatch.GetTimestamp();
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < body; b++)
                    if (PlayerContact.InReach(new Position(30 + (f & 1) * 0.01, 0, 0), far, b >= 48) || contact.Tracking(ids[b])) reached++;
            double ns = (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / (frames * (double)body);
            lines.Add(string.Format(CultureInfo.InvariantCulture, "Pre-check per body (far): {0:F1} ns; per frame at 72 bodies: {1:F4} ms (reached {2})", ns, ns * 72 / 1e6, reached));
        }
        File.AppendAllText(path, string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine);
    }
}
