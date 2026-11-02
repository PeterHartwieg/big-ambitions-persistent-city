"""Collect completed foreground matrices; never launches tests, the game or a rig."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

root = Path(__file__).resolve().parents[2]
result = {'native_dynamic_integration': 'incomplete; conditional/offline evidence only',
          'base': '4ee0c8d', 'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
          'matrices': {}, 'offline_measurement_flags': []}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def objects(source):
    cursor = 0
    decoder = json.JSONDecoder()
    while (start := source.find('{', cursor)) >= 0:
        try:
            value, length = decoder.raw_decode(source[start:])
            yield value
            cursor = start + length
        except json.JSONDecodeError:
            cursor = start + 1

def flags(value, path, log):
    if isinstance(value, list):
        for i, child in enumerate(value):
            flags(child, path + [i], log)
    elif isinstance(value, dict):
        for key, child in value.items():
            normalized = key.lower().replace('_', '')
            if isinstance(child, (int, float)) and not isinstance(child, bool):
                spike = 'spike' in normalized and child > 0
                duration = (normalized in {'max', 'p95'} or normalized.endswith(('ms', 'milliseconds'))) and child >= 30
                if spike or duration:
                    result['offline_measurement_flags'].append({'log': log, 'path': path + [key], 'value': child,
                        'kind': 'reported spike count' if spike else 'offline duration/distribution field; intervals and aggregates are not frame spikes'})
            flags(child, path + [key], log)

for name, manifest in [('d3a', 'build/d3a/gates/results.json'), ('d4', 'build/d4-validation/results.json')]:
    records = json.loads((root / manifest).read_text())
    records = list(records.values()) if isinstance(records, dict) else records
    for record in records:
        assert record['exit'] == 0, record
        log = root / record['log']
        record['sha256'] = sha(log)
        record['command'] = [part.replace(str(root), '<repo>') for part in record['command']]
        record['command'][0] = Path(record['command'][0]).name
        for i, value in enumerate(objects(log.read_text(encoding='utf-8-sig', errors='replace'))):
            flags(value, [i], record['log'])
    result['matrices'][name] = records

result['source_sha256'] = {str(p.relative_to(root)).replace('\\', '/'): sha(p)
    for directory in ('Core', 'Runtime', 'Persistence') for p in sorted((root / directory).glob('*.cs'))}
for directory in ('tools/e5b-complete-menu-regression', 'tools/food-checkout-regression'):
    for p in sorted((root / directory).glob('*')):
        if p.is_file():
            result['source_sha256'][p.relative_to(root).as_posix()] = sha(p)
result['source_sha256']['tests/CompleteFoodMenuTests.cs'] = sha(root / 'tests/CompleteFoodMenuTests.cs')
result['e5b_measurements'] = list(objects((root / 'build/d3a/gates/e5b-complete-menu.log').read_text()))
d4_menu = next(record for record in result['matrices']['d4']
              if any('E5bCompleteMenuRegression.csproj' in argument for argument in record['command']))
result['e5b_measurements_final'] = list(objects((root / d4_menu['log']).read_text()))
checkout = (root / 'build/d3a/gates/food-checkout.log').read_text()
result['checkout_d3a'] = checkout.splitlines()[-3:]
d4_checkout = next(record for record in result['matrices']['d4']
                   if any('FoodCheckoutRegression.csproj' in argument for argument in record['command']))
result['checkout_final'] = (root / d4_checkout['log']).read_text().splitlines()[-3:]
result['unit_summary'] = [line for line in (root / 'build/d3a/gates/test.log').read_text().splitlines() if 'Passed!' in line]

checks = (root / 'docs/checks/e5b-complete-menu.md').read_text(encoding='utf-8-sig')
pr = (root / 'docs/reviews/2026-11-02-e5b-complete-menu-pr-description.md').read_text(encoding='utf-8-sig')
header = '## In-game checks for the next batched rig'
assert checks[checks.index(header):] == pr[pr.index(header):], 'Rig lists differ'
assert len(re.findall(r'^\d+\. ', checks[checks.index(header):], re.M)) == 6
assert checks.count('rig-automated (') == 6
destination = root / 'docs/checks/e5b-complete-menu-results.json'
destination.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('PASS foreground matrix receipts, log hashes, source hashes and identical six-check rig handoff: ' + str(destination.relative_to(root)))
