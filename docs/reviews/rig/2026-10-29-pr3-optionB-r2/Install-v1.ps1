param([ValidateSet('before','after')][string]$Build='before')
$ErrorActionPreference='Stop';$stage='<private-evidence>';$profile=Join-Path $env:USERPROFILE 'AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests'
. "$env:USERPROFILE\.big-ambitions-bridge\Control.ps1"
$l=[BridgeControl.LeaseStore]::ForInstance('agent1');$p=$l.Begin([IO.File]::ReadAllText((Join-Path $stage 'lease-token.private')),'lifecycle')
try{
 if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Rig running'}
 if(!(Test-Path (Join-Path $stage 'backup-receipt.json'))){throw 'Verified backup required'}
 $source=Join-Path (Split-Path (Get-Location) -Parent) ('rig-pr3-'+$Build+'\build\PersistentCity')
 Copy-Item -LiteralPath (Join-Path $source 'PersistentCity.dll') -Destination (Join-Path $profile 'ModsLocal\PersistentCity\PersistentCity.dll') -Force
 Copy-Item -LiteralPath (Join-Path $source 'persistent-city.json') -Destination (Join-Path $profile 'ModsLocal\PersistentCity\persistent-city.json') -Force
 foreach($name in @('PR3RigGuard','FoodDemandAuditProbe')){New-Item -ItemType Directory -Force (Join-Path $profile ('ModsLocal\'+$name)) | Out-Null}
 Copy-Item -LiteralPath (Join-Path $stage ($Build+'\PR3RigGuard.dll')) -Destination (Join-Path $profile 'ModsLocal\PR3RigGuard\PR3RigGuard.dll') -Force
 Copy-Item -LiteralPath '<private-fixtures>\performance-pc-20261027\functional-probe-v3.dll' -Destination (Join-Path $profile 'ModsLocal\FoodDemandAuditProbe\FoodDemandAuditProbe.dll') -Force
 $receipt=Get-Content C:\BigAmbitions-AgentTests\agent-install.json -Raw|ConvertFrom-Json
 foreach($f in $receipt.files){$f.sha256=(Get-FileHash -LiteralPath $f.path).Hash.ToLowerInvariant()}
 $receipt|ConvertTo-Json -Depth 12|Set-Content -Encoding UTF8 C:\BigAmbitions-AgentTests\agent-install.json
 @{build=$Build;dll=(Get-FileHash (Join-Path $source 'PersistentCity.dll')).Hash.ToLowerInvariant();helper=(Get-FileHash (Join-Path $profile 'ModsLocal\FoodDemandAuditProbe\FoodDemandAuditProbe.dll')).Hash.ToLowerInvariant();utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Tee-Object -FilePath (Join-Path $stage ($Build+'-installed.json'))
}finally{$l.End($p)}
