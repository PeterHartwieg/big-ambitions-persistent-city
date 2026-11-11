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
SAMPLE = ROOT / "tests/economy-interventions/evidence/f6h-f6j-rerun-acceptance.json"
BASELINE = ROOT / "tests/movement-equivalence/baseline.json"
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
        # F9b: without supplied assemblies the game block says so and holds no hashes.
        self.assertEqual({"supplied": False, "managed": None, "set": None, "differing": [], "assemblies": {}}, result["provenance"]["game"])

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
        rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": result["provenance"],
                                   "stages": [{"id": "rig-check-1", "class": "native", "verdict": "pass"}]}))
        perf = f.dir / "perf.json"
        perf.write_text(json.dumps({"schema": run.SCHEMA, "class": "performance", "verdict": "fail", "reference": "docs/perf/f4.md"}))
        merged_out = f.dir / "merged"
        code = run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--performance", str(perf),
                         "--out", str(merged_out), "--lock", str(f.lock)])
        merged = json.loads((merged_out / "acceptance.json").read_text())
        self.assertEqual(0, code)
        self.assertEqual("pass", merged["summary"]["verdict"])
        self.assertEqual("fail", merged["classes"]["performance"]["verdict"])
        rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": result["provenance"],
                                   "stages": [{"id": "rig-check-1", "class": "native", "verdict": "fail"}]}))
        self.assertEqual(1, run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig),
                                      "--out", str(merged_out), "--lock", str(f.lock)]))


def rig_file(directory, provenance, verdict="pass", evidence=b"rig log", check="rig-check-1"):
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "logs").mkdir(exist_ok=True)
    (directory / "logs/check-1.log").write_bytes(evidence)
    path = directory / "rig.json"
    path.write_text(json.dumps({"schema": run.SCHEMA, "provenance": provenance, "stages": [
        {"id": check, "class": "native", "verdict": verdict,
         "evidence": [{"path": "logs/check-1.log", "sha256": hashlib.sha256(evidence).hexdigest(), "bytes": len(evidence), "retained": False}]}]}))
    return path


