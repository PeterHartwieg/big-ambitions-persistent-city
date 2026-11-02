"""Batch full pins, immutable historical pins, no gameplay reachability or native writes."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import runpy
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
PINS = json.loads((HERE / "source-pins.json").read_text())
history = runpy.run_path(str(HERE / "historical.py"))
readers = {"Runtime/DirectStockShowcaseReader.cs", "Runtime/RemainingBatchFeasibilityReaders.cs"}
readers.update(f"Runtime/{name}FeasibilityReader.cs" for name in ("Donut", "Cupcake", "Salad", "Icecream", "FrenchFries", "Pizza"))
names = ("Donut", "Cupcake", "Salad", "Icecream", "FrenchFries", "Pizza", "CheesePlatter", "CottonCandy", "Popcorn", "Kabob", "CandyCane", "ChristmasCookies")
symbols = tuple(n + "Feasibility" for n in names) + ("DirectStockShowcase", "DirectStockCandidate", "RemainingSeasonalStockProbe")


def scan(sources):
    for path, source in sources.items():
        code = re.sub(r"//[^\n]*|/\*.*?\*/", "", source, flags=re.S)
        if path not in readers:
            assert not any(s in code for s in symbols), "Gameplay caller: " + path
            continue
        for forbidden in ("Harmony", "SetUp(", "ProcessCustomer", "CustomerEntry", "OrderEntry", "SaveGameManager",
                          "ItemCached", "GetStockInstance", "GetBuildingRegistration", "CreateInstance", "Task.",
                          "Thread", "Update(", "Timer", "FoodSupplyReader", ".HasTag(", "TagRef.", ".tagIndexes",
                          "SetValue", ".Invoke(", "SeasonHelper", "PlayerPrefSettings", "GetItemNameBySeason",
                          "IsAvailableInCurrentSeason", "Addressables", "RegisterModItem", "OnItemsLoaded"):
            assert forbidden not in code, path + ": " + forbidden
        assert not re.search(r"\b(?:registration|instance|item|resource|cargo|type|product|entry)\.\w+\s*=(?!=)", code), path
    code = "\n".join(sources[p] for p in readers)
    assert code.count('GetField("tags",') == 1 and code.count(".GetValue(") == 1


sources = {p.relative_to(ROOT).as_posix(): p.read_text(encoding="utf-8-sig")
           for folder in ("Core", "Runtime", "Persistence", "Compatibility")
           for p in (ROOT / folder).rglob("*.cs") if not {"bin", "obj"}.intersection(p.parts)}
scan(sources)
for snippet in ("DonutFeasibilityReader.Read(null);", "CheesePlatterFeasibilityReader.Read(null);",
                "RemainingSeasonalStockProbe.Read(null, null);"):
    mutated = dict(sources, **{"Runtime/Nested/UnexpectedCaller.cs": snippet})
    try:
        scan(mutated)
    except AssertionError:
        pass
    else:
        raise AssertionError("Caller mutation survived: " + snippet)
for snippet in ("Harmony", "Update(", "product.type = 0;", "field.SetValue(product, 0);",
                "instance.ItemCached", "TagRef.Itemtag.isbag", "SeasonHelper.CurrentSeasonName",
                "PlayerPrefSettings.SeasonalDecorations"):
    mutated = dict(sources)
    mutated["Runtime/RemainingBatchFeasibilityReaders.cs"] += "\n" + snippet
    try:
        scan(mutated)
    except AssertionError:
        pass
    else:
        raise AssertionError("Reader mutation survived: " + snippet)
for path, expected in PINS.items():
    assert hashlib.sha256((ROOT / path).read_text(encoding="utf-8").encode()).hexdigest() == expected, path

# Every historical hash literal remains unchanged. The complete new guard/helper
# versions are pinned above, so only the enumerated exact batch insertions disappear.
for path in history["EDITS"]:
    original = subprocess.check_output(["git", "show", "a1b993a:" + path], cwd=ROOT).decode().replace("\r\n", "\n")
    assert history["source"](path) == original, "Historical content changed: " + path
    if path.endswith("source_checks.py"):
        assert re.findall(r"[a-f0-9]{64}", (ROOT / path).read_text()) == re.findall(r"[a-f0-9]{64}", original), path
for product in ("donut", "salad", "icecream", "frenchfries", "pizza"):
    subprocess.run([sys.executable, f"tools/{product}-feasibility-regression/source_checks.py"], cwd=ROOT, check=True)

args = argparse.ArgumentParser()
args.add_argument("--audit-base")
base = args.parse_args().audit_base
if base:
    entries = subprocess.check_output(["git", "ls-tree", "-rz", base], cwd=ROOT).rstrip(b"\0").split(b"\0")
    protected = []
    for entry in entries:
        metadata, raw_path = entry.split(b"\t", 1)
        path = raw_path.decode()
        if path.startswith(("Core/", "Runtime/", "Persistence/", "Compatibility/", "tests/", "tools/", "config/")) and path not in PINS:
            protected.append((path, metadata.split()[2].decode()))
    hashes = subprocess.check_output(["git", "hash-object", "--stdin-paths"], cwd=ROOT,
                                    input="".join(p + "\n" for p, _ in protected), text=True).splitlines()
    assert len(hashes) == len(protected)
    for (path, expected), actual in zip(protected, hashes):
        assert actual == expected, "Incoming contract changed: " + path
    print(f"PASS {len(protected)} unchanged incoming production/test/tool/config blobs against {base}")
print("PASS batch complete pins, 11 isolation mutations, exact historical additions only; no hooks/polls/writes/gameplay caller")
