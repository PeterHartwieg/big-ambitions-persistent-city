"""Final unit matrix and cross-process seeded taste proof. Foreground, guarded, no game execution, rig access or deadlines."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
dotnet = str(Path(os.environ['LOCALAPPDATA']) / 'Microsoft/dotnet/dotnet.exe')
guard = ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
         r'C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1', '-Repo', str(root)]
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false')
subprocess.run(guard, cwd=root, env=env, check=True)
subprocess.run([sys.executable, 'tools/build.py', '--game-managed',
                r'C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed'],
               cwd=root, env=env, check=True)
focused_command = [dotnet, 'test', 'tests/PersistentCity.Tests.csproj', '--disable-build-servers']
subprocess.run(guard, cwd=root, env=env, check=True)
focused = subprocess.run(focused_command, cwd=root, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
focused_log = root / 'build/food-metadata/final-tests.log'
focused_log.parent.mkdir(parents=True, exist_ok=True)
focused_log.write_text(focused.stdout)
if focused.returncode:
    print(focused.stdout)
    raise SystemExit(focused.returncode)
summary=next(line for line in focused.stdout.splitlines() if 'Passed!' in line)
print(summary, flush=True)
list_command=[dotnet,'test','tests/PersistentCity.Tests.csproj','--no-build','--list-tests','--disable-build-servers']
subprocess.run(guard,cwd=root,env=env,check=True)
listed=subprocess.run(list_command,cwd=root,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,check=True)
list_log=root / 'build/food-metadata/final-list-tests.log'
list_log.write_text(listed.stdout)
assert 'LargeIntegerTokensCannotBypassEscapedTableBudgetBeforeClamping' in listed.stdout
command = [dotnet, 'run', '--no-build', '--project', str(root / 'tools/food-metadata-regression/FoodMetadataRegression.csproj'),
           '--configuration', 'Release', '--', '--taste-vector']

def reference(consumer, product):
    text = f'pc:product-name-taste:v1|{len(consumer)}:{consumer}|{len(product)}:{product}'
    value = 2166136261 ^ 957
    for unit in text:
        value = ((value ^ ord(unit)) * 16777619) & 0xffffffff
    value ^= value >> 16
    value = (value * 0x7feb352d) & 0xffffffff
    value ^= value >> 15
    percentile = value % 100
    return 0 if percentile < 10 else 1 if percentile < 30 else 2 if percentile < 70 else 3 if percentile < 90 else 4

products = ['foodtrucks:itemname_dumplings', 'foodtrucks:itemname_choppedcheese']
expected = ','.join(str(reference(f'pc:{i}', product)) for i in range(100) for product in products)
outputs = []
for _ in range(2):
    subprocess.run(guard, cwd=root, env=env, check=True)
    result = subprocess.run(command, cwd=root, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
    outputs.append(result.stdout.strip())
assert outputs == [expected, expected], 'Cross-process/reference taste mismatch'
proof = dict(seed=957, products=products, consumers='pc:0 through pc:99', processes=2,
             matchesIndependentReference=True, identicalAcrossProcesses=True,
             nativeTargetBuild=dict(exit=0,dllSHA256=hashlib.sha256((root / 'build/PersistentCity/PersistentCity.dll').read_bytes()).hexdigest()),
             finalUnitSummary=summary,
             finalDiscovery=dict(exit=0,log=list_log.relative_to(root).as_posix(),
                                 sha256=hashlib.sha256(list_log.read_bytes()).hexdigest()),
             focusedTests=dict(command=['dotnet.exe', *focused_command[1:]], exit=0,
                               log=focused_log.relative_to(root).as_posix(),
                               sha256=hashlib.sha256(focused_log.read_bytes()).hexdigest()),
             sha256=hashlib.sha256(expected.encode('utf-8')).hexdigest(), bands=expected)
path = root / 'docs/checks/e5c-name-taste-process-proof.json'
path.write_text(json.dumps(proof, indent=2) + '\n')
print('PASS: two fresh processes match the independent name-taste v1 reference; ' + proof['sha256'])
