import sys,time,subprocess,hashlib
from pathlib import Path
import rig
label=sys.argv[1];name='FT Food Routines PR1 Rerun 20261029'
log=Path(r'C:\BigAmbitions-AgentTests\Player-agent.log');offset=log.stat().st_size
rig.record(label+'-boundary-start',dict(logBytes=offset,utc=time.time(),fixture=name))
subprocess.run([sys.executable,str(rig.STAGE/'load.py'),label,name],check=True)
subprocess.run([sys.executable,str(rig.STAGE/'measure.py'),label],check=True)
rig.call(label+'-manual-save','save',{'name':name+' '+label+' Saved'})
rig.probe(label+'-saved','capture',True)
# The native load command leaves/unloads the current city before loading the named save.
rig.call(label+'-reload','load',{'name':name+' '+label+' Saved'})
rig.waitcity(label+'-reloaded',name+' '+label+' Saved')
rig.call(label+'-reload-pause','time',{'pause':True})
rig.probe(label+'-reload-autosave-off','autosave-off',True)
while True:
 try:
  r=rig.probe(label+'-reload-ready-'+str(time.time_ns()),'status')
  if r['caughtUp']:break
 except RuntimeError as e:
  if 'Population not ready' not in str(e) and 'NullReference' not in str(e):raise
 time.sleep(1)
rig.call(label+'-reload-play','time',{'speed':1,'pause':False,'keep_needs':True})
start=time.monotonic()
while time.monotonic()-start<10:time.sleep(.5)
rig.call(label+'-reload-end-pause','time',{'pause':True})
rig.probe(label+'-reload-autosave-after','autosave-check',True)
rig.probe(label+'-reloaded-final','capture',True)
with log.open('rb') as f:f.seek(offset);segment=f.read()
(rig.STAGE/(label+'-controlled-native-window.log')).write_bytes(segment)
rig.record(label+'-boundary-end',dict(logBytes=log.stat().st_size,utc=time.time(),segmentSha256=hashlib.sha256(segment).hexdigest()))
print(label,'controlled load/play/manual save/unload/reload/play completed',flush=True)
