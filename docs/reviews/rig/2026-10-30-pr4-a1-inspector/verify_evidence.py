"""Recompute the public evidence without game access."""
from pathlib import Path
import json,gzip,csv,io,hashlib
O=Path(__file__).parent

def dist(a):
 a=sorted(a);n=len(a);return dict(samples=n,p50=a[int(n*.5)],p95=a[int(n*.95)],maximum=a[-1],mean=sum(a)/n)
def leaf(x):return x.replace('\\','/').split('/')[-1]
w={x['label']:x for x in json.loads((O/'windows-summary.json').read_text())};summ=json.loads((O/'performance-summary.json').read_text());allvalues={}
for label,x in w.items():
 r=json.loads((O/(label+'-stop.json')).read_text());assert x['clean'] and r['realSeconds']>=60
 assert not r['sampleError'] and r['stopReason']=='frames-stop', (label,r['sampleError'],r['stopReason'])
 for key in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls','droppedModFrames','activeReportCount']:assert not r[key],(label,key)
 for key in ['Dropped','Duplicates','InvalidOrder','SkippedUnityFrames','outstandingPrimaryScopes']:assert not r['rawObservations'][key],(label,key)
 rows={}
 for key in ['perFrameEvidence','observationEvidence']:
  rows[key]=list(csv.DictReader(io.StringIO(gzip.decompress((O/(leaf(r[key])+'.gz')).read_bytes()).decode())))
 m=rows['perFrameEvidence'];o=rows['observationEvidence'];f=r['rawObservations']['stopwatchFrequency']
 vals={'modWork':[float(y['combinedUnionMs'])+float(y['PopulationRuntime.OnGUIInclusiveMs']) for y in m],'wholeFrame':[(int(y['endTicks'])-int(y['startTicks']))*1000/f for y in o],'inspectorUpdate':[float(y.get('ResidentInspector.UpdateInclusiveMs',0)) for y in m]}
 for k,v in vals.items():
  d=dist(v)
  for metric in ['samples','p50','p95','maximum','mean']:assert abs(d[metric]-x[k][metric])<1e-9,(label,k,metric)
  allvalues.setdefault(label.split('-')[0],{}).setdefault(k,[]).extend(v)
 assert x['autosave']['preventAutoSave'] and not x['autosave']['savingGameInProgress']
 if label.startswith('after'):assert x['inspectorTiming']['picks']==0 and x['inspectorTiming']['builds']==0
for arm,fields in allvalues.items():
 for k,v in fields.items():
  d=dist(v)
  for metric in d:assert abs(d[metric]-summ[arm][k][metric])<1e-9,(arm,k,metric)
for x in json.loads((O/'file-manifest.json').read_text()):assert hashlib.sha256((O/x['file']).read_bytes()).hexdigest()==x['sha256'],x['file']
print('PASS: six clean captures, exact pooled/per-window percentiles, autosave, zero picks, attribution counters and manifest.')
