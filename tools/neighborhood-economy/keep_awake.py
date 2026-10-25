"""Hold Windows system wakefulness only for the currently owned isolated rig process."""
from pathlib import Path
import sys,os,json
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'food-demand-audit'))
from pc import *
owned=json.loads(a.locks.lock_action('agent1','status'));assert owned['owner']==os.environ['BA_LOCK_OWNER']
rig=status();assert rig['running'] and rig['pid']
worker=r'''
param([int]$RigPid,[long]$RigStarted)
$ErrorActionPreference='Stop'
$rig=Get-Process -Id $RigPid
if($rig.Path -ne 'C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests.exe' -or $rig.StartTime.ToUniversalTime().Ticks -ne $RigStarted){throw 'Rig identity changed'}
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class RigWake { [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags); }'
if([RigWake]::SetThreadExecutionState([uint32]2147483649) -eq 0){throw 'System wake request failed'}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot ('keep-awake-'+$RigPid+'.json')),'{"active":true}')
try { while(!$rig.HasExited){[void]$rig.WaitForExit(30000);$rig.Refresh()} }
finally { [void][RigWake]::SetThreadExecutionState([uint32]2147483648); [IO.File]::WriteAllText((Join-Path $PSScriptRoot ('keep-awake-'+$RigPid+'.json')),'{"active":false}') }
'''
path=REMOTE+r'\keep-rig-awake.ps1'
script="[IO.File]::WriteAllText("+a.literal(path)+","+a.literal(worker)+")\n"
script+="$rig=Get-Process -Id "+str(rig['pid'])+";if($rig.Path -ne 'C:\\BigAmbitions-AgentTests\\Big Ambitions Agent Tests.exe'){throw 'Wrong rig process'}\n"
script+="$rigWakeArguments=@('-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File','\""+path+"\"','-RigPid',"+str(rig['pid'])+",'-RigStarted',$rig.StartTime.ToUniversalTime().Ticks);$worker=Start-Process powershell.exe -ArgumentList $rigWakeArguments -WindowStyle Hidden -PassThru\n"
script+="@{workerPid=$worker.Id;rigPid=$rig.Id;rigStarted=$rig.StartTime.ToUniversalTime().ToString('o');scope='System-required only until this rig process exits; no power policy changes'}|ConvertTo-Json"
print(json.dumps(record('rig-keep-awake-'+str(rig['pid']),json.loads(remote(script,True))),indent=2))
