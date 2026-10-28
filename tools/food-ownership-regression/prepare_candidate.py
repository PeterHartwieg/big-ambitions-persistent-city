#!/usr/bin/env python3
"""Freeze/build a K=1 or K=2 native candidate. Never edit the working source, deploy, or touch saves."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--factor', type=int, choices=(1, 2), required=True)
parser.add_argument('--game-managed', type=Path, required=True)
args = parser.parse_args()
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=False)
for folder in ('Core', 'Persistence', 'Runtime', 'Compatibility', 'config'):
    shutil.copytree(repo / folder, output / folder, ignore=shutil.ignore_patterns('bin', 'obj'))
(output / 'tools').mkdir()
shutil.copy2(repo / 'tools/build.py', output / 'tools/build.py')
shutil.copy2(repo / 'README.md', output / 'README.md')
balance = output / 'Core/CityBalance.cs'
text = balance.read_text()
needle = 'public const int ConsumerRepresentationFactor = 2;'
if text.count(needle) != 1:
    raise SystemExit('Expected the reviewed K=2 source profile; refusal leaves a reviewable snapshot.')
balance.write_text(text.replace(needle, f'public const int ConsumerRepresentationFactor = {args.factor};'))
dependencies = repo / 'build/PersistentCity/Dependencies'
if dependencies.is_dir():
    shutil.copytree(dependencies, output / 'build/PersistentCity/Dependencies')
with (output / 'native-build.log').open('w') as log:
    subprocess.run(['python3', str(output / 'tools/build.py'), '--game-managed', str(args.game_managed.resolve())],
                   check=True, stdout=log, stderr=subprocess.STDOUT)
sources = sorted(p for folder in ('Core', 'Persistence', 'Runtime', 'Compatibility') for p in (output / folder).glob('*.cs'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
candidate = output / 'build/PersistentCity/PersistentCity.dll'
(output / 'candidate-manifest.json').write_text(json.dumps({'representationFactor': args.factor,
    'scope': 'Compile-time fresh-city comparison; no existing wallets or identities were changed.',
    'candidate': {'path': str(candidate), 'sha256': sha(candidate)},
    'sources': [{'file': str(p.relative_to(output)), 'sha256': sha(p)} for p in sources]}, indent=2) + '\n')
print(candidate)
print('Candidate config remains disabled. Build only; no rig access or deployment.')