class Summarize(unittest.TestCase):
    def offline(self):
        f = Fixture(self)
        f.fake_test("tests", [("PersistentCity.Tests.A.B", "Passed")])
        _, result, _ = f.run()
        return f, result

    def summarize(self, f, *extra):
        out = f.dir / "merged"
        code = run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--out", str(out), "--lock", str(f.lock), *extra])
        return code, json.loads((out / "acceptance.json").read_text()), out

    def test_rig_evidence_from_another_source_is_not_accepted(self):
        f, result = self.offline()
        other = copy.deepcopy(result["provenance"])
        other["source"]["commit"] = "0" * 40
        code, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig", other)))
        stage = next(s for s in merged["stages"] if s["id"] == "rig-check-1")
        self.assertEqual("incomplete", stage["verdict"])
        self.assertIn("is not the offline run's source", stage["reason"])
        self.assertEqual("incomplete", merged["summary"]["verdict"])
        self.assertFalse(merged["importedEvidence"][0]["sameSource"])
        self.assertEqual("0" * 40, merged["importedEvidence"][0]["provenance"]["source"]["commit"])
        dirty = copy.deepcopy(result["provenance"])
        dirty["source"]["dirty"], dirty["source"]["diffSha256"] = True, "1" * 64
        _, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig2", dirty)))
        self.assertEqual("incomplete", merged["summary"]["verdict"])
        _, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig3", result["provenance"])))
        self.assertEqual("pass", merged["summary"]["verdict"])
        self.assertEqual(0, code)

    def test_merged_evidence_resolves_and_matches_in_the_new_directory(self):
        f, result = self.offline()
        _, merged, out = self.summarize(f, "--rig", str(rig_file(f.dir / "rig", result["provenance"])))
        entries = [e for s in merged["stages"] for e in s["evidence"]]
        self.assertIn("rig/0-rig/logs/check-1.log", [e["path"] for e in entries])
        self.assertGreaterEqual(len(entries), 3)
        for entry in entries:
            data = (out / entry["path"]).read_bytes()
            self.assertEqual(entry["sha256"], hashlib.sha256(data).hexdigest())
            self.assertEqual(entry["bytes"], len(data))

    def test_missing_or_altered_evidence_demotes_the_stage(self):
        f, result = self.offline()
        rig = rig_file(f.dir / "rig", result["provenance"])
        (f.dir / "rig/logs/check-1.log").write_bytes(b"edited after the check")
        _, merged, _ = self.summarize(f, "--rig", str(rig))
        stage = next(s for s in merged["stages"] if s["id"] == "rig-check-1")
        self.assertEqual("incomplete", stage["verdict"])
        self.assertIn("does not match its hash", stage["reason"])

    def test_rig_evidence_for_a_different_dll_is_not_accepted(self):
        f, result = self.offline()
        ours = copy.deepcopy(result["provenance"])
        ours["artifact"]["persistentCityDllSha256"] = "a" * 64
        (f.out / "acceptance.json").write_text(json.dumps(dict(result, provenance=ours)))
        theirs = copy.deepcopy(ours)
        theirs["artifact"]["persistentCityDllSha256"] = "b" * 64
        _, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig", theirs)))
        stage = next(s for s in merged["stages"] if s["id"] == "rig-check-1")
        self.assertEqual("incomplete", stage["verdict"])
        self.assertIn("PersistentCity.dll", stage["reason"])
        self.assertFalse(merged["importedEvidence"][0]["sameSource"])
        self.assertEqual("incomplete", merged["summary"]["verdict"])
        _, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig2", ours)))
        self.assertEqual("pass", merged["summary"]["verdict"])
        unrecorded = copy.deepcopy(ours)
        unrecorded["artifact"]["persistentCityDllSha256"] = None  # only one side recorded a hash: no conflict
        _, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig3", unrecorded)))
        self.assertEqual("pass", merged["summary"]["verdict"])

    def test_failed_rig_record_from_another_source_does_not_fail_this_run(self):
        f, result = self.offline()
        other = copy.deepcopy(result["provenance"])
        other["source"]["commit"] = "0" * 40
        code, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig", other, verdict="fail")))
        stage = next(s for s in merged["stages"] if s["id"] == "rig-check-1")
        self.assertEqual("incomplete", stage["verdict"])
        self.assertEqual([], merged["summary"]["failed"])
        self.assertEqual("incomplete", merged["summary"]["verdict"])
        self.assertEqual([{"id": "rig-check-1", "verdict": "fail", "reason": ""}], merged["importedEvidence"][0]["stages"])
        self.assertEqual(0, code)

    def test_successive_merges_never_overwrite_earlier_evidence(self):
        f, result = self.offline()
        out = f.dir / "merged"
        run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--out", str(out), "--lock", str(f.lock),
                  "--rig", str(rig_file(f.dir / "a", result["provenance"], evidence=b"first check", check="rig-check-1"))])
        code = run.main(["summarize", "--result", str(out / "acceptance.json"), "--lock", str(f.lock),
                         "--rig", str(rig_file(f.dir / "b", result["provenance"], evidence=b"second check", check="rig-check-2"))])
        merged = json.loads((out / "acceptance.json").read_text())
        self.assertEqual(0, code)
        self.assertEqual("pass", merged["summary"]["verdict"])
        prefixes = [e["prefix"] for e in merged["importedEvidence"]]
        self.assertEqual(len(prefixes), len(set(prefixes)))
        entries = [e for s in merged["stages"] for e in s["evidence"]]
        rig_paths = [e["path"] for e in entries if e["path"].startswith("rig/")]
        self.assertEqual(2, len(set(rig_paths)))
        for entry in entries:
            data = (out / entry["path"]).read_bytes()
            self.assertEqual(entry["sha256"], hashlib.sha256(data).hexdigest(), entry["path"])

    def test_partial_full_profile_with_passing_rig_stays_incomplete(self):
        f = Fixture(self)
        f.fake_test("unit", [("PersistentCity.Tests.A.B", "Passed")])
        f.fake_test("population", [("PersistentCity.Tests.A.C", "Passed")])
        _, result, _ = f.run("--only", "provenance")
        self.assertEqual(["provenance", "unit", "population"], [s["id"] for s in result["stages"]])
        self.assertEqual("omitted by --only", next(s for s in result["stages"] if s["id"] == "unit")["reason"])
        _, merged, _ = self.summarize(f, "--rig", str(rig_file(f.dir / "rig", result["provenance"])))
        self.assertEqual("incomplete", merged["summary"]["verdict"])
        self.assertEqual(["unit", "population"], merged["summary"]["incomplete"])
        self.assertEqual("incomplete", merged["classes"]["semantic"]["verdict"])


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
        self.assertEqual(["provenance", "tests", "other"], [s["id"] for s in result["stages"]])
        self.assertEqual(["not-run", "pass"], [f.stage_record(result, i)["verdict"] for i in ("tests", "other")])
        self.assertEqual(["other"], result["onlyStages"])
        self.assertEqual("incomplete", result["summary"]["verdict"])
        self.assertEqual(0, code)

    def test_failed_test_build_fails_and_dependent_filters_do_not_run_old_binaries(self):
        f = Fixture(self)
        f.stage(id="test-build", **{"class": "semantic"}, rule="exit-zero", buildsTests=True,
                steps=[["{python}", f.script("build_fail.py", "raise SystemExit('error CS0001: broken')\n")]])
        f.stage(id="population", **{"class": "semantic"}, rule="trx", filter="FullyQualifiedName~F5hIntegratedAcceptanceTests")
        code, result, _ = f.run()
        self.assertEqual("fail", f.stage_record(result, "test-build")["verdict"])
        population = f.stage_record(result, "population")
        self.assertEqual("fail", population["verdict"])
        self.assertIn("test build of this run failed", population["reason"])
        self.assertNotIn("childSeconds", population["metrics"])
        self.assertIn("test-build", result["summary"]["failed"])
        self.assertEqual(1, code)

    def test_filters_use_no_build_only_after_a_successful_test_build(self):
        f = Fixture(self)
        f.stage(id="test-build", **{"class": "semantic"}, rule="exit-zero", buildsTests=True,
                steps=[["{python}", f.script("build_ok.py", "print('built')\n")]])
        f.stage(id="population", **{"class": "semantic"}, rule="trx", filter="FullyQualifiedName~F5hIntegratedAcceptanceTests")
        _, result, _ = f.run()
        self.assertIn("--no-build", f.stage_record(result, "population")["argv"])
        self.assertNotIn("--no-build", run.stage_steps({"id": "x", "filter": "FullyQualifiedName~X"}, None)[0][0])

    def test_test_host_crash_with_an_empty_trx_fails(self):
        f = Fixture(self)
        script = f.script("crash.py", """
            import pathlib, sys
            target = pathlib.Path(sys.argv[1]); target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results /></TestRun>')
            print("The active test run was aborted. Reason: Test host process crashed")
            raise SystemExit(1)
            """)
        f.stage(id="tests", **{"class": "semantic"}, rule="trx", trx="trx/tests.trx", steps=[["{python}", script, "{out}/trx/tests.trx"]])
        _, result, _ = f.run()
        stage = f.stage_record(result, "tests")
        self.assertEqual("fail", stage["verdict"])
        self.assertIn("crash", stage["reason"])

    def test_explicit_sdk_is_first_on_path_for_build_and_recorded(self):
        f = Fixture(self)
        sdk = f.dir / "sdk"
        sdk.mkdir()
        if os.name == "nt":
            host = sdk / "dotnet.cmd"
            host.write_text("@echo 9.9.9-fake\n")
        else:
            host = sdk / "dotnet"
            host.write_text("#!/bin/sh\necho 9.9.9-fake\n")
            host.chmod(0o755)
        f.stage(id="which", **{"class": "semantic"}, rule="exit-zero",
                steps=[["{python}", f.script("which.py", "import shutil, pathlib, sys; pathlib.Path(sys.argv[1]).write_text(shutil.which('dotnet') or '')\n"),
                        "{out}/which.txt"]])
        catalogue = {"schema": "s", "profiles": {"quick": f.profile}, "fixtures": [], "capabilities": [], "settings": [],
                     "stages": [{"id": "provenance", "class": "provenance", "rule": "provenance", "required": False, "steps": []}] + f.stages}
        path = f.dir / "stages.json"
        path.write_text(json.dumps(catalogue))
        run.main(["quick", "--out", str(f.out), "--catalogue", str(path), "--lock", str(f.lock), "--dotnet", str(host)])
        result = json.loads((f.out / "acceptance.json").read_text())
        self.assertEqual("9.9.9-fake", result["provenance"]["host"]["dotnetSdk"])
        self.assertEqual(sdk.resolve(), Path((f.out / "which.txt").read_text()).resolve().parent)

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


