import sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent));import rig
arm=sys.argv[1]
rig.call(arm+'-pause','time',{'pause':True})
rig.probe(arm+'-autosave-off','autosave-off',True)
rig.probe(arm+'-finance-initial','capture',True)
rig.probe(arm+'-finance-start','finance-start',True)
rig.probe(arm+'-optional-start','optional-start',True)
rig.probe(arm+'-needs-start','skip-needs-start')
try:
 for step,hours in enumerate([17,24],1):
  h=rig.health();clock=h['time'].split(':');minute=h['day']*1440+int(clock[0])*60+int(clock[1])
  rig.call(arm+f'-skip-{step}','console',{'line':f'skiptime {hours}h true'})
  while True:
   h=rig.health();clock=h['time'].split(':');now=h['day']*1440+int(clock[0])*60+int(clock[1])
   if now>=minute+hours*60-1 and h.get('time_skip') is False:break
   time.sleep(.5)
  rig.call(arm+f'-skip-{step}-pause','time',{'pause':True})
  while not rig.probe(arm+f'-skip-{step}-ready-'+str(time.time_ns()),'status')['caughtUp']:time.sleep(.5)
  rig.probe(arm+f'-day-{step}-capture','capture',True)
  print(arm,'step',step,'done',h['day'],h['time'],flush=True)
finally:
 rig.probe(arm+'-needs-end','skip-needs-end')
 rig.probe(arm+'-finance-end','finance-end',True)
 rig.probe(arm+'-optional-end','optional-end',True)
 # probe copies the named JSONL after stop so the retained evidence includes the final line.
 for src in (rig.PROFILE/'ModsLocal/PR3RigGuard').glob('*-*.jsonl'):
  import shutil;shutil.copy2(src,rig.STAGE/src.name)
rig.probe(arm+'-final','capture',True)
rig.shot(arm+'-financial-end')
