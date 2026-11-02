"""Complete E5d offline gates and measurements, sequentially in the foreground."""
from pathlib import Path
import datetime
import json
import os
import subprocess
import sys
import time

root = Path(__file__).resolve().parents[2]
out = root / "build/e5d/final-gate"
out.mkdir(parents=True, exist_ok=True)
environment = dict(os.environ, MSBUILDDISABLENODEREUSE="1",
                   DOTNET_CLI_USE_MSBUILD_SERVER="0", UseSharedCompilation="false")
commands = [
    ("d3a", [sys.executable, "tools/run-d3a-checks.py"]),
    ("rival-memory", [sys.executable, "tools/rival-memory-regression/validate.py"]),
    ("remaining-source", [sys.executable, "tools/e3-remaining-feasibility-regression/source_checks.py"]),
    ("measure", [sys.executable, "tools/name-identity-regression/measure.py"]),
    ("report", [sys.executable, "tools/name-identity-regression/report.py"]),
]
results = []
for label, command in commands:
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    tick = time.perf_counter()
    log = out / (label + ".log")
    print(f"{label}: starting foreground phase; log {log.relative_to(root)}", flush=True)
    with log.open("w", encoding="utf-8") as stream:
        code = subprocess.run(command, cwd=root, env=environment,
                              stdout=stream, stderr=subprocess.STDOUT).returncode
    results.append(dict(phase=label, command=command, started=started,
                        seconds=time.perf_counter() - tick, exit=code,
                        log=log.relative_to(root).as_posix()))
    (out / "results.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    if code:
        print("\n".join(log.read_text(encoding="utf-8", errors="replace").splitlines()[-35:]), flush=True)
        raise SystemExit(code)
    print(f"{label}: PASS", flush=True)
print("PASS: all E5d foreground phases; no native evidence collected.", flush=True)
