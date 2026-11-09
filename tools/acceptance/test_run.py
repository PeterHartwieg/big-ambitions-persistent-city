"""F9a runner tests with fake stage commands. python -m unittest discover -s tools/acceptance -p test_*.py"""
import copy
import getpass
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import economy_stage  # noqa: E402
import run  # noqa: E402

ROOT = HERE.parents[1]
SAMPLE = ROOT / "tools/economy-interventions/evidence/f6h-f6j-rerun-acceptance.json"
BASELINE = ROOT / "tools/movement-equivalence/baseline.json"
EXPECTATIONS = HERE / "expectations/movement.json"
HOUSEHOLD = "control household week-6 metrics outside the rerun thresholds in at least one seed"

TRX = """<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>{results}</Results>
  <TestDefinitions>{definitions}</TestDefinitions>
</TestRun>
"""

FAKE_TEST = """
import sys, pathlib
args = sys.argv[1:]
outcomes = {outcomes!r}
target = pathlib.Path(args[args.index("--trx") + 1])
if outcomes is None:
    print("No test matches the given testcase filter `X` in Y")
    sys.exit(0)
target.parent.mkdir(parents=True, exist_ok=True)
results, definitions = "", ""
for i, (name, outcome) in enumerate(outcomes):
    cls, method = name.rsplit(".", 1)
    results += '<UnitTestResult testId="id%d" testName="%s" outcome="%s" />' % (i, name, outcome)
    definitions += '<UnitTest id="id%d" name="%s"><TestMethod className="%s" name="%s" /></UnitTest>' % (i, method, cls, method)
target.write_text({trx!r}.format(results=results, definitions=definitions))
sys.exit(1 if any(o == "Failed" for _, o in outcomes) else 0)
"""


def sample(**changes):
    data = json.loads(SAMPLE.read_text(encoding="utf-8"))
    data["failedCriteria"] = []
    data["defaults"] = dict(data["defaults"], priceWeight=1.2)
    data.update(changes)
    return data


class Fixture:
    """A temporary out directory, lock and catalogue whose stages are fake Python scripts under {out}."""

    def __init__(self, test):
        self.tmp = tempfile.TemporaryDirectory()
        test.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)
        self.out = self.dir / "out"
        self.out.mkdir()
        self.lock = self.dir / "acceptance.lock"
        self.stages = []
        self.profile = ["provenance"]

    def script(self, name, body):
        (self.out / name).write_text(textwrap.dedent(body), encoding="utf-8")
        return "{out}/" + name

    def stage(self, **stage):
        stage.setdefault("required", stage["class"] in ("semantic", "native"))
        self.stages.append(stage)
        self.profile.append(stage["id"])

    def fake_test(self, stage_id, outcomes, cls="semantic", **extra):
        script = self.script(stage_id + "_test.py", FAKE_TEST.format(outcomes=outcomes, trx=TRX))
        self.stage(id=stage_id, **{"class": cls}, rule="trx",
                   steps=[["{python}", script, "--trx", "{out}/trx/" + stage_id + ".trx"]],
                   trx="trx/" + stage_id + ".trx", **extra)

    def native_pass(self):
        self.stage(id="rig", **{"class": "native"}, rule="exit-zero", rig=True,
                   steps=[["{python}", self.script("ok.py", "print('ok')\n")]])

    def run(self, *extra):
        catalogue = {"schema": "persistentcity-acceptance-stages-1", "profiles": {"quick": self.profile, "full": self.profile},
                     "stages": [{"id": "provenance", "class": "provenance", "rule": "provenance", "required": False, "steps": []}] + self.stages,
                     "settings": json.loads((HERE / "stages.json").read_text())["settings"], "fixtures": [], "capabilities": []}
        path = self.dir / "stages.json"
        path.write_text(json.dumps(catalogue), encoding="utf-8")
        code = run.main(["quick", "--out", str(self.out), "--catalogue", str(path), "--lock", str(self.lock),
                         "--dotnet", "missing-dotnet-for-tests", *extra])
        text = (self.out / "acceptance.json").read_text(encoding="utf-8")
        return code, json.loads(text), text

    def stage_record(self, result, stage_id):
        return next(s for s in result["stages"] if s["id"] == stage_id)


