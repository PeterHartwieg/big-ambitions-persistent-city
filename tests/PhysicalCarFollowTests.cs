using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

// F2d (docs/plans/f2-movement.md Decision 2): cars follow the traffic kernel and never write logical state. The
// production PhysicalPopulation runs on the native doubles; a car moves only when a test drives it through the
// production CommandLimit, so every lag here is one the test made. Each case runs a plain twin city without bodies and
// compares save bytes (E1).
[Collection(StreetFoodHooks.Collection)]
public sealed class PhysicalCarFollowTests
{
    private const string Native = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const int Sections = 12;
    private const double Now = 1440 + 600, GamePerSecond = 1 / 3.0, Section = 12;
    // Kernel pace: 12 m per 1.2 game minutes, so 10 m per game minute, 3.33 m per real second here.
    private const double KernelMetresPerSecond = 10 * GamePerSecond;
    private static Position Node(int i) => new(40 + Section * i, 0, 0);
    private static Position Work => new(Node(Sections).X + 10, 0, 0);

    private sealed class Roads : IRoutePlanner, ITrafficRoutePlanner
    {
        public Roads(TrafficSignalSchedule? signal) => TrafficNetwork = new TrafficNetwork(Native, Enumerable.Range(0, Sections + 1).Select(i =>
            new TrafficNetworkNode { Id = i, Position = Node(i), KilometresPerHour = 36, AllowsCar = true, Boundary = true,
                Signal = i == 3 ? signal : null, Successors = i < Sections ? new[] { i + 1 } : Array.Empty<int>() }).ToList(), 1);
        public TrafficNetwork? TrafficNetwork { get; set; }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int id) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }
    private static Route Journey()
    {
        var route = new Route { Legs = new() };
        for (int i = 0; i < Sections; i++)
            route.Legs.Add(new RouteLeg { From = Node(i), To = Node(i + 1), Minutes = 1.2, Mode = TravelMode.Car,
                ServiceId = "native:road:v1:" + Native + ":" + i + ":" + (i + 1) });
        route.Legs.Add(new RouteLeg { From = Node(Sections), To = Work, Minutes = 5, Mode = TravelMode.Walk });
        return route;
    }
    private static CityState Build(int cars)
    {
        var ids = Enumerable.Range(0, cars).Select(i => new CitizenIdentity { Id = "c" + i, Name = "C" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var state = Population.Create(ids, new[] {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = cars, Position = Node(0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = cars, Position = Work } }, "f2d", "0.40.4", 3, Now);
        foreach (var c in state.Citizens)
        {
            c.HomeId = c.PlaceId = "home"; c.DayPlaceId = c.DestinationId = "work"; c.MealOuting = null;
            c.Phase = CitizenPhase.Travelling; c.DestinationActivity = Activity.Work; c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = Node(0) };
            c.Journey = Journey(); c.DepartureMinute = Now; c.NextEventMinute = Now + c.Journey.Minutes; c.ActivityUntilMinute = Now + 600;
        }
        return state;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly CitySimulator City, Plain;
        internal readonly PhysicalPopulation Bodies;
        internal readonly long FirstSequence = PhysicalPopulation.Telemetry.LastSequence;
        internal readonly WalkerMovementTelemetry T = PhysicalPopulation.Telemetry;
        internal readonly long Handoffs = PhysicalPopulation.Telemetry.CarHandoffs, Held = PhysicalPopulation.Telemetry.KernelHeldAtNativeGreen,
            Stopped = PhysicalPopulation.Telemetry.NativeStopAtKernelGreen;
        internal double Minute;
        internal PhysicalVehicles Cars => PhysicalVehicles.Last!;
        internal PhysicalPedestrians Walkers => PhysicalPedestrians.Last!;
        internal Fixture(int cars = 1, TrafficSignalSchedule? signal = null)
        {
            StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrianBody.Unreachable = null;
            PhysicalPedestrians.VisiblePoint = null; MultimodalRoutes.CarSamples = true;
            NativeTravelPace.Focus = Node(6); Time.Next(3);
            City = new CitySimulator(Build(cars), new Roads(signal));
            Plain = new CitySimulator(Build(cars), new Roads(signal));
            Minute = Now + 0.01; City.AdvanceTo(Minute); Plain.AdvanceTo(Minute);
            Bodies = new(new Transform { Component = new PopulationRuntime() }, City, new MultimodalRoutes(), typeof(Fixture).Assembly,
                new VendorBodyTests.Log(), 8, 4, 120, new DwellSpots());
        }
        internal CitizenState Person(string id) => City.State.Citizens.Single(c => c.Identity.Id == id);
        internal PhysicalVehicleBody? Car(string id) => Cars.Active.TryGetValue(id, out var body) ? body : null;
        // The next committed road corner ahead of a car body: Gley raises its reached marker there.
        internal Position? Corner(PhysicalVehicleBody body)
        {
            Position? best = null; double nearest = double.PositiveInfinity;
            // Every person drives the same committed road, also after the journey ended.
            foreach (var leg in Journey().Legs.Where(l => l.Mode == TravelMode.Car))
            {
                double d = Position.Distance(body.Position, leg.To);
                if (d < nearest && leg.To.X >= body.Position.X - 1e-9) { nearest = d; best = leg.To; }
            }
            return best;
        }
        /// <summary>One frame: native cars drive (CommandLimit), the city advances, then PhysicalPopulation.Update.</summary>
        internal void Frame(float seconds = 1 / 30f, double speed = KernelMetresPerSecond * 1.2)
        {
            Cars.Drive(seconds, speed, Corner);
            Minute += seconds * GamePerSecond; City.AdvanceTo(Minute); Plain.AdvanceTo(Minute);
            Time.Next(seconds); Bodies.Update();
            Assert.False(City.Traffic!.HasPhysicalControl);
        }
        internal void Until(Func<bool> done, int frames = 20000, float seconds = 1 / 30f)
        {
            for (int i = 0; i < frames && !done(); i++) Frame(seconds);
            Assert.True(done());
        }
        internal double LogicalX(string id) => Person(id).Phase == CitizenPhase.Travelling ? City.SampleJourney(Person(id)).Position.X : double.PositiveInfinity;
        internal bool HasBody(string id) => Cars.Active.ContainsKey(id) || Walkers.Active.ContainsKey(id);
        internal List<WalkerCorrection> Corrections()
        {
            var list = new List<WalkerCorrection>();
            T.Since(FirstSequence, list, out bool gap);
            Assert.False(gap);
            return list;
        }
        internal void AssertSameAsBodiesOff()
        {
            Bodies.BeforeSave(); Plain.RefreshOptionalStops();
            Assert.Equal(SaveCodec.Write(Plain.State), SaveCodec.Write(City.State));
        }
        public void Dispose() { MultimodalRoutes.CarSamples = false; PhysicalPedestrians.VisiblePoint = null; Bodies.Dispose(); }
    }

    /// <summary>A slow visible head car (0.3x the kernel's pace) neither holds itself nor its follower: the kernel
    /// discharges both in FIFO order at the bodies-off minutes. Neither car is ever commanded past its kernel
    /// coordinate, no vehicle is under physical control, and the save equals the bodies-off twin throughout.</summary>
    [Fact]
    public void SlowVisibleHeadCarNeverHoldsTheKernelOrItsFollower()
    {
        using var f = new Fixture(cars: 2);
        var order = new List<string>(); var plainOrder = new List<string>();
        f.City.Traffic!.VehicleCompleted += (id, at) => order.Add(id + "@" + at.ToString("R"));
        f.Plain.Traffic!.VehicleCompleted += (id, at) => plainOrder.Add(id + "@" + at.ToString("R"));
        f.Until(() => f.Car("c0") != null && f.Car("c1") != null, 600);
        f.Car("c0")!.OnScreen = true; f.Car("c1")!.OnScreen = true;
        for (int frame = 0; frame < 4000 && order.Count < 2; frame++)
        {
            foreach (var body in f.Cars.Active.Values) body.Pace = body.CitizenId == "c0" ? 0.3 : 1.2;
            f.Frame();
            foreach (var body in f.Cars.Active.Values) Assert.True(body.Position.X <= f.LogicalX(body.CitizenId) + 1e-6, "a car is ahead of the kernel");
            if (frame % 500 == 0) f.AssertSameAsBodiesOff();
        }
        Assert.Equal(2, order.Count);
        Assert.Equal(plainOrder, order);
        Assert.StartsWith("c0@", order[0]);
        // The slow head fell behind the kernel; it was hurried, not moved, while on screen.
        Assert.True(f.T.CarLagSamples > 0 && f.T.CarMaximumLagMetres > CarFollow.CatchUpMetres);
        Assert.DoesNotContain(f.Corrections(), c => c.OnScreen && c.Kind == WalkerCorrectionKind.Move);
        f.Until(() => f.Person("c0").CompletedJourneys == 1 && f.Person("c1").CompletedJourneys == 1);
        f.AssertSameAsBodiesOff();
    }

    /// <summary>Off screen, a car more than 30 m behind its kernel coordinate is released (one LagRelease with the
    /// before pose and the logical pose) and a car is re-acquired at the logical pose at the next selection, without the
    /// retry cooldown. It never reappears in view.</summary>
    [Fact]
    public void OffScreenCarBeyondThirtyMetresIsReleasedAndReacquiredAtTheLogicalPose()
    {
        using var f = new Fixture();
        f.Until(() => f.Car("c0") != null, 600);
        var first = f.Car("c0")!;
        first.Pace = 0;
        f.Until(() => f.Car("c0") != first, 2000);
        var release = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.LagRelease, release.Kind); Assert.True(release.Car); Assert.False(release.OnScreen);
        Assert.Equal("c0", release.PersonId); Assert.Equal(first.Position, release.Before);
        Assert.True(Position.Distance(release.Before, release.After) > CarFollow.CorrectionMetres);
        Assert.Contains("body=car", WalkerMovementTelemetry.Format(release));
        // Re-acquired at the logical pose within a selection tick (no two-second cooldown).
        f.Until(() => f.Car("c0") != null, 30);
        Assert.InRange(f.Car("c0")!.Position.X, f.LogicalX("c0") - 1, f.LogicalX("c0") + 1e-6);
        f.Until(() => f.Person("c0").CompletedJourneys == 1);
        f.AssertSameAsBodiesOff();
    }

    /// <summary>Q3 on screen: a car more than 30 m behind keeps catching up for 10 s, then stops at its next corner and
    /// fades out over 1 s (one Fade, on screen, with its before and after poses); it is not moved on screen. While the
    /// logical pose is in view the person gets no body; once it is out of view a car reappears at the logical pose.</summary>
    [Fact]
    public void VisibleCarBeyondThirtyMetresHurriesThenFadesAtTheNextCornerAndReappearsOutOfView()
    {
        using var f = new Fixture();
        f.Until(() => f.Car("c0") != null, 600);
        var body = f.Car("c0")!;
        body.OnScreen = true; body.CanFade = true; body.Pace = 0;
        PhysicalPedestrians.VisiblePoint = _ => true;
        double behind = 0; f.Until(() => (behind = f.LogicalX("c0") - body.Position.X) > CarFollow.CorrectionMetres, 4000);
        // Ten seconds of hurrying standing mid-leg: no fade, no correction.
        for (int i = 0; i < 300; i++) f.Frame();
        Assert.Same(body, f.Car("c0")); Assert.Equal(1f, body.Alpha);
        Assert.Empty(f.Corrections());
        // It reaches its corner: it stands and fades out there.
        body.Pace = 1; f.Until(() => body.Alpha < 1, 2000);
        var at = body.Position;
        Assert.InRange(Enumerable.Range(0, Sections + 1).Min(i => Position.Distance(at, Node(i))), 0, CarFollow.CornerMetres);
        f.Until(() => f.Car("c0") == null, 60);
        Assert.Equal(at, body.Position);
        Assert.Contains(0f, body.Fades);
        var fade = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.Fade, fade.Kind); Assert.True(fade.Car); Assert.True(fade.OnScreen); Assert.Equal(at, fade.Before);
        Assert.Equal(f.City.SampleJourney(f.Person("c0")).Position.X, fade.After.X, 1);
        // No body while the logical pose is in view; one at the logical pose once it is not.
        for (int i = 0; i < 90; i++) f.Frame();
        Assert.False(f.HasBody("c0"));
        PhysicalPedestrians.VisiblePoint = null;
        f.Until(() => f.Car("c0") != null, 30);
        Assert.InRange(f.Car("c0")!.Position.X, f.LogicalX("c0") - 1, f.LogicalX("c0") + 1e-6);
        Assert.DoesNotContain(f.Corrections(), c => c.OnScreen && c.Kind == WalkerCorrectionKind.Move);
        f.AssertSameAsBodiesOff();
    }

    /// <summary>Q3 with opaque materials: a visible car past the hurry bound at a corner is not faded; it keeps following
    /// until it is off screen, and is then released off screen.</summary>
    [Fact]
    public void OpaqueVisibleCarIsNotFadedButKeptUntilItIsOutOfView()
    {
        using var f = new Fixture();
        f.Until(() => f.Car("c0") != null, 600);
        var body = f.Car("c0")!;
        body.OnScreen = true; body.CanFade = false; body.Pace = 0;
        f.Until(() => f.LogicalX("c0") - body.Position.X > CarFollow.CorrectionMetres, 4000);
        for (int i = 0; i < 300; i++) f.Frame();
        body.Pace = 1;
        for (int i = 0; i < 300; i++) f.Frame();
        Assert.Same(body, f.Car("c0")); Assert.Empty(body.Fades); Assert.Empty(f.Corrections());
        body.OnScreen = false;
        f.Until(() => f.Car("c0") != body, 30);
        var release = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.LagRelease, release.Kind); Assert.False(release.OnScreen); Assert.True(release.Car);
        f.AssertSameAsBodiesOff();
    }

    /// <summary>A mode boundary is never a hold: the car reaches its parking point (the end of its car legs), hands over
    /// (no correction), and a walker is acquired at the logical pose at the next selection.</summary>
    [Fact]
    public void ParkingHandsOverToAWalkerAtTheLogicalPose()
    {
        using var f = new Fixture();
        f.Until(() => f.Car("c0") != null, 600);
        f.Car("c0")!.Pace = 1.5;
        f.Until(() => f.Car("c0") == null, 4000);
        Assert.Equal(f.Handoffs + 1, f.T.CarHandoffs);
        Assert.Empty(f.Corrections());
        f.Until(() => f.Walkers.Active.ContainsKey("c0"), 30);
        Assert.Equal(TravelMode.Walk, f.City.SampleJourney(f.Person("c0")).Mode);
        Assert.InRange(Position.Distance(f.Walkers.Active["c0"].Position, f.City.PositionOf(f.Person("c0"))), 0, 1);
        f.AssertSameAsBodiesOff();
    }

    /// <summary>Q3 at the journey's end: the person arrives (traffic completed, egress walked) while an opaque car still
    /// lags on screen. The car is not removed in view: it drives on to its parking point and hands over there.</summary>
    [Fact]
    public void ArrivalWhileAVisibleCarLagsKeepsItUntilItParks()
    {
        using var f = new Fixture();
        f.Until(() => f.Car("c0") != null, 600);
        var body = f.Car("c0")!;
        body.OnScreen = true; body.CanFade = false; body.Pace = 0;
        f.Until(() => f.Person("c0").Phase != CitizenPhase.Travelling, 4000);
        Assert.Same(body, f.Car("c0"));
        Assert.Empty(f.Corrections());
        body.Pace = 1.5;
        f.Until(() => f.Car("c0") == null, 4000);
        Assert.True(body.Position.X >= Node(Sections).X - 1e-6, "the car drove on to its parking point");
        Assert.Equal(f.Handoffs + 1, f.T.CarHandoffs);
        Assert.Empty(f.Corrections());
        f.AssertSameAsBodiesOff();
    }

    /// <summary>Decision 6, measured: a standing visible car held by the kernel at its red while Gley's target light is
    /// green counts one KernelHeldAtNativeGreen episode, with seconds. The signal phase is not changed.</summary>
    [Fact]
    public void KernelHeldAtANativeGreenIsCountedAsAPhaseMismatch()
    {
        // Red at the third section's end from 2001 to 3000.
        var signal = new TrafficSignalSchedule { CycleMinutes = 1000, GreenStartMinutes = 0, GreenDurationMinutes = 1 };
        using var f = new Fixture(signal: signal);
        f.Until(() => f.Car("c0") != null, 600);
        var body = f.Car("c0")!;
        body.OnScreen = true; body.NativeRed = false;
        f.Until(() => f.City.Traffic!.GetPermission("c0") is var p && !p.SignalGreen
            && p.NominalMinutes >= p.LinkEndNominalMinutes - 1e-9 && body.Moved == 0, 4000);
        for (int i = 0; i < 60; i++) f.Frame();
        Assert.Equal(f.Held + 1, f.T.KernelHeldAtNativeGreen);
        Assert.True(f.T.KernelHeldAtNativeGreenSeconds > 1.5);
        Assert.Equal(f.Stopped, f.T.NativeStopAtKernelGreen);
        Assert.Contains("phase_kernel_held_native_green=", f.T.Status(0, f.Bodies.CarFollowers));
        f.AssertSameAsBodiesOff();
    }

    /// <summary>Decision 6, measured: a visible car stopped by its native red while the kernel has driven past that
    /// junction counts one NativeStopAtKernelGreen episode; the kernel does not wait for it.</summary>
    [Fact]
    public void NativeStopAtAKernelGreenIsCountedAsAPhaseMismatch()
    {
        using var f = new Fixture();
        f.Until(() => f.Car("c0") != null, 600);
        var body = f.Car("c0")!;
        body.OnScreen = true; body.NativeRed = true;
        for (int i = 0; i < 30; i++) f.Frame();
        // Gley holds it at its stop line: it stands while the kernel moves on past that junction.
        body.Pace = 0;
        double kernelAt = f.LogicalX("c0");
        for (int i = 0; i < 240; i++) f.Frame();
        Assert.True(f.LogicalX("c0") > kernelAt + 20);
        Assert.Same(body, f.Car("c0"));
        Assert.Equal(f.Stopped + 1, f.T.NativeStopAtKernelGreen);
        Assert.True(f.T.NativeStopAtKernelGreenSeconds > 1);
        Assert.Equal(f.Held, f.T.KernelHeldAtNativeGreen);
        f.AssertSameAsBodiesOff();
    }

    /// <summary>Reload with a visible car mid-leg equals bodies-off: the save is the logical state (no body offset, no
    /// physical control), and the reloaded city continues to equal bytes.</summary>
    [Fact]
    public void ReloadWithAVisibleCarMidLegEqualsBodiesOff()
    {
        using var f = new Fixture();
        f.Until(() => f.Car("c0") != null, 600);
        f.Car("c0")!.Pace = 0.5;
        for (int i = 0; i < 200; i++) f.Frame();
        Assert.NotNull(f.Car("c0"));
        f.Bodies.BeforeSave();
        string saved = SaveCodec.Write(f.City.State);
        Assert.Equal(SaveCodec.Write(f.Plain.State), saved);
        Assert.DoesNotContain(SaveWire.Read(saved).State!.Traffic!.Vehicles, v => v.PhysicalControl);
        var reloaded = new CitySimulator(SaveWire.Read(saved).State!, new Roads(null));
        reloaded.AdvanceTo(f.Minute + 30); f.Plain.AdvanceTo(f.Minute + 30);
        Assert.Equal(SaveCodec.Write(f.Plain.State), SaveCodec.Write(reloaded.State));
    }
}
