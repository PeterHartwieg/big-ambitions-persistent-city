"""Isolation and byte identity checks; does not load any game or profile."""
from pathlib import Path
import re
import hashlib

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

# Pin the approved E3 implementation without requiring its feature-branch commits.
# A squash merge need not retain E3_BASE in the destination's history. Hashes below
# are SHA-256 of those approved blobs; normalize checkout CRLF to canonical LF.
# Whole-tree preservation is audited per merge, independently of this guard.
APPROVED_SHA256 = {
    "Runtime/DonutFeasibilityReader.cs": "880d062dcbf9a21af4168ac44b25fd1bfc91311830058a26356c4aeca4b30cf1",
    "tools/donut-feasibility-regression/Program.cs": "32da8b5d093099da5616ee97d7fda3032eb7f92bc3bef22038853e8d145d21d0",
    "tools/donut-feasibility-regression/DonutFeasibilityRegression.csproj": "d92a8ceffcba3d1848b782b48e0251a165904f4362f2fcc131a74cf75c07afd6",
}
for path, expected in APPROVED_SHA256.items():
    current = hashlib.sha256((ROOT / path).read_bytes().replace(b"\r\n", b"\n")).hexdigest()
    assert current == expected, f"Approved E3 source/harness changed: {path}"
print(f"PASS approved E3 reader and harness bytes match {E3_BASE} (history-independent pins)")
