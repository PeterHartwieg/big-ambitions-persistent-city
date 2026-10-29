from pathlib import Path
import rig,time
def unload(label):
 if rig.health()['scene']=='city':
  r=rig.probe(label+'-unload','unload',True)
  marker=rig.PROFILE/'ModsLocal/PR3RigGuard'/r['marker']
  while not marker.exists():time.sleep(.3)
  __import__('shutil').copy2(marker,rig.STAGE/marker.name)
  while rig.health()['scene']!='menu':time.sleep(.3)
 # Let the native loading screen finish its fade before calling the bridge load.
 time.sleep(2)
def load(label,name,refused=False):
 rig.call(label+'-load','load',{'name':name});rig.waitcity(label+'-loaded',name);rig.call(label+'-pause','time',{'pause':True});rig.probe(label+'-autosave-off','autosave-off',True)
 while True:
  r=rig.probe(label+'-runtime-'+str(time.time_ns()),'runtime-state')
  if r.get('failed'):
   if not refused:raise RuntimeError(r)
   return r
  if not refused and r.get('discoveryReady') and str(r.get('status','')).startswith('Persistent population active:'):return r
  time.sleep(.5)
