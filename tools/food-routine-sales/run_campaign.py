"""Automatic meal demand with native checkpoint reloads; restore using session.py restore."""
from protocol import *

def wait_health(predicate, timeout=300):
 until=time.monotonic()+timeout
 while time.monotonic()<until:
  h=health()
  if predicate(h):return h
  time.sleep(2)
 raise RuntimeError('Health wait timed out: '+str(h))
def ready():
 wait_health(lambda h:h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done')
 checked('pause-ready','time',{'pause':True,'speed':1,'keep_needs':True})
 end=time.monotonic()+300
 while time.monotonic()<end:
  s=probe_call('status',30)
  if 'error' not in s and abs(s['nativeMinute']-s['minute'])<.01:return s
  time.sleep(2)
 raise RuntimeError('Population unavailable: '+str(s))
def boundary(status):
 record('arm-'+status,probe_call('pause-'+status))
 checked('run-'+status,'time',{'pause':False,'speed':1,'keep_needs':True})
 record('speed-'+status,probe_call('fast-forward'))
 wait_health(lambda h:h.get('paused') is True,900)
 record('normal-'+status,probe_call('normal'))
 s=record('before-'+status,probe_call('status'))
 if status not in s.get('totals',{}):raise RuntimeError('Checkpoint not reached: '+str(s))
 name='FT Food Routines '+status+' 20261022'
 checked('save-'+status,'save',{'name':name})
 checked('load-'+status,'load',{'name':name})
 restored=record('after-'+status,ready())
 for key in ['purchases','spendingCents']:
  if s[key]!=restored[key]:raise RuntimeError('Reload changed '+key)
 if s['supply']['ProducerUnits']!=restored['supply']['ProducerUnits']:raise RuntimeError('Reload stock mismatch')
 print('Native reload passed:',status,'purchases',restored['purchases'],'stock',restored['supply']['ProducerUnits'],flush=True)
 return restored

def until(day,clock):
 checked('run-'+str(day)+'-'+clock.replace(':',''),'time',{'run_until':{'day':day,'time':clock},'speed':1,'keep_needs':True})
 probe_call('fast-forward')
 wait_health(lambda h:h.get('day',0)>=day and h.get('paused') is True,1800)
 probe_call('normal')
 return ready()

try:
 checked('new-game','new_game',{'name':'FT Food Routines 20261022 r4'})
 ready()
 setup=record('setup',probe_call('setup',90)); assert 'error' not in setup,setup
 print('Fixture established; natural routines active:',setup,flush=True)
 # No resident, appetite time, budget or buying choice is scripted.
 for phase in ['Travelling','Serving','Paid']: boundary(phase)
 replay=record('duplicate-attempt',probe_call('replay-paid'))
 assert replay.get('paid') and 'already confirms' in replay.get('reason',''),replay
 for key in ['purchases','spendingCents','orders','receipts']:
  assert replay['before'][key]==replay['after'][key],key
 assert replay['before']['supply']['ProducerUnits']==replay['after']['supply']['ProducerUnits']
 print('Native receipt replay guard passed without a second order or stock debit',flush=True)
 s=record('after-checkpoints',probe_call('status'))
 start=int(s['nativeMinute']//1440)
 for day in range(start,start+2):
  for hour in range(10,23):
   if day*1440+hour*60>s['nativeMinute']:
    s=until(day,f'{hour:02d}:00')
  print('Natural day',day,'purchases',s['purchases'],'outcomes',s['totals'],flush=True)
 s=record('final',until(start+2,'01:00'))
 record('population-dump',probe_call('dump'))
 record('bank-final',a.call('state',{}))
 assert s['purchases']>0 and not s['totals'].get('Uncertain'),s
 assert s['housekeeping']['restocks']>0 and s['housekeeping']['stockTasks']>0,s['housekeeping']
 print('Native natural routine campaign complete:',s['purchases'],'purchases',flush=True)
except Exception as e:
 record('campaign-error',{'error':str(e)})
 try:a.call('time',{'pause':True,'speed':1})
 except Exception:pass
 raise
