"""Verify public raw distributions, bounded D1 scheduling and evidence integrity."""
from pathlib import Path
import csv,gzip,lzma,io,json,hashlib,math,struct
from fractions import Fraction
S=Path(__file__).parent
def get(n):return json.loads((S/(n+'.json')).read_text())
def raw(n):return list(csv.DictReader(io.StringIO((lzma.decompress((S/n).read_bytes()) if n.endswith('.xz') else gzip.decompress((S/n).read_bytes())).decode())))
def dist(v):
 x=sorted(v);return dict(samples=len(x),p50=x[int(len(x)*.5)],p95=x[int(len(x)*.95)],maximum=x[-1],mean=sum(x)/len(x))
def equal(a,b):
 assert a['samples']==b['samples']
 for k in ['p50','p95','maximum','mean']:assert math.isclose(a[k],b[k],abs_tol=1e-7),(k,a[k],b[k])
for name,expected in get('manifest').items():assert hashlib.sha256((S/name).read_bytes()).hexdigest()==expected,name
p=get('performance-summary');assert len(p['windows'])==8 and p['allNormalPlayValid']
for w in p['windows']+p['supplementalWindows']:
 mf=next(n for n in w['rawFiles'] if n.startswith('frames-mod-work-'));wf=next(n for n in w['rawFiles'] if n.startswith('frames-observations-'))
 m=raw(mf);wall=raw(wf);df=next((n for n in w['rawFiles'] if n.startswith('d1-')),None);dr=raw(df) if df else []
 assert len(dr)==len({x['frame'] for x in dr})
 d={int(x['frame']):float(x['ms']) for x in dr}
 equal(dist([float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])+d.get(int(x['unityFrame']),0) for x in m]),w['modMs'])
 equal(dist([(int(x['endTicks'])-int(x['startTicks']))*1000/w['stopwatchFrequency'] for x in wall]),w['wholeMs'])
 assert w['seconds']>=60 and w['clean'] and w['saveFilesUnchanged'] and not w['saveActivity']
 assert w['autosaveBefore']['preventAutoSave'] and w['autosaveAfter']['preventAutoSave'] and not any(w['attributionErrors'].values())
 assert w['d1FramesMapped']==sum(int(x['unityFrame']) in d for x in m)
 assert len(w['spikesAtLeast30ms'])==sum(float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])+d.get(int(x['unityFrame']),0)>=30 for x in m)
f=get('fact-match');assert f['allReadableFactsMatch'] and f['eligibleRivalFood']>0
assert f['oldReader']['refusedSession'] and 'city changed' in f['oldReader']['error']
assert not f['newReader']['refusedSession'] and f['sameShopFactsAfterReload']
for b in f['beforeComparisons']+f['afterReloadComparisons']:
 assert all(b['checks'].values())
 for price in b['prices']:
  native=struct.unpack('<f',struct.pack('<i',price['nativeBinary32Bits']))[0]
  assert Fraction(native)*(100<<28)==price['observedUnits']
z=get('zero-native-changes');assert z['repeats']>=100 and z['allEqual'] and not z['truncatedPaths']
assert z['clockBefore']['paused'] and z['clockAfter']['paused']
assert (z['clockBefore']['day'],z['clockBefore']['time'])==(z['clockAfter']['day'],z['clockAfter']['time'])
can=get('native-canaries')
for field in ['rivalRegistration','saveFinanceFields']:
 for side in ['before','after']:
  observed=hashlib.sha256(json.dumps(can[side][field],sort_keys=True,separators=(',',':')).encode()).hexdigest()
  assert observed==z['hashes'][field][side]
c=get('cold-spread');r=raw(c['rawFile']);assert len(r)==len({x['frame'] for x in r}) and c['continuationBound']==16
assert not c['spikesAtLeast30ms']
assert c['startupCold']['maximumMs']<30
last=0
for row in c['firstRefreshFrames']:
 if row['phase']=='cold-unit16':assert 0<=row['retained']-last<=16
 last=row['retained']
restore=get('restore-receipt');lease=get('lock-released')
assert restore['restored'] and restore['preferences_hash_matches'] and not restore['rig_running']
assert not lease['lease']['held'] and lease['lease']['pending']==0 and not lease['rig_running']
assert get('session-initial')['locked'] and get('session-final')['locked']
print('PASS: 8 windows, native facts/prices, paused 100-cycle hashes, bounded cold refresh, cleanup, manifest')
