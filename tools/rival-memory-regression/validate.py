"""Foreground offline acceptance; serialize all dotnet work with the shared host guard."""
import hashlib
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
out = root / "build/d4-validation"
out.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser()
parser.add_argument("--start-at", type=int, default=1, help="Resume a stopped foreground batch at this command number")
args = parser.parse_args()
# E4 deliberately generates its frozen comparison inputs into ignored build storage.
preparation = subprocess.run([sys.executable, "tools/e4-menu-regression/prepare.py"], cwd=root,
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
(out / "preparation.txt").write_bytes(preparation.stdout)
if preparation.returncode:
    print(preparation.stdout.decode("utf-8", errors="replace"))
    sys.exit(preparation.returncode)
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
guard = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
commands = [[sys.executable, "tools/build.py", "--game-managed",
             r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"],
            [dotnet, "test", "tests/PersistentCity.Tests.csproj", "--disable-build-servers"],
            [dotnet, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests", "--disable-build-servers"]]
commands += [[dotnet, "run", "--project", p.relative_to(root).as_posix(), "--configuration", "Release", "--disable-build-servers"]
             for p in sorted((root / "tools").glob("*-regression/*.csproj"))]
commands += [[sys.executable, "tools/" + p + "/run.py"] for p in
             ("native-command-regression", "physical-selection-regression", "vehicle-ownership-regression", "optional-stop-regression")]
commands += [[dotnet, "run", "--project", "tools/simulate/Simulate.csproj", "--disable-build-servers", "--", "28", "build/harness-demo"],
             [dotnet, "run", "--project", "tools/food-routines/FoodRoutines.csproj", "--configuration", "Release", "--disable-build-servers", "--", "build/harness-food-routines-demo", "957"],
             [dotnet, "run", "--project", "tools/food-demand/FoodDemand.csproj", "--disable-build-servers", "--", "build/harness-food-demand-demo"]]
commands += [[sys.executable, "tools/" + p + "/source_checks.py"] for p in ("donut-feasibility-regression", "salad-feasibility-regression")]
results = json.loads((out / "results.json").read_text())[:args.start_at - 1] if args.start_at > 1 else []
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
for i, command in enumerate(commands):
    if i + 1 < args.start_at:
        continue
    label = " ".join(command[1:])
    print(f"[{i+1}/{len(commands)}] {label}", flush=True)
    # Python adapters/source guards can invoke dotnet internally, too.
    subprocess.run(guard, cwd=root, env=env, check=True)
    run = subprocess.run(command, cwd=root, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    log = out / f"{i+1:02}.txt"
    log.write_bytes(run.stdout)
    result = dict(command=command, exit=run.returncode, log=log.relative_to(root).as_posix(),
                  sha256=hashlib.sha256(run.stdout).hexdigest())
    results.append(result)
    (out / "results.json").write_text(json.dumps(results, indent=2))
    print(f"  exit {run.returncode}; {len(run.stdout)} output bytes", flush=True)
    if run.returncode:
        print(run.stdout.decode("utf-8", errors="replace"), flush=True)
        sys.exit(run.returncode)
print(f"PASS {len(results)}/{len(commands)} foreground commands", flush=True)
