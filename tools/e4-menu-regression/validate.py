"""Required E4 foreground gates. No subprocess deadlines, background workers or game access."""
from pathlib import Path
import json
import os
import subprocess
import time

root = Path(__file__).resolve().parents[2]
out = root / "build/e4-menu/validation"
out.mkdir(parents=True, exist_ok=True)
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
guard = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0", UseSharedCompilation="false")
projects = ["core-identity", "diagnostic-response", "event-observation", "food-catalogue", "food-checkout", "food-hours",
            "food-household-catalogue", "food-ownership", "food-supply", "frame-observation", "gate-access", "road-ceiling",
            "road-handoff", "road-invalidation", "road-speed", "route-discovery", "save-contract", "startup-clock",
            "traffic-closure", "traffic-queue", "walking-quote", "campaign-reader"]
names = ["CoreIdentity", "DiagnosticResponse", "EventObservation", "FoodCatalogue", "FoodCheckout", "FoodHours",
         "FoodHouseholdCatalogue", "FoodOwnership", "FoodSupply", "FrameObservation", "GateAccess", "RoadCeiling",
         "RoadHandoff", "RoadInvalidation", "RoadSpeed", "RouteDiscovery", "SaveContract", "StartupClock",
         "TrafficClosure", "TrafficQueue", "WalkingQuote", "CampaignReader"]
commands = [["python", "tools/build.py", "--game-managed", r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"],
            [dotnet, "test", "tests/PersistentCity.Tests.csproj"],
            [dotnet, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"]]
commands += [[dotnet, "run", "--project", f"tools/{folder}-regression/{name}Regression.csproj", "--configuration", "Release"] for folder, name in zip(projects, names)]
commands += [["python", f"tools/{folder}-regression/run.py"] for folder in ["native-command", "physical-selection", "vehicle-ownership", "optional-stop"]]
commands += [[dotnet,"run","--project","tools/simulate/Simulate.csproj","--","28","build/harness-demo"],
             [dotnet,"run","--project","tools/food-routines/FoodRoutines.csproj","--configuration","Release","--","build/harness-food-routines-demo","957"],
             [dotnet,"run","--project","tools/food-demand/FoodDemand.csproj","--","build/harness-food-demand-demo"],
             [dotnet,"run","--project","tools/e4-menu-regression/E4MenuRegression.csproj","--configuration","Release"]]
results=[]
for index, command in enumerate(commands):
    print(f"[{index+1}/{len(commands)}] {' '.join(command)}",flush=True)
    if command[0] == dotnet or command[0] == "python" and command[1].endswith("/run.py"):
        subprocess.run(guard,cwd=root,env=env,check=True)
    started=time.monotonic()
    with (out/f"{index+1:02}.log").open("w",encoding="utf-8") as log:
        result=subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
    results.append(dict(command=command,exit=result.returncode,seconds=round(time.monotonic()-started,3),log=f"{index+1:02}.log"))
    (out/"results.json").write_text(json.dumps(results,indent=2))
    print(f"exit {result.returncode}; {results[-1]['seconds']} s",flush=True)
print(f"{sum(r['exit']==0 for r in results)}/{len(results)} foreground gates passed",flush=True)
raise SystemExit(any(r["exit"] for r in results))
