"""E3 salad isolation and exact preservation of approved donut/cupcake code/cases."""
from pathlib import Path
import re
import subprocess
import runpy

ROOT = Path(__file__).resolve().parents[2]
BASE = "682aa77"
runpy.run_path(str(ROOT / "tools/donut-feasibility-regression/source_checks.py"))

def original(path):
    return subprocess.check_output(["git", "show", f"{BASE}:{path}"], cwd=ROOT, text=True)

for name in ("Donut", "Cupcake"):
    path = f"Runtime/{name}FeasibilityReader.cs"
    assert (ROOT / path).read_text() == original(path), path
path = "Runtime/DirectStockShowcaseReader.cs"
shared = (ROOT / path).read_text()
shared = shared.replace('        internal static readonly DirectStockCandidate Salad = new DirectStockCandidate("ba:itemname_salad");\n', '')
shared = shared.replace('See the E3 per-product records;', 'See e3-donut.md and e3-cupcake.md;')
assert shared == original(path), "Only the pinned descriptor and documentation may change"
path = "tools/donut-feasibility-regression/Program.cs"
cases = (ROOT / path).read_text()
cases = cases.replace('#elif SALAD\nusing CandidateReader = PersistentCity.Runtime.SaladFeasibilityReader;\n', '')
cases = re.sub(r"#if SALAD\n.*?#endif\n", "", cases, flags=re.S)
assert cases == original(path), "Approved donut/cupcake cases must remain byte-for-byte equivalent"
print("PASS approved cupcake-head shared implementation, facades and donut/cupcake cases preserved exactly")
