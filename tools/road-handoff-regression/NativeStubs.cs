using System;
using System.Collections.Generic;
using PersistentCity.Core;
namespace StagingHarness
{
    public static class Guard
    {
        public static int MainThread = Environment.CurrentManagedThreadId;
        public static long Reads;
        public static long FailAtRead = -1;
        public static void Read()
        {
            if (Environment.CurrentManagedThreadId != MainThread) throw new InvalidOperationException("Native read from worker");
            Reads++;
            if (Reads == FailAtRead) throw new InvalidOperationException("Injected native read failure");
        }
    }
}
namespace GleyTrafficSystem
{
    public interface IIntersection { }
    public enum VehicleTypes { Car }
    public sealed class Waypoint
    {
        private IIntersection? associatedIntersection;
        public void Associate(IIntersection? value) { associatedIntersection = value; }
        public IIntersection? AssociationForTest => associatedIntersection;
        public UnityEngine.Vector3 position;
        public int maxSpeed = 30;
        public bool temporaryDisabled;
        private int index;
        public int listIndex { get { StagingHarness.Guard.Read(); return index; } set { index = value; } }
        private bool entering, exiting, yielding, stopping;
        public bool enter { get { StagingHarness.Guard.Read(); return entering; } set { entering = value; } }
        public bool exit { get { StagingHarness.Guard.Read(); return exiting; } set { exiting = value; } }
        public bool giveWay { get { StagingHarness.Guard.Read(); return yielding; } set { yielding = value; } }
        public bool stop { get { StagingHarness.Guard.Read(); return stopping; } set { stopping = value; } }
        public List<int>? allowedAgents = new List<int> { 0 };
        public List<VehicleTypes>? allowedCars = new List<VehicleTypes> { VehicleTypes.Car };
        public List<int>? neighbors = new List<int>();
        private readonly List<int> lanes = new List<int>();
        public List<int>? otherLanes { get { StagingHarness.Guard.Read(); return lanes; } }
    }
    public sealed class IntersectionStopWaypointsIndex { public List<int> roadWaypoints = new List<int>(); }
    public class GenericIntersection : IIntersection
    {
        public string name = "fixture";
        public List<int>? exitWaypoints = new List<int>();
        public List<IntersectionStopWaypointsIndex> Groups = new List<IntersectionStopWaypointsIndex>();
        public int GroupReads;
        public virtual List<IntersectionStopWaypointsIndex> GetWaypoints() { StagingHarness.Guard.Read(); GroupReads++; return Groups; }
    }
    public sealed class PriorityIntersection : GenericIntersection { }
    public sealed class TrafficLightsIntersection : GenericIntersection { public float greenLightTime = 20, yellowLightTime = 10; }
}
namespace GleyUrbanAssets
{
    public sealed class CurrentSceneData : UnityEngine.Object
    {
        public GleyTrafficSystem.Waypoint[] allWaypoints = Array.Empty<GleyTrafficSystem.Waypoint>();
        public GleyTrafficSystem.PriorityIntersection[]? allPriorityIntersections;
        public GleyTrafficSystem.TrafficLightsIntersection[]? allLightsIntersections;
        public int[]? roadIds;
    }
}
