"""Sequential foreground C2a offline gate. No subprocess timeout or background workers."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

root = Path(__file__).resolve().parents[2]
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
wait = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
managed = r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
commands = [[sys.executable, "tools/build.py", "--game-managed", managed],
            [dotnet, "test", "tests/PersistentCity.Tests.csproj"],
            [dotnet, "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--list-tests"]]
for tool in ["core-identity", "diagnostic-response", "event-observation", "food-catalogue", "food-checkout", "food-hours", "food-household-catalogue", "food-ownership", "food-supply", "frame-observation", "gate-access", "road-ceiling", "road-handoff", "road-invalidation", "road-speed", "route-discovery", "save-contract", "startup-clock", "traffic-closure", "traffic-queue", "walking-quote"]:
    project = {"core-identity":"CoreIdentity", "diagnostic-response":"DiagnosticResponse", "event-observation":"EventObservation", "food-catalogue":"FoodCatalogue", "food-checkout":"FoodCheckout", "food-hours":"FoodHours", "food-household-catalogue":"FoodHouseholdCatalogue", "food-ownership":"FoodOwnership", "food-supply":"FoodSupply", "frame-observation":"FrameObservation", "gate-access":"GateAccess", "road-ceiling":"RoadCeiling", "road-handoff":"RoadHandoff", "road-invalidation":"RoadInvalidation", "road-speed":"RoadSpeed", "route-discovery":"RouteDiscovery", "save-contract":"SaveContract", "startup-clock":"StartupClock", "traffic-closure":"TrafficClosure", "traffic-queue":"TrafficQueue", "walking-quote":"WalkingQuote"}[tool]
    commands.append([dotnet, "run", "--project", f"tools/{tool}-regression/{project}Regression.csproj", "--configuration", "Release"])
for tool in ["native-command", "physical-selection", "vehicle-ownership", "optional-stop"]:
    commands.append([sys.executable, f"tools/{tool}-regression/run.py"])
commands += [[dotnet,"run","--project","tools/simulate/Simulate.csproj","--","28","build/harness-demo"],
             [dotnet,"run","--project","tools/food-routines/FoodRoutines.csproj","--configuration","Release","--","build/harness-food-routines-demo","957"],
             [dotnet,"run","--project","tools/food-demand/FoodDemand.csproj","--","build/harness-food-demand-demo"],
             [dotnet,"run","--project","tools/food-candidate-regression/FoodCandidateRegression.csproj","--configuration","Release"],
             [dotnet,"run","--project","tools/food-choice-regression/FoodChoiceRegression.csproj","--configuration","Release"]]
logroot = root / "build/c2a-checks"
logroot.mkdir(exist_ok=True)
manifest = {"source": subprocess.check_output(["git","rev-parse","HEAD"],cwd=root,text=True).strip(), "startedUtc":datetime.datetime.now(datetime.timezone.utc).isoformat(),"commands":[]}
manifest_path=root / "docs/reviews/2026-10-31-c2a-offline-checks.json"
for index,command in enumerate(commands):
    path=logroot / f"{index+1:02}.log"
    start=time.perf_counter()
    print(f"[{index+1}/{len(commands)}] {' '.join(command[1:])}",flush=True)
    with path.open("wb") as log:
        if index>0:
            result=subprocess.run(wait,cwd=root,stdout=log,stderr=subprocess.STDOUT)
            if result.returncode: raise SystemExit("dotnet wait failed")
        result=subprocess.run(command,cwd=root,stdout=log,stderr=subprocess.STDOUT)
    raw=path.read_bytes()
    manifest["commands"].append({"command":command,"exitCode":result.returncode,"seconds":round(time.perf_counter()-start,3),"log":str(path.relative_to(root)),"sha256":hashlib.sha256(raw).hexdigest(),"tail":raw.decode("utf-8",errors="replace")[-2000:]})
    manifest_path.write_text(json.dumps(manifest,indent=2),encoding="utf-8")
    print(f"  exit {result.returncode}",flush=True)
    if result.returncode: print(raw.decode("utf-8",errors="replace")[-6000:],flush=True)
manifest["finishedUtc"]=datetime.datetime.now(datetime.timezone.utc).isoformat()
manifest_path.write_text(json.dumps(manifest,indent=2),encoding="utf-8")
raise SystemExit(any(row["exitCode"] for row in manifest["commands"]))
