from pathlib import Path
import json,math
import rig,drive,functional as f,a3,suite3
S=Path(__file__).parent
picks=json.loads((S/'inflight-selection.json').read_text())['picks']
def go(label,target):
 day=int(target//1440);minute=int(target-day*1440);a3.until(label,day,minute//60,minute%60,speed=1)
def state(label,consumer):
 drive.export(label)
 v=json.loads((S/('semantic-'+label+'.json')).read_text());m=next(x for x in v['Food']['Residents']if x['ResidentId']==consumer);person=next(x for x in v['Citizens']if x['Identity']['Id']==m['AnchorId']);return v,m,person
for slot,pick in picks.items():
 consumer=pick['ResidentId'];source=suite3.N+'Rush24'
 if not(S/('semantic-uninterrupted-'+slot+'-end.json')).exists():
  f.reload('uninterrupted-'+slot+'-source',source);rig.probe('uninterrupted-'+slot+'-external-start','rig2',True,{'action':'external-start'});a3.until('uninterrupted-'+slot+'-end',7,14,0,speed=6);rig.probe('uninterrupted-'+slot+'-external-stop','rig2',True,{'action':'external-stop'});drive.export('uninterrupted-'+slot+'-end')
 for phase in ['outward','serving','paid','return']:
  label='D4Rival'+slot+' '+phase;name='FT Food Routines Locked Combined '+label+' 20261102'
  if phase=='outward' and(S/('savedsemantic-'+label+'-saved.json')).exists():source=name;continue
  f.reload(label+'-clock-source',source);rig.probe(label+'-clock-external-start','rig2',True,{'action':'external-start'})
  _,m,p=state(label+'-clock-before',consumer)
  if phase=='serving':go(label+'-clock',math.ceil(m['DueAt'])+1)
  elif phase=='paid':go(label+'-clock',math.ceil(m['ServiceAt'])+1)
  elif phase=='return':go(label+'-clock',math.ceil(m['ServiceAt']+20)+1)
  v,m,p=state(label+'-clock-phase',consumer)
  observed=(m['Status']==1 if phase=='outward'else m['Status']==2 if phase=='serving'else m['Status']==3 if phase=='paid'else bool((m['Excursion']or{}).get('Returning')or(p['MealOuting']or{}).get('Returning')))
  rig.record(label+'-clock-trigger',dict(method='Ordinary native bridge run-until minute, pause, then normal native save; no outcome or money injection',consumer=consumer,phase=phase,observed=observed,minute=v['Minute'],status=m['Status'],context=m['LastContextKey'],memory=m['Memory']))
  print('CLOCK PHASE',slot,phase,observed,v['Minute'],m['Status'],flush=True)
  if not observed:
   rig.probe(label+'-external-audit','rig2',True,{'action':'external-stop'});break
  f.save(label+'-clock-save',name);drive.export(label+'-saved')
  rig.probe(label+'-external-audit','rig2',True,{'action':'external-stop'})
  source=name
 f.reload('interrupted-'+slot+'-return-source',source);a3.until('interrupted-'+slot+'-end',7,14,0,speed=6);drive.export('interrupted-'+slot+'-end')
 print('CLOCK JOURNEY CHAIN COMPLETE',slot,consumer,flush=True)
suite3.day();suite3.variants()
print('C1-C5 MAIN NATIVE WORK COMPLETE; GAME PAUSED',flush=True)
