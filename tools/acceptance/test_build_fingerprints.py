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
import run  # noqa: E402
from test_run import Fixture  # noqa: E402

ROOT = HERE.parents[1]
BUILD = ROOT / "tools/build.py"
SUPPORTED = HERE / "supported-game.json"
STEAM = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed")
NO_SDK = "Cannot find dotnet"


def build_references():
    text = BUILD.read_text(encoding="utf-8")
    block = re.search(r"references = \[(.*?)\]", text, re.S).group(1)
    return [n + ".dll" for n in re.findall(r'"([^"]+)"', block)] + ["Newtonsoft.Json.dll"]


def installed_managed():
    path = Path(os.environ.get("PC_GAME_MANAGED") or STEAM)
    return path if (path / "BigAmbitions.dll").is_file() else None


class FakeGame:

    def __init__(self, test):
        self.tmp = tempfile.TemporaryDirectory()
        test.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)
        self.managed = self.dir / "Managed"
        self.managed.mkdir()
        for name in build_references():
            (self.managed / name).write_bytes(b"fake " + name.encode())
        self.declared = self.dir / "supported-game.json"
        self.declare("fake-set", {n: self.sha(n) for n in build_references()})
        self.out = self.dir / "out" / "build.json"

    def sha(self, name):
        return hashlib.sha256((self.managed / name).read_bytes()).hexdigest()

    def declare(self, name, assemblies, *more):
        sets = [{"name": name, "assemblies": assemblies}] + list(more)
        self.declared.write_text(json.dumps({"schema": "persistentcity-supported-game-1", "current": name, "sets": sets}))

    def build(self, *extra, provenance=True):
        empty = self.dir / "empty"
        empty.mkdir(exist_ok=True)
        env = dict(os.environ, PATH=str(empty), LOCALAPPDATA=str(empty))
        argv = [sys.executable, str(BUILD), "--game-managed", str(self.managed), "--fingerprints", str(self.declared), *extra]
        if provenance:
            argv += ["--provenance", str(self.out)]
        return subprocess.run(argv, capture_output=True, text=True, env=env, check=False)

    def record(self):
        return json.loads(self.out.read_text(encoding="utf-8"))


class BuildProvenance(unittest.TestCase):
    def test_matching_set_is_named_and_the_build_continues(self):
        game = FakeGame(self)
        done = game.build()
        self.assertIn(NO_SDK, done.stderr)
        record = game.record()
        self.assertEqual("fake-set", record["set"])
        self.assertEqual([], record["differing"])
        self.assertEqual(sorted(build_references()), sorted(record["assemblies"]))
        self.assertEqual(game.sha("BigAmbitions.dll"), record["assemblies"]["BigAmbitions.dll"])
        self.assertIn("Game fingerprint set: fake-set", done.stdout)

    def test_one_changed_file_is_unknown_with_its_name_and_never_stops_the_build(self):
        game = FakeGame(self)
        (game.managed / "BigAmbitions.dll").write_bytes(b"a game update")
        done = game.build()
        record = game.record()
        self.assertEqual("unknown", record["set"])
        self.assertEqual(["BigAmbitions.dll"], record["differing"])
        self.assertIn("differs: BigAmbitions.dll", done.stdout)
        self.assertIn(NO_SDK, done.stderr)
        plain = game.build(provenance=False)
        self.assertEqual(done.returncode, plain.returncode)
        self.assertEqual(done.stderr, plain.stderr)

    def test_closest_of_several_sets_names_the_differing_files(self):
        game = FakeGame(self)
        hashes = {n: game.sha(n) for n in build_references()}
        far = {n: "0" * 64 for n in hashes}
        near = dict(hashes, **{"HGPlugins.dll": "1" * 64})
        game.declare("far", far, {"name": "near", "assemblies": near})
        self.assertEqual(["HGPlugins.dll"], (game.build(), game.record())[1]["differing"])
        game.declare("far", far, {"name": "near", "assemblies": near}, {"name": "exact", "assemblies": hashes})
        game.build()
        self.assertEqual("exact", game.record()["set"])

    def test_json_assembly_option_is_hashed_as_newtonsoft(self):
        game = FakeGame(self)
        other = game.dir / "elsewhere" / "Newtonsoft.Json.dll"
        other.parent.mkdir()
        other.write_bytes(b"another json")
        game.build("--json-assembly", str(other))
        record = game.record()
        self.assertEqual(["Newtonsoft.Json.dll"], record["differing"])
        self.assertEqual(hashlib.sha256(b"another json").hexdigest(), record["assemblies"]["Newtonsoft.Json.dll"])

    def test_missing_assembly_is_still_refused_as_today(self):
        game = FakeGame(self)
        (game.managed / "DayNightCycle.dll").unlink()
        with_flag, without = game.build(), game.build(provenance=False)
        self.assertNotEqual(0, with_flag.returncode)
        self.assertIn("Required game assembly is missing: DayNightCycle.dll", with_flag.stderr)
        self.assertEqual((without.returncode, without.stdout, without.stderr), (with_flag.returncode, with_flag.stdout, with_flag.stderr))
        self.assertFalse(game.out.exists())

    def test_no_path_in_the_output(self):
        game = FakeGame(self)
        (game.managed / "UnityEngine.UI.dll").write_bytes(b"changed")
        done = game.build()
        text = game.out.read_text(encoding="utf-8")
        self.assertEqual("<supplied>", json.loads(text)["managed"])
        line = next(l for l in done.stdout.splitlines() if l.startswith("Game fingerprint set:"))
        for output in (text, line):
            for raw in {str(game.dir), str(game.managed), str(ROOT), str(Path.home())}:
                self.assertNotIn(raw, output)
                self.assertNotIn(raw.replace("\\", "/"), output)
                self.assertNotIn(raw.replace("\\", "\\\\"), output)
            self.assertIsNone(re.search(r"[A-Za-z]:[\\/]|/Users/|/home/|Managed[\\/]", output))


