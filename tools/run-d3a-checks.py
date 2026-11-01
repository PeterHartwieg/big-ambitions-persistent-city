"""Required D3a gates, sequential and foreground-only; no execution deadlines."""
from pathlib import Path
import datetime
import json
import os
import subprocess
import sys
import time

root = Path(__file__).resolve().parents[1]
output = root / 'build/d3a/gates'
output.mkdir(parents=True, exist_ok=True)
dotnet = str(Path(os.environ['LOCALAPPDATA']) / 'Microsoft/dotnet/dotnet.exe')
environment = os.environ.copy()
environment.update(MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0', UseSharedCompilation='false')
wait = ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
        r'C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1', '-Repo', str(root)]
steps = [('prepare', [sys.executable, 'tools/e4-menu-regression/prepare.py']),
         ('build', [sys.executable, 'tools/build.py', '--game-managed',
                    r'C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed']),
         ('test', [dotnet, 'test', 'tests/PersistentCity.Tests.csproj']),
         ('list-tests', [dotnet, 'test', 'tests/PersistentCity.Tests.csproj', '--no-build', '--list-tests'])]
for name in ['core-identity', 'diagnostic-response', 'event-observation', 'food-catalogue', 'food-checkout',
             'food-hours', 'food-household-catalogue', 'food-ownership', 'food-supply', 'frame-observation',
             'gate-access', 'road-ceiling', 'road-handoff', 'road-invalidation', 'road-speed', 'route-discovery',
             'save-contract', 'startup-clock', 'traffic-closure', 'traffic-queue', 'walking-quote', 'campaign-reader',
             'donut-feasibility', 'cupcake-feasibility', 'salad-feasibility', 'rival-outflow', 'awareness', 'food-candidate',
             'outflow-line']:
    project = next((root / 'tools' / (name + '-regression')).glob('*.csproj'))
    steps.append((name, [dotnet, 'run', '--project', str(project.relative_to(root)), '--configuration', 'Release']))
for name in ['native-command', 'physical-selection', 'vehicle-ownership', 'optional-stop']:
    steps.append((name, [sys.executable, f'tools/{name}-regression/run.py']))
steps += [('simulate', [dotnet, 'run', '--project', 'tools/simulate/Simulate.csproj', '--', '28', 'build/harness-demo']),
          ('food-routines', [dotnet, 'run', '--project', 'tools/food-routines/FoodRoutines.csproj', '--configuration', 'Release', '--', 'build/harness-food-routines-demo', '957']),
          ('food-demand', [dotnet, 'run', '--project', 'tools/food-demand/FoodDemand.csproj', '--', 'build/harness-food-demand-demo'])]
for name in ['donut', 'salad']:
    steps.append((name + '-source', [sys.executable, f'tools/{name}-feasibility-regression/source_checks.py']))
selected = set(sys.argv[1:])
manifest = output / 'results.json'
results = json.loads(manifest.read_text()) if manifest.exists() else {}
for name, command in steps:
    if selected and name not in selected:
        continue
    if name not in {'prepare', 'build', 'donut-source', 'salad-source'}:
        if subprocess.run(wait, cwd=root).returncode:
            raise SystemExit('Other-dotnet wait refused; no dependent process started.')
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    tick = time.perf_counter()
    path = output / (name + '.log')
    with path.open('w', encoding='utf-8') as log:
        code = subprocess.run(command, cwd=root, env=environment, stdout=log, stderr=subprocess.STDOUT).returncode
    results[name] = dict(command=command, started=started, seconds=time.perf_counter()-tick, exit=code,
                         log=str(path.relative_to(root)))
    manifest.write_text(json.dumps(results, indent=2), encoding='utf-8')
    print(f'{name}: exit {code}; {results[name]["seconds"]:.2f}s; {path.relative_to(root)}', flush=True)
    if code:
        print('\n'.join(path.read_text(encoding='utf-8', errors='replace').splitlines()[-35:]), flush=True)
        raise SystemExit(code)
