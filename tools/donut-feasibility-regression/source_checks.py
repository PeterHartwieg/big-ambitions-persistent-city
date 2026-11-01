"""Isolation and byte identity checks; does not load any game or profile."""
from pathlib import Path
import argparse
import hashlib
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
readers = {ROOT / ("Runtime/" + name + ".cs") for name in
           ("DonutFeasibilityReader", "CupcakeFeasibilityReader", "DirectStockShowcaseReader")}
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
            for symbol in ("DonutFeasibility", "CupcakeFeasibility", "DirectStockShowcase", "DirectStockCandidate"):
                assert symbol not in source, str(path)

# Pins come from approved cupcake head 682aa77 and landed donut 3f7b885.
# Normalize CRLF; no feature-branch commit must survive a squash merge.
APPROVED_SHA256 = {
    "Runtime/DirectStockShowcaseReader.cs": "614616650dc0ce4db364790c6e2ca91ead009922fb31818b6ff41e6afe705540",
    "Runtime/DonutFeasibilityReader.cs": "df432206c1993f4ad50b0950b50ce5bde1a6e8e6c1b184461813774ee11fb2d1",
    "Runtime/CupcakeFeasibilityReader.cs": "971ea51552b7d4d7bd928d699476ef6b75e33935a001c4a7edd81401a0c71429",
    "tools/donut-feasibility-regression/Program.cs": "6cb8277b432e45f750882d28693837a5a2fe52811a913ceaf520059b96779962",
    "tools/donut-feasibility-regression/DonutFeasibilityRegression.csproj": "ed5a586c37d8c76a752a3f057d5b1a1f9520cca9fdb6cf5bfeb897c9c126a15b",
    "tools/cupcake-feasibility-regression/CupcakeFeasibilityRegression.csproj": "241d75ac9b97dce8bf72bcf546bb1262eb293973c21fc801a516d2154d724fab",
    "tools/cupcake-feasibility-regression/approved-donut-reader.cs.txt": "880d062dcbf9a21af4168ac44b25fd1bfc91311830058a26356c4aeca4b30cf1"
}
for path, expected in APPROVED_SHA256.items():
    current = hashlib.sha256((ROOT / path).read_bytes().replace(b"\r\n", b"\n")).hexdigest()
    assert current == expected, f"Approved E3 source/harness changed: {path}"
print("PASS isolation and history-independent approved cupcake/shared-reader/donut fixture pins")

# Preserve the landed donut branch/refusal body and every original test case.
original = (ROOT / "tools/cupcake-feasibility-regression/approved-donut-reader.cs.txt").read_text(encoding="utf-8")
shared = (ROOT / "Runtime/DirectStockShowcaseReader.cs").read_text(encoding="utf-8")
old_body = original[original.index("            if (AuthoredTags == null)"):]
new_body = shared[shared.index("            if (AuthoredTags == null)"):]
assert new_body.replace("new DirectStockFields(", "new DonutFeasibilitySnapshot(") == old_body
current_tests = (ROOT / "tools/donut-feasibility-regression/Program.cs").read_text(encoding="utf-8")
current_tests = re.sub(r"#if CUPCAKE\n.*?#endif\n", "", current_tests, flags=re.S)
original_tests = current_tests.replace("CandidateReader", "DonutFeasibilityReader")
assert hashlib.sha256(original_tests.encode()).hexdigest() == "32da8b5d093099da5616ee97d7fda3032eb7f92bc3bef22038853e8d145d21d0"
project = (ROOT / "tools/donut-feasibility-regression/DonutFeasibilityRegression.csproj").read_text(encoding="utf-8")
project = project.replace('    <Compile Include="../../Runtime/DirectStockShowcaseReader.cs" Link="DirectStockShowcaseReader.cs" />\n', '')
assert hashlib.sha256(project.encode()).hexdigest() == "d92a8ceffcba3d1848b782b48e0251a165904f4362f2fcc131a74cf75c07afd6"
print("PASS landed donut read body/refusals, original cases and project after mechanical shared-reader substitutions")

# Whole-tree preservation is a per-sync audit against the actual incoming head.
# Keep it explicit so the standing isolation check does not freeze other slices.
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--audit-base", help="incoming development commit for this sync's whole-tree audit")
args = parser.parse_args()
if args.audit_base:
    tree = subprocess.check_output(["git", "ls-tree", "-rz", args.audit_base], cwd=ROOT)
    protected = []
    for entry in tree.rstrip(b"\0").split(b"\0"):
        metadata, raw_path = entry.split(b"\t", 1)
        path = raw_path.decode("utf-8")
        if path.startswith(("Core/", "Persistence/", "Runtime/", "tests/", "Compatibility/", "tools/", "config/")):
            if path == "Runtime/DonutFeasibilityReader.cs" or path.startswith("tools/donut-feasibility-regression/"):
                continue  # Approved shared-reader composition is pinned and compared above.
            protected.append((path, metadata.split()[2].decode()))
    # One Git process preserves .gitattributes/CRLF filtering without hundreds of child processes.
    actual = subprocess.check_output(["git", "hash-object", "--stdin-paths"], cwd=ROOT,
                                     input="".join(path + "\n" for path, _ in protected), text=True).splitlines()
    assert len(actual) == len(protected)
    for (path, expected), current in zip(protected, actual):
        assert current == expected, f"Incoming source/contract changed: {path}"
    print(f"PASS whole-tree preservation: {len(protected)} incoming source/test/tool/config blobs match {args.audit_base}")
