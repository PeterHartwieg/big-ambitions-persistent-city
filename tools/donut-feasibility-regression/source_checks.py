"""Isolation and byte identity checks; does not load any game or profile."""
from pathlib import Path
import hashlib
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = "00d1321b257e70e57829cfef2ca823b4d0e25060"
readers = {ROOT / ("Runtime/" + name + ".cs") for name in
           ("DonutFeasibilityReader", "CupcakeFeasibilityReader", "SaladFeasibilityReader", "DirectStockShowcaseReader")}
text = "\n".join(path.read_text(encoding="utf-8") for path in sorted(readers))
code = re.sub(r"//[^\n]*|/\*.*?\*/", "", text, flags=re.S)
for forbidden in ("Harmony", "SetUp(", "ProcessCustomer", "CustomerEntry", "OrderEntry",
                  "SaveGameManager", "ItemCached", "GetStockInstance", "GetBuildingRegistration",
                  "CreateInstance", "Task.", "Thread", "Update(", "Timer", "FoodSupplyReader", ".HasTag(", "TagRef.", ".tagIndexes", "SetValue", ".Invoke("):
    assert forbidden not in code, forbidden
assert code.count('GetField("tags",') == 1 and code.count('.GetValue(') == 1
assert not re.search(r"\b(?:registration|instance|item|resource|cargo|type|product|entry)\.\w+\s*=(?!=)", code)
for directory in ("Core", "Runtime", "Persistence", "Compatibility"):
    for path in (ROOT / directory).glob("*.cs"):
        if path not in readers:
            source = path.read_text(encoding="utf-8-sig")
            for symbol in ("DonutFeasibility", "CupcakeFeasibility", "SaladFeasibility", "DirectStockShowcase", "DirectStockCandidate"):
                assert symbol not in source, str(path)

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

# Refactor preserves every original check/branch/message after explicit mechanical substitutions.
DONUT_HEAD = "462a99d"
original = subprocess.check_output(["git", "show", f"{DONUT_HEAD}:Runtime/DonutFeasibilityReader.cs"], cwd=ROOT, text=True)
shared = (ROOT / "Runtime/DirectStockShowcaseReader.cs").read_text()
old_body = original[original.index("            if (AuthoredTags == null)"):]
new_body = shared[shared.index("            if (AuthoredTags == null)"):]
assert new_body.replace("new DirectStockFields(", "new DonutFeasibilitySnapshot(") == old_body
original_tests = subprocess.check_output(["git", "show", f"{DONUT_HEAD}:tools/donut-feasibility-regression/Program.cs"], cwd=ROOT, text=True)
current_tests = (ROOT / "tools/donut-feasibility-regression/Program.cs").read_text()
current_tests = re.sub(r"#if CUPCAKE\n.*?#endif\n", "", current_tests, flags=re.S)
current_tests = re.sub(r"#if SALAD\n.*?#endif\n", "", current_tests, flags=re.S)
assert current_tests.replace("CandidateReader", "DonutFeasibilityReader") == original_tests
print("PASS original donut read body and cases unchanged after mechanical refactor/reader alias")
