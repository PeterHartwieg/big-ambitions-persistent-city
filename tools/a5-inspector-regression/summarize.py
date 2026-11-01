"""Publish A5 aggregates; --sync-base validates schema 20 without replacing pre-E1 measurements."""
import hashlib
import json
from pathlib import Path
import platform
import statistics
import subprocess
import sys

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
if sys.argv[1:] == ["--sync-base"]:
    # E1 adds one regression project to A5's original 50-command foreground gate.
    assert len(results) == 51 and len({r["name"] for r in results}) == 51
    assert all(r["exit"] == 0 for r in results), "Full schema-20 gate incomplete"
    current = output("save-contract-regression.log")
    evidence = json.loads((ROOT / "docs/reviews/2026-11-01-e1-sync-latest-results.json").read_text(encoding="utf-8"))
    base_log = next(r["tail"] for r in evidence["commands"] if r["name"] == "save-contract-regression")
    base = json.loads(next(line for line in base_log.splitlines() if line.startswith("{")))
    stable = lambda value: {k: v for k, v in value.items() if k not in ("writeBytes", "readBytes")}
    assert current["schema"] == 20
    assert stable(current) == stable(base), "Schema-20 stable save contract differs from E1 base"
    historical = json.loads((ROOT / "docs/reviews/2026-11-01-a5-measurements.json").read_text(encoding="utf-8"))
    old = historical["saveContract"]
    assert current["weatherProjectionLength"] == old["length"]
    assert current["weatherProjectionDigest"] == old["digest"]
    assert current["economy"] == old["economy"] and current["refusals"] == old["refusals"]
    report = dict(
        date="2026-11-01", a5Head="f3eb16b", mergedBase="5877453",
        boundary="Offline foreground correctness gate only. Historical A5 timings remain pre-E1; no native performance or rig proof.",
        unitSummary=next(line for line in (LOG / "unit.log").read_text(encoding="utf-8").splitlines() if "Passed!" in line),
        productionDllSha256=hashlib.sha256((ROOT / "build/PersistentCity/PersistentCity.dll").read_bytes()).hexdigest(),
        gate=[dict(r, log=str((LOG / (r["name"] + ".log")).relative_to(ROOT)),
                   logSha256=hashlib.sha256((LOG / (r["name"] + ".log")).read_bytes()).hexdigest()) for r in results],
        schema20StableContractMatchesE1=True, schema19ProjectionAndEconomyMatchHistoricalA5=True,
        saveContract=current,
        historicalMeasurements="docs/reviews/2026-11-01-a5-measurements.json",
    )
    target = ROOT / "docs/reviews/2026-11-01-a5-sync-base-results.json"
    target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(report["unitSummary"])
    print("PASS schema-20 E1 ledger and historical schema-19 projection; wrote " + str(target.relative_to(ROOT)))
    sys.exit(0)

assert not sys.argv[1:], "Usage: summarize.py [--sync-base]"
# Original schema-19 report generator: requires the original logs and before images.
# The sync mode above writes a separate file and never relabels historical timings.
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
