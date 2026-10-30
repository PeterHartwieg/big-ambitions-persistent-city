param([ValidateSet('alone','before','after')][string]$Order='alone',[string]$Arm='workday',[int]$WaitForPriorPid=0)
$ErrorActionPreference='Stop'
$stage=Join-Path $env:USERPROFILE 'HART-Builds\persistent-city-perf-review-2026-10-30-r2'
if($WaitForPriorPid){Wait-Process -Id $WaitForPriorPid -ErrorAction SilentlyContinue}
if(Test-Path 'C:\BigAmbitions-AgentTests\Player-agent.log'){Copy-Item -LiteralPath 'C:\BigAmbitions-AgentTests\Player-agent.log' -Destination (Join-Path $stage ($Arm+'-prior.log'))}
. "$env:USERPROFILE\.big-ambitions-bridge\Control.ps1"
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$token=[IO.File]::ReadAllText((Join-Path $stage 'lease-token.private'))
$permit=$lease.Begin($token,'lifecycle')
$process=$null
try {
 if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Agent1 still running'}
 python (Join-Path $PSScriptRoot 'runtime_tooling.py') $Order
 if($LASTEXITCODE){throw 'Installation failed'}
 $receipt=Get-Content 'C:\BigAmbitions-AgentTests\agent-install.json' -Raw|ConvertFrom-Json
 foreach($f in $receipt.files){if((Get-FileHash -LiteralPath $f.path).Hash.ToLowerInvariant() -ne $f.sha256){throw 'Artifact verification failed'}}
 $process=Start-Process -FilePath 'C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests.exe' -WorkingDirectory 'C:\BigAmbitions-AgentTests' -ArgumentList @('-agent-tests','-disable-force-steam','-console','-foodtrucks-test','-drivenrides-test','-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-logFile','"C:\BigAmbitions-AgentTests\Player-agent.log"') -WindowStyle Hidden -PassThru
 @{pid=$process.Id;started_ticks=$process.StartTime.ToUniversalTime().Ticks.ToString();exe=$process.Path;mode='windowed';port=18330}|ConvertTo-Json|Set-Content -Encoding UTF8 'C:\BigAmbitions-AgentTests\run.json'
} finally {$lease.End($permit)}
$driverExit=-1
try {
 python (Join-Path $PSScriptRoot 'run_workday.py') $Arm
 $driverExit=$LASTEXITCODE
} finally {
 $process.Refresh()
 if(!$process.HasExited){
  $env:BA_LOCK_TOKEN=$token
  try {& 'C:\BigAmbitions-AgentTests\Agent-Windows.ps1' -Action stop}catch {& 'C:\BigAmbitions-AgentTests\Agent-Windows.ps1' -Action stop -Force}
 }
 $process.WaitForExit()
 Copy-Item -LiteralPath 'C:\BigAmbitions-AgentTests\Player-agent.log' -Destination (Join-Path $stage ($Arm+'-native.log'))
 @{arm=$Arm;order=$Order;driverExit=$driverExit;gameExit=$process.ExitCode;stopped=$true;utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $stage ($Arm+'-process-end.json'))
}
if($driverExit){throw ('Measurement driver failed: '+$Arm)}
