$ErrorActionPreference='Stop'
$e='<private-stage>'
. <user-home>\.big-ambitions-bridge\Control.ps1
$lease=[BridgeControl.LeaseStore]::ForInstance('agent1')
$permit=$lease.Begin([IO.File]::ReadAllText((Join-Path $e 'lease-token.private')),'lifecycle')
try {
 if(Get-CimInstance Win32_Process|Where-Object {$_.Name -like '*Ambitions*.exe'}){throw 'Game running'}
 $r=Get-Content C:\BigAmbitions-AgentTests\agent-install.json -Raw|ConvertFrom-Json
 foreach($f in $r.files){if((Get-FileHash -LiteralPath $f.path).Hash.ToLowerInvariant() -ne $f.sha256){throw ('Artifact mismatch '+$f.path)}}
 $p=Start-Process -FilePath 'C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests.exe' -WorkingDirectory 'C:\BigAmbitions-AgentTests' -ArgumentList @('-agent-tests','-disable-force-steam','-console','-foodtrucks-test','-drivenrides-test','-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-logFile','"C:\BigAmbitions-AgentTests\Player-agent.log"') -WindowStyle Normal -PassThru
 @{pid=$p.Id;started_ticks=$p.StartTime.ToUniversalTime().Ticks.ToString();exe=$p.Path;mode='windowed';port=18330}|ConvertTo-Json|Set-Content -Encoding UTF8 C:\BigAmbitions-AgentTests\run.json
 Write-Output ('Foreground-owned rig PID '+$p.Id)
} finally {$lease.End($permit)}

$p.WaitForExit()
Write-Output ('Rig exited '+$p.ExitCode)
