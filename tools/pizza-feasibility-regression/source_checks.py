"""Pizza pins/isolation; --audit-base explicitly checks incoming-tree preservation."""
from pathlib import Path
import hashlib
import runpy
import subprocess

ROOT = Path(__file__).resolve().parents[2]
prior = runpy.run_path(str(ROOT / "tools/donut-feasibility-regression/source_checks.py"), run_name="__main__")
PINS = {
    'Runtime/DirectStockShowcaseReader.cs': 'e5d0ccf51317a9e393db75a6f35f29ad37264a17c669109ef69054cb803cfa8e',
    'Runtime/PizzaFeasibilityReader.cs': '405c404b3bf5db5a53aaf3499b2cca0ead84b9c310cdbcfcd66dd660aa712031',
    'tools/pizza-feasibility-regression/PizzaFeasibilityRegression.csproj': 'aae90ea4187cb9def744ce5a3c8899abfa78472bab2d665b10e1eaa7bc3a718d',
    'tools/pizza-feasibility-regression/Program.cs': '973e0752fcc24b8b5699901aaeb1c409617ccbf102095ae5f9c4238dae710cca',
}
for path, expected in PINS.items():
    source = (ROOT / path).read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(source).hexdigest() == expected, path

print("PASS pizza source pins and no gameplay caller/poll/writer")
# Per-slice/sync audit only: standing isolation must not freeze unrelated later slices.
BASE = prior["args"].audit_base
if not BASE:
    raise SystemExit(0)
allowed = {"Runtime/DirectStockShowcaseReader.cs", "tools/donut-feasibility-regression/source_checks.py"}
tree = subprocess.check_output(["git", "ls-tree", "-rz", BASE], cwd=ROOT)
protected = []
for entry in tree.rstrip(b"\0").split(b"\0"):
    metadata, raw_path = entry.split(b"\t", 1)
    path = raw_path.decode()
    if path.startswith(("Core/", "Runtime/", "Persistence/", "Compatibility/", "tests/", "tools/", "config/")) and path not in allowed:
        protected.append((path, metadata.split()[2].decode()))
actual = subprocess.check_output(["git", "hash-object", "--stdin-paths"], cwd=ROOT,
    input="".join(path + "\n" for path, _ in protected), text=True).splitlines()
assert len(actual) == len(protected)
for (path, expected), current in zip(protected, actual):
    assert current == expected, "Incoming source/contract changed: " + path
print(f"PASS {len(protected)} unchanged incoming source/test/tool/config blobs against {BASE}")
