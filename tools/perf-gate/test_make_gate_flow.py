"""F4d offline checks: python -m unittest discover -s tools/perf-gate -p test_*.py"""
import copy
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import make_gate_flow as gen  # noqa: E402

FIXTURES, SCRIPTS, CONTRACT, DIGEST = gen.load_inputs()

# Written out by hand from the plan, not read from the generator, so the tests do not check the generator against itself.
LOADS = {'process.launch', 'ft.load', 'fixture.produce', 'fixture.stress'}
TIME_JUMPS = LOADS | {'ft.save', 'time_machine.skip'}
PLAN_PENDING = {  # docs/plans/f4-perf-gate.md section 7, the pending capabilities named per check
    'G1': {'RIG-FOREGROUND', 'RIG-PRIORITY'},
    'G2': {'RIG-PERF-UNION'},
    'G3': {'RIG-PRESENT'},
    'G4': {'RIG-PERF-UNION', 'RIG-PRESENT', 'RIG-FOREGROUND', 'RIG-SHOPFIX'},
    'G5': {'RIG-PCCONFIG', 'RIG-PERF-UNION', 'RIG-PRESENT'},
    'G6': {'RIG-DRIVE', 'RIG-PERF-UNION', 'RIG-PRESENT'},
    'G7': {'RIG-UI-SCRIPT', 'RIG-PERF-UNION', 'RIG-PRESENT'},
    'G8': {'RIG-SHOPFIX', 'RIG-PERF-UNION', 'RIG-PRESENT'},
    'G9': {'RIG-PCCONFIG', 'RIG-PERF-UNION'},
    'G10': {'RIG-PERF-UNION'},
    'G13': {'RIG-UI-SCRIPT', 'RIG-SHOPEDIT', 'RIG-DRIVE', 'RIG-PERF-UNION', 'RIG-PRESENT'},
    'G14': {'RIG-PERF-UNION'},
    'G15': {'RIG-PCFIX', 'RIG-PERF-UNION', 'RIG-PRESENT'},
}
OP_NEEDS = {  # op prefix or name -> the pending capability the plan says it needs
    'probe.': 'RIG-PERF-UNION', 'trace.': 'RIG-PRESENT', 'settings.': 'RIG-FOREGROUND', 'priority.': 'RIG-PRIORITY',
    'sampler.': 'RIG-PRIORITY', 'pcconfig.': 'RIG-PCCONFIG', 'fixture.shops': 'RIG-SHOPFIX',
    'fixture.stress': 'RIG-PCFIX', 'drive.': 'RIG-DRIVE', 'ui.': 'RIG-UI-SCRIPT', 'shop.edit_price': 'RIG-SHOPEDIT',
}


def build(fixtures=FIXTURES, scripts=SCRIPTS, contract=CONTRACT, **kw):
    return gen.build_flow(fixtures, scripts, contract, DIGEST, **kw)


FLOW = build()
STEPS = FLOW['steps']
WINDOWS = FLOW['windows']


def index_of(step):
    return int(step['id']) - 1


def window_steps(wid):
    return [s for s in STEPS if s['window'] == wid]


def first_start(wid):
    return next(s for s in window_steps(wid) if s['op'] in ('trace.start', 'probe.measure_start'))


