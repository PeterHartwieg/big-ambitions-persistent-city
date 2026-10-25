#!/usr/bin/env python3
"""Compile a frozen food probe plus read-only continuity module; never install or launch."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--subway-evidence', type=Path, help='Captured continuity analysis for guarded optional pickup fixture')
parser.add_argument('--campaign', type=Path, required=True)
parser.add_argument('--audit-source', type=Path, required=True)
parser.add_argument('--persistent-city', type=Path, required=True)
parser.add_argument('--game-managed', type=Path, default=Path.home() / 'Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed')
args = parser.parse_args()
root = args.campaign.resolve() / 'Probe'
root.mkdir(parents=True, exist_ok=True)
frozen = root / 'sources'
frozen.mkdir(exist_ok=True)
names = ['Probe.cs', 'Pilot.cs', 'ClockAudit.cs', 'Neighborhood.cs', 'Frames.cs', 'Menus.cs']
for name in names:
    shutil.copy2(args.audit_source / name, frozen / name)
main = frozen / 'Probe.cs'
text = main.read_text()
# Permit a previously frozen continuity source set without adding duplicate dispatch/ticks.
text = text.replace('                else if (op == "continuity") Respond(Continuity(request));\n', '')
text = text.replace('                else if (op.StartsWith("pickup-",StringComparison.Ordinal)) Respond(PickupCommand(request));\n', '')
text = text.replace('            TickPickup();\n', '')
needle = 'if (op == "status") Respond(Status());'
if text.count(needle) != 1:
    raise SystemExit('Expected one frozen food-probe status dispatch')
text = text.replace(needle, needle + '\n                else if (op == "continuity") Respond(Continuity(request));')
if text.count('SampleFrame();') != 1:
    raise SystemExit('Expected one frozen food-probe frame sampling call')
text = text.replace('SampleFrame();', 'SampleFrame();\n            TickPickup();')
text = text.replace('else if (op == "continuity") Respond(Continuity(request));', 'else if (op == "continuity") Respond(Continuity(request));\n                else if (op.StartsWith("pickup-",StringComparison.Ordinal)) Respond(PickupCommand(request));')
main.write_text(text)
shutil.copy2(Path(__file__).with_name('Continuity.cs'), frozen / 'Continuity.cs')
shutil.copy2(Path(__file__).with_name('Pickup.cs'), frozen / 'Pickup.cs')
if args.subway_evidence:
    data = json.loads(args.subway_evidence.read_text())
    proof = {'source': str(args.subway_evidence.resolve()), 'sha256': hashlib.sha256(args.subway_evidence.read_bytes()).hexdigest(), 'subwayFirstSamples': data['subwayFirstSamples']}
    (root / 'pickup-subway-evidence.json').write_text(json.dumps(proof, indent=2) + '\n')
managed = args.game_managed.resolve()
refs = ['mscorlib', 'System', 'System.Core', 'netstandard', 'BigAmbitions', 'BigAmbitions.ModAPI', 'BigAmbitions.AI', 'BigAmbitions.Items', 'BigAmbitions.Characters', 'BigAmbitions.Neighborhoods', 'HGExtensions', 'HGPlugins', 'DayNightCycle', 'ExternalPlugins', 'UnityEngine.CoreModule', 'UnityEngine.ScreenCaptureModule', 'UnityEngine.AnimationModule', 'UnityEngine.PhysicsModule', 'UnityEngine.AIModule', 'Newtonsoft.Json']
candidate = args.persistent_city.resolve()
references = [managed / (name + '.dll') for name in refs] + [candidate, candidate.parent / 'Dependencies/0Harmony.dll']
for path in references:
    if not path.is_file():
        raise SystemExit('Missing local assembly: ' + str(path))
sdk = subprocess.check_output(['dotnet', '--list-sdks'], text=True).splitlines()[-1]
match = re.fullmatch(r'(\S+) \[(.*)\]', sdk)
if not match:
    raise SystemExit('Cannot locate .NET compiler')
csc = Path(match[2]) / match[1] / 'Roslyn/bincore/csc.dll'
output = root / 'FoodDemandAuditProbe.dll'
sources = sorted(frozen.glob('*.cs'))
command = ['dotnet', str(csc), '-nologo', '-noconfig', '-nostdlib+', '-target:library', '-langversion:9', '-nullable:enable', '-optimize+', '-warn:4', '-warnaserror+', '-deterministic+', '-out:' + str(output)]
if args.subway_evidence:
    command.append('-resource:' + str(root / 'pickup-subway-evidence.json') + ',PersistentCity.PickupSubwayEvidence.json')
subprocess.run(command + ['-reference:' + str(p) for p in references] + [str(p) for p in sources], check=True)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
(root / 'build-manifest.json').write_text(json.dumps({'compilerSdk': match[1], 'output': {'file': output.name, 'sha256': sha(output)}, 'embeddedSubwayEvidence': sha(root / 'pickup-subway-evidence.json') if args.subway_evidence else None, 'sources': [{'file': p.name, 'sha256': sha(p)} for p in sources], 'references': [{'path': str(p), 'sha256': sha(p)} for p in references]}, indent=2) + '\n')
print(output)
print('Build only; no rig changes.')
