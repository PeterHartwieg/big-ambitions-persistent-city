from pathlib import Path
import csv,gzip,hashlib,io,json,struct
R=Path(__file__).parent
J=lambda n:json.loads((R/(n+'.json')).read_text())
def percentile(a,q):a=sorted(a);return a[min(len(a)-1,int(len(a)*q))]
def main():
 manifest=J('file-manifest')
 for row in manifest['files']:
  p=R/row['file'];assert p.is_file()and hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'],row['file']
 perf=J('performance-summary');assert [r['window']for r in perf['windows']]==['base1','cand1','base2','cand2']
 for w in perf['windows']:
  assert w['seconds']>=60 and w['cleanNoSave']and w['autosaveOffBeforeAfter']
  work=list(csv.DictReader(io.StringIO(gzip.decompress((R/w['evidence']['mod-work']['file']).read_bytes()).decode())))
  values=[float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])for x in work]
  assert len(values)==w['modWork']['samples']
  for q,key in [(.5,'p50'),(.95,'p95')]:assert abs(percentile(values,q)-w['modWork'][key])<1e-9
  assert abs(max(values)-w['modWork']['maximum'])<1e-9
  assert sum(v>=30 for v in values)==len(w['modSpikesGe30ms'])
  obs=list(csv.DictReader(io.StringIO(gzip.decompress((R/w['evidence']['observations']['file']).read_bytes()).decode())))
  f=w['rawObservations']['stopwatchFrequency'];whole=[(int(x['endTicks'])-int(x['startTicks']))*1000/f for x in obs]
  for q,key in [(.5,'p50'),(.95,'p95')]:assert abs(percentile(whole,q)-w['wholeFrame'][key])<1e-9
  assert abs(max(whole)-w['wholeFrame']['maximum'])<1e-9
 h=J('held-config-summary');assert h['heldImageBitExact']and h['heldChoiceSame']and h['a3ControlChoiceChanged']and h['sameDayNewContextUsesEditedImage']
 for row in h['rows']:
  if row['tuning']:
   wire=row['tuning'];assert len(wire)==193 and wire[0]=='1'
   decoded=[struct.unpack('>d',bytes.fromhex(wire[i:i+16]))[0]for i in range(1,193,16)]
   assert decoded==list(row['coefficients'].values())
 for r in J('refusal-summary')['checks']:assert r['bytesPreserved']and r['nativeSaveSha256Before']==r['nativeSaveSha256After']and r['nativeOrdersStockAndCashUnchanged']
 assert J('history32-summary')['storedRoundtripExact']['normalizedEqual']
 assert J('supplement-source-preservation')['nativeSourceBytesUnchanged']
 c=J('cleanup-summary');assert c['restored']and not c['leaseHeld']and c['leasePending']==0 and not c['ownedProcessesRemaining']
 assert J('log-review')['unexpectedPersistentCityMessageCount']==0
 print('PASS: manifest, raw frame distributions/spike counts, held wire images, refusal preservation, history, source hashes, logs and cleanup.')
if __name__=='__main__':main()
