"""Collect completed foreground receipts and explicit offline timing flags; runs no tests."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

root = Path(__file__).resolve().parents[2]


def read(path):
    data = path.read_bytes()
    return data.decode("utf-16" if data.startswith(b"\xff\xfe") else "utf-8-sig", errors="replace")


def objects(text):
    decoder = json.JSONDecoder()
    at = 0
    while True:
        at = text.find("{", at)
        if at < 0:
            return
        try:
            value, end = decoder.raw_decode(text[at:])
        except ValueError:
            at += 1
            continue
        yield value
        at += end


def flags(value, path=""):
    if isinstance(value, dict):
        for key, child in value.items():
            current = path + "/" + key
            lower = key.lower()
            if isinstance(child, (int, float)) and not any(word in lower for word in ("count", "spike", "sample")):
                threshold = 5 if "p95" in lower else 30
                timing_key = lower.endswith("ms") or lower.endswith("milliseconds") or (
                    "spikes30" in value and lower in ("p50", "p95", "max"))
                if timing_key and child >= threshold:
                    yield dict(path=current, value=child, thresholdMs=threshold,
                               spikesAtLeast30=value.get("spikesAtLeast30Ms", value.get("spikes30")),
                               kind="offline timing; aggregate/cold totals are not unit or native mod-work spikes")
            yield from flags(child, current)
    elif isinstance(value, list):
        for i, child in enumerate(value):
            yield from flags(child, path + f"/{i}")


outer = json.loads((root / "build/e5a/gates/results.json").read_text())
d3a = json.loads((root / "build/d3a/gates/results.json").read_text())
d4 = json.loads((root / "build/d4-validation/results.json").read_text())
assert len(outer) == 5 and all(row["exit"] == 0 for row in outer)
assert all(row["exit"] == 0 for row in d3a.values()) and all(row["exit"] == 0 for row in d4)
logs = sorted({root / row["log"] for row in [*outer, *d3a.values(), *d4]})
parsed_flags = []
for log in logs:
    text = read(log)
    for value in objects(text):
        for flag in flags(value):
            parsed_flags.append(dict(log=log.relative_to(root).as_posix(), **flag))
    for line in text.splitlines():
        if line.lstrip().startswith("Restored "):
            continue  # Package restore latency is not a component performance sample.
        values = [float(v.replace(",", ".")) for v in re.findall(r"(?<![\w.,<>=])(\d+(?:[.,]\d+)?)\s+ms\b", line)]
        if any(value >= 30 for value in values):
            parsed_flags.append(dict(log=log.relative_to(root).as_posix(), kind="offline text timing; inspect aggregate context",
                                     line=line[:1000], valuesAtLeast30Ms=[v for v in values if v >= 30]))
final = list(objects(read(root / "build/e5a/gates/e5a-final.log")))[-1]
before = json.loads((root / "build/e5a/before/receipt.json").read_text())
assert before["exit"] == 0
parsed_flags.extend(dict(log="build/e5a/before/receipt.json", **flag) for flag in flags(before["result"]))
exploratory = {}
for name in ("first-run", "optimized-run", "targeted-final"):
    path = root / f"build/e5a/{name}.log"
    if path.exists():
        exploratory[name] = dict(sha256=hashlib.sha256(path.read_bytes()).hexdigest(), result=list(objects(read(path)))[-1])
        parsed_flags.extend(dict(log=path.relative_to(root).as_posix(), **flag) for flag in flags(exploratory[name]["result"]))
test_counts = re.findall(r"Passed:\s+(\d+).*Skipped:\s+(\d+).*Total:\s+(\d+)", read(root / "build/d3a/gates/test.log"))
discovery = read(root / "build/d3a/gates/list-tests.log").split("The following Tests are available:", 1)[1]
discovered = sum(bool(line.strip()) for line in discovery.splitlines())
save = list(objects(read(root / "build/d3a/gates/save-contract.log")))[-1]
assert save["schema"] == 24
process_audit = json.loads(read(root / "build/e5a/process-audit-final.json"))
assert process_audit["CurrentCheckoutProcesses"] == 0
native_inputs = json.loads(read(root / "build/e5a/native-inputs-final.json"))
source_paths = [root / name for name in ("Core/FoodEligibilityImage.cs", "Core/LoadedStockEligibility.cs",
                "Runtime/LoadedCatalogueEligibilityReader.cs", "Runtime/LoadedCatalogueLifecycleMembers.cs")]
source_paths += sorted((root / "tools/e5a-eligibility-regression").glob("*.py"))
source_paths += sorted((root / "tools/e5a-eligibility-regression").glob("*.cs"))
source_paths += sorted((root / "tools/e5a-eligibility-regression").glob("*.csproj"))
receipt = dict(nativeEvidence=False, wholeFrameMeasured=False,
               sourceBase="034bb7629391ef0fedb61d7122b0d6ee3c451ff9",
               testedCodeHead=subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
               outer=outer, d3a=d3a, d4=d4, unitCounts=test_counts, discovered=discovered,
               saveContract=save, final=final, before=before, exploratory=exploratory,
               nativeInputs=native_inputs, processAudit=process_audit,
               sourceHashes={p.relative_to(root).as_posix(): hashlib.sha256(read(p).encode()).hexdigest() for p in source_paths},
               finalSourceAudit=read(root / "build/e5a/final-source-audit.log"),
               parsedOfflineFlags=parsed_flags,
               logs=[dict(path=p.relative_to(root).as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in logs],
               dllSha256=hashlib.sha256((root / "build/PersistentCity/PersistentCity.dll").read_bytes()).hexdigest())
target = root / "docs/checks/e5a-offline-results.json"
target.write_text(json.dumps(receipt, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
print(json.dumps(dict(d3a=len(d3a), d4=len(d4), outer=len(outer), units=test_counts, discovered=discovered,
                      assertions=final["checks"], offlineFlags=len(parsed_flags), receipt=target.relative_to(root).as_posix())))
