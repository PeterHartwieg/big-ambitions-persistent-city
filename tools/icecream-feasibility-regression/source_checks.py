"""Pinned icecream probe/cases, reader isolation and unchanged three-product contracts."""
from pathlib import Path
import hashlib
import runpy

ROOT = Path(__file__).resolve().parents[2]
runpy.run_path(str(ROOT / "tools/donut-feasibility-regression/source_checks.py"), run_name="__main__")
PINS = {'Runtime/DirectStockShowcaseReader.cs': '41056ff2b361af9a3b5a9befa1081757a4be8fc640a0d1b0f1df949ee0e7c19c',
 'Runtime/IcecreamFeasibilityReader.cs': 'e2e52931c751a0d728cbd0af3d17e932cdd92aaeca6405121452778b925fcf4e',
 'tools/icecream-feasibility-regression/IcecreamFeasibilityRegression.csproj': 'ba8f10d8a85dd978e631dbe6797bf02616b0e171dc243a57e9676c35a341a79b',
 'tools/icecream-feasibility-regression/Program.cs': '8d1e2ec8792eee607cdc337d25a29cfef1bee9be29e95c6f1b26e7fb367ed582',
 'tools/icecream-feasibility-regression/generate_cases.py': '4b3db341d8bfd6e97a9ebbee6adba1748cb7c97c76c9a5e750f437f03f2d45c0',
 'tools/icecream-feasibility-regression/measure_baseline.py': '6961c4560cf0ecb9f9ece5a326e704e3d26c9115349296499ad4d05823c37237'}
for path, expected in PINS.items():
    assert hashlib.sha256((ROOT / path).read_text(encoding="utf-8").encode()).hexdigest() == expected, path
allowed = {ROOT / ("Runtime/" + name + ".cs") for name in
           ("DirectStockShowcaseReader", "DonutFeasibilityReader", "CupcakeFeasibilityReader",
            "SaladFeasibilityReader", "IcecreamFeasibilityReader")}
for directory in ("Core", "Runtime", "Persistence", "Compatibility"):
    for path in (ROOT / directory).rglob("*.cs"):
        if "obj" in path.parts or "bin" in path.parts or path in allowed:
            continue
        source = path.read_text(encoding="utf-8-sig")
        for symbol in ("IcecreamFeasibility", "DirectStockShowcase", "DirectStockCandidate"):
            assert symbol not in source, path
facade = (ROOT / "Runtime/IcecreamFeasibilityReader.cs").read_text(encoding="utf-8")
assert facade == (ROOT / "Runtime/SaladFeasibilityReader.cs").read_text(encoding="utf-8").replace("Salad", "Icecream").replace("itemname_salad", "itemname_icecream")
print("PASS icecream SHA-256 pins, conditional facade, no hooks/polls/gameplay caller; old read branches and cases unchanged")
