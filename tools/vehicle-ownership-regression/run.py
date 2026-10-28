#!/usr/bin/env python3
"""Run actual Runtime ownership methods against controlled Unity test doubles."""
from pathlib import Path
import argparse, re, subprocess, tempfile
from xml.sax.saxutils import escape
parser=argparse.ArgumentParser(__doc__)
parser.add_argument('--source-root',type=Path,default=Path(__file__).resolve().parents[2])
args=parser.parse_args();root=args.source_root.resolve();here=Path(__file__).resolve().parent
source=(root/'Runtime/PhysicalVehicles.cs').read_text()
a=source.index('        internal static bool Ordinary(');b=source.index('        private static uint Hash(',a)
# The body block carries the ownership epoch and the per-epoch component verdict memo.
c=source.index('        public string OwnershipLossReason');d=source.index('        public bool RouteFinished',c)
# Every native mutation the adapter performs goes through these epoch-advancing wrappers.
e=source.index('        // Native mutations this adapter performs.');f=source.index('        internal void AssertHealthy()',e)
helpers=source[e:f]
# Per-frame taxi classification and active-taxi count used by the native AddVehicle selector.
g=source.index('        internal static bool IsTaxi(');h=source.index('        public void Disable()',g)

# Static call-site contract for the epoch boundaries the Unity-free doubles cannot execute.
def method(signature):
    start=source.index(signature);open_=source.index('{',start);depth=0
    for i in range(open_,len(source)):
        depth+={'{':1,'}':-1}.get(source[i],0)
        if depth==0: return source[open_+1:i]
    raise SystemExit('unterminated '+signature)
bump='PhysicalVehicleBody.AdvanceOwnershipEpoch();'
failures=[]
for signature in ('        public void Update()','        internal void BeginNativeFixedStep()'):
    if method(signature).strip().split('\n')[0].strip()!=bump: failures.append(signature.strip()+' must advance the epoch first')
release=method('        public void Release(PhysicalVehicleBody body)')
if bump not in release or release.index(bump)>release.index('body.OwnershipLossReason'):
    failures.append('Release must advance the epoch before its ownership verdict')
outside=source[:e]+source[f:]
for raw in ('Manager.RemoveVehicle(','.LoadVehicle(','.SetVehicleTargetWaypoint(','.SetVehicleAction(','RemoveAction!.Invoke(','RemoveAction.Invoke('):
    if raw in outside: failures.append('raw native mutation outside the epoch wrappers: '+raw)
for name in re.findall(r'private static \S+ (\w+)\(',helpers):
    body=method('        private static '+re.search(r'private static (\S+) '+name+r'\(',helpers).group(1)+' '+name+'(')
    if 'finally { '+bump+' }' not in body: failures.append(name+' must advance the epoch in finally')
acquire=method('        public bool TryAcquire(')
if 'LoadNative(' not in acquire or 'TargetNative(' not in acquire or acquire.index('TargetNative(')>acquire.index('if (!body.Valid)'):
    failures.append('TryAcquire must activate and target through the wrappers before its final verdict')
if failures: raise SystemExit('Epoch call-site contract failed:\n  '+'\n  '.join(failures))
print('PASS epoch call sites: Update, BeginNativeFixedStep and Release advance first; '+str(len(re.findall(r'private static \S+ \w+\(',helpers)))+' native mutation wrappers advance in finally; no raw native mutation elsewhere')

with tempfile.TemporaryDirectory(prefix='pc-vehicle-ownership-') as temp:
 p=Path(temp)
 (p/'Methods.cs').write_text('using System; using System.Collections.Generic; using System.Linq; using UnityEngine; using GleyTrafficSystem; using UI; using Helpers; using PersistentCity.Core; namespace PersistentCity.Runtime { partial class PhysicalVehicles {\n'+source[a:b]+'\n'+helpers+'\n'+source[g:h]+'\n} partial class PhysicalVehicleBody {\n'+source[c:d]+'\n} }')
 (p/'Harness.cs').write_text((here/'Harness.cs').read_text())
 (p/'VehicleScriptOwnership.cs').write_text((root/'Runtime/VehicleScriptOwnership.cs').read_text())
 (p/'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><AssemblyName>BigAmbitions.OwnershipHarness</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="'+escape(str(root/'Core/PersistentCity.Core.csproj'))+'" /></ItemGroup></Project>')
 subprocess.run(['dotnet','run','--project',str(p/'Harness.csproj'),'--configuration','Release'],check=True)
