import copy
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import economy_stage  # noqa: E402
import rig_manifest as rm  # noqa: E402
import run  # noqa: E402
from test_run import Fixture  # noqa: E402

ROOT = HERE.parents[1]
CHECKS = [{"id": "c-first", "title": "provenance", "mode": "locked-PC", "existing": [], "pending": [], "pinned": "first"},
          {"id": "c-a", "title": "a", "mode": "locked-PC", "existing": [], "pending": []},
          {"id": "c-b", "title": "b", "mode": "input-only", "existing": [], "pending": []},
          {"id": "c-c", "title": "c", "mode": "locked-PC", "existing": [], "pending": []},
          {"id": "c-last", "title": "merge", "mode": "locked-PC", "existing": [], "pending": [], "pinned": "last"}]


class Repo:

    def __init__(self, test):
        self.tmp = tempfile.TemporaryDirectory()
        test.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)
        env = {"GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@example.invalid", "GIT_COMMITTER_NAME": "t",
               "GIT_COMMITTER_EMAIL": "t@example.invalid", "GIT_CONFIG_NOSYSTEM": "1"}
        self.env = dict(os.environ, **env)
        self.git("init", "-q", "-b", "main")
        self.git("config", "commit.gpgsign", "false")
        self.counter = 0

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.dir, env=self.env, capture_output=True, text=True, check=True).stdout.strip()

    def commit(self, *paths, message=None):
        for path in paths:
            target = self.dir / path
            target.parent.mkdir(parents=True, exist_ok=True)
            self.counter += 1
            target.write_text(f"change {self.counter}\n")
        self.git("add", "-A")
        self.git("commit", "-q", "--allow-empty", "-m", message or f"commit {self.counter}")
        return self.git("rev-parse", "HEAD")

    def merge_side(self, *side_commits):
        branch = f"side{self.counter}"
        self.git("checkout", "-q", "-b", branch)
        for paths in side_commits:
            self.commit(*paths)
        self.git("checkout", "-q", "main")
        self.git("merge", "-q", "--no-ff", "-m", "merge side", branch)
        return self.git("rev-parse", "HEAD")


def row(row_id, paths, check, last=None, recorded=None, fingerprint="set-a"):
    return {"id": row_id, "assumption": row_id, "records": [], "paths": paths, "rigCheck": check, "recordedAt": recorded,
            "lastRigHead": last, "lastRigFingerprint": fingerprint if last else None, "lastRigEvidence": None}


def ledger(*rows):
    return {"schema": rm.SCHEMA, "debtThreshold": 3, "checks": copy.deepcopy(CHECKS), "rows": list(rows)}


def by_id(state):
    return {r["id"]: r for r in state["rows"]}


