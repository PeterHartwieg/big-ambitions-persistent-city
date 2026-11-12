import copy
import itertools
import json
import unittest
from pathlib import Path
from acceptance import (HOUSEHOLD_THRESHOLDS, band, derive_household_thresholds, dominance_floor,
                        price_arms, recovery, staffing_checks, staffing_raises_meals, within_household)

EVIDENCE = Path(__file__).parent / "evidence"


def run(counts, prices):
    products = [{"productId": p, "purchases": n, "revenueUnits": n * prices[p]} for p, n in counts.items()]
    return {"days": [{"own": {"purchases": sum(counts.values()), "revenueUnits": sum(p["revenueUnits"] for p in products),
                              "shops": [{"products": products}]}} for _ in range(42)]}


class AcceptanceTests(unittest.TestCase):
    def test_cost_box_certificate_equals_exhaustive_corners_even_when_products_switch(self):
        a = run({"a": 12, "b": 3}, {"a": 15, "b": 30})
        b = run({"a": 8, "b": 9}, {"a": 10, "b": 20})
        corners = [7 * ((12 * 15 + 3 * 30) - (8 * 10 + 9 * 20) - (12 - 8) * ca - (3 - 9) * cb)
                   for ca, cb in itertools.product([0, 10], [0, 20])]
        self.assertEqual(min(corners), dominance_floor(a, b, 2, {"a": 10, "b": 20}))

    def test_price_only_increase_is_red_even_with_unknown_nonlossmaking_wholesale(self):
        a = run({"a": 50}, {"a": 150})
        b = run({"a": 50}, {"a": 100})
        self.assertGreater(dominance_floor(a, b, 2, {"a": 100}), 0)
        c = run({"a": 10}, {"a": 150})
        self.assertLess(dominance_floor(c, b, 2, {"a": 100}), 0)

    def test_recovery_requires_three_wholly_reverted_days_and_can_fail(self):
        controls = [run({"a": n}, {"a": 100}) for n in range(98, 103)]
        recovered = copy.deepcopy(controls[2])
        self.assertEqual(3, recovery(recovered, controls))
        for d in recovered["days"][28:]:
            d["own"]["purchases"] = 500
        self.assertIsNone(recovery(recovered, controls))

    def test_seed_band_keeps_dispersion_and_observed_extremes(self):
        b = band([1, 2, 3, 4, 5])
        self.assertEqual(3, b["mean"])
        self.assertEqual((1, 5), (b["min"], b["max"]))
        self.assertLess(b["low"], 1)
        self.assertGreater(b["high"], 5)

    def test_household_thresholds_are_the_rerun_control_band_rounded_outward(self):
        report = json.loads((EVIDENCE / "f6h-f6j-rerun-acceptance.json").read_text())
        household = report["householdControlSeedBands"]
        self.assertEqual(HOUSEHOLD_THRESHOLDS, derive_household_thresholds(household))
        self.assertTrue(all(household["withinThresholdsPerSeed"]))
        self.assertFalse(within_household(HOUSEHOLD_THRESHOLDS["week6ArrearsShareMax"] + 1e-4, 0, 1000))
        self.assertFalse(within_household(0, 0, HOUSEHOLD_THRESHOLDS["week6P10DollarsMin"] - 1))

    def test_price_table_flags_binding_capacity_and_measures_arc_elasticity(self):
        def day(q, cents, refused, peak):
            return {"own": {"purchases": q, "revenueUnits": q * cents, "shops": [{"byHour": [peak], "offers": {"price": refused, "considered": 100}}]},
                    "share": {"city": {"home": 1000 - q}}}
        def arm(q, cents, peak, refused=1, seeds=5):
            return [{"days": [day(q, cents, refused, peak)] * 42, "fixture": {"mealsPerHour": 60}} for _ in range(seeds)]
        controls = arm(100, 10, 50)
        table = price_arms({"control/control": controls, "price/menu-plus-50": arm(60, 15, 60)}, controls, {}, 1)
        menu = table["price/menu-plus-50"]
        self.assertTrue(menu["capacityBinding"])
        self.assertFalse(table["control/control"]["capacityBinding"])
        self.assertAlmostEqual(-0.4, menu["quantityChange"]["mean"])
        self.assertAlmostEqual(-0.1, menu["revenueChange"]["mean"])
        self.assertAlmostEqual(-0.5 / 0.4, menu["arcElasticity"]["mean"])
        self.assertAlmostEqual(0.01, menu["unaffordableRefusalShare"]["mean"])

    def test_capacity_and_meal_quantity_read_meals_not_items_after_f6j(self):
        def day(meals, items, cents, peak_meals, peak_items):
            shop = {"meals": meals, "mealsByCheckoutHour": [peak_meals], "byHour": [peak_items], "offers": {"price": 0, "considered": 100}}
            return {"own": {"purchases": items, "revenueUnits": items * cents, "shops": [shop]}, "share": {"city": {"home": 1000 - meals}}}
        def arm(meals, items, cents, peak_meals, peak_items):
            return [{"days": [day(meals, items, cents, peak_meals, peak_items)] * 42, "fixture": {"mealsPerHour": 60}} for _ in range(5)]
        controls = arm(100, 120, 10, 50, 80)
        table = price_arms({"control/control": controls, "price/menu-plus-50": arm(80, 100, 15, 40, 70)}, controls, {}, 1)
        self.assertFalse(table["control/control"]["capacityBinding"])
        self.assertAlmostEqual(1.2, table["control/control"]["itemsPerMeal"]["mean"])
        self.assertAlmostEqual(12.0 / 100, table["control/control"]["ticketDollarsPerMeal"]["mean"])
        menu = table["price/menu-plus-50"]
        self.assertAlmostEqual(-0.2, menu["mealQuantityChange"]["mean"])
        self.assertAlmostEqual(100 / 120 - 1, menu["quantityChange"]["mean"])
        self.assertAlmostEqual(1500 / 1200 - 1, menu["revenueChange"]["mean"])


    def test_extras_change_splits_into_main_mix_and_within_main(self):
        def arm(tickets):
            meals = sum(t["meals"] for t in tickets); items = meals + sum(t["extras"] for t in tickets)
            shop = {"meals": meals, "mealsByCheckoutHour": [1], "byHour": [1], "offers": {"price": 0, "considered": 1}, "tickets": tickets}
            day = {"own": {"purchases": items, "revenueUnits": items, "shops": [shop]}, "share": {"city": {"home": 1}}}
            return [{"days": [day] * 42, "fixture": {"mealsPerHour": 60}} for _ in range(5)]
        t = lambda main, meals, extras: {"mainProductId": main, "meals": meals, "extras": extras, "plannedExtras": extras}
        controls = arm([t("a", 50, 50), t("b", 50, 0)])
        table = price_arms({"control/control": controls, "price/menu-plus-50": arm([t("a", 20, 20), t("b", 60, 0)]),
                            "price/product-plus-25": arm([t("a", 50, 25), t("b", 50, 0)])}, controls, {}, 1)
        mix = table["price/menu-plus-50"]
        self.assertAlmostEqual(20 / 80 - 0.5, mix["extrasPerMealChangeFromMainMix"]["mean"])
        self.assertAlmostEqual(0, mix["extrasPerMealChangeWithinMain"]["mean"])
        within = table["price/product-plus-25"]
        self.assertAlmostEqual(0, within["extrasPerMealChangeFromMainMix"]["mean"])
        self.assertAlmostEqual(-0.25, within["extrasPerMealChangeWithinMain"]["mean"])

    def test_staffing_gate_counts_meals_not_items(self):
        def run(meals, items, seed):
            hours = [0] * 24; hours[12] = meals
            shop = {"meals": meals, "mealsByCheckoutHour": hours, "byHour": [0] * 12 + [items] + [0] * 11}
            return {"seed": seed, "days": [{"day": n + 1, "own": {"purchases": items, "shops": [shop]}} for n in range(42)]}
        controls = [run(100, 150, s) for s in range(5)]
        cut = [run(101, 110, s) for s in range(5)]
        for key in ("staffing/lunch-counters-halved", "staffing/zero-staff-day"):
            row = staffing_checks(key, cut, controls)
            self.assertFalse(row["staffingDoesNotRaiseFortnightMeals"])
            self.assertTrue(all(d > 0 for d in row["staffingActiveWindowDeltaMealsPerSeed"]))
            self.assertTrue(staffing_raises_meals(row))
        fewer = staffing_checks("staffing/lunch-counters-halved", [run(90, 160, s) for s in range(5)], controls)
        self.assertFalse(staffing_raises_meals(fewer))


if __name__ == "__main__":
    unittest.main()
