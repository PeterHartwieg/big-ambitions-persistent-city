#!/usr/bin/env python3
"""Same-checkpoint neighborhood comparisons on the isolated Mac rig. No Windows access."""
from mac_rig import *
campaign=os.environ.get('PC_FOOD_CAMPAIGN','neighborhood-mac-20261023')
run=MacRig(campaign,mode=os.environ.get('PC_RIG_MODE','headless'))
small='address:ba%3Astreet_thirdstreet:30:shop';overlap='address:ba%3Astreet_thirdstreet:46:shop'
separated='address:ba%3Astreet_ninthavenue:20:shop';large='address:ba%3Astreet_fourthavenue:22:shop'
cases=[('one-small',{'shops':[small]},3),('two-overlapping',{'shops':[small,overlap]},2),
 ('two-separated',{'shops':[small,separated]},2),('larger',{'shops':[large]},2),
 ('higher-price',{'shops':[small],'priceFactor':1.5},2),('lower-service',{'shops':[small],'skill':25},2),
 ('longer-hours',{'shops':[small],'opens':6,'closes':22},2),('lunch-hours',{'shops':[small],'opens':10,'closes':14},2)]
if os.environ.get('PC_CASES'):
 selected=set(os.environ['PC_CASES'].split(','));assert selected<={c[0] for c in cases};cases=[c for c in cases if c[0] in selected]
base='FT Food Routines Neighborhood Base 20261023'
def stable(key,value):
 value=json.loads(json.dumps(value))
 if key=='economy':value.pop('minute',None)
 if key=='shops':
  for shop in value:shop['supply'].pop('AtMinute',None)
 return value
try:
 run.stage('preparing-mac');run.begin()
 if os.environ.get('PC_DIAGNOSE_FIXTURE_SAVE'):
  run.load('diagnose-fixture',os.environ['PC_DIAGNOSE_FIXTURE_SAVE']);run.record('prior-fixture-staffing',run.probe('fixture-staffing'))
 run.load('pre-food',base);run.record('candidate-catalogue',run.probe('neighborhood-candidates'))
 for caseIndex,(label,arguments,days) in enumerate(cases):
  run.stage('setup-'+label);start=run.ready(base) if caseIndex==0 else run.load(label,base);assert start['scope'] is None
  setup=run.record(label+'-setup',run.probe('neighborhood-setup',**arguments))
  for shop in setup['staffing']:
   assert len(shop['days'])==7 and all(d['isOpen'] and d['shifts'] for d in shop['days']),('no weekly shifts',shop)
   assert all(w['employeeExists'] and w['stationExists'] for d in shop['days'] for w in d['shifts']),('broken station assignment',shop)
   assert any(w['employee'] for w in shop['middayStaff']),('no native midday service',shop)
  employees=[w['employeeId'] for shop in setup['staffing'] for w in shop['days'][0]['shifts']]
  assert len(set(employees))==len(employees),('employee shared between fixtures',employees)
  print(label,'capacities',[(f['customerCapacity'],f['buildingCapacity']) for f in setup['fixtures']],flush=True)
  checkpoint='FT Food Routines Mac Neighborhood '+label+' Start 20261023'
  run.call(label+'-save-start','save',{'name':checkpoint})
  for day in range(1,days+1):
   prefix=label+'-day-'+str(day);run.stage(prefix)
   start=run.record(prefix+'-start',run.ready());assert int(start['minute']%1440)<480
   run.record(prefix+'-audit-start',run.probe('audit-start'))
   run.record(prefix+'-skip-needs-start',run.probe('skip-needs-start'))
   started=time.monotonic();run.call(prefix+'-native-skip','console',{'line':'skiptime 1d true'});time.sleep(2)
   final=run.ready();run.record(prefix+'-skip-needs-end',run.probe('skip-needs-end'))
   run.record(prefix+'-final',final);assert abs(final['nativeMinute']-start['nativeMinute']-1440)<.1
   run.record(prefix+'-skip-timing',{'seconds':time.monotonic()-started,'minutes':final['nativeMinute']-start['nativeMinute'],
      'heldTicks':final['clock']['heldEconomySkipTicks']-start['clock']['heldEconomySkipTicks']})
   run.record(prefix+'-audit-end',run.probe('audit-end'))
   for name in ['food-audit.json','audit-start-population.json','audit-end-population.json']:shutil.copy2(run.helper/name,run.root/(prefix+'-'+name))
   print(prefix,'purchases',final['purchases']-start['purchases'],'seconds',round(time.monotonic()-started,1),flush=True)
   checkpoint='FT Food Routines Mac Neighborhood '+label+' Day '+str(day)+' 20261023'
   run.call(prefix+'-save','save',{'name':checkpoint})
  run.record(label+'-report',run.probe('economy-report'))
  before=run.record(label+'-before-reload',run.probe('status'));after=run.record(label+'-after-reload',run.load(label+'-reload',checkpoint))
  for key in ['purchases','spendingCents','economy','accounts','shops']:assert stable(key,before[key])==stable(key,after[key]),(label,'reload changed',key)
  if any(m['Status']==3 for m in after['active']):
   replay=run.record(label+'-receipt-replay',run.probe('replay-paid'));assert replay['paid']
   for key in ['purchases','spendingCents','economy','accounts','shops']:assert replay['before'][key]==replay['after'][key],(label,'replay changed',key)
  run.stage('verified-'+label)
 run.record('comparison-complete',{'cases':[c[0] for c in cases],'days':sum(c[2] for c in cases),'platform':'Mac','mode':run.mode})
except BaseException as error:
 run.record('error',{'type':type(error).__name__,'error':str(error)[:8000]});raise
finally:run.close()
