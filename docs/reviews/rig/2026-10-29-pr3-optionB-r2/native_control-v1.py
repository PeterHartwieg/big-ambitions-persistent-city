import sys,time,shutil
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent));import rig
arm=sys.argv[1];name=sys.argv[2]
log=Path(r'C:\BigAmbitions-AgentTests\Player-agent.log');offset=log.stat().st_size
rig.record(arm+'-control-boundary-start',{'logBytes':offset,'utc':time.time()})
rig.call(arm+'-pause','time',{'pause':True})
rig.probe(arm+'-pre-save','capture',True)
rig.probe(arm+'-pre-save-finance','finance-snapshot',True)
rig.call(arm+'-manual-save','save',{'name':name})
rig.probe(arm+'-saved','capture',True)
rig.call(arm+'-reload','load',{'name':name})

while rig.health()['scene']!='loading':time.sleep(.1)
rig.waitcity(arm+'-reloaded',name);rig.call(arm+'-reloaded-pause','time',{'pause':True})
rig.probe(arm+'-reloaded-autosave-off','autosave-off',True)
while True:
 try:
  r=rig.probe(arm+'-ready-'+str(time.time_ns()),'status')
  if r['caughtUp']:break
 except RuntimeError as e:
  if 'Population not ready' not in str(e) and 'NullReference' not in str(e):raise
 time.sleep(1)
rig.probe(arm+'-reloaded','capture',True)
rig.probe(arm+'-reloaded-finance','finance-snapshot',True)
rig.shot(arm+'-reloaded-city')
with log.open('rb') as source:
 source.seek(offset);segment=source.read()
(rig.STAGE/(arm+'-controlled-native-window.log')).write_bytes(segment)
rig.record(arm+'-control-boundary-end',{'logBytes':log.stat().st_size,'utc':time.time()})
print(arm,'manual-save / unload / reload completed',flush=True)
