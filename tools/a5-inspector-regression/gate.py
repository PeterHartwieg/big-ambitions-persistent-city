"""Sequential foreground A5 offline gate; no game/rig access and no command time limits."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/a5-gate"
OUT.mkdir(parents=True, exist_ok=True)
DOTNET = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
GUARD = Path("C:/Users/Peter/Coding_Projects/.agent-logs/wait-other-dotnet.ps1")
ENV = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="1", UseSharedCompilation="false")
ENV["PATH"] = str(DOTNET.parent) + os.pathsep + ENV["PATH"]


def run(name, command, guard=True):
    if guard:
        result = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(GUARD), "-Repo", str(ROOT)], cwd=ROOT, env=ENV)
        if result.returncode:
            raise SystemExit(f"dotnet guard failed: {result.returncode}")
    start = time.monotonic()
    with (OUT / (name + ".log")).open("w", encoding="utf-8") as log:
        result = subprocess.run([str(x) for x in command], cwd=ROOT, env=ENV, stdout=log, stderr=subprocess.STDOUT)
    row = dict(name=name, command=[str(x) for x in command], exit=result.returncode, seconds=round(time.monotonic()-start, 3))
    results.append(row)
    (OUT / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"{name}: exit {result.returncode}, {row['seconds']} s", flush=True)
    if result.returncode:
        print((OUT / (name + ".log")).read_text(encoding="utf-8")[-7000:], flush=True)
        raise SystemExit(result.returncode)


results = []
run("build", [sys.executable, "tools/build.py", "--game-managed", "C:/Program Files (x86)/Steam/steamapps/common/Big Ambitions/Big Ambitions_Data/Managed"])
run("unit", [DOTNET, "test", "tests/PersistentCity.Tests.csproj"])
run("discovery", [DOTNET, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"])
for folder in sorted((ROOT / "tools").glob("*-regression")):
    projects = list(folder.glob("*.csproj"))
    if projects:
        assert len(projects) == 1
        run(folder.name, [DOTNET, "run", "--project", projects[0].relative_to(ROOT), "--configuration", "Release"])
    elif (folder / "run.py").exists():
        run(folder.name, [sys.executable, (folder / "run.py").relative_to(ROOT)])
for name, project, args in [
    ("simulate", "tools/simulate/Simulate.csproj", ["28", "build/harness-demo"]),
    ("food-routines", "tools/food-routines/FoodRoutines.csproj", ["build/harness-food-routines-demo", "957"]),
    ("food-demand", "tools/food-demand/FoodDemand.csproj", ["build/harness-food-demand-demo"]),
]:
    command = [DOTNET, "run", "--project", project]
    if name == "food-routines":
        command += ["--configuration", "Release"]
    run(name, command + ["--"] + args)
for name, project, args in [
    ("choice-rush", "food-choice-regression/FoodChoiceRegression", ["--rush"]),
    ("choice-integrated", "food-choice-regression/FoodChoiceRegression", ["--integrated"]),
    ("choice-rush-integrated", "food-choice-regression/FoodChoiceRegression", ["--rush", "--integrated"]),
    ("choice-rush-integrated-5000", "food-choice-regression/FoodChoiceRegression", ["--rush-5000", "--integrated"]),
    ("save-bench", "save-contract-regression/SaveContractRegression", ["--bench", "--decisions"]),
]:
    run(name, [DOTNET, "run", "--project", "tools/" + project + ".csproj", "--configuration", "Release", "--"] + args)
print(f"PASS: {len(results)} foreground commands", flush=True)
