"""Sequential, foreground-only D2 offline gate. No execution deadlines or detached workers."""
from pathlib import Path
import datetime
import json
import os
import subprocess
import sys
import time

root = Path(__file__).resolve().parents[1]
output = root / 'build/d2/gates'
output.mkdir(parents=True, exist_ok=True)
dotnet = str(Path(os.environ['LOCALAPPDATA']) / 'Microsoft/dotnet/dotnet.exe')
environment = os.environ.copy()
environment.update(MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0', UseSharedCompilation='false')
wait = ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
        r'C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1', '-Repo', str(root)]
projects = ['core-identity', 'diagnostic-response', 'event-observation', 'food-catalogue',
            'food-checkout', 'food-hours', 'food-household-catalogue', 'food-ownership',
            'food-supply', 'frame-observation', 'gate-access', 'road-ceiling', 'road-handoff',
            'road-invalidation', 'road-speed', 'route-discovery', 'save-contract', 'startup-clock',
            'traffic-closure', 'traffic-queue', 'walking-quote', 'campaign-reader',
            'awareness', 'food-candidate', 'rival-outflow']
steps = [('build', [sys.executable, 'tools/build.py', '--game-managed',
                   r'C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed'])]
steps += [('test', [dotnet, 'test', 'tests/PersistentCity.Tests.csproj']),
          ('list-tests', [dotnet, 'test', 'tests/PersistentCity.Tests.csproj', '--no-build', '--list-tests'])]
for name in projects:
    project = next((root / 'tools' / (name + '-regression')).glob('*.csproj'))
    steps.append((name, [dotnet, 'run', '--project', str(project.relative_to(root)), '--configuration', 'Release']))
for name in ['native-command', 'physical-selection', 'vehicle-ownership', 'optional-stop']:
    steps.append((name, [sys.executable, f'tools/{name}-regression/run.py']))
steps += [('simulate', [dotnet, 'run', '--project', 'tools/simulate/Simulate.csproj', '--', '28', 'build/harness-demo']),
          ('food-routines', [dotnet, 'run', '--project', 'tools/food-routines/FoodRoutines.csproj', '--configuration', 'Release', '--', 'build/harness-food-routines-demo', '957']),
          ('food-demand', [dotnet, 'run', '--project', 'tools/food-demand/FoodDemand.csproj', '--', 'build/harness-food-demand-demo'])]
for case in ['reference', 'consumers5000', 'CF32', 'CF64']:
    steps.append(('capacity-' + case, [dotnet, 'run', '--project', 'tools/save-contract-regression/SaveContractRegression.csproj',
                                    '--configuration', 'Release', '--', '--capacity', case, '1']))
selected = set(sys.argv[1:])
results = []
for name, command in steps:
    if selected and name not in selected:
        continue
    # Python regression harnesses also invoke dotnet run internally.
    if name != 'build':
        if subprocess.run(wait, cwd=root).returncode != 0:
            raise SystemExit('Other-dotnet wait refused; no dependent test/run was started.')
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    tick = time.perf_counter()
    path = output / (name + '.log')
    with path.open('w', encoding='utf-8') as log:
        code = subprocess.run(command, cwd=root, stdout=log, stderr=subprocess.STDOUT, env=environment).returncode
    result = dict(name=name, command=command, started=started, seconds=time.perf_counter()-tick,
                  exit=code, log=str(path.relative_to(root)))
    results.append(result)
    (output / ('results-' + ('selected' if selected else 'all') + '.json')).write_text(json.dumps(results, indent=2), encoding='utf-8')
    print(f'{name}: exit {code}; {result["seconds"]:.2f}s; {result["log"]}', flush=True)
    if code:
        print('\n'.join(path.read_text(encoding='utf-8', errors='replace').splitlines()[-25:]), flush=True)
raise SystemExit(any(r['exit'] for r in results))
