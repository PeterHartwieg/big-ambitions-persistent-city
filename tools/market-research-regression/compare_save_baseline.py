"""Compare exact schema-19 UTF-8 fixture bytes with and without C4.

No checkout/branch changes, game input, save installation or background commands.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / "build/c4/save-comparison"
parser = argparse.ArgumentParser(__doc__)
parser.add_argument("--baseline", default="origin/codex/persistent-city",
                    help="Current integrated population source without C4")
args = parser.parse_args()
ref = subprocess.check_output(["git", "rev-parse", args.baseline], cwd=root, text=True).strip()
baseline = out / "source" / ref
for path in subprocess.check_output(["git", "ls-tree", "-r", "--name-only", ref, "--", "Core", "Persistence"], cwd=root, text=True).splitlines():
    if not path.endswith(".cs"):
        continue
    target = baseline / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(subprocess.check_output(["git", "show", f"{ref}:{path}"], cwd=root))
dotnet = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
wait = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
        r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
for label, source in [("baseline", baseline), ("candidate", root)]:
    subprocess.run(wait, cwd=root, check=True)
    subprocess.run([str(dotnet), "run", "--project", "tools/market-research-regression/save-bytes/SaveBytes.csproj",
                    "--configuration", "Release", f"-p:SourceRoot={source.as_posix()}", "--", str(out / label)], cwd=root, check=True)
result = {"baseline": ref, "candidateCoreCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(), "fixtures": []}
for name in ["population.json", "food.json"]:
    before = (out / "baseline" / name).read_bytes()
    after = (out / "candidate" / name).read_bytes()
    assert json.loads(before)["Schema"] == json.loads(after)["Schema"] == 19, "Expected current population schema 19"
    assert before == after, f"Changed saved bytes: {name}"
    result["fixtures"].append({"fixture": name, "schema": 19, "baselineBytes": len(before), "candidateBytes": len(after), "delta": 0,
                               "identicalBytes": True, "sha256": hashlib.sha256(after).hexdigest()})
json_text = json.dumps(result, indent=2) + "\n"
(out / "result.json").write_text(json_text, encoding="utf-8")
print(json_text)
