"""Integration check: the real benchmark leaves its source checkout unchanged."""

import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


def snapshot(root):
    return {
        str(path.relative_to(root)): (
            hashlib.sha256(path.read_bytes()).hexdigest(), path.stat().st_mtime_ns
        ) if path.is_file() else None
        for path in root.rglob("*")
    }


@unittest.skipUnless(shutil.which("dotnet"), "Requires the .NET 10 SDK")
class BenchmarkIsolationTest(unittest.TestCase):
    def test_checkout_and_existing_artifacts_are_unchanged(self):
        here = Path(__file__).resolve().parent
        for existing_artifacts in (False, True):
            with self.subTest(existing_artifacts=existing_artifacts):
                with tempfile.TemporaryDirectory(prefix="pc-audit-test-") as directory:
                    sandbox = Path(directory)
                    checkout = sandbox / "checkout"
                    shutil.copytree(here.parents[3] / "Core", checkout / "Core",
                                    ignore=shutil.ignore_patterns("bin", "obj"))
                    assets = checkout / "docs/reviews/assets" / here.name
                    assets.mkdir(parents=True)
                    for name in ("run-budget-benchmark.py", "budget-posting-benchmark.cs.txt"):
                        shutil.copy2(here / name, assets / name)
                    if existing_artifacts:
                        for name in ("bin", "obj"):
                            artifact = checkout / "Core" / name / "preexisting.txt"
                            artifact.parent.mkdir()
                            artifact.write_text("Preserve existing build artifacts.\n")
                    scratch = sandbox / "scratch"
                    scratch.mkdir()
                    before = snapshot(checkout)
                    result = subprocess.run(
                        [sys.executable, str(assets / "run-budget-benchmark.py")],
                        cwd=checkout, check=True, capture_output=True, text=True,
                        env={**os.environ, **{key: str(scratch) for key in ("TMPDIR", "TEMP", "TMP")}},
                    )
                    self.assertIn("Residents=50000; consumers=100000;", result.stdout)
                    self.assertEqual(before, snapshot(checkout))
                    self.assertEqual([], list(scratch.glob("pc-city-audit-*")))


if __name__ == "__main__":
    unittest.main()