class ResultHygiene(unittest.TestCase):
    def test_no_absolute_path_or_user_name_in_result(self):
        f = Fixture(self)
        f.stage(id="loud", **{"class": "semantic"}, rule="exit-zero",
                steps=[["{python}", f.script("loud.py", f"import os, sys; print(os.getcwd(), sys.executable); raise SystemExit(3)\n")]])
        f.fake_test("tests", [("PersistentCity.Tests.A.B", "Passed")])
        code, result, text = f.run()
        self.assertEqual(1, code)
        for raw in {str(ROOT), str(ROOT).replace("\\", "/"), str(Path.home()), str(f.dir), sys.executable, tempfile.gettempdir()}:
            self.assertNotIn(raw, text)
            self.assertNotIn(raw.replace("\\", "\\\\"), text)
        user = getpass.getuser()
        if len(user) >= 3:
            self.assertIsNone(re.search(r"\b" + re.escape(user) + r"\b", text, re.IGNORECASE), "user name leaked")
        self.assertIsNone(re.search(r"[A-Za-z]:[\\/]|/Users/|/home/", text))
        host = result["provenance"]["host"]
        self.assertEqual({"os", "architecture", "dotnetSdk", "python"}, set(host))
        self.assertNotIn(os.environ.get("COMPUTERNAME", "<none>"), text)
        self.assertEqual("{python}", f.stage_record(result, "loud")["argv"][0])
        self.assertNotIn("game", result["provenance"])  # F9b adds the game block

    def test_provenance_records_source_settings_and_compiled_price_weight(self):
        f = Fixture(self)
        _, result, _ = f.run()
        prov = result["provenance"]
        self.assertRegex(prov["source"]["commit"], r"^[0-9a-f]{40}$")
        self.assertIn("dirty", prov["source"])
        if prov["source"]["dirty"]:
            self.assertRegex(prov["source"]["diffSha256"], r"^[0-9a-f]{64}$")
        defaults = prov["settings"]["compiledDefaults"]
        self.assertEqual(1.2, defaults["FoodChoicePriceWeight"])
        self.assertEqual(1, defaults["FoodChoiceHouseholdWeight"])
        self.assertEqual(1, defaults["AdvanceFrameSliceEventBudget"])
        self.assertEqual(hashlib.sha256((ROOT / "config/persistent-city.json").read_bytes()).hexdigest(), prov["settings"]["configSha256"])
        self.assertEqual("persistentcity-acceptance-1", result["schema"])

    def test_evidence_hashes_match_the_files(self):
        f = Fixture(self)
        f.fake_test("tests", [("PersistentCity.Tests.A.B", "Passed"), ("PersistentCity.Tests.A.C", "Passed")])
        _, result, _ = f.run()
        entries = [e for s in result["stages"] for e in s["evidence"]]
        self.assertGreaterEqual(len(entries), 2)
        for entry in entries:
            path = f.out / entry["path"]
            self.assertFalse(Path(entry["path"]).is_absolute())
            data = path.read_bytes()
            self.assertEqual(hashlib.sha256(data).hexdigest(), entry["sha256"])
            self.assertEqual(len(data), entry["bytes"])
            self.assertFalse(entry["retained"])


