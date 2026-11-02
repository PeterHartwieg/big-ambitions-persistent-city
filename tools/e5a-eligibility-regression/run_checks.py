"""Required legacy gates and E5a proof, all synchronously in the foreground."""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import os
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
out = root / "build/e5a/gates"
out.mkdir(parents=True, exist_ok=True)
guard = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
commands = [
    ("d3a-required", [sys.executable, "tools/run-d3a-checks.py"]),
    ("d4-required", [sys.executable, "tools/rival-memory-regression/validate.py"]),
    ("e3-source", [sys.executable, "tools/e3-remaining-feasibility-regression/source_checks.py"]),
    ("e5a-source", [sys.executable, "tools/e5a-eligibility-regression/source_checks.py"]),
    ("e5a-final", [dotnet, "run", "--project", "tools/e5a-eligibility-regression/E5aEligibilityRegression.csproj", "--configuration", "Release", "--disable-build-servers"]),
]
parser = argparse.ArgumentParser(__doc__)
parser.add_argument("--start-at", type=int, default=1)
args = parser.parse_args()
receipt = out / "results.json"
results = json.loads(receipt.read_text())[:args.start_at - 1] if args.start_at > 1 else []
for index, (name, command) in enumerate(commands, 1):
    if index < args.start_at:
        continue
    print(f"START {index}/{len(commands)} {name}", flush=True)
    subprocess.run(guard, cwd=root, env=env, check=True)
    log = out / (name + ".log")
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with log.open("w", encoding="utf-8") as stream:
        code = subprocess.run(command, cwd=root, env=env, stdout=stream, stderr=subprocess.STDOUT).returncode
    results.append(dict(name=name, command=command, started=started, exit=code,
                        log=log.relative_to(root).as_posix(), sha256=hashlib.sha256(log.read_bytes()).hexdigest()))
    receipt.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print(f"END {name}: exit {code}", flush=True)
    if code:
        print("\n".join(log.read_text(encoding="utf-8", errors="replace").splitlines()[-35:]), flush=True)
        raise SystemExit(code)
print("PASS all foreground gates", flush=True)
