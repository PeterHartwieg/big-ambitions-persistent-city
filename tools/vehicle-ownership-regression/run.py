#!/usr/bin/env python3
"""Run actual Runtime ownership methods against controlled Unity test doubles."""
from pathlib import Path
import argparse, subprocess, tempfile
from xml.sax.saxutils import escape
parser=argparse.ArgumentParser(__doc__)
parser.add_argument('--source-root',type=Path,default=Path(__file__).resolve().parents[2])
args=parser.parse_args();root=args.source_root.resolve();here=Path(__file__).resolve().parent
source=(root/'Runtime/PhysicalVehicles.cs').read_text()
a=source.index('        internal static bool Ordinary(');b=source.index('        private static uint Hash(',a)
c=source.index('        public string OwnershipLossReason');d=source.index('        public bool RouteFinished',c)
with tempfile.TemporaryDirectory(prefix='pc-vehicle-ownership-') as temp:
 p=Path(temp)
 (p/'Methods.cs').write_text('using System; using System.Linq; using UnityEngine; using GleyTrafficSystem; using UI; using Helpers; using PersistentCity.Core; namespace PersistentCity.Runtime { partial class PhysicalVehicles {\n'+source[a:b]+'\n} partial class PhysicalVehicleBody {\n'+source[c:d]+'\n} }')
 (p/'Harness.cs').write_text((here/'Harness.cs').read_text())
 (p/'VehicleScriptOwnership.cs').write_text((root/'Runtime/VehicleScriptOwnership.cs').read_text())
 (p/'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><AssemblyName>BigAmbitions.OwnershipHarness</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="'+escape(str(root/'Core/PersistentCity.Core.csproj'))+'" /></ItemGroup></Project>')
 subprocess.run(['dotnet','run','--project',str(p/'Harness.csproj'),'--configuration','Release'],check=True)