class Verdicts(unittest.TestCase):
    def result(self, *verdicts, rig=True, performance=None):
        stages = [{"id": f"s{i}", "class": "semantic", "required": True, "verdict": v} for i, v in enumerate(verdicts)]
        if rig:
            stages.append({"id": "rig", "class": "native", "required": True, "verdict": "pass", "rig": True})
        result = {"stages": stages, "classes": {}}
        if performance:
            result["classes"]["performance"] = {"verdict": performance, "reference": "docs/perf/f4.md"}
        return run.summarize_result(result)

    def test_fail_beats_incomplete_beats_pass(self):
        self.assertEqual("fail", self.result("pass", "incomplete", "fail", "not-run")["summary"]["verdict"])
        self.assertEqual("incomplete", self.result("pass", "incomplete")["summary"]["verdict"])
        self.assertEqual("incomplete", self.result("pass", "not-run")["summary"]["verdict"])
        self.assertEqual("pass", self.result("pass", "expected-red")["summary"]["verdict"])
        summary = self.result("fail", "not-run")["summary"]
        self.assertEqual(["s0"], summary["failed"])
        self.assertEqual(["s1"], summary["incomplete"])

    def test_performance_and_gameplay_never_change_the_verdict(self):
        for performance in ("fail", "pass", None):
            result = self.result("pass", performance=performance)
            self.assertEqual("pass", result["summary"]["verdict"])
            self.assertEqual("not-assessed", result["classes"]["gameplay"]["verdict"])
        failing = self.result("fail", performance="pass")
        self.assertEqual("fail", failing["summary"]["verdict"])
        self.assertEqual("pass", failing["classes"]["performance"]["verdict"])
        perf_stage = {"stages": [{"id": "s", "class": "semantic", "required": True, "verdict": "pass"},
                                 {"id": "p", "class": "performance", "required": True, "verdict": "fail"},
                                 {"id": "rig", "class": "native", "required": True, "verdict": "pass", "rig": True}]}
        self.assertEqual("pass", run.summarize_result(perf_stage)["summary"]["verdict"])

    def test_missing_rig_results_leave_native_incomplete(self):
        result = self.result("pass", rig=False)
        self.assertEqual("incomplete", result["summary"]["verdict"])
        self.assertIn("rig", result["summary"]["incomplete"])

    def test_summarize_merges_rig_and_performance(self):
        f = Fixture(self)
        f.fake_test("tests", [("PersistentCity.Tests.A.B", "Passed")])
        _, result, _ = f.run()
        self.assertEqual("incomplete", result["summary"]["verdict"])
        rig = f.dir / "rig.json"
        rig.write_text(json.dumps({"schema": run.SCHEMA, "stages": [{"id": "rig-check-1", "class": "native", "verdict": "pass"}]}))
        perf = f.dir / "perf.json"
        perf.write_text(json.dumps({"schema": run.SCHEMA, "class": "performance", "verdict": "fail", "reference": "docs/perf/f4.md"}))
        merged_out = f.dir / "merged"
        code = run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--performance", str(perf),
                         "--out", str(merged_out), "--lock", str(f.lock)])
        merged = json.loads((merged_out / "acceptance.json").read_text())
        self.assertEqual(0, code)
        self.assertEqual("pass", merged["summary"]["verdict"])
        self.assertEqual("fail", merged["classes"]["performance"]["verdict"])
        rig.write_text(json.dumps({"schema": run.SCHEMA, "stages": [{"id": "rig-check-1", "class": "native", "verdict": "fail"}]}))
        self.assertEqual(1, run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig),
                                      "--out", str(merged_out), "--lock", str(f.lock)]))


