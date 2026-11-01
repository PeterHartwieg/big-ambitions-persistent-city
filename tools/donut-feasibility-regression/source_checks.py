"""Isolation and byte identity checks; does not load any game or profile."""
from pathlib import Path
import hashlib
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
# The second sync imports approved A3/B0/C3/E2c and documentation changes.
# Pin the incoming tree; independently retain the approved E3 reader below.
BASE = "b17e4a919e7a5499ffa22730752d63addbf3e082"
E3_BASE = "462a99d8313968a2a1760ac88806b3aed47a0003"
reader = ROOT / "Runtime/DonutFeasibilityReader.cs"
text = reader.read_text(encoding="utf-8")
code = re.sub(r"//[^\n]*|/\*.*?\*/", "", text, flags=re.S)
for forbidden in ("Harmony", "SetUp(", "ProcessCustomer", "CustomerEntry", "OrderEntry",
                  "SaveGameManager", "ItemCached", "GetStockInstance", "GetBuildingRegistration",
                  "CreateInstance", "Task.", "Thread", "Update(", "Timer", "FoodSupplyReader", ".HasTag(", "TagRef.", ".tagIndexes", "SetValue", ".Invoke("):
    assert forbidden not in code, forbidden
assert code.count('GetField("tags",') == 1 and code.count('.GetValue(') == 1
assert not re.search(r"\b(?:registration|instance|item|resource|cargo|type|product|entry)\.\w+\s*=(?!=)", code)
for directory in ("Core", "Runtime", "Persistence", "Compatibility"):
    for path in (ROOT / directory).glob("*.cs"):
        if path != reader:
            assert "DonutFeasibility" not in path.read_text(encoding="utf-8-sig"), str(path)

paths = subprocess.check_output(["git", "ls-tree", "-r", "--name-only", BASE], cwd=ROOT, text=True).splitlines()
preserved = []
for path in paths:
    if path.startswith(("Core/", "Persistence/", "Runtime/", "tests/", "Compatibility/",
                        "tools/food-catalogue-regression/", "tools/food-supply-regression/",
                        "tools/food-checkout-regression/", "tools/save-contract-regression/")):
        original = subprocess.check_output(["git", "show", f"{BASE}:{path}"], cwd=ROOT)
        # Compare Git blobs to avoid checkout CRLF normalization obscuring byte identity.
        current = subprocess.check_output(["git", "hash-object", path], cwd=ROOT, text=True).strip()
        expected = hashlib.sha1(f"blob {len(original)}\0".encode() + original).hexdigest()
        assert current == expected, f"Existing source/contract changed: {path}"
        preserved.append(path)
print(f"PASS isolation/no native field assignments; {len(preserved)} existing blobs byte-identical to {BASE}")

# The imported base has no donut reader: independently retain E3's approved
# runtime and harness blobs, so updating BASE cannot hide a reader change.
for path in ("Runtime/DonutFeasibilityReader.cs",
             "tools/donut-feasibility-regression/Program.cs",
             "tools/donut-feasibility-regression/DonutFeasibilityRegression.csproj"):
    expected = subprocess.check_output(["git", "rev-parse", f"{E3_BASE}:{path}"], cwd=ROOT, text=True).strip()
    current = subprocess.check_output(["git", "hash-object", path], cwd=ROOT, text=True).strip()
    assert current == expected, f"Approved E3 source/harness changed: {path}"
print(f"PASS approved E3 reader and harness blobs byte-identical to {E3_BASE}")
