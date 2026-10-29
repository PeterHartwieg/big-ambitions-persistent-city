from pathlib import Path
import sys,json,time,shutil,hashlib
import rig
s=rig.STAGE;guard=rig.PROFILE/'ModsLocal/PR3RigGuard';base='FT Food Routines PR3 OB20261029'
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
  if refused and str(r.get('status','')).startswith('Persistent population active:'):raise RuntimeError('Test fixture input hook missed startup; no refusal evidence accepted')
  if not refused and r.get('discoveryReady') and str(r.get('status','')).startswith('Persistent population active:'):return r
  time.sleep(.5)
rows=[]
resuming="--resume-loaded" in sys.argv
for case in json.loads((s/'schema-cases.json').read_text()):
 if case['expected']=='Loaded':continue
 if case['schema']==15:continue
 label='native-'+Path(case['file']).name.replace('.private.json','')
 if resuming:
  runtime=rig.probe(label+'-resume-runtime','runtime-state');assert runtime.get('failed');resuming=False
 else:
  unload(label)
  (guard/'inject-fixture.json').write_text(json.dumps({'save':base,'file':case['file']}))
  runtime=load(label,base,True)
 rig.probe(label+'-before-save','stored-payload',True)
 (guard/'inject-fixture.json').unlink()
 name='FT Food Routines PR3 Refused '+label.replace('native-','')+' 20261029'
 rig.call(label+'-save','save',{'name':name});rig.probe(label+'-after-save','stored-payload',True)
 unload(label+'-reload');load(label+'-reload',name,True);rig.probe(label+'-reloaded','stored-payload',True)
 files=[next(s.glob(label+tag+'-*-stored-payload.json')) for tag in ['-before-save','-after-save','-reloaded']]
 hashes=[hashlib.sha256(p.read_bytes()).hexdigest() for p in files]
 row=dict(case=case['file'],expected=case['expected'],runtimeFailed=runtime.get('failed'),runtimeStatus=runtime.get('status'),sourceSha256=case['sha256'],storedHashes=hashes,preservedAcrossSaveReload=all(h==case['sha256'] for h in hashes))
 rows.append(row);(s/'native-refusal-summary.json').write_text(json.dumps(rows,indent=2));print(label,row['preservedAcrossSaveReload'],flush=True)
 assert row['runtimeFailed'] and row['preservedAcrossSaveReload']
print('All native refusal inputs preserved',len(rows),flush=True)
