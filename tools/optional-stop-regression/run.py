#!/usr/bin/env python3
"""Exercise the actual optional-stop physical adapter methods with movement doubles."""
from pathlib import Path
import subprocess, tempfile, sys
from xml.sax.saxutils import escape
import os, shutil
# Prefer dotnet on PATH; otherwise use the per-user SDK install the offline gate specifies.
dotnet = shutil.which('dotnet') or str(Path(os.environ.get('LOCALAPPDATA', '')) / 'Microsoft/dotnet/dotnet.exe')
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
    (folder/'StationaryCrowds.cs').write_text((root/'Runtime/StationaryCrowds.cs').read_text())
    (folder/'StationaryScope.cs').write_text((Path(__file__).parent/'StationaryScope.cs.txt').read_text())
    (folder/'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="'+escape(str(root/'Persistence/PersistentCity.Persistence.csproj'))+'" /></ItemGroup></Project>')
    subprocess.run([dotnet,'run','--project',str(folder/'Harness.csproj'),'-c','Release','--']+sys.argv[1:],check=True)
    if '--performance' in sys.argv:
        perf=folder/'perf'
        perf.mkdir()
        # Instrument the existing occupancy loop in a disposable Core copy; never edit
        # production or infer scan counts from a second implementation of admission.
        scheduling=(root/'Core/OptionalStopScheduling.cs').read_text()
        start=scheduling.index('        private bool Occupied(')
        end=scheduling.index('        private bool TryConversation(',start)
        occupied=scheduling[start:end]
        assert occupied.count('foreach (var c in withStops)')==1
        occupied=occupied.replace('            foreach (var c in withStops)\n',
            '            OccupancyCalls++;\n            foreach (var c in withStops)\n            { OccupancyVisits++;\n')
        occupied=occupied.replace('            return false;', '            }\n            return false;')
        (perf/'OptionalStopScheduling.cs').write_text(scheduling[:start]+occupied+scheduling[end:])
        population=source
        def between(begin,end):
            return population[population.index(begin):population.index(end,population.index(begin))]
        selector=between('            // Candidate selection. tools/physical-selection-regression extracts','                PhysicalRoadSample road = default;')
        candidates=between('        /// <summary>One physical selection candidate','        // Waiting for a route or for an opening is indoors.')
        outdoor=between('        private bool Outdoor(','        /// <summary>\n        /// Which waiting citizens')
        visible=between('        private bool OptionalVisible(','        private bool TryJoinOptional(')
        fixture=(root/'tools/physical-selection-regression/Harness.cs.txt').read_text().split('static class Program {')[0]
        capped='using System; using System.Collections.Generic; using System.Linq; using PersistentCity.Core;\n'+fixture+'\nclass CapSelection : Fixture {\n'+candidates+outdoor+visible+'''
        public int Select() {
            int people=InitialPeople,cars=InitialCars,attempts=0,admitted=0;
        '''+selector+'''
                bindings[citizen.Identity.Id]=new object();
                if(car) cars++; else people++;
                admitted++;
            }
            InitialPeople=people; InitialCars=cars; return admitted;
        }
        }
        '''
        (perf/'Caps.cs').write_text(capped)
        (perf/'Performance.cs').write_text((root/'tools/optional-stop-regression/Performance.cs.txt').read_text())
        # Freeze the current default and accepted maximum as the two selector stress arms.
        assert 'MaximumPedestrians { get; set; } = 48;' in (root/'Runtime/PrototypeSettings.cs').read_text()
        assert 'settings.MaximumPedestrians > 256' in (root/'Runtime/PersistentCityMod.cs').read_text()
        (perf/'Perf.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Compile Include="'+escape(str(root/'Core/*.cs'))+'" Exclude="'+escape(str(root/'Core/OptionalStopScheduling.cs'))+'" /></ItemGroup></Project>')
        subprocess.run([dotnet,'run','--project',str(perf/'Perf.csproj'),'-c','Release'],check=True)
