"""Sequential foreground B2 offline checks. No rig access, background workers or timeouts."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

root = Path(__file__).resolve().parents[1]
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
wait = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
        r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
managed = r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
commands = [["python", "tools/build.py", "--game-managed", managed],
    [dotnet, "test", "tests/PersistentCity.Tests.csproj"],
    [dotnet, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"]]
for name, project in [
    ("core-identity", "CoreIdentity"), ("diagnostic-response", "DiagnosticResponse"),
    ("event-observation", "EventObservation"), ("food-catalogue", "FoodCatalogue"),
    ("food-checkout", "FoodCheckout"), ("food-hours", "FoodHours"),
    ("food-household-catalogue", "FoodHouseholdCatalogue"), ("food-ownership", "FoodOwnership"),
    ("food-supply", "FoodSupply"), ("frame-observation", "FrameObservation"),
    ("gate-access", "GateAccess"), ("road-ceiling", "RoadCeiling"), ("road-handoff", "RoadHandoff"),
    ("road-invalidation", "RoadInvalidation"), ("road-speed", "RoadSpeed"),
    ("route-discovery", "RouteDiscovery"), ("save-contract", "SaveContract"),
    ("startup-clock", "StartupClock"), ("traffic-closure", "TrafficClosure"),
    ("traffic-queue", "TrafficQueue"), ("walking-quote", "WalkingQuote")]:
    commands.append([dotnet, "run", "--project", f"tools/{name}-regression/{project}Regression.csproj", "--configuration", "Release"])
for name in ["native-command", "physical-selection", "vehicle-ownership", "optional-stop"]:
    commands.append(["python", f"tools/{name}-regression/run.py"])
commands += [
    [dotnet, "run", "--project", "tools/simulate/Simulate.csproj", "--", "28", "build/harness-demo"],
    [dotnet, "run", "--project", "tools/food-routines/FoodRoutines.csproj", "--configuration", "Release", "--", "build/harness-food-routines-demo", "957"],
    [dotnet, "run", "--project", "tools/food-demand/FoodDemand.csproj", "--", "build/harness-food-demand-demo"],
    [dotnet, "run", "--project", "tools/food-regulars-regression/FoodRegularsRegression.csproj", "--configuration", "Release"]]
logs = root / "build/b2-offline"
logs.mkdir(parents=True, exist_ok=True)
report = {"boundary": "Offline only, foreground sequential. Wait script before each dotnet test/run. No rig.", "commands": []}
failed = False
for index, command in enumerate(commands):
    if command[0] == dotnet:
        subprocess.run(wait, cwd=root, check=True)
    shown = ["%LOCALAPPDATA%/Microsoft/dotnet/dotnet.exe" if part == dotnet else part for part in command]
    print(f"[{index+1}/{len(commands)}] " + " ".join(shown), flush=True)
    start = time.monotonic()
    result = subprocess.run(command, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, encoding="utf-8", errors="replace")
    output = result.stdout
    (logs / f"{index+1:02d}.txt").write_text(output, encoding="utf-8")
    row = {"command": shown, "exitCode": result.returncode, "seconds": round(time.monotonic()-start, 3),
           "outputSha256": hashlib.sha256(output.encode()).hexdigest()}
    if index == 1:
        row["testResult"] = next((line.strip() for line in output.splitlines() if "Passed!" in line or "Failed!" in line), "no summary")
    if index == 2:
        row["discoveredTests"] = sum(line.startswith("    PersistentCity.Tests.") for line in output.splitlines())
    report["commands"].append(row)
    print(f"exit={result.returncode}; {row.get('testResult', '')}", flush=True)
    if result.returncode:
        print(output[-12000:], flush=True)
        failed = True
    if "food-regulars-regression/FoodRegularsRegression.csproj" in command:
        (root / "docs/reviews/2026-10-31-b2-measurements.json").write_text(output[output.index("{"):], encoding="utf-8")
source_paths = sorted(p for folder in ["Core", "Runtime", "Persistence", "tests", "tools/food-regulars-regression", "tools/food-ownership-regression"] for p in (root/folder).glob("*.cs"))
report["sourceSha256"] = {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in source_paths}
report["allPassed"] = not failed
(root / "docs/reviews/2026-10-31-b2-offline-checks.json").write_text(json.dumps(report, indent=2)+"\n", encoding="utf-8")
raise SystemExit(1 if failed else 0)
