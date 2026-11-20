import ast
import builtins
import importlib
import json
import os
import subprocess
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
HELPERS = sorted(p for p in HERE.glob('*.py') if not p.name.startswith('test_'))


def unbound_names(source, filename='<source>'):
    tree = ast.parse(source, filename)
    bound = set(dir(builtins)) | {'__file__', '__name__'}
    for node in ast.walk(tree):
        if isinstance(node, ast.Name) and isinstance(node.ctx, (ast.Store, ast.Del)):
            bound.add(node.id)
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            bound.add(node.name)
        elif isinstance(node, ast.arg):
            bound.add(node.arg)
        elif isinstance(node, (ast.Import, ast.ImportFrom)):
            bound.update((a.asname or a.name).split('.')[0] for a in node.names)
        elif isinstance(node, ast.ExceptHandler) and node.name:
            bound.add(node.name)
    return sorted({n.id for n in ast.walk(tree) if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Load) and n.id not in bound})


class HomeDir:
    def __enter__(self):
        self.temp = tempfile.TemporaryDirectory()
        self.home = Path(self.temp.name).resolve()
        self.patch = mock.patch.dict(os.environ, {'HOME': str(self.home), 'USERPROFILE': str(self.home)})
        self.patch.start()
        return self.home

    def __exit__(self, *exc):
        self.patch.stop()
        self.temp.cleanup()


class PrestagePathTests(unittest.TestCase):
    RELATIVE = 'Coding_Projects/.agent-logs/persistent-city-rig-2026-11-20a-mac/package/persistent-city.json'

    def test_tilde_package_config_resolves_where_the_raw_path_fails(self):
        import prestage
        with HomeDir() as home:
            config = home / self.RELATIVE
            config.parent.mkdir(parents=True)
            config.write_text(json.dumps({'SaveCostDiagnostics': True, 'Other': 3}))
            with self.assertRaises(FileNotFoundError):
                Path('~/' + self.RELATIVE).read_text()
            data = prestage.set_package_config('~/' + self.RELATIVE, {'SaveCostDiagnostics': False})
            self.assertEqual(data, {'SaveCostDiagnostics': False, 'Other': 3})
            self.assertEqual(json.loads(config.read_text()), data)
            self.assertFalse((Path.cwd() / '~').exists())

    def test_stage_package_and_cli_expand_tilde(self):
        import prestage
        with HomeDir() as home:
            build = home / 'wt/build/PersistentCity'
            build.mkdir(parents=True)
            (build / 'persistent-city.json').write_text(json.dumps({'SaveCostDiagnostics': True}))
            (build / 'PersistentCity.dll').write_bytes(b'dll')
            summary = prestage.main(['--stage', '~/stage', '--build', '~/wt/build/PersistentCity', '--package', '~/HART-Builds/run/package',
                                     '--fixture', 'FT X', '--package-only', '--set', 'Extra=1'])
            package = home / 'HART-Builds/run/package'
            self.assertEqual(summary['package'], str(package))
            self.assertEqual(json.loads((package / 'persistent-city.json').read_text()), {'SaveCostDiagnostics': False, 'Extra': 1})
            self.assertTrue((package / 'PersistentCity.dll').is_file())

    def test_backup_profile_expands_tilde(self):
        import prestage
        with HomeDir() as home:
            profile = home / 'profile'
            (profile / 'ModsLocal/PersistentCity').mkdir(parents=True)
            (profile / 'persistent-city.json').write_text('{}')
            manifest, _, _ = prestage.backup_profile('~/stage', 'FT X', home='~', profile='~/profile', rig='~/rig', preferences='~/prefs.plist')
            self.assertIsNotNone(manifest['profile/persistent-city.json'])
            self.assertTrue((home / 'stage/backup/profile/persistent-city.json').is_file())
            self.assertTrue((home / 'stage/pre-stage-manifest.json').is_file())

    def test_install_mac_invocation_uses_absolute_paths(self):
        import rigpaths
        with HomeDir() as home:
            command = rigpaths.stage_mod_command('~/rig', 'PersistentCity', '~/HART-Builds/run/package')
            self.assertEqual(command[1:], [str(home / 'rig/scripts/install-mac.py'), '--stage-mod', 'PersistentCity', str(home / 'HART-Builds/run/package')])
            self.assertFalse(any('~' in part for part in command))
            self.assertTrue(Path(command[-1]).is_absolute())
            self.assertNotIn('~', rigpaths.stage_mod_line('~/rig', 'PersistentCity', '~/pkg', 'run-x'))
            with mock.patch('builtins.print'):
                self.assertEqual(rigpaths.main(['--rig', '~/rig', '--mod', 'PersistentCity', '--source', '~/pkg', '--owner', 'run-x']),
                                 rigpaths.stage_mod_line('~/rig', 'PersistentCity', '~/pkg', 'run-x'))


class FakeBridge:
    def __init__(self):
        self.commands = []
        self.autosave = {'enabled': True, 'autosave_disabled': True, 'suppressed': True}

    def __call__(self, args):
        self.commands.append(args)
        kind = args[0]
        if kind == 'call' and args[1] == 'console':
            line = json.loads(args[2])['line']
            output = ['[Persistent City] gc retained ' + json.dumps({'bytes': 10, 'objects': 2, 'type_counts': {'RouteLeg': 4}})] if line == 'pc gc retained' else []
            body = {'result': {'output': output}}
        elif kind == 'autosave':
            body = self.autosave
        elif kind == 'op' and args[1] == 'perf_union':
            body = {'after': {'mono_heap': {'now_bytes': 5}, 'after_forced_gc_bytes': 7}}
        elif kind == 'op' and args[1] == 'pcstate':
            body = {'native': True, 'value': {'travelClockRate': 1, 'ownedBodyCombiners': 3}}
        else:
            body = {}
        return subprocess.CompletedProcess(args, 0, json.dumps(body), '')

    def console_lines(self):
        return [json.loads(c[2])['line'] for c in self.commands if c[:2] == ['call', 'console']]


class RetentionRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        root = Path(self.temp.name)
        (root / 'support').mkdir()
        (root / 'Player.log').write_text('Population: 1000 citizens; 0 travelling\n')
        self.env = mock.patch.dict(os.environ, {'PC_RIG_RUN': 'rig-test-run', 'BA_LOCK_OWNER': 'rig-test-run', 'PC_RIG_STAGE': str(root / 'stage'),
                                                'PC_RIG_SUPPORT': str(root / 'support'), 'PC_RIG_PLAYER_LOG': str(root / 'Player.log'),
                                                'PC_RIG_CHECKOUT': str(root / 'rig')})
        self.env.start()
        for name in ('rigcommon', 'retention'):
            sys.modules.pop(name, None)
        self.common = importlib.import_module('rigcommon')
        self.bridge = FakeBridge()
        self.common.bridge = self.bridge
        self.stage = root / 'stage'

    def tearDown(self):
        for name in ('rigcommon', 'retention'):
            sys.modules.pop(name, None)
        self.env.stop()
        self.temp.cleanup()

    def local(self, module):
        module.game_pid = lambda: 4242
        module.rss_kib = lambda pid: 1000
        module.footprint = lambda label, pid: '1.0G'
        return module

    def test_helpers_bind_every_name_they_use(self):
        for path in HELPERS:
            self.assertEqual(unbound_names(path.read_text(), str(path)), [], path.name)

    def test_snapshot_runs_console_after_gc_retained(self):
        retention = self.local(importlib.import_module('retention'))
        row = retention.snapshot('R0')
        self.assertEqual(self.bridge.console_lines(), ['pc gc retained', 'pc movement status', 'pc population manifest'])
        self.assertEqual(row['census']['bytes'], 10)
        self.assertEqual(row['mesh']['ownedBodyCombiners'], 3)
        self.assertEqual(retention.readings_summary([row])['RouteLeg'], [4])
        self.assertTrue((self.stage / 'retention-R0-movement.json').is_file())

    def test_the_16t_import_without_console_is_caught(self):
        source = (HERE / 'retention.py').read_text()
        broken = source.replace(' census, console,', ' census,', 1)
        self.assertNotEqual(broken, source)
        self.assertEqual(unbound_names(broken), ['console'])
        module = types.ModuleType('retention_broken')
        exec(compile(broken, 'retention_broken.py', 'exec'), module.__dict__)
        self.local(module)
        with self.assertRaises(NameError) as raised:
            module.snapshot('R0')
        self.assertIn("'console'", str(raised.exception))
        self.assertEqual(self.bridge.console_lines(), ['pc gc retained'])

    def test_snapshot_rejects_an_autosave_on_receipt_before_the_census(self):
        retention = self.local(importlib.import_module('retention'))
        self.bridge.autosave = {'enabled': True, 'autosave_disabled': False, 'suppressed': False}
        with self.assertRaisesRegex(RuntimeError, 'autosave is not suppressed'):
            retention.snapshot('R0')
        self.assertEqual(self.bridge.console_lines(), [])
        self.assertFalse((self.stage / 'retention-R0-set.json').exists())
        self.bridge.autosave = {'enabled': True, 'autosave_disabled': True, 'suppressed': False}
        with self.assertRaises(RuntimeError):
            self.common.auto('half-suppressed')

    def test_wrap_reads_the_hart_builds_run_directory(self):
        with HomeDir() as home:
            sys.modules.pop('rigcommon', None)
            common = importlib.import_module('rigcommon')
            self.assertFalse(common.wrap())
            signal = home / 'HART-Builds/rig-test-run/WRAP_UP_NOW.md'
            signal.parent.mkdir(parents=True)
            signal.write_text('stop')
            self.assertTrue(common.wrap())

    def test_no_run_and_no_stage_refuses_to_write_evidence(self):
        with HomeDir() as home, mock.patch.dict(os.environ, {'PC_RIG_RUN': '', 'PC_RIG_STAGE': ''}):
            sys.modules.pop('rigcommon', None)
            common = importlib.import_module('rigcommon')
            self.assertIsNone(common.STAGE)
            with self.assertRaises(RuntimeError):
                common.write('retention-failure', {'error': 'x'})
            with self.assertRaises(RuntimeError):
                common.archive('retention')
            self.assertFalse((home / 'Coding_Projects').exists())

    def test_bridge_calls_require_the_lock_owner(self):
        with mock.patch.dict(os.environ, {'BA_LOCK_OWNER': 'someone-else'}):
            with self.assertRaises(RuntimeError):
                self.common.console('x', 'pc gc retained')
        self.assertEqual(self.bridge.commands, [])

    def test_stage_default_expands_tilde(self):
        with mock.patch.dict(os.environ, {'PC_RIG_STAGE': '~/Coding_Projects/.agent-logs/rig-test-run'}):
            sys.modules.pop('rigcommon', None)
            common = importlib.import_module('rigcommon')
            self.assertTrue(common.STAGE.is_absolute())
            self.assertNotIn('~', str(common.STAGE))


if __name__ == '__main__':
    unittest.main()
