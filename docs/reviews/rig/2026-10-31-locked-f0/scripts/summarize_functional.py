from pathlib import Path
import json,math,hashlib,re
S=Path(__file__).parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def point(p):
 if not p:return None
 return tuple(float(p.get(k,p.get(k.upper(),0))) for k in ['x','y','z'])
def distance(a,b):return math.sqrt(sum((x-y)**2 for x,y in zip(a,b)))
def counts(row):return {k:sum(b['kind']==k for b in row.get('bindings',[])) for k in ['pedestrian','car']}
def motion(rows,key,idkey):
 seen={};maxima={}
 for row in rows:
  for obj in row.get(key,[]):
   p=point(obj.get('position'));ident=str(obj[idkey])
   if p is None:continue
   if ident in seen:maxima[ident]=max(maxima.get(ident,0),distance(seen[ident],p))
   else:seen[ident]=p
 return {'identities':len(seen),'identitiesSeenTwice':len(maxima),'movingMoreThan5cm':sum(d>.05 for d in maxima.values()),'maximumDisplacement':max(maxima.values(),default=0),'movingIds':[k for k,v in maxima.items() if v>.05]}
traces=[]
for f in sorted(S.glob('f0-track-*.json')):
 rows=read(f)
 if not rows:continue
 phases=[];previous=None
 for row in rows:
  phase=tuple(row.get(k) for k in ['inside','entering','exiting','viewActive','hidden'])
  if phase!=previous:phases.append({k:row.get(k) for k in ['frame','seconds','inside','entering','exiting','viewActive','hidden','address','fallback']}|{'bodies':counts(row)});previous=phase
 active=[r for r in rows if r.get('inside') and r.get('viewActive')]
 insideRows=[r for r in rows if r.get('inside') and not r.get('entering') and not r.get('exiting')]
 radii=[distance(point(b['position']),point(r['anchor'])) for r in active for b in r['bindings'] if point(b.get('position')) is not None and point(r.get('anchor')) is not None]
 outside={b['id'] for r in rows if not r['inside'] for b in r['bindings']};inside={b['id'] for r in active for b in r['bindings']}
 duplicateIds=[r['frame'] for r in rows if len({b['id'] for b in r['bindings']})!=len(r['bindings'])]
 duplicateBodies=[]
 for r in rows:
  ids=[b.get('nativeId') for b in r['bindings'] if b.get('nativeId')]
  if len(set(ids))!=len(ids):duplicateBodies.append(r['frame'])
 traces.append({'file':f.name,'samples':len(rows),'seconds':rows[-1]['seconds']-rows[0]['seconds'],'firstFrame':rows[0]['frame'],'lastFrame':rows[-1]['frame'],'phases':phases,'maxPedestrians':max(counts(r)['pedestrian'] for r in rows),'maxCars':max(counts(r)['car'] for r in rows),'activeMaxPedestrians':max((counts(r)['pedestrian'] for r in active),default=0),'activeMaxCars':max((counts(r)['car'] for r in active),default=0),'maximumActiveBodyDistance':max(radii,default=None),'reacquiredFromOutsideIds':sorted(outside&inside),'ownedMotion':motion(active,'bindings','id'),'firstInsideFrame':next((r['frame'] for r in rows if r['inside']),None),'firstReleasedDuringEntryFrame':next((r['frame'] for r in rows if r['inside'] and r['entering'] and not r['bindings']),None),'firstViewActiveFrame':next((r['frame'] for r in active),None),'nativeTrafficMotion':motion(insideRows,'nativeCars','id'),'nativeOrdinaryPedestrianMotion':motion(insideRows,'nativePeople','id'),'nativeCarMax':max((len(r['nativeCars']) for r in insideRows),default=0),'nativePedestrianMax':max((len(r['nativePeople']) for r in insideRows),default=0),'duplicateResidentFrames':duplicateIds,'duplicateNativeBodyFrames':duplicateBodies})
budgets=[]
for f in sorted(S.glob('f0-budget-*.json')):
 a=read(f);rows=[r for r in a if 'attempts' in r];errors=[r for r in a if 'error' in r]
 budgets.append({'file':f.name,'selectionPasses':len(rows),'maximumAttemptsPerPass':max((r['attempts'] for r in rows),default=0),'minimumObservedPassInterval':min((b['seconds']-a['seconds'] for a,b in zip(rows,rows[1:])),default=None),'errors':errors,'maxPedestrians':max((counts({'bindings':r['bodies']})['pedestrian'] for r in rows),default=0),'maxCars':max((counts({'bindings':r['bodies']})['car'] for r in rows),default=0),'totalAttempts':sum(r['attempts'] for r in rows),'acquisitionDelta':rows[-1]['acquired']-rows[0]['acquired'] if rows else 0,'releaseDelta':rows[-1]['released']-rows[0]['released'] if rows else 0,'refusalDelta':rows[-1]['unavailable']-rows[0]['unavailable'] if rows else 0})
states={};contracts={}
for f in S.glob('*.json'):
 if f.name.startswith(('response-','installed-','fixture-')):continue
 try:r=read(f)
 except Exception:continue
 if isinstance(r,dict) and 'ids' in r and 'inside' in r and 'citizens' in r:
  ids=r.get('ids') or [];out={k:r.get(k) for k in ['frame','seconds','day','hour','minute','timeScale','inside','entering','exiting','address','size','version','elevator','floor','configured','viewActive','fallback','anchor','revision','hidden','physical','diagnostics','citizens','failed','status','payload','acquired','released','bindings']}
  out['uniqueResidents']=len(set(ids));out['residentIdsSha256']=hashlib.sha256('\n'.join(sorted(ids)).encode()).hexdigest()
  light=r.get('lighting',{});out['lighting']={k:v for k,v in light.items() if k!='volumeObjects'};out['lighting']['volumeStates']=sorted((v['enabled'],v['active']) for v in light.get('volumeObjects',[]))
  out['nativePedestrianCount']=len(r.get('nativePeople',[]));out['nativeCarCount']=len(r.get('nativeCars',[]));states[f.stem]=out
 if isinstance(r,dict) and r.get('key')=='persistentcity:state_v1' and 'topLevelFields' in r:contracts[f.stem]=r
result={'role':'rig_test','states':states,'traces':traces,'admissionBudgets':budgets,'saveContracts':contracts,'note':'Transition and budget instrumentation is functional evidence, disabled during primary performance. Phase snapshots may precede the next mod Update; frame-adjacent release states are retained. Native-car totals include resident-owned native vehicles, distinguished by nativeId in body records.'}
(S/'functional-summary.json').write_text(json.dumps(result,indent=2))
print('Summarized',len(states),'states,',len(traces),'traces,',len(budgets),'budget captures,',len(contracts),'save contracts')