class AutosaveTests(unittest.TestCase):
    def test_autosave_verified_after_every_load(self):
        loads = [s for s in STEPS if s['op'] in LOADS]
        self.assertGreater(len(loads), 100)
        for s in loads:
            nxt = STEPS[index_of(s) + 1]
            self.assertEqual(nxt['op'], 'autosave.verify_off', f"step {s['id']} {s['op']} not followed by an autosave check")
            self.assertEqual(nxt['process'], s['process'])

    def test_autosave_verified_before_every_window(self):
        self.assertTrue(WINDOWS)
        for w in WINDOWS:
            start = first_start(w['id'])
            i = index_of(start) - 1
            while STEPS[i]['op'] != 'autosave.verify_off':
                self.assertNotIn(STEPS[i]['op'], TIME_JUMPS, f"{w['id']}: {STEPS[i]['op']} between check and start")
                i -= 1
            check = STEPS[i]
            self.assertEqual(check['window'], w['id'])
            self.assertEqual(check['process'], start['process'])
            self.assertLessEqual(index_of(start) - i, 6, f"{w['id']}: autosave check is not immediately before the start")

    def test_autosave_recorded_once_first_and_restored_last(self):
        records = [s for s in STEPS if s['op'] == 'autosave.record_disable']
        self.assertEqual(len(records), 1)
        first_launch = next(s for s in STEPS if s['op'] == 'process.launch')
        self.assertLess(index_of(records[0]), index_of(first_launch))
        restore = [s for s in STEPS if s['op'] == 'autosave.restore']
        self.assertEqual(len(restore), 1)
        last_stop = [s for s in STEPS if s['op'] == 'process.stop'][-1]
        self.assertGreater(index_of(restore[0]), index_of(last_stop))
        self.assertEqual(STEPS[index_of(restore[0]) + 1]['op'], 'autosave.verify_restored')

    def test_save_sequence_read_around_every_window(self):
        for w in WINDOWS:
            reads = [s for s in window_steps(w['id']) if s['op'] == 'save_epoch.read']
            self.assertEqual(len(reads), 2, w['id'])
            self.assertIn('unchanged', reads[1]['args']['expect'])


class WindowLengthTests(unittest.TestCase):
    def test_every_window_has_300_clean_seconds_after_warmup_by_construction(self):
        floor = CONTRACT['windows']['min_clean_real_seconds']
        warm = CONTRACT['windows']['warmup_real_seconds']
        gate = [w for w in WINDOWS if w['kind'] == 'window']
        self.assertGreater(len(gate), 80)
        for w in gate:
            steps = window_steps(w['id'])
            run = next(s for s in steps if s['op'] == 'script.run')
            terms = run['until']['all']
            clean = [t['clean_real_seconds_at_least'] for t in terms if 'clean_real_seconds_at_least' in t]
            self.assertEqual(len(clean), 1, w['id'])
            self.assertGreaterEqual(clean[0], floor, w['id'])
            self.assertTrue(all(len(t) == 1 for t in terms), 'stop terms are all-of, so none can end the window early')
            self.assertGreater(run['until']['abort_after_real_seconds'], clean[0])
            # Warm-up: the last load before the window, then readiness, then >= 60 s discarded, then the start.
            start = index_of(first_start(w['id']))
            last_load = max(index_of(s) for s in STEPS[:start] if s['op'] in LOADS)
            between = STEPS[last_load:start]
            ready = [index_of(s) for s in between if s['op'] in ('pc.readiness', 'native.readiness')]
            warmups = [s for s in between if s['op'] == 'wait.real' and s['args'].get('discard')
                       and s['args']['seconds'] >= warm and ready and index_of(s) > min(ready)]
            self.assertTrue(ready, f"{w['id']}: no readiness after the last load")
            self.assertTrue(warmups, f"{w['id']}: no {warm} s discarded warm-up after readiness")
            want = 'pc.readiness' if w['build'] == 'candidate' else 'native.readiness'
            self.assertTrue(any(s['op'] == want for s in between), w['id'])

    def test_game_span_at_declared_pace_covers_the_clean_floor(self):
        pace = FIXTURES['declared_pace']['real_seconds_per_game_minute']
        for w in WINDOWS:
            if w['kind'] == 'window' and w['game_span_minutes']:
                self.assertGreaterEqual(w['game_span_minutes'] * pace / w['labels']['native_speed'],
                                        CONTRACT['windows']['min_clean_real_seconds'], w['id'])

    def test_clean_floor_follows_the_contract(self):
        contract = copy.deepcopy(CONTRACT)
        contract['windows']['min_clean_real_seconds'] = 600
        flow = build(contract=contract)
        for s in flow['steps']:
            if s['op'] == 'script.run' and s['window'] and not s['window'].startswith(('p', 'G')):
                floor = next(t['clean_real_seconds_at_least'] for t in s['until']['all'] if 'clean_real_seconds_at_least' in t)
                self.assertGreaterEqual(floor, 600, s['window'])

    def test_first_window_of_every_process_is_a_discarded_prime(self):
        by_process = {}
        for w in WINDOWS:
            if w['kind'] != 'segment':
                by_process.setdefault(w['process'], []).append(w)
        for p, ws in by_process.items():
            self.assertEqual(ws[0]['kind'], 'prime', f'process {p}')
            self.assertTrue(ws[0]['labels']['first_in_process'])
            self.assertTrue(all(not w['labels']['first_in_process'] for w in ws[1:]))
        for w in WINDOWS:
            if w['kind'] == 'segment' and w['latency']:
                self.assertEqual(w['checks'], ['G10'], 'only latency segments measure a fresh process')
            elif w['kind'] == 'segment':
                self.assertTrue(any(x['kind'] == 'prime' and x['process'] == w['process'] for x in WINDOWS), w['id'])

    def test_window_starts_at_the_window_minute_in_both_builds(self):
        for w in WINDOWS:
            if w['kind'] == 'window' and w['labels']['condition'].startswith('W-'):
                gate = [s for s in window_steps(w['id']) if s['op'] == 'wait.until_game_minute']
                self.assertEqual(len(gate), 1, w['id'])
                self.assertEqual(gate[0]['args']['minute'], w['labels']['checkpoint_minute'])
                self.assertLess(index_of(gate[0]), index_of(first_start(w['id'])))


