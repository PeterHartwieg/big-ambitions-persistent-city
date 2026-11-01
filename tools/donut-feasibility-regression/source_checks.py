"""Isolation and byte identity checks; does not load any game or profile."""
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
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

print("PASS isolation/no native field assignments or gameplay callers")

# Only the approved E3 implementation is pinned in this standing regression.
# Whole-tree preservation is audited once per merge; unrelated development
# changes must not invalidate this reader's isolation checks.
for path in ("Runtime/DonutFeasibilityReader.cs",
             "tools/donut-feasibility-regression/Program.cs",
             "tools/donut-feasibility-regression/DonutFeasibilityRegression.csproj"):
    expected = subprocess.check_output(["git", "rev-parse", f"{E3_BASE}:{path}"], cwd=ROOT, text=True).strip()
    current = subprocess.check_output(["git", "hash-object", path], cwd=ROOT, text=True).strip()
    assert current == expected, f"Approved E3 source/harness changed: {path}"
print(f"PASS approved E3 reader and harness blobs byte-identical to {E3_BASE}")
