"""F7a save-cost baseline driver (issue #94). Offline only.

Runs every fixture in its own foreground process (so peak working set is per fixture), after
waiting for other worktrees' dotnet test/run. No deadlines, background jobs, game or rig inputs.
Writes JSONL to docs/perf/assets/f7a/ (metrics only, never payloads).

    python tools/save-cost/measure.py [samples] [fixture ...]
"""
import json
import os
import pathlib
import platform
import subprocess
import sys
import datetime

root = pathlib.Path(__file__).resolve().parents[2]
dotnet = os.path.join(os.environ["LOCALAPPDATA"], "Microsoft", "dotnet", "dotnet.exe")
wait = r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1"
project = root / "tools" / "save-cost" / "SaveCost.csproj"
out = root / "docs" / "perf" / "assets" / "f7a"
representative = ["representative-957x2", "representative-5000x2", "representative-50000x2"]
stress = ["957x2", "957x3", "5000x2", "5000x3", "50000x2", "50000x3", "churn", "CF32", "CF64", "encoding-max"]


def wait_for_dotnet():
    if os.path.exists(wait):
        subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", wait, "-Repo", str(root)], check=True)


def run(arguments):
    wait_for_dotnet()
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    result = subprocess.run([dotnet, "run", "--project", str(project), "--configuration", "Release", "--no-build", "--"] + arguments,
                            cwd=root, capture_output=True, text=True)
    if result.returncode != 0:
        sys.stderr.write(result.stdout + result.stderr)
        raise SystemExit("save-cost " + " ".join(arguments) + " failed")
    line = [l for l in result.stdout.splitlines() if l.startswith("{")][-1]
    return {"started": started, "arguments": arguments, "result": json.loads(line)}


def main():
    # With no fixture list: build, record metadata and determinism, then measure every fixture.
    # With a fixture list: append those rows only (lets long matrices run as short foreground steps).
    samples = sys.argv[1] if len(sys.argv) > 1 else "7"
    fixtures = sys.argv[2:]
    full = not fixtures
    fixtures = fixtures or representative + stress
    out.mkdir(parents=True, exist_ok=True)
    wait_for_dotnet()
    subprocess.run([dotnet, "build", str(project), "--configuration", "Release", "-p:UseSharedCompilation=false"], cwd=root, check=True,
                   stdout=subprocess.DEVNULL)
    if full or fixtures == ["determinism"]:
        head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, capture_output=True, text=True).stdout.strip()
        (out / "metadata.json").write_text(json.dumps({"sourceHead": head, "host": platform.platform(), "python": platform.python_version(),
            "dotnet": subprocess.run([dotnet, "--version"], capture_output=True, text=True).stdout.strip(), "samples": int(samples),
            "note": "Windows .NET 10 synthetic; one process per fixture; not native or Mono evidence"}, indent=2) + "\n")
        with open(out / "determinism.jsonl", "w", encoding="utf-8") as handle:
            handle.write(json.dumps(run(["determinism"])) + "\n")
        if not full:
            return
    with open(out / "baseline.jsonl", "w" if full else "a", encoding="utf-8") as handle:
        for fixture in fixtures:
            row = run([fixture, samples])
            handle.write(json.dumps(row) + "\n"); handle.flush()
            print(fixture, row["result"]["payload"]["characters"], row["result"]["save"]["totalDiagnosticsOffMs"])


if __name__ == "__main__":
    main()
