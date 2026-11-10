#!/usr/bin/env python3
"""Reference-run evidence and the cross-machine digest comparison (F9h; docs/plans/f9-city-acceptance.md, row F9h,
Decision 6 and section 3.10).

python tools/acceptance/evidence.py record --result build/acceptance/<runId>/acceptance.json [--evidence DIR]
python tools/acceptance/evidence.py compare [--evidence DIR]

`record` copies the small, path-free parts of one `full` run into `<evidence>/<machine>/` (machine = host OS and
architecture, for example `windows-x64`): `summary.json` (provenance, classes, summary, ledger state and every stage's
verdict, seconds, metrics and digests, plus the F6h run digests) and `f6h-household-bands.json` (the week-6 control seed
bands derived with F6's existing `derive_household_thresholds`, a hand-off to the F6 owner; acceptance.py is not edited).
Raw TRX, logs and harness output stay under the run's `{out}`.

`compare` reads every machine's `summary.json` and writes `<evidence>/comparison.json`: per simulation digest, the value
on each machine and `equal` or `platform-divergent`. Machines declared in `machines.json` without a record are `pending`
or `not-run` with their reason. Build digests (the DLL and the supplied game assemblies) are machine inputs and are not
compared. Records from different commits are compared when no simulation input (SIMULATION_INPUTS) differs between
the commits and the recorded settings are equal, so a Mac run at the head where the PC record merged compares with it;
otherwise the verdict is `different-source`. Python 3 standard library only.
"""
import argparse
import hashlib
import importlib.util
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
EVIDENCE = HERE / "evidence"
ACCEPTANCE_PY = ROOT / "tools/economy-interventions/acceptance.py"
SUMMARY_SCHEMA = "persistentcity-acceptance-evidence-1"
BANDS_SCHEMA = "persistentcity-f6h-household-bands-1"
COMPARISON_SCHEMA = "persistentcity-acceptance-comparison-1"
OS_NAMES = {"windows": "windows", "darwin": "macos", "linux": "linux"}
ARCH_NAMES = {"amd64": "x64", "x86_64": "x64", "arm64": "arm64", "aarch64": "arm64"}
NOT_COMPARED = {"build"}
# Paths whose content decides the simulation digests. Two records from different commits are compared only when no file
# under these paths differs between the commits and their settings match (a docs or evidence commit between them is fine).
SIMULATION_INPUTS = ["Core", "Persistence", "Runtime", "Compatibility", "config", "tests", "tools/economy-interventions",
                     "tools/movement-equivalence", "tools/acceptance/stages.json", "tools/acceptance/expectations",
                     "tools/acceptance/run.py", "tools/acceptance/economy_stage.py"]


def machine_key(host):
    os_name = str(host.get("os", "unknown")).lower()
    arch = str(host.get("architecture", "unknown")).lower()
    return f"{OS_NAMES.get(os_name, os_name)}-{ARCH_NAMES.get(arch, arch)}"


