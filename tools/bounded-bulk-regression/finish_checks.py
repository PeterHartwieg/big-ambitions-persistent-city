"""Complete the required F3 matrix after run-d3a-checks.py, foreground only, no deadlines."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
out = root / 'build/f3-checks'
out.mkdir(parents=True, exist_ok=True)
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false')
# --finish is the existing wrapper's resume point: D3a's complete matrix is already green;
# it still runs the full rival-memory foreground matrix and its own remaining validation.
commands = [[sys.executable, 'tools/food-metadata-regression/validate.py', '--finish']]
commands += [[sys.executable, f'tools/{name}/source_checks.py'] for name in [
    'e3-remaining-feasibility-regression', 'e5a-eligibility-regression', 'food-metadata-api-regression']]
commands += [[sys.executable, '-m', 'unittest', 'discover', '-s', 'tools/e5b-native-rig-proof', '-p', 'test_*.py']]
results = []
for index, command in enumerate(commands):
    path = out / f'{index + 1:02}.log'
    print('Starting ' + ' '.join(command[1:]), flush=True)
    with path.open('w', encoding='utf-8') as stream:
        code = subprocess.run(command, cwd=root, env=env, stdout=stream, stderr=subprocess.STDOUT).returncode
    results.append(dict(command=[Path(command[0]).name, *command[1:]], exit=code,
                        log=path.relative_to(root).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    (out / 'results.json').write_text(json.dumps(results, indent=2) + '\n')
    print(f'Completed {index + 1}/{len(commands)}: exit {code}', flush=True)
    if code:
        print(path.read_text(encoding='utf-8', errors='replace')[-6000:], flush=True)
        raise SystemExit(code)