class Stages(unittest.TestCase):
    def test_build_is_not_run_without_game_assemblies(self):
        f = Fixture(self)
        f.stage(id="build", **{"class": "native"}, rule="build", steps=[["{python}", "tools/build.py", "--game-managed", "{managed}"]])
        code, result, _ = f.run()
        build = f.stage_record(result, "build")
        self.assertEqual("not-run", build["verdict"])
        self.assertIn("--game-managed", build["reason"])
        self.assertEqual(0, code)
        self.assertEqual("incomplete", result["summary"]["verdict"])
        self.assertIn("build", result["summary"]["incomplete"])
        self.assertIsNone(result["provenance"]["artifact"]["persistentCityDllSha256"])

    def test_build_with_supplied_assemblies_records_the_dll_hash_and_hides_the_path(self):
        f = Fixture(self)
        managed = f.dir / "Managed"
        managed.mkdir()
        dll = "ab" * 32
        f.stage(id="build", **{"class": "native"}, rule="build",
                steps=[["{python}", f.script("build.py", f"import sys; print('Managed', sys.argv[1]); print('DLL SHA256: {dll}')\n"), "{managed}"]])
        _, result, text = f.run("--game-managed", str(managed))
        self.assertEqual("pass", f.stage_record(result, "build")["verdict"])
        self.assertEqual(dll, result["provenance"]["artifact"]["persistentCityDllSha256"])
        self.assertNotIn(str(managed), text)
        self.assertEqual("{managed}", f.stage_record(result, "build")["argv"][-1])

    def test_trx_counts_and_failures(self):
        f = Fixture(self)
        f.fake_test("tests", [("PersistentCity.Tests.A.B", "Passed"), ("PersistentCity.Tests.A.C", "Failed"),
                              ("PersistentCity.Tests.A.D", "NotExecuted")])
        code, result, _ = f.run()
        stage = f.stage_record(result, "tests")
        self.assertEqual("fail", stage["verdict"])
        self.assertEqual((3, 1, 1, 1), tuple(stage["metrics"][k] for k in ("total", "passed", "failed", "skipped")))
        self.assertEqual(["PersistentCity.Tests.A.C"], stage["metrics"]["failedTests"])
        self.assertEqual(1, code)

    def test_absent_test_filter_is_not_run_and_does_not_fail_the_runner(self):
        f = Fixture(self)
        f.fake_test("tests", [("PersistentCity.Tests.A.B", "Passed")])
        f.fake_test("city-s", None)
        f.native_pass()
        code, result, _ = f.run()
        self.assertEqual("not-run", f.stage_record(result, "city-s")["verdict"])
        self.assertEqual("incomplete", result["summary"]["verdict"])
        self.assertEqual(["city-s"], result["summary"]["incomplete"])
        self.assertEqual(0, code)
        self.assertEqual(3, f.run("--strict")[0])

    def test_filtered_stages_reuse_the_unit_trx(self):
        f = Fixture(self)
        f.fake_test("unit", [("PersistentCity.Tests.F5hIntegratedAcceptanceTests.A", "Passed"),
                             ("PersistentCity.Tests.Other.CurrentWriterGoldenIsPinned", "Passed"),
                             ("PersistentCity.Tests.Other.B", "Failed")])
        f.stage(id="population", **{"class": "semantic"}, rule="trx", filter="FullyQualifiedName~F5hIntegratedAcceptanceTests")
        f.stage(id="saves", **{"class": "semantic"}, rule="trx", filter="Name~CurrentWriterGolden")
        _, result, _ = f.run()
        for stage_id in ("population", "saves"):
            stage = f.stage_record(result, stage_id)
            self.assertEqual("unit", stage["reusedFrom"])
            self.assertEqual("pass", stage["verdict"])
            self.assertEqual(1, stage["metrics"]["passed"])
        self.assertEqual("fail", f.stage_record(result, "unit")["verdict"])
        self.assertIsNone(run.filter_terms("FullyQualifiedName~F9IntegratedCityTests&Category=city-s"))

    def test_only_runs_the_named_stages_and_records_the_subset(self):
        f = Fixture(self)
        f.fake_test("tests", [("PersistentCity.Tests.A.B", "Failed")])
        f.fake_test("other", [("PersistentCity.Tests.A.C", "Passed")])
        code, result, _ = f.run("--only", "other")
        self.assertEqual(["other"], [s["id"] for s in result["stages"]])
        self.assertEqual(["other"], result["onlyStages"])
        self.assertEqual(0, code)

    def test_a_crashing_stage_is_a_failed_stage(self):
        f = Fixture(self)
        f.stage(id="movement", **{"class": "semantic"}, rule="movement", steps=[["{python}", f.script("m.py", "print('no output')\n")]],
                output="movement.json", expectations="tools/acceptance/expectations/movement.json")
        _, result, _ = f.run()
        self.assertEqual("fail", f.stage_record(result, "movement")["verdict"])

    def test_catalogue_profiles_match_the_plan(self):
        catalogue = json.loads((HERE / "stages.json").read_text())
        ids = {s["id"] for s in catalogue["stages"]}
        self.assertTrue(set(catalogue["profiles"]["quick"]) <= ids and set(catalogue["profiles"]["full"]) <= ids)
        self.assertEqual(["provenance", "population", "economy-fast", "saves", "city-s"],
                         [s for s in catalogue["profiles"]["quick"] if s != "test-build"])
        self.assertEqual(["provenance", "build", "unit", "population", "economy", "saves", "movement", "city-s", "city-m"],
                         catalogue["profiles"]["full"])
        text = json.dumps(catalogue)
        self.assertNotIn("regression", text)
        self.assertIsNone(re.search(r"[A-Za-z]:\\\\|/Users/|LOCALAPPDATA|wait-other-dotnet", text))


