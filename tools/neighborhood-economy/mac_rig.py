"""Owned local Mac test lifecycle; restores the preexisting profile, preferences and manifest."""
from pathlib import Path
import hashlib,importlib.machinery,importlib.util,json,os,plistlib,shutil,subprocess,sys,time,uuid

repo=Path(__file__).resolve().parents[2]
rig=repo.parent/'hart-self-driving/tools/agent-tests'
sys.path.insert(0,str(rig))
from mac_common import ROOT as rig_root,PROFILE,BUNDLE,lifecycle,control
loader=importlib.machinery.SourceFileLoader('neighborhood_mac_agent',str(rig/'mac-agent'))
spec=importlib.util.spec_from_loader(loader.name,loader)
agent=importlib.util.module_from_spec(spec);loader.exec_module(agent)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

class NativeProbeError(RuntimeError):
 """The owned helper returned a completed, explicit operation refusal."""

class MacRig:
 def __init__(self,campaign,physical=False,mode='headless',enabled=True):
  self.root=repo/'build'/campaign;self.backup=self.root/'backup'
  self.helper=PROFILE/'ModsLocal/FoodDemandAuditProbe'
  self.physical=physical;self.mode=mode;self.enabled=enabled;self.prepared=False;self.acquired=False
  os.environ['BA_LOCK_OWNER']='persistent-city-'+campaign
  os.environ['BA_BRIDGE_URL']='http://127.0.0.1:28330'
 def record(self,name,value):
  (self.root/(name+'.json')).write_text(json.dumps(value,indent=2)+'\n');return value
 def stage(self,name):
  self.record('stage',{'stage':name,'utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())});print(name,flush=True)
 def begin(self):
  if control('status')['held'] or agent.process():raise RuntimeError('Mac rig is busy')
  if self.backup.exists() or self.helper.exists():raise RuntimeError('Existing backup/helper refused')
  package=self.root/'source/build/PersistentCity';probe=self.root/'Probe/FoodDemandAuditProbe.dll'
  receipt=json.loads((probe.parent/'build-manifest.json').read_text())
  assert sha(probe)==receipt['output']['sha256']
  assert sha(package/'PersistentCity.dll')==next(r['sha256'] for r in receipt['references'] if Path(r['path']).name=='PersistentCity.dll')
  self.record('acquired',control('acquire',purpose='Neighborhood economy native market and performance checks'));self.acquired=True
  with lifecycle():
   agent.verify();self.backup.mkdir()
   subprocess.run(['cp','-cR',str(PROFILE),str(self.backup/'profile')],check=True)
   shutil.copy2(rig_root/'install.json',self.backup/'install.json')
   subprocess.run(['defaults','export',BUNDLE,str(self.backup/'preferences.plist')],check=True)
   self.old={str(p.relative_to(PROFILE)):sha(p) for p in PROFILE.rglob('*') if p.is_file()}
   assert all(sha(self.backup/'profile'/n)==h for n,h in self.old.items())
   self.record('before-files',self.old);self.prepared=True
   imports=self.root/'import-manifest.json'
   if imports.exists():
    manifest=json.loads(imports.read_text());assert manifest['name'].startswith('FT Food Routines ')
    for row in manifest['files']:
     relative=Path(row['path'].replace('\\','/'));source=self.root/'import'/relative;target=PROFILE/relative
     assert relative.parts[0]=='SaveGames' and '..' not in relative.parts and sha(source)==row['sha256']
     if target.exists():assert sha(target)==row['sha256'],str(target)
     else:target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
   self.helper.mkdir();shutil.copy2(probe,self.helper/probe.name)
   shutil.copytree(package/'Dependencies',self.helper/'Dependencies')
   target=PROFILE/'ModsLocal/PersistentCity';shutil.copy2(package/'PersistentCity.dll',target/'PersistentCity.dll')
   config=target/'persistent-city.json';settings=json.loads(config.read_text())
   settings.update(Enabled=self.enabled,PhysicalEnabled=self.physical,GameClockFactor=1/3)
   config.write_text(json.dumps(settings,indent=2)+'\n')
   installed=json.loads((rig_root/'install.json').read_text())
   for path in [target/'PersistentCity.dll',config,*self.helper.rglob('*.dll')]:installed['files'][str(path)]=sha(path)
   (rig_root/'install.json').write_text(json.dumps(installed,indent=2)+'\n');agent.verify()
   self.record('installation',{'candidate':sha(package/'PersistentCity.dll'),'probe':sha(probe),'physical':self.physical,'enabled':self.enabled,'mode':self.mode,
    'mods':{str(p.relative_to(PROFILE/'ModsLocal')):sha(p) for p in (PROFILE/'ModsLocal').rglob('*.dll')}})
  self.start()
 def start(self):
  if self.mode!='windowed':return self.record('start',agent.start(self.mode))
  # The installed rig runtime already supports windowed isolation. Keep its verified
  # executable and receipt protocol; omit batchmode so native rendering settings apply.
  with lifecycle():
   if agent.process():raise RuntimeError('Mac rig already running')
   if str(agent.EXE) in subprocess.check_output(['ps','-axo','comm='],text=True).splitlines():raise RuntimeError('Untracked rig process')
   import socket
   with socket.socket() as check:
    check.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1)
    check.bind(('127.0.0.1',28330));check.listen(1)
   resources=agent.resources()
   if resources['disk_free_gib']<12 or resources['memory_free_percent']<20:raise RuntimeError('Insufficient Mac headroom '+str(resources))
   agent.verify()
   args=[str(agent.EXE),'-agent-tests','-disable-force-steam','-console','-foodtrucks-test','-drivenrides-test',
    '-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-logFile',str(rig_root/'Player-agent.log')]
   with (rig_root/'launcher.log').open('ab') as output:
    process=subprocess.Popen(args,cwd=agent.EXE.parent,stdin=subprocess.DEVNULL,stdout=output,stderr=output,start_new_session=True)
   identity=subprocess.check_output(['ps','-ww','-p',str(process.pid),'-o','lstart=','-o','comm='],text=True).strip()
   assert identity.endswith(str(agent.EXE))
   (rig_root/'run.json').write_text(json.dumps({'pid':process.pid,'identity':identity,'exe':str(agent.EXE),'mode':'windowed'},indent=2))
   subprocess.Popen(['/usr/bin/caffeinate','-i','-w',str(process.pid)],stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
  end=time.monotonic()+300
  while time.monotonic()<end:
   if not agent.process():raise RuntimeError('Rendered rig exited')
   try:return self.record('start',agent.health())
   except (agent.BridgeError,ConnectionError):time.sleep(1)
  raise RuntimeError('Rendered rig startup timed out')
 def population_mode(self,enabled):
  self.stop_owned()
  with lifecycle():
   config=PROFILE/'ModsLocal/PersistentCity/persistent-city.json';settings=json.loads(config.read_text())
   settings.update(Enabled=enabled,PhysicalEnabled=self.physical)
   config.write_text(json.dumps(settings,indent=2)+'\n')
   installed=json.loads((rig_root/'install.json').read_text());installed['files'][str(config)]=sha(config)
   (rig_root/'install.json').write_text(json.dumps(installed,indent=2)+'\n');agent.verify()
  self.enabled=enabled;self.start()
 def focus(self,label):
  # Activate only the process whose executable and start identity the rig owns.
  # AppKit activation does not create another game process or use UI scripting.
  with lifecycle('diagnostic'):
   running=agent.process()
   if running is None:raise RuntimeError('No owned Mac player to focus')
   script='ObjC.import("AppKit"); $.NSRunningApplication.runningApplicationWithProcessIdentifier('+str(running['pid'])+').activateWithOptions(3)'
   result=subprocess.run(['/usr/bin/osascript','-l','JavaScript','-e',script],capture_output=True,text=True,check=True)
   return self.record(label+'-focus',{'pid':running['pid'],'activationResult':result.stdout.strip()})
 def call(self,name,command,arguments=None):
  result=self.record(name,agent.call(command,arguments or {}))
  if not result.get('ok'):raise RuntimeError(str(result))
  return result
 def probe(self,op,timeout=180,**arguments):
  ident=op+'-'+uuid.uuid4().hex[:8]
  # The native pump briefly holds this same OS gate. Retry only that sharing
  # collision, with the same request ID so a completed request cannot execute twice.
  for attempt in range(8):
   try:
    with lifecycle('diagnostic'):
     request=self.helper/'request.tmp';request.write_text(json.dumps({'id':ident,'op':op,**arguments}));request.replace(self.helper/'request.json')
    break
   except RuntimeError as error:
    if '/agentmac/gate' not in str(error) or 'being used by another process' not in str(error) or attempt==7:raise
    time.sleep(.25*(attempt+1))
  response=self.helper/('response-'+ident+'.json');end=time.monotonic()+timeout
  while True:
   if response.exists():
    try:data=json.loads(response.read_text());break
    except json.JSONDecodeError:pass
   if time.monotonic()>end:raise RuntimeError('Probe timeout or incomplete response '+op)
   time.sleep(.25)
  if 'error' in data:raise NativeProbeError(data['error'])
  return data
 def ready(self,load=None,population=True):
  end=time.monotonic()+900
  while time.monotonic()<end:
   h=agent.health()
   if (h.get('load') or {}).get('status')=='failed':raise RuntimeError(str(h))
   if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done' and not h.get('time_skip') and (load is None or h['load']['name']==load):
    if not h.get('paused'):self.call('pause','time',{'pause':True,'speed':1,'keep_needs':True})
    if not population:return h
    try:
     result=self.probe('status',30)
     if result['caughtUp']:return result
    except RuntimeError as error:
     if 'Population not ready' not in str(error):raise
     self.record('population-initialization',self.probe('runtime-state',30))
   time.sleep(1)
  raise RuntimeError('Native city/population did not become ready')
 def load(self,label,name,population=True):
  self.call(label+'-load','load',{'name':name})
  # Health is a per-frame snapshot. The command can finish before that snapshot
  # reflects BeginLoad, especially when loading the same checkpoint again.
  observed=agent.health()['frame'];end=time.monotonic()+900
  while agent.health()['frame']<=observed+1:
   if time.monotonic()>end:raise RuntimeError('Native load did not publish a fresh frame')
   time.sleep(.1)
  return self.ready(name,population)
 def stop_owned(self):
  if agent.process():
   try:self.record('stop',agent.stop())
   except RuntimeError as error:
    self.record('shutdown-wait',{'reason':str(error)})
    if not agent.wait_stopped(30):
     if 'Mac rig still running' not in str(error):raise
     try:self.record('forced-stop-after-timeout',agent.stop(force=True))
     except RuntimeError:
      if not agent.wait_stopped(30):raise
      self.record('forced-stop-after-timeout',{'stopped':True,'extendedShutdownWait':True})
 def close(self):
  if not self.acquired:return
  self.stop_owned()
  if self.prepared:
   with lifecycle():
    assert not agent.process()
    if (rig_root/'Player-agent.log').exists():shutil.copy2(rig_root/'Player-agent.log',self.root/'Player-agent.log')
    if self.helper.exists():shutil.move(str(self.helper),self.root/'probe-output')
    restored=[]
    for name,expected in self.old.items():
     target=PROFILE/name
     if not target.exists() or sha(target)!=expected:
      target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(self.backup/'profile'/name,target);restored.append(name)
    assert all(sha(PROFILE/name)==expected for name,expected in self.old.items())
    shutil.copy2(self.backup/'install.json',rig_root/'install.json');agent.verify()
    subprocess.run(['defaults','export',BUNDLE,str(self.root/'preferences-after.plist')],check=True)
    before=plistlib.loads((self.backup/'preferences.plist').read_bytes());after=plistlib.loads((self.root/'preferences-after.plist').read_bytes())
    for key in set(after)-set(before):subprocess.run(['defaults','delete',BUNDLE,key],check=True,stdout=subprocess.DEVNULL)
    subprocess.run(['defaults','import',BUNDLE,str(self.backup/'preferences.plist')],check=True)
    subprocess.run(['defaults','export',BUNDLE,str(self.root/'preferences-restored.plist')],check=True)
    assert plistlib.loads((self.root/'preferences-restored.plist').read_bytes())==before
    self.record('cleanup',{'preexistingFilesVerified':len(self.old),'restoredFiles':restored,'preferencesRestored':True,
      'manifestVerified':True,'probeRemoved':not self.helper.exists(),'normalProfileTouched':False})
  self.record('released',control('release'));self.stage('restored-and-released')