class DeclaredSets(unittest.TestCase):
    def test_declared_file_holds_hashes_only_for_every_referenced_assembly(self):
        declared = json.loads(SUPPORTED.read_text(encoding="utf-8"))
        self.assertEqual("persistentcity-supported-game-1", declared["schema"])
        names = [s["name"] for s in declared["sets"]]
        self.assertIn(declared["current"], names)
        self.assertEqual(len(names), len(set(names)))
        self.assertNotIn("unknown", names)
        for entry in declared["sets"]:
            self.assertEqual(sorted(build_references()), sorted(entry["assemblies"]))
            for value in entry["assemblies"].values():
                self.assertRegex(value, r"^[0-9a-f]{64}$")
        self.assertIsNone(re.search(r"[A-Za-z]:[\\/]|/Users/|/home/", SUPPORTED.read_text(encoding="utf-8")))
        self.assertEqual([], [p.name for p in HERE.rglob("*.dll")])


@unittest.skipIf(installed_managed() is None, "installed game assemblies not found (set PC_GAME_MANAGED)")
class InstalledGame(unittest.TestCase):

    def build(self, *extra):
        done = subprocess.run([sys.executable, str(BUILD), "--game-managed", str(installed_managed()), *extra],
                              capture_output=True, text=True, check=True)
        return re.search(r"DLL SHA256: ([0-9a-f]{64})", done.stdout).group(1), done.stdout

    def test_dll_hash_is_unchanged_by_the_provenance_flag(self):
        with tempfile.TemporaryDirectory() as tmp:
            record = Path(tmp) / "build.json"
            plain, _ = self.build()
            flagged, stdout = self.build("--provenance", str(record))
            again, _ = self.build()
            self.assertEqual(plain, flagged)
            self.assertEqual(plain, again)
            game = json.loads(record.read_text(encoding="utf-8"))
        declared = json.loads(SUPPORTED.read_text(encoding="utf-8"))
        if os.environ.get("PC_GAME_MANAGED") is None:
            self.assertEqual(declared["current"], game["set"], game["differing"])
        self.assertIn("Game fingerprint set: " + game["set"], stdout)


class RunnerBuildStage(unittest.TestCase):

    def run_build(self, game_set, differing=()):
        f = Fixture(self)
        managed = f.dir / "Managed"
        managed.mkdir()
        record = {"schema": "persistentcity-build-provenance-1", "managed": "<supplied>", "set": game_set,
                  "differing": list(differing), "assemblies": {"BigAmbitions.dll": "cd" * 32}}
        script = f.script("build.py", f"""
            import json, sys
            out = sys.argv[sys.argv.index("--provenance") + 1]
            open(out, "w").write(json.dumps({record!r}))
            print("DLL SHA256: {'ab' * 32}")
            """)
        f.stage(id="build", **{"class": "native"}, rule="build", provenance="build.json",
                steps=[["{python}", script, "--game-managed", "{managed}", "--provenance", "{out}/build.json"]])
        f.native_pass()
        _, result, text = f.run("--game-managed", str(managed))
        self.assertNotIn(str(managed), text)
        return f, result

    def test_declared_set_is_recorded_and_native_can_pass(self):
        f, result = self.run_build("steam-1331550-build-25482473")
        game = result["provenance"]["game"]
        self.assertEqual((True, "<supplied>", "steam-1331550-build-25482473"), (game["supplied"], game["managed"], game["set"]))
        self.assertEqual("cd" * 32, game["assemblies"]["BigAmbitions.dll"])
        self.assertEqual("pass", f.stage_record(result, "build")["verdict"])
        self.assertIn("build.json", [e["path"] for e in f.stage_record(result, "build")["evidence"]])
        self.assertEqual("pass", result["classes"]["native"]["verdict"])
        self.assertNotIn("game-fingerprint", result["summary"]["required"])

    def test_unknown_set_marks_native_incomplete_not_failed(self):
        f, result = self.run_build("unknown", ["BigAmbitions.dll"])
        self.assertEqual("pass", f.stage_record(result, "build")["verdict"])
        self.assertEqual("incomplete", result["classes"]["native"]["verdict"])
        self.assertEqual(["game-fingerprint"], result["summary"]["incomplete"])
        self.assertEqual("incomplete", result["summary"]["verdict"])
        self.assertEqual(["BigAmbitions.dll"], result["provenance"]["game"]["differing"])

    def test_catalogue_build_stage_passes_the_provenance_flag(self):
        stage = next(s for s in json.loads((HERE / "stages.json").read_text())["stages"] if s["id"] == "build")
        self.assertEqual(["--provenance", "{out}/build.json"], stage["steps"][0][-2:])
        self.assertEqual("build.json", stage["provenance"])
        self.assertIn("build.json", run.stage_outputs(stage))


if __name__ == "__main__":
    unittest.main()
