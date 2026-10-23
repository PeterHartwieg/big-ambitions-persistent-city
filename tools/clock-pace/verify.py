"""Validate the matched native Mac campaign; does not contact a game."""
from pathlib import Path
import json, hashlib, statistics, sys

root=Path(__file__).resolve().parents[2]/'build/clock-pace-mac-20261022/mac-run'
if len(sys.argv)>1:root=Path(sys.argv[1])
read=lambda name:json.loads((root/(name+'.json')).read_text())
results={}
for label,factor in [('baseline',1),('candidate',1/3)]:
    start,end,audit=read(label+'-start'),read(label+'-final'),read(label+'-food-audit')
    day=int(start['minute']//1440);count=end['purchases']-start['purchases']
    def stock(data,product):return sum(c['amount'] for i in data['nativeItems'] for c in i.get('cargoInstances') or [] if c['itemName']==product)
    register=next(i for i in start['nativeItems'] if i['itemName']=='ba:itemname_cashregister')
    bag=register['cargoInstances'][0]['itemName']
    history=[h for h in end['history'] if h['dayNumber']==day]
    checks={'clock_rate':abs(end['travelClockRate']-factor)<.00001,
        'one_day':abs(end['minute']-start['minute']-1440)<1,
        'pastries_conserved':stock(start,'ba:itemname_croissant')-stock(end,'ba:itemname_croissant')==count,
        'bags_conserved':stock(start,bag)-stock(end,bag)==count,
        'native_settlement':sum(h['totalCustomers'] for h in history)==count and not end['orders'],
        'no_uncertain':not end['totals'].get('Uncertain'),
        'all_returned':not end['outings'],
        'one_daily_decision':len({r['Intent']['Id'] for r in audit['decisions']})==len(audit['decisions'])}
    if label=='candidate':checks['driven_rides_adapter_installed']=end['clock']['drivenRides'] is True
    assert all(checks.values()),(label,checks)
    rows=audit['decisions'];timings=sorted(r['ElapsedMilliseconds'] for r in rows)
    reasons={}
    for r in rows:
        for reason in r['RouteChecks'].values():reasons[reason]=reasons.get(reason,0)+1
    population=read(label+'-audit-end-population')
    activities=['Home','Work','Shop','Leisure','Study']
    visits={name:sum(v['Activity']==i and v['AtMinute']>=start['minute'] for c in population['Citizens'] for v in c['RecentVisits']) for i,name in enumerate(activities)}
    results[label]={'purchases':count,'revenue':sum(h['totalRevenue'] for h in history),'decisions':len(rows),'admittedVisits':visits,
        'chosen':sum(r['Intent']['Status']==1 for r in rows),
        'routines':{k:end['routines'][k]-start['routines'][k] for k in ['journeys','missed']},
        'decisionWork':{'totalMs':sum(timings),'p95Ms':timings[int(len(timings)*.95)],'maxMs':max(timings),'routeCalls':sum(r['RouteCalls'] for r in rows)},
        'simulation':end['clock'],'routeChecks':reasons,'checks':checks}

a,b=read('baseline-start'),read('candidate-start')
assert abs(a['minute']-b['minute'])<1 and a['purchases']==b['purchases'] and a['scope']==b['scope']
assert a['nativeItems']==b['nativeItems'] and a['nativeShifts']==b['nativeShifts']
pa,pb=read('baseline-audit-start-population'),read('candidate-audit-start-population')
assert pa['Seed']==pb['Seed'] and pa['PopulationKey']==pb['PopulationKey']
assert [c['Identity'] for c in pa['Citizens']]==[c['Identity'] for c in pb['Citizens']]
results['comparison']={'sameCheckpointAndPopulation':True,'sameStockAndStaff':True,'startMinuteDifference':b['minute']-a['minute'],'physicalRendering':False,
    'note':'Native Mac headless comparison at the same native fast-forward speed. Calendar factor is the changed variable; resulting journeys, queues and decisions may differ. Timing includes diagnostic trace capture. No Windows or rendered-FPS claim.'}
reload=read('settled-reload');replay=read('paid-replay')
assert reload['purchases']==read('candidate-final')['purchases']
assert replay['paid'] and replay['before']['purchases']==replay['after']['purchases']
assert replay['before']['nativeItems']==replay['after']['nativeItems']
results['reloadAndPaidReplay']=True
(root/'comparison.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps(results,indent=2))
