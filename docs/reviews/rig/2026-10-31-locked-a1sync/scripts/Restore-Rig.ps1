$ErrorActionPreference='Stop'
$stage='<private-root>\HART-Builds\persistent-city-rig-locked-a1sync'
$profile='<private-root>\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests'
$backup=Join-Path $stage 'backup'
$tested=Join-Path $stage 'tested-profile'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Plain([string]$path){
 $part=[IO.Path]::GetFullPath($path)
 while($part){if((Test-Path -LiteralPath $part) -and ((Get-Item -Force -LiteralPath $part).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Reparse point refused: $part"};$parent=Split-Path $part -Parent;if($parent -eq $part){break};$part=$parent}
}
. "$env:USERPROFILE/.big-ambitions-bridge/Control.ps1"
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$permit=$lease.Begin([IO.File]::ReadAllText((Join-Path $stage 'lease-token.private')),'lifecycle')
try{
 if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Stop isolated rig before restoring'}
 if([IO.Path]::GetFullPath($profile) -ne '<private-root>\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests'){throw 'Unexpected profile target'}
 if(![IO.Path]::GetFullPath($tested).StartsWith($stage+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected evidence target'}
 Plain $profile;Plain $tested;Plain (Join-Path $backup 'profile')
 if(Test-Path -LiteralPath $tested){throw 'Tested profile already retained'}
 Copy-Item C:\BigAmbitions-AgentTests\Player-agent.log (Join-Path $stage 'native-final.log')
 Copy-Item C:\BigAmbitions-AgentTests\agent-install.json (Join-Path $stage 'tested-agent-install.json')
 Copy-Item C:\BigAmbitions-AgentTests\run.json (Join-Path $stage 'tested-run.json')
 reg export 'HKCU\Software\Hovgaard Games\Big Ambitions Agent Tests' (Join-Path $stage 'tested-preferences.reg') /y | Out-Null
 if($LASTEXITCODE){throw 'Could not retain test preferences'}
 Move-Item -LiteralPath $profile -Destination $tested
 $null=robocopy (Join-Path $backup 'profile') $profile /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /XJ
 if($LASTEXITCODE -ge 8){throw 'Profile restore copy failed'}
 $registry='HKCU:\Software\Hovgaard Games\Big Ambitions Agent Tests'
 if($registry -ne 'HKCU:\Software\Hovgaard Games\Big Ambitions Agent Tests'){throw 'Unexpected registry key'}
 if(Test-Path -LiteralPath $registry){Remove-Item -LiteralPath $registry -Recurse}
 reg import (Join-Path $backup 'preferences.reg') | Out-Null
 if($LASTEXITCODE){throw 'Preference restore failed'}
 foreach($file in @('agent-install.json','run.json','Player-agent.log')){Copy-Item -LiteralPath (Join-Path $backup $file) -Destination (Join-Path 'C:\BigAmbitions-AgentTests' $file) -Force}
 $inventory=Get-Content (Join-Path $backup 'profile-hashes.json') -Raw|ConvertFrom-Json
 $actual=@(Get-ChildItem -LiteralPath $profile -Recurse -File)
 if($actual.Count -ne $inventory.Count){throw 'Restored file inventory differs'}
 foreach($file in $inventory){if((Hash (Join-Path $profile $file.file)) -ne $file.sha256){throw "Restored profile hash mismatch: $($file.file)"}}
 $receipt=Get-Content C:\BigAmbitions-AgentTests\agent-install.json -Raw|ConvertFrom-Json
 foreach($file in $receipt.files){if((Hash $file.path) -ne $file.sha256){throw "Original rig reference hash mismatch: $($file.path)"}}
 reg export 'HKCU\Software\Hovgaard Games\Big Ambitions Agent Tests' (Join-Path $stage 'restored-preferences.reg') /y | Out-Null
 if($LASTEXITCODE){throw 'Could not verify restored preferences'}
 $prefsMatch=(Hash (Join-Path $backup 'preferences.reg')) -eq (Hash (Join-Path $stage 'restored-preferences.reg'))
 if(!$prefsMatch){throw 'Restored preference export hash differs'}
 $rootHashes=@{};foreach($file in @('agent-install.json','run.json','Player-agent.log')){
  $current=Hash (Join-Path 'C:\BigAmbitions-AgentTests' $file);$original=Hash (Join-Path $backup $file)
  if($current -ne $original){throw "Root file restore mismatch: $file"};$rootHashes[$file]=$current
 }
 @{restored=$true;utc=[DateTime]::UtcNow.ToString('o');profile_files_verified=$inventory.Count;receipt_references_verified=@($receipt.files).Count;preferences_sha256=(Hash (Join-Path $stage 'restored-preferences.reg'));preferences_hash_matches=$prefsMatch;root_file_hashes=$rootHashes;tested_profile=$tested;rig_running=[bool](Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue)}|ConvertTo-Json -Depth 8|Tee-Object -FilePath (Join-Path $stage 'restore-receipt.json')
}finally{$lease.End($permit)}
