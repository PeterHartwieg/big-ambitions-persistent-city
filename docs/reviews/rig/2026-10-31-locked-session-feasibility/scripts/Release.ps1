$ErrorActionPreference='Stop';$stage='<private-stage>'
. "$env:USERPROFILE\.big-ambitions-bridge\Control.ps1"
$l=[BridgeControl.LeaseStore]::ForInstance('agent1');$token=[IO.File]::ReadAllText((Join-Path $stage 'lease-token.private'));$l.AssertOwner($token)
if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Rig still running'}
$cleanup=Get-Content (Join-Path $stage 'restore-receipt.json') -Raw|ConvertFrom-Json
if(!$cleanup.restored -or !$cleanup.preferences_hash_matches){throw 'Verified restoration required'}
while((($l.StatusJson()|ConvertFrom-Json).pending) -gt 0){Start-Sleep -Milliseconds 500}
$l.ReleaseJson($token)|Set-Content (Join-Path $stage 'release-receipt.json')
$status=$l.StatusJson()|ConvertFrom-Json
if($status.held -or $status.pending -ne 0){throw 'Release verification failed'}
@{utc=[DateTime]::UtcNow.ToString('o');lease=$status;rig_running=[bool](Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue);original_preferences_verified=$cleanup.preferences_hash_matches;profile_files_verified=$cleanup.profile_files_verified;references_verified=$cleanup.receipt_references_verified}|ConvertTo-Json -Depth 8|Tee-Object -FilePath (Join-Path $stage 'lock-released.json')