class Staleness(unittest.TestCase):
    def test_stale_from_synthetic_git_history(self):
        repo = Repo(self)
        base = repo.commit("Core/Alpha.cs", "Runtime/Beta.cs", "docs/x.md")
        repo.commit("Core/Alpha.cs")
        repo.commit("docs/x.md")
        state = rm.assess(ledger(row("alpha", ["Core/Al*.cs"], "c-a", base), row("beta", ["Runtime/**/*.cs"], "c-b", base),
                                 row("docs", ["docs/*.md"], "c-c", base)), repo.dir, fingerprint="set-a")
        rows = by_id(state)
        self.assertEqual(("stale", 1), (rows["alpha"]["status"], rows["alpha"]["debt"]))
        self.assertEqual(("fresh", 0), (rows["beta"]["status"], rows["beta"]["debt"]))
        self.assertEqual(("stale", 1), (rows["docs"]["status"], rows["docs"]["debt"]))
        self.assertEqual(["alpha", "docs"], state["stale"])
        self.assertFalse(state["batchDue"])
        self.assertEqual(["c-a", "c-c"], state["requiredChecks"])
        self.assertEqual(repo.git("rev-parse", "HEAD"), state["head"])

    def test_debt_counts_first_parent_commits_that_touched_the_paths(self):
        repo = Repo(self)
        base = repo.commit("Core/Alpha.cs", "Core/Other.cs")
        repo.commit("Core/Alpha.cs")
        repo.commit("Core/Other.cs")
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", base)), repo.dir, fingerprint="set-a")
        self.assertEqual(1, by_id(state)["alpha"]["debt"])
        self.assertFalse(state["batchDue"])
        repo.merge_side(["Core/Alpha.cs"], ["Core/Alpha.cs"])
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", base)), repo.dir, fingerprint="set-a")
        self.assertEqual(2, by_id(state)["alpha"]["debt"])
        self.assertFalse(state["batchDue"])
        touched = repo.commit("Core/Alpha.cs")
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", base)), repo.dir, fingerprint="set-a")
        alpha = by_id(state)["alpha"]
        self.assertEqual(3, alpha["debt"])
        self.assertEqual(touched[:12], alpha["touchedBy"][0])
        self.assertTrue(state["batchDue"])
        self.assertEqual(["alpha has debt >= 3"], state["batchDueReasons"])
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", touched)), repo.dir, fingerprint="set-a")
        self.assertEqual(("fresh", 0), (by_id(state)["alpha"]["status"], by_id(state)["alpha"]["debt"]))
        self.assertEqual([], state["requiredChecks"])

    def test_side_branch_evidence_head_is_resolved_off_the_chain(self):
        repo = Repo(self)
        repo.commit("Core/Alpha.cs")
        repo.git("checkout", "-q", "-b", "side")
        side = repo.commit("Core/Alpha.cs")
        repo.git("checkout", "-q", "main")
        repo.git("merge", "-q", "--no-ff", "-m", "merge", "side")
        repo.commit("Core/Alpha.cs")
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", side)), repo.dir, fingerprint="set-a")
        # Conservative: the merge that brought the evidenced commit in also counts, as does the later commit.
        self.assertEqual(2, by_id(state)["alpha"]["debt"])
        self.assertEqual(2, state["gitQueries"])

    def test_row_without_rig_evidence_is_unproven_with_debt_since_its_record(self):
        repo = Repo(self)
        recorded = repo.commit("docs/record.md", "Core/Alpha.cs")
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", recorded=recorded)), repo.dir, fingerprint="set-a")
        self.assertEqual(("unproven", 0), (by_id(state)["alpha"]["status"], by_id(state)["alpha"]["debt"]))
        self.assertEqual(["alpha"], state["unproven"])
        self.assertEqual(["c-a"], state["requiredChecks"])
        for _ in range(3):
            repo.commit("Core/Alpha.cs")
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", recorded=recorded)), repo.dir, fingerprint="set-a")
        self.assertEqual(("unproven", 3), (by_id(state)["alpha"]["status"], by_id(state)["alpha"]["debt"]))
        self.assertTrue(state["batchDue"])

    def test_evidence_head_outside_the_history_is_stale(self):
        repo = Repo(self)
        repo.commit("Core/Alpha.cs")
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", "0" * 40)), repo.dir, fingerprint="set-a")
        alpha = by_id(state)["alpha"]
        self.assertEqual(("stale", None), (alpha["status"], alpha["debt"]))
        self.assertIn("not in this history", alpha["reasons"][0])

    def test_fingerprint_change_marks_every_row_stale(self):
        repo = Repo(self)
        base = repo.commit("Core/Alpha.cs", "Runtime/Beta.cs")
        rows = [row("alpha", ["Core/Alpha.cs"], "c-a", base), row("beta", ["Runtime/Beta.cs"], "c-b", base),
                row("gamma", ["Runtime/Beta.cs"], "c-c", recorded=base)]
        self.assertEqual([], rm.assess(ledger(*rows), repo.dir, fingerprint="set-a")["stale"])
        for fingerprint, current in (("set-b", None), (None, "set-b"), ("unknown", "set-a")):
            state = rm.assess(ledger(*rows), repo.dir, fingerprint=fingerprint, current=current)
            self.assertEqual(["alpha", "beta", "gamma"], state["stale"], fingerprint)
            self.assertTrue(state["fingerprintChanged"])
            self.assertTrue(state["batchDue"])
            self.assertIn("game fingerprint changed", state["batchDueReasons"])
            self.assertEqual(["c-a", "c-b", "c-c"], state["requiredChecks"])
        self.assertEqual([], rm.assess(ledger(*rows), repo.dir, fingerprint=None, current="set-a")["stale"])

    def test_rows_proved_on_the_new_fingerprint_stay_fresh_after_a_change(self):
        repo = Repo(self)
        base = repo.commit("Core/Alpha.cs", "Runtime/Beta.cs")
        rows = [row("alpha", ["Core/Alpha.cs"], "c-a", base, fingerprint="set-b"),
                row("beta", ["Runtime/Beta.cs"], "c-b", base, fingerprint="set-a"),
                row("gamma", ["Runtime/Beta.cs"], "c-c", recorded=base)]
        state = rm.assess(ledger(*rows), repo.dir, fingerprint="set-b")
        statuses = {r["id"]: r["status"] for r in state["rows"]}
        self.assertEqual({"alpha": "fresh", "beta": "stale", "gamma": "stale"}, statuses)
        self.assertTrue(state["fingerprintChanged"])
        self.assertEqual(["c-b", "c-c"], state["requiredChecks"])
        self.assertEqual(["alpha", "beta", "gamma"], rm.assess(ledger(*rows), repo.dir, fingerprint="unknown")["stale"])

    def test_globs(self):
        self.assertTrue(rm.glob_regex("Runtime/PhysicalPopulation*.cs").match("Runtime/PhysicalPopulation.Queues.cs"))
        self.assertFalse(rm.glob_regex("Runtime/PhysicalPopulation*.cs").match("Runtime/x/PhysicalPopulation.cs"))
        self.assertTrue(rm.glob_regex("tools/**/x.py").match("tools/x.py"))
        self.assertTrue(rm.glob_regex("tools/**/x.py").match("tools/a/b/x.py"))
        self.assertFalse(rm.glob_regex("Core/A?.cs").match("Core/Ab/.cs"))
        self.assertFalse(rm.glob_regex("Core/Alpha.cs").match("Core/Alpha.csx"))


