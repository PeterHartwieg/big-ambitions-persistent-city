"""Capture one ordinary day in the retained disposable fixture; no forced buyers."""
from protocol import *
import sys
label=sys.argv[1] if len(sys.argv)>1 else 'baseline'
name=sys.argv[2] if len(sys.argv)>2 else 'FT Food Routines Settled 20261022'
def ready():
 end=time.monotonic()+300
 while True:
  h=health()
  if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done':break
  if time.monotonic()>end:raise RuntimeError('Load timeout')
  time.sleep(2)
 checked(label+'-pause','time',{'pause':True,'speed':1,'keep_needs':True})
 while True:
  d=probe_call('status',30)
  if 'error' not in d and abs(d['minute']-d['nativeMinute'])<.01:return d
  if time.monotonic()>end:raise RuntimeError('Population catch-up timeout')
  time.sleep(2)
def until(day,clock):
 checked(label+'-run-'+str(day)+'-'+clock.replace(':',''),'time',{'run_until':{'day':day,'time':clock},'speed':1,'keep_needs':True})
 probe_call('fast-forward')
 end=time.monotonic()+900
 while True:
  h=health()
  if h.get('paused') and h.get('day',0)>=day:break
  if time.monotonic()>end:raise RuntimeError('Clock timeout')
  time.sleep(2)
 probe_call('normal');return ready()
try:
 startup=time.monotonic()+240
 while not health():
  if time.monotonic()>startup:raise RuntimeError('Bridge startup timeout')
  time.sleep(2)
 checked(label+'-load','load',{'name':name});start=record(label+'-start',ready())
 assert int(start['minute']%1440)<480,start['minute']
 record(label+'-audit-start',probe_call('audit-start'))
 day=int(start['minute']//1440)
 for hour in [10,12,14,18,23]:
  s=until(day,f'{hour:02d}:00')
  print(label,hour,'purchases',s['purchases']-start['purchases'],'decisions',s['auditDecisions'],flush=True)
 s=record(label+'-final',until(day+1,'01:00'))
 record(label+'-audit-end',probe_call('audit-end'))
 for f in ['food-audit.json','audit-start-population.json','audit-end-population.json']:
  subprocess.run(['scp','-q','-o','BatchMode=yes','homepc:'+HELPER.replace('\\','/')+'/'+f,str(ROOT/(label+'-'+f))],check=True,timeout=90)
 print(label,'complete',s['purchases']-start['purchases'],'native purchases',flush=True)
except Exception as e:
 record(label+'-error',{'error':str(e)})
 try:a.call('time',{'pause':True,'speed':1})
 except Exception:pass
 raise
