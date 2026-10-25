"""Verify matched PC evidence without contacting the game."""
from pathlib import Path
import json,sys,collections
root=Path(sys.argv[1] if len(sys.argv)>1 else 'build/routine-venues-20261023')
read=lambda n:json.loads((root/(n+'.json')).read_text())
results={}
for label in ['baseline','candidate']:
 start,end,audit=read(label+'-start'),read(label+'-final'),read(label+'-food-audit')
 count=end['purchases']-start['purchases'];day=int(start['minute']//1440)
 stock=lambda d,p:sum(c['amount'] for i in d['nativeItems'] for c in i.get('cargoInstances') or [] if c['itemName']==p)
 register=next(i for i in start['nativeItems'] if i['itemName']=='ba:itemname_cashregister');bag=register['cargoInstances'][0]['itemName']
 history=[h for h in end['history'] if h['dayNumber']==day]
 rows=audit['routineDiagnostics'];missed=[d for d in rows if d['Outcome']=='missed'];population=read(label+'-audit-end-population')
 activities=['Home','Work','Shop','Leisure','Study']
 visits={a:sum(v['Activity']==i and v['AtMinute']>=start['minute'] for c in population['Citizens'] for v in c['RecentVisits']) for i,a in enumerate(activities)}
 checks={'clock':abs(end['travelClockRate']-1/3)<1e-8,'oneDay':abs(end['minute']-start['minute']-1440)<1,
  'stock':stock(start,'ba:itemname_croissant')-stock(end,'ba:itemname_croissant')==count,
  'bags':stock(start,bag)-stock(end,bag)==count,'settled':not end['orders'] and sum(h['totalCustomers'] for h in history)==count,
  'noUncertain':not end['totals'].get('Uncertain'),'foodReturned':not end['outings'],
  'missCounterExplained':len(missed)==end['routines']['missed']-start['routines']['missed'],
  'uniqueDailyFoodIntentions':len({d['Intent']['Id'] for d in audit['decisions']})==len(audit['decisions'])}
 assert all(checks.values()),(label,checks)
 results[label]={'purchases':count,'revenue':sum(h['totalRevenue'] for h in history),'visits':visits,
  'journeys':end['routines']['journeys']-start['routines']['journeys'],'missed':len(missed),
  'missReasons':dict(collections.Counter(d['Reason'] for d in missed)),
  'missActivities':dict(collections.Counter(activities[d['Activity']] for d in missed)),
  'carriedInLateArrivals':sum(d['Reason']=='arrival-after-deadline' and d['Deadline']<=start['minute'] for d in missed),
  'newOptionalArrivalMisses':sum(d['Activity'] in [2,3] and d['Reason'] in ['arrival-after-deadline','arrival-no-opening'] and d['Deadline']>start['minute'] for d in missed),
  'diagnostics':dict(collections.Counter(d['Outcome']+':'+d['Reason'] for d in rows)),
  'simulation':end['clock'],'checks':checks}
a,b=read('baseline-start'),read('candidate-start')
pa,pb=read('baseline-audit-start-population'),read('candidate-audit-start-population')
assert a['minute']==b['minute'] and a['nativeItems']==b['nativeItems'] and a['nativeShifts']==b['nativeShifts'] and a['scope']==b['scope']
# Loading the old normal-clock checkpoint retimes cached commutes using live
# native navigation. Record that nuisance variation instead of claiming bitwise
# identical estimates; all other persisted fields must still match exactly.
assert {k:v for k,v in pa.items() if k!='Citizens'}=={k:v for k,v in pb.items() if k!='Citizens'}
assert len(pa['Citizens'])==len(pb['Citizens'])
commute_changes=[]
for x,y in zip(pa['Citizens'],pb['Citizens']):
 assert {k:v for k,v in x.items() if k not in ['CommutePreparation','NextEventMinute']}=={k:v for k,v in y.items() if k not in ['CommutePreparation','NextEventMinute']}
 cx,cy=x['CommutePreparation'],y['CommutePreparation']
 assert {k:v for k,v in cx.items() if k!='TravelMinutes'}=={k:v for k,v in cy.items() if k!='TravelMinutes'}
 travel_delta=cy['TravelMinutes']-cx['TravelMinutes'];departure_delta=y['NextEventMinute']-x['NextEventMinute']
 if departure_delta:assert travel_delta and abs(departure_delta+travel_delta)<.00001
 if travel_delta or departure_delta:commute_changes.append({'citizen':x['Identity']['Id'],'travelDeltaMinutes':travel_delta,'departureDeltaMinutes':departure_delta})
results['startingCommuteVariation']={'changedEstimates':len(commute_changes),
 'changesAboveOneMillisecond':sum(abs(r['travelDeltaMinutes'])>1/60000 for r in commute_changes),
 'maximumAbsoluteGameMinutes':max([abs(r['travelDeltaMinutes']) for r in commute_changes] or [0]),
 'changes':commute_changes,'allOtherPopulationFieldsIdentical':True,
 'note':'Same saved checkpoint; cached commutes retime from the old clock at load against live native routes. This limits causal precision of the native comparison.'}
reload=read('settled-reload-checks');assert all(reload.values())
results['sameCheckpointStockStaffAndResidentAssignments']=True;results['reloadAndDuplicateProtection']=reload
results['scope']='Two full PC game days from one shared checkpoint at one-third calendar speed. Physical population disabled; diagnostic work included, not a rendered FPS clearance. Optional shopping/leisure visits still do not directly purchase.'
(root/'comparison.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps(results,indent=2))
