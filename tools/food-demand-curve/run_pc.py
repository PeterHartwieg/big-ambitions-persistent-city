"""Deploy only inside owned, backed-up rig; always stop and restore on exit."""
from pc import *
import time, uuid, zipfile, io
baseline=json.loads((ROOT/'baseline.json').read_text());assert baseline['copyVerified']
assert not status()['running']
assert json.loads(a.locks.lock_action('agent1','status'))['owner']==os.environ['BA_LOCK_OWNER']
package=ROOT/'source/build/PersistentCity';probe=ROOT/'Probe/FoodDemandCurveProbe.dll'
receipt=json.loads((ROOT/'Probe/build-manifest.json').read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(probe)==receipt['output']['sha256']
assert sha(package/'PersistentCity.dll')==next(x['sha256'] for x in receipt['references'] if x['path'].endswith('/PersistentCity.dll'))
record('normal-process-before',json.loads(remote("@(Get-Process -Name 'Big Ambitions' -ErrorAction SilentlyContinue | ForEach-Object{@{pid=$_.Id;started=$_.StartTime.ToUniversalTime().ToString('o');path=$_.Path}})|ConvertTo-Json -Compress") or '[]'))
def health():return status().get('health') or {}
def probe_call(op,timeout=180):
 ident=op+'-'+uuid.uuid4().hex[:8]
 text=json.dumps({'id':ident,'op':op})
 remote('[IO.File]::WriteAllText('+a.literal(HELPER+r'\request.tmp')+','+a.literal(text)+');Move-Item -Force -LiteralPath '+a.literal(HELPER+r'\request.tmp')+' -Destination '+a.literal(HELPER+r'\request.json'),True,kind='diagnostic')
 path=HELPER+'\\response-'+ident+'.json';until=time.monotonic()+timeout
 while time.monotonic()<until:
  data=remote('if(Test-Path -LiteralPath '+a.literal(path)+'){[IO.File]::ReadAllText('+a.literal(path)+')}')
  if data:return json.loads(data)
  time.sleep(2)
 raise RuntimeError('Probe timed out: '+op)
def checked(name,cmd,args):
 d=call(name,cmd,args)
 if not d.get('ok'):raise RuntimeError(str(d))
 return d
try:
 buf=io.BytesIO()
 with zipfile.ZipFile(buf,'w',zipfile.ZIP_DEFLATED) as z:
  z.write(package/'PersistentCity.dll','PersistentCity/PersistentCity.dll')
  z.write(probe,'FoodDemandCurveProbe/FoodDemandCurveProbe.dll')
 put(REMOTE+r'\candidate.zip',buf.getvalue())
 script=r'''
$p='PROFILE';$b='BACKUP';$helper='HELPER';if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Rig running'};if(Test-Path $helper){throw 'Existing probe refused'}
$marker='C:\BigAmbitions-AgentTests\agent-install.json';$m=Get-Content -Raw $marker|ConvertFrom-Json
foreach($file in $m.files){if((Get-FileHash -Algorithm SHA256 -LiteralPath $file.path).Hash.ToLowerInvariant() -ne $file.sha256){throw 'Existing manifest mismatch'}}
Expand-Archive -LiteralPath (Join-Path $b 'candidate.zip') -DestinationPath (Join-Path $p 'ModsLocal') -Force
$config=Join-Path $p 'ModsLocal\PersistentCity\persistent-city.json';$settings=Get-Content -Raw $config|ConvertFrom-Json;$settings.Enabled=$true;$settings.PhysicalEnabled=$false;$settings|ConvertTo-Json|Set-Content -Encoding UTF8 $config
$changed=@((Join-Path $p 'ModsLocal\PersistentCity\PersistentCity.dll'),$config)+@(Get-ChildItem $helper -File -Recurse|ForEach-Object{$_.FullName})
foreach($path in $changed){$hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant();$entry=@($m.files|Where-Object path -eq $path);if($entry.Count){foreach($e in $entry){$e.sha256=$hash}}else{$m.files+=@{path=$path;sha256=$hash}}}
$m|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 $marker
@{files=@($changed|ForEach-Object{@{path=$_;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash.ToLowerInvariant()}})}|ConvertTo-Json -Depth 5
'''.replace('PROFILE',PROFILE).replace('BACKUP',REMOTE).replace('HELPER',HELPER)
 record('deployment',json.loads(remote(script,True)));print('Candidate deployed to isolated PC rig',flush=True)
 record('start',json.loads(a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'start','Mode':'background'},'agent1')))
 until=time.monotonic()+240
 while not health():
  if time.monotonic()>until:raise RuntimeError('Bridge startup timeout')
  time.sleep(2)
 name='FT Food Demand Curve '+uuid.uuid4().hex[:7]
 checked('new-game','new_game',{'name':name});until=time.monotonic()+300
 while True:
  h=health();load=h.get('load') or {}
  if load.get('status')=='failed':raise RuntimeError(str(load))
  if h.get('scene')=='city' and load.get('status')=='done' and load.get('name')==name:break
  if time.monotonic()>until:raise RuntimeError('New city load timeout')
  time.sleep(2)
 checked('pause-start','time',{'pause':True,'speed':1,'keep_needs':True});record('health-start',health())
 until=time.monotonic()+240
 while True:
  ready=probe_call('status',30)
  if 'error' not in ready:
   begin=probe_call('begin',30)
   if 'error' not in begin:break
  if time.monotonic()>until:raise RuntimeError('Population not ready: '+str(ready))
  time.sleep(3)
 record('begin',begin);assert begin['populationUnchanged'];end_day=int(begin['minute']//1440)+2
 checked('run','time',{'run_until':{'day':end_day,'time':'00:00'},'speed':1,'keep_needs':True})
 assert record('fast-forward',probe_call('fast-forward'))['nativeFastForward']
 until=time.monotonic()+1800;last_print=0
 while True:
  h=health()
  if time.monotonic()-last_print>30:print('Recording routines:',h.get('day'),h.get('time'),flush=True);last_print=time.monotonic()
  if h.get('day',0)>=end_day and h.get('paused'):break
  if time.monotonic()>until:raise RuntimeError('Observation timeout')
  time.sleep(3)
 checked('pause-end','time',{'pause':True,'speed':1});assert not record('normal-speed',probe_call('normal'))['nativeFastForward']
 until=time.monotonic()+300
 while True:
  ready=probe_call('status')
  if 'error' not in ready and ready['minute']>=end_day*1440:break
  if time.monotonic()>until:raise RuntimeError('Population catch-up timeout')
  time.sleep(2)
 print('Routines complete; quoting native walking routes and sweeping prices',flush=True)
 result=record('native-result',probe_call('finish',900))
 if 'error' in result:raise RuntimeError(result['error'])
 assert result['marketUnchanged'] and result['populationUnchanged']
 print(json.dumps({k:result[k] for k in ['residents','spans','RouteCalls','completeRoundTrips','strict','marketUnchanged','populationUnchanged']}),flush=True)
 print('Target:',result['selection']['name'],[(p['factor'],p['meals']) for p in result['points']],flush=True)
 record('health-end',health())
finally:
 if status()['running']:
  try:record('stop',json.loads(a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'stop'},'agent1')))
  except Exception as e:
   record('stop-error',{'error':str(e)});until=time.monotonic()+45
   while status()['running'] and time.monotonic()<until:time.sleep(2)
 if status()['running']:raise RuntimeError('Rig still running; backup and ownership retained')
 # Collect evidence before restoring the preexisting profile.
 script=r'''
$b='BACKUP';$helper='HELPER';if(Test-Path $helper){Compress-Archive -Path (Join-Path $helper '*.json') -DestinationPath (Join-Path $b 'probe-output.zip') -Force};Copy-Item -LiteralPath 'C:\BigAmbitions-AgentTests\Player-agent.log' -Destination (Join-Path $b 'Player-agent.log');'Saved evidence'
'''.replace('BACKUP',REMOTE).replace('HELPER',HELPER)
 print(remote(script,True),flush=True)
 script=r'''
$p='PROFILE';$b='BACKUP';$helper='HELPER';if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Rig running'}
$old=Get-Content -Raw (Join-Path $b 'baseline-profile.json')|ConvertFrom-Json;$restored=@();foreach($f in $old){$path=Join-Path $p $f.path;if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant() -ne $f.sha256){New-Item -ItemType Directory -Force (Split-Path $path -Parent)|Out-Null;Copy-Item -LiteralPath (Join-Path (Join-Path $b 'profile') $f.path) -Destination $path -Force;$restored+=$f.path}}
if(Test-Path $helper){Move-Item -LiteralPath $helper -Destination (Join-Path $b ('probe-output-'+[DateTime]::UtcNow.ToString('HHmmss')))}
$bad=@($old|Where-Object{(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $p $_.path)).Hash.ToLowerInvariant() -ne $_.sha256});if($bad.Count){throw 'Restore mismatch'}
Copy-Item -LiteralPath (Join-Path $b 'agent-install.json') -Destination 'C:\BigAmbitions-AgentTests\agent-install.json' -Force
$key='HKCU:\Software\Hovgaard Games\Big Ambitions Agent Tests';if(Test-Path $key){Remove-Item -LiteralPath $key -Recurse};if(Test-Path (Join-Path $b 'preferences.reg')){& reg import (Join-Path $b 'preferences.reg')|Out-Null;if($LASTEXITCODE){throw 'Preferences restore failed'};& reg export 'HKCU\Software\Hovgaard Games\Big Ambitions Agent Tests' (Join-Path $b 'preferences-restored.reg') /y|Out-Null;if((Get-FileHash (Join-Path $b 'preferences.reg')).Hash -ne (Get-FileHash (Join-Path $b 'preferences-restored.reg')).Hash){throw 'Preferences mismatch'}}
$m=Get-Content -Raw 'C:\BigAmbitions-AgentTests\agent-install.json'|ConvertFrom-Json;foreach($f in $m.files){if((Get-FileHash -Algorithm SHA256 $f.path).Hash.ToLowerInvariant() -ne $f.sha256){throw 'Manifest mismatch'}}
@{preexistingFilesVerified=$old.Count;restored=$restored;probeRemoved=(!(Test-Path $helper));manifestVerified=$true;preferencesRestored=$true;normalProfileTouched=$false}|ConvertTo-Json -Depth 5
'''.replace('PROFILE',PROFILE).replace('BACKUP',REMOTE).replace('HELPER',HELPER)
 record('cleanup',json.loads(remote(script,True,timeout=300)));record('released',json.loads(a.locks.lock_action('agent1','release')));print('PC rig restored, stopped and released',flush=True)
 record('normal-process-after',json.loads(remote("@(Get-Process -Name 'Big Ambitions' -ErrorAction SilentlyContinue | ForEach-Object{@{pid=$_.Id;started=$_.StartTime.ToUniversalTime().ToString('o');path=$_.Path}})|ConvertTo-Json -Compress") or '[]'))
 for name in ['probe-output.zip','Player-agent.log']:
  if remote('Test-Path -LiteralPath '+a.literal(REMOTE+'\\'+name))=='True':
   subprocess.run(['scp','-q','-o','BatchMode=yes','homepc:'+REMOTE.replace('\\','/')+'/'+name,str(ROOT/name)],check=True,timeout=90)
 if (ROOT/'probe-output.zip').exists():
  with zipfile.ZipFile(ROOT/'probe-output.zip') as z:z.extractall(ROOT/'probe-output')
