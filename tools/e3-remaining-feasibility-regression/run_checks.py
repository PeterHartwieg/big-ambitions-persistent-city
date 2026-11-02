"""One sequential foreground gate; requested legacy runners repeat their own inner gates."""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-remaining-batch/gates"
OUT.mkdir(parents=True, exist_ok=True)
DOTNET = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
GUARD = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(ROOT)]
ENV = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER="0", DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="1",
           MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false", DOTNET_CLI_TELEMETRY_OPTOUT="1")
steps = [
    ("d3a-required", [sys.executable, "tools/run-d3a-checks.py"]),
    ("d4-required", [sys.executable, "tools/rival-memory-regression/validate.py"]),
    ("batch-source", [sys.executable, "tools/e3-remaining-feasibility-regression/source_checks.py", "--audit-base", "a1b993a"]),
    ("batch-measure", [DOTNET, "run", "--project", "tools/e3-remaining-feasibility-regression/E3RemainingFeasibilityRegression.csproj",
                       "--configuration", "Release", "--", "measure"])
]
parser = argparse.ArgumentParser(__doc__)
parser.add_argument("--start-at", type=int, default=1)
args = parser.parse_args()
manifest = OUT / "results.json"
results = json.loads(manifest.read_text())[:args.start_at - 1] if args.start_at > 1 else []
for i, (name, command) in enumerate(steps, 1):
    if i < args.start_at:
        continue
    print(f"START {i}/{len(steps)} {name}", flush=True)
    subprocess.run(GUARD, cwd=ROOT, env=ENV, check=True)
    log = OUT / (name + ".log")
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with log.open("w", encoding="utf-8") as stream:
        result = subprocess.run(command, cwd=ROOT, env=ENV, stdout=stream, stderr=subprocess.STDOUT)
    results.append(dict(name=name, command=command, started=started, exit=result.returncode,
                        log=log.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(log.read_bytes()).hexdigest()))
    manifest.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print(f"END {name}: exit {result.returncode}; {log.relative_to(ROOT)}", flush=True)
    if result.returncode:
        print("\n".join(log.read_text(encoding="utf-8").splitlines()[-40:]), flush=True)
        raise SystemExit(result.returncode)
print("PASS all required foreground gates; individual receipts remain under build/d3a and build/d4-validation", flush=True)
