#!/usr/bin/env python3
"""Run exact production command methods with deferred Unity-command test doubles."""
from pathlib import Path
import subprocess
import tempfile
import sys
from xml.sax.saxutils import escape
import os, shutil
# Prefer dotnet on PATH; otherwise use the per-user SDK install the offline gate specifies.
dotnet = shutil.which('dotnet') or str(Path(os.environ.get('LOCALAPPDATA', '')) / 'Microsoft/dotnet/dotnet.exe')
root = Path(__file__).resolve().parents[2]
source = (root / 'Runtime/PhysicalVehicles.cs').read_text()
start = source.index('        internal void BeginNativeFixedStep()')
end = source.index('        public void Update()', start)
methods = source[start:end]
body_start = source.index('        internal void RecordReached(')
body_end = source.index('        public string Id {', body_start)
hooks = (root / 'Runtime/PhysicalTrafficHooks.cs').read_text()
hook_start = hooks.index('        private static void FixedStepPrefix(')
hook_end = hooks.index('        private static void SpeedChangingPrefix(', hook_start)
with tempfile.TemporaryDirectory(prefix='pc-native-command-') as folder:
    folder = Path(folder)
    (folder / 'Methods.cs').write_text('using System; using System.Linq; using UnityEngine; using PersistentCity.Core; namespace CommandHarness { partial class PhysicalVehicles {\n' + methods + '\n} partial class PhysicalVehicleBody {\n' + source[body_start:body_end] + '\n} partial class PhysicalTrafficHooks {\n' + hooks[hook_start:hook_end] + '\n} }')
    (folder / 'Harness.cs').write_text((Path(__file__).parent / 'Harness.cs').read_text())
    (folder / 'NativeIl.cs').write_text((Path(__file__).parent / 'NativeIl.cs').read_text())
    (folder / 'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="'+ escape(str(root / 'Core/PersistentCity.Core.csproj')) +'" /><Reference Include="0Harmony"><HintPath>'+ escape(str(root / 'build/PersistentCity/Dependencies/0Harmony.dll')) +'</HintPath></Reference></ItemGroup></Project>')
    subprocess.run([dotnet, 'run', '--project', str(folder / 'Harness.csproj'), '--configuration', 'Release', '--'] + sys.argv[1:], check=True)
