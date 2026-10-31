#!/usr/bin/env python3
"""Compile source-extracted admission/selection expressions and complete route sampler.
Baseline files freeze the pre-change implementation. No handwritten optimized selector.
"""
from pathlib import Path
from interior_wiring import generate
import os
import shutil
import subprocess
import json
dotnet = shutil.which('dotnet')
if dotnet is None and os.name == 'nt' and os.environ.get('LOCALAPPDATA'):
    local_host = Path(os.environ['LOCALAPPDATA']) / 'Microsoft/dotnet/dotnet.exe'
    if local_host.is_file():
        dotnet = str(local_host)
if dotnet is None:
    raise SystemExit('Cannot find dotnet. Add the .NET SDK to PATH; Windows also checks %LOCALAPPDATA%/Microsoft/dotnet/dotnet.exe.')
here = Path(__file__).resolve().parent
root = here.parent.parent

def between(s, start, end):
    return s[s.index(start):s.index(end, s.index(start))]

def fragments(vehicle, population, journey):
    return {
        'vehicle': between(vehicle, '            var all = Manager.GetVehicleList();', '            LastAdmissionStage = "junction rear clearance";'),
        'ordinary': between(vehicle, '        internal static bool Ordinary(', '        private static uint Hash('),
        'hash': between(vehicle, '        private static uint Hash(', '        public bool TryAcquire('),
        # The selection builds, sorts and walks a reusable candidate list; its struct and comparer
        # are extracted too. The frozen baseline predates them and has no 'candidates' block.
        'selection': between(population, '            // Candidate selection. tools/physical-selection-regression extracts', '                PhysicalRoadSample road = default;'),
        'candidates': between(population, '        /// <summary>One physical selection candidate', '        // Waiting for a route or for an opening is indoors.'),
        'optional_visible': between(population, '        private bool OptionalVisible(', '        private bool TryJoinOptional('),
        'outdoor': between(population, '        private bool Outdoor(', '        /// <summary>\n        /// Which waiting citizens'),
        'sample': between(journey, '        internal static PhysicalJourneySample SampleRoute(', '        private void ReturnPhysicalEvent(')
    }
baseline_path = here / 'baseline.json'
baseline = json.loads(baseline_path.read_text())
current = fragments(*[(root / p).read_text() for p in ['Runtime/PhysicalVehicles.cs', 'Runtime/PhysicalPopulation.cs', 'Core/PhysicalJourneys.cs']])
before_f0 = dict(current)
full_pool_check = '''                // Both pools are full: observations and queue movement above still ran; no
                // candidate can acquire a body until a later update frees a slot.
                if (people >= pedestrianLimit && cars >= vehicleLimit) break;
'''
assert before_f0['selection'].count(full_pool_check) == 1
before_f0['selection'] = before_f0['selection'].replace(full_pool_check, '')
classes = []
for name, parts in [('Before', baseline), ('BeforeF0', before_f0), ('After', current)]:
    classes.append('class ' + name + ' : Fixture {\n' + parts['ordinary'] + parts['hash'] + parts['outdoor'] + parts.get('optional_visible', '') + parts.get('candidates', '') + parts['sample'] + '''
 public bool Slot(CitizenState citizen, out VehicleComponent? chosen) {
 chosen = null;
 if (citizen.Car == null) return false;
''' + parts['vehicle'] + '''
 chosen = slot; return true;
 }
 public List<string> Select() {
 int people = InitialPeople, cars = InitialCars, attempts = 0;
 var result = new List<string>();
''' + parts['selection'] + '''
 result.Add(citizen.Identity.Id);
 if (!Failed.Contains(citizen.Identity.Id)) { if (car) cars++; else people++; }
 }
 return result;
 }
}
''')
program = '''using System;
using System.Linq;
using System.Collections.Generic;
using PersistentCity.Core;
''' + '\n'.join(classes) + (here / 'Harness.cs.txt').read_text()
program = program.replace(' static void Main() {', ' static void Main() { F0Wiring.Tests.Run(); SelectionPerf.Run();')
program += generate(root, between)
program += (here / 'SelectionPerf.cs.txt').read_text()
build = here / 'obj' / 'generated'
build.mkdir(parents=True, exist_ok=True)
(build / 'Program.cs').write_text(program)
# Count actual production duration-property evaluations without changing its arithmetic.
model = (root / 'Core' / 'Model.cs').read_text()
old = 'get { double total = 0; foreach (var leg in Legs) total += leg.Minutes; return total; }'
assert model.count(old) == 1
model = model.replace('public sealed class Route\n    {', 'public sealed class Route\n    {\n        public static int DurationReads;')
model = model.replace(old, old.replace('double total = 0;', 'DurationReads++; double total = 0;'))
(build / 'Model.cs').write_text(model)
import xml.etree.ElementTree as ET
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
properties = ET.SubElement(project, 'PropertyGroup')
for key, value in {'OutputType': 'Exe', 'TargetFramework': 'net10.0', 'Nullable': 'enable',
                   'TreatWarningsAsErrors': 'true', 'EnableDefaultCompileItems': 'false'}.items():
    ET.SubElement(properties, key).text = value
items = ET.SubElement(project, 'ItemGroup')
ET.SubElement(items, 'Compile', Include='Program.cs')
ET.SubElement(items, 'Compile', Include='Model.cs')
ET.SubElement(items, 'Compile', Include=str(root / 'Runtime' / 'InteriorViewState.cs'))
ET.SubElement(items, 'Compile', Include=str(root / 'Runtime' / 'PrototypeSettings.cs'))
ET.SubElement(items, 'PackageReference', Include='Newtonsoft.Json', Version='13.0.2')
ET.SubElement(items, 'Compile', Include=str(root / 'Core' / '*.cs'), Exclude=str(root / 'Core' / 'Model.cs'))
ET.ElementTree(project).write(build / 'Regression.csproj', encoding='unicode')
subprocess.run([dotnet, 'run', '--project', str(build / 'Regression.csproj'), '--configuration', 'Release'], check=True)
