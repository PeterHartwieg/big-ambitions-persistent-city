from pathlib import Path
import sys,time,shutil,json,subprocess,hashlib,math
import rig,run_review as r,f0_driver as d
S=Path(__file__).parent
def reload(label,name):
 rig.call(label+'-load','load',{'name':name});rig.waitcity(label+'-loaded',name)
 rig.call(label+'-pause','time',{'pause':True});rig.probe(label+'-autosave-off','autosave-off',True)
 while True:
  st=rig.probe(label+'-background-'+str(time.time_ns()),'runtime-state')
  if st.get('failed'):raise RuntimeError(st)
  if str(st.get('status','')).startswith('Persistent population active:'):break
  time.sleep(1)
def export(label):
 rig.probe(label+'-start','audit-start')
 src=rig.PROFILE/'ModsLocal/FoodDemandAuditProbe/audit-start-population.json';dest=S/(label+'.private.json');shutil.copy2(src,dest)
 rig.probe(label+'-end','audit-end')
 return json.loads(dest.read_text(encoding='utf-8-sig'))
reload('logical-inside','FT Food Routines Locked F0 On Saved 20261031')
a=export('logical-before');d.snapshot('logical-inside-before')
rig.call('logical-normal','time',{'speed':1,'pause':False,'keep_needs':True});d.exitshop('logical-exit');d.enter('logical-reentry')
rig.call('logical-inside-save','save',{'name':'FT Food Routines Locked F0 Continuity Saved 20261031'})
reload('logical-reloaded','FT Food Routines Locked F0 Continuity Saved 20261031')
b=export('logical-after');d.snapshot('logical-inside-after')
def contract(j):
 ids=sorted(c['Identity']['Id'] for c in j['Citizens']);cars=sorted((c['Identity']['Id'],c['Car']['Id']) for c in j['Citizens'] if c.get('Car'))
 return {'schema':j['Schema'],'residents':len(ids),'uniqueResidents':len(set(ids)),'residentIdsSha256':hashlib.sha256('\n'.join(ids).encode()).hexdigest(),'cars':len(cars),'uniqueCarIds':len({x[1] for x in cars}),'carOwnershipSha256':hashlib.sha256(json.dumps(cars,separators=(',',':')).encode()).hexdigest(),'carOwnershipSamples':cars[:8],'minute':j['Minute'],'completedJourneys':sum(c.get('CompletedJourneys',0) for c in j['Citizens']),'trafficProgressSamples':[{k:v[k] for k in ['VehicleId','RouteIndex','EnqueuedAt','EnteredAt','ReadyAt','MotionStartedAt','MotionStartFraction','PhysicalControl'] if k in v} for v in (j.get('Traffic') or {}).get('Vehicles',[])][:8],'travelling':sum(c.get('Journey') is not None for c in j['Citizens']),'sampleJourneys':[{'resident':c['Identity']['Id'],'carId':(c.get('Car') or {}).get('Id'),'phase':c.get('Phase'),'departureMinute':c.get('DepartureMinute'),'elapsedSinceDeparture':j['Minute']-c.get('DepartureMinute',j['Minute']),'routeMinutes':sum(leg['Minutes'] for leg in c['Journey']['Legs']),'nextEventMinute':c.get('NextEventMinute')} for c in j['Citizens'] if c.get('Journey')][:8]}
x,y=contract(a),contract(b)
rig.record('logical-continuity-summary',{'role':'rig_test','before':x,'after':y,'residentIdsPreserved':x['residentIdsSha256']==y['residentIdsSha256'],'carOwnershipPreserved':x['carOwnershipSha256']==y['carOwnershipSha256'],'method':'Native inside load, export owned logical state via existing audit seam, exit/re-enter, native inside save, reload, export. Exports private; only digests and small ID samples published. No performance window or save profiling.'})
print('LOGICAL CONTINUITY',x['residents'],y['residents'],'cars',x['cars'],y['cars'],'resident IDs',x['residentIdsSha256']==y['residentIdsSha256'],'car ownership',x['carOwnershipSha256']==y['carOwnershipSha256'],flush=True)
