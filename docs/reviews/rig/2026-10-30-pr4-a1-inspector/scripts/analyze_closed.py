"""rig_test reduction: Update/authority union plus nonnested PopulationRuntime.OnGUI per Unity frame."""
from pathlib import Path
import json,csv,statistics,hashlib,gzip
S=Path(__file__).parent

def read(p):
 b=p.read_bytes();return json.loads(b.decode('utf-16' if b[:2] in [b'\xff\xfe',b'\xfe\xff'] else 'utf-8-sig'))
def dist(a):
 a=sorted(a);n=len(a);return dict(samples=n,p50=a[min(n-1,int(n*.5))],p95=a[min(n-1,int(n*.95))],maximum=a[-1],mean=sum(a)/n)
def leaf(x):return str(x).replace('\\','/').split('/')[-1]
def main():
 windows=[];groups={};manifest=[]
 for p in sorted(S.glob('*-closed-*-stop.json')):
  label=p.stem.removesuffix('-stop');arm=label.split('-')[0];r=read(p);v=read(S/(label+'-validity.json'));assert v['clean'] and r['realSeconds']>=60
  for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls','droppedModFrames','activeReportCount']:assert not r.get(k),(label,k)
  mp=S/leaf(r['perFrameEvidence']);op=S/leaf(r['observationEvidence']);m=list(csv.DictReader(mp.open()));o=list(csv.DictReader(op.open()));frequency=r['rawObservations']['stopwatchFrequency']
  union=[float(x['combinedUnionMs']) for x in m];draw=[float(x['PopulationRuntime.OnGUIInclusiveMs']) for x in m];combined=[a+b for a,b in zip(union,draw)];whole=[(int(x['endTicks'])-int(x['startTicks']))*1000/frequency for x in o]
  pick=[float(x.get('ResidentInspector.UpdateInclusiveMs',0)) for x in m]
  timing=read(S/(label+'-inspector-after.json'))
  item=dict(label=label,seconds=r['realSeconds'],clean=True,modWork=dist(combined),primaryUnion=dist(union),onGUI=dist(draw),wholeFrame=dist(whole),inspectorUpdate=dist(pick),inspectorTiming=timing,modSpikesAtLeast30ms=[dict(unityFrame=x['unityFrame'],milliseconds=z) for x,z in zip(m,combined) if z>=30],wholeStallsAtLeast100ms=[dict(priorFrame=x['priorUnityFrame'],currentFrame=x['currentUnityFrame'],milliseconds=z) for x,z in zip(o,whole) if z>=100],autosave=read(S/(label+'-autosave-before.json')),validity=v)
  windows.append(item);g=groups.setdefault(arm,dict(mod=[],whole=[],pick=[],draw=[],seconds=0,windows=[]));g['mod']+=combined;g['whole']+=whole;g['pick']+=pick;g['draw']+=draw;g['seconds']+=r['realSeconds'];g['windows'].append(label)
  for f in [mp,op]:manifest.append(dict(window=label,file=f.name,bytes=f.stat().st_size,sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
 summaries={a:dict(seconds=g['seconds'],windows=g['windows'],modWork=dist(g['mod']),wholeFrame=dist(g['whole']),inspectorUpdate=dist(g['pick']),onGUI=dist(g['draw']),modSpikesAtLeast30ms=sum(x>=30 for x in g['mod']),wholeStallsAtLeast100ms=sum(x>=100 for x in g['whole']),closedP95Gate=dist(g['mod'])['p95']<5) for a,g in groups.items()}
 (S/'windows-summary.json').write_text(json.dumps(windows,indent=2));(S/'performance-summary.json').write_text(json.dumps(summaries,indent=2));(S/'raw-provenance.json').write_text(json.dumps(manifest,indent=2));print(json.dumps(summaries,indent=2))
if __name__=='__main__':main()
