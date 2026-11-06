"""F7p compact-format prototype matrix (issue #94, plan section 3.6). Offline only.

Runs every (fixture, candidate) in its own foreground process, after waiting for other worktrees'
dotnet test/run, so peak working set belongs to that pair. No deadlines, background jobs, game or
rig inputs. Appends JSONL to docs/perf/assets/f7p/matrix.jsonl (metrics only, never payloads) and
checks candidate A against F7a's retained writer-25 hashes.

    python tools/compact-format/measure.py [samples] [fixture ...]
"""
import datetime
import json
import os
import pathlib
import platform
import subprocess
import sys

root = pathlib.Path(__file__).resolve().parents[2]
dotnet = os.path.join(os.environ["LOCALAPPDATA"], "Microsoft", "dotnet", "dotnet.exe")
wait = r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1"
project = root / "tools" / "compact-format" / "CompactFormat.csproj"
out = root / "docs" / "perf" / "assets" / "f7p"
f7a = root / "docs" / "perf" / "assets" / "f7a" / "baseline.jsonl"
fixtures_all = ["representative-957x2", "representative-5000x2", "representative-50000x2",
                "957x2", "957x3", "5000x2", "5000x3", "50000x2", "50000x3", "churn", "CF32", "CF64", "encoding-max"]
candidates = ["A", "B", "C", "D"]


def wait_for_dotnet():
    if os.path.exists(wait):
        subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", wait, "-Repo", str(root)], check=True)


def run(fixture, candidate, samples):
    wait_for_dotnet()
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    result = subprocess.run([dotnet, "run", "--project", str(project), "--configuration", "Release", "--no-build", "--",
                             fixture, candidate, samples], cwd=root, capture_output=True, text=True)
    if result.returncode != 0:
        sys.stderr.write(result.stdout + result.stderr)
        raise SystemExit("compact-format " + fixture + " " + candidate + " failed")
    line = [l for l in result.stdout.splitlines() if l.startswith("{")][-1]
    return {"started": started, "result": json.loads(line)}


def main():
    samples = sys.argv[1] if len(sys.argv) > 1 else "7"
    fixtures = sys.argv[2:] or fixtures_all
    retained = {}
    for line in open(f7a, encoding="utf-8"):
        row = json.loads(line)["result"]
        retained[row["fixture"]] = row["payload"]["sha256"]
    out.mkdir(parents=True, exist_ok=True)
    wait_for_dotnet()
    subprocess.run([dotnet, "build", str(project), "--configuration", "Release", "-p:UseSharedCompilation=false"], cwd=root, check=True,
                   stdout=subprocess.DEVNULL)
    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, capture_output=True, text=True).stdout.strip()
    (out / "metadata.json").write_text(json.dumps({"sourceHead": head, "host": platform.platform(), "python": platform.python_version(),
        "dotnet": subprocess.run([dotnet, "--version"], capture_output=True, text=True).stdout.strip(), "samples": int(samples),
        "note": "Windows .NET 10 synthetic; one process per fixture and candidate; not native or Mono evidence"}, indent=2) + "\n")
    with open(out / "matrix.jsonl", "a", encoding="utf-8") as handle:
        for fixture in fixtures:
            for candidate in candidates:
                row = run(fixture, candidate, samples)
                r = row["result"]
                if r["source"]["sha256"] != retained.get(fixture):
                    raise SystemExit(fixture + ": writer-25 source differs from F7a's retained hash")
                handle.write(json.dumps(row) + "\n"); handle.flush()
                print(fixture, candidate, r["payload"]["characters"], r["payload"]["characterRatioToWriter25"],
                      r["encodeMs"]["p50"], r["decodeMs"]["p50"], flush=True)


if __name__ == "__main__":
    main()
