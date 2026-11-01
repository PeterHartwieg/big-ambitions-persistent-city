"""Frenchfries full-source pins plus unchanged approved E3 branches/cases and isolation.

Normalize CRLF; no pre-squash feature-branch history is required.
"""
from pathlib import Path
import hashlib
import re
import runpy

ROOT = Path(__file__).resolve().parents[2]
PINS = {'Runtime/DirectStockShowcaseReader.cs': '3a4b2a7c1df094c6fa973c3cc60b8117a0f32e73d9cff7c9b21a351b67ed2127', 'Runtime/FrenchFriesFeasibilityReader.cs': 'd1e9ed8b023dc9d2d848a1565af138e8d5f1618fb8402433b79dd57ed592e873', 'tools/frenchfries-feasibility-regression/Program.cs': '7b0940480af5afddaedc52d78e1cc8ad3a533732284e362a327d08c49ee02f9b', 'tools/frenchfries-feasibility-regression/FrenchFriesFeasibilityRegression.csproj': 'cd6d3ca634edb3428a458fede8e4d68939e19eef73e1155574f61dd57aea96e1'}
for path, expected in PINS.items():
    assert hashlib.sha256((ROOT / path).read_text(encoding="utf-8").encode()).hexdigest() == expected, path
runpy.run_path(str(ROOT / "tools/donut-feasibility-regression/source_checks.py"), run_name="__main__")

readers = {ROOT / "Runtime" / (name + ".cs") for name in (
    "DirectStockShowcaseReader", "DonutFeasibilityReader", "CupcakeFeasibilityReader",
    "SaladFeasibilityReader", "FrenchFriesFeasibilityReader")}
# Recursively cover future nested runtime/core sources as well as today's flat tree.
for folder in ("Core", "Runtime", "Persistence", "Compatibility"):
    for path in (ROOT / folder).rglob("*.cs"):
        if {"bin", "obj"}.intersection(path.parts) or path in readers:
            continue
        text = path.read_text(encoding="utf-8-sig")
        assert not re.search(r"FrenchFriesFeasibility|DirectStockCandidate|DirectStockShowcaseReader", text), path
facade = (ROOT / "Runtime/FrenchFriesFeasibilityReader.cs").read_text(encoding="utf-8")
assert facade.count("DirectStockShowcaseReader.Read(") == 1
assert "DirectStockCandidate.FrenchFries" in facade
assert "public const string Product = \"ba:itemname_frenchfries\";" in facade
assert len(re.findall(r"public static .* Read\(", facade)) == 1
assert "private DirectStockCandidate(string productId)" in (ROOT / "Runtime/DirectStockShowcaseReader.cs").read_text()
print("PASS pinned fries facade/cases; no gameplay caller, hooks, polls, writes or arbitrary product selector")
