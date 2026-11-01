using System.Diagnostics;
using BigAmbitions.InteriorDesigner.InteriorElements;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

int checks=0;
void Check(bool pass,string label) { checks++; if (!pass) throw new Exception(label); }
GameObject Node(Transform? parent=null) { var n=new GameObject(); parent?.Attach(n.transform); return n; }
var manager = InstanceBehavior<BuildingManager>.Value;
var layout=Node(); manager.currentBuildingVersion=layout.transform;
var exterior=Node(); manager.cityBuildingController=exterior.Add<CityBuildingController>(); manager.cityBuildingController.building=manager.building;
manager.cityBuildingController.entranceDoors=new[] { new BuildingEntranceDoor { doorId=7,doorTransform=exterior.transform } };
var inside=Node(layout.transform); inside.transform.position=new(1000,0,0);
var exit=inside.Add<ExitZone>(); exit.despawner=inside.Add<ExitZoneDespawner>();exit.despawner.exitToZoneId=7;manager.exitZones.Add(exit);
InteriorElementsHelper.InteriorElementsCache.Add(0,Node(layout.transform).Add<Element>());
Camera.main=Node().Add<Camera>();
var scene=new Scene();scene.Roots.Add(layout);scene.Roots.Add(exterior);SceneManager.Scenes.Add(scene);
var runtime=new PopulationRuntime();
int reads=InstanceBehavior<BuildingManager>.Reads;
// Exercise actual facade, including default arguments, with a stale host that would otherwise throw.
var stale=new PopulationRuntime();stale.Stale();
for (int i=0;i<1000;i++) { Check(stale.ReadStreetProxyDoors()==null,"doors disabled");Check(stale.ReadStreetProxyInventory()==null,"inventory disabled"); }
Check(reads==InstanceBehavior<BuildingManager>.Reads,"disabled native reads");
bool threw=false;try { stale.ReadStreetProxyDoors(true); } catch(InvalidOperationException) { threw=true; } Check(threw,"stale host refusal");
long coldBytes=GC.GetAllocatedBytesForCurrentThread(),coldStart=Stopwatch.GetTimestamp();
var d=runtime.ReadStreetProxyDoors(true)!;
double coldDoorMs=Stopwatch.GetElapsedTime(coldStart).TotalMilliseconds;long coldDoorBytes=GC.GetAllocatedBytesForCurrentThread()-coldBytes;
Console.WriteLine($"cold first doors: {coldDoorMs:F6} ms; {coldDoorBytes} B; spike>=30ms={coldDoorMs>=30}");
Check(d.Complete && d.Decision!.UniqueIds && d.Decision.CandidateAvailable && !d.Decision.Consistent,"candidate without edge proof");
Check(d.Decision!.Failure==DoorProbeFailure.InsufficientEdges && !d.RendererAvailable,"closed on missing edge evidence");
manager.exitZones.Add(exit);Check(runtime.ReadStreetProxyDoors(true)!.Decision!.Failure==DoorProbeFailure.Ambiguous,"duplicate ids refuse");manager.exitZones.RemoveAt(1);
exit.despawner.exitToZoneId=8;Check(runtime.ReadStreetProxyDoors(true)!.Decision!.Failure==DoorProbeFailure.Missing,"wrong id refuses");exit.despawner.exitToZoneId=7;
exit.isElevator=true;Check(runtime.ReadStreetProxyDoors(true)!.Decision!.Failure==DoorProbeFailure.Elevator,"elevator refuses");exit.isElevator=false;
exit.despawner=null!;Check(!runtime.ReadStreetProxyDoors(true)!.Complete,"missing despawner refuses");exit.despawner=inside.GetComponent<ExitZoneDespawner>()!;
manager.enteringBuilding=true;Check(!runtime.ReadStreetProxyDoors(true)!.Complete,"transition refuses");manager.enteringBuilding=false;
NativeInteriorGuard.Verdict=new(true,"stand-in");
Check(runtime.ReadStreetProxyDoors(true)!.GuardDetected && !runtime.ReadStreetProxyDoors(true)!.RendererAvailable,"guard cannot activate drawing");NativeInteriorGuard.Verdict=new(false,"fixture");
MeshRenderer MeshNode(int x,bool enabled=true)
{
    var n=Node(exterior.transform);var r=n.Add<MeshRenderer>();r.bounds=new Bounds { min=new(x-1,-1,-1),max=new(x+1,1,1) };
    r.enabled=enabled;n.Add<MeshFilter>().sharedMesh=new Mesh();r.Shared.Add(new Material());return r;
}
var crossing=MeshNode(30,false);crossing.PropertyBlock=true;
manager.cityBuildingController.renderersToFade.Add(crossing);
var nonAncestorLod=Node(layout.transform).Add<LODGroup>();nonAncestorLod.Levels=new[] { new LOD { renderers=new Renderer[] { crossing } } };
var volume=Node(exterior.transform).Add<UnityEngine.Rendering.Volume>();
Node(exterior.transform).Add<Light>();
var owned=new PhysicalPopulation();var person=Node(exterior.transform).Add<ThirdPersonCharacter>();person.navmeshAgent=person.gameObject.Add<UnityEngine.AI.NavMeshAgent>();person.navmeshAgent.velocity=new(1,0,0);owned.Add("pc:1",person);
var car=Node(exterior.transform).Add<GleyTrafficSystem.VehicleComponent>();car.rb=car.gameObject.Add<Rigidbody>();car.rb.velocity=new(1,0,0);owned.Add("pc:2",c:car);runtime.SetBodies(owned);
var traffic=Node(exterior.transform).Add<GleyTrafficSystem.VehicleComponent>();traffic.rb=traffic.gameObject.Add<Rigidbody>();traffic.rb.velocity=new(1,0,0);
coldBytes=GC.GetAllocatedBytesForCurrentThread();coldStart=Stopwatch.GetTimestamp();
var inv=runtime.ReadStreetProxyInventory(true)!;
double coldInventoryMs=Stopwatch.GetElapsedTime(coldStart).TotalMilliseconds;long coldInventoryBytes=GC.GetAllocatedBytesForCurrentThread()-coldBytes;
Console.WriteLine($"cold first inventory (one renderer/3 bodies/light/volume): {coldInventoryMs:F6} ms; {coldInventoryBytes} B; spike>=30ms={coldInventoryMs>=30}");
Check(inv.Complete && inv.Renderers.Count==1,"complete renderer snapshot");
Check(inv.Renderers[0].Radius30==StreetRadiusClass.Crossing && !inv.Renderers[0].Enabled,"disabled crossing renderer included");
Check(inv.Renderers[0].HasPropertyBlock && inv.Renderers[0].OwnerHints.Length>0,"owner and property metadata");
Check(inv.Renderers[0].OwnerMembership.Length==2 && inv.Renderers[0].OwnerMembership.Any(s=>s.EndsWith(":fade"))
    && inv.Renderers[0].OwnerMembership.Any(s=>s.EndsWith(":lod0")),"exact owner references, including nonancestor LOD");
