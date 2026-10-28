#!/usr/bin/env python3
"""Exercise the actual optional-stop physical adapter methods with movement doubles."""
from pathlib import Path
import subprocess, tempfile
from xml.sax.saxutils import escape
root=Path(__file__).resolve().parents[2]
source=(root/'Runtime/PhysicalPopulation.cs').read_text()
methods=source[source.index('        private bool OptionalVisible('):source.index('        private bool MotionPermission(')]
standing=source[source.index('        private IEnumerable<KeyValuePair<string, int>> Standing('):source.index('        private bool TryLine(')]
with tempfile.TemporaryDirectory(prefix='pc-optional-stop-') as folder:
    folder=Path(folder)
    (folder/'Methods.cs').write_text('using System; using System.Linq; using System.Collections.Generic; using PersistentCity.Core; partial class Harness {\n'+methods+standing+'\n}')
    (folder/'Harness.cs').write_text((Path(__file__).parent/'Harness.cs.txt').read_text())
    (folder/'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="'+escape(str(root/'Core/PersistentCity.Core.csproj'))+'" /></ItemGroup></Project>')
    subprocess.run(['dotnet','run','--project',str(folder/'Harness.csproj'),'-c','Release'],check=True)
