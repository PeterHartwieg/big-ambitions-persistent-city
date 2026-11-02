"""Sequential foreground schema-24/25 measurements. No deadlines or game/rig access."""
from pathlib import Path
import datetime
import json
import os
import subprocess
import sys
import time

root = Path(__file__).resolve().parents[2]
out = root / "build/e5d/measurements"
out.mkdir(parents=True, exist_ok=True)
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
environment = os.environ.copy()
environment.update(MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0", UseSharedCompilation="false")
wait = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
        r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
subprocess.run([sys.executable, "tools/name-identity-regression/prepare.py"], cwd=root, check=True)
manifest = out / "results.json"
results = json.loads(manifest.read_text(encoding="utf-8")) if manifest.exists() else {}
cases = sys.argv[1:] or ["957x2", "957x3", "5000x2", "5000x3", "50000x2", "50000x3", "CF32", "CF64", "churn", "held64", "encoding", "record-boundary", "input-boundary"]
for case in cases:
    arms = ["baseline", "candidate"] if case not in {"held64", "encoding", "record-boundary", "input-boundary"} else ["candidate"]
    for arm in arms:
        subprocess.run(wait, cwd=root, check=True)
        project = "build/e5d/baseline-runner/NameIdentityBaseline.csproj" if arm == "baseline" else "tools/name-identity-regression/NameIdentityRegression.csproj"
        command = [dotnet, "run", "--project", project, "--configuration", "Release", "--", case, "3"]
        key = f"{case}-{arm}"
        started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        tick = time.perf_counter()
        log = out / f"{key}.log"
        with log.open("w", encoding="utf-8") as stream:
            code = subprocess.run(command, cwd=root, env=environment, stdout=stream, stderr=subprocess.STDOUT).returncode
        lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
        if code:
            print("\n".join(lines[-35:]), flush=True)
            raise SystemExit(f"{key}: exit {code}")
        payload = json.loads(next(line for line in reversed(lines) if line.startswith("{")))
        results[key] = dict(command=command, started=started, seconds=time.perf_counter() - tick,
                            exit=code, log=str(log.relative_to(root)), result=payload)
        if arm == "candidate" and "baseline" in arms:
            baseline = results[f"{case}-baseline"]["result"]
            for field in ["chars", "bytes", "Streaming", "Semantic"]:
                assert baseline["before"][field] == payload["before"][field], f"Frozen-24 mismatch: {case}/{field}"
            assert baseline["sha256"] == payload["beforeSha256"], f"Frozen-24 byte mismatch: {case}"
        manifest.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
        print(f"{key}: PASS; {results[key]['seconds']:.2f}s", flush=True)
