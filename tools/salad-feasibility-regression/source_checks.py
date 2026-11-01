"""E3 salad isolation and exact preservation of approved donut/cupcake/salad contracts.

The shared guard pins full salad additions and the normalized landed cupcake/donut
contracts without feature-branch ancestry; --audit-base remains an explicit sync audit.
"""
from pathlib import Path
import runpy

ROOT = Path(__file__).resolve().parents[2]
runpy.run_path(str(ROOT / "tools/donut-feasibility-regression/source_checks.py"), run_name="__main__")
print("PASS approved salad additions and landed cupcake/donut shared implementation, facades and cases")
