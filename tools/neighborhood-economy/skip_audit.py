"""One native skipped day with bounded resident processing; retains the ordinary audit format."""
from pathlib import Path
import sys,time,json,subprocess
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'food-demand-audit'))
from protocol import *
label,checkpoint=sys.argv[1:3]
def probe(op):
 d=probe_call(op,60)
 if 'error' in d:raise RuntimeError(d['error'])
 return d
def ready():
 end=time.monotonic()+600
 while time.monotonic()<end:
  h=health()
  if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done' and not h.get('time_skip'):
   checked(label+'-pause','time',{'pause':True,'speed':1,'keep_needs':True})
   d=probe_call('status',30)
   if 'error' not in d and d['caughtUp']:return d
  time.sleep(2)
 raise RuntimeError('Population or native skip did not finish')
needs=False
try:
 checked(label+'-load','load',{'name':checkpoint})
 start=record(label+'-start',ready());assert int(start['minute']%1440)<480
 record(label+'-audit-start',probe('audit-start'))
 record(label+'-skip-needs-start',probe('skip-needs-start'));needs=True
 started=time.monotonic()
 checked(label+'-native-skip','console',{'line':'skiptime 1d true'})
 time.sleep(2)
 final=ready()
 record(label+'-skip-needs-end',probe('skip-needs-end'));needs=False
 record(label+'-final',final)
 assert abs(final['nativeMinute']-start['nativeMinute']-1440)<.1
 record(label+'-skip-timing',{'seconds':time.monotonic()-started,'minutes':final['nativeMinute']-start['nativeMinute'],
    'heldTicks':final['clock']['heldEconomySkipTicks']-start['clock']['heldEconomySkipTicks']})
 record(label+'-audit-end',probe('audit-end'))
 for f in ['food-audit.json','audit-start-population.json','audit-end-population.json']:
  subprocess.run(['scp','-q','-o','BatchMode=yes','homepc:'+HELPER.replace('\\','/')+'/'+f,str(ROOT/(label+'-'+f))],check=True,timeout=90)
 print(label,'skipped day',final['purchases']-start['purchases'],'purchases',round(time.monotonic()-started,1),'seconds',flush=True)
except BaseException as e:
 record(label+'-error',{'error':str(e)})
 raise
finally:
 if needs and not health().get('time_skip'):record(label+'-skip-needs-restore',probe('skip-needs-end'))
