"""Run this slice's offline gates sequentially in the foreground, without deadlines.

Logs/inspection stay in ignored build/e3-frenchfries. No game/profile/rig access.
"""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-frenchfries/gates"
OUT.mkdir(parents=True, exist_ok=True)
DOTNET = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
ENV = os.environ.copy()
ENV.update(DOTNET_CLI_USE_MSBUILD_SERVER="0", DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="1", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
ENV["PATH"] = str(Path(DOTNET).parent) + os.pathsep + ENV.get("PATH", "")
GUARD = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(ROOT)]
commands = [[sys.executable, "tools/build.py", "--game-managed",
             r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"],
            [DOTNET, "test", "tests/PersistentCity.Tests.csproj", "-p:UseSharedCompilation=false", "--disable-build-servers"],
            [DOTNET, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"]]
for name, project in (
    ("core-identity-regression", "CoreIdentityRegression"),
    ("diagnostic-response-regression", "DiagnosticResponseRegression"),
    ("event-observation-regression", "EventObservationRegression"),
    ("food-catalogue-regression", "FoodCatalogueRegression"),
    ("food-checkout-regression", "FoodCheckoutRegression"),
    ("food-hours-regression", "FoodHoursRegression"),
    ("food-household-catalogue-regression", "FoodHouseholdCatalogueRegression"),
    ("food-ownership-regression", "FoodOwnershipRegression"),
    ("food-supply-regression", "FoodSupplyRegression"),
    ("frame-observation-regression", "FrameObservationRegression"),
    ("gate-access-regression", "GateAccessRegression"),
    ("road-ceiling-regression", "RoadCeilingRegression"),
    ("road-handoff-regression", "RoadHandoffRegression"),
    ("road-invalidation-regression", "RoadInvalidationRegression"),
    ("road-speed-regression", "RoadSpeedRegression"),
    ("route-discovery-regression", "RouteDiscoveryRegression"),
    ("save-contract-regression", "SaveContractRegression"),
    ("startup-clock-regression", "StartupClockRegression"),
    ("traffic-closure-regression", "TrafficClosureRegression"),
    ("traffic-queue-regression", "TrafficQueueRegression"),
    ("walking-quote-regression", "WalkingQuoteRegression"),
    ("campaign-reader-regression", "CampaignReaderRegression"),
    ("donut-feasibility-regression", "DonutFeasibilityRegression"),
    ("cupcake-feasibility-regression", "CupcakeFeasibilityRegression"),
    ("salad-feasibility-regression", "SaladFeasibilityRegression"),
    ("frenchfries-feasibility-regression", "FrenchFriesFeasibilityRegression"),
):
    commands.append([DOTNET, "run", "--project", f"tools/{name}/{project}.csproj", "--configuration", "Release",
                     "-p:UseSharedCompilation=false", "--disable-build-servers"])
for script in ("native-command-regression/run.py", "physical-selection-regression/run.py",
               "vehicle-ownership-regression/run.py", "optional-stop-regression/run.py",
               "donut-feasibility-regression/source_checks.py", "salad-feasibility-regression/source_checks.py",
               "frenchfries-feasibility-regression/source_checks.py"):
    commands.append([sys.executable, "tools/" + script])
for project, args in (("simulate/Simulate", ["28", "build/harness-demo"]),
                      ("food-routines/FoodRoutines", ["build/harness-food-routines-demo", "957"]),
                      ("food-demand/FoodDemand", ["build/harness-food-demand-demo"])):
    commands.append([DOTNET, "run", "--project", "tools/" + project + ".csproj",
                     "-p:UseSharedCompilation=false", "--disable-build-servers"]
                    + (["--configuration", "Release"] if project.startswith("food-routines/") else []) + ["--"] + args)
commands += [[sys.executable, "tools/cupcake-feasibility-regression/compare_donut.py"],
             [sys.executable, "tools/salad-feasibility-regression/compare_baseline.py", "differential"]]
results = []
for i, command in enumerate(commands, 1):
    # Also guard Python harnesses/builds: they may themselves invoke the compiler/run host.
    subprocess.run(GUARD, cwd=ROOT, env=ENV, check=True)
    label = next((arg for arg in command if arg.endswith((".csproj", ".py"))), command[0])
    print(f"[{i}/{len(commands)}] {label}", flush=True)
    log = OUT / f"{i:02d}.log"
    with log.open("w", encoding="utf-8") as output:
        result = subprocess.run(command, cwd=ROOT, env=ENV, stdout=output, stderr=subprocess.STDOUT)
    results.append(dict(command=command, exit=result.returncode, log=str(log.relative_to(ROOT)),
                        sha256=hashlib.sha256(log.read_bytes()).hexdigest()))
    (OUT / "results.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print(f"exit {result.returncode}: " + log.read_text(encoding="utf-8")[-1000:], flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)
print(f"PASS {len(commands)}/{len(commands)} foreground gates", flush=True)
