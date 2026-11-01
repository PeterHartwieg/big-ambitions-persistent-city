"""Pinned icecream probe/cases, reader isolation and unchanged three-product contracts."""
from pathlib import Path
import hashlib
import re
import runpy

ROOT = Path(__file__).resolve().parents[2]
runpy.run_path(str(ROOT / "tools/donut-feasibility-regression/source_checks.py"), run_name="__main__")
PINS = {'Runtime/DirectStockShowcaseReader.cs': '41056ff2b361af9a3b5a9befa1081757a4be8fc640a0d1b0f1df949ee0e7c19c', 'Runtime/IcecreamFeasibilityReader.cs': 'e2e52931c751a0d728cbd0af3d17e932cdd92aaeca6405121452778b925fcf4e', 'tools/icecream-feasibility-regression/IcecreamFeasibilityRegression.csproj': 'cbee2cd2cdd9f3c0ed4c4721e5f9872c10cfd1ec3fd5e8854d5c8af76d6861a2', 'tools/icecream-feasibility-regression/Program.cs': '8d1e2ec8792eee607cdc337d25a29cfef1bee9be29e95c6f1b26e7fb367ed582', 'tools/icecream-feasibility-regression/generate_cases.py': 'b11665ae5e710d47355ad6503a89ab84c8b34c57327e6fc7cb765128635319cc'}
for path, expected in PINS.items():
    assert hashlib.sha256((ROOT / path).read_text(encoding="utf-8").encode()).hexdigest() == expected, path
allowed = {ROOT / "Runtime/DirectStockShowcaseReader.cs", ROOT / "Runtime/IcecreamFeasibilityReader.cs"}
for directory in ("Core", "Runtime", "Persistence", "Compatibility"):
    for path in (ROOT / directory).rglob("*.cs"):
        if "obj" in path.parts or "bin" in path.parts or path in allowed:
            continue
        assert "IcecreamFeasibility" not in path.read_text(encoding="utf-8-sig"), path
facade = (ROOT / "Runtime/IcecreamFeasibilityReader.cs").read_text(encoding="utf-8")
assert facade == (ROOT / "Runtime/SaladFeasibilityReader.cs").read_text(encoding="utf-8").replace("Salad", "Icecream").replace("itemname_salad", "itemname_icecream")
print("PASS icecream SHA-256 pins, conditional facade, no hooks/polls/gameplay caller; old read branches and cases unchanged")
