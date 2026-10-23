"""Continue retained r4 city after correcting the fixture's explicit native shift enum.
The main mod DLL is unchanged. Run a full subsequent day to observe natural end-of-shift restocking.
"""
from protocol import *
def ready():
 end=time.monotonic()+300
 while True:
  h=health()
  if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done':break
  if time.monotonic()>end:raise RuntimeError('Load timeout')
  time.sleep(2)
 checked('pause-resume','time',{'pause':True,'speed':1,'keep_needs':True})
 while True:
  d=probe_call('status',30)
  if 'error' not in d and abs(d['minute']-d['nativeMinute'])<.01:return d
  if time.monotonic()>end:raise RuntimeError('Population catch-up timeout')
  time.sleep(2)
def until(day,clock):
 checked('r5-run-'+str(day)+'-'+clock,'time',{'run_until':{'day':day,'time':clock},'speed':1,'keep_needs':True})
 probe_call('fast-forward')
 end=time.monotonic()+900
 while True:
  h=health()
  if h.get('paused') and h.get('day',0)>=day:break
  if time.monotonic()>end:raise RuntimeError('Native clock timeout')
  time.sleep(2)
 probe_call('normal');return ready()
end=time.monotonic()+180
while not health():
 assert time.monotonic()<end;time.sleep(2)
checked('r5-load','load',{'name':'FT Food Routines Before Shift Correction'})
ready()
correction=record('fixture-shift-correction',probe_call('correct-fixture-shift'))
assert 'error' not in correction,correction
for key in ['purchases','spendingCents','orders','receipts','history']:
 assert correction['before'][key]==correction['after'][key]
print('Only fixture shift type corrected; prior native sales and receipts retained',flush=True)
record('two-day-native-end',until(3,'01:00'))
for hour in range(10,19):
 s=until(3,f'{hour:02d}:00')
 print('Day3',hour,'purchases',s['purchases'],'housekeeping',s['housekeeping'],flush=True)
final=record('final',until(4,'01:00'))
record('population-dump',probe_call('dump'));record('bank-final',a.call('state',{}))
assert final['housekeeping']['restocks']>0,final['housekeeping']
print('Completed native recurrence, settlement and end-of-shift housekeeping',flush=True)
