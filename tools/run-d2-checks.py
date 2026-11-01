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
steps = [('e4-prepare', [sys.executable, 'tools/e4-menu-regression/prepare.py']),
         ('build', [sys.executable, 'tools/build.py', '--game-managed',
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
known = {name for name, _ in steps}
for directory in sorted((root / 'tools').glob('*-regression')):
    name = directory.name.removesuffix('-regression')
    projects_here = list(directory.glob('*.csproj'))
    if name not in known and len(projects_here) == 1:
        steps.append((name, [dotnet, 'run', '--project', str(projects_here[0].relative_to(root)), '--configuration', 'Release']))
steps.append(('pickup-continuity', [dotnet, 'run', '--project', 'tools/persistent-continuity/regression/PickupRegression.csproj', '--configuration', 'Release']))
for name, project, arguments in [
    ('choice-rush', 'food-choice-regression/FoodChoiceRegression', ['--rush']),
    ('choice-integrated', 'food-choice-regression/FoodChoiceRegression', ['--integrated']),
    ('choice-rush-integrated', 'food-choice-regression/FoodChoiceRegression', ['--rush', '--integrated']),
    ('choice-rush-integrated-5000', 'food-choice-regression/FoodChoiceRegression', ['--rush-5000', '--integrated']),
    ('save-bench', 'save-contract-regression/SaveContractRegression', ['--bench', '--decisions']),
]:
    steps.append((name, [dotnet, 'run', '--project', 'tools/' + project + '.csproj', '--configuration', 'Release', '--'] + arguments))
steps.append(('c4-save-comparison', [sys.executable, 'tools/market-research-regression/compare_save_baseline.py', '--baseline', 'HEAD']))
steps.append(('e4-default-comparison', [sys.executable, 'tools/e4-menu-regression/compare-default.py']))
for name in ['donut', 'salad', 'icecream', 'pizza', 'frenchfries']:
    steps.append((name + '-isolation', [sys.executable, 'tools/' + name + '-feasibility-regression/source_checks.py']))
steps += [('salad-differential', [sys.executable, 'tools/salad-feasibility-regression/compare_baseline.py', 'differential']),
          ('frenchfries-baseline', [sys.executable, 'tools/frenchfries-feasibility-regression/measure_before.py'])]
steps.append(('icecream-baseline', [sys.executable, 'tools/icecream-feasibility-regression/measure_baseline.py']))
selected = set(sys.argv[1:])
results = []
for name, command in steps:
    if selected and name not in selected:
        continue
    # Python regression harnesses also invoke dotnet run internally.
    if name not in {'build', 'e4-prepare', 'donut-isolation', 'salad-isolation', 'icecream-isolation', 'pizza-isolation'}:
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
