#!/usr/bin/env python3
"""Exercise the actual optional-stop physical adapter methods with movement doubles."""
from pathlib import Path
import subprocess, tempfile
from xml.sax.saxutils import escape
root=Path(__file__).resolve().parents[2]
source=(root/'Runtime/PhysicalPopulation.cs').read_text()
methods=source[source.index('        private void OnOptionalDeparture('):source.index('        private bool MotionPermission(')]
arrival=source[source.index('        private bool TryAwaitArrival('):source.index('        /// <summary>Join the door', source.index('        private bool TryAwaitArrival('))]
standing=source[source.index('        private IEnumerable<KeyValuePair<string, int>> Standing('):source.index('        private bool TryLine(')]
native=(root/'Runtime/OptionalStopEnvironment.cs').read_text()
geometry=native[native.index('        private OptionalStopAvailability GeometryStatus('):native.index('        private static bool Sample(')]
discovery=native[native.index('        public IReadOnlyList<OptionalStopSpot> Spots('):native.index('        private bool NativeDoorPresent(')]
native=native[native.index('        private bool NativeDoorPresent('):native.index('        private IReadOnlyList<OptionalStopSpot> ParkEdge(')]
dwell=(root/'Runtime/DwellSpots.cs').read_text()
dwell=dwell[dwell.index('namespace PersistentCity.Runtime'):dwell.index('        private static DoorQueue? Sample(')]
dwell += '        public static int SampleCalls;\n        private static DoorQueue? Sample(string key, Place place)\n        { SampleCalls++; return new DoorQueue(key, place.Position, place.Position, new[] { place.Position }); }\n    }\n}\n'
with tempfile.TemporaryDirectory(prefix='pc-optional-stop-') as folder:
    folder=Path(folder)
    (folder/'Methods.cs').write_text('using System; using System.Linq; using System.Collections.Generic; using PersistentCity.Core; partial class Harness {\n'+methods+arrival+standing+'\n}')
    (folder/'NativeDoor.cs').write_text((Path(__file__).parent/'NativeDoor.cs.txt').read_text().replace('/* PRODUCTION METHOD */',native+discovery+geometry))
    (folder/'DwellCache.cs').write_text('using System; using System.Collections.Generic; using PersistentCity.Core;\n'+dwell)
    (folder/'Harness.cs').write_text((Path(__file__).parent/'Harness.cs.txt').read_text())
    (folder/'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="'+escape(str(root/'Persistence/PersistentCity.Persistence.csproj'))+'" /></ItemGroup></Project>')
    subprocess.run(['dotnet','run','--project',str(folder/'Harness.csproj'),'-c','Release'],check=True)
