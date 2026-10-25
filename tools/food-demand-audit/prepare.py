from pc import *
import shutil, zipfile
assert json.loads((ROOT/'baseline.json').read_text())['copyVerified']
script=r'''
$m='C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests_Data\Managed';$names=@('mscorlib','System','System.Core','netstandard','BigAmbitions','BigAmbitions.ModAPI','BigAmbitions.AI','BigAmbitions.Items','BigAmbitions.Characters','BigAmbitions.Neighborhoods','HGExtensions','HGPlugins','DayNightCycle','ExternalPlugins','UnityEngine.CoreModule','UnityEngine.IMGUIModule','UnityEngine.TextRenderingModule','UnityEngine.ScreenCaptureModule','UnityEngine.AnimationModule','UnityEngine.PhysicsModule','UnityEngine.AIModule','Newtonsoft.Json');$refs='BACKUP\refs';New-Item -ItemType Directory $refs|Out-Null
foreach($n in $names){Copy-Item -LiteralPath (Join-Path $m ($n+'.dll')) -Destination $refs}
Compress-Archive -Path (Join-Path $refs '*') -DestinationPath 'BACKUP\refs.zip'
Get-ChildItem $refs -File|ForEach-Object{@{name=$_.Name;sha256=(Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()}}|ConvertTo-Json
'''.replace('BACKUP',REMOTE)
record('pc-reference-hashes',json.loads(remote(script,True)))
subprocess.run(['scp','-q','-o','BatchMode=yes','homepc:'+REMOTE.replace('\\','/')+'/refs.zip',str(ROOT/'refs.zip')],check=True,timeout=60)
with zipfile.ZipFile(ROOT/'refs.zip') as z:z.extractall(ROOT/'refs')
# Freeze the campaign's current sources before testing. Never overwrite an existing snapshot.
frozen=REPO
for name in ['Core','Runtime','Persistence','Compatibility','tools','config']:
 if name=='tools':
  (ROOT/'source/tools').mkdir(parents=True,exist_ok=True);shutil.copy2(frozen/'tools/build.py',ROOT/'source/tools/build.py')
 else:shutil.copytree(frozen/name,ROOT/'source'/name,ignore=shutil.ignore_patterns('bin','obj'))
for name in ['Core','Persistence']:
 shutil.copy2(REPO/name/('PersistentCity.'+name+'.csproj'),ROOT/'source'/name/('PersistentCity.'+name+'.csproj'))
package=ROOT/'source/build/PersistentCity';package.mkdir(parents=True)
shutil.copytree(frozen/'build/PersistentCity/Dependencies',package/'Dependencies')
with (ROOT/'native-build.log').open('w') as f:subprocess.run(['python3',str(ROOT/'source/tools/build.py'),'--game-managed',str(ROOT/'refs')],check=True,stdout=f,stderr=subprocess.STDOUT)
print((ROOT/'native-build.log').read_text())
