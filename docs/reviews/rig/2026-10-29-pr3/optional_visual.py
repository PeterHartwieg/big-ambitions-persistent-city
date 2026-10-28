import rig,time,json,shutil,subprocess,sys
from pathlib import Path
s=rig.STAGE;guard=rig.PROFILE/'ModsLocal/PR3RigGuard';label='default-visual'
# Use a fresh copy; normal progression only. Rain is observed rather than changed.
if '--continue-skip' not in sys.argv:
 subprocess.run([sys.executable,str(s/'load.py'),label],check=True)
 rig.probe(label+'-optional-start','optional-start',True)
 rig.probe(label+'-needs-start','skip-needs-start')
try:
 # Advance naturally via the established native skip, outside all timing windows.
 if '--continue-skip' not in sys.argv:rig.call(label+'-morning-skip','console',{'line':'skiptime 27h true'})
 while True:
  h=rig.health();clock=h['time'].split(':');now=h['day']*1440+int(clock[0])*60+int(clock[1])
  if now>=6*1440+10*60+44 and h.get('time_skip') is False:break
  time.sleep(.5)
 rig.call(label+'-pause','time',{'pause':True})
 while not rig.probe(label+'-ready-'+str(time.time_ns()),'status')['caughtUp']:time.sleep(.5)
 status=rig.probe(label+'-weather','optional-status',True)
 # Observe two native daytime hours at normal speed. This is a fixed gameplay sample, not a CLI timeout.
 rig.probe(label+'-arm','arm-optional-dwell',True)
 checkpoint=guard/'optional-dwell-checkpoint.json'
 if checkpoint.exists():checkpoint.unlink()
 rig.call(label+'-play','time',{'speed':1,'pause':False,'keep_needs':True})
 start=rig.health();clock=start['time'].split(':');end=start['day']*1440+int(clock[0])*60+int(clock[1])+120
 while not checkpoint.exists():
  h=rig.health();clock=h['time'].split(':');now=h['day']*1440+int(clock[0])*60+int(clock[1])
  if now>=end:break
  time.sleep(.5)
 rig.call(label+'-checkpoint-pause','time',{'pause':True})
 if checkpoint.exists():
  shutil.copy2(checkpoint,s/'optional-dwell-checkpoint.json');q=json.loads(checkpoint.read_text());p=q['position']
  import math
  door=q['stop']['ReturnPosition'];dx=p['X']-door['X'];dz=p['Z']-door['Z'];length=math.hypot(dx,dz) or 1
  r=rig.call(label+'-dwell-image','shoot',{'name':'default-optional-dwell','x':p['X']+dx/length*6,'y':p['Y']+3,'z':p['Z']+dz/length*6,'look_at':{'x':p['X'],'y':p['Y']+1,'z':p['Z']},'frames':120,'game_view':True,'width':1280,'height':720})
  shutil.copy2(r['path'],rig.SHOTS/'default-optional-dwell.png')
  subprocess.run([__import__('sys').executable,str(s/'native_control.py'),'optional-dwell-control','FT Food Routines PR3 OptionalDwell 20261029'],check=True)
  # Saved stop must preserve its original continuation. Resume and retain its physical return trace.
  rig.probe(label+'-after-reload-start','optional-start',True)
  rig.call(label+'-resume','time',{'speed':1,'pause':False,'keep_needs':True})
  stop_end=q['stop']['EndMinute'];target=max(stop_end+10,q['stop']['ResumeEventMinute']+10)
  while rig.probe(label+'-resume-status-'+str(time.time_ns()),'status')['minute']<target:time.sleep(2)
  rig.call(label+'-resume-pause','time',{'pause':True});rig.probe(label+'-resumed','capture',True)
  rig.probe(label+'-after-reload-end','optional-end',True)
 else:rig.record(label+'-incomplete',{'reason':'No bound optional-stop dwell in the two native-hour normal-speed window','weatherAtStart':status})
finally:
 while rig.health().get('time_skip'):time.sleep(.5)
 rig.call(label+'-final-pause','time',{'pause':True});rig.probe(label+'-needs-end','skip-needs-end');rig.probe(label+'-optional-end','optional-end',True)
 for p in guard.glob('*-optional.jsonl'):shutil.copy2(p,s/p.name)
print('Optional visual sample complete',flush=True)
