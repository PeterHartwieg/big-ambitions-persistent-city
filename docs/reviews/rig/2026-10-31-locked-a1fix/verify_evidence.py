"""Standalone public-artifact verification; no game, builds, or offline mod tests."""
from pathlib import Path
import json,gzip,csv,io,hashlib,math
S=Path(__file__).parent
manifest=json.loads((S/'manifest.json').read_text())
for f,h in manifest.items():
 assert hashlib.sha256((S/f).read_bytes()).hexdigest()==h, f
p=json.loads((S/'performance-summary.json').read_text())
assert len(p['windows'])==5
for w in p['windows']:
 mr,wr=[[dict(r) for r in csv.DictReader(io.StringIO(gzip.decompress((S/f).read_bytes()).decode('utf-8-sig')))] for f in w['rawFiles']]
 mods=[float(r['combinedUnionMs'])+float(r['PopulationRuntime.OnGUIInclusiveMs']) for r in mr]
 walls=[(int(r['endTicks'])-int(r['startTicks']))*1000/w['stopwatchFrequency'] for r in wr]
 for a,d in [(mods,w['modMs']),(walls,w['wholeMs'])]:
  a.sort();assert len(a)==d['samples']
  for k,v in [('p50',a[int(len(a)*.5)]),('p95',a[int(len(a)*.95)]),('maximum',a[-1])]:assert math.isclose(v,d[k],abs_tol=1e-7),(w['label'],k)
 spikes=[(int(r['unityFrame']),float(r['combinedUnionMs'])+float(r['PopulationRuntime.OnGUIInclusiveMs'])) for r in mr if float(r['combinedUnionMs'])+float(r['PopulationRuntime.OnGUIInclusiveMs'])>=30]
 assert spikes==[(r['unityFrame'],r['modMs']) for r in w['spikesAtLeast30ms']]
 assert w['seconds']>=60 and w['validity']['clean'] and w['validity']['autosaveOffBeforeAfter'] and w['validity']['saveFilesUnchanged'] and not w['validity']['saveActivity']
 assert not any(w['attributionErrors'].values())
c=json.loads((S/'content-comparison.json').read_text())
assert all(c[k] for k in ['controlRedReproduced','candidateNoRawKeys','addressesExactNative','allTextExactNativeSubstitution','allReloadTextIdentical','identitiesAndArgumentsUnchanged'])
assert len(c['comparisons'])>=5
m=json.loads((S/'matched-reload-comparison.json').read_text())
assert m['sameNativeMinute'] and m['nativeBatchMinutesExactlyEqual'] and m['allCitizensExactlyEqual'] and m['allBaselineDisplayFieldsIdenticalBeforeAndAfterReload']
for e in c['comparisons']:
 for f in e['fields'].values():assert f['nativeSubstitutionExpected']==f['candidate'] and f['reloadIdentical']
assert json.loads((S/'cleanup-summary.json').read_text())['restore']['restored']
print('PASS: manifest, five raw distributions/spike lists, validity, content comparison and cleanup')
