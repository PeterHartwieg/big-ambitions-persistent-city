import json,time,shutil,sys,subprocess
import rig
from rate_arms import configure,skip,config,original
from schema_native_helpers import unload
guard=rig.PROFILE/'ModsLocal/PR3RigGuard';rows=[]
def close_ui(label):
 ui=rig.call(label+'-ui','ui',{'op':'list'});buttons=[b for b in ui['buttons'] if b['path'].endswith('Campaign menu/Icon button')]
 if buttons:rig.call(label+'-close-campaign','ui',{'op':'click','id':buttons[0]['id']})
 try:rig.call(label+'-close-phone','ui',{'op':'escape'})
 except RuntimeError as e:
  if 'Nothing is open' not in str(e):raise
def minute(h):
 clock=h['time'].split(':');return h['day']*1440+int(clock[0])*60+int(clock[1])
try:
 for event,rate in [('hand-on',50),('pass-on',50),('conversation',25)]:
  label='event-'+event;configure(label,rate,'FT Food Routines PR3 OB Rate'+str(rate))
  if event=='conversation':
   rig.probe(label+'-needs-start','skip-needs-start')
   try:skip(label+'-evening',13)
   finally:rig.probe(label+'-needs-end','skip-needs-end')
  close_ui(label)
  marker=guard/'optional-event-checkpoint.json'
  if marker.exists():marker.unlink()
  rig.probe(label+'-trace-start','optional-start',True)
  rig.probe(label+'-arm','arm-optional-event',True,{'event':event})
  rig.probe(label+'-autosave-before','autosave-check',True)
  start=minute(rig.health());rig.call(label+'-play','time',{'speed':1,'pause':False,'keep_needs':True})
  while not marker.exists() and minute(rig.health())<start+240:time.sleep(.5)
  rig.call(label+'-pause','time',{'pause':True})
  while not rig.probe(label+'-caught-'+str(time.time_ns()),'status')['caughtUp']:time.sleep(.5)
  end=rig.probe(label+'-trace-end','optional-end',True);shutil.copy2(end['output'],rig.STAGE/__import__('pathlib').Path(end['output']).name)
  if marker.exists():
   while True:
    try:q=json.loads(marker.read_text());break
    except (PermissionError,json.JSONDecodeError):time.sleep(.02)
   shutil.copy2(marker,rig.STAGE/(label+'-checkpoint.json'))
   subprocess.run([sys.executable,str(rig.STAGE/'native_control.py'),label+'-control','FT Food Routines PR3 OB Event '+event],check=True)
   row=dict(event=event,observed=True,checkpoint=q,saveReloadCompleted=True)
  else:row=dict(event=event,observed=False,saveReloadCompleted=False,verdict='INCOMPLETE',reason='No matching event in the four-native-hour normal-speed workload; no synthetic arrival or draw was injected.')
  rows.append(row);rig.record('event-persistence-cases',rows);print(event,row['observed'],flush=True)
finally:
 unload('event-cases-restore');config.write_bytes(original)
