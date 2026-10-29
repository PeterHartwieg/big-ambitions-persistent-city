from pathlib import Path
import json,math
s=Path(__file__).parent;cases=json.loads((s/'event-persistence-cases.json').read_text());out={}
for case in cases:
 label='event-'+case['event'];q=case.get('checkpoint')
 if not q:out[label]=dict(verdict='INCOMPLETE');continue
 traceLabel=case.get('traceLabel',label)
 meta=json.loads((s/(traceLabel+'-trace-start.json')).read_text());trace=s/Path(meta['output']).name;rows=[json.loads(x) for x in trace.read_text().splitlines()]
 cleared=[r['value'] for r in rows if r['kind']=='pending-cleared' and r['value']['key']==q['key']]
 transfers=[r['value'] for r in rows if r['kind']=='pass-attempt' and r['value'].get('donor')==q['id'] and r['value']['minute']==q['minute']]
 result=dict(checkpoint=q,donorClears=cleared,passAttempts=transfers)
 if q['stop'] and len(q['stop']['Members'])==2:
  p=next(s.glob(label+'-control-pre-save-*-population.json'));d=json.loads(p.read_text());a,b=[next(c for c in d['Citizens'] if c['Identity']['Id']==ident)['OptionalStop'] for ident in q['stop']['Members']]
  pos=lambda st:[st['Position'][x] for x in ['X','Y','Z']]
  distance=math.dist(pos(a),pos(b));checks=dict(distinctMembers=len(set(a['Members']))==2,reciprocalMembers=a['Members']==b['Members'],sharedStartEnd=(a['StartMinute'],a['EndMinute'])==(b['StartMinute'],b['EndMinute']),distinctSafeSpots=distance>=.65,reciprocalFacing=all(a['Facing'][x]==b['Position'][x] and b['Facing'][x]==a['Position'][x] for x in ['X','Y','Z']))
  result.update(pairChecks=checks,spotDistance=distance,pairPassed=all(checks.values()))
 out[label]=result
(s/'event-detail-verification.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
