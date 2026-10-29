import sys,time,json
sys.path.insert(0,r'<private-evidence>');import rig
while True:
 try:
  if rig.health()['scene'] in ['menu','city','loading']:break
 except Exception:pass
 time.sleep(1)
arm=sys.argv[1];name=sys.argv[2] if len(sys.argv)>2 else 'FT Food Routines PR3 OB20261029'

if rig.health()['scene']!='loading':rig.call(arm+'-load','load',{'name':name})
time.sleep(2)
rig.waitcity(arm+'-loaded',name);rig.call(arm+'-pause','time',{'pause':True});rig.probe(arm+'-autosave-off','autosave-off',True)
while True:
 r=rig.probe(arm+'-runtime-'+str(time.time_ns()),'runtime-state')
 if r.get('failed'):raise RuntimeError(r)
 if r.get('discoveryReady') and str(r.get('status','')).startswith('Persistent population active:'):break
 time.sleep(1)
# Population initialization and road preparation can finish on adjacent frames.
while True:
 try:r=rig.probe(arm+'-ready-'+str(time.time_ns()),'status');break
 except RuntimeError as e:
  if 'Population not ready' not in str(e):raise
  time.sleep(1)
ui=rig.call(arm+'-ui-list','ui',{'op':'list'})
close=[b for b in ui['buttons'] if b['path'].endswith('Campaign menu/Icon button')]
if close:rig.call(arm+'-close-campaign','ui',{'op':'click','id':close[0]['id']})
try:rig.call(arm+'-close-phone','ui',{'op':'escape'})
except RuntimeError as e:
 if 'Nothing is open' not in str(e):raise
rig.probe(arm+'-initial','capture',True)
try:rig.shot(arm+'-loaded-city')
except Exception as e:rig.record(arm+'-capture-failure',{'error':str(e)})
print(arm,'ready',r['routines'],flush=True)
