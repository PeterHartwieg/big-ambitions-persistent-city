"""Evidence integrity only; a passing verifier does not pass the failed rig gate."""
from pathlib import Path
import json,csv,io,lzma,math,hashlib,re
R=Path(__file__).parent
def get(n):return json.loads((R/(n+'.json')).read_text())
def dist(values):
 x=sorted(values);n=len(x);return dict(samples=n,p50=x[int(n*.5)],p95=x[int(n*.95)],maximum=x[-1])
def equal(a,b):
 assert a['samples']==b['samples']
 for key in ['p50','p95','maximum']:assert math.isclose(a[key],b[key],abs_tol=1e-7),(key,a,b)
perf=get('performance-summary');assert len(perf['windows'])==4
spikes=0;failed=[]
for w in perf['windows']:
 assert w['seconds']>=60 and w['validity']['clean'] and w['validity']['saveFilesUnchanged']
 assert w['autosaveBefore']['preventAutoSave'] and w['autosaveAfter']['preventAutoSave']
 assert not any(w['errors'].values())
 mf,wf=[R/name for name in w['rawFiles']]
 m=list(csv.DictReader(io.StringIO(lzma.decompress(mf.read_bytes()).decode())))
 wall=list(csv.DictReader(io.StringIO(lzma.decompress(wf.read_bytes()).decode())))
 freq=w['stopwatchFrequency'];assert freq>0
 mod=[float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs']) for x in m]
 whole=[(int(x['endTicks'])-int(x['startTicks']))*1000/freq for x in wall]
 equal(dist(mod),w['modMs']);equal(dist(whole),w['wholeMs'])
 assert {str(t):sum(x>t for x in whole) for t in [50,100,250]}==w['wholeStalls']
 rawspikes=[int(x['unityFrame']) for x,v in zip(m,mod) if v>=30]
 assert rawspikes==[x['frame'] for x in w['spikesAtLeast30ms']]
 spikes+=len(rawspikes)
 if dist(mod)['p95']>=5:failed.append(w['label'])
assert failed==['cand100-breakfast'] and not perf['p95Pass']
audit=get('chance-audit');assert audit['allReconciled']
for arm in audit['arms']:
 for pair in arm['pairs']:assert pair['residual']==0
samples=json.loads(lzma.decompress((R/'state-samples.json.xz').read_bytes()))
reloads=get('reload-proof');assert reloads['allExistingStopRecordsPreserved'] and len(reloads['observations'])==8
for case in reloads['observations']:
 before=samples[case['beforeFile']];after=samples[case['afterFile']]
 assert before['identityDigest']==after['identityDigest']
 new={x['id']:x for x in after['stops']}
 for old in before['stops']:
  current=new[old['id']]
  assert all(old[k]==current[k] for k in ['assigned','current','activity','phase','ActivityUntilMinute','NextEventMinute','OptionalStopChoice','stop'])
zero=get('zero-control');assert zero['maxEligible']==573 and zero['nonVacuous'] and zero['zeroNewStops']
initial=samples['cand0-weekday-initial.json'];inherited={(x['id'],x['stop']['VisitMinute']) for x in initial['stops']}
for name,v in samples.items():
 if name.startswith('cand0-weekday-sample-'):
  assert v['tally']['Drawn']==v['tally']['Realised']==0
  assert all((x['id'],x['stop']['VisitMinute']) in inherited for x in v['stops'])
red=get('red-evidence')['R1'][0];assert len(red['records'])==349 and red['recordsObservedAtOrAfterPlannedStart']==349 and not red['replayedAfterPlannedStart']
assert all(x['cancelledInRain'] and x['stillCancelledAfterClearing'] and x['startEndUnchanged'] and x['deadlineUnchanged'] for x in red['records'])
assert get('log-review')['noPersistentCityErrors']
assert all(x['status']=='Loaded' and x['SourceSchema']==16 and x['sourceHash']==x['upgradedHash'] for x in get('save-contract')['nativeReadbacks'])
cleanup=get('cleanup-summary');assert cleanup['profileFilesVerified']==4828 and cleanup['installReferencesVerified']==107
assert cleanup['preferencesHashMatches'] and not cleanup['leaseHeld'] and cleanup['leasePending']==0
assert not cleanup['ownedGameProcessesRemaining'] and not cleanup['ownedCoordinatorLauncherBuildProcessesRemaining'] and not cleanup['gameKillsRequired']
assert get('session-final')['locked']
for path in R.rglob('*'):
 if not path.is_file():continue
 assert path.suffix.lower() not in ['.dll','.exe','.hsg','.reg','.private']
 data=lzma.decompress(path.read_bytes()) if path.suffix=='.xz' else path.read_bytes()
 assert not re.search(rb'c:[/\\]+users[/\\]+peter',data,re.I),path.name
 if path.name!='verify_evidence.py':assert not re.search(rb'gpt-[0-9]|claude-opus-[0-9]|glm-[0-9]',data,re.I),path.name
for target in re.findall(r'\]\(([^)]+)\)',(R/'README.md').read_text()):
 if not target.startswith('https://'):assert (R/target).is_file(),target
manifest=R/'artifact-manifest.json'
if manifest.exists():
 for name,expected in get('artifact-manifest')['sha256'].items():assert hashlib.sha256((R/name).read_bytes()).hexdigest()==expected,name
assert sum(p.stat().st_size for p in R.rglob('*') if p.is_file())<4*2**20
print('Evidence integrity PASS: four windows; recorded FAIL at 100% breakfast p95; zero >=30ms mod spikes; exact reload/accounting/zero-control/restore receipts verified.')
