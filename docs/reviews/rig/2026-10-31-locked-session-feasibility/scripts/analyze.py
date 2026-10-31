from pathlib import Path
import csv,json,math,statistics,gzip,hashlib,sys
stage=Path(__file__).parent

def load(name):return json.loads((stage/(name+'.json')).read_text(encoding='utf-8-sig'))
def dist(xs):
 ys=sorted(xs);n=len(ys)
 return {'samples':n,'mean':sum(ys)/n,'p50':ys[min(n-1,int(n*.5))],'p95':ys[min(n-1,int(n*.95))],'maximum':ys[-1]}
windows=[];allwhole=[];allmod=[]
for i in range(1,4):
 label=f'locked-window-{i}';r=load(label+'-stop');valid=load(label+'-validity');before=load(label+'-clock-before');after=load(label+'-clock-after')
 mf=list(stage.glob('frames-mod-work-'+label+'-stop-*.csv'));wf=list(stage.glob('frames-observations-'+label+'-stop-*.csv'));assert len(mf)==len(wf)==1
 modrows=list(csv.DictReader(mf[0].open()));wallrows=list(csv.DictReader(wf[0].open()));freq=r['rawObservations']['stopwatchFrequency']
 whole=[(int(x['endTicks'])-int(x['startTicks']))*1000/freq for x in wallrows];mod=[float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs']) for x in modrows]
 assert len(whole)==r['frameMilliseconds']['samples'] and len(mod)==r['completedUnityFrames']
 assert math.isclose(dist(whole)['p95'],r['frameMilliseconds']['p95'],abs_tol=1e-8)
 errors={k:r.get(k) for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls','droppedModFrames','activeReportCount']};assert not any(errors.values())
 assert valid['clean'] and r['realSeconds']>=60
 spikes=[{'unityFrame':int(row['unityFrame']),'modMs':v,'entryBranch':row['entryBranch']} for row,v in zip(modrows,mod) if v>=30]
 w={'label':label,'realSeconds':r['realSeconds'],'fps':len(whole)/r['realSeconds'],'clockBefore':{'day':before['day'],'time':before['time']},'clockAfter':{'day':after['day'],'time':after['time']},'nativeGameMinutes':r['nativeGameMinutes'],'wholeFrameMs':dist(whole),'modWorkIncludingGuiMs':dist(mod),'modWorkPrimaryUnionMs':r['combinedModWorkPerUnityFrameMilliseconds'],'modSpikesAtLeast30ms':spikes,'wholeStallsOver50ms':sum(x>50 for x in whole),'wholeStallsOver100ms':sum(x>100 for x in whole),'wholeStallsOver250ms':sum(x>250 for x in whole),'validity':valid,'attributionErrors':errors,'rawObservations':r['rawObservations'],'environment':r['environment'],'rawFiles':[mf[0].name,wf[0].name]}
 windows.append(w);allwhole+=whole;allmod+=mod
result={'role':'diagnose','method':'Update/authority union plus same-frame whole PopulationRuntime.OnGUI; nested scopes not added; floor(n*q) percentiles; initial partial wall interval retained, initial/current partial mod frame excluded. Input guard callbacks outside these scopes remain unmeasured.','windows':windows,'pooled':{'wholeFrameMs':dist(allwhole),'modWorkIncludingGuiMs':dist(allmod),'fps':len(allwhole)/sum(w['realSeconds'] for w in windows),'modSpikesAtLeast30ms':sum(len(w['modSpikesAtLeast30ms']) for w in windows)},'sourceBuild':'6d43276198eba5d0dd016b51bc7fc670bc651762'}
(stage/'performance-summary.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k in ['pooled']},indent=2))
