using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.MovementEquivalence;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

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
        Checkpoints = new[] { MovementFixture.StartMinute + 1, MovementFixture.StartMinute + 8 * 60, MovementFixture.StartMinute + 11 * 60 },
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
        // Without the stream the bodies-off run differs: the holds are real outcomes.
        var bare = HoldReplay.Replay(end, end, Array.Empty<PlayerHoldRecord>());
        Assert.True(bare.Equal);
        Assert.NotEqual(live.Checkpoints[0].Payload, end.Payload);
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

    // ---- Schema 35: older writer refused, golden pinned ----

    [Fact]
    public void AnOlderWriterCityIsRefusedWithTheStartANewCityStatusAndKept()
    {
        var state = CurrentSchemaOnlyTests.City();
        string older = DevelopmentChain.Write34(state);
        Assert.StartsWith("{\"Schema\":34,\"f\":\"pcc-c\",", older);
        Assert.DoesNotContain("PlayerHold", SaveCodec.ExpandCompact(older));
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { [SaveStore.StateKey] = older, ["other"] = "kept" };
        var read = SaveStore.ReadFrom(data, state.PopulationKey);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Null(read.State); Assert.Equal(34, read.SourceSchema);
        Assert.Contains("older development build; start a new city", read.Error);
        Assert.Equal(34, SaveCodec.DevelopmentSchemaOf(older));
        Assert.False(SaveStore.WriteTo(data, CurrentSchemaOnlyTests.City()).Success);
        Assert.False(SaveSession.Open(data, state.PopulationKey).WriteTo(data, CurrentSchemaOnlyTests.City()).Success);
        Assert.Same(older, data[SaveStore.StateKey]); Assert.Equal("kept", data["other"]);
    }

    [Fact]
    public void TheHoldFieldsAreSavedAndValidated()
    {
        var city = City();
        var w = Walker(city, (c, inLeg) => inLeg >= 0.3);
        Assert.True(city.HoldForPlayer(w.Identity.Id, 0.2, PlayerHoldCause.PlayerFoot).Applied);
        string payload = SaveCodec.Write(city.State);
        var body = SaveWire.Body(payload);
        Assert.Contains("\"PlayerHoldSequence\":1", body);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(1, read.State!.PlayerHoldSequence);
        Assert.Equal(w.PlayerHoldMinutes, read.State.Citizens.Single(c => c.Identity.Id == w.Identity.Id).PlayerHoldMinutes);
        Assert.Equal(payload, SaveCodec.Write(read.State));
        string Edited(Action<CityState> edit)
        {
            var copy = SaveCodec.Read(payload).State!; edit(copy);
            try { return SaveCodec.Write(copy); } catch (SaveValidationException e) { return "refused: " + e.Message; }
        }
        Assert.Contains("Player hold minutes", Edited(s => s.Citizens.Single(c => c.Identity.Id == w.Identity.Id).PlayerHoldMinutes = 1.01));
        Assert.Contains("Player hold minutes", Edited(s => s.Citizens.Single(c => c.Identity.Id == w.Identity.Id).PlayerHoldMinutes = -0.1));
        Assert.Contains("without a journey", Edited(s => s.Citizens.First(c => c.Phase == CitizenPhase.AtPlace).PlayerHoldMinutes = 0.1));
        Assert.Contains("hold sequence", Edited(s => s.PlayerHoldSequence = -1));
    }

    [Theory]
    [InlineData(5150, "4187373b9cca6eb6c619e7aae9e1d270d067e5bc1ac40ee64c1aee5d02e210ae", 73332)]
    [InlineData(9217, "149d635dbab32312988b96bcde19d011fe1a06814db5a9f175380d29741a5abb", 72970)]
    public void TheCurrentWriterGoldenIsPinned(int seed, string pin, int characters)
    {
        Assert.Equal(35, CityState.PlayerHoldSchema);
        Assert.Equal(CityState.PlayerHoldSchema, CityState.CurrentSchema);
        var state = ChangeJournalTests.Fixture(seed);
        string payload = SaveCodec.Write(state);
        Assert.StartsWith("{\"Schema\":35,\"f\":\"pcc-c\",\"v\":1,\"k\":[", payload);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        Assert.Equal(payload, SaveCodec.Encode(SaveCodec.Capture(state)));
        string actual = EconomicLedgerTests.Sha(payload) + " " + payload.Length;
        Assert.True(pin + " " + characters == actual, actual);
    }
}