class Manifest(unittest.TestCase):
    def test_manifest_orders_stale_rows_first(self):
        repo = Repo(self)
        base = repo.commit("A.cs", "B.cs", "C.cs")
        head = repo.commit("C.cs")
        repo.commit("C.cs")
        repo.commit("B.cs")
        led = ledger(row("fresh", ["A.cs"], "c-a", head), row("unproven", ["A.cs"], "c-b", recorded=base),
                     row("stale-1", ["B.cs"], "c-c", head), row("stale-2", ["C.cs"], "c-a", base),
                     dict(row("unscheduled", ["A.cs"], None, recorded=base), pendingCapability="RIG-X"))
        doc = rm.manifest(led, rm.assess(led, repo.dir, fingerprint="set-a"))
        order = [c["id"] for c in doc["checks"]]
        self.assertEqual(["c-first", "c-a", "c-c", "c-b", "c-last"], order)
        checks = {c["id"]: c for c in doc["checks"]}
        self.assertEqual(("stale", 2, ["fresh", "stale-2"]), (checks["c-a"]["status"], checks["c-a"]["debt"], checks["c-a"]["rows"]))
        self.assertEqual("unproven", checks["c-b"]["status"])
        self.assertTrue(checks["c-b"]["due"])
        self.assertEqual([{"row": "unscheduled", "status": "unproven", "debt": 0, "pendingCapability": "RIG-X"}], doc["unscheduled"])
        self.assertIn("c-a", rm.render(doc))

    def test_fresh_checks_follow_due_checks(self):
        repo = Repo(self)
        head = repo.commit("A.cs", "B.cs")
        led = ledger(row("fresh", ["A.cs"], "c-a", head), row("unproven", ["B.cs"], "c-c", recorded=head))
        order = [c["id"] for c in rm.manifest(led, rm.assess(led, repo.dir, fingerprint="set-a"))["checks"]]
        self.assertEqual(["c-first", "c-c", "c-a", "c-b", "c-last"], order)

    def test_rig_manifest_profile_writes_the_manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(0, run.main(["rig-manifest", "--out", tmp]))
            doc = json.loads((Path(tmp) / "rig-manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(rm.MANIFEST_SCHEMA, doc["schema"])
        ids = [c["id"] for c in doc["checks"]]
        self.assertEqual("f9-1", ids[0])
        self.assertEqual("f9-10", ids[-1])
        self.assertEqual(sorted(f"f9-{i}" for i in range(1, 11)), sorted(i for i in ids if i.startswith("f9-")))
        due = [c["due"] for c in doc["checks"][1:-1]]
        self.assertEqual(sorted(due, reverse=True), due)


def native_result(required_checks, *rig_stages, stale=(), due=False):
    stages = [{"id": "unit", "class": "semantic", "required": True, "verdict": "pass"}]
    stages += [dict(s, **{"class": "native", "required": True, "rig": True}) for s in rig_stages]
    evidence = {"status": "assessed", "requiredChecks": list(required_checks), "batchDue": due,
                "batchDueReasons": ["x has debt >= 3"] if due else [], "stale": list(stale), "unproven": []}
    return run.summarize_result({"stages": stages, "classes": {}, "nativeEvidence": evidence})


class NativeClass(unittest.TestCase):
    def test_merged_rig_result_with_a_failed_check_makes_native_fail(self):
        result = native_result(["f9-4", "f9-7"], {"id": "f9-4", "verdict": "fail"}, {"id": "f9-7", "verdict": "pass"})
        self.assertEqual("fail", result["classes"]["native"]["verdict"])
        self.assertEqual("fail", result["summary"]["verdict"])
        self.assertEqual(["f9-4"], result["summary"]["failed"])
        result = native_result([], {"id": "f9-3", "verdict": "fail"})
        self.assertEqual("fail", result["classes"]["native"]["verdict"])

    def test_missing_rig_results_make_native_incomplete(self):
        none = native_result(["f9-4", "f9-7"])
        self.assertEqual("incomplete", none["classes"]["native"]["verdict"])
        self.assertEqual(["f9-4", "f9-7"], none["summary"]["incomplete"])
        self.assertNotIn("rig", none["summary"]["required"])
        partial = native_result(["f9-4", "f9-7"], {"id": "f9-7", "verdict": "pass"})
        self.assertEqual("incomplete", partial["classes"]["native"]["verdict"])
        self.assertEqual(["f9-4"], partial["summary"]["incomplete"])
        whole = native_result(["f9-4", "f9-7"], {"id": "rig-batch", "checks": ["f9-4", "f5h-4"], "verdict": "pass"},
                              {"id": "f9-7", "verdict": "pass"})
        self.assertEqual("pass", whole["classes"]["native"]["verdict"])
        self.assertEqual("pass", whole["summary"]["verdict"])

    def test_batch_due_and_stale_rows_are_listed_but_never_change_the_verdict(self):
        result = native_result(["f9-4"], {"id": "f9-4", "verdict": "pass"}, stale=["save-epoch"], due=True)
        self.assertEqual("pass", result["summary"]["verdict"])
        self.assertTrue(result["summary"]["nativeBatchDue"])
        self.assertEqual(["save-epoch"], result["summary"]["staleNativeRows"])
        self.assertIn("native batch due: x has debt >= 3", result["summary"]["notes"])

    def test_without_a_ledger_the_f9a_placeholder_stays(self):
        result = run.summarize_result({"stages": [{"id": "unit", "class": "semantic", "required": True, "verdict": "pass"}]})
        self.assertEqual(["rig"], result["summary"]["incomplete"])
        self.assertNotIn("nativeBatchDue", result["summary"])


def seed_ledger(source):
    seeded = copy.deepcopy(source)
    for r in seeded["rows"]:
        r["lastRigHead"] = r["lastRigFingerprint"] = r["lastRigEvidence"] = None
    return seeded


class SummarizeWithLedger(unittest.TestCase):

    def offline(self):
        f = Fixture(self)
        self.ledger_path = f.dir / "native-assumptions.json"
        self.ledger_path.write_text(json.dumps(seed_ledger(rm.load())), encoding="utf-8")
        managed = f.dir / "Managed"
        managed.mkdir()
        current = rm.declared_set()
        script = f.script("build.py", f"""
            import json, sys
            out = sys.argv[sys.argv.index("--provenance") + 1]
            open(out, "w").write(json.dumps({{"set": "{current}", "differing": [], "assemblies": {{}}}}))
            print("DLL SHA256: {'ab' * 32}")
            """)
        f.stage(id="build", **{"class": "native"}, rule="build", provenance="build.json",
                steps=[["{python}", script, "--game-managed", "{managed}", "--provenance", "{out}/build.json"]])
        return f, managed, current

    def run_offline(self, f, managed):
        catalogue = {"schema": "persistentcity-acceptance-stages-1", "nativeAssumptions": str(self.ledger_path),
                     "profiles": {"quick": f.profile, "full": f.profile},
                     "stages": [{"id": "provenance", "class": "provenance", "rule": "provenance", "required": False, "steps": []}] + f.stages,
                     "fixtures": [], "settings": [], "capabilities": []}
        path = f.dir / "stages.json"
        path.write_text(json.dumps(catalogue), encoding="utf-8")
        run.main(["quick", "--out", str(f.out), "--catalogue", str(path), "--lock", str(f.lock), "--dotnet", "missing-dotnet-for-tests",
                  "--game-managed", str(managed)])
        return path, json.loads((f.out / "acceptance.json").read_text(encoding="utf-8"))

    def test_rig_results_fill_the_required_checks_and_the_ledger_records_the_head(self):
        f, managed, current = self.offline()
        catalogue, result = self.run_offline(f, managed)
        evidence = result["nativeEvidence"]
        self.assertEqual("assessed", evidence["status"])
        self.assertEqual(current, evidence["fingerprint"])
        self.assertEqual(hashlib.sha256(self.ledger_path.read_bytes()).hexdigest(), evidence["ledgerSha256"])
        required = evidence["requiredChecks"]
        self.assertTrue(required)
        self.assertEqual("incomplete", result["classes"]["native"]["verdict"])
        self.assertEqual(sorted(required), sorted(result["summary"]["incomplete"]))
        self.assertIn("nativeBatchDue", result["summary"])
        rig = f.dir / "rig.json"
        stages = [{"id": c, "class": "native", "verdict": "pass"} for c in required]
        rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": result["provenance"], "stages": stages[:-1]}))
        out = f.dir / "merged"
        run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--out", str(out),
                  "--lock", str(f.lock), "--catalogue", str(catalogue)])
        merged = json.loads((out / "acceptance.json").read_text())
        self.assertEqual([required[-1]], merged["summary"]["incomplete"])
        rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": result["provenance"], "stages": stages}))
        before = (ROOT / "tools/acceptance/native-assumptions.json").read_bytes()
        code = run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--out", str(out),
                         "--lock", str(f.lock), "--catalogue", str(catalogue)])
        merged = json.loads((out / "acceptance.json").read_text())
        self.assertEqual(0, code)
        self.assertEqual("pass", merged["classes"]["native"]["verdict"])
        self.assertEqual("pass", merged["summary"]["verdict"])
        updated = json.loads((out / "native-assumptions.json").read_text())
        original = {r["id"]: r for r in rm.load(self.ledger_path)["rows"]}
        commit = result["provenance"]["source"]["commit"]
        expected = sorted(r["id"] for r in updated["rows"] if r["rigCheck"] in required)
        self.assertEqual(expected, sorted(merged["nativeEvidence"]["updatedRows"]))
        for r in updated["rows"]:
            if r["id"] in expected:
                self.assertEqual((commit, current), (r["lastRigHead"], r["lastRigFingerprint"]))
            else:
                self.assertEqual(original[r["id"]], r)
        self.assertEqual(before, (ROOT / "tools/acceptance/native-assumptions.json").read_bytes())
        manifest_out = f.dir / "manifest"
        self.assertEqual(0, run.main(["rig-manifest", "--result", str(out / "acceptance.json"), "--out", str(manifest_out),
                                      "--catalogue", str(catalogue)]))
        doc = json.loads((manifest_out / "rig-manifest.json").read_text())
        statuses = {r["id"]: r["status"] for r in doc["ledger"]["rows"]}
        self.assertTrue(all(statuses[i] == "fresh" for i in expected), statuses)
        state = rm.assess(updated, ROOT, commit, current)
        self.assertTrue(set(expected) <= {r["id"] for r in state["rows"] if r["status"] == "fresh"})

    def test_a_failed_rig_check_fails_the_merged_run_and_updates_no_row_for_it(self):
        f, managed, current = self.offline()
        catalogue, result = self.run_offline(f, managed)
        required = result["nativeEvidence"]["requiredChecks"]
        rig = f.dir / "rig.json"
        stages = [{"id": c, "class": "native", "verdict": "fail" if c == "f9-4" else "pass"} for c in required]
        rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": result["provenance"], "stages": stages}))
        out = f.dir / "merged"
        code = run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--out", str(out),
                         "--lock", str(f.lock), "--catalogue", str(catalogue)])
        merged = json.loads((out / "acceptance.json").read_text())
        self.assertEqual(1, code)
        self.assertEqual("fail", merged["classes"]["native"]["verdict"])
        self.assertEqual(["f9-4"], merged["summary"]["failed"])
        updated = {r["id"]: r for r in json.loads((out / "native-assumptions.json").read_text())["rows"]}
        self.assertIsNone(updated["save-epoch"]["lastRigHead"])

    def test_rig_evidence_on_another_or_unknown_game_never_passes_native(self):
        f, managed, current = self.offline()
        catalogue, result = self.run_offline(f, managed)
        required = result["nativeEvidence"]["requiredChecks"]
        stages = [{"id": c, "class": "native", "verdict": "pass"} for c in required]
        out = f.dir / "merged"
        for game, reason in (({"supplied": True, "set": "unknown"}, "match no declared fingerprint"),
                             ({"supplied": True, "set": "other-game-build"}, "is not the offline build's"),
                             ({"supplied": True, "set": None}, "records no game fingerprint"),
                             (None, "records no game fingerprint")):
            provenance = copy.deepcopy(result["provenance"])
            if game is None:
                provenance.pop("game")
            else:
                provenance["game"] = dict(provenance["game"], **game)
            rig = f.dir / "rig.json"
            rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": provenance, "stages": stages}))
            code = run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--out", str(out),
                             "--lock", str(f.lock), "--catalogue", str(catalogue)])
            merged = json.loads((out / "acceptance.json").read_text())
            self.assertEqual(0, code, game)
            self.assertEqual("incomplete", merged["classes"]["native"]["verdict"], game)
            self.assertEqual("incomplete", merged["summary"]["verdict"], game)
            self.assertEqual(sorted(required), sorted(merged["summary"]["incomplete"]), game)
            rig_stages = [s for s in merged["stages"] if s.get("rig")]
            self.assertTrue(rig_stages and all(reason in s["reason"] for s in rig_stages), game)
            self.assertEqual([], merged["nativeEvidence"].get("updatedRows", []), game)

    def test_rig_set_other_than_the_declared_current_is_refused_when_the_offline_run_hashed_nothing(self):
        prov = {"source": {"commit": "a" * 40, "dirty": False, "diffSha256": None}, "game": {"supplied": False, "set": None}}
        rig = copy.deepcopy(prov)
        rig["game"] = {"supplied": True, "set": "other-game-build"}
        self.assertIn("declared current set", run.identity_problem({"provenance": prov}, {"provenance": rig}))
        rig["game"]["set"] = rm.declared_set()
        self.assertIsNone(run.identity_problem({"provenance": prov}, {"provenance": rig}))

    def test_merge_fixture_does_not_depend_on_committed_evidence(self):
        real = rm.load()
        head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
        partial, _ = rm.update(real, [{"id": "f9-4", "verdict": "pass"}], head, rm.declared_set(), "run-1")
        complete, _ = rm.update(real, [{"id": c["id"], "verdict": "pass"} for c in real["checks"]], head, rm.declared_set(), "run-2")
        self.assertEqual(seed_ledger(real), seed_ledger(partial))
        self.assertEqual(seed_ledger(real), seed_ledger(complete))
        state = rm.assess(seed_ledger(complete), ROOT, head, rm.declared_set())
        self.assertEqual(sorted({r["rigCheck"] for r in real["rows"] if r["rigCheck"]}), sorted(state["requiredChecks"]))
        self.assertIn("f9-4", state["requiredChecks"])

    def test_update_needs_a_declared_fingerprint(self):
        led = rm.load()
        for fingerprint in (None, "unknown"):
            _, rows = rm.update(led, [{"id": "f9-4", "verdict": "pass"}], "a" * 40, fingerprint, "run")
            self.assertEqual([], rows)
        _, rows = rm.update(led, [{"id": "batch", "checks": ["f9-4"], "verdict": "pass"}], "a" * 40, "set", "run")
        self.assertIn("save-epoch", rows)


