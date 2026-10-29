$ErrorActionPreference='Stop'
$stage='<private-evidence>'
. "$env:USERPROFILE\.big-ambitions-bridge\Control.ps1"
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
while($true){
 $status=$lease.StatusJson()|ConvertFrom-Json
 @{utc=[DateTime]::UtcNow.ToString('o');status=$status}|ConvertTo-Json -Depth 8 -Compress|Add-Content (Join-Path $stage 'lease-wait.jsonl')
 if(!$status.held -and !$status.legacy_blocked){
  $token=[Guid]::NewGuid().ToString('N')
  try{$acquired=$lease.AcquireJson('persistent-city-pr3-ob-rig','PR3 isolated before/after strict financial migration checkout QA',$token)|ConvertFrom-Json
   if($acquired.held -and $acquired.owner -eq 'persistent-city-pr3-ob-rig'){
    [IO.File]::WriteAllText((Join-Path $stage 'lease-token.private'),$token)
    $acquired|ConvertTo-Json -Depth 8|Set-Content (Join-Path $stage 'lease-acquired.json')
    Write-Output 'Lease acquired safely';break
   }
  }catch{Write-Output 'Lease acquisition race; waiting'}
 }
 Write-Output ('Waiting: agent1 owner='+$status.owner+' pending='+$status.pending)
 Start-Sleep -Seconds 30
}
