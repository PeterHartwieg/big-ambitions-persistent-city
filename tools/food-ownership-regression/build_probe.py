#!/usr/bin/env python3
"""Build the guarded ownership helper from frozen diagnostic sources. No rig access or deployment."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--campaign', type=Path, required=True)
parser.add_argument('--persistent-city', type=Path, required=True)
parser.add_argument('--game-managed', type=Path, required=True)
parser.add_argument('--audit-source', type=Path, default=repo / 'tools/food-demand-audit')
args = parser.parse_args()
root = args.campaign.resolve() / 'OwnershipProbe'
root.mkdir(parents=True, exist_ok=False)
frozen = root / 'sources'
frozen.mkdir()
for name in ['Probe.cs', 'Pilot.cs', 'ClockAudit.cs', 'Neighborhood.cs', 'Frames.cs', 'Menus.cs', 'Operations.cs', 'ReturnQuotes.cs']:
    shutil.copy2(args.audit_source / name, frozen / name)
main = frozen / 'Probe.cs'
text = main.read_text()
needle = 'if (op == "status") Respond(Status());'
if text.count(needle) != 1:
    raise SystemExit('Expected one diagnostic dispatch point')
text = text.replace(needle, 'if (op.StartsWith("ownership-", StringComparison.Ordinal)) Respond(OwnershipOperation(op, request));\n                else ' + needle)
text = text.replace('if(state!=null && pilotShop==null)', 'if(state!=null && state.Shops.Count>0 && pilotShop==null)')
main.write_text(text)
# Adapt only frozen diagnostic fixtures to the new automatic owner. Never weaken the
# production candidate or modify the preserved original helper used by old campaigns.
neighborhood = frozen / 'Neighborhood.cs'
text = neighborhood.read_text().replace('|| Simulation.State.Food != null)', '|| Simulation.State.Food?.Shops.Count != 0)')
text = text.replace('            Runtime.StartFoodRoutines(ids);\n            Runtime.EnableHouseholdFinances();',
                    '            // Automatic ownership acquires at a later drained native hour; wallets already initialized.')
neighborhood.write_text(text)
menus = frozen / 'Menus.cs'
menus.write_text(menus.read_text().replace('if (Simulation.State.Food != null ||', 'if (Simulation.State.Food?.Shops.Count != 0 ||'))
shutil.copy2(Path(__file__).with_name('Ownership.cs'), frozen / 'Ownership.cs')
managed = args.game_managed.resolve()
names = ['mscorlib', 'System', 'System.Core', 'netstandard', 'BigAmbitions', 'BigAmbitions.ModAPI', 'BigAmbitions.AI',
         'BigAmbitions.Items', 'BigAmbitions.Characters', 'BigAmbitions.Neighborhoods', 'HGExtensions', 'HGPlugins',
         'DayNightCycle', 'ExternalPlugins', 'UnityEngine.CoreModule', 'UnityEngine.ScreenCaptureModule',
         'UnityEngine.AnimationModule', 'UnityEngine.PhysicsModule', 'UnityEngine.AIModule', 'Newtonsoft.Json']
candidate = args.persistent_city.resolve()
references = [managed / (n + '.dll') for n in names] + [candidate, candidate.parent / 'Dependencies/0Harmony.dll']
for path in references:
    if not path.is_file():
        raise SystemExit('Missing local reference: ' + str(path))
sdk = subprocess.check_output(['dotnet', '--list-sdks'], text=True).splitlines()[-1]
match = re.fullmatch(r'(\S+) \[(.*)\]', sdk)
if not match:
    raise SystemExit('Cannot locate .NET compiler')
csc = Path(match[2]) / match[1] / 'Roslyn/bincore/csc.dll'
output = root / 'FoodDemandAuditProbe.dll'
sources = sorted(frozen.glob('*.cs'))
command = ['dotnet', str(csc), '-nologo', '-noconfig', '-nostdlib+', '-target:library', '-langversion:9',
           '-nullable:enable', '-optimize+', '-warn:4', '-warnaserror+', '-deterministic+', '-out:' + str(output)]
subprocess.run(command + ['-reference:' + str(p) for p in references] + [str(p) for p in sources], check=True)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
(root / 'build-manifest.json').write_text(json.dumps({'compilerSdk': match[1],
    'output': {'file': output.name, 'sha256': sha(output)},
    'sources': [{'file': p.name, 'sha256': sha(p)} for p in sources],
    'references': [{'path': str(p), 'sha256': sha(p)} for p in references]}, indent=2) + '\n')
print(output)
print('Build only; no game launch, save changes, installation or rig access.')