class OrderTests(unittest.TestCase):
    def conditions(self):
        groups = {}
        for w in WINDOWS:
            if w['kind'] == 'window':
                key = (w['labels']['condition'], w['script'], w['labels']['arm'])
                groups.setdefault(key, []).append(w)
        return groups

    def test_abab_order_per_condition(self):
        both = 0
        for key, ws in self.conditions().items():
            builds = [w['build'] for w in ws]
            if set(builds) != {'reference', 'candidate'}:
                continue
            both += 1
            reps = len(ws) // 2
            self.assertEqual(builds, ['reference', 'candidate'] * reps, key)
            self.assertEqual([w['labels']['repetition'] for w in ws], [r for r in range(1, reps + 1) for _ in (0, 1)], key)
            for a, b in zip(ws[::2], ws[1::2]):
                self.assertNotEqual(a['process'], b['process'], 'a build switch needs its own process')
        self.assertGreaterEqual(both, 14)

    def test_repetitions_follow_the_plan(self):
        for (condition, script, arm), ws in self.conditions().items():
            if arm != 'primary' or condition.startswith('G'):
                continue
            per_build = len([w for w in ws if w['build'] == 'candidate'])
            self.assertEqual(per_build, 4 if condition in ('W-morning', 'W-evening') else 2, (condition, script))

    def test_matrix_covers_every_window_and_script(self):
        keys = set(self.conditions())
        for name, w in FIXTURES['windows'].items():
            for script in w['scripts']:
                self.assertIn((name, script, 'primary'), keys)
        self.assertIn(('W-evening@High', 'S-path', 'report-only'), keys)
        self.assertIn(('W-evening@caps-256-64', 'S-path', 'report-only'), keys)
        for name in ('W-morning', 'W-evening', 'W-midnight'):
            self.assertIn((f'{name}@Stress10k', 'S-path', 'stress'), keys)

    def test_labels_never_collide_for_the_analyzer(self):
        seen = set()
        for w in WINDOWS:
            if w['kind'] != 'window':
                continue
            lab = w['labels']
            key = (w['export_group'], lab['build'], lab['condition'], lab['script'], lab['arm'], lab['repetition'])
            self.assertNotIn(key, seen)
            seen.add(key)
            self.assertIn(lab['arm'], CONTRACT['windows']['arms'])
            self.assertEqual(lab['source'], 'native')

    def test_batch_order_follows_the_plan(self):
        first = {}
        for s in STEPS:
            for c in s['checks']:
                first.setdefault(c, index_of(s))
        order = ['G1', 'G2', 'G3', 'G4', 'G9', 'G13', 'G14', 'G15', 'G10', 'G11', 'cleanup']
        self.assertEqual(sorted(order, key=first.get), order)
        header = next(s for s in STEPS if s['op'] == 'header.require')
        self.assertLess(index_of(header), min(index_of(s) for s in STEPS if s['op'] in ('trace.start', 'probe.measure_start')))


