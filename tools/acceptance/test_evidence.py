"""F9h evidence writer and cross-machine comparison (tools/acceptance/evidence.py). Synthetic results only."""
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

import evidence

MACHINES = {"schema": "persistentcity-acceptance-machines-1", "machines": [
    {"key": "windows-x64", "label": "PC", "status": "pending", "reason": "F9h PC run"},
    {"key": "macos-arm64", "label": "Mac", "status": "pending", "reason": "pending F9h-mac"},
    {"key": "linux-x64", "label": "Linux box", "status": "not-run",
     "reason": "not run: the Linux box does not build or test (team rule)"}]}


def band(low, high):
    return {"mean": (low + high) / 2, "sd": 1.0, "low": low, "high": high, "min": low, "max": high, "perSeed": [low, high]}


def write_run(root, os_name, arch, commit="c" * 40, movement="a" * 64, f6h_run="b" * 64, dll="d" * 64):
    out = Path(root) / f"run-{os_name}"
    (out / "f6h").mkdir(parents=True)
    result = {
        "schema": "persistentcity-acceptance-1", "runId": f"run-{os_name}", "profile": "full",
        "provenance": {"source": {"commit": commit}, "host": {"os": os_name, "architecture": arch},
                       "settings": {"compiledDefaults": {"FoodChoicePriceWeight": 1.2, "FoodChoiceHouseholdWeight": 1}}},
        "summary": {"verdict": "incomplete"}, "classes": {"semantic": "pass"},
        "nativeEvidence": {"status": "assessed", "batchDue": True, "rows": ["dropped"]},
        "stages": [
            {"id": "build", "class": "native", "verdict": "pass", "digests": {"PersistentCity.dll": dll}, "evidence": []},
            {"id": "economy", "class": "semantic", "verdict": "incomplete", "digests": {},
             "evidence": [{"path": "f6h/acceptance.json", "sha256": "e" * 64, "bytes": 10, "retained": False}]},
            {"id": "movement", "class": "semantic", "verdict": "pass", "digests": {"stationary": movement}, "evidence": []}]}
    household = {"arrearsShare": [band(0, 0.004)] * 5 + [band(0.001, 0.00512)],
                 "unfedOwnerDays": [band(0, 5)] * 5 + [band(1.0, 6.04)],
                 "p10Dollars": [band(600, 700)] * 5 + [band(640.7, 720)], "withinThresholdsPerSeed": [True, False]}
    f6h = {"runs": 80, "defaults": {"priceWeight": 1.2}, "householdControlSeedBands": household,
           "metricsSha256": {"control-control-seed0.json": f6h_run}}
    (out / "f6h" / "acceptance.json").write_text(json.dumps(f6h), encoding="utf-8")
    (out / "acceptance.json").write_text(json.dumps(result), encoding="utf-8")
    return out / "acceptance.json"