Check(inv.Environment.Count==2 && !inv.StaticMembershipProven,"volume/light metadata without profile getter");
Check(inv.ActiveResidents[0]==1 && inv.MovingResidents[0]==1 && inv.MovingOwnedVehicles[0]==1 && inv.MovingNativeTraffic[0]==1,"owned and native counts distinct");
Check(inv.Bodies.Count==3 && !crossing.enabled && crossing.PropertyBlock && volume.weight==1,"no native mutation");
// A sibling building's base set is the only ownership path for this renderer.
int landmarkChildren=layout.transform.childCount;
var baseOnlyNode=Node(layout.transform);var baseOnly=baseOnlyNode.Add<MeshRenderer>();
baseOnly.bounds=new Bounds { min=new(19,-1,-1),max=new(21,1,1) };baseOnlyNode.Add<MeshFilter>().sharedMesh=new Mesh();
var baseOwner=Node(layout.transform).Add<CityBuildingController>();baseOwner.BaseRenderers=new Renderer[] { baseOnly };
var baseSnapshot=runtime.ReadStreetProxyInventory(true)!;
var baseRow=baseSnapshot.Renderers.Single(r=>r.Instance==baseOnly.GetInstanceID());
Check(baseSnapshot.Complete && baseRow.OwnerHints.Length==0 && baseRow.OwnerMembership.Length==1
    && baseRow.OwnerMembership[0]==$"CityBuildingController#{baseOwner.GetInstanceID()}:lodBase","nonancestor base-only building LOD membership");
void BaseRefusal(string reason)
{
    var refusal=runtime.ReadStreetProxyInventory(true)!;
    Check(!refusal.Complete && !refusal.CountsComplete && refusal.Refusal.Contains(reason),"base renderer refusal: "+reason);
}
baseOwner.BaseRenderers=null!;BaseRefusal("unreadable owner collection");
baseOwner.BaseRenderers=new Renderer[] { null! };BaseRefusal("missing owner renderer reference");
baseOwner.BaseRenderers=Enumerable.Repeat<Renderer>(baseOnly,StreetInventoryBudget.MaximumOwnerReferences+1).ToArray();BaseRefusal("owner reference cap");
baseOwner.BaseRenderers=new Renderer[] { baseOnly };
for(int i=0;i<StreetInventoryBudget.MaximumOwnersPerRenderer;i++)
    Node(layout.transform).Add<CityBuildingController>().BaseRenderers=new Renderer[] { baseOnly };