class Movement(unittest.TestCase):
    def setUp(self):
        self.baseline = json.loads(BASELINE.read_text(encoding="utf-8"))
        self.listed = json.loads(EXPECTATIONS.read_text(encoding="utf-8"))["expectedRed"]

    def test_expectation_file_is_seeded_from_baseline(self):
        differing = [a["arm"] for a in self.baseline["arms"] if a.get("comparison") and not a["comparison"]["Identical"]]
        self.assertEqual(differing, self.listed)

    def test_expected_red_arm_passes(self):
        verdict, _, metrics, notes = run.assess_movement(self.baseline, self.listed)
        self.assertEqual("expected-red", verdict)
        self.assertEqual(sorted(self.listed), sorted(metrics["expectedRedArms"]))
        self.assertEqual([], notes)
        summary = run.summarize_result({"stages": [{"id": "movement", "class": "semantic", "required": True, "verdict": verdict},
                                                   {"id": "rig", "class": "native", "required": True, "verdict": "pass", "rig": True}]})
        self.assertEqual("pass", summary["summary"]["verdict"])

    def test_unlisted_differing_arm_fails(self):
        listed = [a for a in self.listed if a != "focus/sweep"]
        verdict, reason, metrics, _ = run.assess_movement(self.baseline, listed)
        self.assertEqual("fail", verdict)
        self.assertEqual(["focus/sweep"], metrics["unexpectedDifferingArms"])
        self.assertIn("focus/sweep", reason)

    def test_listed_arm_that_turns_green_is_reported_not_failed(self):
        data = copy.deepcopy(self.baseline)
        arm = next(a for a in data["arms"] if a["arm"] == "caps/24-12")
        arm["comparison"]["Identical"] = True
        verdict, _, metrics, notes = run.assess_movement(data, self.listed)
        self.assertEqual("expected-red", verdict)
        self.assertEqual(["caps/24-12"], metrics["listedNowGreen"])
        self.assertTrue(any("caps/24-12" in n for n in notes))
        for a in data["arms"]:
            if a.get("comparison"):
                a["comparison"]["Identical"] = True
        verdict, _, metrics, notes = run.assess_movement(data, self.listed)
        self.assertEqual("pass", verdict)
        self.assertEqual(len(self.listed), len(notes))

    def test_failed_self_check_fails(self):
        data = copy.deepcopy(self.baseline)
        data["selfChecks"][0]["pass"] = False
        self.assertEqual("fail", run.assess_movement(data, self.listed)[0])

    def test_end_to_end_with_recorded_baseline(self):
        f = Fixture(self)
        copier = f.script("movement.py", f"import shutil, sys; shutil.copy(sys.argv[1], sys.argv[2])\n")
        f.stage(id="movement", **{"class": "semantic"}, rule="movement",
                steps=[["{python}", copier, "tools/movement-equivalence/baseline.json", "{out}/movement.json"]],
                output="movement.json", expectations="tools/acceptance/expectations/movement.json")
        _, result, _ = f.run()
        stage = f.stage_record(result, "movement")
        self.assertEqual("expected-red", stage["verdict"])
        self.assertIn("bodies-off/plain-60", stage["digests"])