class SaveTests(unittest.TestCase):
    def test_reference_arm_never_saves(self):
        reference = {p['process'] for p in FLOW['processes'] if p['build'] == 'reference'}
        self.assertGreater(len(reference), 15)
        for s in STEPS:
            if s['process'] in reference or s['build'] == 'reference':
                self.assertNotIn(s['op'], ('ft.save', 'autosave.restore'), s['id'])
            if s['op'] == 'fixture.copy' and s['build'] == 'reference':
                self.assertTrue(s['args']['never_save'])
        for p in FLOW['processes']:
            if p['build'] == 'reference':
                mods = next(s for s in STEPS if s['process'] == p['process'] and s['op'] == 'modset.apply')
                self.assertIn('without PersistentCity', mods['args']['set'])
                arm = [s for s in STEPS if s['process'] == p['process'] and s['op'] == 'probe.arm']
                self.assertTrue(arm and all(a['args']['mode'] == 'reference' and 0.1 <= a['args']['factor'] <= 1 for a in arm))

    def test_saves_only_in_fixture_preparation_and_never_inside_a_window(self):
        saves = [s for s in STEPS if s['op'] == 'ft.save']
        self.assertTrue(saves)
        for s in saves:
            self.assertEqual(s['checks'], ['fixtures'])
            self.assertIsNone(s['window'])
            self.assertEqual(s['build'], 'candidate')
        for w in WINDOWS:
            self.assertFalse([s for s in window_steps(w['id']) if s['op'] in ('ft.save', 'autosave.restore')], w['id'])

    def test_every_window_checkpoint_is_saved_before_it_is_loaded(self):
        saved = {}
        for s in STEPS:
            if s['op'] == 'ft.save':
                saved[s['args']['save']] = index_of(s)
            if s['op'] == 'fixture.copy':
                self.assertIn(s['args']['source'], saved)
                self.assertLess(saved[s['args']['source']], index_of(s))


class CapabilityTests(unittest.TestCase):
    def test_each_pending_capability_is_named_on_the_step_that_needs_it(self):
        hits = {v: 0 for v in OP_NEEDS.values()}
        for s in STEPS:
            for prefix, cap in OP_NEEDS.items():
                if s['op'] == prefix or (prefix.endswith('.') and s['op'].startswith(prefix)):
                    self.assertIn(cap, s['capabilities']['pending'], f"step {s['id']} {s['op']}")
                    hits[cap] += 1
            for nested in s.get('loop', []) + s.get('samplers', []):
                for prefix, cap in OP_NEEDS.items():
                    if nested['op'] == prefix or (prefix.endswith('.') and nested['op'].startswith(prefix)):
                        self.assertIn(cap, nested['capabilities']['pending'])
                        self.assertIn(cap, s['capabilities']['pending'], f"script.run {s['id']} hides {cap}")
                        hits[cap] += 1
        self.assertTrue(all(hits.values()), hits)

    def test_pending_names_are_pending_in_the_catalogue(self):
        catalogue = FLOW['capabilities']
        for s in STEPS:
            for name in s['capabilities']['pending']:
                self.assertEqual(catalogue[name]['status'], 'pending')
            for name in s['capabilities']['existing']:
                self.assertEqual(catalogue[name]['status'], 'existing')

    def test_each_check_names_the_plan_pending_capabilities(self):
        for check, want in PLAN_PENDING.items():
            summary = FLOW['checks'][check]
            have = set(summary['pending']) | set(summary['pending_via_fixture'])
            self.assertLessEqual(want, have, f'{check} misses {sorted(want - have)}')

    def test_reference_manage_script_has_no_pc_screens(self):
        runs = [s for s in STEPS if s['op'] == 'script.run' and s['args']['script'] == 'S-manage']
        self.assertTrue(runs)
        for s in runs:
            keys = [n['args'].get('keys') for n in s['loop'] if n['op'] == 'ui.key']
            clicks = [n for n in s['loop'] if n['op'] == 'ui.click']
            if s['build'] == 'reference':
                self.assertNotIn('Ctrl+F8', keys)
                self.assertFalse(clicks)
            else:
                self.assertIn('Ctrl+F8', keys)
                self.assertEqual(len(clicks), 3)

    def test_dense_scripts_sample_bodies(self):
        for s in STEPS:
            if s['op'] == 'script.run' and s['args']['script'] == 'S-path':
                every = {n['op']: n['every_real_seconds'] for n in s['samplers']}
                self.assertEqual(every.get('view.screen_pos'), 10)
                self.assertEqual(every.get('log.physical_diagnostics'), 60)

    def test_pending_index_matches_the_steps(self):
        counts = {}
        for s in STEPS:
            for n in s['capabilities']['pending']:
                counts[n] = counts.get(n, 0) + 1
        self.assertEqual({n: v['steps'] for n, v in FLOW['pending_capabilities'].items()}, counts)

    def test_unknown_op_is_refused(self):
        scripts = copy.deepcopy(SCRIPTS)
        scripts['S-path']['loop'].append(dict(op='camera.teleport_somewhere', args={}))
        with self.assertRaises(gen.FlowError):
            build(scripts=scripts)


