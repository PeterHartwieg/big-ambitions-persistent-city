"""Read-only verification of published rig distributions and evidence hashes."""
from pathlib import Path
import csv,gzip,json,hashlib,io,math
S=Path(__file__).parent
p=json.loads((S/'performance-summary.json').read_text())
def stats(values):
 values=sorted(values);n=len(values)
 return dict(samples=n,p50=values[min(n-1,int(n*.5))],p95=values[min(n-1,int(n*.95))],maximum=values[-1])
for w in p['windows']:
 rows={}
 for kind,r in w['evidence'].items():
  b=(S/r['file']).read_bytes();assert hashlib.sha256(b).hexdigest()==r['sha256'];rows[kind]=list(csv.DictReader(io.StringIO(gzip.decompress(b).decode())))
 mod=stats([float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])for x in rows['mod-work']]);freq=w['rawObservations']['stopwatchFrequency'];whole=stats([(int(x['endTicks'])-int(x['startTicks']))*1000/freq for x in rows['observations']])
 for expected,actual in [(w['modWork'],mod),(w['wholeFrame'],whole)]:
  for k,v in expected.items():assert abs(actual[k]-v)<1e-8,(w['window'],k,v,actual[k])
 flags=[x for x in rows['mod-work']if float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])>=30]
 assert len(flags)==len(w['modSpikesGe30ms']);assert w['cleanNoSave']and w['autosaveOffBeforeAfter'];assert w['seconds']>=60
 print(w['window'],'mod p95/max',round(mod['p95'],4),round(mod['maximum'],4),'flags',len(flags))
if(S/'native-day-summary.json').exists():
 n=json.loads((S/'native-day-summary.json').read_text());assert n['allPaymentsReconcile']and n['roundedNativeRevenueExact'];assert n['nativeSnapshotUnchangedAfterReload'];assert all(n['independentReplay'][k]for k in ['nativeSnapshotEqual','consumerMoneyEqual','memoryEqual','receiptDictionaryEqual'])
if(S/'capacity-summary.json').exists():
 c=json.loads((S/'capacity-summary.json').read_text());assert c['allFourEntriesRetainedOnNativeLoad'];assert c['ordinaryNativeInsertions']>0;assert c['sourcePayloadHash']==c['loadedPayloadHash']
if(S/'refusal-summary.json').exists():
 for c in json.loads((S/'refusal-summary.json').read_text())['cases']:assert c['fileUnchanged']and c['livePayloadPreserved']
print('Evidence verification complete; verdict and remaining scope are in README.md.')
