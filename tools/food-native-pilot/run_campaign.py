"""Run only the explicitly owned disposable pilot; cleanup is session.py restore."""
from protocol import *

def wait_health(predicate, timeout=300):
 until=time.monotonic()+timeout;last=0
 while True:
  h=health()
  if predicate(h):return h
  if time.monotonic()>until:raise RuntimeError('Timed out: '+str(h))
  if time.monotonic()-last>30:
   print('Rig:',h.get('scene'),h.get('day'),h.get('time'),flush=True);last=time.monotonic()
  time.sleep(3)

def until(day,clock):
 checked('advance-'+str(day)+'-'+clock.replace(':',''),'time',{'run_until':{'day':day,'time':clock},'speed':1,'keep_needs':True})
 record('fast-forward',probe_call('fast-forward'))
 h=wait_health(lambda h:h.get('day',0)>=day and h.get('paused'),1800)
 checked('pause-'+str(day),'time',{'pause':True,'speed':1,'keep_needs':True})
 record('normal-'+str(day),probe_call('normal'))
 target=day*1440+int(clock.split(':')[0])*60+int(clock.split(':')[1])
 deadline=time.monotonic()+900;last=0
 while True:
  progress=probe_call('status',30)
  if progress.get('minute',-1)>=target:break
  if time.monotonic()>deadline:raise RuntimeError('Population failed to catch up: '+str(progress))
  if time.monotonic()-last>30:print('Paused population catch-up:',progress,flush=True);last=time.monotonic()
  time.sleep(3)
 return h

try:
 wait_health(lambda h:h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done')
 checked('pause-start','time',{'pause':True,'speed':1,'keep_needs':True})
 end=time.monotonic()+240
 while True:
  inventory=probe_call('inventory',30)
  if 'error' not in inventory:break
  if time.monotonic()>end:raise RuntimeError(str(inventory))
  time.sleep(5)
 record('inventory-r2',inventory)
 setup=record('pilot-setup',probe_call('setup-pilot',60))
 if 'error' in setup:raise RuntimeError(setup['error'])
 print('Pilot fixture:',setup,flush=True)
 if setup['initialSupply']['Units'] != 300 or not setup['initialSupply']['MealsPerHour']:raise RuntimeError('Native fixture is not serviceable: '+str(setup))
 begin=record('begin',probe_call('begin',30))
 if 'error' in begin:raise RuntimeError(begin['error'])
 start=setup['startDay']
 until(start,'09:00')
 checks=record('supply-checks',probe_call('supply-checks',60))
 if 'error' in checks or checks['baseline']['Units'] != 300 or not checks['baseline']['MealsPerHour'] or not checks['readUnchanged'] or not checks['restored'] or not all(checks['checks'].values()):raise RuntimeError('Supply checks failed: '+str(checks))
 print('Native stock/staff/closure checks:',checks,flush=True)
 for day in range(start,start+3):
  # Native fast-forward can outrun the bounded population engine. Pause and let the
  # actual scheduler catch up at each live transaction checkpoint; never backdate sales.
  for minute in range(600,681,10):
   until(day,f'{minute//60:02d}:{minute%60:02d}')
   progress=record(f'live-{day}-{minute}',probe_call('pilot-status',60))
   if not progress.get('armed'):raise RuntimeError('Pilot stopped: '+str(progress))
  print('Completed live window:',day,progress,flush=True)
 until(start+3,'01:00')
 pilot=record('pilot-final',probe_call('pilot-status',60));print('Pilot result:',pilot,flush=True)
 record('bank-final',a.call('state',{}))
 if 'error' in pilot:raise RuntimeError(pilot['error'])
 if not pilot['receipts']:raise RuntimeError('No real purchase completed; inspect native pilot')
 result=record('native-result',probe_call('finish',1200))
 if 'error' in result:raise RuntimeError(result['error'])
 print('Multi-day comparison:',{k:result[k] for k in ['residents','spans','knownOrigins','RouteCalls','marketUnchanged','populationUnchanged']},flush=True)
except Exception as e:
 record('campaign-error',{'error':str(e)})
 try:
  a.call('time',{'pause':True,'speed':1})
  probe_call('normal',30)
 except Exception:pass
 raise
