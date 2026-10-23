#!/usr/bin/env python3
"""Compile the same fixtures against two real source trees and compare every route before timing."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--baseline', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
parser.add_argument('--benchmark', action='store_true')
args = parser.parse_args()
args.output = args.output.resolve()
args.output.mkdir(parents=True, exist_ok=True)
env = dict(os.environ, DOTNET_TieredCompilation='0')

def execute(command, log):
    result = subprocess.run(command, text=True, capture_output=True, env=env)
    (args.output / log).write_text(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError(f'{log}: {result.stdout[-1500:]}\n{result.stderr[-1500:]}')
    return result.stdout

sources = {'baseline': args.baseline.resolve(), 'candidate': ROOT}
outputs = {}
source_hashes = {}
for label, source in sources.items():
    outputs[label] = args.output / (label + '-harness')
    source_hashes[label] = {str(p.relative_to(source)): hashlib.sha256(p.read_bytes()).hexdigest()
                            for folder in ('Core', 'Compatibility', 'Runtime')
                            for p in sorted((source / folder).glob('*.cs'))}
    execute(['dotnet', 'build', str(ROOT / 'tools/road-ceiling-regression/RoadCeilingRegression.csproj'),
             '--configuration', 'Release', '--output', str(outputs[label]), '-p:SourceRoot=' + str(source)],
            label + '-build.log')

def run(label, suffix, mode=None):
    command = ['dotnet', str(outputs[label] / 'RoadCeilingRegression.dll')]
    if mode:
        command.append(mode)
    return [json.loads(line) for line in execute(command, label + '-' + suffix + '.log').splitlines()]

def assert_parity(left, right):
    assert left['checks'] == right['checks']
    assert len(left['results']) == len(right['results'])
    for a, b in zip(left['results'], right['results']):
        assert {k: v for k, v in a.items() if k != 'nativeReads'} == {
            k: v for k, v in b.items() if k != 'nativeReads'}, (a, b)

parity = {label: run(label, 'regression')[0] for label in sources}
assert_parity(parity['baseline'], parity['candidate'])
results = {'exactRouteMatches': len(parity['candidate']['results']),
           'checksPerRun': parity['candidate']['checks'], 'sourceSha256': source_hashes, 'parity': parity}
print(f"Exact route parity: {results['exactRouteMatches']} cases; {results['checksPerRun']} assertions per run.", flush=True)
if args.benchmark:
    # The timed fixtures differ from some regression cases; prove their exact complete
    # results match across source versions before either version enters a timed loop.
    fixtures = {label: run(label, 'benchmark-fixtures', '--benchmark-fixtures') for label in sources}
    for label in sources:
        assert_parity(parity[label], fixtures[label][0])
    assert fixtures['baseline'][1:] == fixtures['candidate'][1:]
    expected_fingerprints = {row['benchmark']: row['fingerprint'] for row in fixtures['baseline'][1:]}
    results['benchmarkFixtureParity'] = fixtures['baseline'][1:]
    runs = []
    for pair in range(3):
        for label in (('baseline', 'candidate') if pair % 2 == 0 else ('candidate', 'baseline')):
            rows = run(label, 'benchmark-' + str(pair), '--benchmark')
            assert_parity(parity[label], rows[0])
            assert len(rows[1:]) == len(expected_fingerprints)
            for row in rows[1:]:
                assert row['fingerprint'] == expected_fingerprints[row['benchmark']]
            runs.append({'pair': pair, 'variant': label, 'rows': rows[1:]})
    results['benchmarkRuns'] = runs
    summary = []
    for name in ('unreachable-walk', 'congested-walk', 'winning-car'):
        row = {'scenario': name}
        for label in sources:
            samples = [r for run_result in runs if run_result['variant'] == label
                       for r in run_result['rows'] if r['benchmark'] == name]
            row[label] = {'milliseconds': statistics.median(r['medianMilliseconds'] for r in samples),
                          'nativeReads': samples[0]['nativeReads'], 'allocatedBytes': samples[0]['allocatedBytes'],
                          'iterations': samples[0]['iterations']}
        row['speedup'] = row['baseline']['milliseconds'] / row['candidate']['milliseconds']
        summary.append(row)
    results['benchmarkSummary'] = summary
    print(json.dumps(summary, indent=2))
(args.output / 'results.json').write_text(json.dumps(results, indent=2) + '\n')
