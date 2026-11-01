"""Sequential foreground C4 gate. No timeouts, background workers, game or rig access."""
import json
import os
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
dotnet = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
env = dict(os.environ)
env["PATH"] = str(dotnet.parent) + os.pathsep + env["PATH"]
env["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
env["MSBUILDDISABLENODEREUSE"] = "1"
env["UseSharedCompilation"] = "false"
out = root / "build/c4/gates"
out.mkdir(parents=True, exist_ok=True)
wait = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
        r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
commands = [
    [sys.executable, "tools/build.py", "--game-managed", r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"],
    wait,
    [str(dotnet), "test", "tests/PersistentCity.Tests.csproj"],
    [str(dotnet), "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"],
]
projects = [
    ("core-identity-regression", "CoreIdentityRegression"), ("diagnostic-response-regression", "DiagnosticResponseRegression"),
    ("event-observation-regression", "EventObservationRegression"), ("food-catalogue-regression", "FoodCatalogueRegression"),
    ("food-checkout-regression", "FoodCheckoutRegression"), ("food-hours-regression", "FoodHoursRegression"),
    ("food-household-catalogue-regression", "FoodHouseholdCatalogueRegression"), ("food-ownership-regression", "FoodOwnershipRegression"),
    ("food-supply-regression", "FoodSupplyRegression"), ("frame-observation-regression", "FrameObservationRegression"),
    ("gate-access-regression", "GateAccessRegression"), ("road-ceiling-regression", "RoadCeilingRegression"),
    ("road-handoff-regression", "RoadHandoffRegression"), ("road-invalidation-regression", "RoadInvalidationRegression"),
    ("road-speed-regression", "RoadSpeedRegression"), ("route-discovery-regression", "RouteDiscoveryRegression"),
    ("save-contract-regression", "SaveContractRegression"), ("startup-clock-regression", "StartupClockRegression"),
    ("traffic-closure-regression", "TrafficClosureRegression"), ("traffic-queue-regression", "TrafficQueueRegression"),
    ("walking-quote-regression", "WalkingQuoteRegression"),
    ("campaign-reader-regression", "CampaignReaderRegression"),
]
commands += [[str(dotnet), "run", "--project", f"tools/{folder}/{project}.csproj", "--configuration", "Release"] for folder, project in projects]
listed = {command[command.index("--project") + 1] for command in commands if "--project" in command}
for project in sorted(root.glob("tools/*-regression/*.csproj")):
    relative = project.relative_to(root).as_posix()
    if relative not in listed and project.parent.name != "market-research-regression":
        commands.append([str(dotnet), "run", "--project", relative, "--configuration", "Release"])
commands += [[sys.executable, f"tools/{name}/run.py"] for name in [
    "native-command-regression", "physical-selection-regression", "vehicle-ownership-regression", "optional-stop-regression"]]
commands += [
    [str(dotnet), "run", "--project", "tools/simulate/Simulate.csproj", "--", "28", "build/harness-demo"],
    [str(dotnet), "run", "--project", "tools/food-routines/FoodRoutines.csproj", "--configuration", "Release", "--", "build/harness-food-routines-demo", "957"],
    [str(dotnet), "run", "--project", "tools/food-demand/FoodDemand.csproj", "--", "build/harness-food-demand-demo"],
    [str(dotnet), "run", "--project", "tools/market-research-regression/MarketResearchRegression.csproj", "--configuration", "Release", "--", "build/c4/measurements.json"],
    [sys.executable, "tools/market-research-regression/compare_save_baseline.py"],
]
results = []
for i, command in enumerate(commands, 1):
    print(f"[{i}/{len(commands)}] {' '.join(command)}", flush=True)
    # Also guard Python regression children, which invoke dotnet run themselves.
    if command != wait and (command[0] == str(dotnet) or command[0] == sys.executable):
        subprocess.run(wait, cwd=root, env=env, check=True)
    path = out / f"{i:02d}.log"
    with path.open("w", encoding="utf-8") as log:
        result = subprocess.run(command, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT)
    results.append({"command": command, "exitCode": result.returncode, "log": str(path.relative_to(root))})
    (out / "manifest.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    if result.returncode:
        print(path.read_text(encoding="utf-8"), flush=True)
        raise SystemExit(result.returncode)
    print("PASS", flush=True)
print(f"{len(results)} foreground command entries passed", flush=True)
