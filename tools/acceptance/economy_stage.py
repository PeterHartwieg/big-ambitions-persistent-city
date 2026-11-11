"""F9a economy stage adapter (docs/plans/f9-city-acceptance.md, Decision 2, "Economy stage adapter").

Reads the JSON that tests/economy-interventions/acceptance.py writes and turns it into one stage verdict.
acceptance.py belongs to F6 and is not changed; its exit code is ignored because it exits 1 on every
complete sweep (it always lists unmeasured criteria). Standard library only.

python tools/acceptance/economy_stage.py <acceptance.json> --compiled-weight 1.2 [--declared-weight W] [--output stage.json]
"""
import argparse
import json
from pathlib import Path

# acceptance.py failedCriteria text for the household-threshold gate (acceptance.py `report`).
HOUSEHOLD_FAILURE = "control household week-6 metrics outside the rerun thresholds"
HOUSEHOLD_DEVIATION = "household thresholds measured at weight 1"
PRICE_DECISION = "docs/decisions/2026-11-08-price-weight.md"

# Every unmeasuredCriteria entry acceptance.py emits today, and where the adapter routes it.
ROUTES = {
    "measured native wholesale costs": {
        "class": "native", "row": "native-wholesale-costs", "verdict": "incomplete",
        "proof": "pending rig capability RIG-WHOLESALE (native wholesale unit cost per product of the FT-F9 shop)"},
    "native staffed-throughput calibration": {
        "class": "native", "row": "native-staffed-throughput", "verdict": "incomplete",
        "proof": "F6b rig check 2, native staffed throughput (docs/checks/f6b.md)"},
    "full section 6 perf and 100000-consumer stress": {
        "class": "performance", "owner": "F4"},
    "F1 cadence byte identity": {
        "class": "semantic", "stages": ["population", "city-s", "city-m"]},
    "F2 body-cap flagged differences": {
        "class": "semantic", "stages": ["movement"]},
    "every gain has a measured cost / no Pareto dominant arm": {
        "class": "superseded", "link": PRICE_DECISION},
    # Only present with an incomplete matrix, which already fails the stage.
    "remaining intervention arms": {
        "class": "semantic", "stages": ["economy"], "note": "completeInterventionMatrix is false"},
}

ORDER = {"pass": 0, "incomplete": 1, "fail": 2}


def _worse(a, b):
    return a if ORDER[a] >= ORDER[b] else b


def assess(data, harness_exit=0, acceptance_exit=None, compiled_weight=1.2, declared_weight=None):
    """Return {verdict, reason, routed, deviations, metrics} for one economy stage.

    data is the parsed acceptance.json, or None when acceptance.py wrote none."""
    deviations = []
    reasons = []
    verdict = "pass"
    if declared_weight is not None:
        deviations.append(f"economy: --price-weight {declared_weight:g} declared; compiled default is {compiled_weight:g}")
        verdict = "incomplete"
        reasons.append("declared price-weight override")
    if harness_exit != 0:
        return {"verdict": "fail", "reason": f"economy harness exited {harness_exit}", "routed": [], "deviations": deviations, "metrics": {}}
    if data is None:
        return {"verdict": "fail", "routed": [], "deviations": deviations, "metrics": {},
                "reason": f"acceptance.py exited {acceptance_exit} and wrote no acceptance.json "
                          "(an accounting assertion aborted the report: conservation, receipts, clock, epoch or ledger identity)"}
    metrics = {"runs": data.get("runs"), "dailyConservationChecks": data.get("dailyConservationChecks"),
               "completeInterventionMatrix": data.get("completeInterventionMatrix"),
               "acceptanceStatus": data.get("status"), "acceptanceExit": acceptance_exit,
               "failedCriteria": list(data.get("failedCriteria", [])),
               "unmeasuredCriteria": list(data.get("unmeasuredCriteria", []))}

    recorded = (data.get("defaults") or {}).get("priceWeight")
    metrics["recordedPriceWeight"] = recorded
    expected = compiled_weight if declared_weight is None else declared_weight
    if recorded is None or abs(float(recorded) - expected) > 1e-9:
        verdict = "fail"
        reasons.append(f"recorded priceWeight {recorded} is not the {'declared' if declared_weight is not None else 'compiled default'} {expected:g}")

    if data.get("completeInterventionMatrix") is not True:
        verdict = "fail"
        reasons.append("intervention matrix incomplete")

    functional = [f for f in metrics["failedCriteria"] if not f.startswith(HOUSEHOLD_FAILURE)]
    household = len(functional) != len(metrics["failedCriteria"])
    if functional:
        verdict = "fail"
        reasons.append("failed: " + "; ".join(functional))
    if household:
        verdict = _worse(verdict, "incomplete")
        deviations.append("economy: " + HOUSEHOLD_DEVIATION)
        reasons.append("household threshold miss only (" + HOUSEHOLD_DEVIATION + ")")

    routed = []
    for entry in metrics["unmeasuredCriteria"]:
        route = ROUTES.get(entry)
        if route is None:
            verdict = _worse(verdict, "incomplete")
            reasons.append("unrecognised unmeasured criterion: " + entry)
            routed.append({"criterion": entry, "class": "unrouted"})
        else:
            routed.append(dict(route, criterion=entry))
    reason = "; ".join(reasons) if reasons else "functional criteria pass; unmeasured criteria routed"
    return {"verdict": verdict, "reason": reason, "routed": routed, "deviations": deviations, "metrics": metrics}


def assess_file(path, **kwargs):
    path = Path(path)
    data = json.loads(path.read_text(encoding="utf-8")) if path.is_file() else None
    return assess(data, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("acceptance", type=Path)
    parser.add_argument("--compiled-weight", type=float, required=True)
    parser.add_argument("--declared-weight", type=float)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = assess_file(args.acceptance, compiled_weight=args.compiled_weight, declared_weight=args.declared_weight)
    text = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.write_text(text, encoding="utf-8")
    print(text, end="")
    raise SystemExit(1 if result["verdict"] == "fail" else 0)


if __name__ == "__main__":
    main()
