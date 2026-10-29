"""Observe a real hand-on at default25 after the high-stock50 window had no eligible taker."""
import json,time,shutil,sys,subprocess
from pathlib import Path
import rig
from rate_arms import configure,config,original
from schema_native_helpers import unload
s=rig.STAGE;guard=rig.PROFILE/'ModsLocal/PR3RigGuard';label='event-hand-on-default'
cases=json.loads((s/'event-persistence-cases.json').read_text())
assert next(c for c in cases if c['event']=='hand-on')['observed'] is False
assert not (s/'event-persistence-initial-attempts.json').exists()
shutil.copy2(s/'event-persistence-cases.json',s/'event-persistence-initial-attempts.json')
def minute(h):
 clock=h['time'].split(':');return h['day']*1440+int(clock[0])*60+int(clock[1])
try:
 configure(label,25,'FT Food Routines PR3 OB Rate25')
 ui=rig.call(label+'-ui','ui',{'op':'list'});buttons=[b for b in ui['buttons'] if b['path'].endswith('Campaign menu/Icon button')]
 if buttons:rig.call(label+'-close-campaign','ui',{'op':'click','id':buttons[0]['id']})
 try:rig.call(label+'-close-phone','ui',{'op':'escape'})
 except RuntimeError as e:
  if 'Nothing is open' not in str(e):raise
 marker=guard/'optional-event-checkpoint.json'
 if marker.exists():marker.unlink()
 rig.probe(label+'-trace-start','optional-start',True)
 rig.probe(label+'-arm','arm-optional-event',True,{'event':'hand-on'})
 rig.probe(label+'-autosave-before','autosave-check',True)
 start=minute(rig.health());rig.call(label+'-play','time',{'speed':1,'pause':False,'keep_needs':True})
 # An eight-game-hour observation workload, never a CLI execution deadline.
 while not marker.exists() and minute(rig.health())<start+480:time.sleep(.5)
 rig.call(label+'-pause','time',{'pause':True})
 while not rig.probe(label+'-caught-'+str(time.time_ns()),'status')['caughtUp']:time.sleep(.5)
 end=rig.probe(label+'-trace-end','optional-end',True);shutil.copy2(end['output'],s/Path(end['output']).name)
 if not marker.exists():raise RuntimeError('Default25 real hand-on remains unobserved; no synthetic event was injected.')
 while True:
  try:q=json.loads(marker.read_text());break
  except (PermissionError,json.JSONDecodeError):time.sleep(.02)
 shutil.copy2(marker,s/'event-hand-on-default-checkpoint.json')
 subprocess.run([sys.executable,str(s/'native_control.py'),'event-hand-on-control','FT Food Routines PR3 OB Event hand-on default25'],check=True)
 row=dict(event='hand-on',observed=True,checkpoint=q,saveReloadCompleted=True,traceLabel=label,source='Default25 checkpoint with actual pending chances; normal speed; previous high-stock50 attempt retained separately.')
 cases=[row if c['event']=='hand-on' else c for c in cases];rig.record('event-persistence-cases',cases)
 print('Default25 real hand-on observed and native save/reload completed',flush=True)
finally:
 unload('handon-default-restore');config.write_bytes(original)
