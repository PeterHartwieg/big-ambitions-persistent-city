#!/usr/bin/env python3
"""Build the core identity harness against a baseline source tree and the checkout, then require identical
simulation results and print the allocation figures of both."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--baseline', required=True, type=Path, help='a copy of Core/ and Persistence/ from before the change')
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
args.output = args.output.resolve()
args.output.mkdir(parents=True, exist_ok=True)
project = ROOT / 'tools/core-identity-regression/CoreIdentityRegression.csproj'
results = {}
for label, source in (('baseline', args.baseline.resolve()), ('candidate', ROOT)):
    out = args.output / (label + '-harness')
    subprocess.run(['dotnet', 'build', str(project), '--configuration', 'Release', '--output', str(out),
                    '-p:SourceRoot=' + str(source)], check=True, capture_output=True, text=True)
    run = subprocess.run(['dotnet', str(out / 'CoreIdentityRegression.dll')], check=True, capture_output=True, text=True)
    results[label] = json.loads(run.stdout)
    (args.output / (label + '.json')).write_text(run.stdout)

def outcome(result):
    return {k: v for k, v in result.items() if k not in ('bytes', 'simulateMs')}

base, cand = outcome(results['baseline']), outcome(results['candidate'])
for key in sorted(set(base) | set(cand)):
    if base.get(key) != cand.get(key):
        raise SystemExit(f'MISMATCH {key}: baseline {base.get(key)!r} candidate {cand.get(key)!r}')
print(f"Identical: {cand['events']} ActivityObserved events (digest {cand['eventDigest']}), "
      f"{len(cand['saves'])} day-boundary saves, routine/opening/choice/mapping probe digests.")
for label in ('baseline', 'candidate'):
    print(label, json.dumps(results[label]['bytes']), 'simulateMs', results[label]['simulateMs'])