class HeaderTests(unittest.TestCase):
    def test_run_header_has_every_decision8_and_i1_field_with_a_filling_step(self):
        header = FLOW['run_header']
        ids = {s['id']: s for s in STEPS}
        fields = CONTRACT['header']['required'] + ['logical_population', 'business_load', 'calendar_pace',
                                                   'observed_body_load']
        for field in fields:
            self.assertIn(field, header)
            if field == 'contract_version':
                self.assertEqual(header[field]['value'], 'perf-gate/v1')
                continue
            self.assertIn(header[field]['filled_by'], ids, field)
        self.assertEqual(ids[header['machine']['filled_by']]['args']['record'], CONTRACT['header']['machine_fields'])
        self.assertEqual(ids[header['logical_population']['filled_by']]['args']['command'], 'pc population manifest')
        self.assertEqual(header['calendar_pace']['declared']['real_seconds_per_game_minute'], 3.3)

    def test_movement_label(self):
        self.assertEqual(build(movement_state='movement=pre-F2d')['run_header']['movement_state']['value'],
                         'movement=pre-F2d')
        rule = next(s for s in STEPS if s['op'] == 'source.identity')['args']['rule']
        self.assertIn('movement=pre-F2d', rule)
        self.assertIn(gen.F2D_COMMIT, rule)


class CliTests(unittest.TestCase):
    def test_cli_output_is_deterministic(self):
        tmp = Path(tempfile.mkdtemp())
        try:
            outs = []
            for i in (1, 2):
                out = tmp / f'flow{i}.json'
                r = subprocess.run([sys.executable, str(HERE / 'make_gate_flow.py'), '--out', str(out),
                                    '--md-out', str(tmp / f'flow{i}.md')], capture_output=True, text=True)
                self.assertEqual(r.returncode, 0, r.stderr)
                outs.append(out.read_bytes())
            self.assertEqual(outs[0], outs[1])
            self.assertEqual(json.loads(outs[0])['format'], 'perf-gate-flow/v1')
        finally:
            shutil.rmtree(tmp)

    def test_cli_refuses_a_bad_script(self):
        tmp = Path(tempfile.mkdtemp())
        try:
            scripts = tmp / 'scripts'
            shutil.copytree(HERE / 'scripts', scripts)
            bad = json.loads((scripts / 'S-path.json').read_text())
            bad['name'] = 'S-other'
            (scripts / 'S-path.json').write_text(json.dumps(bad))
            r = subprocess.run([sys.executable, str(HERE / 'make_gate_flow.py'), '--out', str(tmp / 'f.json'),
                                '--scripts', str(scripts)], capture_output=True, text=True)
            self.assertEqual(r.returncode, 2)
            self.assertIn('REFUSED', r.stderr)
            self.assertFalse((tmp / 'f.json').exists())
        finally:
            shutil.rmtree(tmp)


if __name__ == '__main__':
    unittest.main()
