"""F6h acceptance report. Offline only; exits 1 on a failed or unmeasured gate.

python3 tools/economy-interventions/acceptance.py build/f6h --output build/f6h/acceptance.json
No guessed wholesale prices: a dominance certificate covers every fixed per-product
wholesale cost between zero and the baseline retail price. It can disprove acceptance,
but cannot certify measured native operating profit or replace the rig throughput read.
"""
import argparse
import hashlib
import json
import math
import statistics as st
from pathlib import Path

# Household control thresholds from the 8 Nov 2026 rerun after F6j extras (base ec186f97): canonical
# capacity-bound fixture, five seeds, week-6 control seed band (mean +/- 2 sd), rounded outward.
# Measured: arrears 0.18808% [high 0.46057%], unfed 1.8 owner-days [high 5.65], p10 $689.99 [low $659.00].
# Post-F6i rerun (3f734cb6): arrears <= 0.47%, unfed <= 5.2, p10 >= $656.
HOUSEHOLD_THRESHOLDS = {"week6ArrearsShareMax": 0.0047, "week6UnfedOwnerDaysMax": 5.7, "week6P10DollarsMin": 658.0}


def band(values):
    mean = st.mean(values)
    sd = st.stdev(values) if len(values) > 1 else 0
    return {"mean": mean, "sd": sd, "low": mean - 2 * sd, "high": mean + 2 * sd,
            "min": min(values), "max": max(values), "perSeed": values}


def inside(value, seed_band):
    return seed_band["low"] <= value <= seed_band["high"]


def trailing(run, day):
    return st.mean(d["own"]["purchases"] for d in run["days"][day - 3:day])


def recovery(run, controls):
    for day in range(31, 43):
        if inside(trailing(run, day), band([trailing(c, day) for c in controls])):
            return day - 28
    return None


def products(run, week):
    counts = {}
    revenue = 0
    for d in run["days"][week * 7:(week + 1) * 7]:
        revenue += d["own"]["revenueUnits"]
        for shop in d["own"]["shops"]:
            for p in shop["products"]:
                counts[p["productId"]] = counts.get(p["productId"], 0) + p["purchases"]
    return counts, revenue


def dominance_floor(high, other, week, base_units):
    """Minimum profit(high)-profit(other) over the entire stated wholesale box.

    Linear in each price, so the minimum occurs at an endpoint. Positive means
    the high arm wins for *every* cost in the box, including measured costs if in it.
    """
    a, ar = products(high, week)
    b, br = products(other, week)
    return ar - br - sum(max(0, a.get(p, 0) - b.get(p, 0)) * cost for p, cost in base_units.items())


def derive_household_thresholds(household):
    """Week-6 control seed band, rounded outward: arrears up to 4 dp, unfed up to 0.1, p10 down to $1."""
    return {"week6ArrearsShareMax": math.ceil(household["arrearsShare"][5]["high"] * 1e4) / 1e4,
            "week6UnfedOwnerDaysMax": math.ceil(household["unfedOwnerDays"][5]["high"] * 10) / 10,
            "week6P10DollarsMin": float(math.floor(household["p10Dollars"][5]["low"]))}


def within_household(arrears, unfed, p10, limits=None):
    limits = limits or HOUSEHOLD_THRESHOLDS
    return arrears <= limits["week6ArrearsShareMax"] and unfed <= limits["week6UnfedOwnerDaysMax"] and p10 >= limits["week6P10DollarsMin"]


