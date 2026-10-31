param([string]$Arm)
$ErrorActionPreference='Stop'
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;" + $env:PATH
$stage='<private-root>/HART-Builds/persistent-city-rig-locked-a1sync'
. "$env:USERPROFILE/.big-ambitions-bridge/Control.ps1"
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$permit=$lease.Begin([IO.File]::ReadAllText((Join-Path $stage 'lease-token.private')),'lifecycle')
try {
 if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Owned game must be stopped'}
 python (Join-Path $stage 'runtime_tooling.py') $Arm
 if($LASTEXITCODE){throw 'Install failed'}
}finally{$lease.End($permit)}
& (Join-Path $stage 'Launch-Rig.ps1')
