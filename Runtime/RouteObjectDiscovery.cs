using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PersistentCity.Runtime
{
    /// <summary>Save-scoped, main-thread discovery. Walks loaded hierarchies (including inactive objects)
    /// in small slices instead of periodically blocking on Unity's global object search.</summary>
    internal sealed class RouteObjectDiscovery : IRouteObjectSource, IDisposable
    {
        private const int MaximumSteps = 2048;
        private const double BudgetMilliseconds = 1;
        private const double PausedInitializationBudgetMilliseconds = 16;
        // Do not fragment branches merely because the last native call crosses the 1 ms
        // scheduling budget. Native testing showed that doing so triples traversal work.
        private const double SplitThresholdMilliseconds = 4;
        // A transient native stall must not permanently fragment the whole city over a long
        // session. Hints are only an optimization; correctness never depends on retaining one.
        private const int MaximumSplitHints = 256;
        private GameObject anchor;
        private readonly List<Scene> scenes = new List<Scene>();
        private readonly List<GameObject> roots = new List<GameObject>();
        private readonly List<FenceDoor> foundDoors = new List<FenceDoor>();
        private readonly List<SubwayStation> foundStations = new List<SubwayStation>();
        private readonly HashSet<Transform> split = new HashSet<Transform>();
        private readonly Stack<Cursor> pending = new Stack<Cursor>();
        private readonly Stack<List<Transform>> childLists = new Stack<List<Transform>>();
        private readonly Dictionary<int, FenceDoor> doors = new Dictionary<int, FenceDoor>();
        private readonly Dictionary<int, SubwayStation> stations = new Dictionary<int, SubwayStation>();
        private readonly List<int> removed = new List<int>();
        private int sceneIndex, rootIndex;
        private bool scanning, disposed;
        private bool restartRequested = true, membershipChanged = true;
        private float nextSweep, sweepStarted;
        private double sweepMilliseconds;
        private long sweepVisits;
        private struct Cursor
        {
            public Transform Node;
            public int Child;
            public int Depth;
            public List<Transform>? Children;
            public Cursor(Transform node, int depth) { Node = node; Child = -1; Depth = depth; Children = null; }
        }
        public RouteObjectDiscovery()
        {
            // Unity omits this special scene from SceneManager's scene list. Own one harmless
            // anchor so its roots are included too; never change the lifetime of a game object.
            anchor = CreateAnchor();
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }
        public bool Ready { get; private set; }
        public int Revision { get; private set; }
        public int Sweeps { get; private set; }
        public long Visited { get; private set; }
        public double MaximumSliceMilliseconds { get; private set; }
        public double LastSweepSeconds { get; private set; }
        public double LastSweepMilliseconds { get; private set; }
        public long LastSweepBranchCaptures { get; private set; }
        public int SplitBranches => split.Count;
        public FenceDoor[] Doors { get; private set; } = Array.Empty<FenceDoor>();
        public SubwayStation[] Stations { get; private set; } = Array.Empty<SubwayStation>();

        public void Reset()
        {
            ClearPending(); childLists.Clear(); roots.Clear(); scenes.Clear(); foundDoors.Clear(); foundStations.Clear(); split.Clear(); doors.Clear(); stations.Clear();
            Doors = Array.Empty<FenceDoor>(); Stations = Array.Empty<SubwayStation>();
            Ready = scanning = false; nextSweep = 0; restartRequested = membershipChanged = true; Revision++;
        }

        /// <summary>Keep population preflight closed until a fresh, complete inventory is available.</summary>
        public void RequestSweep() { Ready = false; restartRequested = membershipChanged = true; }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) => RequestSweep();
        private void SceneUnloaded(Scene scene) => RequestSweep();

        public void Step(bool preparingPausedCity = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RouteObjectDiscovery));
            long started = Stopwatch.GetTimestamp();
            // Before the first complete inventory, population processing is fenced.
            // A slow rendered load should not stretch this preparation over hundreds
            // of frames. Keep the same hard work bound and normal recurring budget.
            double budget = preparingPausedCity && Sweeps == 0 ? PausedInitializationBudgetMilliseconds : BudgetMilliseconds;
            // A removal can be one half of a replacement. Do not expose a temporarily empty
            // gate set: fence readiness and rediscover before publishing either side.
            if (Prune(doors) | Prune(stations)) RequestSweep();
            bool changed = false;
            if (restartRequested || (!scanning && Time.unscaledTime >= nextSweep)) { restartRequested = false; Begin(); }
            bool worked = scanning;
            for (int step = 0; scanning && step < MaximumSteps; step++)
            {
                if (step > 0 && Elapsed(started) >= budget) break;
                if (pending.Count == 0)
                {
                    if (rootIndex < roots.Count)
                    {
                        var root = roots[rootIndex++];
                        // Split the top two levels even on the first sweep; large city roots
                        // commonly contain most of the world below them.
                        if (root != null) pending.Push(new Cursor(root.transform, 2));
                    }
                    else if (sceneIndex < scenes.Count)
                    {
                        var scene = scenes[sceneIndex++];
                        roots.Clear(); rootIndex = 0;
                        if (scene.IsValid() && scene.isLoaded)
                        {
                            // Root enumeration is native and cannot be preempted. Reuse capacity and
                            // include its cost in slice diagnostics; no recursive native calls here.
                            if (roots.Capacity < scene.rootCount) roots.Capacity = scene.rootCount;
                            scene.GetRootGameObjects(roots);
                        }
                    }
                    else
                    {
                        scanning = false; Sweeps++;
                        LastSweepSeconds = Time.unscaledTime - sweepStarted;
                        roots.Clear(); scenes.Clear(); foundDoors.Clear(); foundStations.Clear();
                    }
                    continue;
                }
                var cursor = pending.Pop();
                if (cursor.Child < 0)
                {
                    if (cursor.Node == null) continue;
                    bool descend = cursor.Depth > 0 || split.Contains(cursor.Node);
                    long queryStarted = Stopwatch.GetTimestamp();
                    if (descend)
                    {
                        cursor.Node.GetComponents(foundDoors);
                        cursor.Node.GetComponents(foundStations);
                    }
                    else
                    {
                        // Native traversal is substantially cheaper than crossing the managed/native
                        // boundary for every irrelevant transform. Slow branches are split further
                        // on the next sweep. A single native call remains a soft-budget operation.
                        cursor.Node.GetComponentsInChildren(true, foundDoors);
                        cursor.Node.GetComponentsInChildren(true, foundStations);
                        if (split.Count < MaximumSplitHints && Elapsed(queryStarted) > SplitThresholdMilliseconds && cursor.Node.childCount > 0)
                            split.Add(cursor.Node);
                    }
                    Visited++;
                    foreach (var door in foundDoors) if (Included(door)) changed |= Add(doors, door);
                    foreach (var station in foundStations) if (Included(station)) changed |= Add(stations, station);
                    foundDoors.Clear(); foundStations.Clear();
                    if (!descend) continue;
                    // Snapshot siblings in this frame. Live child indices across frames can skip
                    // an entire unseen gate subtree when an earlier sibling is destroyed/moved.
                    // Copying one parent's children is another soft-budget operation; lists are
                    // reused with bounded retained capacity. Do not recursively copy descendants.
                    cursor.Children = childLists.Count == 0 ? new List<Transform>() : childLists.Pop();
                    int children = cursor.Node.childCount;
                    for (int i = 0; i < children; i++) cursor.Children.Add(cursor.Node.GetChild(i));
                    cursor.Child = 0;
                }
                // References stay valid across sibling reorder/removal. Visit a reparented live
                // child even if its original parent was destroyed after this snapshot.
                if (cursor.Child < cursor.Children!.Count)
                {
                    var child = cursor.Children[cursor.Child++];
                    pending.Push(cursor);
                    if (child != null) pending.Push(new Cursor(child, Math.Max(0, cursor.Depth - 1)));
                }
                else Recycle(cursor.Children);
            }
            membershipChanged |= changed;
            if (changed && scanning) Ready = false;
            if (!scanning && worked)
            {
                // A slow city must still get an idle interval between complete sweeps.
                nextSweep = Time.unscaledTime + 30;
                // Publish one complete generation, never a different subset on each slice.
                // A synchronous invalidation during the final native read wins over completion.
                if (!restartRequested && membershipChanged)
                {
                    Doors = new FenceDoor[doors.Count]; doors.Values.CopyTo(Doors, 0);
                    Stations = new SubwayStation[stations.Count]; stations.Values.CopyTo(Stations, 0);
                    Revision++; membershipChanged = false;
                }
                if (!restartRequested) Ready = true;
            }
            double elapsed = Elapsed(started);
            MaximumSliceMilliseconds = Math.Max(MaximumSliceMilliseconds, elapsed);
            if (worked)
            {
                sweepMilliseconds += elapsed;
                if (!scanning) { LastSweepMilliseconds = sweepMilliseconds; LastSweepBranchCaptures = Visited - sweepVisits; }
            }
        }

        private void Begin()
        {
            scanning = true; sweepStarted = Time.unscaledTime;
            sweepMilliseconds = 0; sweepVisits = Visited;
            sceneIndex = rootIndex = 0; roots.Clear(); scenes.Clear(); ClearPending();
            split.RemoveWhere(node => node == null || !node.gameObject.scene.isLoaded);
            for (int i = 0; i < SceneManager.sceneCount; i++) scenes.Add(SceneManager.GetSceneAt(i));
            if (anchor == null) anchor = CreateAnchor();
            var persistent = anchor.scene;
            if (persistent.IsValid() && persistent.isLoaded && !scenes.Contains(persistent)) scenes.Add(persistent);
        }
        private static bool Included(Component component) => component != null
            && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded
            // Native parity fixtures: DontSaveInBuild and DontUnloadUnusedAsset alone are
            // still returned by FindObjectsByType. DontSaveInEditor is its exclusion bit.
            && (component.hideFlags & HideFlags.DontSaveInEditor) == 0
            && (component.gameObject.hideFlags & HideFlags.DontSaveInEditor) == 0;
        private static GameObject CreateAnchor()
        {
            var value = new GameObject("Persistent City discovery anchor") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(value); return value;
        }
        private static bool Add<T>(Dictionary<int, T> cache, T value) where T : Component
        {
            int id = value.GetInstanceID();
            if (cache.TryGetValue(id, out var previous) && ReferenceEquals(previous, value)) return false;
            cache[id] = value; return true;
        }
        private bool Prune<T>(Dictionary<int, T> cache) where T : Component
        {
            removed.Clear();
            foreach (var pair in cache) if (!Included(pair.Value)) removed.Add(pair.Key);
            foreach (int id in removed) cache.Remove(id);
            return removed.Count != 0;
        }
        private static double Elapsed(long started) => (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        private void Recycle(List<Transform> children)
        {
            children.Clear();
            if (childLists.Count < 32 && children.Capacity <= 4096) childLists.Push(children);
        }
        private void ClearPending()
        {
            while (pending.Count > 0) { var cursor = pending.Pop(); if (cursor.Children != null) Recycle(cursor.Children); }
        }
        public void Dispose()
        {
            if (disposed) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            disposed = true; Reset();
            if (anchor != null) Object.Destroy(anchor);
        }
    }
}
