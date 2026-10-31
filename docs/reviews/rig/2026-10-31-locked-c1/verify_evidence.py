"""Recompute published sample distributions and native invariants. Evidence-only; no game/.NET access."""
from pathlib import Path
import csv,io,json,lzma,math,hashlib,urllib.parse
P=Path(__file__).parent
def get(n):return json.loads((P/(n+'.json')).read_text())
def raw(n):return list(csv.DictReader(io.StringIO(lzma.decompress((P/n).read_bytes()).decode())))
def data(n):return json.loads(lzma.decompress((P/n).read_bytes()))
def dist(v):
 s=sorted(v);n=len(s);return dict(samples=n,p50=s[int(n*.5)],p95=s[int(n*.95)],maximum=s[-1],mean=sum(s)/n)
def same(a,b):
 for k,v in a.items():assert math.isclose(v,b[k],rel_tol=1e-10,abs_tol=1e-7),(k,v,b[k])
perf=get('performance-summary')
for w in perf['windows']:
 files=w['rawFiles'];m=raw(next(f for f in files if f.startswith('frames-mod-work-')));wall=raw(next(f for f in files if f.startswith('frames-observations-')))
 reader=next((f for f in files if f.startswith('c1-')),None);dr=raw(reader) if reader else [];dm={int(r['frame']):r for r in dr}
 vals=[];spikes=[]
 for row in m:
  f=int(row['unityFrame']);v=float(row['combinedUnionMs'])+float(row['PopulationRuntime.OnGUIInclusiveMs'])+(float(dm[f]['ms']) if f in dm else 0);vals.append(v)
  if v>=30:spikes.append(f)
 same(dist(vals),w['modMs']);same(dist([(int(r['endTicks'])-int(r['startTicks']))*1000/w['stopwatchFrequency'] for r in wall]),w['wholeMs'])
 assert spikes==[x['frame'] for x in w['spikesAtLeast30ms']]
 assert w['validity']['clean'] and w['seconds']>=60 and w['normalPlay'] and not any(w['errors'].values())
 if reader:assert w['readerFramesMapped']>=len(m)*.98,(w['label'],'reader not exercised across window')
 for phase,v in w['readerPhases'].items():
  same(dist([float(r['ms']) for r in dr if r['phase']==phase]),v['readMs'])
proof=get('native-read-proof')
for o in proof['observations']:
 a=data(next(f for f in o['rawFiles'] if '-idle-before.' in f));b=data(next(f for f in o['rawFiles'] if '-idle-after.' in f));c=data(next(f for f in o['rawFiles'] if '-read-after.' in f));assert a==b==c and o['allEqual']
 assert o['repeatDumpsIdentical'] and all(r['count']>=100 and r['allIdentical'] and r['minuteBefore']==r['minuteAfter'] for r in o['repeats'])
 dumps=data(next(f for f in o['rawFiles'] if '-dumps.' in f));regs={r['index']:r['fields'] for r in a['registrations']}
 for scope in dumps['scopes']:
  dto=scope['dto'];assert dto['AtMinute']==a['minute'] and dto['Revision']==0 and len(dto['Businesses'])==scope['count']
  for idx,shop in zip(scope['indices'],dto['Businesses']):
   native=regs[idx];prom=native['promotion'];assert len(shop['Campaigns'])==len(native['marketingCampaigns'] or [])
   for prop,field in [('TrafficIndex','trafficIndex'),('Marketing','marketing'),('Total','total')]:
    value=None if prom is None else prom[field];expected=value if isinstance(value,int) and 0<=value<=100 else None;assert shop[prop]==expected
   prefix='business:'+urllib.parse.quote(native['StreetName'],safe='')+':'+str(native['StreetNumber'])+':'+str(native['creationDay'])+':'+urllib.parse.quote(native['businessTypeName'] or '',safe='')+':'
   assert shop['BusinessId'].startswith(prefix)
assert proof['afterReloadBusinessesEqual']
diag=get('detached-diagnostics');by={x['name']:x for x in diag}
for n in ['duplicate-index','missing-index','revision-change','minute-change']:assert by[n]['refused']
assert by['bounded-unit']['cursor']==1 and by['bounded-unit']['partialUnpublished']
for x in diag:
 for b in x.get('snapshot',{}).get('Businesses',[]):
  for c in b['Campaigns']:assert c['ResidentGroup']==0 and all(c[k] is None for k in ['Active','StartMinute','EndMinute','DurationMinutes','NativeTarget'])
for n in ['disabled','none','unsupported','missing','changed','changed-enum']:assert by[n]['snapshot']['Businesses'][0]['Status']==1
assert all(c['Status']==2 for c in by['duplicate']['snapshot']['Businesses'][0]['Campaigns'])
full=get('full-scope-diagnostic');files=full['rawFiles'];dr=raw(next(f for f in files if f.startswith('c1-')));m=raw(next(f for f in files if f.startswith('frames-mod-work-')));dm={int(x['unityFrame']):x for x in m};combined=[]
for r in dr:
 p=dm[int(r['frame'])];combined.append(float(r['ms'])+float(p['combinedUnionMs'])+float(p['PopulationRuntime.OnGUIInclusiveMs']))
same(dist(combined),full['combinedOnReadFramesMs']);assert len(dr)==full['operations'] and not full['missingPrimaryFrames'];assert sum(x>=30 for x in combined)==len(full['spikesAtLeast30ms'])
save=get('save-contract');assert save['inputPayloadEqual'] and save['migrationResultEqual'] and save['writtenPayloadByteIdentical'] and save['writtenRoundTripsExact']
assert get('source-integration')['persistenceAllUnchanged']
assert not get('native-coverage')['truncatedPaths']
if (P/'restore-receipt.json').exists():
 restore=get('restore-receipt');release=get('lock-released');assert restore['restored'] and restore['preferences_hash_matches'];assert not release['lease']['held'] and release['lease']['pending']==0 and not release['rig_running']
for n in ['session-initial','session-final']:
 if (P/(n+'.json')).exists():assert get(n)['locked']
print(json.dumps(dict(verified=True,windows=len(perf['windows']),expectedWindows=16,allMatchedP95Below5ms=perf['allP95Below5ms'],matchedSpikesAtLeast30ms=len(perf['spikes']),fullScopeSpikesAtLeast30ms=len(full['spikesAtLeast30ms']),nativeComparisons=len(proof['observations']))))