class Record(unittest.TestCase):
    def test_summary_and_bands_are_written_per_machine_and_path_free(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Path(tmp) / "evidence"
            written = evidence.record(write_run(tmp, "Windows", "AMD64"), store)
            self.assertEqual([p.relative_to(store).as_posix() for p in written],
                             ["windows-x64/summary.json", "windows-x64/f6h-household-bands.json"])
            for path in written:
                self.assertNotIn(tmp, path.read_text(encoding="utf-8"))
                self.assertNotIn(Path(tmp).as_posix(), path.read_text(encoding="utf-8"))
            summary = json.loads(written[0].read_text(encoding="utf-8"))
            self.assertEqual(summary["nativeEvidence"]["batchDue"], True)
            self.assertNotIn("rows", summary["nativeEvidence"])
            economy = next(s for s in summary["stages"] if s["id"] == "economy")
            self.assertEqual(economy["f6hRuns"], {"control-control-seed0.json": "b" * 64})
            self.assertNotIn("retained", economy["evidence"][0])

    def test_bands_use_the_existing_f6_function(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Path(tmp) / "evidence"
            bands = json.loads(evidence.record(write_run(tmp, "Windows", "AMD64"), store)[1].read_text(encoding="utf-8"))
            module = evidence.f6h_module()
            expected = module.derive_household_thresholds(
                json.loads((Path(tmp) / "run-Windows/f6h/acceptance.json").read_text())["householdControlSeedBands"])
            self.assertEqual(bands["derived"], expected)
            self.assertEqual(bands["derived"], {"week6ArrearsShareMax": 0.0052, "week6UnfedOwnerDaysMax": 6.1,
                                                "week6P10DollarsMin": 640.0})
            self.assertEqual(bands["currentThresholds"], module.HOUSEHOLD_THRESHOLDS)
            self.assertFalse(bands["adopted"])
            self.assertEqual(bands["priceWeight"], 1.2)

    def test_machine_keys(self):
        self.assertEqual(evidence.machine_key({"os": "Windows", "architecture": "AMD64"}), "windows-x64")
        self.assertEqual(evidence.machine_key({"os": "Darwin", "architecture": "arm64"}), "macos-arm64")
        self.assertEqual(evidence.machine_key({"os": "Linux", "architecture": "x86_64"}), "linux-x64")


class Compare(unittest.TestCase):
    def store(self, tmp):
        store = Path(tmp) / "evidence"
        store.mkdir()
        (store / "machines.json").write_text(json.dumps(MACHINES), encoding="utf-8")
        return store

    def test_pc_only_is_pending_with_the_mac_pending_and_linux_not_run_by_rule(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = self.store(tmp)
            evidence.record(write_run(tmp, "Windows", "AMD64"), store)
            doc = evidence.compare(store)
            self.assertEqual(doc["verdict"], "pending")
            status = {m["key"]: m["status"] for m in doc["machines"]}
            self.assertEqual(status, {"windows-x64": "recorded", "macos-arm64": "pending", "linux-x64": "not-run"})
            linux = next(m for m in doc["machines"] if m["key"] == "linux-x64")
            self.assertIn("team rule", linux["reason"])
            self.assertEqual({r["status"] for r in doc["digests"]}, {"single"})
            self.assertFalse(any(r["name"].startswith("build/") for r in doc["digests"]))

    def test_equal_digests_are_cross_machine_deterministic_and_build_digests_are_not_compared(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = self.store(tmp)
            evidence.record(write_run(tmp, "Windows", "AMD64", dll="1" * 64), store)
            evidence.record(write_run(tmp, "Darwin", "arm64", dll="2" * 64), store)
            doc = evidence.compare(store)
            self.assertEqual(doc["verdict"], "cross-machine-deterministic")
            self.assertEqual({r["name"] for r in doc["digests"]}, {"movement/stationary", "economy/f6h/control-control-seed0.json"})

    def test_a_difference_is_platform_divergent_with_the_first_differing_digest(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = self.store(tmp)
            evidence.record(write_run(tmp, "Windows", "AMD64"), store)
            evidence.record(write_run(tmp, "Darwin", "arm64", f6h_run="9" * 64), store)
            doc = evidence.compare(store)
            self.assertEqual(doc["verdict"], "platform-divergent")
            self.assertEqual(doc["firstDifference"], "economy/f6h/control-control-seed0.json")
            movement = next(r for r in doc["digests"] if r["name"] == "movement/stationary")
            self.assertEqual(movement["status"], "equal")

    def test_different_commits_are_not_compared(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = self.store(tmp)
            evidence.record(write_run(tmp, "Windows", "AMD64"), store)
            evidence.record(write_run(tmp, "Darwin", "arm64", commit="f" * 40, f6h_run="9" * 64), store)
            self.assertEqual(evidence.compare(store)["verdict"], "different-source")


def git_repo(root):
    """A repository with three commits: base, a docs-only commit and a Core commit. Returns their SHAs."""
    repo = Path(root) / "repo"
    (repo / "Core").mkdir(parents=True)
    (repo / "docs").mkdir()
    def git(*args):
        return subprocess.run(["git", "-c", "user.name=t", "-c", "user.email=t@t", *args], cwd=repo, check=True,
                              capture_output=True, text=True).stdout.strip()
    git("init", "-q")
    shas = []
    for path, text in (("Core/Sim.cs", "a"), ("docs/f9h.md", "record"), ("Core/Sim.cs", "b")):
        (repo / path).write_text(text, encoding="utf-8")
        git("add", "-A")
        git("commit", "-q", "-m", path)
        shas.append(git("rev-parse", "HEAD"))
    return repo, shas


class CompareAcrossCommits(unittest.TestCase):
    def store(self, tmp):
        store = Path(tmp) / "evidence"
        store.mkdir()
        (store / "machines.json").write_text(json.dumps(MACHINES), encoding="utf-8")
        return store

    def test_a_later_head_with_only_docs_and_evidence_changes_is_compared(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo, (base, docs, _) = git_repo(tmp)
            store = self.store(tmp)
            evidence.record(write_run(tmp, "Windows", "AMD64", commit=base), store)
            evidence.record(write_run(tmp, "Darwin", "arm64", commit=docs), store)
            self.assertEqual(evidence.compare(store, repo)["verdict"], "cross-machine-deterministic")

    def test_a_simulation_input_change_between_the_commits_is_different_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo, (base, _, core) = git_repo(tmp)
            store = self.store(tmp)
            evidence.record(write_run(tmp, "Windows", "AMD64", commit=base), store)
            evidence.record(write_run(tmp, "Darwin", "arm64", commit=core), store)
            doc = evidence.compare(store, repo)
            self.assertEqual(doc["verdict"], "different-source")
            self.assertIn("Core/Sim.cs", doc["reason"])


if __name__ == "__main__":
    unittest.main()
