$ErrorActionPreference='Stop'
$stage='<private-evidence>'
$profile='<agent1-profile>'
. <user-home>\.big-ambitions-bridge\Control.ps1
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$permit=$lease.Begin([IO.File]::ReadAllText((Join-Path $stage 'lease-token.private')),'lifecycle')
function Hash($p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
try {
 if(Get-CimInstance Win32_Process|Where-Object {$_.Name -like '*Ambitions*.exe'}){throw 'Game process exists'}
 $backup=Join-Path $stage 'backup'
 if(Test-Path $backup){throw 'Backup exists; never overwrite'}
 New-Item -ItemType Directory $backup|Out-Null
 if(Get-ChildItem -LiteralPath $profile -Recurse -Force|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Profile reparse point'}
 $inventory=@(Get-ChildItem -LiteralPath $profile -Recurse -File|ForEach-Object {@{file=$_.FullName.Substring($profile.Length+1);sha256=(Hash $_.FullName)}})
 $inventory|ConvertTo-Json -Depth 5|Set-Content (Join-Path $backup 'profile-hashes.json')
 $null=robocopy $profile (Join-Path $backup 'profile') /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /XJ
 if($LASTEXITCODE -ge 8){throw 'Backup copy failed'}
 foreach($f in $inventory){if((Hash (Join-Path (Join-Path $backup 'profile') $f.file)) -ne $f.sha256){throw "Backup mismatch $($f.file)"}}
 foreach($f in @('agent-install.json','run.json','Player-agent.log')){Copy-Item -LiteralPath (Join-Path 'C:\BigAmbitions-AgentTests' $f) -Destination $backup}
 reg export 'HKCU\Software\Hovgaard Games\Big Ambitions Agent Tests' (Join-Path $backup 'preferences.reg') /y|Out-Null
 if($LASTEXITCODE){throw 'Registry backup failed'}
 $receipt=Get-Content (Join-Path $backup 'agent-install.json') -Raw|ConvertFrom-Json
 foreach($f in $receipt.files){if((Hash $f.path) -ne $f.sha256){throw "Existing receipt mismatch $($f.path)"}}
 @{utc=[DateTime]::UtcNow.ToString('o');verified=$true;profile_files=$inventory.Count;receipt_references=@($receipt.files).Count;receipt_sha256=(Hash (Join-Path $backup 'agent-install.json'))}|ConvertTo-Json|Tee-Object -FilePath (Join-Path $stage 'backup-receipt.json')
}finally{$lease.End($permit)}
