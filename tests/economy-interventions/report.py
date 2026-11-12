import json
import sys
from pathlib import Path


def main(out: Path) -> None:
    summary = json.loads((out / "baseline-summary.json").read_text(encoding="utf-8"))
    cents = summary["unitsPerCent"]
    print("| Arm | Own purchases/day, weeks 1-2 / 3-4 / 5-6 (seed mean) | Outside control band (weeks 3-4) | Revenue $/week, week 3 | Rival meals/week, week 3 | Recovery days per seed | Residual after 14 d |")
    print("| --- | --- | --- | ---: | ---: | --- | ---: |")
    for arm in summary["arms"]:
        own = arm["weekly"]["ownPurchases"]["mean"]
        per_day = [(own[0] + own[1]) / 14, (own[2] + own[3]) / 14, (own[4] + own[5]) / 14]
        revenue = arm["weekly"]["ownRevenueUnits"]["mean"][2] / cents / 100
        rival = arm["weekly"]["rivalMeals"]["mean"][2]
        rec = arm.get("recovery")
        days = ", ".join("n/a" if d is None else str(d) for d in rec["daysPerSeed"]) if rec else "-"
        residual = f'{rec["residualAfter14DaysMean"]:+.1f}' if rec else "-"
        print(f'| {arm["family"]}/{arm["arm"]} | {per_day[0]:.1f} / {per_day[1]:.1f} / {per_day[2]:.1f} | '
              f'{"yes" if arm["effect"]["outsideControlBand"] else "no"} | {revenue:,.0f} | {rival:,.0f} | {days} | {residual} |')
    print()
    for arm in summary["arms"]:
        if "arcElasticity" in arm:
            e = arm["arcElasticity"]
            print(f'- {arm["arm"]}: arc elasticity ({e["quantity"]}) mean {e["mean"]}, per seed {e["perSeed"]}')
    print()
    print("| Arm | Missed activities/week (w1-2 / w3-4 / w5-6) | Job losses+finds (6 w) | Income $/week, week 3 | Arrears owners end of week 6 | p10 balance $ end w1 / w6 | Unfed days (6 w) |")
    print("| --- | --- | ---: | ---: | ---: | --- | ---: |")
    for arm in summary["arms"]:
        w = arm["weekly"]
        missed = w["missedActivities"]["mean"]
        jobs = sum(w["jobLosses"]["mean"]) + sum(w["jobFinds"]["mean"])
        income = w["incomeReceivedUnits"]["mean"][2] / cents / 100
        p10 = w["p10BalanceUnitsEndOfWeek"]["mean"]
        print(f'| {arm["family"]}/{arm["arm"]} | {(missed[0] + missed[1]) / 2:.1f} / {(missed[2] + missed[3]) / 2:.1f} / {(missed[4] + missed[5]) / 2:.1f} | '
              f'{jobs:.1f} | {income:,.0f} | {w["arrearsOwnersEndOfWeek"]["mean"][5]:.1f} | {p10[0] / cents / 100:,.0f} / {p10[5] / cents / 100:,.0f} | {sum(w["unfedDays"]["mean"]):.1f} |')
    print()
    print("household drift:", summary["householdDrift"])
    checks = sum(a["accounting"]["conservationChecks"] for a in summary["arms"])
    print("conservation checks:", checks, "duplicates:", sum(a["accounting"]["duplicateReceipts"] for a in summary["arms"]),
          "historical misses:", sum(a["accounting"]["historicalClockMisses"] for a in summary["arms"]))
    curve = out / "rival-curve.json"
    if curve.exists():
        c = json.loads(curve.read_text(encoding="utf-8"))
        print()
        print(f'| Residents / consumers | Own-only served/day | Own peak hour | Rival-only served/day | Rival peak hour |  (own staffed ceiling {c["ownStaffedCeilingPerHour"]}/h, {c["ownStaffedCeilingPerDay"]}/day)')
        print("| --- | ---: | ---: | ---: | ---: |")
        for p in c["points"]:
            print(f'| {p["residents"]:,} / {p["consumers"]:,} | {p["ownServedPerDay"]:,.1f} | {p["ownPeakHour"]} | {p["rivalServedPerDay"]:,.1f} | {p["rivalPeakHour"]} |')
    perf = out / "perf.json"
    if perf.exists():
        p = json.loads(perf.read_text(encoding="utf-8"))
        print()
        for s in p["sizes"]:
            print(f'- {s["residents"]} residents / {s["consumers"]} consumers: {s["secondsPerWeekMedian"]} s/week median (max {s["secondsPerWeekMax"]}), '
                  f'{s["allocatedBytesPerWeekMedian"]:,} B/week median (max {s["allocatedBytesPerWeekMax"]:,}), {s["epochsPerWeek"]} epochs')
        print(f'- six-week arm median {p.get("sixWeekArmSecondsMedian")} s, {p.get("sixWeekArmAllocatedBytesMedian"):,} B')


if __name__ == "__main__":
    main(Path(sys.argv[1] if len(sys.argv) > 1 else "build/f6a-baseline"))
