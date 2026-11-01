"""Publish sanitized A5 aggregates and verify the unchanged integration save contract."""
import hashlib
import json
from pathlib import Path
import platform
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LOG = ROOT / "build/a5-gate"


def output(name):
    data = (LOG / name).read_bytes()
    text = data.decode("utf-16" if data.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig")
    for line in text.splitlines():
        if line.startswith("{"):
            try:
                return json.loads(text[text.index(line):])
            except json.JSONDecodeError:
                pass
    raise ValueError("No JSON result: " + name)


results = json.loads((LOG / "results.json").read_text(encoding="utf-8"))
assert len(results) == 50 and all(r["exit"] == 0 for r in results), "Full gate incomplete"
before, after = output("save-before.json"), output("save-contract-regression.log")
allocation_keys = ("writeBytes", "readBytes")
assert {k: v for k, v in before.items() if k not in allocation_keys} == {k: v for k, v in after.items() if k not in allocation_keys}, "Stable save-contract output changed"


def rush(name):
    raw = output(name)
    summary = []
    for mode in sorted({r["mode"] for r in raw["results"]}):
        for accounting in sorted({r["frameAccounting"] for r in raw["results"]}):
            rows = [r for r in raw["results"] if r["mode"] == mode and r["frameAccounting"] == accounting]
            summary.append(dict(mode=mode, accounting=accounting,
                medianFrames=statistics.median(r["measuredFramesWith16Citizen128Unit2MsLoop"] for r in rows),
                p95LoopRangeMs=[min(r["syntheticLoopMs"]["p95"] for r in rows), max(r["syntheticLoopMs"]["p95"] for r in rows)],
                maxLoopMs=max(r["syntheticLoopMs"]["max"] for r in rows),
                flaggedLoops=sum(r["syntheticLoopMs"]["spikesAtLeast30Ms"] for r in rows),
                worstBacklogAtFastestClock=max(r["backlog"][-1]["pendingAtWindowEnd"] for r in rows)))
    return dict(raw=raw, summary=summary)


report = dict(
    date="2026-11-01", baseline="51ce709", productionCode="bcc3b00",
    boundary="Offline, guarded sequential foreground .NET 10; no native mod-work frames, whole-frame data, game or rig activity.",
    platform=platform.platform(),
    productionDllSha256=hashlib.sha256((ROOT / "build/PersistentCity/PersistentCity.dll").read_bytes()).hexdigest(),
    unitSummary=next(line for line in (LOG / "unit.log").read_text(encoding="utf-8").splitlines() if "Passed!" in line),
    gate=[dict(name=r["name"], exit=r["exit"], seconds=r["seconds"]) for r in results],
    saveContractStableOutputUnchanged=True, saveContract=after,
    allocationDiagnostics=dict(before={k: before[k] for k in allocation_keys}, after={k: after[k] for k in allocation_keys}),
    inspectorBefore=output("inspector-before.json"), inspectorInitialAfter=output("inspector-after.json"),
    inspectorCurrent=output("a5-inspector-regression.log"),
    a3Comparable=output("food-choice-regression.log"),
    activeMemoryAwareness=output("choice-integrated.log"),
    a3ComparableRush=rush("choice-rush.log"),
    activeMemoryAwarenessRush=rush("choice-rush-integrated.log"),
    activeMemoryAwarenessRush5000=rush("choice-rush-integrated-5000.log"),
    saveBench=output("save-bench.log"),
)
target = ROOT / "docs/reviews/2026-11-01-a5-measurements.json"
target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(report["unitSummary"])
print("PASS unchanged stable save-contract output (allocation counters reported separately); wrote " + str(target.relative_to(ROOT)))
