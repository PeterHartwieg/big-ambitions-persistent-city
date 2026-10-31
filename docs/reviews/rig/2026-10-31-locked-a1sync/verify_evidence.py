from pathlib import Path
import json,gzip,csv,io,hashlib,math
S=Path(__file__).parent
for f,h in json.loads((S/'manifest.json').read_text()).items():assert hashlib.sha256((S/f).read_bytes()).hexdigest()==h,f
p=json.loads((S/'performance-summary.json').read_text());assert len(p['windows'])==5
hooks=['PhysicalTrafficHooks.FixedStepPrefix','PhysicalTrafficHooks.FixedStepPostfix','PhysicalTrafficHooks.FixedStepFinalizer','PhysicalVehicles.DeferNativeForce','PhysicalVehicles.ApplyNativeRotation','PhysicalIntersections.UpdatePostfix']
for w in p['windows']:
 mr,wr=[list(csv.DictReader(io.StringIO(gzip.decompress((S/f).read_bytes()).decode('utf-8-sig')))) for f in w['rawFiles']]
 mods=[float(r['combinedUnionMs'])+float(r['PopulationRuntime.OnGUIInclusiveMs'])+sum(float(r[n+'InclusiveMs']) for n in hooks) for r in mr]
 walls=[(int(r['endTicks'])-int(r['startTicks']))*1000/w['stopwatchFrequency'] for r in wr]
 for a,d in [(mods,w['modMs']),(walls,w['wholeMs'])]:
  a.sort();assert len(a)==d['samples']
  for k,v in [('p50',a[int(len(a)*.5)]),('p95',a[int(len(a)*.95)]),('maximum',a[-1])]:assert math.isclose(v,d[k],abs_tol=1e-7),(w['label'],k)
 spikes=[(int(r['unityFrame']),v) for r,v in zip(mr,mods) if v>=30]
 # Sorting for distribution changes only mods; recompute original-order spike values.
 spikes=[(int(r['unityFrame']),float(r['combinedUnionMs'])+float(r['PopulationRuntime.OnGUIInclusiveMs'])+sum(float(r[n+'InclusiveMs']) for n in hooks)) for r in mr if float(r['combinedUnionMs'])+float(r['PopulationRuntime.OnGUIInclusiveMs'])+sum(float(r[n+'InclusiveMs']) for n in hooks)>=30]
 assert spikes==[(r['unityFrame'],r['modMs']) for r in w['spikesAtLeast30ms']]
 assert w['seconds']>=60 and w['validity']['clean'] and w['validity']['autosaveOffBeforeAfter'] and w['validity']['saveFilesUnchanged'] and not w['validity']['saveActivity']
 assert not any(w['attributionErrors'].values())
c=json.loads((S/'content-comparison.json').read_text());assert len(c['rows'])>=5
assert all(c[k] for k in ['allAddressesExactNative','allTextEqualA1fix','allTextEqualReload','noRawKeys'])
assert all(x['payloadIdentical'] and x['countersIdentical'] for x in c['readOnlyBatches'])
f=json.loads((S/'functional-summary.json').read_text());assert f['allContractsSchema16'] and f['allSavedFieldsEqual'] and f['allSavedInteriorFieldsAbsent']
assert not any(s['duplicateOwnedBodyIds'] or s['duplicateOwnedNativeInstances'] for s in f['states'].values())
r=json.loads((S/'save-reload-summary.json').read_text());assert r['allResidentIdsPreserved'] and r['allCarOwnershipPreserved'] and r['savedPayloadIdenticalToReloadSource']
k=json.loads((S/'cleanup-summary.json').read_text());assert k['restore']['restored'] and k['restore']['preferences_hash_matches'] and not k['release']['lease']['held'] and not k['remainingOwnedGames'] and not k['remainingOwnedHelpers']
print('PASS: artifact hashes, five raw distributions/spikes, autosave/attribution, strings/read-only state, schema/continuity and cleanup')
