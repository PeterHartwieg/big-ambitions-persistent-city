"""Foreground-only E3 offline gates. No game/rig/profile access and no command timeouts."""
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-salad-sync-base/gates"
OUT.mkdir(parents=True, exist_ok=True)
DOTNET = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
WAIT = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
        "C:/Users/Peter/Coding_Projects/.agent-logs/wait-other-dotnet.ps1", "-Repo", str(ROOT)]
env = os.environ.copy()
env["PATH"] = str(Path(DOTNET).parent) + os.pathsep + env.get("PATH", "")
env["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
env["MSBUILDDISABLENODEREUSE"] = "1"
env["UseSharedCompilation"] = "false"
gates = [
    ("build", [sys.executable, "tools/build.py", "--game-managed",
               "C:/Program Files (x86)/Steam/steamapps/common/Big Ambitions/Big Ambitions_Data/Managed"]),
    ("unit", [DOTNET, "test", "tests/PersistentCity.Tests.csproj"]),
    ("list", [DOTNET, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"]),
]
projects = [
    ("core-identity", "CoreIdentity"), ("diagnostic-response", "DiagnosticResponse"),
    ("event-observation", "EventObservation"), ("food-catalogue", "FoodCatalogue"),
    ("food-checkout", "FoodCheckout"), ("food-hours", "FoodHours"),
    ("food-household-catalogue", "FoodHouseholdCatalogue"), ("food-ownership", "FoodOwnership"),
    ("food-supply", "FoodSupply"), ("frame-observation", "FrameObservation"),
    ("gate-access", "GateAccess"), ("road-ceiling", "RoadCeiling"), ("road-handoff", "RoadHandoff"),
    ("road-invalidation", "RoadInvalidation"), ("road-speed", "RoadSpeed"),
    ("route-discovery", "RouteDiscovery"), ("save-contract", "SaveContract"),
    ("startup-clock", "StartupClock"), ("traffic-closure", "TrafficClosure"),
    ("traffic-queue", "TrafficQueue"), ("walking-quote", "WalkingQuote"), ("campaign-reader", "CampaignReader"),
]
for name, project in projects:
    gates.append((name, [DOTNET, "run", "--project", f"tools/{name}-regression/{project}Regression.csproj",
                         "--configuration", "Release"]))
# Include every regression project brought in by the combined development tree.
listed = {command[command.index("--project") + 1] for _, command in gates if "--project" in command}
for project in sorted(ROOT.glob("tools/*-regression/*.csproj")):
    relative = project.relative_to(ROOT).as_posix()
    if relative not in listed and project.parent.name not in ("donut-feasibility-regression", "cupcake-feasibility-regression"):
        gates.append((project.parent.name.removesuffix("-regression"),
                      [DOTNET, "run", "--project", relative, "--configuration", "Release"]))
for name in ("native-command", "physical-selection", "vehicle-ownership", "optional-stop"):
    gates.append((name, [sys.executable, f"tools/{name}-regression/run.py"]))
gates += [
    ("pickup", [DOTNET, "run", "--project", "tools/persistent-continuity/regression/PickupRegression.csproj", "--configuration", "Release"]),
    ("optional-stop-performance", [sys.executable, "tools/optional-stop-regression/run.py", "--performance", "--findings"]),
    ("simulate", [DOTNET, "run", "--project", "tools/simulate/Simulate.csproj", "--", "28", "build/harness-demo"]),
    ("food-routines", [DOTNET, "run", "--project", "tools/food-routines/FoodRoutines.csproj", "--configuration", "Release", "--", "build/harness-food-routines-demo", "957"]),
    ("food-demand", [DOTNET, "run", "--project", "tools/food-demand/FoodDemand.csproj", "--", "build/harness-food-demand-demo"]),
    ("donut", [DOTNET, "run", "--project", "tools/donut-feasibility-regression/DonutFeasibilityRegression.csproj", "--configuration", "Release"]),
    ("cupcake", [DOTNET, "run", "--project", "tools/cupcake-feasibility-regression/CupcakeFeasibilityRegression.csproj", "--configuration", "Release"]),
    ("isolation", [sys.executable, "tools/donut-feasibility-regression/source_checks.py"]),
    ("donut-differential", [sys.executable, "tools/cupcake-feasibility-regression/compare_donut.py"]),
    ("salad-isolation", [sys.executable, "tools/salad-feasibility-regression/source_checks.py"]),
    ("salad-differential", [sys.executable, "tools/salad-feasibility-regression/compare_baseline.py", "differential"]),
    ("salad-baseline", [sys.executable, "tools/salad-feasibility-regression/compare_baseline.py", "before"]),
    ("donut-baseline", [sys.executable, "tools/cupcake-feasibility-regression/measure_before.py"]),
]
results = []
for name, command in gates:
    print(f"START {name}", flush=True)
    # Python harnesses also launch dotnet; serialize those too. No tests are detached.
    if name not in ("isolation", "salad-isolation"):
        wait = subprocess.run(WAIT, cwd=ROOT, env=env, capture_output=True, text=True)
        (OUT / f"{name}-wait.txt").write_text(wait.stdout + wait.stderr, encoding="utf-8")
        if wait.returncode:
            raise SystemExit(f"wait failed for {name}: {wait.returncode}")
    with (OUT / f"{name}.txt").open("w", encoding="utf-8") as log:
        result = subprocess.run(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
    results.append({"name": name, "command": command, "exit": result.returncode})
    (OUT / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"END {name}: {result.returncode}", flush=True)
    if result.returncode:
        print((OUT / f"{name}.txt").read_text(encoding="utf-8"), flush=True)
        raise SystemExit(result.returncode)
print(f"PASS {len(results)} foreground gates", flush=True)