def price_arms(groups, controls, base_units, units):
    """Price-arm table: per-seed quantity, revenue and arc elasticity against the matched control
    over both intervention weeks, plus whether own capacity bound at any hour and the share of
    evaluated own offers refused as unaffordable (wallet-exhaustion check)."""
    def fortnight(run):
        days = run["days"][14:28]
        shops = [s for d in days for s in d["own"]["shops"]]
        # F6j: shop purchases count items (main + extras). Meals are visits, from outcomes; capacity is
        # meals per hour, so the binding test reads meals by checkout hour (pre-F6j runs: byHour = meals).
        hours = "mealsByCheckoutHour" if all("mealsByCheckoutHour" in s for s in shops) else "byHour"
        meals = sum(s["meals"] for s in shops) if hours != "byHour" else sum(d["own"]["purchases"] for d in days)
        mains = {}
        for s in shops:
            for t in s.get("tickets", []):
                m = mains.setdefault(t["mainProductId"], [0, 0, 0])
                m[0] += t["meals"]; m[1] += t["extras"]; m[2] += t["plannedExtras"]
        return {"q": sum(d["own"]["purchases"] for d in days), "r": sum(d["own"]["revenueUnits"] for d in days), "mains": mains,
                "meals": meals, "home": sum(d["share"]["city"]["home"] for d in days),
                "peak": max(max(s[hours]) for s in shops),
                "refused": sum(s["offers"]["price"] for s in shops), "considered": sum(s["offers"]["considered"] for s in shops)}
    capacity = controls[0]["fixture"]["mealsPerHour"]
    table = {}
    for key in ["control/control"] + sorted(k for k in groups if k.startswith("price/")):
        rows = [fortnight(r) for r in groups[key]]
        base = [fortnight(c) for c in controls]
        menu = key == "price/menu-plus-50"
        change = 0.5 if menu else (0.25 if key.endswith("plus-25") else -0.25)
        arc_price = change / (1 + change / 2)
        entry = {
            "ownPurchasesPerDay": band([x["q"] / 14 for x in rows]),
            "revenueDollarsPerDay": band([x["r"] / units / 100 / 14 for x in rows]),
            "homeMealsPerDay": band([x["home"] / 14 for x in rows]),
            "ownMealsPerDay": band([x["meals"] / 14 for x in rows]),
            "itemsPerMeal": band([x["q"] / x["meals"] for x in rows]),
            "extrasPerMeal": band([sum(m[1] for m in x["mains"].values()) / sum(m[0] for m in x["mains"].values()) for x in rows]) if all(x["mains"] for x in rows) else None,
            "ticketDollarsPerMeal": band([x["r"] / units / 100 / x["meals"] for x in rows]),
            "unaffordableRefusalShare": band([x["refused"] / x["considered"] for x in rows]),
            "peakServedPerHour": [x["peak"] for x in rows], "capacityPerHour": capacity,
            "capacityBinding": any(x["peak"] >= capacity for x in rows),
        }
        if key != "control/control":
            entry["quantityChange"] = band([x["q"] / b["q"] - 1 for x, b in zip(rows, base)])
            entry["revenueChange"] = band([x["r"] / b["r"] - 1 for x, b in zip(rows, base)])
            # Menu arc uses own purchases; product arms use all own purchases too (cross-product
            # substitution included), so their value is a shop-level response, not own-product.
            entry["arcElasticity"] = band([(x["q"] - b["q"]) / ((x["q"] + b["q"]) / 2) / arc_price for x, b in zip(rows, base)])
            entry["mealQuantityChange"] = band([x["meals"] / b["meals"] - 1 for x, b in zip(rows, base)])
            entry["mealArcElasticity"] = band([(x["meals"] - b["meals"]) / ((x["meals"] + b["meals"]) / 2) / arc_price for x, b in zip(rows, base)])
            entry["homeMealsChange"] = band([x["home"] / b["home"] - 1 for x, b in zip(rows, base)])
            if all(x["mains"] for x in rows):
                # Extras per meal, arm minus control, split into a main-mix part (arm's mains at control's per-main
                # extras rate) and a within-main part (arm's per-main rate change at arm's mix). Under a whole-menu
                # markup the extras bands and draws are unchanged, so a wallet that cuts extras shows up within-main.
                def split(x, b):
                    meals, base_meals = sum(m[0] for m in x["mains"].values()), sum(m[0] for m in b["mains"].values())
                    rate = lambda m: m[1] / m[0] if m[0] else 0
                    mix = sum(m[0] / meals * rate(b["mains"].get(k, [0, 0, 0])) for k, m in x["mains"].items()) - sum(m[1] for m in b["mains"].values()) / base_meals
                    within = sum(m[1] for m in x["mains"].values()) / meals - sum(m[0] / meals * rate(b["mains"].get(k, [0, 0, 0])) for k, m in x["mains"].items())
                    return mix, within
                parts = [split(x, b) for x, b in zip(rows, base)]
                entry["extrasPerMealChangeFromMainMix"] = band([m for m, _ in parts])
                entry["extrasPerMealChangeWithinMain"] = band([w for _, w in parts])
        table[key] = entry
    return table


