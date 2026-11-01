"""Sequential foreground offline gates; no game, profile or background workers."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-pizza/gates"
OUT.mkdir(parents=True, exist_ok=True)
DOTNET = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
GUARD = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(ROOT)]
ENV = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1",
           UseSharedCompilation="false", DOTNET_CLI_TELEMETRY_OPTOUT="1")


def run(name, command, guard=False):
    print(f"Running {name}", flush=True)
    if guard:
        subprocess.run(GUARD, cwd=ROOT, env=ENV, check=True)
    log = OUT / (name + ".log")
    with log.open("w", encoding="utf-8") as stream:
        result = subprocess.run(command, cwd=ROOT, env=ENV, stdout=stream, stderr=subprocess.STDOUT)
    record = dict(name=name, command=command, exit=result.returncode,
                  log=str(log.relative_to(ROOT)), sha256=hashlib.sha256(log.read_bytes()).hexdigest())
    print(json.dumps(record), flush=True)
    if result.returncode:
        print(log.read_text(encoding="utf-8"), flush=True)
        raise SystemExit(result.returncode)
    return record


if __name__ == "__main__":
    phase = sys.argv[1]
    records = []
    projects = ["donut", "cupcake", "salad"] if phase == "before" else ["pizza"]
    if phase in ("before", "pizza"):
        for product in projects:
            records.append(run(phase + "-" + product, [DOTNET, "run", "--project",
                f"tools/{product}-feasibility-regression/{product.title()}FeasibilityRegression.csproj",
                "--configuration", "Release"], True))
    elif phase == "all":
        records.append(run("build", [sys.executable, "tools/build.py", "--game-managed",
            r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"], True))
        records.append(run("unit", [DOTNET, "test", "tests/PersistentCity.Tests.csproj"], True))
        records.append(run("discovery", [DOTNET, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"], True))
        for folder, name in [
            ("core-identity", "CoreIdentity"), ("diagnostic-response", "DiagnosticResponse"),
            ("event-observation", "EventObservation"), ("food-catalogue", "FoodCatalogue"),
            ("food-checkout", "FoodCheckout"), ("food-hours", "FoodHours"),
            ("food-household-catalogue", "FoodHouseholdCatalogue"), ("food-ownership", "FoodOwnership"),
            ("food-supply", "FoodSupply"), ("frame-observation", "FrameObservation"),
            ("gate-access", "GateAccess"), ("road-ceiling", "RoadCeiling"),
            ("road-handoff", "RoadHandoff"), ("road-invalidation", "RoadInvalidation"),
            ("road-speed", "RoadSpeed"), ("route-discovery", "RouteDiscovery"),
            ("save-contract", "SaveContract"), ("startup-clock", "StartupClock"),
            ("traffic-closure", "TrafficClosure"), ("traffic-queue", "TrafficQueue"),
            ("walking-quote", "WalkingQuote"), ("campaign-reader", "CampaignReader"),
            ("donut-feasibility", "DonutFeasibility"), ("cupcake-feasibility", "CupcakeFeasibility"),
            ("salad-feasibility", "SaladFeasibility"), ("pizza-feasibility", "PizzaFeasibility")]:
            records.append(run(folder, [DOTNET, "run", "--project",
                f"tools/{folder}-regression/{name}Regression.csproj", "--configuration", "Release"], True))
        for folder in ["native-command", "physical-selection", "vehicle-ownership", "optional-stop"]:
            records.append(run(folder, [sys.executable, f"tools/{folder}-regression/run.py"], True))
        for name, command in [
            ("simulate", [DOTNET, "run", "--project", "tools/simulate/Simulate.csproj", "--", "28", "build/harness-demo"]),
            ("food-routines", [DOTNET, "run", "--project", "tools/food-routines/FoodRoutines.csproj", "--configuration", "Release", "--", "build/harness-food-routines-demo", "957"]),
            ("food-demand", [DOTNET, "run", "--project", "tools/food-demand/FoodDemand.csproj", "--", "build/harness-food-demand-demo"])]:
            records.append(run(name, command, True))
        for product in ["donut", "salad", "pizza"]:
            records.append(run(product + "-source", [sys.executable, f"tools/{product}-feasibility-regression/source_checks.py"]))
    else:
        raise SystemExit("Expected before, pizza or all")
    (OUT / (phase + "-results.json")).write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