class Economy(unittest.TestCase):
    def test_recorded_sample_with_only_unmeasured_criteria_passes_with_every_item_routed(self):
        data = sample()
        result = economy_stage.assess(data, harness_exit=0, acceptance_exit=1, compiled_weight=1.2)
        self.assertEqual("pass", result["verdict"], result["reason"])
        self.assertEqual(data["unmeasuredCriteria"], [r["criterion"] for r in result["routed"]])
        classes = {r["criterion"]: r["class"] for r in result["routed"]}
        self.assertEqual("native", classes["measured native wholesale costs"])
        self.assertEqual("native", classes["native staffed-throughput calibration"])
        self.assertEqual("performance", classes["full section 6 perf and 100000-consumer stress"])
        self.assertEqual("superseded", classes["every gain has a measured cost / no Pareto dominant arm"])
        self.assertEqual(["movement"], next(r for r in result["routed"] if r["criterion"] == "F2 body-cap flagged differences")["stages"])
        self.assertNotIn("unrouted", classes.values())

    def test_recorded_sample_as_written_fails(self):
        data = json.loads(SAMPLE.read_text(encoding="utf-8"))  # pre-F6k dominance failure, weight 1
        self.assertEqual("fail", economy_stage.assess(data, compiled_weight=1.2)["verdict"])

    def test_recovery_failure_fails(self):
        data = sample(failedCriteria=["price/menu-plus-50: recovery exceeds 14 days in at least one seed"])
        self.assertEqual("fail", economy_stage.assess(data, compiled_weight=1.2)["verdict"])
        data = sample(failedCriteria=[HOUSEHOLD, "staffing/cut: staffing cut raises served meals"])
        self.assertEqual("fail", economy_stage.assess(data, compiled_weight=1.2)["verdict"])

    def test_household_threshold_only_miss_is_incomplete(self):
        result = economy_stage.assess(sample(failedCriteria=[HOUSEHOLD]), compiled_weight=1.2)
        self.assertEqual("incomplete", result["verdict"])
        self.assertIn("economy: household thresholds measured at weight 1", result["deviations"])

    def test_unknown_unmeasured_entry_is_incomplete(self):
        data = sample()
        data["unmeasuredCriteria"] = data["unmeasuredCriteria"] + ["a new F6 criterion"]
        result = economy_stage.assess(data, compiled_weight=1.2)
        self.assertEqual("incomplete", result["verdict"])
        self.assertIn("a new F6 criterion", result["reason"])

    def test_incomplete_matrix_fails(self):
        self.assertEqual("fail", economy_stage.assess(sample(completeInterventionMatrix=False), compiled_weight=1.2)["verdict"])

    def test_recorded_weight_other_than_compiled_default_is_refused(self):
        data = sample()
        data["defaults"]["priceWeight"] = 1
        self.assertEqual("fail", economy_stage.assess(data, compiled_weight=1.2)["verdict"])

    def test_price_weight_override_is_a_deviation_and_never_a_pass(self):
        data = sample()
        data["defaults"]["priceWeight"] = 1
        result = economy_stage.assess(data, compiled_weight=1.2, declared_weight=1.0)
        self.assertEqual("incomplete", result["verdict"])
        self.assertTrue(any("--price-weight 1" in d for d in result["deviations"]))

    def economy_fixture(self, acceptance_body):
        f = Fixture(self)
        harness = f.script("harness.py", """
            import sys, pathlib
            pathlib.Path(sys.argv[1]).mkdir(parents=True, exist_ok=True)
            pathlib.Path(sys.argv[1], "argv.txt").write_text(" ".join(sys.argv[2:]))
            """)
        acceptance = f.script("acceptance.py", acceptance_body)
        f.stage(id="economy", **{"class": "semantic"}, rule="economy", acceptance="f6h/acceptance.json",
                steps=[["{python}", harness, "{out}/f6h", "--integrated", "{priceWeightArgs}"],
                       ["{python}", acceptance, "{out}/f6h", "--output", "{out}/f6h/acceptance.json"]])
        f.native_pass()
        return f

    def test_fake_acceptance_that_raises_on_conservation_gives_fail(self):
        f = self.economy_fixture("""
            assert False, "daily conservation violated"
            """)
        code, result, _ = f.run()
        stage = f.stage_record(result, "economy")
        self.assertEqual("fail", stage["verdict"])
        self.assertIn("wrote no acceptance.json", stage["reason"])
        self.assertEqual(1, code)

    def test_runner_ignores_acceptance_exit_and_applies_the_adapter(self):
        payload = json.dumps(sample())
        f = self.economy_fixture(f"""
            import sys, pathlib
            pathlib.Path(sys.argv[3]).write_text({payload!r})
            raise SystemExit(1)
            """)
        code, result, _ = f.run()
        stage = f.stage_record(result, "economy")
        self.assertEqual("pass", stage["verdict"], stage["reason"])
        self.assertEqual("pass", result["summary"]["verdict"])
        self.assertEqual(2, len(result["classes"]["native"]["pending"]))
        self.assertIn("acceptance.json", stage["digests"])
        self.assertEqual(0, code)

    def test_runner_price_weight_override_is_passed_and_recorded_as_deviation(self):
        data = sample()
        data["defaults"]["priceWeight"] = 1.0
        payload = json.dumps(data)
        f = self.economy_fixture(f"""
            import sys, pathlib
            pathlib.Path(sys.argv[3]).write_text({payload!r})
            """)
        code, result, _ = f.run("--price-weight", "1")
        self.assertIn("--price-weight 1.0", (f.out / "f6h/argv.txt").read_text())
        self.assertEqual("incomplete", f.stage_record(result, "economy")["verdict"])
        self.assertNotEqual("pass", result["summary"]["verdict"])
        self.assertTrue(result["summary"]["deviations"])
        self.assertEqual({"FoodChoicePriceWeight": 1.0}, result["provenance"]["settings"]["declaredOverrides"])


