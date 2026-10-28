import sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent));import rig
rig.probe('after-interrupted-drain-autosave','autosave-off',True)
rig.probe('after-interrupted-drain-needs-start','skip-needs-start')
try:
 h=rig.health();hour,minute=map(int,h['time'].split(':'));start=h['day']*1440+hour*60+minute
 rig.call('after-interrupted-drain-skip','console',{'line':'skiptime 17h true'})
 while True:
  h=rig.health();hour,minute=map(int,h['time'].split(':'));now=h['day']*1440+hour*60+minute
  if now>=start+17*60-1 and h.get('time_skip') is False:break
  time.sleep(.5)
 rig.call('after-interrupted-drain-pause','time',{'pause':True})
 while not rig.probe('after-interrupted-drain-ready-'+str(time.time_ns()),'status')['caughtUp']:time.sleep(.5)
 rig.probe('after-interrupted-drained','capture',True)
 rig.probe('after-interrupted-drained-finance','finance-snapshot',True)
finally:rig.probe('after-interrupted-drain-needs-end','skip-needs-end')
print('Interrupted checkouts drained to next morning',flush=True)
