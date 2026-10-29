from pathlib import Path
from collections import Counter,defaultdict
import json,math,hashlib
s=Path(__file__).parent
KINDS={0:'QueueAtOpening',1:'WaitToCross',2:'Break',3:'WaitForSomeone',4:'Conversation',5:'WindowShopping',6:'Audience',7:'Resting',8:'WaitForPickup',9:'WaitForBus'}
def wilson(k,n):
 if not n or not 0<=k<=n:return None
 z=1.959963984540054;p=k/n;d=1+z*z/n;a=(p+z*z/(2*n))/d;b=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d
 return [0.0 if k==0 else a-b,1.0 if k==n else a+b]
def stats(eligible,stops):
 n=len(eligible);k=len(stops);return dict(eligible=n,scheduledRecipients=k,share=k/n if n else None,wilson95=wilson(k,n))
out={}
for p in sorted(s.glob('*-trace-start-*-optional.jsonl')):
 label=p.name.split('-trace-start-')[0]
 selected=s/(label+'-trace-start.json')
 if not selected.exists() or Path(json.loads(selected.read_text())['output']).name!=p.name:continue
 rows=[json.loads(x) for x in p.read_text(encoding='utf-8-sig').splitlines()]
 choices=[r['value'] for r in rows if r['kind']=='choice'];a=[r['value'] for r in rows if r['kind']=='admission'];stops=[r['value'] for r in rows if r['kind']=='scheduled']
 start=next(r['value'] for r in rows if r['kind']=='start');end=next((r['value'] for r in reversed(rows) if r['kind']=='end'),None)
 if end is None:continue
 opening=start['audit']['pending'];closing=end['audit']['pending'];t0=start['audit']['tally'];t1=end['audit']['tally'];delta={k:t1[k]-t0[k] for k in t1} if t1 else {}
 result=stats(choices,stops)
 result.update(inputSha256=hashlib.sha256(p.read_bytes()).hexdigest(),rawFile=p.name,openingPending=opening,closingPending=closing,tallyDelta=delta,drawn=sum(x['accepted'] for x in choices),observerFaults=end['faults'],admissionCount=len(a),scheduledUnique=len({x['key'] for x in stops}),kind=dict(Counter(KINDS.get(x['stop']['Kind'],str(x['stop']['Kind'])) for x in stops)),venue=dict(Counter(x['place'] for x in stops)),sources=dict(Counter(x['source'] for x in stops)),conversationMembers=sum(len(x['stop']['Members'])>0 for x in stops))
 result['auditExact']=delta.get('Drawn')==delta.get('Realised',0)+closing-opening+delta.get('Dropped',0) and delta.get('Eligible')==len(choices) and delta.get('Realised')==len(stops)
 result['conversations']=len({(x['stop']['TargetId'],x['stop']['StartMinute']) for x in stops if x['stop']['Members']})
 trims=[r['value'] for r in rows if r['kind']=='drop'];no_recipient=sum(not r['value']['available'] for r in rows if r['kind']=='pass-attempt');pol=sum(x.get('dropped',0) for x in a if x.get('percentage')==100)-sum(x['reason']=='100-policy-trim' for x in trims)
 result['dropsByCause']={'trim':sum(x['reason']=='trim' for x in trims),'100Policy':sum(x['reason']=='100-policy-trim' for x in trims)+pol,'unavailableRecipient':no_recipient,'capExhaustion':delta.get('Dropped',0)-len(trims)-no_recipient-pol};result['trimOrder']=trims
 hourly=[];daily=[]
 for width,target in [(60,hourly),(1440,daily)]:
  bins=range(int(start['minute']//width),int(end['minute']//width)+1)
  last_pending=opening
  for b in bins:
   aa=[x for x in a if int(x['minute']//width)==b];cc=[x for x in choices if int(x['minute']//width)==b];ss=[x for x in stops if int(x['minute']//width)==b]
   if aa:last_pending=aa[-1]['pendingAfter']
   row=stats(cc,ss);row.update(bin=b,pendingEnd=last_pending,drawn=sum(x['accepted'] for x in cc),dropped=sum(x['dropped'] for x in aa));target.append(row)
 result['hourly']=hourly;result['daily']=daily
 daily24=[];previous=t0;stock=opening
 for f in sorted(s.glob(label+'-d*-closing.json'),key=lambda f:int(f.name.split('-d')[-1].split('-')[0])):
  audit=json.loads(f.read_text())['audit'];t=audit['tally'];n=t['Eligible']-previous['Eligible'];k=t['Realised']-previous['Realised']
  daily24.append(dict(day=int(f.name.split('-d')[-1].split('-')[0]),eligible=n,scheduledRecipients=k,share=k/n if n else None,wilson95=wilson(k,n),openingPending=stock,closingPending=audit['pending'],drawn=t['Drawn']-previous['Drawn'],dropped=t['Dropped']-previous['Dropped']))
  stock=audit['pending'];previous=t
 result['daily24h']=daily24
 ids=sorted(c['Identity']['Id'] for c in json.loads((s/'schema13-fixture.private.json').read_text())['Citizens']);per={ident:stats([x for x in a if x['id']==ident],[x for x in stops if x['id']==ident]) for ident in ids}
 distribution={}
 for pc in [.02,.05]:
  group=set(ids[:max(1,math.ceil(len(ids)*pc))]);distribution[str(pc)]={'lowest':stats([x for x in a if x['id'] in group],[x for x in stops if x['id'] in group]),'rest':stats([x for x in a if x['id'] not in group],[x for x in stops if x['id'] not in group]),'citizens':len(group)}
 result['identityDistribution']=distribution
 (s/(label+'-per-citizen.json')).write_text(json.dumps(per,indent=2))
 result['metrics']=end['metrics'];result['visibilityClaim']='Scheduled recipients measured during native time skips; these do not establish a visible dwell census. Normal-speed physical evidence is separate.'
 out[label]=result
 print(label,result['scheduledRecipients'],result['eligible'],result['share'],result['wilson95'],'pending',opening,closing,'audit',result['auditExact'],flush=True)
(s/'rate-summary.json').write_text(json.dumps(out,indent=2))
