"""Pizza pins/isolation; --audit-base explicitly checks incoming-tree preservation."""
from pathlib import Path
import hashlib
import runpy
import subprocess

ROOT = Path(__file__).resolve().parents[2]
prior = runpy.run_path(str(ROOT / "tools/donut-feasibility-regression/source_checks.py"), run_name="__main__")
# Combined pizza/icecream reader; the icecream guard changes only its full-reader pin and facade allowlist.
PINS = {
    'tools/icecream-feasibility-regression/source_checks.py': '78334d24112652e986130716c1961d4a8d8782e2c046cdfa0c06063e75b81acf',
    'Runtime/DirectStockShowcaseReader.cs': '239650040cb06dde4f0d8030e659c906a5fe09c8b37ea7be9dc763162ddb880d',
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
allowed = {"Runtime/DirectStockShowcaseReader.cs", "tools/donut-feasibility-regression/source_checks.py",
           "tools/icecream-feasibility-regression/source_checks.py",
           "tools/pizza-feasibility-regression/source_checks.py",
           "tools/salad-feasibility-regression/compare_baseline.py"}  # Exact guard pin above.
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