def report(directory):
    groups = {}
    hashes = {}
    for path in sorted((directory / "runs").glob("*.json")):
        raw = path.read_bytes()
        run = json.loads(raw)
        assert run["schema"] == "persistentcity-f6h-metrics-1", path
        assert len(run["days"]) == 42, path
        key = run["family"] + "/" + run["arm"]
        groups.setdefault(key, []).append(run)
        hashes[path.name] = hashlib.sha256(raw).hexdigest()
    controls = sorted(groups["control/control"], key=lambda r: r["seed"])
    assert len(controls) == 5 and [r["seed"] for r in controls] == list(range(5))
    units = controls[0]["fixture"]["unitsPerCent"]
    # Read actual product IDs from the fixture outputs rather than relying on name spelling.
    base_units = {}
    for d in controls[0]["days"]:
        for shop in d["own"]["shops"]:
            for p in shop["products"]:
                if p["purchases"]:
                    cost = p["revenueUnits"] // p["purchases"]
                    assert cost * p["purchases"] == p["revenueUnits"]
                    base_units[p["productId"]] = cost
    assert len(base_units) == 3
    metrics = {
        "ownPurchases": lambda d: d["own"]["purchases"],
        "revenueUnits": lambda d: d["own"]["revenueUnits"],
        "rivalMeals": lambda d: d["rival"]["meals"],
        "incomeUnits": lambda d: d["households"]["incomeReceivedUnits"],
    }
    rows = {}
    conservation = 0
    for key, runs in groups.items():
        runs.sort(key=lambda r: r["seed"])
        assert [r["seed"] for r in runs] == list(range(5)), key
        for seed, run in enumerate(runs):
            assert run["days"][:14] == controls[seed]["days"][:14], (key, "unmatched baseline")
            for d in run["days"]:
                a = d["accounting"]
                assert a["conserved"] and a["duplicateReceipts"] == a["historicalClockMisses"] == a["epochReadViolations"] == 0
                assert a["nativeRevenueUnits"] == a["confirmedPreparedUnits"] + a["uncertainConfirmedUnits"]
                assert a["outsideLedgerUnits"] == a["watermarkedOutflowUnits"]
                conservation += a["conservationChecks"]
                day = d["day"]
                expired = run["days"][day - 15]["accounting"]["ordinaryDiscretionaryUnits"] if day > 14 else 0
                assert a["ordinaryWindowUnits"] == a["ordinaryDiscretionaryUnits"] - expired
        row = {"weekly": {}, "effectOutsideControlBand": {}}
        for name, metric in metrics.items():
            row["weekly"][name] = [band([sum(metric(d) for d in r["days"][w * 7:(w + 1) * 7]) for r in runs]) for w in range(6)]
            value = st.mean([st.mean(metric(d) for d in r["days"][14:28]) for r in runs])
            control_band = band([st.mean(metric(d) for d in r["days"][14:28]) for r in controls])
            row["effectOutsideControlBand"][name] = not inside(value, control_band)
        if key != "control/control":
            row["recoveryDaysPerSeed"] = [recovery(r, controls) for r in runs]
            row["recoveryWithin14Days"] = all(d is not None and d <= 14 for d in row["recoveryDaysPerSeed"])
            row["residualPurchasesPerDayAfter14Days"] = [trailing(r, 42) - trailing(c, 42) for r, c in zip(runs, controls)]
            row["staffingDoesNotRaiseFortnightMeals"] = all(
                sum(d["own"]["purchases"] for d in r["days"][14:28]) <= sum(d["own"]["purchases"] for d in c["days"][14:28])
                for r, c in zip(runs, controls)) if key.startswith("staffing/") else None
        if key.startswith("staffing/"):
            def active_meals(run):
                if key.endswith("zero-staff-day"):
                    return run["days"][16]["own"]["purchases"]
                return sum(sum(shop["byHour"][11:14]) for d in run["days"][14:28] for shop in d["own"]["shops"])
            row["staffingActiveWindowDeltaMealsPerSeed"] = [active_meals(r) - active_meals(c) for r, c in zip(runs, controls)]
            row["staffingWeeklyDeltaMealsPerSeed"] = [[sum(d["own"]["purchases"] for d in r["days"][w * 7:(w + 1) * 7]) -
                sum(d["own"]["purchases"] for d in c["days"][w * 7:(w + 1) * 7]) for w in (2, 3)] for r, c in zip(runs, controls)]
            row["staffingDaysWithMoreMealsThanControlPerSeed"] = [[d["day"] for d, c in zip(r["days"][14:28], control["days"][14:28])
                if d["own"]["purchases"] > c["own"]["purchases"]] for r, control in zip(runs, controls)]
        rows[key] = row
    household = {}
    for name, measure in {
        "arrearsShare": lambda r, w: r["days"][w * 7 + 6]["households"]["arrearsOwners"] / r["days"][w * 7 + 6]["households"]["owners"],
        "p10Dollars": lambda r, w: r["days"][w * 7 + 6]["households"]["p10BalanceUnits"] / units / 100,
        "unfedOwnerDays": lambda r, w: sum(d["households"]["unfedDays"] for d in r["days"][w * 7:w * 7 + 7]),
    }.items():
        household[name] = [band([measure(r, w) for r in controls]) for w in range(6)]
    household["p10FallsEveryWeekPerSeed"] = [all(r["days"][w * 7 + 6]["households"]["p10BalanceUnits"] < r["days"][(w - 1) * 7 + 6]["households"]["p10BalanceUnits"] for w in range(1, 6)) for r in controls]
    week6 = {"week6ArrearsShare": household["arrearsShare"][5], "week6UnfedOwnerDays": household["unfedOwnerDays"][5],
             "week6P10Dollars": household["p10Dollars"][5]}
    household["thresholds"] = HOUSEHOLD_THRESHOLDS
    household["derivedFromThisRun"] = derive_household_thresholds(household)
    household["withinThresholdsPerSeed"] = [within_household(a, u, p) for a, u, p in zip(week6["week6ArrearsShare"]["perSeed"], week6["week6UnfedOwnerDays"]["perSeed"], week6["week6P10Dollars"]["perSeed"])]
    certificates = []
    if "price/menu-plus-50" in groups:
        for seed, high in enumerate(groups["price/menu-plus-50"]):
            for week in (2, 3):
                comparisons = {key: dominance_floor(high, runs[seed], week, base_units) / units / 100
                               for key, runs in groups.items() if key == "control/control" or key.startswith("price/product-")}
                certificates.append({"seed": seed, "week": week + 1, "minimumProfitAdvantageDollars": comparisons})
    dominant = len(certificates) == 10 and all(min(c["minimumProfitAdvantageDollars"].values()) > 0 for c in certificates)
    failures = []
    if dominant:
        failures.append("among price arms, menu +50% maximizes weekly revenue minus every fixed wholesale vector in [0, baseline retail] in every seed in weeks 3 and 4")
    for key, row in rows.items():
        if key == "control/control":
            continue
        if not row["recoveryWithin14Days"]:
            failures.append(key + ": recovery exceeds 14 days in at least one seed")
        if row["staffingDoesNotRaiseFortnightMeals"] is False or any(delta > 0 for delta in row.get("staffingActiveWindowDeltaMealsPerSeed", [])):
            failures.append(key + ": staffing cut raises served meals")
    if any(household["p10FallsEveryWeekPerSeed"]):
        failures.append("control p10 falls every week in at least one seed")
    if not all(household["withinThresholdsPerSeed"]):
        failures.append("control household week-6 metrics outside the rerun thresholds in at least one seed")
    families = {family: (any(any(row["effectOutsideControlBand"].values())
                           for key, row in rows.items() if key.startswith(family + "/"))
                           if any(key.startswith(family + "/") for key in rows) else None)
                for family in ("price", "staffing", "hours", "shortage", "employment", "access", "rival")}
    for family, passed in families.items():
        if passed is False:
            failures.append(family + ": no measured effect outside control band")
    profile = controls[0].get("profile", {"name": "capacity-bound", "mealsPerHour": controls[0]["fixture"]["mealsPerHour"]})
    profile = dict(profile, rivalListed=controls[0]["fixture"]["rivalListed"])
    prices = price_arms(groups, controls, base_units, units) if any(k.startswith("price/") for k in groups) else {}
    return {"status": "stopped" if failures else "partial", "profile": profile, "unitsPerCent": units, "runs": len(hashes), "completeInterventionMatrix": len(groups) == 17,
            "dailyConservationChecks": conservation, "arms": rows, "householdControlSeedBands": household,
            "familyEffectOutsideControlBand": families, "menuDominanceCertificates": certificates, "priceArms": prices,
            "failedCriteria": failures, "unmeasuredCriteria": ["measured native wholesale costs", "every gain has a measured cost / no Pareto dominant arm",
                "native staffed-throughput calibration", "F1 cadence byte identity", "F2 body-cap flagged differences", "full section 6 perf and 100000-consumer stress"] + (["remaining intervention arms"] if len(groups) != 17 else []),
            "defaults": controls[0]["integrated"], "metricsSha256": hashes}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = report(args.directory)
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(result["status"], result["runs"], "runs;", result["dailyConservationChecks"], "daily owner checks")
    for failure in result["failedCriteria"]:
        print("FAILED:", failure)
    raise SystemExit(1 if result["failedCriteria"] or result["unmeasuredCriteria"] else 0)


if __name__ == "__main__":
    main()