class Lock(unittest.TestCase):
    def test_lock_refuses_a_second_runner(self):
        f = Fixture(self)
        holder = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
        self.addCleanup(holder.wait)
        self.addCleanup(holder.kill)
        f.lock.write_text(json.dumps({"pid": holder.pid, "checkout": "elsewhere"}))
        self.assertEqual(4, run.main(["quick", "--out", str(f.out), "--lock", str(f.lock)]))
        self.assertEqual(holder.pid, json.loads(f.lock.read_text())["pid"])
        self.assertFalse((f.out / "acceptance.json").exists())

    def test_lock_clears_a_stale_one(self):
        f = Fixture(self)
        gone = subprocess.Popen([sys.executable, "-c", "pass"])
        gone.wait()
        f.lock.write_text(json.dumps({"pid": gone.pid, "checkout": "elsewhere"}))
        lock = run.RunnerLock(f.lock)
        with lock:
            self.assertEqual(gone.pid, lock.cleared_stale["pid"])
            self.assertEqual(os.getpid(), json.loads(f.lock.read_text())["pid"])
            with self.assertRaises(run.LockHeld):
                run.RunnerLock(f.lock).acquire()
        self.assertFalse(f.lock.exists())

    def test_other_test_processes_are_waited_for_or_refused(self):
        with self.assertRaises(run.LockHeld):
            run.wait_for_dotnet(no_wait=True, probe=lambda: ["testhost.exe 1234"])
        calls = iter([["testhost.exe 1"], []])
        run.wait_for_dotnet(no_wait=False, poll=0, probe=lambda: next(calls))


if __name__ == "__main__":
    unittest.main()
