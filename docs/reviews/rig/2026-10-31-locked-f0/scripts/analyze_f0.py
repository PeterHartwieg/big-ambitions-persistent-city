from pathlib import Path
import csv,json,math,gzip,hashlib,sys
S=Path(__file__).parent
def read(label):return json.loads((S/(label+'.json')).read_text(encoding='utf-8-sig'))
def dist(v):
 v=sorted(v);n=len(v)
 return {'samples':n,'p50':v[min(n-1,int(n*.5))],'p95':v[min(n-1,int(n*.95))],'maximum':v[-1],'mean':sum(v)/n}
windows=[]
for label in ['base-A','cand-off','base-B','cand-on']:
 if not (S/(label+'-stop.json')).exists():continue
 r=read(label+'-stop');valid=read(label+'-validity')
 mf=list(S.glob('frames-mod-work-'+label+'-stop-*.csv'));wf=list(S.glob('frames-observations-'+label+'-stop-*.csv'));assert len(mf)==len(wf)==1
 modrows=list(csv.DictReader(mf[0].open()));wallrows=list(csv.DictReader(wf[0].open()));freq=r['rawObservations']['stopwatchFrequency']
 hookNames=['PhysicalTrafficHooks.FixedStepPrefix','PhysicalTrafficHooks.FixedStepPostfix','PhysicalTrafficHooks.FixedStepFinalizer','PhysicalVehicles.DeferNativeForce','PhysicalVehicles.ApplyNativeRotation','PhysicalIntersections.UpdatePostfix']
 for component in r['components']:
  if component['name'] in hookNames:assert component['calls']==component['outsidePrimaryScopeCalls'],component
 primaryGui=[float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs']) for x in modrows]
 whole=[(int(x['endTicks'])-int(x['startTicks']))*1000/freq for x in wallrows];mod=[v+sum(float(row[n+'InclusiveMs']) for n in hookNames) for row,v in zip(modrows,primaryGui)]
 errors={k:r.get(k) for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls','droppedModFrames','activeReportCount']}
 assert valid['clean'] and r['realSeconds']>=60 and not any(errors.values())
 assert len(whole)==r['frameMilliseconds']['samples'] and len(mod)==r['completedUnityFrames']
 assert math.isclose(dist(whole)['p95'],r['frameMilliseconds']['p95'],abs_tol=1e-8)
 spikes=[]
 for row,v in zip(modrows,mod):
  if v>=30:
   components={k:float(x) for k,x in row.items() if k.endswith('Ms') and x and float(x)>0 and k not in ['combinedUnionMs']}
   spikes.append({'unityFrame':int(row['unityFrame']),'modMs':v,'entryBranch':row['entryBranch'],'components':components})
 load=read(label+'-load-start');body=read(label+'-inside');after=read(label+'-after')
 w={'label':label,'mode':'baseline' if label.startswith('base') else 'view-off' if label.endswith('off') else 'view-on','shop':body['address'],'realSeconds':r['realSeconds'],'fps':len(whole)/r['realSeconds'],'nativeGameMinutes':r['nativeGameMinutes'],'clockBefore':read(label+'-health-before')['time'],'clockAfter':f"{after['hour']:02}:{int(after['minute']):02}",'wholeFrameMs':dist(whole),'modWorkMs':dist(mod),'primaryPlusGuiMs':dist(primaryGui),'additionalNativeHookNames':hookNames,'population':r['citizens'],'peakPedestrians':r['peakObservedPedestrians'],'peakCars':r['peakObservedCars'],'eventPlanningCalls':sum(int(x['planCalls']) for x in modrows),'eventSlices':sum(int(x['eventSlices']) for x in modrows),'navMeshTimedFrames':sum(float(x['navMeshPathMs'])>0 for x in modrows),'nativeQueryCallCounts':'Not exposed in reused primary observer; planning-call and timed-frame counts retained','collections':r['collections'],'allocationCounterAvailable':r['allocationCounterAvailable'],'allocatedBytesPerFrame':r['allocatedBytesPerFrame'],'managedBytesBefore':r['managedBytesBefore'],'managedBytesAfter':r['managedBytesAfter'],'components':r['components'],'modSpikesAtLeast30ms':spikes,'wholeStallsOver50ms':sum(x>50 for x in whole),'wholeStallsOver100ms':sum(x>100 for x in whole),'wholeStallsOver250ms':sum(x>250 for x in whole),'validity':valid,'attributionErrors':errors,'load':load,'loaded':read('tested-build')['actual']['baseline' if label.startswith('base') else 'candidate'],'rawFiles':[mf[0].name,wf[0].name],'rawObservations':r['rawObservations'],'environment':r['environment'],'gate':dist(mod)['p95']<5}
 windows.append(w)
result={'role':'rig_test','method':'Per-frame Update/authority union plus same-frame whole PopulationRuntime.OnGUI and six disjoint native physics/intersection hook bodies. Asserted all added hook calls outside primary scope; native prefix/postfix/finalizer and force/rotation methods are separate bodies, not the enclosing native method. Nested primary components not added. Percentiles floor(n*q). Other native hook callbacks outside primary scopes unmeasured. Diagnostic transition/body/budget capture disabled during primary perf. Whole-frame intervals informational.','windows':windows,'allMeasuredModP95Below5ms':all(w['gate'] for w in windows)}
(S/'performance-summary.json').write_text(json.dumps(result,indent=2))
for w in windows:print(w['label'],round(w['realSeconds'],3),round(w['fps'],2),'whole',w['wholeFrameMs'],'mod',w['modWorkMs'],'spikes',w['modSpikesAtLeast30ms'])
