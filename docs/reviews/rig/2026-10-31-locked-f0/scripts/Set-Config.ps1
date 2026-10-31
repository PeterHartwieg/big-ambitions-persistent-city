param([ValidateSet('off','on','zero','tiny','disabled')][string]$Mode)
$ErrorActionPreference='Stop'
$stage='<PRIVATE_STAGE>'
. "$env:USERPROFILE/.big-ambitions-bridge/Control.ps1"
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$permit=$lease.Begin([IO.File]::ReadAllText((Join-Path $stage 'lease-token.private')),'lifecycle')
try {
 $path=Join-Path $env:USERPROFILE 'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/ModsLocal/PersistentCity/persistent-city.json'
 $config=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
 $config|Add-Member -NotePropertyName InteriorCityView -NotePropertyValue $(if($Mode -eq 'off'){'off'}else{'on'}) -Force
 $config|Add-Member -NotePropertyName InteriorMaximumPedestrians -NotePropertyValue $(if($Mode -eq 'zero'){0}elseif($Mode -eq 'tiny'){2}else{24}) -Force
 $config|Add-Member -NotePropertyName InteriorMaximumVehicles -NotePropertyValue $(if($Mode -eq 'zero'){0}elseif($Mode -eq 'tiny'){1}else{12}) -Force
 $config.Enabled=$Mode -ne 'disabled'
 $config|ConvertTo-Json|Set-Content -LiteralPath $path -Encoding UTF8
 @{mode=$Mode;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $stage ('config-'+$Mode+'-'+[DateTime]::UtcNow.Ticks+'.json'))
}finally{$lease.End($permit)}
