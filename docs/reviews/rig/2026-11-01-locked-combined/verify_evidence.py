"""Recompute every reported primary frame distribution from the compressed observations."""
from pathlib import Path
import csv,gzip,hashlib,io,json,math
S=Path(__file__).parent
def stat(values):
 a=sorted(values)
 return dict(samples=len(a),p50=a[min(len(a)-1,int(len(a)*.5))],p95=a[min(len(a)-1,int(len(a)*.95))],maximum=a[-1])
def check(actual,expected):
 assert actual.keys()==expected.keys()
 for k in actual:assert math.isclose(actual[k],expected[k],rel_tol=1e-10,abs_tol=1e-10),(k,actual[k],expected[k])
summary=json.loads((S/'performance-summary.json').read_text())
for window in summary['windows']:
 data={}
 for kind,ref in window['evidence'].items():
  raw=(S/ref['file']).read_bytes();assert hashlib.sha256(raw).hexdigest()==ref['sha256']
  data[kind]=list(csv.DictReader(io.StringIO(gzip.decompress(raw).decode('utf-8-sig'))))
 work=data['mod-work'];obs=data['observations'];frequency=window['rawObservations']['stopwatchFrequency']
 check(stat([float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs']) for x in work]),window['modWork'])
 check(stat([float(x['combinedUnionMs']) for x in work]),window['updateAuthorityUnion'])
 check(stat([(int(x['endTicks'])-int(x['startTicks']))*1000/frequency for x in obs]),window['wholeFrame'])
 spikes=[int(x['unityFrame']) for x in work if float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])>=30]
 assert spikes==[x['unityFrame'] for x in window['modSpikesGe30ms']]
 assert window['cleanNoSave'] and window['autosaveOffBeforeAfter'] and window['seconds']>=60
 assert window['p95GatePass']==(window['modWork']['p95']<5)
 print(window['window'],window['modWork'],len(spikes),'flagged spikes')
print('Evidence distributions and compressed-file hashes verified.')
