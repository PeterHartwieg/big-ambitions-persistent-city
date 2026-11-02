"""Required foreground matrices; no deadlines, game or rig access."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--finish', action='store_true', help='D3a matrix already completed; run remaining required D4/source matrix')
args = parser.parse_args()
commands = [[sys.executable, 'tools/food-metadata-regression/prepare.py']]
if not args.finish:
    commands.append([sys.executable, 'tools/run-d3a-checks.py'])
commands += [[sys.executable, 'tools/rival-memory-regression/validate.py'],
             [sys.executable, 'tools/e3-remaining-feasibility-regression/source_checks.py']]
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false')
for command in commands:
    print(' '.join(command), flush=True)
    subprocess.run(command, cwd=root, env=env, check=True)

results = {}
for name, path in [('d3a', root / 'build/d3a/gates/results.json'), ('d4', root / 'build/d4-validation/results.json')]:
    results[name] = json.loads(path.read_text())
    records = results[name].values() if isinstance(results[name], dict) else results[name]
    for record in records:
        assert record['exit'] == 0, record
        log = root / record['log']
        record['sha256'] = hashlib.sha256(log.read_bytes()).hexdigest()
        record['command'][0] = Path(record['command'][0]).name
        record['command'] = [s.replace(str(root), '<repo>') for s in record['command']]
destination = root / 'docs/checks/e5c-neutral-metadata-results.json'
destination.write_text(json.dumps(results, indent=2) + '\n')
print(f'PASS; command manifest and log hashes: {destination.relative_to(root)}', flush=True)
