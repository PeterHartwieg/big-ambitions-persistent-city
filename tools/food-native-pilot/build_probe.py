#!/usr/bin/env python3
"""Build only the isolated Windows native-sales diagnostic helper. Does not install or launch."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

source = Path(__file__).resolve().parent
module = source
repo = source.parents[1]
root = repo / 'build/food-native-pilot-20261022/Probe'
root.mkdir(parents=True, exist_ok=True)
default_managed = repo / 'build/food-native-pilot-20261022/refs'
parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--game-managed', type=Path, default=default_managed)
args = parser.parse_args()
managed = args.game_managed.resolve()
names = ['mscorlib', 'System', 'System.Core', 'netstandard', 'BigAmbitions',
         'BigAmbitions.ModAPI', 'BigAmbitions.AI', 'BigAmbitions.Items', 'BigAmbitions.Characters', 'BigAmbitions.Neighborhoods', 'HGExtensions', 'HGPlugins', 'DayNightCycle', 'ExternalPlugins', 'UnityEngine.CoreModule', 'UnityEngine.AnimationModule', 'UnityEngine.PhysicsModule', 'UnityEngine.AIModule', 'Newtonsoft.Json']
references = [managed / (name + '.dll') for name in names] + [repo / 'build/food-native-pilot-20261022/source/build/PersistentCity/PersistentCity.dll']
references.append(repo / 'build/food-native-pilot-20261022/source/build/PersistentCity/Dependencies/0Harmony.dll')
for path in references:
    if not path.is_file():
        raise SystemExit('Missing required local assembly: ' + str(path))
sdks = subprocess.check_output(['dotnet', '--list-sdks'], text=True).splitlines()
match = re.fullmatch(r'(\S+) \[(.*)\]', sdks[-1])
if not match:
    raise SystemExit('Cannot locate SDK compiler')
csc = Path(match[2]) / match[1] / 'Roslyn/bincore/csc.dll'
output = root / 'FoodNativePilotProbe.dll'
command = ['dotnet', str(csc), '-nologo', '-noconfig', '-nostdlib+', '-target:library',
           '-langversion:9', '-nullable:enable', '-optimize+', '-warn:4', '-warnaserror+',
           '-deterministic+', '-pathmap:' + str(root) + '=/Probe', '-out:' + str(output)]
command += ['-reference:' + str(path) for path in references]
sources = [module / 'Probe.cs', module / 'Pilot.cs']
subprocess.run(command + [str(p) for p in sources], check=True)
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
manifest = {'compilerSdk': match[1], 'output': {'file': output.name, 'sha256': sha(output)},
            'sources': [{'file': source.name, 'sha256': sha(source)} for source in sources],
            'references': [{'path': str(path), 'sha256': sha(path)} for path in references]}
(root / 'build-manifest.json').write_text(json.dumps(manifest, indent=2, sort_keys=True) + '\n')
print(output)
print('SHA256:', sha(output))
print('Build only; no game launch, installation, dependency copying or rig changes.')
