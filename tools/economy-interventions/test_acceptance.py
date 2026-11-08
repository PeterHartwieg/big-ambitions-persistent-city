"""Fast tests of the independent acceptance reader's red-path and cost-box proof."""
import copy
import itertools
import json
import unittest
from pathlib import Path
from acceptance import (HOUSEHOLD_THRESHOLDS, band, derive_household_thresholds, dominance_floor,
                        price_arms, recovery, within_household)

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
        # Sufficient demand loss removes the universal dominance certificate.
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
        # Pinned: a rerun that changes the canonical control band must re-derive the thresholds.
        report = json.loads((EVIDENCE / "f6h-rerun-acceptance.json").read_text())
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


if __name__ == "__main__":
    unittest.main()
