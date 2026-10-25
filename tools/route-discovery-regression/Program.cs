using System;
using System.Linq;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
internal static class Program
{
    static int checks;
    static void Check(bool condition,string reason){checks++;if(!condition)throw new Exception(reason);}
    static void Step(RouteObjectDiscovery source,bool preparingPausedCity=false)
    {
        int before=GameObject.ComponentReads;source.Step(preparingPausedCity);
        Check(GameObject.ComponentReads-before<=4096,"unbounded branch queries in one slice");Time.unscaledTime+=.04f;
    }
    static void Sweep(RouteObjectDiscovery source)
    {
        int before=source.Sweeps;Time.unscaledTime+=31;
        for(int i=0;i<10000&&source.Sweeps==before;i++)Step(source);
        Check(source.Sweeps==before+1,"sweep did not finish");
    }
    static void Main()
    {
        SceneManager.Reset();
        var root=new GameObject();root.activeSelf=false;
        var gate=root.Add<FenceDoor>();var duplicate=root.Add<FenceDoor>();var station=root.Add<SubwayStation>();
        var child=new GameObject();child.transform.SetParent(root.transform);var privateGate=child.Add<HamptonsPrivateFenceDoor>();
        var persistent=new GameObject();Object.DontDestroyOnLoad(persistent);var persistentGate=persistent.Add<FenceDoor>();
        var asset=new GameObject();asset.Move(default);asset.Add<FenceDoor>();
        var hidden=new GameObject().Add<FenceDoor>();hidden.hideFlags=HideFlags.DontSave;
        var hierarchyHidden=new GameObject().Add<FenceDoor>();hierarchyHidden.hideFlags=HideFlags.HideInHierarchy;
        using(var source=new RouteObjectDiscovery())
        {
            Check(!source.Ready,"initial inventory published before full discovery");Sweep(source);
            Check(source.Ready,"initial inventory never became ready");
            Check(source.Doors.Length==5&&source.Doors.Contains(gate)&&source.Doors.Contains(duplicate)&&source.Doors.Contains(privateGate)&&source.Doors.Contains(persistentGate)&&source.Doors.Contains(hierarchyHidden),"inactive, multiple, private, persistent or hidden-in-hierarchy doors lost");
            Check(source.Stations.SequenceEqual(new[]{station}),"inactive station lost");
            var original=source.Doors;int revision=source.Revision;Sweep(source);
            Check(ReferenceEquals(original,source.Doors)&&source.Revision==revision,"unchanged inventory republished/allocated");
            var late=new GameObject();late.activeSelf=false;var added=late.Add<FenceDoor>();Sweep(source);
            Check(source.Doors.Contains(added),"new inactive root not discovered");
            var lateStation=child.Add<SubwayStation>();Sweep(source);Check(source.Stations.Contains(lateStation),"new component on known object missed");
            Object.Destroy(added);Step(source);Check(!source.Ready||!source.Doors.Contains(added),"destroyed component published as ready");Sweep(source);Check(!source.Doors.Contains(added),"destroyed component retained after reconciliation");
            hidden.hideFlags=HideFlags.None;Sweep(source);Check(source.Doors.Contains(hidden),"newly eligible component missed");
            hidden.hideFlags=HideFlags.DontSave;Step(source);Check(!source.Ready||!source.Doors.Contains(hidden),"ineligible component published ready");Sweep(source);
            var scene=SceneManager.New();var extra=new GameObject();extra.Move(scene);var extraGate=extra.Add<FenceDoor>();Sweep(source);Check(source.Doors.Contains(extraGate),"new additive scene missed");
            SceneManager.Unload(scene);Check(!source.Ready,"scene unload did not fence readiness");Sweep(source);Check(!source.Doors.Contains(extraGate),"unloaded scene retained");
            scene.Data!.Loaded=true;Sweep(source);Check(source.Doors.Contains(extraGate),"reloaded scene missed");
            child.transform.SetParent(null);Sweep(source);Check(source.Doors.Contains(privateGate),"reparented live gate removed");
            Object.Destroy(gate);var replacement=root.Add<FenceDoor>();replacement.Id=gate.Id;Sweep(source);
            Check(source.Doors.Contains(replacement)&&!source.Doors.Any(x=>ReferenceEquals(x,gate)),"instance ID reuse kept dead wrapper");
            source.Reset();Check(!source.Ready&&source.Doors.Length==0&&source.Stations.Length==0,"save reset retained objects/readiness");Sweep(source);Check(source.Doors.Contains(replacement),"reset could not rediscover");
            source.Dispose();source.Dispose();bool rejected=false;try{source.Step();}catch(ObjectDisposedException){rejected=true;}
            Check(rejected&&SceneManager.Persistent.Data!.Roots.Count==1,"disposal leaked anchor or accepted work");
        }
        // Large, wide hierarchies must yield before traversing a root; a disappearing cursor
        // and hierarchy mutation between slices must not corrupt or falsely remove live records.
        SceneManager.Reset();root=new GameObject();
        for(int i=0;i<15000;i++){var go=new GameObject();go.transform.SetParent(root.transform);}
        var end=root.transform.Children.Last().gameObject.Add<FenceDoor>();
        using(var source=new RouteObjectDiscovery())
        {
            Step(source);Check(!source.Ready,"wide hierarchy traversed without yielding");
            root.transform.Children[100].gameObject.Add<SubwayStation>();
            Sweep(source);Check(source.Doors.Contains(end),"wide tree lost tail");
            int count=source.Doors.Length;end.transform.SetParent(null);Sweep(source);
            Check(source.Doors.Length==count&&source.Doors.Contains(end),"reparenting removed live cached object");
            Object.Destroy(root);Sweep(source);Check(source.Doors.Contains(end),"destroying old parent removed moved gate");
        }
        // Destroy/recreate a gate between slices: never publish the temporary empty set,
        // and publish all new gates in one generation after a full reconciliation.
        SceneManager.Reset();root=new GameObject();
        for(int i=0;i<12000;i++){var go=new GameObject();go.transform.SetParent(root.transform);}
        var replaced=root.transform.Children[0].gameObject.Add<FenceDoor>();
        using(var source=new RouteObjectDiscovery())
        {
            Sweep(source);var old=source.Doors;int revision=source.Revision;
            Object.Destroy(replaced);var replacement=root.transform.Children.Last().gameObject.Add<FenceDoor>();
            Step(source);Check(!source.Ready&&source.Revision==revision&&ReferenceEquals(old,source.Doors),"removal published ahead of replacement");
            Sweep(source);Check(source.Ready&&source.Revision==revision+1&&source.Doors.SequenceEqual(new[]{replacement}),"replacement did not publish atomically");
            var scene=SceneManager.New();Check(!source.Ready,"additive scene did not fence readiness");
            var elsewhere=new GameObject();elsewhere.Move(scene);var addedElsewhere=elsewhere.Add<SubwayStation>();Sweep(source);
            Check(source.Stations.Contains(addedElsewhere),"new scene generation missed new station");
            source.RequestSweep();Check(!source.Ready,"explicit refresh kept old readiness");Sweep(source);Check(source.Ready,"explicit refresh did not finish");
        }
        // Unity removes destroyed children from live sibling indices. The very first
        // published inventory must still include the following child's unseen gate subtree.
        SceneManager.Reset();root=new GameObject();
        for(int i=0;i<10000;i++){var go=new GameObject();go.transform.SetParent(root.transform);}
        var following=root.transform.Children[1].gameObject.Add<FenceDoor>();var first=root.transform.Children[0].gameObject;
        Transform.SlowDirect.Add(root.transform);Transform.DirectQueries.Clear();
        using(var source=new RouteObjectDiscovery())
        {
            for(int i=0;i<100&&!Transform.DirectQueries.Contains(root.transform);i++)Step(source);
            Check(!source.Ready&&Transform.DirectQueries.Contains(root.transform),"fixture did not interrupt descended parent");
            Transform.SlowDirect.Clear();Object.Destroy(first);Sweep(source);
            Check(source.Doors.Contains(following),"destroying earlier sibling skipped unseen gate in first generation");
        }
        // Native parity: individual build/unload flags remain discoverable; editor-dont-save
        // on either a component or its GameObject excludes that object.
        SceneManager.Reset();
        var build=new GameObject().Add<FenceDoor>();build.hideFlags=HideFlags.DontSaveInBuild;
        var unload=new GameObject().Add<FenceDoor>();unload.hideFlags=HideFlags.DontUnloadUnusedAsset;
        var editor=new GameObject().Add<FenceDoor>();editor.hideFlags=HideFlags.DontSaveInEditor;
        var goHidden=new GameObject();goHidden.hideFlags=HideFlags.DontSaveInEditor;goHidden.Add<FenceDoor>();
        using(var source=new RouteObjectDiscovery()){Sweep(source);Check(source.Doors.Length==2&&source.Doors.Contains(build)&&source.Doors.Contains(unload),"hide flags disagree with native fixture");}
        // Deep hierarchies use an explicit scheduling stack; each native subtree operation
        // has a soft time budget and cannot itself be preempted.
        SceneManager.Reset();root=new GameObject();var parent=root.transform;
        for(int i=0;i<12000;i++){var go=new GameObject();go.transform.SetParent(parent);parent=go.transform;}
        var deep=parent.gameObject.Add<SubwayStation>();
        using(var source=new RouteObjectDiscovery()){Sweep(source);Check(source.Stations.Contains(deep),"deep hierarchy not discovered");}
        // Simulate repeatable native stalls. Slow branches must split on the next pass,
        // but transient stalls cannot grow the retained hint set without limit over time.
        SceneManager.Reset();root=new GameObject();var container=new GameObject();container.transform.SetParent(root.transform);
        var branches=new System.Collections.Generic.List<Transform>();
        for(int i=0;i<280;i++)
        {
            var branch=new GameObject();branch.transform.SetParent(container.transform);
            var leaf=new GameObject();leaf.transform.SetParent(branch.transform);leaf.Add<FenceDoor>();
            branches.Add(branch.transform);Transform.Slow.Add(branch.transform);
        }
        using(var source=new RouteObjectDiscovery())
        {
            Sweep(source);Check(source.SplitBranches==256,"adaptive hint cache did not remain capped");
            Check(source.Doors.Length==280,"capping hints lost discovered doors");
            Transform.Slow.Clear();Transform.RecursiveQueries.Clear();Sweep(source);
            Check(!Transform.RecursiveQueries.Contains(branches[0]),"slow branch was queried recursively again");
            Check(Transform.RecursiveQueries.Contains(branches.Last()),"uncached branch skipped instead of safely searched");
            Check(source.Doors.Length==280&&source.LastSweepBranchCaptures>0&&source.LastSweepMilliseconds>=0,"split traversal lost objects or diagnostics");
            Object.Destroy(branches[0].gameObject);Sweep(source);Check(source.SplitBranches<=255,"destroyed split hint retained");
        }
        // Even a sweep lasting longer than the polling interval gets a full idle gap;
        // explicit topology invalidation must still bypass that deadline.
        SceneManager.Reset();root=new GameObject();
        for(int i=0;i<10000;i++){var go=new GameObject();go.transform.SetParent(root.transform);}
        using(var source=new RouteObjectDiscovery())
        {
            for(int i=0;i<10000&&source.Sweeps==0;i++){Time.unscaledTime+=20;Step(source);}
            Check(source.Ready&&source.LastSweepSeconds>30,"fixture did not exceed polling interval");
            int sweeps=source.Sweeps,reads=GameObject.ComponentReads;float completed=Time.unscaledTime;
            Step(source);Time.unscaledTime=completed+29;Step(source);
            Check(source.Sweeps==sweeps&&GameObject.ComponentReads==reads,"long sweep ran again without idle interval");
            Time.unscaledTime=completed+31;Step(source);Check(GameObject.ComponentReads>reads,"periodic sweep did not resume after idle interval");
            Sweep(source);reads=GameObject.ComponentReads;source.RequestSweep();Step(source);
            Check(GameObject.ComponentReads>reads&&!source.Ready,"explicit invalidation did not bypass idle interval");
        }
        // Defensive reentrant-callback fixture. A finite burst of invalidations during
        // the last scene read must not publish a stale generation; recover after it stops.
        SceneManager.Reset();
        using(var source=new RouteObjectDiscovery())
        {
            int events=0;var additions=new System.Collections.Generic.List<FenceDoor>();
            SceneManager.RootsRead=scene=>
            {
                if(!ReferenceEquals(scene.Data,SceneManager.Persistent.Data)||events==3)return;
                events++;var addedScene=SceneManager.New();var addedObject=new GameObject();addedObject.Move(addedScene);additions.Add(addedObject.Add<FenceDoor>());
            };
            for(int i=0;i<10000&&events<3;i++)
            {
                Step(source);Check(!source.Ready&&source.Revision==0&&source.Doors.Length==0,"reentrant invalidation published incomplete inventory");
            }
            Check(events==3,"reentrant fixture did not run");
            SceneManager.RootsRead=null;Sweep(source);
            Check(source.Ready&&source.Revision==1&&additions.All(source.Doors.Contains),"finite invalidation burst did not recover atomically");
        }
        // The larger first-pass budget during a paused load cannot change the published
        // inventory, bypass atomic readiness, or remove the hard branch-query bound.
        SceneManager.Reset();root=new GameObject();
        for(int i=0;i<15000;i++)
        {
            var go=new GameObject();go.transform.SetParent(root.transform);
            if(i%1000==0)go.Add<FenceDoor>();
            if(i%2300==0)go.Add<SubwayStation>();
        }
        using(var ordinary=new RouteObjectDiscovery())using(var paused=new RouteObjectDiscovery())
        {
            Sweep(ordinary);
            for(int i=0;i<10000&&!paused.Ready;i++)
            {
                Check(paused.Doors.Length==0&&paused.Stations.Length==0,"partial paused inventory published");
                Step(paused,true);
            }
            Check(paused.Ready&&paused.Sweeps==1,"paused preparation failed to finish");
            Check(ordinary.Doors.OrderBy(d=>d.Id).SequenceEqual(paused.Doors.OrderBy(d=>d.Id)),"paused budget changed doors");
            Check(ordinary.Stations.OrderBy(s=>s.Id).SequenceEqual(paused.Stations.OrderBy(s=>s.Id)),"paused budget changed stations");
            var prior=paused.Doors;int revision=paused.Revision;
            Sweep(paused);Check(ReferenceEquals(prior,paused.Doors)&&paused.Revision==revision,"normal sweep after paused load changed inventory");
        }
        Console.WriteLine("PASS "+checks+" discovery assertions, including bounded branch scheduling through wide/deep hierarchies, inactive additions, mutation, persistent/additive scenes, paused initialization and disposal.");
    }
}
