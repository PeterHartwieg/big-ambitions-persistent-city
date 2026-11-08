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
# F2f baseline: the merged F5f selection over every indexed body candidate, reconstructed from the
# current text by restoring the F5f roster line, so every other line stays production code.
before_f2f = dict(current)
f2f_loop = '''            // F2f: the active-journey index members filed in the grid cells within the radius (visitors withheld
            // until F5i), or every citizen after a shadow mismatch. Filters and order below are the same for both sources.
            var roster = journeyIndexFallback ? simulation.State.Citizens : simulation.ActiveJourneys.Grid.Query(focus, radius, nearby);
'''
f5f_roster = '''            // F5f: iterate the maintained active-journey index (visitors withheld until F5i), or every
            // citizen after a shadow mismatch. Filters and order below are the same for both sources.
            var roster = journeyIndexFallback ? simulation.State.Citizens : simulation.ActiveJourneys.BodyCandidates;
'''
assert before_f2f['selection'].count(f2f_loop) == 1
before_f2f['selection'] = before_f2f['selection'].replace(f2f_loop, f5f_roster)
assert 'Grid.Query' not in before_f2f['selection']
# F5f perf baseline: the unchanged pre-F5f loop over every citizen, with no index and no ID
# classification, reconstructed from the F5f text so every other line stays production code.
before_f5f = dict(before_f2f)
f5f_loop = '''            // F5f: iterate the maintained active-journey index (visitors withheld until F5i), or every
            // citizen after a shadow mismatch. Filters and order below are the same for both sources.
            var roster = journeyIndexFallback ? simulation.State.Citizens : simulation.ActiveJourneys.BodyCandidates;
            for (int index = 0; index < roster.Count; index++)
            {
                var c = roster[index];
'''
f5f_filter = '''                if (journeyIndexFallback && !ActiveJourneyIndex.ReceivesBody(c)) continue;
'''
for text, base in [(f5f_loop, '''            foreach (var c in simulation.State.Citizens)
            {
'''), (f5f_filter, '')]:
    assert before_f5f['selection'].count(text) == 1, text
    before_f5f['selection'] = before_f5f['selection'].replace(text, base)
# The vendor-stay block after the loop (E6e) reads ActiveJourneys.Contains; only the roster loop is reconstructed.
assert 'ActiveJourneys.BodyCandidates' not in before_f5f['selection'] and 'ReceivesBody' not in before_f5f['selection']
classes = []
for name, parts in [('Before', baseline), ('BeforeF0', before_f0), ('BeforeF5f', before_f5f), ('BeforeF2f', before_f2f), ('After', current)]:
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
# F7c: a committed route caches its total; an uncommitted one sums its legs. Count every read of either.
old = 'public double Minutes => legs.IsReadOnly ? committedMinutes : Sum();'
assert model.count(old) == 1
model = model.replace('public sealed class Route\n    {', 'public sealed class Route\n    {\n        public static int DurationReads;')
model = model.replace(old, 'public double Minutes { get { DurationReads++; return legs.IsReadOnly ? committedMinutes : Sum(); } }')
(build / 'Model.cs').write_text(model)
import xml.etree.ElementTree as ET
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
properties = ET.SubElement(project, 'PropertyGroup')
for key, value in {'OutputType': 'Exe', 'TargetFramework': 'net10.0', 'Nullable': 'enable',
                   'TreatWarningsAsErrors': 'true', 'EnableDefaultCompileItems': 'false',
                   # Short timing loops otherwise measure unoptimized tier-0 code for whichever arm runs first.
                   'TieredCompilation': 'false'}.items():
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
