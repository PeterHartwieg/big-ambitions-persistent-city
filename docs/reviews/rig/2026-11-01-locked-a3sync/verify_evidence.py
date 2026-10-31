from pathlib import Path
import json,csv,gzip,hashlib,math,io
P=Path(__file__).parent

def load(n):return json.loads((P/(n+'.json')).read_text())
def dist(a):
 a.sort();return dict(samples=len(a),p50=a[int(len(a)*.5)],p95=a[int(len(a)*.95)],maximum=a[-1],mean=sum(a)/len(a))
def same(a,b):
 assert a.keys()==b.keys()
 for k in a:assert math.isclose(a[k],b[k],abs_tol=1e-7),(k,a[k],b[k])
manifest=load('manifest')
for f in manifest['files']:
 p=P/f['file'];assert p.stat().st_size==f['bytes'];assert hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256']
r=load('performance-summary');assert len(r['windows'])==8
pooled={}
for w in r['windows']:
 files=w['rawFiles'];modfile=next(x for x in files if 'mod-work-' in x['file']);wallfile=next(x for x in files if 'observations-' in x['file'])
 def rows(f):return list(csv.DictReader(io.StringIO(gzip.decompress((P/f['file']).read_bytes()).decode())))
 mr=rows(modfile);wr=rows(wallfile);freq=w['stopwatchFrequency'];mod=[float(x['combinedUnionMs'])+sum(float(x[f]) for f in w['hookFields']) for x in mr];whole=[(int(x['endTicks'])-int(x['startTicks']))*1000/freq for x in wr]
 same(dist(mod),w['modMs']);same(dist(whole),w['wholeFrameMs']);assert w['seconds']>=60 and w['validity']['clean'] and not any(w['errors'].values());assert w['validity']['autosaveOffBeforeAfter'] and w['validity']['saveFilesUnchanged'] and not w['validity']['saveActivity']
 assert w['modMs']['p95']<5
 spikes=[(int(x['unityFrame']),v) for x,v in zip(mr,[float(x['combinedUnionMs'])+sum(float(x[f]) for f in w['hookFields']) for x in mr]) if v>=30]
 assert [x[0] for x in spikes]==[x['unityFrame'] for x in w['spikesAtLeast30ms']]
 for t in [50,100,250]:assert sum(x>t for x in whole)==w['wholeFrameStalls'][str(t)]
 key=w['arm']+'-'+w['condition'];group=pooled.setdefault(key,dict(mod=[],whole=[]));group['mod']+=mod;group['whole']+=whole
for key,v in pooled.items():same(dist(v['mod']),r['pooled'][key]['modMs']);same(dist(v['whole']),r['pooled'][key]['wholeFrameMs'])
m=load('migration-summary');assert m['sourceSchema']==16 and m['schemaAfterUpgrade']==17;assert set(m['schemasSavedReload1Reload2'])=={17};assert m['allLegacyFieldsExact'] and m['traitsIdenticalAcrossReloads'] and m['sourceSavedAgainExact'];assert m['activationDay']==m['expectedActivationDay'];assert m['optionalChancesPreservedByMigration'] and m['shopScopePreserved'];assert m['noon']['allLegacyFieldsExact']
for x in m['nativeSaveContracts']:assert x['authorityEqual'] and x['receiptDictionaryEqual']
for x in m['refusals']:assert x['before']==x['after'] and x['runtime']['failed']
a=load('a3-summary');replay=a['replay'];assert replay['allSharedProjectionsEqual'] and replay['sharedContexts']>0 and replay['onlyFirst']==replay['onlySecond']==0 and not replay['endConsumerMoneyMismatches']
projections=json.loads(gzip.decompress((P/a['replayProjectionFile']).read_bytes()))
assert projections['replay1']==projections['replay2'];assert len(projections['replay1'])==replay['sharedContexts']
def keypart(x):x=str(x);return '|'+str(len(x))+':'+x
def stable(text,seed):
 v=2166136261^(seed&0xffffffff)
 for c in text:v=((v^ord(c))*16777619)&0xffffffff
 v^=v>>16;v=(v*0x7feb352d)&0xffffffff;v^=v>>15;return v
for x in projections['replay1']:
 prefix=''.join(keypart(x[k]) for k in ['consumer','day','mealKind','context'])
 for shop in x['shortlist']:
  assert shop['shortlistHash']==stable('pc:food-shortlist:v1'+prefix+keypart(shop['business']),a['seed'])
  for offer in shop['offers']:assert offer['draw']==stable('pc:food-choice:v1'+prefix+keypart(x['attempt'])+keypart(shop['business'])+keypart(offer['product']),a['seed'])
 assert x['householdDraw']==stable('pc:food-choice:v1'+prefix+keypart(x['attempt'])+keypart('')+keypart(''),a['seed'])
for x in a['runs'].values():
 z=x['accounting'];assert z['receiptAndResidentMoneyEqual'] and z['receiptAndResidentCountEqual'] and z['historyAndSpendEqual'];assert not z['after']['conservationFailures'] and not z['after']['negativeBalancesOrHolds']
z=a['runs']['inflight']['accounting'];assert z['newPaidReceipts']==z['residentPurchaseDelta']==z['nativePurchasesDelta']==1;assert z['producerStockDelta']==z['bagStockDelta']==-1
for key in ['replay1','replay2']:
 z=a['runs'][key]['accounting'];assert z['newPaidReceipts']==z['nativePurchasesDelta']==z['producerStockDelta']==z['bagStockDelta']==0
mid=load('midjourney-summary');assert mid['savedPayloadIdenticalAcrossReload'] and all(x['intentTermsIdentical'] for x in mid['activeMeals'])
assert all(a['completedReturn'][k] for k in ['atOriginalHome','journeyCleared','outingCleared','excursionCleared'])
for label in ['replay1','replay2']:
 z=a['runs'][label]['accounting'];assert z['nativeOrderCountStart']==z['nativeOrderCountEnd'] and z['physicalNativeItemsUnchanged']
ins=load('inspector-summary');entries={x['label']:x for x in ins['entries']}
assert 'not recorded (before the upgrade)' in entries['noon-upgrade-popup']['fields']['body']
assert 'chose household food' in entries['household-popup']['fields']['body']
native={x['id']:x for x in ins['independentNativeAddresses']}
for cid in ['c0','c102','c149']:
 x=entries['migration-inspector-'+cid]['fields'];assert native[cid]['home']['formatted'] in x['homeLabel'] and native[cid]['work']['formatted'] in x['workLabel']
for x in load('inspector-summary')['entries']:assert x['payloadEqual'] and x['routesUnchanged'] and x['marketUnchanged'] and x['traitWordsOnly'] and not x['rawStreetKeyShown']
l=load('log-checks');assert not l['unexpectedModExceptions'] and not l['unexpectedObserverExceptions'] and not l['unexpectedModErrorLines']
c=load('cleanup-summary');assert c['restore']['restored'] and c['restore']['preferences_hash_matches'] and not c['lease']['lease']['held'];assert not c['remainingOwnedHelperCandidates'] and not c['remainingAgent1Games']
assert c['restore']['profile_files_verified']==c['backup']['profile_files'] and c['restore']['receipt_references_verified']==c['backup']['receipt_references']
original={x['file']:x['sha256'] for x in c['backupRootManifest']}
for filename,value in c['restore']['root_file_hashes'].items():assert value==original[filename]
assert c['restore']['preferences_sha256']==original['preferences.reg']
print('PASS: manifest, eight raw timing windows, migration/refusal/reload, deterministic accounting, inspector read-only content, logs, restore and lease checks.')
