param([ValidateSet('cand-tiny','cand-zero','cand-on-restored','cand-disabled','cand-on-logical')][string]$Mode)
$ErrorActionPreference='Stop'
$stage='<PRIVATE_STAGE>'
. "$env:USERPROFILE/.big-ambitions-bridge/Control.ps1"
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$permit=$lease.Begin([IO.File]::ReadAllText((Join-Path $stage 'lease-token.private')),'lifecycle')
try {
 if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Owned game must be stopped'}
 $installMode=if($Mode -in @('cand-on-restored','cand-on-logical')){'cand-on'}else{$Mode}
 python (Join-Path $stage 'runtime_tooling.py') $installMode
 if($LASTEXITCODE){throw 'Isolated install failed'}
}finally{$lease.End($permit)}
& (Join-Path $stage 'Launch-Rig.ps1')