def sha256_file(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def f6h_module():
    """F6's acceptance.py, imported read-only for its derive_household_thresholds."""
    spec = importlib.util.spec_from_file_location("f6h_acceptance", ACCEPTANCE_PY)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def household_bands(f6h, result):
    """The F6h hand-off: control seed bands at the recorded weight, derived with the existing function."""
    module = f6h_module()
    household = f6h["householdControlSeedBands"]
    derived = module.derive_household_thresholds(household)
    week6 = {name: {k: household[name][5][k] for k in ("mean", "sd", "low", "high", "min", "max", "perSeed")}
             for name in ("arrearsShare", "unfedOwnerDays", "p10Dollars")}
    return {"schema": BANDS_SCHEMA, "for": "F6 owner (tools/economy-interventions/acceptance.py HOUSEHOLD_THRESHOLDS)",
            "adopted": False, "runId": result["runId"], "commit": result["provenance"]["source"]["commit"],
            "machine": machine_key(result["provenance"]["host"]),
            "priceWeight": (f6h.get("defaults") or {}).get("priceWeight"),
            "householdWeight": result["provenance"]["settings"]["compiledDefaults"].get("FoodChoiceHouseholdWeight"),
            "runs": f6h.get("runs"), "derivedWith": "acceptance.derive_household_thresholds",
            "derived": derived, "currentThresholds": module.HOUSEHOLD_THRESHOLDS,
            "currentThresholdsMeasuredAt": "FoodChoicePriceWeight 1",
            "withinCurrentThresholdsPerSeed": household.get("withinThresholdsPerSeed"),
            "week6ControlBands": week6}


def stage_summary(stage, f6h):
    keep = {k: stage.get(k) for k in ("id", "class", "required", "exit", "seconds", "verdict", "reason", "metrics", "digests")}
    for key in ("routed", "deviations", "notes", "reusedFrom"):
        if key in stage:
            keep[key] = stage[key]
    keep["evidence"] = [{k: e.get(k) for k in ("path", "sha256", "bytes")} for e in stage.get("evidence", [])]
    if stage["id"] == "economy" and f6h is not None:
        keep["f6hRuns"] = f6h.get("metricsSha256", {})
    return keep


def record(result_path, evidence=EVIDENCE):
    result_path = Path(result_path)
    result = json.loads(result_path.read_text(encoding="utf-8"))
    out = result_path.parent
    f6h_path = out / "f6h" / "acceptance.json"
    f6h = json.loads(f6h_path.read_text(encoding="utf-8")) if f6h_path.is_file() else None
    key = machine_key(result["provenance"]["host"])
    target = Path(evidence) / key
    target.mkdir(parents=True, exist_ok=True)
    native = result.get("nativeEvidence") or {}
    summary = {"schema": SUMMARY_SCHEMA, "machine": key, "runId": result["runId"], "profile": result["profile"],
               "startedUtc": result.get("startedUtc"), "finishedUtc": result.get("finishedUtc"),
               "acceptanceSha256": sha256_file(result_path), "provenance": result["provenance"],
               "summary": result["summary"], "classes": result["classes"],
               "nativeEvidence": {k: native.get(k) for k in ("status", "head", "fingerprint", "batchDue", "batchDueReasons",
                                                             "stale", "unproven", "requiredChecks", "ledgerSha256")},
               "stages": [stage_summary(s, f6h) for s in result["stages"]]}
    written = [target / "summary.json"]
    written[0].write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    if f6h is not None and "householdControlSeedBands" in f6h:
        bands = household_bands(f6h, result)
        written.append(target / "f6h-household-bands.json")
        written[1].write_text(json.dumps(bands, indent=2) + "\n", encoding="utf-8")
    return written


def simulation_digests(summary):
    digests = {}
    for stage in summary["stages"]:
        if stage["id"] in NOT_COMPARED:
            continue
        for name, value in (stage.get("digests") or {}).items():
            digests[f"{stage['id']}/{name}"] = value
        for name, value in (stage.get("f6hRuns") or {}).items():
            digests[f"economy/f6h/{name}"] = value
    return digests


def changed_inputs(base, head, repo=ROOT):
    """Simulation input files that differ between two commits, or None when git cannot compare them."""
    done = subprocess.run(["git", "diff", "--name-only", base, head, "--", *SIMULATION_INPUTS], cwd=repo,
                          capture_output=True, text=True, check=False)
    return [line for line in done.stdout.splitlines() if line] if done.returncode == 0 else None


def source_problem(summaries, recorded, repo):
    """None when every recorded machine ran the same simulation inputs, else the reason they are not comparable."""
    commits = sorted({summaries[k]["provenance"]["source"]["commit"] for k in recorded})
    settings = {json.dumps(summaries[k]["provenance"].get("settings"), sort_keys=True) for k in recorded}
    if len(settings) > 1:
        return "recorded machines ran different settings; digests are not comparable"
    sources = {(summaries[k]["provenance"]["source"].get("commit"), summaries[k]["provenance"]["source"].get("diffSha256"))
               for k in recorded}
    if any(summaries[k]["provenance"]["source"].get("dirty") for k in recorded) and len(sources) > 1:
        return "a record was taken from a dirty tree and the others do not share its commit and diff; digests are not comparable"
    for commit in commits[1:]:
        changed = changed_inputs(commits[0], commit, repo)
        if changed is None:
            return f"git cannot compare {commits[0][:12]} and {commit[:12]}; digests are not comparable"
        if changed:
            return (f"simulation inputs differ between {commits[0][:12]} and {commit[:12]} "
                    f"({len(changed)} files, first {changed[0]}); digests are not comparable")
    return None


def compare(evidence=EVIDENCE, repo=ROOT):
    evidence = Path(evidence)
    declared = json.loads((evidence / "machines.json").read_text(encoding="utf-8"))["machines"]
    summaries = {p.parent.name: json.loads(p.read_text(encoding="utf-8")) for p in sorted(evidence.glob("*/summary.json"))}
    machines = []
    for entry in declared:
        found = summaries.get(entry["key"])
        row = {"key": entry["key"], "label": entry["label"]}
        if found:
            row.update(status="recorded", runId=found["runId"], commit=found["provenance"]["source"]["commit"],
                       verdict=found["summary"]["verdict"], semantic=found["classes"]["semantic"])
        else:
            row.update(status=entry["status"], reason=entry["reason"])
        machines.append(row)
    for key in sorted(set(summaries) - {m["key"] for m in declared}):
        found = summaries[key]
        machines.append({"key": key, "label": key, "status": "recorded", "runId": found["runId"],
                         "commit": found["provenance"]["source"]["commit"], "verdict": found["summary"]["verdict"],
                         "semantic": found["classes"]["semantic"]})
    recorded = [m["key"] for m in machines if m["status"] == "recorded"]
    commits = {m["commit"] for m in machines if m["status"] == "recorded"}
    digests = {key: simulation_digests(summaries[key]) for key in recorded}
    rows, first, missing = [], None, []
    for name in sorted(set().union(*digests.values()) if digests else []):
        values = {key: digests[key].get(name) for key in recorded}
        present = {v for v in values.values() if v is not None}
        if len(recorded) < 2:
            status = "single"
        elif None in values.values():
            status = "missing"
            missing.append(name)
        elif len(present) == 1:
            status = "equal"
        else:
            status = "platform-divergent"
            first = first or name
        rows.append({"name": name, "values": values, "status": status})
    problem = source_problem(summaries, recorded, repo) if len(recorded) > 1 else None
    if problem:
        verdict, reason = "different-source", problem
    elif len(recorded) < 2:
        verdict, reason = "pending", "one machine recorded; the others are pending or not run"
    elif first:
        verdict, reason = "platform-divergent", f"first differing digest: {first}"
    elif missing:
        verdict, reason = "incomplete", f"{len(missing)} digests recorded on only some machines, first {missing[0]}"
    else:
        verdict, reason = "cross-machine-deterministic", f"{len(rows)} digests equal on {', '.join(recorded)}"
    doc = {"schema": COMPARISON_SCHEMA, "verdict": verdict, "reason": reason, "firstDifference": first,
           "commits": sorted(commits), "simulationInputs": SIMULATION_INPUTS, "machines": machines, "digests": rows}
    (evidence / "comparison.json").write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    return doc


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=["record", "compare"])
    parser.add_argument("--result", help="record: a full run's acceptance.json")
    parser.add_argument("--evidence", default=str(EVIDENCE))
    args = parser.parse_args(argv)
    if args.command == "record":
        if not args.result:
            parser.error("record needs --result")
        for path in record(args.result, args.evidence):
            print("wrote", path.relative_to(Path(args.evidence).resolve().parent).as_posix()
                  if path.resolve().is_relative_to(Path(args.evidence).resolve().parent) else path.name)
        return 0
    doc = compare(args.evidence)
    print(f"comparison: {doc['verdict']} ({doc['reason']})")
    for m in doc["machines"]:
        print(f"  {m['key']:14} {m['status']:9} {m.get('runId') or m.get('reason', '')}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