BaseRefusal("per-renderer owner cap");
layout.transform.Children.RemoveRange(landmarkChildren,layout.transform.childCount-landmarkChildren);
// Adversarial fixtures: unknown scripts and row/node/material/ancestry caps publish partial refusals.
crossing.Shared.AddRange(Enumerable.Range(0,32).Select(_=>new Material()));
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"material cap refuses");crossing.Shared.RemoveRange(1,32);
manager.cityBuildingController.renderersToHide.AddRange(Enumerable.Repeat<Renderer>(crossing,65537));
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"owner reference cap refuses");manager.cityBuildingController.renderersToHide.Clear();
var oldLevels=nonAncestorLod.Levels;nonAncestorLod.Levels=new LOD[9];
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"LOD level cap refuses");nonAncestorLod.Levels=oldLevels;
var missing=Node(exterior.transform);missing.Components.Add(null!);
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"unknown script refuses");exterior.transform.Children.Remove(missing.transform);
var deep=Node(exterior.transform);var cursor=deep.transform;
for(int i=0;i<65;i++)cursor=Node(cursor).transform;
var deepRenderer=cursor.gameObject.Add<MeshRenderer>();deepRenderer.bounds=new Bounds();
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"ancestry cap refuses");exterior.transform.Children.Remove(deep.transform);
int oldChildren=exterior.transform.childCount;
for(int i=0;i<1025;i++)MeshNode(60);
var capped=runtime.ReadStreetProxyInventory(true)!;
Check(!capped.Complete && !capped.CountsComplete && capped.Rows==StreetInventoryBudget.MaximumRows,"row cap never certifies counts");
Measure("inventory row-cap refusal (1025 renderers)",()=> { if(runtime.ReadStreetProxyInventory(true)!.Complete)throw new Exception("cap failed"); },50);
exterior.transform.Children.RemoveRange(oldChildren,exterior.transform.childCount-oldChildren);
var bulk=Node();scene.Roots.Add(bulk);
for(int i=0;i<65536;i++)Node(bulk.transform);
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"node cap refuses");
Measure("inventory node-cap refusal (65536 transforms)",()=> { if(runtime.ReadStreetProxyInventory(true)!.Complete)throw new Exception("cap failed"); },50);
scene.Roots.Remove(bulk);
var manyRoots=new Scene();for(int i=0;i<4097;i++)manyRoots.Roots.Add(new GameObject());SceneManager.Scenes.Add(manyRoots);
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"root cap refuses");SceneManager.Scenes.Remove(manyRoots);
var crowded=Node(exterior.transform);for(int i=0;i<65;i++)crowded.Add<Behaviour>();
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"component cap refuses");exterior.transform.Children.Remove(crowded.transform);
for(int i=0;i<64;i++)SceneManager.Scenes.Add(new Scene());
Check(!runtime.ReadStreetProxyInventory(true)!.Complete,"scene cap refuses");SceneManager.Scenes.RemoveRange(1,64);

// Warm actual commands and measure synchronous per-call cost; output formatting is excluded.
for(int i=0;i<128;i++)MeshNode(60);
void Measure(string label,Action action,int count,bool requireZeroBytes=false)
{
    for(int i=0;i<20;i++)action();
    var times=new double[count];var bytes=new long[count];int spikes=0;
    for(int i=0;i<count;i++) { long b=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();action();times[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;bytes[i]=GC.GetAllocatedBytesForCurrentThread()-b;if(times[i]>=30)spikes++; }
    Array.Sort(times);
    if(requireZeroBytes && bytes.Any(b=>b!=0))throw new Exception("disabled command allocated");
    Console.WriteLine($"{label}: p50={times[count/2]:F6} p95={times[(int)Math.Ceiling(count*.95)-1]:F6} max={times[^1]:F6} ms; meanBytes={bytes.Average():F2}; spikes>=30ms={spikes}; calls={count}");
}
Measure("doors (one pair/no edges)",()=> { if(!runtime.ReadStreetProxyDoors(true)!.Complete)throw new Exception("benchmark doors refused"); },300);
Measure("inventory (129 renderers/3 bodies/light/volume)",()=> { if(!runtime.ReadStreetProxyInventory(true)!.Complete)throw new Exception("benchmark inventory refused"); },300);
reads=InstanceBehavior<BuildingManager>.Reads;
Measure("disabled 1000 calls of each command",()=> { for(int i=0;i<1000;i++) { stale.ReadStreetProxyDoors();stale.ReadStreetProxyInventory(); } },300,true);
Check(reads==InstanceBehavior<BuildingManager>.Reads,"disabled benchmark performs no native reads");
Console.WriteLine($"PASS: {checks} actual-source diagnostic checks. Native/Unity doubles, no game/render/native-driver/serialization time included.");
