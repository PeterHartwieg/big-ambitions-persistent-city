"""Verify sanitized rig evidence; Python standard library only. Not a game or dotnet test."""
from pathlib import Path
import csv,gzip,hashlib,io,json,math
S=Path(__file__).parent
def get(n):return json.loads((S/n).read_text())
def dist(xs):
    xs=sorted(xs);n=len(xs)
    return dict(samples=n,p50=xs[int(n*.5)],p95=xs[int(n*.95)],maximum=xs[-1],mean=sum(xs)/n)
def same(a,b):
    assert a['samples']==b['samples']
    for k in ['p50','p95','maximum','mean']:assert math.isclose(a[k],b[k],abs_tol=1e-8,rel_tol=1e-10),(k,a[k],b[k])
manifest=get('file-manifest.json')
for item in manifest['files']:
    p=S/item['file'];assert p.stat().st_size==item['bytes'];assert hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256'],p.name
assert sum(p.stat().st_size for p in S.iterdir() if p.is_file())<4_000_000
perf=get('performance-summary.json');assert len(perf['windows'])==5
pooled={}
for w in perf['windows']:
    mr,wr=[list(csv.DictReader(io.StringIO(gzip.decompress((S/f).read_bytes()).decode()))) for f in w['rawFiles']]
    whole=[(int(r['endTicks'])-int(r['startTicks']))*1000/w['stopwatchFrequency'] for r in wr]
    mod=[float(r['combinedUnionMs'])+float(r['PopulationRuntime.OnGUIInclusiveMs']) for r in mr]
    same(dist(whole),w['wholeMs']);same(dist(mod),w['modMs'])
    assert w['modMs']['p95']<5 and w['seconds']>=60 and not w['spikesAtLeast30ms']
    assert w['clean'] and w['autosaveOffBeforeAfter'] and w['saveFilesUnchanged'] and not w['saveActivity']
    assert w['autosaveBefore']['preventAutoSave'] and w['autosaveAfter']['preventAutoSave']
    assert not any(w['attributionErrors'].values())
    assert math.isclose((w['captureStoppedTicks']-w['captureStartedTicks'])/w['stopwatchFrequency'],w['seconds'],abs_tol=1e-8)
    key='baseline-closed' if w['label'].startswith('base') else 'candidate-open-active-choice' if w['label'].endswith('open') else 'candidate-closed-legacy-day'
    entry=pooled.setdefault(key,dict(whole=[],mod=[]));entry['whole']+=whole;entry['mod']+=mod
for k,v in pooled.items():same(dist(v['whole']),perf['pooled'][k]['wholeMs']);same(dist(v['mod']),perf['pooled'][k]['modMs'])
m=get('migration-summary.json');assert m['writtenSchema']==17 and m['allLegacyFieldsPreservedByMigration'] and m['traitsAndTodayRoundtripEqual']
assert m['activeMealsAtBaseline']==6 and m['activeLegacyFieldsUnchanged']
assert m['noonMigration']['allLegacyFieldsPreserved'] and m['noonMigration']['traitsAndTodayRoundtripEqual']
assert m['noonMigration']['unrecorded']['consumers']==301 and m['choiceStartsDay']==m['expectedNextDay']==6
for r in m['nativeRefusals']:assert r['integrity']['before']==r['integrity']['after'] and r['integrity']['runtime']['failed']
n=get('native-save-contract.json');assert len(n['comparisons'])==2
for r in n['comparisons']:assert r['authorityEqual'] and r['receiptDictionaryEqual']
assert n['comparisons'][1]['sourceReceipts']==27
a=get('a3-summary.json');assert a['replay']['matchingContexts']==878 and not a['replay']['mismatchingContexts'] and not a['replay']['endConsumerMoneyMismatches']
v=a['runs']['inflight']['accounting'];assert v['newPaidReceipts']==v['residentPurchaseDelta']==v['nativePurchasesDelta']==1
assert v['receiptAndResidentMoneyEqual'] and v['historyAndSpendEqual'] and v['producerStockDelta']==v['bagStockDelta']==-1
assert not v['after']['conservationFailures'] and not v['after']['negativeBalancesOrHolds']
assert a['completedReturn']['atOriginalHome'] and a['completedReturn']['journeyCleared'] and a['completedReturn']['outingCleared']
p=a['poolReuse'];assert p['entirePayloadEqual'] and len(p['nearBodies'])==len(p['returnBodies'])==1 and not p['farBodies']
assert p['nearBodies'][0]['person']['character']==p['returnBodies'][0]['person']['character']
assert p['nearBodies'][0]['identity']==p['returnBodies'][0]['identity']
assert a['legacyDaySettlement']['nativeFloatMatchesExactSumRoundedOnce']
i=get('inspector-summary.json');assert i['addressKeyDefectObserved']
frozen=[e for e in i['entries'] if e['frozenRouteAndMarketObservation']];assert len(frozen)>=3
for e in frozen:assert e['payloadEqual'] and e['routesUnchanged'] and e['marketCallsUnchanged'] and e['bodiesUnchanged']
assert 'chose household food' in next(e['fields']['body'] for e in frozen if e['label']=='verified-household-popup')
assert 'not recorded (before the upgrade)' in next(e['fields']['body'] for e in frozen if e['label']=='noon-upgrade-popup')
l=get('log-checks.json');assert not l['unexpectedPersistentCityExceptions'] and not l['unexpectedPersistentCityErrorLines']
c=get('cleanup-summary.json');assert c['restore']['restored'] and c['restore']['preferences_hash_matches'] and c['restore']['profile_files_verified']==4828
assert c['restore']['receipt_references_verified']==107 and not c['release']['lease']['held'] and c['release']['lease']['pending']==0
assert not c['remainingOwnedGames'] and not c['remainingOwnedHelpers'] and not c['forcedKills']
assert get('session-final.json')['locked']
checks=get('checks.json');assert checks['verdict']=='FAIL' and checks['counts']['A1']['DEFERRED-INPUT']==5
assert [r['check'] for r in checks['checks'] if r['status']=='FAIL']==['Readable localized home/work address text']
print('Evidence verification PASS: 5 captures, migration/refusal, 878 replay contexts, accounting, body reuse, cleanup; recorded rig verdict remains FAIL (address text).')
