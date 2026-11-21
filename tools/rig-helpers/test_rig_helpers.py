import ast
import builtins
import importlib
import json
import math
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
        self.retained = {'bytes': 10, 'objects': 2, 'type_counts': {'RouteLeg': 4}}
        self.minute, self.step, self.paused = 3810.01, 0.0, True
        self.arrival = .00999832153

    def __call__(self, args):
        self.commands.append(args)
        kind = args[0]
        if kind == 'call' and args[1] == 'console':
            line = json.loads(args[2])['line']
            output = ['[Persistent City] gc retained ' + json.dumps(self.retained)] if line == 'pc gc retained' else []
            body = {'result': {'output': output}}
        elif kind == 'call' and args[1] == 'state':
            whole = math.floor(self.minute + .01)
            body = {'result': {'clock': {'paused': self.paused, 'time': '%02d:%02d' % divmod(whole % 1440, 60), 'day': whole // 1440 + 1,
                                         'run_until': None}}}
        elif kind == 'call' and args[1] == 'time':
            request = json.loads(args[2])
            self.paused = bool(request.get('pause'))
            if request.get('run_until'):
                h, m = map(int, request['run_until']['time'].split(':'))
                self.minute = (int(request['run_until']['day']) - 1) * 1440 + 60 * h + m + self.arrival
                self.paused = True
            body = {'session': 's'}
        elif kind == 'autosave':
            body = self.autosave
        elif kind == 'op' and args[1] == 'perf_union':
            body = {'after': {'mono_heap': {'now_bytes': 5}, 'after_forced_gc_bytes': 7}}
        elif kind == 'op' and args[1] == 'pcstate' and json.loads(args[2])['property'] == 'AccountingDiagnostics':
            if not self.paused:
                self.minute += self.step
            body = {'native': True, 'value': {'nativeMinute': self.minute}}
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
        self.support = root / 'support'

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
        self.assertEqual(retention.readings_summary([row])['RouteLeg outside current journeys'], [None])
        self.assertTrue((self.stage / 'retention-R0-movement.json').is_file())

    def test_tracked_types_and_route_leg_owners_are_read_when_top_types_omit_route_leg(self):
        retention = self.local(importlib.import_module('retention'))
        row = {'census': {'type_counts': {'CitySimulator': 1}, 'top_types': [{'name': 'String', 'objects': 9}],
                          'tracked_types': [{'name': 'RouteLeg', 'objects': 11456, 'bytes': 1191424}],
                          'route_legs': {'retained': 11456, 'in_current_journeys': 11400, 'outside_current_journeys': 56,
                                         'split_prefix_legs': 310, 'travelling': 1383}},
               'heap': {}, 'mono': {}, 'mesh': {'ownedBodyCombiners': 1, 'liveMergedMeshes': 1, 'unmergedBodyCombiners': 0,
                                                'liveMergedVertices': 1, 'bakeScratchBytes': 1}}
        series = retention.readings_summary([row])
        self.assertEqual(series['RouteLeg'], [11456])
        self.assertEqual(series['RouteLeg outside current journeys'], [56])
        self.assertEqual(series['RouteLeg split prefix legs'], [310])
        self.assertEqual(series['RouteLeg travelling'], [1383])
        self.assertTrue(retention.still_climbing({'RouteLeg outside current journeys': [0, 1, 2, 3]}))
        self.assertFalse(retention.still_climbing({'RouteLeg in current journeys': [0, 1, 2, 3], 'RouteLeg travelling': [0, 5, 9, 12]}))
        self.assertEqual(series['RouteLeg in caches'], [None])
        self.assertEqual(series['RouteLeg outside caches'], [None])

    def test_bounded_route_caches_are_read_apart_from_the_leak_series(self):
        retention = self.local(importlib.import_module('retention'))
        row = {'census': {'tracked_types': [{'name': 'RouteLeg', 'objects': 12179}, {'name': 'Route', 'objects': 1084}],
                          'tracked_types_outside_caches': [{'name': 'RouteLeg', 'objects': 9990}, {'name': 'Route', 'objects': 640}],
                          'cache_roots': [{'name': 'cache.routes.walking.cache', 'bound': 1024, 'routes': 444, 'route_legs': 2189}],
                          'route_legs': {'retained': 12179, 'in_current_journeys': 9936, 'outside_current_journeys': 2243,
                                         'in_caches': 2189, 'outside_current_journeys_and_caches': 54}},
               'heap': {}, 'mono': {}, 'mesh': {'ownedBodyCombiners': 1, 'liveMergedMeshes': 1, 'unmergedBodyCombiners': 0,
                                                'liveMergedVertices': 1, 'bakeScratchBytes': 1}}
        series = retention.readings_summary([row])
        self.assertEqual(series['RouteLeg'], [12179])
        self.assertEqual(series['RouteLeg outside caches'], [9990])
        self.assertEqual(series['Route outside caches'], [640])
        self.assertEqual(series['RouteLeg in caches'], [2189])
        self.assertEqual(series['RouteLeg outside current journeys and caches'], [54])
        self.assertTrue(retention.still_climbing({'Route outside caches': [0, 1, 2, 3]}))

    def test_route_growth_alone_keeps_the_leak_runner_collecting(self):
        retention = self.local(importlib.import_module('retention'))
        flat = {'census.bytes': [900, 1000, 1000, 1000], 'mono heap': [5, 6, 6, 6]}
        self.assertFalse(retention.still_climbing(flat))
        for name in ('RouteLeg outside current journeys', 'Route outside caches', 'Route', 'RouteLeg[]', 'List<RouteLeg>', 'RouteLegs'):
            with self.subTest(series=name):
                self.assertTrue(retention.still_climbing({**flat, name: [0, 20, 32, 40]}))
        self.assertFalse(retention.still_climbing({**flat, 'Route outside caches': [0, 20, 40, 32]}))
        row = {'census': {'tracked_types': [{'name': n, 'objects': k} for k, n in enumerate(('Route', 'RouteLeg[]', 'List<RouteLeg>', 'RouteLegs'), 1)]},
               'heap': {}, 'mono': {}, 'mesh': {'ownedBodyCombiners': 1, 'liveMergedMeshes': 1, 'unmergedBodyCombiners': 0,
                                                'liveMergedVertices': 1, 'bakeScratchBytes': 1}}
        series = retention.readings_summary([row])
        self.assertEqual([series[n] for n in ('Route', 'RouteLeg[]', 'List<RouteLeg>', 'RouteLegs')], [[1], [2], [3], [4]])

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

    def test_failed_autosave_postcheck_publishes_no_sample(self):
        retention = self.local(importlib.import_module('retention'))
        on = {'enabled': True, 'autosave_disabled': True, 'suppressed': True}
        off = {'enabled': True, 'autosave_disabled': False, 'suppressed': False}
        receipts = iter([on, off])
        original = self.bridge.__call__

        def bridge(args):
            if args[0] == 'autosave':
                self.bridge.autosave = next(receipts)
            return original(args)
        self.common.bridge = bridge
        with self.assertRaisesRegex(RuntimeError, 'R0-autosave-after: autosave is not suppressed'):
            retention.snapshot('R0')
        self.assertEqual(self.bridge.console_lines(), ['pc gc retained', 'pc movement status', 'pc population manifest'])
        self.assertFalse((self.stage / 'retention-R0-set.json').exists())

    def test_snapshot_records_the_native_minute(self):
        retention = self.local(importlib.import_module('retention'))
        self.assertEqual(retention.snapshot('R0')['native_minute'], 3810.01)

    def test_mac_defaults_to_the_ft_fixture_and_an_explicit_fixture_wins(self):
        with mock.patch.dict(os.environ, {'PC_RIG_FIXTURE': ''}):
            self.assertEqual(self.common.default_fixture('darwin'), 'FT GR5 13f Timing v1')
            self.assertEqual(self.common.default_fixture('win32'), '')
            with mock.patch.object(sys, 'platform', 'darwin'):
                sys.modules.pop('rigcommon', None)
                self.assertEqual(importlib.import_module('rigcommon').FIXTURE, 'FT GR5 13f Timing v1')
        with mock.patch.dict(os.environ, {'PC_RIG_FIXTURE': 'FT Other'}):
            self.assertEqual(self.common.default_fixture('darwin'), 'FT Other')
            self.assertEqual(self.common.default_fixture('win32'), 'FT Other')

    def test_mac_load_sends_the_default_fixture_name(self):
        with mock.patch.dict(os.environ, {'PC_RIG_FIXTURE': ''}), mock.patch.object(sys, 'platform', 'darwin'):
            sys.modules.pop('rigcommon', None)
            common = importlib.import_module('rigcommon')
        bridge = self.bridge

        def reply(args):
            if args[0] == 'resources':
                return subprocess.CompletedProcess(args, 0, json.dumps({'disk_free_gib': 50, 'memory_free_percent': 60}), '')
            if args[0] == 'load':
                return subprocess.CompletedProcess(args, 0, json.dumps({'error': 'stop-here'}), '')
            return bridge(args)
        common.bridge = reply
        with self.assertRaisesRegex(RuntimeError, 'stop-here'):
            common.start_and_load('l-retimed-initial-setup', recovery=False)
        sent = json.loads((self.stage / 'l-retimed-initial-setup-load.json').read_text())['command']
        self.assertEqual(json.loads(sent[2]), {'name': 'FT GR5 13f Timing v1'})

    def test_pc_without_a_fixture_still_refuses_to_load(self):
        with mock.patch.dict(os.environ, {'PC_RIG_FIXTURE': ''}), mock.patch.object(sys, 'platform', 'win32'):
            sys.modules.pop('rigcommon', None)
            common = importlib.import_module('rigcommon')
        common.bridge = self.bridge
        with self.assertRaisesRegex(RuntimeError, 'PC_RIG_FIXTURE must name the FT fixture save'):
            common.start_and_load('l-retimed-initial-setup')
        self.assertEqual(self.bridge.commands, [])

    def test_game_pid_waits_briefly_and_returns_none_without_a_pid(self):
        run_file = self.support / 'run.json'
        self.assertIsNone(self.common.game_pid(timeout=0))
        run_file.write_text(json.dumps({'mode': 'headless', 'rig_test': True}))
        self.assertIsNone(self.common.game_pid(timeout=.05, interval=.01))
        run_file.write_text('[]')
        self.assertIsNone(self.common.game_pid(timeout=0))
        run_file.write_text(json.dumps({'pid': 58706}))
        self.assertEqual(self.common.game_pid(timeout=0), 58706)

    def test_snapshot_without_a_pid_sends_nothing_to_the_game(self):
        retention = self.local(importlib.import_module('retention'))
        retention.game_pid = lambda: None
        with self.assertRaisesRegex(RuntimeError, 'run.json has no game pid'):
            retention.snapshot('drain')
        self.assertEqual(self.bridge.commands, [])

    def test_drain_target_a_fraction_above_a_fixed_native_minute_is_reached(self):
        self.assertTrue(self.common.native_reached(3900.00999832153, 3900.0111065))
        self.assertFalse(self.common.native_reached(3899.98, 3900.0111065))
        self.assertTrue(self.common.native_reached(3900.01, 3900.5, paused=True))
        self.assertFalse(self.common.native_reached(3900.01, 3900.5))
        self.assertFalse(self.common.native_reached(3899.99, 3900.0111065, paused=True))
        self.bridge.minute, self.bridge.paused = 3900.00999832153, True
        reached = self.common.wait_native('l-retimed-drain', 3900.0111065, timeout=0)
        self.assertEqual(reached['native_minute'], 3900.00999832153)
        self.assertLess(abs(reached['offset']), self.common.NATIVE_TOLERANCE)

    def test_drain_waiter_fails_fast_when_the_clock_is_paused_short_of_the_target(self):
        self.bridge.minute, self.bridge.paused = 3899.5, True
        with self.assertRaisesRegex(RuntimeError, 'paused at 3899.5 before 3900.0111065'):
            self.common.wait_native('l-retimed-drain', 3900.0111065, timeout=60)
        self.bridge.paused, self.bridge.step = False, 0.0
        with self.assertRaisesRegex(RuntimeError, 'timed out at 3899.5 on the way to 3900.0111065'):
            self.common.wait_native('l-retimed-drain', 3900.0111065, timeout=0)

    def test_route_leg_is_read_at_every_checkpoint_from_tracked_types(self):
        retention = self.local(importlib.import_module('retention'))
        rows = []
        for key, legs in (('R0', 3036), ('R1', 1538), ('R2', 1440), ('R3', 1916), ('After30GameMinutes', 11453)):
            self.bridge.retained = {'bytes': 10, 'objects': 2, 'top_types': [{'name': 'String', 'objects': 9}],
                                    'tracked_types': [{'name': 'RouteLeg', 'objects': legs, 'bytes': legs * 104}],
                                    'route_legs': {'outside_current_journeys': legs // 100, 'travelling': 174}}
            rows.append(retention.snapshot(key))
        series = retention.readings_summary(rows)
        self.assertEqual(series['RouteLeg'], [3036, 1538, 1440, 1916, 11453])
        self.assertEqual(series['RouteLeg outside current journeys'], [30, 15, 14, 19, 114])
        self.assertEqual(series['RouteLeg travelling'], [174] * 5)

    def targets(self, retention, minutes, paused=(), censuses=None):
        args = types.SimpleNamespace(build_sha='abc', start='15:30')
        keys = ['R%d' % i for i in range(len(minutes))]
        censuses = censuses or minutes
        rows = [{'native_minute': m, 'census_native_minute': c, 'census': {'tracked_types': [{'name': 'RouteLeg', 'objects': 100 + i}]}, 'heap': {}, 'mono': {},
                 'clock': {'paused': i in paused, 'day': 3, 'time': retention.clock_text(m) if isinstance(m, float) and math.isfinite(m) else None},
                 'mesh': {f: 0 for f in retention.FIELDS}} for i, (m, c) in enumerate(zip(minutes, censuses))]
        return retention.export_targets(args, keys, rows, True)

    def test_retimed_process_exports_its_checkpoint_native_minutes(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3810.01, 3819.143536, 3901.011407], paused=(0, 2))
        saved = json.loads((self.stage / 'retention-targets.json').read_text())
        self.assertEqual(saved, plan)
        self.assertEqual([p['native_minute'] for p in saved['checkpoints']], [3810.01, 3819.143536, 3901.011407])
        self.assertEqual([(p['paused'], p['time']) for p in saved['checkpoints']], [(True, '15:30'), (False, '15:39'), (True, '17:01')])
        self.assertEqual(saved['series']['RouteLeg'], [100, 101, 102])
        for broken in ([3810.01, None], [3810.01, 3810.01], [], [3810.01, float('nan')]):
            with self.assertRaisesRegex(RuntimeError, 'Retimed process did not export matched native clock targets'):
                self.targets(retention, broken)

    def test_snapshot_records_the_clock_state(self):
        retention = self.local(importlib.import_module('retention'))
        self.assertEqual(retention.snapshot('R0')['clock'], {'paused': True, 'day': 3, 'time': '15:30'})

    def test_control_reads_at_the_retimed_native_minutes(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3810.01, 3810.5, 3811.2, 3900.0111065])
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3810.01, True, .05
        rows = retention.follow_targets(plan)
        offsets = [row['native_minute'] - point['native_minute'] for row, point in zip(rows, plan['checkpoints'])]
        self.assertEqual(len(rows), 4)
        self.assertTrue(all(-self.common.NATIVE_TOLERANCE <= o < .2 for o in offsets), offsets)
        rates = [json.loads(c[2]) for c in self.bridge.commands if c[:2] == ['call', 'time']]
        self.assertEqual(rates, [{'speed': 1, 'pause': False, 'keep_needs': True}])
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        self.assertEqual(matched['keys'], ['R0', 'R1', 'R2', 'R3'])
        self.assertEqual(matched['unmatched'], [])
        self.assertEqual(matched['retimed_minus_control']['RouteLeg'], [96, 97, 98, 99])
        self.assertTrue((self.stage / 'retention-control-R3-set.json').is_file())

    def test_control_is_paused_where_the_retimed_checkpoint_was_paused(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3810.01, 3811.2, 3841.2], paused=(0, 2))
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step, self.bridge.arrival = 3810.01, True, .05, .2
        with mock.patch.object(self.common.time, 'sleep'):
            rows = retention.follow_targets(plan)
        self.assertEqual([row['clock']['paused'] for row in rows], [True, False, True])
        self.assertEqual(rows[2]['native_minute'], 3841.2)
        times = [json.loads(c[2]) for c in self.bridge.commands if c[:2] == ['call', 'time']]
        self.assertEqual(times[-1]['run_until'], {'day': '3', 'time': '16:01'})
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        self.assertEqual([c['matched'] for c in matched['checkpoints']], [True, True, True])

    def test_control_sample_past_the_bound_is_unmatched(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3810.5, 3811.0])
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3812.0, False, .05
        retention.follow_targets(plan)
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        self.assertEqual(matched['unmatched'], ['R0', 'R1'])
        self.assertEqual([c['passed_at_start'] for c in matched['checkpoints']], [True, True])
        self.assertEqual(matched['retimed_minus_control']['RouteLeg'], [None, None])
        self.assertGreater(matched['native_offsets'][0], retention.MATCH_BOUND)

    def test_retimed_drain_targets_thirty_minutes_after_the_last_checkpoint(self):
        retention = self.local(importlib.import_module('retention'))
        last = {'native_minute': 3870.0111065}
        self.bridge.minute, self.bridge.paused = 3872.3, False
        with mock.patch.object(self.common.time, 'sleep'):
            arrived = retention.drain([last])
        times = [json.loads(c[2]) for c in self.bridge.commands if c[:2] == ['call', 'time']]
        self.assertEqual(times[-1]['run_until'], {'day': '3', 'time': '17:00'})
        self.assertEqual(arrived['native_minute'], 3900.00999832153)
        reached = json.loads((self.stage / 'retention-drain-reached.json').read_text())
        self.assertEqual(reached['target'], 3900.0111065)
        self.assertLess(abs(reached['offset']), self.common.NATIVE_TOLERANCE)

    def test_retimed_drain_refuses_a_clock_already_past_the_target(self):
        retention = self.local(importlib.import_module('retention'))
        self.bridge.minute, self.bridge.paused = 3901.0, False
        with self.assertRaisesRegex(RuntimeError, 'already passed the drain target'):
            retention.drain([{'native_minute': 3870.0111065}])

    def test_retimed_drain_in_the_target_minute_pauses_in_place_on_the_same_day(self):
        retention = self.local(importlib.import_module('retention'))
        self.bridge.minute, self.bridge.paused = 3900.010, False
        arrived = retention.drain([{'native_minute': 3870.0111065}])
        times = [json.loads(c[2]) for c in self.bridge.commands if c[:2] == ['call', 'time']]
        self.assertEqual(times, [{'speed': 1, 'pause': True, 'keep_needs': True}])
        self.assertEqual(math.floor(arrived['native_minute']), 3900)
        reached = json.loads((self.stage / 'retention-drain-reached.json').read_text())
        self.assertLess(abs(reached['offset']), self.common.NATIVE_TOLERANCE)

    def test_arrival_outside_the_target_native_minute_is_refused(self):
        retention = self.local(importlib.import_module('retention'))
        self.bridge.minute, self.bridge.paused, self.bridge.arrival = 3872.3, False, 1440.0
        with mock.patch.object(self.common.time, 'sleep'):
            with self.assertRaisesRegex(RuntimeError, 'outside the native minute of 3900.0111065'):
                retention.drain([{'native_minute': 3870.0111065}])
        self.assertFalse((self.stage / 'retention-drain-reached.json').exists())

    def test_paused_control_already_in_the_checkpoint_minute_pauses_in_place(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3810.01, 3841.2], paused=(1,))
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3841.15, False, 0.0
        rows = retention.follow_targets(plan)
        times = [json.loads(c[2]) for c in self.bridge.commands if c[:2] == ['call', 'time']]
        self.assertFalse(any('run_until' in t for t in times), times)
        self.assertTrue(rows[1]['clock']['paused'])
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        self.assertEqual(matched['unmatched'], ['R0'])

    def test_late_census_is_unmatched_even_when_the_first_native_read_is_in_bound(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3811.0])
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3810.6, False, .25
        retention.follow_targets(plan)
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        point = matched['checkpoints'][0]
        self.assertLessEqual(point['offset'], retention.MATCH_BOUND)
        self.assertGreater(point['census_offset'], retention.MATCH_BOUND)
        self.assertEqual(matched['unmatched'], ['R0'])
        self.assertEqual(matched['retimed_minus_control']['RouteLeg'], [None])

    def test_delayed_retimed_census_is_unmatched_against_an_earlier_control_census(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3811.0], censuses=[3811.79])
        self.assertEqual(plan['checkpoints'][0]['census_native_minute'], 3811.79)
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3810.9, False, .06
        retention.follow_targets(plan)
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        point = matched['checkpoints'][0]
        self.assertLessEqual(point['census_offset'], retention.MATCH_BOUND)
        self.assertGreater(abs(point['census_gap']), retention.MATCH_BOUND)
        self.assertEqual(matched['unmatched'], ['R0'])
        self.assertEqual(matched['retimed_minus_control']['RouteLeg'], [None])

    def test_post_census_reads_in_bound_do_not_match_brackets_that_cannot(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3811.0], censuses=[3811.79])
        retention.wait_native = lambda label, target: setattr(self.bridge, 'minute', 3811.02)
        retention.census = lambda label: (setattr(self.bridge, 'minute', 3811.31), self.bridge.retained)[1]
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3810.9, False, 0.0
        retention.follow_targets(plan)
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        point = matched['checkpoints'][0]
        self.assertAlmostEqual(point['census_gap'], -.48)
        self.assertAlmostEqual(point['bracket_gap'], .77)
        self.assertEqual(matched['unmatched'], ['R0'])
        self.assertEqual(matched['retimed_minus_control']['RouteLeg'], [None])

    def test_native_read_rounding_into_the_next_minute_keeps_the_displayed_day(self):
        self.bridge.minute, self.bridge.paused = 3839.995, False
        original = self.bridge.__call__

        def lagging(args):
            reply = original(args)
            if args[:2] == ['call', 'state'] and self.bridge.minute < 3840:
                body = json.loads(reply.stdout)
                body['result']['clock'].update(time='15:59', day=3)
                reply = subprocess.CompletedProcess(args, 0, json.dumps(body), '')
            return reply
        self.common.bridge = lagging
        with mock.patch.object(self.common.time, 'sleep'):
            arrived = self.common.pause_at_native('lagging', 3841.01)
        times = [json.loads(c[2]) for c in self.bridge.commands if c[:2] == ['call', 'time']]
        self.assertEqual(times[0]['run_until'], {'day': '3', 'time': '16:01'})
        self.assertEqual(math.floor(arrived), 3841)

    def test_targets_without_a_retimed_census_minute_withhold_the_delta(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3811.0])
        del plan['checkpoints'][0]['census_native_minute']
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3810.95, False, .02
        retention.follow_targets(plan)
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        self.assertIsNone(matched['checkpoints'][0]['census_gap'])
        self.assertEqual(matched['unmatched'], ['R0'])

    def test_midnight_between_the_native_and_state_reads_keeps_the_target_day(self):
        self.bridge.minute, self.bridge.paused = 4319.98, False
        original = self.bridge.__call__

        def crossing(args):
            if args[:2] == ['call', 'state'] and not self.bridge.paused and self.bridge.minute < 4320:
                self.bridge.minute = 4320.05
            return original(args)
        self.common.bridge = crossing
        with mock.patch.object(self.common.time, 'sleep'):
            arrived = self.common.pause_at_native('midnight', 4321.01)
        times = [json.loads(c[2]) for c in self.bridge.commands if c[:2] == ['call', 'time']]
        self.assertEqual(times[0]['run_until'], {'day': '4', 'time': '00:01'})
        self.assertEqual(math.floor(arrived), 4321)

    def test_paused_control_resumes_for_a_running_checkpoint_it_already_reached(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3810.015])
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        self.bridge.minute, self.bridge.paused, self.bridge.step = 3810.010, True, .01
        rows = retention.follow_targets(plan)
        self.assertFalse(rows[0]['clock']['paused'])
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        self.assertEqual(matched['unmatched'], [])

    def test_paused_snapshot_never_matches_a_running_checkpoint(self):
        retention = self.local(importlib.import_module('retention'))
        plan = self.targets(retention, [3810.015])
        retention.wait_native = lambda label, target: self.common.wait_native(label, target, timeout=5, interval=0)
        retention.rate = lambda label, speed, pause=False: {}
        self.bridge.minute, self.bridge.paused = 3810.010, True
        rows = retention.follow_targets(plan)
        self.assertTrue(rows[0]['clock']['paused'])
        matched = json.loads((self.stage / 'retention-matched.json').read_text())
        self.assertEqual(matched['unmatched'], ['R0'])
        self.assertTrue(matched['checkpoints'][0]['observed_paused'])

    def test_control_refuses_a_targets_file_without_native_minutes(self):
        retention = self.local(importlib.import_module('retention'))
        path = self.stage / 'retention-targets.json'
        self.stage.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps({'build_sha': 'abc', 'fixture': '', 'checkpoints': [{'key': 'R0'}]}))
        with self.assertRaisesRegex(RuntimeError, 'Retimed process did not export matched native clock targets'):
            retention.main(['--build-sha', 'abc', '--control', str(path)])
        self.assertEqual(self.bridge.commands, [])

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
