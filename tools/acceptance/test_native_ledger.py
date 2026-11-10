"""F9d native-evidence ledger and rig manifest (Decision 7). python -m unittest discover -s tools/acceptance -p test_*.py

Staleness and debt run on a synthetic Git history in a temporary repository; the merge cases run the runner's own
summarize on fake results. The real ledger is checked for records, globs and checks that exist at this head.
"""
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
    """A throwaway Git repository with a first-parent chain, side branches and merges."""

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
        """A side branch with one commit per path list, merged with --no-ff: one first-parent commit."""
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
        repo.commit("Core/Other.cs")  # not a dependent path
        state = rm.assess(ledger(row("alpha", ["Core/Alpha.cs"], "c-a", base)), repo.dir, fingerprint="set-a")
        self.assertEqual(1, by_id(state)["alpha"]["debt"])
        self.assertFalse(state["batchDue"])
        # A merge of a side branch with two touching commits is one merge into the chain: debt 2, still below 3.
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
        # Evidence at the head clears it.
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
        # The declared current set is used when the run hashed no assemblies.
        self.assertEqual([], rm.assess(ledger(*rows), repo.dir, fingerprint=None, current="set-a")["stale"])

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
        # Pinned first; c-a (stale-2, debt 2) before c-c (stale-1, debt 1); then the unproven check; pinned last.
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
        self.assertEqual(sorted(due, reverse=True), due)  # due checks before checks with only fresh rows


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
        # A failed check that no stale row needs still fails: rig evidence is never ignored.
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


class SummarizeWithLedger(unittest.TestCase):
    """End to end on the real ledger at this head: offline run, rig file for every required check, summarize."""

    def offline(self):
        f = Fixture(self)
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
        catalogue = {"schema": "persistentcity-acceptance-stages-1", "nativeAssumptions": "tools/acceptance/native-assumptions.json",
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
        self.assertEqual(hashlib.sha256((ROOT / evidence["ledger"]).read_bytes()).hexdigest(), evidence["ledgerSha256"])
        required = evidence["requiredChecks"]
        self.assertTrue(required)
        self.assertEqual("incomplete", result["classes"]["native"]["verdict"])
        self.assertEqual(sorted(required), sorted(result["summary"]["incomplete"]))
        self.assertIn("nativeBatchDue", result["summary"])
        # The rig batch reports every check but the last one: still incomplete.
        rig = f.dir / "rig.json"
        stages = [{"id": c, "class": "native", "verdict": "pass"} for c in required]
        rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": result["provenance"], "stages": stages[:-1]}))
        out = f.dir / "merged"
        run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--out", str(out),
                  "--lock", str(f.lock), "--catalogue", str(catalogue)])
        merged = json.loads((out / "acceptance.json").read_text())
        self.assertEqual([required[-1]], merged["summary"]["incomplete"])
        # All of them: native passes, and the updated ledger records this head for every row whose check passed.
        rig.write_text(json.dumps({"schema": run.SCHEMA, "provenance": result["provenance"], "stages": stages}))
        before = (ROOT / "tools/acceptance/native-assumptions.json").read_bytes()
        code = run.main(["summarize", "--result", str(f.out / "acceptance.json"), "--rig", str(rig), "--out", str(out),
                         "--lock", str(f.lock), "--catalogue", str(catalogue)])
        merged = json.loads((out / "acceptance.json").read_text())
        self.assertEqual(0, code)
        self.assertEqual("pass", merged["classes"]["native"]["verdict"])
        self.assertEqual("pass", merged["summary"]["verdict"])
        updated = json.loads((out / "native-assumptions.json").read_text())
        commit = result["provenance"]["source"]["commit"]
        expected = sorted(r["id"] for r in updated["rows"] if r["rigCheck"] in required)
        self.assertEqual(expected, sorted(merged["nativeEvidence"]["updatedRows"]))
        for r in updated["rows"]:
            if r["id"] in expected:
                self.assertEqual((commit, current), (r["lastRigHead"], r["lastRigFingerprint"]))
            else:
                self.assertIsNone(r["lastRigHead"])
        self.assertEqual(before, (ROOT / "tools/acceptance/native-assumptions.json").read_bytes())  # no --update-ledger
        # The updated ledger at the same head and set: every row with a passed check is fresh.
        state = rm.assess(updated, ROOT, commit, current)
        self.assertEqual(sorted(expected), sorted(r["id"] for r in state["rows"] if r["status"] == "fresh"))

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
        self.assertIsNone(updated["save-epoch"]["lastRigHead"])  # proved by f9-4

    def test_update_needs_a_declared_fingerprint(self):
        led = rm.load()
        for fingerprint in (None, "unknown"):
            _, rows = rm.update(led, [{"id": "f9-4", "verdict": "pass"}], "a" * 40, fingerprint, "run")
            self.assertEqual([], rows)
        _, rows = rm.update(led, [{"id": "batch", "checks": ["f9-4"], "verdict": "pass"}], "a" * 40, "set", "run")
        self.assertIn("save-epoch", rows)


class SeededLedger(unittest.TestCase):
    """The committed ledger: rows seeded from existing records, each pointing at files and checks that exist."""

    @classmethod
    def setUpClass(cls):
        cls.ledger = rm.load()
        cls.tracked = subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.splitlines()

    def test_rows_are_seeded_from_the_plan_records(self):
        ids = [r["id"] for r in self.ledger["rows"]]
        self.assertEqual(len(ids), len(set(ids)))
        for wanted in ("save-epoch", "buylive-sale-authority", "hourly-settlement", "agency-billing", "streetfood-bridge", "subway-portals"):
            self.assertIn(wanted, ids)
        # The economy stage routes two native rows; the ledger holds both under the same IDs.
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
                    # A record moved to the docs-history tag on 10 Nov 2026 (docs/decisions/2026-11-10-lean-docs.md).
                    tag, inner = path.split(":", 1)
                    if subprocess.run(["git", "rev-parse", "--verify", "--quiet", "refs/tags/" + tag], cwd=ROOT,
                                      capture_output=True).returncode != 0:
                        continue  # a clone without the tag
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
            self.assertIsNone(r["lastRigHead"])  # no complete native proof exists yet (6 Nov batch: partial passes only)

    @unittest.skipIf(subprocess.run(["git", "rev-parse", "--is-shallow-repository"], cwd=ROOT, capture_output=True,
                                    text=True).stdout.strip() == "true", "shallow clone")
    def test_record_commits_are_ancestors_of_this_head(self):
        for r in self.ledger["rows"]:
            code = subprocess.run(["git", "merge-base", "--is-ancestor", r["recordedAt"], "HEAD"], cwd=ROOT).returncode
            self.assertEqual(0, code, r["id"])
        state = rm.assess(self.ledger, ROOT, "HEAD", None, rm.declared_set())
        self.assertTrue(all(r["debt"] is not None for r in state["rows"]))
        self.assertEqual(1, state["gitQueries"])


if __name__ == "__main__":
    unittest.main()
