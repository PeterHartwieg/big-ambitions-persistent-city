from pathlib import Path
import json,re,hashlib,math
S=Path(__file__).parent
def read(name):return json.loads((S/name).read_text(encoding='utf-8-sig'))
logs=[];spikes=[];unmapped=[]
for p in sorted(S.glob('*-Player.private.log')):
 text=p.read_text(encoding='utf-8',errors='replace');lines=text.splitlines()
 bad=[l for l in lines if re.search(r'Persistent City stopped:|Physical population unavailable;|\[Persistent City\].*Not active|InteriorCityView hidden fallback: exception;',l,re.I)]
 pcStacks=[];nativeStacks=[]
 for match in re.finditer(r'(?m)^(?:[A-Za-z_][\w.]*\.)?\w*Exception(?::|\s)',text):
  block=text[match.start():].split('\n\n',1)[0]
  block=block.split('\n[',1)[0]
  if 'PersistentCity.' in block:pcStacks.append(block)
  else:nativeStacks.append(block[:6000])
 evidence=[l for l in lines if 'InteriorCityView' in l or 'Physical population active:' in l or 'Ordinary ambient pedestrian requests:' in l]
 logs.append({'run':p.name.replace('-Player.private.log',''),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'unexpectedPersistentCityMessages':bad,'persistentCityExceptionStacks':pcStacks,'otherExceptionStacks':nativeStacks,'interiorAndOwnershipEvidence':evidence})
 for l in lines:
  m=re.search(r'Slow routing frame=(\d+), realtime=([\d.]+), prepared=(\w+), preparation_ms=([\d.]+), closure_ms=([\d.]+)',l)
  if m:
   prep,closure=float(m[4]),float(m[5])
   if prep+closure>=30:spikes.append({'run':logs[-1]['run'],'unityFrame':int(m[1]),'realtime':float(m[2]),'phase':'routing preparation/closure','window':'outside primary: startup/load/setup','minimumObservedModCostMs':prep+closure,'preparationMs':prep,'closureMs':closure})
  m=re.search(r'([\d.]+) ms maximum advance',l)
  if m and float(m[1])>=30:unmapped.append({'run':logs[-1]['run'],'phase':'background advancement summary','window':'outside primary / broad log summary, no frame mapping','maximumAdvanceMs':float(m[1])})
(S/'log-summary.json').write_text(json.dumps({'role':'rig_test','runs':logs,'unexpectedModMessages':sum(len(x['unexpectedPersistentCityMessages']) for x in logs),'persistentCityExceptionStacks':sum(len(x['persistentCityExceptionStacks']) for x in logs),'intentionalFailureWarning':'The direct FailClosed seam uses fallback enum Exception; its one warning is expected and is not a thrown exception.'},indent=2))
(S/'outside-window-spikes.json').write_text(json.dumps({'role':'rig_test','measuredNormalPlaySpikes':'See performance-summary.json; every >=30 ms sample has frame/phase there.','routingLowerBounds':spikes,'unmappedAdvanceSummaries':unmapped,'scope':'These are logged component costs outside primary windows, not complete per-frame totals. Cold native preparation, time jumps and test diagnostics are separate from the normal-play p95 gate.'},indent=2))
functional=read('functional-summary.json');budgets=[]
for p in S.glob('f0-budget-*.json'):
 rows=read(p.name)
 if 'on-native-activity-' not in p.name:continue
 anchor=functional['states']['on-native-activity-state']['anchor'];xyz=lambda o:tuple(o[k] for k in ['X','Y','Z']);previous={};admissions=[];edgeDepartures=[];lastReleased={};churn=[]
 for i,row in enumerate(rows):
  if 'bodies' not in row:continue
  current={b['id']:b for b in row['bodies']}
  if i:
   for ident,b in current.items():
    if ident not in previous or previous[ident].get('body')!=b.get('body'):
     radius=math.dist(xyz(b['position']),xyz(anchor));admissions.append({'resident':ident,'frame':row['frame'],'radius':radius})
     if ident in lastReleased and row['seconds']-lastReleased[ident]['seconds']<=2 and lastReleased[ident]['radius']>=55:churn.append({'resident':ident,'frame':row['frame'],'admissionRadius':radius})
   for ident,b in previous.items():
    if ident not in current:
     radius=math.dist(xyz(b['position']),xyz(anchor));lastReleased[ident]={'seconds':row['seconds'],'radius':radius}
     if radius>=58:edgeDepartures.append({'resident':ident,'lastSampleRadius':radius,'absentByFrame':row['frame']})
  previous=current
 budgets.append({'source':p.name,'recordedNewAdmissions':len(admissions),'maximumRecordedAdmissionRadius':max((a['radius'] for a in admissions),default=None),'newAdmissionSamples':admissions,'nearOuterEdgeDepartures':edgeDepartures,'nearEdgeReacquisitionsWithin2Seconds':churn,'note':'Departure radii are the previous sample, so crossing the strict release boundary is inferred rather than an injected boundary proof.'})
(S/'radius-edge-summary.json').write_text(json.dumps({'role':'rig_test','captures':budgets},indent=2))
print('Unexpected mod diagnostics',sum(len(x['unexpectedPersistentCityMessages']) for x in logs),'PC stacks',sum(len(x['persistentCityExceptionStacks']) for x in logs),'outside-window routing >=30 ms',len(spikes),'radius',[(x['maximumRecordedAdmissionRadius'],len(x['nearOuterEdgeDepartures']),len(x['nearEdgeReacquisitionsWithin2Seconds'])) for x in budgets])
