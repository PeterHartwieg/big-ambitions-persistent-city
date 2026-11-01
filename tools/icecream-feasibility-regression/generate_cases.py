"""Reuse the unchanged, pinned native doubles/cases; generated source stays in obj."""
from pathlib import Path
import hashlib
import sys

ROOT = Path(__file__).resolve().parents[2]
source = (ROOT / "tools/donut-feasibility-regression/Program.cs").read_text(encoding="utf-8")
assert hashlib.sha256(source.encode()).hexdigest() == "707be922277d715ca17bbf1d38c05c5f608c53ce1c87ef8db545752b67c3f507"
source = source.replace("using CandidateReader = PersistentCity.Runtime.DonutFeasibilityReader;",
                        "using CandidateReader = PersistentCity.Runtime.IcecreamFeasibilityReader;")
source = source.replace("static class Program\n", "static partial class Program\n")
source = source.replace("        var timings = new List<object>();", "        IcecreamChecks(cold);\n        var timings = new List<object>();")
target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text("#nullable enable\n" + source, encoding="utf-8")
