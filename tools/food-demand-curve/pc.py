"""Owned disposable Windows rig; helpers never address the normal game."""
from pathlib import Path
import os, importlib.machinery, importlib.util, json, hashlib, subprocess, base64
REPO=Path(__file__).resolve().parents[2]
ROOT=REPO/'build/food-demand-curve-20261022'
os.environ['BA_LOCK_OWNER']='persistent-city-demand-curve-20261022'
p=REPO.parent/'big-ambitions-driven-rides/tools/agent-tests/agent'
l=importlib.machinery.SourceFileLoader('curve_pc_agent',str(p));s=importlib.util.spec_from_loader(l.name,l);a=importlib.util.module_from_spec(s);l.exec_module(a)
REMOTE=r'C:\BigAmbitions-AgentTests-Rollback\FoodDemandCurve-20261022'
PROFILE=r'C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests'
HELPER=PROFILE+r'\ModsLocal\FoodDemandCurveProbe'
def remote(script,mutate=False,timeout=60,kind="lifecycle"):
 if mutate:
  script=a.locks.PREFIX+"\n$lease=[BridgeControl.LeaseStore]::ForInstance('agent1');$permit=$lease.Begin($inputData.token,"+a.literal(kind)+");try{\n"+script+"\n}finally{$lease.End($permit)}"
  return a.remote(script,timeout=timeout,payload={'token':a.locks.token('agent1')})
 return a.remote(script,timeout=timeout)
def record(name,obj):
 (ROOT/(name+'.json')).write_text(json.dumps(obj,indent=2));return obj
def call(name,cmd,args=None):return record(name,a.call(cmd,args or {}))
def status():return json.loads(a.remote("& 'C:\\BigAmbitions-AgentTests\\Agent-Windows.ps1' -Action status"))
def put(path,data):
 # Small chunks avoid the host's historically unreliable large stdin transfers.
 data=base64.b64encode(data).decode(); parts=[data[i:i+4000] for i in range(0,len(data),4000)]
 remote("[IO.File]::WriteAllText("+a.literal(path+'.b64')+",'')",True)
 for part in parts:remote("[IO.File]::AppendAllText("+a.literal(path+'.b64')+","+a.literal(part)+")",True)
 remote("[IO.File]::WriteAllBytes("+a.literal(path)+",[Convert]::FromBase64String([IO.File]::ReadAllText("+a.literal(path+'.b64')+")));Remove-Item -LiteralPath "+a.literal(path+'.b64'),True)

def get(path):return remote("[IO.File]::ReadAllText("+a.literal(path)+")")
if __name__=='__main__':
 assert not status()['running']
 script=r'''
$ErrorActionPreference='Stop';$p='PROFILE';$b='BACKUP';if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Rig running'};if(Test-Path $b){throw 'Baseline already exists'}
New-Item -ItemType Directory -Path $b|Out-Null
function Inventory($dir){@(Get-ChildItem -LiteralPath $dir -Recurse -File|Sort-Object FullName|ForEach-Object{@{path=$_.FullName.Substring($dir.Length+1);bytes=$_.Length;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})}
$before=Inventory $p;$before|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $b 'baseline-profile.json')
& robocopy $p (Join-Path $b 'profile') /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ /NFL /NDL /NJH /NJS /NP|Out-Null;if($LASTEXITCODE -ge 8){throw 'Profile copy failure'}
$copy=Inventory (Join-Path $b 'profile');$mismatch=@(Compare-Object ($before|ForEach-Object{$_.path+'|'+$_.bytes+'|'+$_.sha256}) ($copy|ForEach-Object{$_.path+'|'+$_.bytes+'|'+$_.sha256}));if($mismatch.Count){throw 'Baseline verify mismatch'}
Copy-Item -LiteralPath 'C:\BigAmbitions-AgentTests\agent-install.json' -Destination (Join-Path $b 'agent-install.json')
$key='HKCU\Software\Hovgaard Games\Big Ambitions Agent Tests';$hadPrefs=Test-Path 'HKCU:\Software\Hovgaard Games\Big Ambitions Agent Tests';if($hadPrefs){& reg export $key (Join-Path $b 'preferences.reg') /y|Out-Null;if($LASTEXITCODE){throw 'Preferences export failed'}}
@{backup=$b;profile=$p;files=$before.Count;bytes=($before|ForEach-Object{$_.bytes}|Measure-Object -Sum).Sum;copyVerified=$true;hadPreferences=$hadPrefs;inventory=$before}|ConvertTo-Json -Depth 6
'''.replace('PROFILE',PROFILE).replace('BACKUP',REMOTE)
 d=record('baseline',json.loads(remote(script,True,timeout=300)));print(json.dumps({k:v for k,v in d.items() if k!='inventory'}))
