from pathlib import Path
import json,gzip,csv,io,hashlib,math
R=Path(__file__).parent
def dist(v):
 v=sorted(v);n=len(v);return {'samples':n,'p50':v[min(n-1,int(n*.5))],'p95':v[min(n-1,int(n*.95))],'maximum':v[-1]}
summary=json.loads((R/'performance-summary.json').read_text())
for w in summary['windows']:
 modFile=next((R/'raw').glob('frames-mod-work-'+w['label']+'-stop-*.csv.gz'));wallFile=next((R/'raw').glob('frames-observations-'+w['label']+'-stop-*.csv.gz'))
 mod=list(csv.DictReader(io.StringIO(gzip.decompress(modFile.read_bytes()).decode('utf-8-sig'))));wall=list(csv.DictReader(io.StringIO(gzip.decompress(wallFile.read_bytes()).decode('utf-8-sig'))))
 values=[float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])+sum(float(x[n+'InclusiveMs']) for n in w['additionalNativeHookNames']) for x in mod]
 whole=[(int(x['endTicks'])-int(x['startTicks']))*1000/w['rawObservations']['stopwatchFrequency'] for x in wall]
 for observed,expected in [(dist(values),w['modWorkMs']),(dist(whole),w['wholeFrameMs'])]:
  for k,v in observed.items():assert math.isclose(v,expected[k],abs_tol=1e-8),(w['label'],k,v,expected[k])
 spikes=[(int(r['unityFrame']),v) for r,v in zip(mod,values) if v>=30]
 assert len(spikes)==len(w['modSpikesAtLeast30ms'])
 for (frame,value),record in zip(spikes,w['modSpikesAtLeast30ms']):assert frame==record['unityFrame'] and math.isclose(value,record['modMs'],abs_tol=1e-8)
 assert w['realSeconds']>=60 and w['validity']['clean'] and not any(w['attributionErrors'].values())
for item in json.loads((R/'raw-provenance.json').read_text()):
 data=gzip.decompress((R/item['file']).read_bytes());assert hashlib.sha256(data).hexdigest()==item['publishedPlainSha256']
functional=json.loads((R/'functional-summary.json').read_text())
for name in ['cand-missing-inside','cand-off-inside']:
 state=functional['states'][name];assert state['hidden'] and not state['bindings'] and state['citizens']==957 and not state['failed']
contracts=list(functional['saveContracts'].values());assert len(contracts)>=4
for contract in contracts:assert contract['savedSchema']==16 and not contract['interiorPropertyNames'] and contract['topLevelFields']==contracts[0]['topLevelFields'] and contract['namespaceFamilies']==contracts[0]['namespaceFamilies']
for trace in functional['traces']:assert not trace['duplicateResidentFrames'] and not trace['duplicateNativeBodyFrames']
for budget in functional['admissionBudgets']:assert budget['maximumAttemptsPerPass']<=4 and not budget['errors']
zero=functional['states']['on-zero-state'];assert zero['viewActive'] and not zero['bindings']
assert len(functional['states']['on-tiny-state']['bindings'])<=2
logical=json.loads((R/'logical-continuity-summary.json').read_text());assert logical['residentIdsPreserved'] and logical['carOwnershipPreserved'] and logical['after']['uniqueResidents']==957 and logical['after']['uniqueCarIds']==188
callbacks=json.loads((R/'on-callbacks-after-shutdown.json').read_text());assert all(e['available'] and not e['persistentCityCallbacks'] for e in callbacks['events']) and not callbacks['ambientOwnerPresent'] and not callbacks['viewConfigured']
assert json.loads((R/'on-aux-completed.json').read_text())['completed']
cleanup=json.loads((R/'cleanup-summary.json').read_text());assert cleanup['restored'] and cleanup['profileFilesVerified']==4828 and cleanup['installReferencesVerified']==107 and cleanup['preferencesHashMatches'] and not cleanup['leaseHeld'] and cleanup['leasePending']==0 and not cleanup['ownedGamesOrDriversRunning']
log=json.loads((R/'log-summary.json').read_text());assert log['unexpectedModMessages']==log['persistentCityExceptionStacks']==0
manifest=R/'manifest.json'
if manifest.exists():
 for name,item in json.loads(manifest.read_text()).items():
  data=(R/name).read_bytes();assert len(data)==item['bytes'] and hashlib.sha256(data).hexdigest()==item['sha256'],name
print('PASS: performance, spike attribution, validity, native invariants, continuity, cleanup and evidence integrity')