class SeededLedger(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.ledger = rm.load()
        cls.tracked = subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.splitlines()

    def test_rows_are_seeded_from_the_plan_records(self):
        ids = [r["id"] for r in self.ledger["rows"]]
        self.assertEqual(len(ids), len(set(ids)))
        for wanted in ("save-epoch", "buylive-sale-authority", "hourly-settlement", "agency-billing", "streetfood-bridge", "subway-portals"):
            self.assertIn(wanted, ids)
        for routed in [r["row"] for r in economy_stage.ROUTES.values() if r["class"] == "native"]:
            self.assertIn(routed, ids)

    def test_every_record_glob_and_check_exists(self):
        checks = {c["id"] for c in self.ledger["checks"]}
        self.assertEqual({f"f9-{i}" for i in range(1, 11)}, {c for c in checks if c.startswith("f9-")})
        self.assertTrue(all(c["mode"] in ("locked-PC", "input-only") for c in self.ledger["checks"]))
        for r in self.ledger["rows"]:
            self.assertTrue(r["records"], r["id"])
            for record in r["records"]:
                path = record.split("#")[0]
                if ":" in path:
                    tag, inner = path.split(":", 1)
                    if subprocess.run(["git", "rev-parse", "--verify", "--quiet", "refs/tags/" + tag], cwd=ROOT,
                                      capture_output=True).returncode != 0:
                        continue
                    self.assertEqual(0, subprocess.run(["git", "cat-file", "-e", path], cwd=ROOT).returncode, record)
                else:
                    self.assertTrue((ROOT / path).is_file(), record)
            for pattern in r["paths"]:
                regex = rm.glob_regex(pattern)
                self.assertTrue(any(regex.match(f) for f in self.tracked), f"{r['id']}: {pattern} matches no tracked file")
            if r["rigCheck"] is None:
                self.assertTrue(r.get("pendingCapability"), r["id"])
            else:
                self.assertIn(r["rigCheck"], checks)
            self.assert_evidence(r)

    def assert_evidence(self, r):
        fields = (r["lastRigHead"], r["lastRigFingerprint"], r["lastRigEvidence"])
        if fields[0] is None:
            self.assertEqual((None, None, None), fields, r["id"])
            return
        self.assertRegex(r["lastRigHead"], r"^[0-9a-f]{7,40}$", r["id"])
        declared = [x["name"] for x in json.loads(rm.SUPPORTED.read_text(encoding="utf-8"))["sets"]]
        self.assertIn(r["lastRigFingerprint"], declared, r["id"])
        self.assertTrue(isinstance(r["lastRigEvidence"], str) and r["lastRigEvidence"], r["id"])
        self.assertIsNotNone(r["rigCheck"], r["id"])

    def test_a_ledger_updated_by_summarize_still_validates(self):
        head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
        updated, rows = rm.update(self.ledger, [{"id": "f9-4", "verdict": "pass"}], head, rm.declared_set(), "run-1")
        self.assertIn("save-epoch", rows)
        for r in updated["rows"]:
            self.assert_evidence(r)
        state = rm.assess(updated, ROOT, head, None, rm.declared_set())
        self.assertEqual("fresh", {r["id"]: r for r in state["rows"]}["save-epoch"]["status"])

    @unittest.skipIf(subprocess.run(["git", "rev-parse", "--is-shallow-repository"], cwd=ROOT, capture_output=True,
                                    text=True).stdout.strip() == "true", "shallow clone")
    def test_record_commits_are_ancestors_of_this_head(self):
        for r in self.ledger["rows"]:
            for revision in filter(None, (r["recordedAt"], r["lastRigHead"])):
                code = subprocess.run(["git", "merge-base", "--is-ancestor", revision, "HEAD"], cwd=ROOT).returncode
                self.assertEqual(0, code, r["id"])
        state = rm.assess(self.ledger, ROOT, "HEAD", None, rm.declared_set())
        self.assertTrue(all(r["debt"] is not None for r in state["rows"]))
        self.assertEqual(1, state["gitQueries"])


if __name__ == "__main__":
    unittest.main()
