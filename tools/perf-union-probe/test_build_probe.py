import contextlib
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

MODULE = Path(__file__).resolve().parent
sys.path.insert(0, str(MODULE))
import build_probe  # noqa: E402


def managed_folder(root):
    managed = Path(root) / 'Big Ambitions_Data/Managed'
    managed.mkdir(parents=True)
    (managed / 'BigAmbitions.dll').write_bytes(b'game')
    return managed


def home(root):
    return mock.patch.dict(os.environ, {'HOME': str(root), 'USERPROFILE': str(root)})


class BuildProbeArgumentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name).resolve()

    def tearDown(self):
        self.temp.cleanup()

    def parse_error(self, argv, **kwargs):
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr), self.assertRaises(SystemExit):
            build_probe.parse(argv, **kwargs)
        return stderr.getvalue()

    def test_mac_defaults_to_the_dll_import_shim_managed_folder(self):
        with home(self.root):
            expected = self.root / 'Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed'
            self.assertEqual(build_probe.host_managed('darwin', {}), expected)
            expected.mkdir(parents=True)
            (expected / 'BigAmbitions.dll').write_bytes(b'game')
            args = build_probe.parse([], platform='darwin', environ={})
        self.assertEqual(args.game_managed, expected)
        self.assertEqual(args.harmony, build_probe.REPO / 'build/PersistentCity/Dependencies/0Harmony.dll')
        self.assertEqual(args.output, build_probe.REPO / 'build/perf-union-probe')

    def test_pc_defaults_to_steam_managed(self):
        self.assertEqual(build_probe.host_managed('win32', {}),
                         Path(r'C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed'))
        self.assertIsNone(build_probe.host_managed('linux', {}))

    def test_environment_and_explicit_flags_win_over_the_host_default(self):
        managed = managed_folder(self.root / 'env')
        self.assertEqual(build_probe.parse([], platform='darwin', environ={'PERF_UNION_GAME_MANAGED': str(managed)}).game_managed, managed)
        other = managed_folder(self.root / 'flag')
        for flag in ('--game-managed', '--managed'):
            self.assertEqual(build_probe.parse([flag, str(other)], platform='linux', environ={}).game_managed, other)

    def test_managed_flag_expands_home(self):
        with home(self.root):
            args = build_probe.parse(['--managed', '~/Managed', '--harmony', '~/h.dll'], platform='linux', environ={})
        self.assertEqual(args.game_managed, self.root / 'Managed')
        self.assertEqual(args.harmony, self.root / 'h.dll')

    def test_unknown_host_without_a_flag_names_the_flag(self):
        self.assertIn('--game-managed is required on this host', self.parse_error([], platform='linux', environ={}))

    def test_missing_default_folder_is_reported_before_the_build(self):
        with home(self.root):
            message = self.parse_error([], platform='darwin', environ={})
        self.assertIn('no BigAmbitions.dll in the default Managed folder', message)
        self.assertIn('dll-import-shim', message)

    def test_harmony_is_never_taken_from_the_game_managed_folder(self):
        managed = managed_folder(self.root / 'app')
        (managed / '0Harmony.dll').write_bytes(b'harmony')
        output = build_probe.REPO / 'build/perf-union-probe-test/harmony-refusal'
        with self.assertRaises(SystemExit) as raised:
            build_probe.build(managed, managed / '0Harmony.dll', output)
        self.assertIn('pinned copy from tools/build.py', str(raised.exception))
        with self.assertRaises(SystemExit) as raised:
            build_probe.build(managed, self.root / 'missing/0Harmony.dll', output)
        self.assertIn('run tools/build.py', str(raised.exception))

    def test_output_rule_and_pinned_checksum_are_unchanged(self):
        managed = managed_folder(self.root / 'pinned')
        harmony = self.root / '0Harmony.dll'
        harmony.write_bytes(b'not harmony')
        for name in build_probe.REFERENCES:
            (managed / (name + '.dll')).write_bytes(b'ref')
        with self.assertRaisesRegex(SystemExit, 'below this checkout ignored build directory'):
            build_probe.build(managed, harmony, self.root / 'out')
        with self.assertRaisesRegex(SystemExit, 'pinned checksum'):
            build_probe.build(managed, harmony, build_probe.REPO / 'build/perf-union-probe-test/checksum')
        self.assertEqual(build_probe.HARMONY_SHA256, '7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c')


if __name__ == '__main__':
    unittest.main()
