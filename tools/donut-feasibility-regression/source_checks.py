"""Isolation and byte identity checks; does not load any game or profile."""
from pathlib import Path
import hashlib
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = "00d1321b257e70e57829cfef2ca823b4d0e25060"
reader = ROOT / "Runtime/DonutFeasibilityReader.cs"
text = reader.read_text(encoding="utf-8")
code = re.sub(r"//[^\n]*|/\*.*?\*/", "", text, flags=re.S)
for forbidden in ("Harmony", "Reflection", "SetUp(", "ProcessCustomer", "CustomerEntry", "OrderEntry",
                  "SaveGameManager", "ItemCached", "GetStockInstance", "GetBuildingRegistration",
                  "CreateInstance", "Task.", "Thread", "Update(", "Timer", "FoodSupplyReader"):
    assert forbidden not in code, forbidden
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
