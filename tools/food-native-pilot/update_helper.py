"""Replace only this campaign's helper while its owned rig is stopped; retain the previous helper."""
from protocol import *
import io,zipfile,sys,re
label=sys.argv[1]
assert re.fullmatch(r'r[0-9]+',label)
assert not status()['running']
buf=io.BytesIO()
with zipfile.ZipFile(buf,'w',zipfile.ZIP_DEFLATED) as z:
 z.write(ROOT/'Probe/FoodNativePilotProbe.dll','FoodNativePilotProbe/FoodNativePilotProbe.dll')
 if '--helper-only' not in sys.argv:z.write(package/'PersistentCity.dll','PersistentCity/PersistentCity.dll')
put(REMOTE+'\\helper-'+label+'.zip',buf.getvalue())
script=r'''
$ErrorActionPreference='Stop';$helper='HELPER';$b='BACKUP';$label='LABEL'
if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){throw 'Rig running'}
$marker='C:\BigAmbitions-AgentTests\agent-install.json';$m=Get-Content -Raw $marker|ConvertFrom-Json
foreach($f in $m.files){if((Get-FileHash -Algorithm SHA256 -LiteralPath $f.path).Hash.ToLowerInvariant() -ne $f.sha256){throw 'Manifest mismatch'}}
Move-Item -LiteralPath $helper -Destination (Join-Path $b ('helper-before-'+$label))
$core=Join-Path (Split-Path $helper -Parent) 'PersistentCity\PersistentCity.dll';Copy-Item -LiteralPath $core -Destination (Join-Path $b ('PersistentCity-before-'+$label+'.dll'))
Copy-Item -LiteralPath 'C:\BigAmbitions-AgentTests\Player-agent.log' -Destination (Join-Path $b ('Player-before-'+$label+'.log'))
Expand-Archive -LiteralPath (Join-Path $b ('helper-'+$label+'.zip')) -DestinationPath (Split-Path $helper -Parent) -Force
$m.files=@($m.files|Where-Object { !$_.path.StartsWith($helper) })
foreach($f in $m.files){if($f.path -eq $core){$f.sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $core).Hash.ToLowerInvariant()}}
Get-ChildItem -LiteralPath $helper -File|ForEach-Object{$m.files+=@{path=$_.FullName;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()}}
$m|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 $marker
@{helper=$helper;label=$label;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $helper 'FoodNativePilotProbe.dll')).Hash.ToLowerInvariant();coreSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $core).Hash.ToLowerInvariant()}|ConvertTo-Json
'''.replace('HELPER',HELPER).replace('BACKUP',REMOTE).replace('LABEL',label)
print(remote(script,True))
print(a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'start','Mode':'background'},'agent1'))
