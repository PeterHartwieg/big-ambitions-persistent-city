using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GleyTrafficSystem;
using GleyUrbanAssets;
using PersistentCity.Core;
using PersistentCity.Runtime;
using StagingHarness;
internal static class Program
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static Position P(double x) => new Position(x,0,0);
    private sealed class Fixture
    {
        public readonly RoadRoutes Roads;
        public readonly Waypoint[] Native;
        public Fixture(bool nextJunction = false, double[]? positions = null, Action<Waypoint[]>? setup = null)
        {
            var native = Native = Enumerable.Range(0, 9).Select(i => new Waypoint { listIndex=i, position=new UnityEngine.Vector3((float)(positions == null ? i*10 : positions[i]),0,0), neighbors=i<8?new List<int>{i+1}:new List<int>() }).ToArray();
            var junction = new PriorityIntersection { exitWaypoints=new List<int>{5} };
            junction.Groups.Add(new IntersectionStopWaypointsIndex { roadWaypoints=new List<int>{3} });
            native[3].Associate(junction); native[3].enter=true; native[3].giveWay=true;
            native[5].Associate(junction); native[5].exit=true;
            var junctions = new List<PriorityIntersection>{junction};
            if (nextJunction)
            {
                var next = new PriorityIntersection { exitWaypoints=new List<int>{8} };
                next.Groups.Add(new IntersectionStopWaypointsIndex { roadWaypoints=new List<int>{7} });
                native[7].Associate(next); native[7].enter=true; native[7].giveWay=true;
                native[8].Associate(next); native[8].exit=true;
                junctions.Add(next);
            }
            setup?.Invoke(native);
            UnityEngine.Object.Scene = new CurrentSceneData { allWaypoints=native, allPriorityIntersections=junctions.ToArray(), allLightsIntersections=Array.Empty<TrafficLightsIntersection>(), roadIds=new int[9] };
            Roads = new RoadRoutes(new PublicRoadAccess());
            int attempts=0;
            while (!Roads.Prepare()) { Check(Roads.IsCurrent, "Fixture changed during preflight"); Check(++attempts<10000, "Preflight stalled"); Thread.Sleep(1); }
        }
        public string Tag(string version,int from,int to) => "native:road:"+version+":"+Roads.TopologyId+":"+from+":"+to;
        public RouteLeg Leg(double from,double to,string version,int a,int b) => new RouteLeg { From=P(from),To=P(to),Mode=TravelMode.Car,Minutes=Math.Max(.001,(to-from)/10),ServiceId=Tag(version,a,b) };
        public CitizenState Journey()
        {
            var legs = new List<RouteLeg> { Leg(0,4,"fragment1",0,1), Leg(4,10,"fragment1",0,1) };
            for(int i=1;i<8;i++) legs.Add(Leg(i*10,(i+1)*10,"v1",i,i+1));
            return new CitizenState { Car=new CarState { Id="car" }, Journey=new Route { Legs=legs } };
        }
        public bool Sample(CitizenState person,int leg,double fraction,out PhysicalRoadSample road)
        {
            var route=person.Journey!; var current=route.Legs[leg];
            double elapsed=route.Legs.Take(leg).Sum(l=>l.Minutes)+fraction*current.Minutes;
            var position=Position.Lerp(current.From,current.To,fraction);
            var sample=new PhysicalJourneySample(elapsed,route.Minutes,leg,fraction,current,position,position,null);
            return Roads.TryPhysicalRoadSample(person,sample,out road);
        }
    }
    public static void Main()
    {
        Guard.MainThread=Environment.CurrentManagedThreadId;
        var f=new Fixture(); var person=f.Journey();
        Check(!f.Sample(person,0,.5,out _),"Current first fragment became physical");
        Check(!f.Sample(person,1,.5,out _),"Current remaining fragment became physical");
        Check(f.Sample(person,2,.5,out var road),"Earlier fragments poisoned later full edge");
        Check(road.SpawnNode==1 && road.RemainingRoadNodes.SequenceEqual(new[]{2,3,4,5,6,7,8}),"Full-edge physical target span changed");
        Check(road.RouteProgress==15,"Historical fragment distances were not retained");
        Check(road.AdmissionBoundaries.Count==2 && road.AdmissionBoundaries[0].Enter && road.AdmissionBoundaries[0].Distance==30 && road.AdmissionBoundaries[1].Exit && road.AdmissionBoundaries[1].Distance==50 && road.AdmissionBoundaries[0].Junction==road.AdmissionBoundaries[1].Junction,"Junction history/identity changed");
        Check(PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Clear approach cannot pass normal admission");
        Check(f.Sample(person,3,.9,out road) && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Near-junction approach bypassed normal admission");
        Check(f.Sample(person,4,.5,out road) && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Inside junction became admissible");
        Check(f.Sample(person,6,.5,out road) && PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Cleared exit lost full-span history");
        foreach(string tag in new[]{"native:road:unknown:"+f.Roads.TopologyId+":0:1","native:road:fragment1:"+f.Roads.TopologyId+":bad:1","native:road:fragment1:other:0:1","native:road:fragment1:"+f.Roads.TopologyId+":0:1:extra"})
        { var bad=f.Journey(); bad.Journey!.Legs[1].ServiceId=tag; Check(!f.Sample(bad,2,.5,out _),"Malformed historical native tag became a boundary: "+tag); }
        var olderUnknown=f.Journey(); olderUnknown.Journey!.Legs[0].ServiceId="native:road:unknown:"+f.Roads.TopologyId+":0:1";
        Check(!f.Sample(olderUnknown,2,.5,out _),"Known fragment hid older unknown native history");
        var geometry=f.Journey(); geometry.Journey!.Legs[1].To=P(12); Check(!f.Sample(geometry,2,.5,out _),"Off-edge fragment geometry became a boundary");
        var reverse=f.Journey(); reverse.Journey!.Legs[1].From=P(10); reverse.Journey.Legs[1].To=P(4); Check(!f.Sample(reverse,2,.5,out _),"Reversed fragment became a boundary");
        var future=f.Journey(); future.Journey!.Legs[8]=f.Leg(70,75,"fragment1",7,8); future.Journey.Legs.Add(f.Leg(75,80,"fragment1",7,8));
        Check(f.Sample(future,2,.5,out road) && road.RemainingRoadNodes.SequenceEqual(new[]{2,3,4,5,6,7}),"Future known fragment poisoned earlier eligible full span");
        var unknownInside=f.Journey(); unknownInside.Journey!.Legs[4].ServiceId="native:road:unknown:"+f.Roads.TopologyId+":3:4";
        Check(!f.Sample(unknownInside,6,.5,out _),"Unknown full-span history was silently skipped");
        var partialInside=f.Journey(); partialInside.Journey!.Legs=new List<RouteLeg>{f.Leg(35,40,"fragment1",3,4),f.Leg(40,50,"v1",4,5),f.Leg(50,60,"v1",5,6),f.Leg(60,70,"v1",6,7)};
        Check(f.Sample(partialInside,1,.5,out road) && road.AdmissionBoundaries.Count==1 && road.AdmissionBoundaries[0].Exit && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Fragment boundary invented missing junction-entry evidence");
        var pairedFragments=f.Journey(); pairedFragments.Journey!.Legs=new List<RouteLeg>{f.Leg(30,35,"fragment1",3,4),f.Leg(35,40,"fragment1",3,4),f.Leg(40,50,"v1",4,5),f.Leg(50,60,"v1",5,6),f.Leg(60,70,"v1",6,7)};
        Check(f.Sample(pairedFragments,3,.5,out road) && road.AdmissionBoundaries.Count==2 && PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Verified entry across fragments was discarded after rear clearance");
        var afterExit=f.Journey(); afterExit.Journey!.Legs=new List<RouteLeg>{f.Leg(30,40,"v1",3,4),f.Leg(40,50,"v1",4,5),f.Leg(50,55,"fragment1",5,6),f.Leg(55,60,"fragment1",5,6),f.Leg(60,70,"v1",6,7),f.Leg(70,80,"v1",7,8)};
        Check(f.Sample(afterExit,4,.01,out road) && road.RemainingRoadNodes.SequenceEqual(new[]{7,8}) && road.AdmissionBoundaries.Count==2 && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,12),"Earlier exit clearance was lost across fragments");
        Check(f.Sample(afterExit,4,.4,out road) && PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,12),"Verified rear clearance after fragments remains permanently blocked");
        var missingPriorEntry=f.Journey(); missingPriorEntry.Journey!.Legs=new List<RouteLeg>{f.Leg(50,55,"fragment1",5,6),f.Leg(55,60,"fragment1",5,6),f.Leg(60,70,"v1",6,7),f.Leg(70,80,"v1",7,8)};
        // A lone leading exit proves outside only after the whole body and margin clear it (PhysicalRoadAdmission).
        Check(f.Sample(missingPriorEntry,2,.9,out road) && road.AdmissionBoundaries.Count==1 && road.AdmissionBoundaries[0].Exit && road.AdmissionBoundaries[0].Distance==0
            && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,4.999,4) && PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Historical exit fragment invented a missing junction entry or skipped rear clearance");
        var nextFixture=new Fixture(true,new double[]{-50,-40,-30,-20,-10,0,2,20,30});
        afterExit.Journey.Legs=new List<RouteLeg>{nextFixture.Leg(-20,-10,"v1",3,4),nextFixture.Leg(-10,0,"v1",4,5),nextFixture.Leg(0,1,"fragment1",5,6),nextFixture.Leg(1,2,"fragment1",5,6),nextFixture.Leg(2,20,"v1",6,7),nextFixture.Leg(20,30,"v1",7,8)};
        Check(nextFixture.Sample(afterExit,4,.1/18,out road) && road.AdmissionBoundaries.Count==4 && Math.Abs(road.RouteProgress-22.1)<1e-9,"Exact two-junction sample did not reach x=2.1");
        Check(road.AdmissionBoundaries.Select(b=>b.Distance).SequenceEqual(new double[]{0,20,40,50}),"Two-junction boundary distances changed");
        Check(!PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Future balanced junction hid insufficient prior-exit rear clearance");
        Check(PhysicalRoadAdmission.Allows(road.AdmissionBoundaries.Skip(2).ToArray(),road.RouteProgress,4),"Future junction independently blocks; regression does not isolate prior-exit clearance");
        Check(road.AdmissionBoundaries[0].Junction==road.AdmissionBoundaries[1].Junction && road.AdmissionBoundaries[2].Junction==road.AdmissionBoundaries[3].Junction && road.AdmissionBoundaries[0].Junction!=road.AdmissionBoundaries[2].Junction,"Distinct junction identities merged across fragment history");
        Check(nextFixture.Sample(afterExit,4,4.0/18,out road) && PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Two-junction sample failed to recover at x=6 after verified rear clearance");
        var nearExit=afterExit;
        nearExit.Journey.Legs[2]=nextFixture.Leg(0,.0005,"fragment1",5,6);
        nearExit.Journey.Legs[3]=nextFixture.Leg(.0005,2,"fragment1",5,6);
        Check(nextFixture.Sample(nearExit,4,.1/18,out road) && road.AdmissionBoundaries.Count==4 && Math.Abs(road.AdmissionBoundaries[1].Distance-20.0005)<1e-9 && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Tiny split after exit duplicated or advanced the exit boundary");
        Check(nextFixture.Sample(nearExit,4,3.0001/18,out road) && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Latest conservative exit distance was discarded near the clearance threshold");
        Check(nextFixture.Sample(nearExit,4,3.0006/18,out road) && PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Tiny split after exit permanently blocks rear clearance");
        var nearEntry=nextFixture.Journey();
        nearEntry.Journey!.Legs=new List<RouteLeg>{nextFixture.Leg(-30,-20.0005,"fragment1",2,3),nextFixture.Leg(-20.0005,-20,"fragment1",2,3),nextFixture.Leg(-20,-10,"v1",3,4),nextFixture.Leg(-10,0,"v1",4,5),nextFixture.Leg(0,2,"v1",5,6),nextFixture.Leg(2,20,"v1",6,7),nextFixture.Leg(20,30,"v1",7,8)};
        Check(nextFixture.Sample(nearEntry,2,.01,out road) && road.AdmissionBoundaries.Count==4 && Math.Abs(road.AdmissionBoundaries[0].Distance-9.9995)<1e-9 && !PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Tiny split before entry duplicated or delayed the entry boundary");
        Check(nextFixture.Sample(nearEntry,5,4.0/18,out road) && road.AdmissionBoundaries.Count==4 && PhysicalRoadAdmission.Allows(road.AdmissionBoundaries,road.RouteProgress,4),"Tiny split before entry permanently blocks later rear clearance");
        SweepRegressions();
        GateRegressions();
        ScopeRegressions();
        Console.WriteLine("PASS actual RoadRoutes: current fragments rejected; later full run sampled; malformed/unknown history rejected; future fragment bounded; junction identities/history/admission preserved.");
    }
    // One physical sample validates each node and junction contract once. Nothing it learns survives into
    // the next sample or a public resolve, though no frame or Update boundary separates these calls.
    private static void ScopeRegressions()
    {
        static string Pt(Position p) => p.X.ToString("R") + "," + p.Y.ToString("R") + "," + p.Z.ToString("R");
        static string Text(PhysicalRoadSample r) => string.Join("|", Pt(r.Position), Pt(r.DirectionTarget), r.SpawnNode.ToString(),
            string.Join(",", r.RemainingRoadNodes), r.TopologyId, r.RouteProgress.ToString("R"),
            string.Join(";", r.AdmissionBoundaries.Select(b => b.Distance.ToString("R") + ":" + b.Enter + ":" + b.Exit + ":" + b.Junction)));
        static int[] Validations(Fixture x) => x.Native.Select(w => w.IndexReads).ToArray();
        static string Delta(Fixture x, int[] before) => string.Join(",", x.Native.Select((w, i) => w.IndexReads - before[i]));
        var f = new Fixture(); var person = f.Journey();
        var junction = (GenericIntersection)f.Native[3].AssociationForTest!;
        Check(f.Sample(person, 2, .5, out var road), "Scope fixture did not sample");
        string reference = Text(road);
        // Leg 2 after two historical fragments touches nodes 0..8; nodes 3 and 5 share one junction.
        int groups = junction.GroupReads; var before = Validations(f);
        Check(f.Sample(person, 2, .5, out road) && Text(road) == reference, "Repeated sample changed");
        Check(junction.GroupReads - groups == 1, "Junction contract validated " + (junction.GroupReads - groups) + " times in one sample");
        Check(Delta(f, before) == "1,1,1,1,1,1,1,1,1", "Nodes not validated exactly once in one sample: " + Delta(f, before));
        groups = junction.GroupReads; before = Validations(f);
        Check(ReferenceEquals(f.Roads.ResolveWaypoint(f.Roads.TopologyId, 3), f.Native[3])
            && ReferenceEquals(f.Roads.ResolveWaypoint(f.Roads.TopologyId, 3), f.Native[3]), "Public resolve refused");
        Check(junction.GroupReads - groups == 2 && f.Native[3].IndexReads - before[3] == 2, "Public resolve reused a validation between calls");
        var two = new Fixture(true); var twoPerson = two.Journey();
        var first = (GenericIntersection)two.Native[3].AssociationForTest!; var second = (GenericIntersection)two.Native[7].AssociationForTest!;
        int firstGroups = first.GroupReads, secondGroups = second.GroupReads; before = Validations(two);
        Check(two.Sample(twoPerson, 2, .5, out road) && road.AdmissionBoundaries.Count == 4, "Two-junction sample failed");
        Check(first.GroupReads - firstGroups == 1 && second.GroupReads - secondGroups == 1 && Delta(two, before) == "1,1,1,1,1,1,1,1,1",
            "Two-junction sample repeated validation: " + Delta(two, before));
        // Historical fragment, entry, interior, exit and final nodes, closed and reopened between samples.
        foreach (int node in new[] { 0, 3, 4, 5, 8 })
        {
            f.Native[node].temporaryDisabled = true;
            Check(!f.Sample(person, 2, .5, out _), "Same-frame closure of node " + node + " missed by the next sample");
            Check(f.Roads.ResolveWaypoint(f.Roads.TopologyId, node) == null, "Public resolve missed closure of node " + node);
            f.Native[node].temporaryDisabled = false;
            Check(f.Sample(person, 2, .5, out road) && Text(road) == reference && f.Roads.IsCurrent, "Same-frame reopening of node " + node + " missed");
        }
        foreach (var mutation in new (string Reason, Action<Fixture> Change)[] {
            ("junction-groups", x => ((GenericIntersection)x.Native[3].AssociationForTest!).Groups[0].roadWaypoints.Add(4)),
            ("junction-exits", x => ((GenericIntersection)x.Native[5].AssociationForTest!).exitWaypoints!.Add(6)),
            ("waypoint-flags", x => x.Native[4].giveWay = true), ("car-restrictions", x => x.Native[6].allowedCars!.Clear()),
            ("waypoint-identity", x => x.Native[1] = new Waypoint()) })
        {
            var x = new Fixture(); var journey = x.Journey();
            Check(x.Sample(journey, 2, .5, out _), "Mutation fixture did not sample");
            mutation.Change(x);
            Check(!x.Sample(journey, 2, .5, out _) && !x.Roads.IsCurrent && x.Roads.InvalidationReason == mutation.Reason,
                "Same-frame " + mutation.Reason + " change missed by the next sample: " + x.Roads.InvalidationReason);
        }
        // A callback inside the sample cannot share its partial scope; a public resolve there stays live.
        bool nested = true; Waypoint? nestedResolve = null; int hooks = 0;
        Guard.OnRead = () => { Guard.OnRead = null; hooks++; nested = f.Sample(person, 2, .5, out _);
            nestedResolve = f.Roads.ResolveWaypoint(f.Roads.TopologyId, 4); f.Native[6].temporaryDisabled = true; };
        try { f.Sample(person, 2, .5, out _); } finally { Guard.OnRead = null; }
        Check(hooks == 1 && !nested && ReferenceEquals(nestedResolve, f.Native[4]), "Reentrant sample shared a partial scope or public resolve was refused");
        Check(!f.Sample(person, 2, .5, out _), "Closure made by a callback inside a sample was missed by the next sample");
        f.Native[6].temporaryDisabled = false;
        Check(f.Sample(person, 2, .5, out road) && Text(road) == reference, "Sample after reentrancy failed");
        // An exception leaves no open scope and no retained validation.
        Guard.FailAtRead = Guard.Reads + 5; bool threw = false;
        try { f.Sample(person, 2, .5, out _); } catch (InvalidOperationException) { threw = true; }
        finally { Guard.FailAtRead = -1; }
        groups = junction.GroupReads; before = Validations(f);
        Check(threw && f.Sample(person, 2, .5, out road) && Text(road) == reference && junction.GroupReads - groups == 1
            && Delta(f, before) == "1,1,1,1,1,1,1,1,1", "Scope or validation survived an exception");
        // Generation wrap cannot revive a stamp from an earlier sample.
        typeof(RoadRoutes).GetField("sampleGeneration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(f.Roads, int.MaxValue - 1);
        for (int i = 0; i < 3; i++)
        {
            Check(f.Sample(person, 2, .5, out road) && Text(road) == reference, "Sample across generation wrap changed");
            f.Native[4].temporaryDisabled = true; Check(!f.Sample(person, 2, .5, out _), "Closure missed across generation wrap");
            f.Native[4].temporaryDisabled = false;
        }
        var speed = new Fixture(); var speedPerson = speed.Journey();
        Check(speed.Sample(speedPerson, 2, .5, out var speedReference), "Speed fixture did not sample");
        speed.Native[4].maxSpeed++; threw = false;
        try { speed.Sample(speedPerson, 2, .5, out _); } catch (InvalidOperationException) { threw = true; }
        speed.Native[4].maxSpeed--;
        Check(threw && speed.Sample(speedPerson, 2, .5, out road) && Text(road) == Text(speedReference), "Unverified speed did not fail, or left a scope open");
        Console.WriteLine("PASS sample scope: each node/junction validated once per sample; public resolve live; same-frame closures, contract changes, reentrancy, exceptions and generation wrap detected.");
    }
    // The periodic refresh keeps a snapshot whose gates are unchanged; only the staged sweep sees nodes no search touches.
    private static void SweepRegressions()
    {
        // Runs the sweep a few nodes at a time; restart=true asks for a new sweep before every slice, as a refresh would.
        static void Sweep(Fixture f, bool restart)
        {
            f.Roads.BeginSweep();
            for (int slice = 0; slice < 40 && f.Roads.IsCurrent; slice++) { if (restart) f.Roads.BeginSweep(); f.Roads.Sweep(1000, 2); }
        }
        var unchanged = new Fixture();
        Sweep(unchanged, false);
        Check(unchanged.Roads.IsCurrent, "Sweep of an unchanged snapshot marked it stale");
        // Captured without car access, so no search ever checks it again.
        var opened = new Fixture(setup: native => native[8].allowedAgents = new List<int>());
        opened.Native[8].allowedAgents = new List<int> { 0 };
        Check(opened.Roads.IsCurrent, "Fixture already stale before the sweep");
        Sweep(opened, false);
        Check(!opened.Roads.IsCurrent, "Sweep missed an excluded node that now allows cars");
        var interrupted = new Fixture(setup: native => native[8].allowedAgents = new List<int>());
        interrupted.Native[8].allowedAgents = new List<int> { 0 };
        Sweep(interrupted, true);
        Check(!interrupted.Roads.IsCurrent, "Refreshes restarted an unfinished sweep before it reached the last node");
        // CaptureNode treats a null neighbour list apart from an empty one; an exit without a junction is valid only with none.
        var neighbours = new Fixture(setup: native => { native[8].neighbors = null; native[8].exit = true; });
        neighbours.Native[8].neighbors = new List<int>();
        Sweep(neighbours, false);
        Check(!neighbours.Roads.IsCurrent, "Sweep treated a null neighbour list as equal to an empty one");
        var stop = new Fixture();
        stop.Native[8].stop = true;
        Sweep(stop, false);
        Check(!stop.Roads.IsCurrent, "Sweep missed a changed stop flag");
        Console.WriteLine("PASS staged snapshot sweep: unchanged snapshot kept; excluded node, interrupted sweep, neighbour nullness and stop flag detected.");
    }
    // Gate boxes: a leaf caught at any pose must neither open a road nor count as a gate change on every refresh.
    private static void GateRegressions()
    {
        static UnityEngine.Vector3 V(float x) => new UnityEngine.Vector3(x, 0, 0);
        static UnityEngine.Collider Part(float centre, float width, UnityEngine.Transform? parent = null)
            => new UnityEngine.Collider { bounds = new UnityEngine.Bounds(V(centre), new UnityEngine.Vector3(width, 2, 1)), transform = new UnityEngine.Transform { parent = parent } };
        // A frame from x -1 to 1 and one leaf: closed it reaches x 2, open it swings out to x 5.
        static FenceDoor Door(string name, bool open, out UnityEngine.Collider leafPart)
        {
            var leaf = new UnityEngine.Transform();
            leafPart = Part(open ? 4.5f : 1.5f, 1, leaf);
            return new FenceDoor { acceptsVehicles = true, name = name, doors = new[] { leaf }, Colliders = new[] { Part(0, 2), leafPart } };
        }
        static void Pose(UnityEngine.Collider leafPart, bool open) => leafPart.bounds = new UnityEngine.Bounds(V(open ? 4.5f : 1.5f), new UnityEngine.Vector3(1, 2, 1));
        var opening = new Position(1.8, 0, -5); var through = new Position(1.8, 0, 5);
        try
        {
            // Captured open, then closed: the closed leaf stays covered and closing is no change.
            var a = Door("a", true, out var aLeaf); UnityEngine.Object.Doors = new[] { a };
            var access = new PublicRoadAccess();
            Pose(aLeaf, false);
            Check(access.Refreshed(out _) == null, "Closing a leaf captured open counted as a gate change");
            Check(!access.ClearSegment(opening, through), "Road through the closed leaf opened after a capture taken open");
            // Captured closed, then opened: the box grows once, then stays.
            var b = Door("b", false, out var bLeaf); UnityEngine.Object.Doors = new[] { b };
            access = new PublicRoadAccess();
            Pose(bLeaf, true);
            var grown = access.Refreshed(out string change);
            Check(grown != null && change.Contains("'b'"), "A leaf reaching a new extreme was not taken into the gate box");
            access = grown!;
            Pose(bLeaf, false);
            Check(access.Refreshed(out _) == null, "Closing again after the box grew counted as a change");
            Pose(bLeaf, true);
            Check(access.Refreshed(out _) == null, "Opening again after the box grew counted as a change");
            // A door replaced by an identical one: the new door is known, so its leaf closing is no change.
            var c = Door("c", true, out _); UnityEngine.Object.Doors = new[] { c };
            access = new PublicRoadAccess();
            var d = Door("c", true, out var dLeaf); UnityEngine.Object.Doors = new[] { d };
            Check(access.Refreshed(out _) == null, "An identical replacement door counted as a gate change");
            Pose(dLeaf, false);
            Check(access.Refreshed(out _) == null, "The replacement door was not recorded, so its leaf closing counted as a change");
            // Moving the frame is a real change.
            d.Colliders[0].bounds = new UnityEngine.Bounds(V(10), new UnityEngine.Vector3(2, 2, 1));
            Check(access.Refreshed(out _) != null, "A moved gate frame was not a change");
            // A sliding leaf seen at both ends, whose boxes do not contain each other: both ends stay blocked and
            // neither end coming back is a change. Keeping only the latest box would fail here.
            var e = Door("e", true, out var eLeaf); UnityEngine.Object.Doors = new[] { e };
            access = new PublicRoadAccess();
            eLeaf.bounds = new UnityEngine.Bounds(V(-4.5f), new UnityEngine.Vector3(1, 2, 1));
            var both = access.Refreshed(out _);
            Check(both != null, "A leaf reaching the far end was not taken into the gate box");
            access = both!;
            Check(!access.ClearSegment(new Position(4.5, 0, -5), new Position(4.5, 0, 5)), "The first leaf end opened after the leaf moved away");
            Check(!access.ClearSegment(new Position(-4.5, 0, -5), new Position(-4.5, 0, 5)), "The second leaf end is not blocked");
            Pose(eLeaf, true);
            Check(access.Refreshed(out _) == null, "The leaf returning to its first end counted as a change");
            eLeaf.bounds = new UnityEngine.Bounds(V(-4.5f), new UnityEngine.Vector3(1, 2, 1));
            Check(access.Refreshed(out _) == null, "The leaf returning to its far end counted as a change");
        }
        finally { UnityEngine.Object.Doors = Array.Empty<FenceDoor>(); }
        Console.WriteLine("PASS public gates: leaf poses neither open a road nor repeat a change; replaced doors are recorded; a moved frame is a change.");
    }
}