# The 16 arms F9a seeded as expected-red from the F2a baseline (docs-history-2026-11-10:docs/checks/f2a.md), used as synthetic red data now that
# F2g recorded the green matrix and F9f emptied the expectation list.
F2A_RED = ["bodies-off/plain-30", "bodies-off/plain-120", "focus/stationary", "focus/sweep", "focus/away-return", "caps/1-1",
           "caps/24-12", "caps/256-64", "lag/ideal", "lag/slow-head-car", "failures/10pct", "failures/50pct", "cadence/30fps",
           "cadence/120fps", "transitions/reload-mid-journey", "transitions/hidden-travel"]


def red(baseline, arms=F2A_RED):
    data = copy.deepcopy(baseline)
    for arm in data["arms"]:
        if arm["arm"] in arms:
            arm["comparison"]["Identical"] = False
    return data


class Movement(unittest.TestCase):
    def setUp(self):
        self.baseline = json.loads(BASELINE.read_text(encoding="utf-8"))
        self.listed = json.loads(EXPECTATIONS.read_text(encoding="utf-8"))["expectedRed"]

    def test_expectation_file_is_emptied_after_f2d(self):
        # F9f: the recorded baseline has every compared arm equal, its self-checks and F2e's E2 rows pass, so the list is empty.
        compared = [a for a in self.baseline["arms"] if a.get("comparison")]
        self.assertTrue(compared)
        self.assertEqual([], [a["arm"] for a in compared if not a["comparison"]["Identical"]])
        self.assertTrue(self.baseline["e2"])
        self.assertTrue(all(c["pass"] for c in self.baseline["selfChecks"] + self.baseline["e2"]))
        self.assertEqual([], self.listed)
        self.assertTrue(set(F2A_RED) <= {a["arm"] for a in compared})

    def test_recorded_baseline_passes_with_the_empty_list(self):
        verdict, reason, metrics, notes = run.assess_movement(self.baseline, self.listed)
        self.assertEqual("pass", verdict)
        self.assertEqual(len([a for a in self.baseline["arms"] if a.get("comparison")]), len(metrics["equalArms"]))
        self.assertEqual([], notes)

    def test_expected_red_arm_passes(self):
        verdict, _, metrics, notes = run.assess_movement(red(self.baseline), F2A_RED)
        self.assertEqual("expected-red", verdict)
        self.assertEqual(sorted(F2A_RED), sorted(metrics["expectedRedArms"]))
        self.assertEqual([], notes)
        summary = run.summarize_result({"stages": [{"id": "movement", "class": "semantic", "required": True, "verdict": verdict},
                                                   {"id": "rig", "class": "native", "required": True, "verdict": "pass", "rig": True}]})
        self.assertEqual("pass", summary["summary"]["verdict"])

    def test_unlisted_differing_arm_fails(self):
        # With the emptied list any differing arm fails the stage.
        verdict, reason, metrics, _ = run.assess_movement(red(self.baseline, ["focus/sweep"]), self.listed)
        self.assertEqual("fail", verdict)
        self.assertEqual(["focus/sweep"], metrics["unexpectedDifferingArms"])
        self.assertIn("focus/sweep", reason)
        listed = [a for a in F2A_RED if a != "focus/sweep"]
        self.assertEqual("fail", run.assess_movement(red(self.baseline), listed)[0])

    def test_listed_arm_that_turns_green_is_reported_not_failed(self):
        data = red(self.baseline, [a for a in F2A_RED if a != "caps/24-12"])
        verdict, _, metrics, notes = run.assess_movement(data, F2A_RED)
        self.assertEqual("expected-red", verdict)
        self.assertEqual(["caps/24-12"], metrics["listedNowGreen"])
        self.assertTrue(any("caps/24-12" in n for n in notes))
        verdict, _, metrics, notes = run.assess_movement(self.baseline, F2A_RED)
        self.assertEqual("pass", verdict)
        self.assertEqual(len(F2A_RED), len(notes))

    def test_failed_self_check_fails(self):
        data = copy.deepcopy(self.baseline)
        data["selfChecks"][0]["pass"] = False
        self.assertEqual("fail", run.assess_movement(data, self.listed)[0])

    def test_failed_e2_replay_row_fails(self):
        # F2e's E2 replay arm is part of the harness output (e2 rows), so the movement stage already runs it.
        data = copy.deepcopy(self.baseline)
        row = next(c for c in data["e2"] if "replay from" in c["check"])
        row["pass"] = False
        verdict, reason, metrics, _ = run.assess_movement(data, self.listed)
        self.assertEqual("fail", verdict)
        self.assertEqual([row["check"]], metrics["failedSelfChecks"])

    def test_city_stages_select_the_f9c_and_f9f_facts(self):
        # F9f's body arms join the city scenario stages: each filter names both classes' prefix and the trait they declare.
        stages = {s["id"]: s for s in json.loads((HERE / "stages.json").read_text())["stages"]}
        sources = [ROOT / "tests/F9IntegratedCityTests.cs", ROOT / "tests/Acceptance/CityMovementArms.cs"]
        for size in ("city-s", "city-m"):
            prefix, trait = re.fullmatch(r"FullyQualifiedName~(\w+)&(\w+=[\w-]+)", stages[size]["filter"]).groups()
            key, value = trait.split("=")
            for source in sources:
                text = source.read_text(encoding="utf-8")
                self.assertIn(f'Trait("{key}", "{value}")', text, source.name)
                self.assertTrue(any(c.startswith(prefix) for c in re.findall(r"public sealed class (\w+)", text)), source.name)

    def test_end_to_end_with_recorded_baseline(self):
        f = Fixture(self)
        copier = f.script("movement.py", f"import shutil, sys; shutil.copy(sys.argv[1], sys.argv[2])\n")
        f.stage(id="movement", **{"class": "semantic"}, rule="movement",
                steps=[["{python}", copier, "tests/movement-equivalence/baseline.json", "{out}/movement.json"]],
                output="movement.json", expectations="tools/acceptance/expectations/movement.json")
        _, result, _ = f.run()
        stage = f.stage_record(result, "movement")
        self.assertEqual("pass", stage["verdict"])
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
        # The unrelated passing rig stage does not clear the two routed native rows.
        self.assertEqual("incomplete", result["summary"]["verdict"])
        self.assertEqual(["native-wholesale-costs", "native-staffed-throughput"], result["summary"]["incomplete"])
        self.assertEqual(2, len(result["classes"]["native"]["pending"]))
        self.assertIn("acceptance.json", stage["digests"])
        self.assertEqual(0, code)

    def test_routed_native_rows_clear_only_through_a_matching_measurement(self):
        stages = [{"id": "economy", "class": "semantic", "required": True, "verdict": "pass",
                   "routed": economy_stage.assess(sample(), compiled_weight=1.2)["routed"]},
                  {"id": "rig-check-2", "class": "native", "required": True, "verdict": "pass", "rig": True}]
        result = run.summarize_result({"stages": copy.deepcopy(stages)})
        self.assertEqual("incomplete", result["summary"]["verdict"])
        self.assertEqual(["native-wholesale-costs", "native-staffed-throughput"], result["summary"]["incomplete"])
        stages += [{"id": "f6b-check-2", "class": "native", "required": True, "verdict": "pass", "rig": True,
                    "measures": "native-staffed-throughput"},
                   {"id": "wholesale", "class": "native", "required": True, "verdict": "fail", "rig": True,
                    "measures": "native-wholesale-costs"}]
        result = run.summarize_result({"stages": copy.deepcopy(stages)})
        self.assertEqual("fail", result["summary"]["verdict"])
        rows = {r["row"]: r for r in result["classes"]["native"]["pending"]}
        self.assertEqual("pass", rows["native-staffed-throughput"]["verdict"])
        self.assertEqual(["f6b-check-2"], rows["native-staffed-throughput"]["measuredBy"])
        stages[-1]["verdict"] = "pass"
        self.assertEqual("pass", run.summarize_result({"stages": stages})["summary"]["verdict"])

    def test_a_stale_passing_report_cannot_hide_a_new_assertion(self):
        payload = json.dumps(sample())
        f = self.economy_fixture(f"""
            import sys, pathlib
            pathlib.Path(sys.argv[3]).write_text({payload!r})
            """)
        self.assertEqual("pass", f.stage_record(f.run()[1], "economy")["verdict"])
        f.script("acceptance.py", """
            assert False, "daily conservation violated"
            """)
        code, result, _ = f.run()
        stage = f.stage_record(result, "economy")
        self.assertEqual("fail", stage["verdict"])
        self.assertIn("wrote no acceptance.json", stage["reason"])
        self.assertFalse((f.out / "f6h/acceptance.json").exists())
        self.assertEqual(1, code)

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


