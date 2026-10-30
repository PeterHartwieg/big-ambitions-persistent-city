# rig_test: always stop the verified agent1 process, restore the complete backup, then release.
$ErrorActionPreference='Stop'
$stage=Join-Path $env:USERPROFILE 'HART-Builds\persistent-city-perf-review-2026-10-30-r2'
. "$env:USERPROFILE\.big-ambitions-bridge\Control.ps1"
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$token=[IO.File]::ReadAllText((Join-Path $stage 'lease-token.private'))
$lease.AssertOwner($token)
$run=Get-Content 'C:\BigAmbitions-AgentTests\run.json' -Raw|ConvertFrom-Json
$process=Get-Process -Id $run.pid -ErrorAction SilentlyContinue
if($process){
 if($process.Path -ne 'C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests.exe' -or $process.StartTime.ToUniversalTime().Ticks.ToString() -ne $run.started_ticks){throw 'Refusing mismatched process'}
 $env:BA_LOCK_TOKEN=$token
 try{& 'C:\BigAmbitions-AgentTests\Agent-Windows.ps1' -Action stop}catch{
  # A broken bridge must not prevent restoration; this validates identity again and owns a terminate permit.
  & 'C:\BigAmbitions-AgentTests\Agent-Windows.ps1' -Action stop -Force
 }
 $process.WaitForExit()
}
& (Join-Path $stage 'Restore-Rig.ps1')
& (Join-Path $stage 'Release.ps1')
