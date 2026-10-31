from pathlib import Path
import json,re,hashlib,csv,gzip,math
S=Path(__file__).parent;O=S/'public-evidence'
def get(n):
 b=(S/(n+'.json')).read_bytes();return json.loads(b.decode('utf-16' if b.startswith(b'\xff\xfe') else 'utf-8-sig'))
def put(n,v):(O/(n+'.json')).write_text(json.dumps(v,indent=2,ensure_ascii=False),encoding='utf-8')
def dist(xs):
 a=sorted(xs);n=len(a);return dict(samples=n,p50=a[int(n*.5)],p95=a[int(n*.95)],maximum=a[-1])
def perf():
 rows=[]
 for label in ['base1','cand1','base2','cand2','cand2-open']:
  if not (S/(label+'-stop.json')).exists():continue
  r=get(label+'-stop');v=get(label+'-validity');mf=next(S.glob('frames-mod-work-'+label+'-stop-*.csv'));wf=next(S.glob('frames-observations-'+label+'-stop-*.csv'))
  mods=list(csv.DictReader(mf.open()));walls=list(csv.DictReader(wf.open()));freq=r['rawObservations']['stopwatchFrequency']
  hooks=['PhysicalTrafficHooks.FixedStepPrefix','PhysicalTrafficHooks.FixedStepPostfix','PhysicalTrafficHooks.FixedStepFinalizer','PhysicalVehicles.DeferNativeForce','PhysicalVehicles.ApplyNativeRotation','PhysicalIntersections.UpdatePostfix']
  for c in r['components']:
   if c['name'] in hooks:assert c['calls']==c['outsidePrimaryScopeCalls'],c
  ms=[float(x['combinedUnionMs'])+float(x['PopulationRuntime.OnGUIInclusiveMs'])+sum(float(x[n+'InclusiveMs']) for n in hooks) for x in mods];ws=[(int(x['endTicks'])-int(x['startTicks']))*1000/freq for x in walls]
  errors={k:r.get(k) for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls','droppedModFrames','activeReportCount']}
  assert len(ws)==r['frameMilliseconds']['samples'] and len(ms)==r['completedUnityFrames']
  assert math.isclose(dist(ws)['p95'],r['frameMilliseconds']['p95'],abs_tol=1e-7)
  for f in [mf,wf]:(O/(f.name+'.gz')).write_bytes(gzip.compress(f.read_bytes(),mtime=0))
  rows.append(dict(label=label,environment=r['environment'],seconds=r['realSeconds'],modMs=dist(ms),wholeMs=dist(ws),spikesAtLeast30ms=[dict(window=label,unityFrame=int(x['unityFrame']),modMs=m,phase=x['entryBranch'],eventMs=x.get('eventMs'),components={k:float(v) for k,v in x.items() if k.endswith('InclusiveMs') and v and float(v)>0}) for x,m in zip(mods,ms) if m>=30],wholeStalls={str(t):sum(x>t for x in ws) for t in [50,100,250]},attributionErrors=errors,validity=v,clockBefore=get(label+'-state-before')['nativeMinute'],clockAfter=get(label+'-clock-after')['time'],loadWait=get(label+'-load-wait'),loadBefore=get(label+'-load-start'),loadBeforeWarmup=get(label+'-load-before'),loadAfter=get(label+'-load-after'),formatCounts=get(label+'-format-counts'),inspectorTiming=get(label+'-inspector-timing'),rawFiles=[mf.name+'.gz',wf.name+'.gz'],stopwatchFrequency=freq))
 put('performance-summary',dict(role='rig_test',method='Update/authority union + same-frame whole PopulationRuntime.OnGUI; nested scopes not added. floor(n*q) percentiles. Six disjoint native physics/intersection hook bodies are added only after asserting their calls are outside primary scopes; other small callbacks remain unmeasured.',windows=rows,allP95Below5ms=all(x['modMs']['p95']<5 for x in rows),allValid=all(x['validity']['clean'] and x['seconds']>=60 and not any(x['attributionErrors'].values()) for x in rows)))
 print(json.dumps([dict(window=x['label'],seconds=x['seconds'],mod=x['modMs'],whole=x['wholeMs'],spikes=len(x['spikesAtLeast30ms'])) for x in rows],indent=2))



def content():
 a=get('a1fix-inspector-summary');b=get('candidate-inspector-summary');c=get('candidate-reloaded-inspector-summary');rows=[]
 for old,new,reloaded in zip(a['entries'],b['entries'],c['entries']):
  assert old['id']==new['id']==reloaded['id']
  addresses=[]
  for key in ['Home','Work']:
   v=new['insight'][key]
   if not v:continue
   native=next(x for x in new['places'] if x['PlaceId']==v['PlaceId'])
   addresses.append(dict(field=key,shown=v['AddressText'],native=native['nativeFormatted'],exact=v['AddressText']==native['nativeFormatted'],nativeRegistrationFound=native['nativeRegistrationFound'],mapArguments={k:v[k] for k in ['PlaceId','Street','Number','CanNavigate']},argumentsEqualA1fix=all(v[k]==old['insight'][key][k] for k in ['PlaceId','Street','Number','CanNavigate'])))
  display=list(new['fields'].values())+[new['insight'][k] for k in ['Story','Heading','HeadingReason','MealNote']]+[m['Text'] for m in new['insight']['Meals']]
  rows.append(dict(id=new['id'],addresses=addresses,noRawKeys=not any(re.search(r'\b(?:ba|rlo):[A-Za-z_]',s,re.I) for s in display),identityEqualA1fix=old['identity']==new['identity'],identityEqualReload=new['identity']==reloaded['identity'],fieldsEqualA1fix=old['fields']==new['fields'],fieldsEqualReload=new['fields']==reloaded['fields'],fields={k:dict(candidate=v,a1fix=old['fields'][k],reloaded=reloaded['fields'][k]) for k,v in new['fields'].items()},placesEqualA1fix=old['places']==new['places']))
 def sameCounters(x):
  before=dict(x['countersBefore']);after=dict(x['countersAfter']);before.pop('frame',None);after.pop('frame',None);return before==after
 result=dict(role='rig_test',method='seam, not gesture proof; independently invokes native Streets.AddressHelper.ToFormattedString(Address) for native building registrations',selection=get('residents'),a1fixNativeMinute=a['nativeMinute'],candidateNativeMinute=b['nativeMinute'],reloadedNativeMinute=c['nativeMinute'],rows=rows,allAddressesExactNative=all(v['exact'] for r in rows for v in r['addresses']),allTextEqualA1fix=all(r['fieldsEqualA1fix'] for r in rows),allTextEqualReload=all(r['fieldsEqualReload'] for r in rows),noRawKeys=all(r['noRawKeys'] for r in rows),readOnlyBatches=[dict(label=n,payloadIdentical=x['payloadIdentical'],payloadHashBefore=x['payloadHashBefore'],payloadHashAfter=x['payloadHashAfter'],countersIdentical=sameCounters(x),countersBefore=x['countersBefore'],countersAfter=x['countersAfter']) for n,x in [('candidate',b),('candidate-reload',c)]],displayOrder=['header','Home','Work or Study','personality','story','heading and reason','breakfast','lunch','dinner','meal note'],displayOrderMethod='Live cached fields and rendered popup; DrawPopup source wires rows in this order. Gesture path deferred.')
 if (S/'post-interior-inspector-summary.json').exists():
  post=get('post-interior-inspector-summary')
  result['postInterior']=dict(payloadIdentical=post['payloadIdentical'],residentIds=[e['id'] for e in post['entries']],identitiesPreserved=all(x['id']==y['id'] and x['identity']==y['identity'] for x,y in zip(b['entries'],post['entries'])),noRawKeys=not any(re.search(r'\b(?:ba|rlo):[A-Za-z_]',v,re.I) for e in post['entries'] for v in e['fields'].values()),allHomeWorkAddressesExactNative=all(v['AddressText']==next(p['nativeFormatted'] for p in e['places'] if p['PlaceId']==v['PlaceId']) for e in post['entries'] for k in ['Home','Work'] if (v:=e['insight'][k]) is not None))
 put('content-comparison',result)
 for n in ['candidate-inspector-summary','candidate-reloaded-inspector-summary','a1fix-inspector-summary','post-interior-inspector-summary']:
  if (S/(n+'.json')).exists():put(n,get(n))
 print('Content:',{k:v for k,v in result.items() if isinstance(v,bool)})

def functional():
 labels=[]
 for path in S.glob('*.json'):
  try:
   v=get(path.stem)
   if isinstance(v,dict) and all(k in v for k in ['bindings','inside','citizens','ids']):labels.append(path.stem)
  except (ValueError,UnicodeError):pass
 states={}
 for label in labels:
  if not (S/(label+'.json')).exists():continue
  v=get(label);ids=v.pop('ids',None)
  if ids is not None:v['residentIdentityDigest']=hashlib.sha256('\n'.join(ids).encode()).hexdigest();v['uniqueResidents']=len(set(ids))
  v['duplicateOwnedBodyIds']=len([b['id'] for b in v['bindings']])!=len({b['id'] for b in v['bindings']})
  native=[b['nativeId'] for b in v['bindings'] if b['nativeId']];v['duplicateOwnedNativeInstances']=len(native)!=len(set(native))
  v['nativePeopleCount']=len(v.pop('nativePeople'));v['nativeCarsCount']=len(v.pop('nativeCars'));states[label]=v
 traces={}
 for label in sorted({p.name[len('f0-track-'):].split('-track-stop-')[0] for p in S.glob('f0-track-*-track-stop-*.json')}):
  files=list(S.glob('f0-track-'+label+'-track-stop-*.json'))
  if not files:continue
  rows=json.loads(files[-1].read_text(encoding='utf-8-sig'));brief=[];last=None
  for i,row in enumerate(rows):
   key=tuple(row.get(k) for k in ['inside','entering','exiting','hidden','viewActive'])
   ids=[b['id'] for b in row['bindings']];key=key+(tuple(sorted(ids)),)
   if key!=last or i in [0,len(rows)-1]:brief.append({k:row.get(k) for k in ['frame','seconds','inside','entering','exiting','hidden','viewActive','fallback','acquired','released']}|dict(bodyIds=ids))
   last=key
  startIds={b['id'] for b in rows[0]['bindings']};afterIds={b['id'] for row in rows if row['inside'] and row['viewActive'] for b in row['bindings']}
  traces[label]=dict(samples=len(rows),transitions=brief,entryHadOwnedBodies=bool(startIds),releasedDuringFade=any(r.get('entering') and not r['bindings'] and r['hidden'] for r in rows),sameIdsReacquired=sorted(startIds&afterIds),duplicatesObserved=any(len(r['bindings'])!=len({b['nativeId'] for b in r['bindings'] if b['nativeId']}) for r in rows),allResidentCounts=sorted({r['citizens'] for r in rows if r['citizens'] is not None}))
 contracts={n:get(n+'-contract') for n in [p.stem[:-len('-contract')] for p in S.glob('*-contract.json')] if 'topLevelFields' in get(n+'-contract')}
 if (S/'candidate-save-contract.json').exists():contracts['candidate-save']=get('candidate-save-contract')
 put('functional-summary',dict(role='rig_test',method='native bridge actions and passive body/transition observations; seam, not gesture proof',states=states,traces=traces,contracts=contracts,allContractsSchema16=all(v['liveSchema']==v['savedSchema']==16 for v in contracts.values()),allSavedFieldsEqual=len({tuple(v['topLevelFields']) for v in contracts.values()})==1,allSavedInteriorFieldsAbsent=all(not v['interiorPropertyNames'] for v in contracts.values())))
 # Logical values are private; only equality, counts, and identity digests leave stage.
 names=['candidate-content-initial','candidate-saved','candidate-roundtrip-initial']
 if all((S/('payload-'+n+'.json')).exists() for n in names):
  snapshots={}
  for n in names:
   j=get('payload-'+n);ids=sorted(c['Identity']['Id'] for c in j['Citizens']);cars=sorted((c['Identity']['Id'],c['Car']['Id']) for c in j['Citizens'] if c.get('Car'))
   snapshots[n]=dict(schema=j['Schema'],minute=j['Minute'],residents=len(ids),uniqueResidents=len(set(ids)),residentIdsSha256=hashlib.sha256('\n'.join(ids).encode()).hexdigest(),cars=len(cars),uniqueCarIds=len({x[1] for x in cars}),carOwnershipSha256=hashlib.sha256(json.dumps(cars,separators=(',',':')).encode()).hexdigest())
  put('save-reload-summary',dict(role='rig_test',snapshots=snapshots,allResidentIdsPreserved=len({v['residentIdsSha256'] for v in snapshots.values()})==1,allCarOwnershipPreserved=len({v['carOwnershipSha256'] for v in snapshots.values()})==1,savedPayloadIdenticalToReloadSource=(S/'saved-candidate-saved.json').read_bytes()==(S/'saved-candidate-roundtrip-initial.json').read_bytes(),autosaveReverified=get('candidate-roundtrip-autosave-off')))

if __name__=='__main__':
 if (S/'candidate-reloaded-inspector-summary.json').exists():content()
 perf()
 functional()