HOLDER = """
import sys, time
sys.path.insert(0, sys.argv[1])
import run
lock = run.RunnerLock(sys.argv[2]).acquire()
print("locked", flush=True)
sys.stdin.read()
"""


class Lock(unittest.TestCase):
    def hold(self, f):
        holder = subprocess.Popen([sys.executable, "-c", HOLDER, str(HERE), str(f.lock)],
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
        self.addCleanup(holder.wait)
        self.addCleanup(holder.stdin.close)
        self.assertEqual("locked", holder.stdout.readline().strip())
        return holder

    def test_lock_refuses_a_second_runner(self):
        f = Fixture(self)
        holder = self.hold(f)
        catalogue = f.dir / "provenance-only.json"
        catalogue.write_text(json.dumps({"profiles": {"quick": ["provenance"]}, "stages": [
            {"id": "provenance", "class": "provenance", "rule": "provenance", "required": False, "steps": []}]}))
        code = run.main(["quick", "--out", str(f.out), "--lock", str(f.lock), "--catalogue", str(catalogue)])
        self.assertEqual(4, code)
        self.assertEqual(holder.pid, run.RunnerLock(f.lock)._holder()["pid"])
        self.assertFalse((f.out / "acceptance.json").exists())

    def test_lock_is_exclusive_while_its_metadata_is_still_empty(self):
        f = Fixture(self)
        first = run.RunnerLock(f.lock)
        first._lock()  # OS lock taken, PID not yet written: the window the old file lock lost
        self.addCleanup(first.release)
        self.assertEqual({}, first._holder())
        with self.assertRaises(run.LockHeld):
            run.RunnerLock(f.lock).acquire()
        first._write()
        with self.assertRaises(run.LockHeld):
            run.RunnerLock(f.lock).acquire()

    def test_lock_clears_a_stale_one(self):
        f = Fixture(self)
        holder = self.hold(f)
        stale_pid = holder.pid
        holder.stdin.close()
        holder.wait()  # the holder exits without releasing; its PID stays in the file
        self.assertEqual(stale_pid, run.RunnerLock(f.lock)._holder()["pid"])
        lock = run.RunnerLock(f.lock)
        with lock:
            self.assertEqual(stale_pid, lock.cleared_stale["pid"])
            self.assertEqual(os.getpid(), run.RunnerLock(f.lock)._holder()["pid"])
            with self.assertRaises(run.LockHeld):
                run.RunnerLock(f.lock).acquire()
        with run.RunnerLock(f.lock) as again:
            self.assertIsNone(again.cleared_stale)

    def test_other_test_processes_are_waited_for_or_refused(self):
        with self.assertRaises(run.LockHeld):
            run.wait_for_dotnet(no_wait=True, probe=lambda: ["testhost.exe 1234"])
        calls = iter([["testhost.exe 1"], []])
        self.assertGreaterEqual(run.wait_for_dotnet(no_wait=False, poll=0, probe=lambda: next(calls)), 0.0)
        self.assertEqual(0.0, run.wait_for_dotnet(no_wait=True, probe=lambda: []))

    def test_probe_sees_dotnet_test_in_every_phase_but_not_builds(self):
        sdk = r"C:\sdk\dotnet.exe"
        rows = [
            ("11", "dotnet.exe", sdk + " test tests/PersistentCity.Tests.csproj"),  # restore/build phase of dotnet test
            ("12", "dotnet.exe", '"' + sdk + '" exec C:\\sdk\\10.0\\vstest.console.dll --framework:x'),
            ("13", "testhost.exe", r'"C:\w\tests\bin\testhost.exe" --port 1'),
            ("14", "dotnet", "/usr/bin/dotnet test --filter X"),
            ("15", "dotnet.exe", '"' + sdk + '" "C:\\sdk\\MSBuild.dll" /nodemode:1 /nodeReuse:false'),
            ("16", "dotnet.exe", sdk + " build tests/PersistentCity.Tests.csproj"),
            ("17", "dotnet.exe", sdk + " run --project tests/movement-equivalence -- --out x"),
            ("99", "dotnet.exe", sdk + " test"),
        ]
        self.assertEqual(["dotnet.exe 11", "dotnet.exe 12", "testhost.exe 13", "dotnet 14"],
                         run.busy_test_processes(rows, own=99))

    def test_probe_runs_on_this_machine(self):
        self.assertIsInstance(run.other_test_processes(), list)

    def test_every_catalogue_stage_that_runs_dotnet_is_guarded(self):
        catalogue = json.loads((HERE / "stages.json").read_text())
        for stage in catalogue["stages"]:
            steps = run.stage_steps(stage)[0]
            text = json.dumps(steps)
            if "{dotnet}" in text or "build.py" in text:
                self.assertTrue(stage.get("dotnet"), stage["id"] + " runs dotnet without the process guard")


if __name__ == "__main__":
    unittest.main()
