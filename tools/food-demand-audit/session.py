from protocol import *
import sys
operation=sys.argv[1]
clock_factor=float(os.environ.get('PC_CLOCK_FACTOR','1'))
assert .1<=clock_factor<=1
physical=os.environ.get('PC_PHYSICAL_ENABLED','0')
population=os.environ.get('PC_POPULATION_ENABLED','1')
mode=os.environ.get('PC_RIG_MODE','background')
assert physical in ('0','1') and population in ('0','1') and mode in ('background','headless','windowed')
if operation=='start':
 assert not status()['running']
 buf=io.BytesIO()
 with zipfile.ZipFile(buf,'w',zipfile.ZIP_DEFLATED) as z:
  z.write(package/'PersistentCity.dll','PersistentCity/PersistentCity.dll')
  z.write(probe,'FoodDemandAuditProbe/FoodDemandAuditProbe.dll')
 put(REMOTE+r'\candidate.zip',buf.getvalue())
 script=r'''
$p='PROFILE';$b='BACKUP';$helper='HELPER';if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Rig running'};if(Test-Path $helper){throw 'Existing probe refused'}
$marker='C:\BigAmbitions-AgentTests\agent-install.json';$m=Get-Content -Raw $marker|ConvertFrom-Json
foreach($file in $m.files){if((Get-FileHash -Algorithm SHA256 -LiteralPath $file.path).Hash.ToLowerInvariant() -ne $file.sha256){throw 'Existing manifest mismatch'}}
Expand-Archive -LiteralPath (Join-Path $b 'candidate.zip') -DestinationPath (Join-Path $p 'ModsLocal') -Force
$config=Join-Path $p 'ModsLocal\PersistentCity\persistent-city.json';$settings=Get-Content -Raw $config|ConvertFrom-Json;$settings.Enabled=POPULATION_ENABLED;$settings.PhysicalEnabled=PHYSICAL_ENABLED;$settings|Add-Member -Force -NotePropertyName GameClockFactor -NotePropertyValue CLOCK_FACTOR;$settings|ConvertTo-Json|Set-Content -Encoding UTF8 $config
$changed=@((Join-Path $p 'ModsLocal\PersistentCity\PersistentCity.dll'),$config)+@(Get-ChildItem $helper -File -Recurse|ForEach-Object{$_.FullName})
foreach($path in $changed){$hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant();$entry=@($m.files|Where-Object path -eq $path);if($entry.Count){foreach($e in $entry){$e.sha256=$hash}}else{$m.files+=@{path=$path;sha256=$hash}}}
$m|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 $marker
@{files=@($changed|ForEach-Object{@{path=$_;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash.ToLowerInvariant()}})}|ConvertTo-Json -Depth 5
'''.replace('PROFILE',PROFILE).replace('BACKUP',REMOTE).replace('HELPER',HELPER).replace('CLOCK_FACTOR',str(clock_factor)).replace('POPULATION_ENABLED','$true' if population=='1' else '$false').replace('PHYSICAL_ENABLED','$true' if physical=='1' else '$false')
 record('deployment',json.loads(remote(script,True)));print('Candidate deployed to isolated PC rig',flush=True)
 record('start',json.loads(a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'start','Mode':mode},'agent1')))
 until=time.monotonic()+240
 while not health():
  if time.monotonic()>until:raise RuntimeError('Bridge startup timeout')
  time.sleep(2)

elif operation=='restore':
 if status()['running']:
  try:record('stop',{'output':a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'stop'},'agent1')})
  except Exception as e:
   record('stop-error',{'error':str(e)});until=time.monotonic()+45
   while status()['running'] and time.monotonic()<until:time.sleep(2)
 until=time.monotonic()+45
 while status()['running'] and time.monotonic()<until:time.sleep(2)
 if status()['running']:raise RuntimeError('Rig still running; backup and ownership retained')
 # Collect evidence before restoring the preexisting profile.
 script=r'''
$b='BACKUP';$helper='HELPER';if(Test-Path $helper){Compress-Archive -Path (Join-Path $helper '*.json') -DestinationPath (Join-Path $b 'probe-output.zip') -Force};Copy-Item -LiteralPath 'C:\BigAmbitions-AgentTests\Player-agent.log' -Destination (Join-Path $b 'Player-agent.log');'Saved evidence'
'''.replace('BACKUP',REMOTE).replace('HELPER',HELPER).replace('CLOCK_FACTOR',str(clock_factor)).replace('POPULATION_ENABLED','$true' if population=='1' else '$false').replace('PHYSICAL_ENABLED','$true' if physical=='1' else '$false')
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
'''.replace('PROFILE',PROFILE).replace('BACKUP',REMOTE).replace('HELPER',HELPER).replace('CLOCK_FACTOR',str(clock_factor)).replace('POPULATION_ENABLED','$true' if population=='1' else '$false').replace('PHYSICAL_ENABLED','$true' if physical=='1' else '$false')
 record('cleanup',json.loads(remote(script,True,timeout=300)));record('released',json.loads(a.locks.lock_action('agent1','release')));print('PC rig restored, stopped and released',flush=True)
 record('normal-process-after',json.loads(remote("@(Get-Process -Name 'Big Ambitions' -ErrorAction SilentlyContinue | ForEach-Object{@{pid=$_.Id;started=$_.StartTime.ToUniversalTime().ToString('o');path=$_.Path}})|ConvertTo-Json -Compress") or '[]'))
 for name in ['probe-output.zip','Player-agent.log']:
  if remote('Test-Path -LiteralPath '+a.literal(REMOTE+'\\'+name))=='True':
   subprocess.run(['scp','-q','-o','BatchMode=yes','homepc:'+REMOTE.replace('\\','/')+'/'+name,str(ROOT/name)],check=True,timeout=90)
 if (ROOT/'probe-output.zip').exists():
  with zipfile.ZipFile(ROOT/'probe-output.zip') as z:z.extractall(ROOT/'probe-output')

elif operation=='probe':
 print(json.dumps(probe_call(sys.argv[2], 900),indent=2))
elif operation=='call':
 print(json.dumps(a.call(sys.argv[2],json.loads(sys.argv[3]) if len(sys.argv)>3 else {}),indent=2))
