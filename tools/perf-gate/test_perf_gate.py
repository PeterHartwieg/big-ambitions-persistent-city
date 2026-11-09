"""F4a offline checks: python -m unittest discover -s tools/perf-gate -p test_*.py"""
import copy
import importlib.util
import json
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import analyze  # noqa: E402
import report  # noqa: E402
import summary_lines  # noqa: E402

_spec = importlib.util.spec_from_file_location('synthetic', HERE / 'fixtures' / 'synthetic.py')
synthetic = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(synthetic)
SUMMARY_FIXTURE = (HERE / 'fixtures' / 'summary-lines.txt').read_text(encoding='utf-8').splitlines()
CONTRACT = analyze.load_contract()


def run(*windows, source='native', **header):
    return analyze.analyze([synthetic.export(list(windows), source=source, **header)], CONTRACT)


def win(report_, wid):
    return next(w for w in report_['windows'] if w['id'] == wid)


class ContractTests(unittest.TestCase):
    def test_contract_freezes_decision_4(self):
        rows = CONTRACT['_rows']
        self.assertEqual(CONTRACT['contract'], 'perf-gate/v1')
        self.assertEqual(list(rows), ['M1', 'M2', 'M3', 'M4', 'P1', 'P2', 'P3', 'P4', 'S1', 'S2', 'S3', 'S4', 'S5', 'L1', 'L2', 'L3', 'L4', 'R1'])
        self.assertEqual((rows['M1']['comparator'], rows['M1']['threshold']), ('<', 5.0))
        self.assertEqual(rows['M2']['threshold'], 30.0)
        self.assertEqual((rows['M3']['threshold'], rows['M4']['threshold']), (10.0, 1.5))
        self.assertEqual((rows['P1']['threshold'], rows['P2']['threshold']), (16.7, 25.0))
        self.assertEqual((rows['P3']['stall_ms'], rows['P3']['max_stalls_per_5_min'], rows['P3']['severe_stall_ms']), (50.0, 2, 250.0))
        self.assertEqual((rows['P4']['p95_max_ms'], rows['P4']['p99_max_ms']), (1.5, 3.0))
        self.assertEqual((rows['S3']['held_fraction_max'], rows['S3']['longest_hold_max_s']), (0.01, 3.0))
        self.assertEqual((rows['S5']['hour_ratio_max'], rows['S5']['day_ratio_max']), (2.0, 1.25))
        self.assertEqual([rows[r]['threshold'] for r in ('L1', 'L2', 'L3')], [60.0, 50.0, 30.0])
        self.assertEqual(CONTRACT['windows']['min_clean_real_seconds'], 300)

    def test_thresholds_come_from_the_contract(self):
        w = synthetic.window('c', mod=[6.0] * 100)
        self.assertEqual(run(w)['rows']['M1']['verdict'], 'fail')
        relaxed = copy.deepcopy(CONTRACT)
        relaxed['_rows']['M1']['threshold'] = 7.0
        self.assertEqual(analyze.analyze([synthetic.export([w])], relaxed)['rows']['M1']['verdict'], 'pass')


class PercentileTests(unittest.TestCase):
    def test_nearest_rank_matches_hand_computed_values(self):
        values = list(range(1, 21))  # 1..20
        # p50: ceil(10) = rank 10 -> 10; p95: ceil(19) = 19 -> 19; p99: ceil(19.8) = 20 -> 20.
        self.assertEqual([analyze.nearest_rank(values, p) for p in (50, 95, 99)], [10, 19, 20])
        seven = [0.5, 1.0, 2.0, 3.0, 4.0, 8.0, 40.0]
        # n=7: p50 rank ceil(3.5)=4 -> 3.0; p95 rank ceil(6.65)=7 -> 40.0; p99 rank 7.
        self.assertEqual([analyze.nearest_rank(seven, p) for p in (50, 95, 99)], [3.0, 40.0, 40.0])
        self.assertEqual(analyze.nearest_rank([4.2], 95), 4.2)
        self.assertIsNone(analyze.nearest_rank([], 95))
        # n=100: p95 rank exactly 95, no interpolation.
        hundred = [float(i) for i in range(1, 101)]
        self.assertEqual(analyze.nearest_rank(hundred, 95), 95.0)
        d = analyze.distribution([3.0, 1.0, 2.0])
        self.assertEqual((d['p50_ms'], d['p95_ms'], d['max_ms'], d['mean_ms']), (2.0, 3.0, 3.0, 2.0))

    def test_window_m1_is_nearest_rank_of_its_own_frames(self):
        mod = [0.1] * 94 + [4.0] + [9.0] * 5  # 100 frames: rank 95 -> 4.0, rank 99 -> 9.0
        r = run(synthetic.window('c', mod=mod))
        w = win(r, 'c')
        self.assertEqual((w['mod_work']['p95_ms'], w['mod_work']['p99_ms']), (4.0, 9.0))
        self.assertEqual(w['rows']['M1']['verdict'], 'pass')

    def test_pooled_figures_pool_raw_frames_not_percentiles(self):
        a = synthetic.window('a', mod=[1.0] * 100, repetition=1)
        b = synthetic.window('b', mod=[1.0] * 90 + [10.0] * 10, repetition=2)
        r = run(a, b)
        pooled = next(g for g in r['pooled'] if g['build'] == 'candidate')
        self.assertEqual(sorted(pooled['windows']), ['a', 'b'])
        # Per-window p95: a=1.0, b=10.0; their mean would be 5.5, their p95 10.0.
        # Pool of 200 raw frames, 10 at 10.0: rank ceil(190) = 190 -> 1.0.
        self.assertEqual(pooled['mod_work']['p95_ms'], 1.0)
        self.assertEqual(pooled['mod_work']['count'], 200)
        self.assertNotIn(pooled['mod_work']['p95_ms'], (5.5, 10.0))
        # Pool p99: rank 198 -> 10.0.
        self.assertEqual(pooled['mod_work']['p99_ms'], 10.0)

    def test_invalid_windows_are_not_pooled(self):
        a = synthetic.window('a', mod=[1.0] * 100)
        b = synthetic.window('b', mod=[50.0] * 100, repetition=2, aborted=True)
        pooled = next(g for g in run(a, b)['pooled'] if g['build'] == 'candidate')
        self.assertEqual(pooled['windows'], ['a'])


class HeaderTests(unittest.TestCase):
    def test_every_missing_header_field_refuses_the_report(self):
        for field in CONTRACT['header']['required']:
            with self.subTest(field=field):
                e = synthetic.export([synthetic.window('c')])
                del e['header'][field]
                with self.assertRaisesRegex(analyze.Refused, field):
                    analyze.analyze([e], CONTRACT)
            with self.subTest(empty=field):
                e = synthetic.export([synthetic.window('c')])
                e['header'][field] = ''
                with self.assertRaises(analyze.Refused):
                    analyze.analyze([e], CONTRACT)

    def test_missing_nested_header_fields_refuse(self):
        cases = [('machine', 'gpu_driver'), ('settings_read_back', 'vsync')]
        for field, sub in cases:
            with self.subTest(field=field, sub=sub):
                e = synthetic.export([synthetic.window('c')])
                del e['header'][field][sub]
                with self.assertRaisesRegex(analyze.Refused, f'{field}.{sub}'):
                    analyze.analyze([e], CONTRACT)
        e = synthetic.export([synthetic.window('c')])
        del e['header']['mod_set'][1]['sha256']
        with self.assertRaisesRegex(analyze.Refused, r'mod_set\[1\]\.sha256'):
            analyze.analyze([e], CONTRACT)

    def test_contract_version_mismatch_refuses(self):
        with self.assertRaisesRegex(analyze.Refused, 'contract_version'):
            run(synthetic.window('c'), contract_version='perf-gate/v0')

    def test_na_header_values_only_for_offline_source(self):
        with self.assertRaisesRegex(analyze.Refused, 'rig_commit'):
            run(synthetic.window('c'), rig_commit='n/a')
        offline = run(synthetic.window('c'), source='offline', rig_commit='n/a', rig_overlay='n/a')
        self.assertEqual(offline['source'], 'offline')
        with self.assertRaisesRegex(analyze.Refused, 'source_commit'):
            run(synthetic.window('c'), source='offline', source_commit='n/a')

    def test_cli_refusal_writes_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            e = synthetic.export([synthetic.window('c')])
            del e['header']['pc_dll_sha256']
            path = Path(tmp, 'export.json')
            path.write_text(json.dumps(e), encoding='utf-8')
            code = subprocess.run([sys.executable, str(HERE / 'analyze.py'), str(path), '--json-out', str(Path(tmp, 'r.json')),
                                   '--md-out', str(Path(tmp, 'r.md'))], capture_output=True, text=True)
            self.assertEqual(code.returncode, 2)
            self.assertIn('REFUSED', code.stderr)
            self.assertFalse(Path(tmp, 'r.json').exists())
            self.assertFalse(Path(tmp, 'r.md').exists())


class InvalidationTests(unittest.TestCase):
    def test_any_failed_receipt_invalidates_the_window(self):
        for receipt in CONTRACT['receipts']['required']:
            with self.subTest(receipt=receipt):
                w = synthetic.window('c')
                w['receipts'][receipt] = 'fail'
                r = run(w)
                self.assertFalse(win(r, 'c')['valid'])
                self.assertIn(f'receipt failed: {receipt}', win(r, 'c')['invalid_reasons'])
                self.assertEqual(r['rows']['M1']['verdict'], 'not established')
                self.assertNotEqual(r['overall'], 'pass')

    def test_missing_receipt_invalidates(self):
        w = synthetic.window('c')
        del w['receipts']['autosave']
        self.assertIn('receipt missing: autosave', win(run(w), 'c')['invalid_reasons'])

    def test_reference_may_mark_only_pc_receipts_na(self):
        ref = synthetic.window('r', build='reference')
        self.assertTrue(win(run(ref), 'r')['valid'])
        cand = synthetic.window('c')
        cand['receipts']['pc_readiness'] = 'n/a'
        self.assertIn('receipt n/a not allowed: pc_readiness', win(run(cand), 'c')['invalid_reasons'])
        ref2 = synthetic.window('r', build='reference')
        ref2['receipts']['autosave'] = 'n/a'
        self.assertIn('receipt n/a not allowed: autosave', win(run(ref2), 'r')['invalid_reasons'])

    def test_invalid_window_is_kept_but_excluded(self):
        good = synthetic.window('good', mod=[0.5] * 100)
        bad = synthetic.window('bad', mod=[20.0] * 100, repetition=2)
        bad['receipts']['autosave'] = 'fail'
        r = run(good, bad)
        self.assertEqual([w['id'] for w in r['windows']], ['good', 'bad'])
        self.assertEqual(r['rows']['M1']['verdict'], 'pass')

    def test_short_first_and_aborted_windows_are_invalid(self):
        for overrides, reason in [(dict(clean_real_seconds=299.0), 'short'), (dict(first_in_process=True), 'first window'),
                                  (dict(aborted=True), 'aborted')]:
            with self.subTest(reason=reason):
                w = synthetic.window('c', **overrides)
                self.assertTrue(any(reason in x for x in win(run(w), 'c')['invalid_reasons']))

    def test_probe_export_flags_invalidate(self):
        w = synthetic.window('c')
        rows = [dict(frame=i, modWorkMs=0.5, wholeFrameMs=16.0, observerMs=0.01, diagnostic=i == 0) for i in range(50)]
        del w['frames']
        w['probe'] = dict(status=dict(timing_coverage_complete=True, cross_frame_scopes=0), frames=rows)
        entry = win(run(w), 'c')
        self.assertTrue(entry['valid'])
        self.assertEqual((entry['frames'], entry['clean_frames'], entry['diagnostic_frames']), (50, 49, 1))
        w['probe']['status']['cross_frame_scopes'] = 1
        self.assertIn('receipt failed: probe_flags', win(run(w), 'c')['invalid_reasons'])
        w['probe']['status'] = dict(timing_coverage_complete=False)
        self.assertIn('receipt failed: probe_coverage', win(run(w), 'c')['invalid_reasons'])


class SourceTests(unittest.TestCase):
    def test_mixed_offline_and_native_windows_are_refused(self):
        offline_window = synthetic.window('o', source='offline')
        with self.assertRaisesRegex(analyze.Refused, 'offline and native'):
            run(synthetic.window('c'), offline_window)

    def test_mixed_offline_and_native_exports_are_refused(self):
        native = synthetic.export([synthetic.window('c')])
        offline = synthetic.export([synthetic.window('o')], source='offline')
        offline['header'] = native['header']
        with self.assertRaisesRegex(analyze.Refused, 'offline and native'):
            analyze.analyze([native, offline], CONTRACT)

    def test_missing_source_and_different_headers_are_refused(self):
        e = synthetic.export([synthetic.window('c')])
        del e['source']
        with self.assertRaises(analyze.Refused):
            analyze.analyze([e], CONTRACT)
        a = synthetic.export([synthetic.window('a')])
        b = synthetic.export([synthetic.window('b', repetition=2)], source_commit='other')
        with self.assertRaisesRegex(analyze.Refused, 'different headers'):
            analyze.analyze([a, b], CONTRACT)


class PairingTests(unittest.TestCase):
    def pair(self, ref_overrides=None, cand_overrides=None, ref_presented=None, cand_presented=None):
        cand = synthetic.window('c', presented=cand_presented or [12.0] * 100, **(cand_overrides or {}))
        ref = synthetic.window('r', build='reference', presented=ref_presented or [11.0] * 100, **(ref_overrides or {}))
        r = run(cand, ref)
        return r, r['pairs'][0]

    def test_matched_pair_gives_delta(self):
        r, pair = self.pair()
        self.assertEqual(pair['verdict'], 'pass')
        self.assertAlmostEqual(pair['value']['p95_ms'], 1.0)
        self.assertEqual(r['rows']['P4']['verdict'], 'pass')
        _, failing = self.pair(cand_presented=[13.0] * 100)
        self.assertEqual(failing['verdict'], 'fail')

    def test_p4_pairs_only_matched_windows(self):
        cases = [('checkpoint', dict(checkpoint_minute=1066)), ('span', dict(game_span_minutes=91.5)),
                 ('pace', dict(reference_pace_ratio=1.03)), ('pace missing', dict(reference_pace_ratio=None))]
        for name, overrides in cases:
            with self.subTest(name):
                r, pair = self.pair(ref_overrides=overrides)
                self.assertEqual(pair['verdict'], 'not established')
                self.assertIsNone(pair['value'])
                self.assertEqual(r['rows']['P4']['verdict'], 'not established')
        # Inside tolerance: span 0.9 game minutes and pace 1.9 % still match.
        _, ok = self.pair(ref_overrides=dict(game_span_minutes=90.9, reference_pace_ratio=1.019))
        self.assertEqual(ok['verdict'], 'pass')

    def test_p4_never_pairs_across_condition_script_or_repetition(self):
        for overrides in (dict(condition='W-lunch'), dict(script='S-stationary'), dict(repetition=2)):
            with self.subTest(**overrides):
                _, pair = self.pair(ref_overrides=overrides)
                self.assertIsNone(pair['reference'])
                self.assertEqual(pair['verdict'], 'not established')

    def test_invalid_reference_gives_no_delta(self):
        cand = synthetic.window('c', presented=[12.0] * 100)
        ref = synthetic.window('r', build='reference', presented=[11.0] * 100)
        ref['receipts']['focused'] = 'fail'
        pair = run(cand, ref)['pairs'][0]
        self.assertEqual(pair['verdict'], 'not established')

    def test_not_established_when_reference_misses_p1(self):
        # Reference p95 17.0 ms misses P1 (<= 16.7); candidate 17.5 ms would fail P1 on its own.
        r, pair = self.pair(ref_presented=[17.0] * 100, cand_presented=[17.5] * 100)
        cand = win(r, 'c')
        self.assertEqual(cand['rows']['P1']['verdict'], 'not established')
        self.assertIn(analyze.NATIVE_BASELINE, cand['rows']['P1']['reason'])
        self.assertEqual(r['rows']['P1']['verdict'], 'not established')
        self.assertEqual(r['sixty_fps'], analyze.NATIVE_BASELINE)
        # The verdict then rests on P4 and the M rows: P4 is judged, never attributed away.
        self.assertEqual(pair['verdict'], 'pass')
        # Without a reference that misses P1, the same candidate fails P1.
        r2, _ = self.pair(ref_presented=[16.0] * 100, cand_presented=[17.5] * 100)
        self.assertEqual(r2['rows']['P1']['verdict'], 'fail')
        self.assertIsNone(r2['sixty_fps'])


class RowTests(unittest.TestCase):
    def test_s_rows(self):
        w = synthetic.window('c')
        w['accounting'] = dict(clock_misses=1, lag_samples=[[0, 0.0], [10, 1.2]], holds=[dict(start_s=5.0, end_s=9.0)])
        rows = win(run(w), 'c')['rows']
        self.assertEqual([rows[r]['verdict'] for r in ('S1', 'S2', 'S3')], ['fail', 'fail', 'fail'])
        self.assertEqual(rows['S4']['verdict'], 'not established')  # lag never back below 0.1 after the hold
        w['accounting'] = dict(clock_misses=0, lag_samples=[[0, 0.0], [9.5, 0.4], [12.0, 0.05]], holds=[dict(start_s=8.0, end_s=9.0)])
        rows = win(run(w), 'c')['rows']
        self.assertEqual([rows[r]['verdict'] for r in ('S1', 'S2', 'S3', 'S4')], ['pass', 'pass', 'pass', 'pass'])
        self.assertEqual(rows['S4']['value'], 3.0)
        w['native_speed'] = 6
        self.assertEqual(win(run(w), 'c')['rows']['S3']['verdict'], 'n/a')

    def test_p3_stalls(self):
        presented = [10.0] * 97 + [60.0, 60.0, 60.0]
        self.assertEqual(win(run(synthetic.window('c', presented=presented)), 'c')['rows']['P3']['verdict'], 'fail')
        severe = [10.0] * 99 + [300.0]
        self.assertEqual(win(run(synthetic.window('c', presented=severe)), 'c')['rows']['P3']['verdict'], 'not established')
        classed = synthetic.window('c', presented=severe, stall_classes={'99': ['pc-union']})
        self.assertEqual(win(run(classed), 'c')['rows']['P3']['verdict'], 'fail')
        native = synthetic.window('c', presented=severe, stall_classes={'99': ['native-frame']})
        self.assertEqual(win(run(native), 'c')['rows']['P3']['verdict'], 'pass')

    def test_stress_arm_is_recorded_but_not_in_overall(self):
        primary = synthetic.window('p')
        stress = synthetic.window('s', arm='stress', mod=[8.0] * 100, repetition=2)
        r = run(primary, stress)
        self.assertEqual(r['rows']['M1']['verdict'], 'pass')
        self.assertEqual(r['secondary_arm_rows']['M1']['verdict'], 'fail')
        self.assertEqual(set(r['secondary_arm_rows']), {'M1', 'S1', 'S2'})

    def test_overall_needs_every_row(self):
        cand = synthetic.window('c', presented=[11.5] * 100)
        ref = synthetic.window('r', build='reference', presented=[11.0] * 100)
        e = synthetic.export([cand, ref])
        r = analyze.analyze([e], CONTRACT)
        self.assertEqual(r['overall'], 'not established')  # S5, L1-L4, R1 have no records
        e['measurements'] = dict(
            S5=dict(hours=[dict(real_s=120, unheld_real_s=100)], days=[dict(real_s=110, unheld_real_s=100)], s1_s2_hold=True),
            L1=dict(samples_s=[40.0]), L2=dict(samples_ms=[31.0, 20.0]), L3=dict(samples_s=[12.0]),
            L4=dict(real_s=30.0, reference_real_s=20.0, s1_s2_hold=True), R1=dict(heap_mb_per_day=[1000, 1020, 1040], m1_p95_per_day=[0.5, 0.55, 0.58]))
        r = analyze.analyze([e], CONTRACT)
        self.assertEqual(r['overall'], 'pass', {k: v['verdict'] for k, v in r['rows'].items()})
        self.assertEqual(r['rows']['L2']['flagged'], [31.0])
        e['measurements']['L2'] = dict(samples_ms=[51.0])
        self.assertEqual(analyze.analyze([e], CONTRACT)['overall'], 'fail')


class SummaryLineTests(unittest.TestCase):
    def test_parser_reads_fix128_and_all_segments(self):
        parsed = summary_lines.parse_lines(SUMMARY_FIXTURE)
        self.assertEqual(len(parsed), 3)  # the traffic line is not a summary
        current = parsed[0]
        self.assertEqual(current['advancement'], dict(frames=1795, events=412, total_ms=214.66, max_advance_ms=42.483, max_events=16))
        self.assertEqual(current['slowest_advance'], dict(unit_ms=39.688, unit_kind='route-planning', optional_stop_ms=0.0, gc_count=1))
        self.assertEqual(current['optional_stops'], dict(calls=88, total_ms=3.115, max_ms=0.412, park_trigger_scans=0))
        self.assertEqual(current['routes']['rebuild_reasons'], {'gate': 2, 'initial': 1})
        self.assertEqual(current['routes']['last_rebuild'], 'gate index=4')
        self.assertEqual(current['planning']['slowest_unit'], 'walk-leg')
        self.assertEqual(current['planning_jobs']['admission_deferrals'], 4)
        self.assertEqual(current['closure'], dict(calls=1795, total_ms=10.452, max_ms=0.042))
        self.assertEqual(current['population']['lag_minutes'], 0.1)
        self.assertEqual((current['physical']['pedestrians'], current['physical']['cars']), (31, 7))
        self.assertEqual(current['physical']['carAdmission'], 'person-17:spawn:none, retry later')
        older = parsed[1]
        self.assertIsNone(older['slowest_advance'])  # 6 Nov build: no fix-128 fields, never guessed
        self.assertEqual(older['advancement']['max_advance_ms'], 34.229)
        self.assertEqual(parsed[2]['physical'], {'physical': False, 'inactive': True})

    def spikes(self):
        return [dict(frame=10, advance_ms=42.49), dict(frame=20, advance_ms=35.0), dict(frame=30, advance_ms=None)]

    def test_fix128_fields_attach_only_to_the_matching_frame(self):
        obs = [dict(summary=summary_lines.parse_line(SUMMARY_FIXTURE[0]))]
        spikes = summary_lines.attach_fix128(self.spikes(), obs, 0.1)
        matched, other, outside = spikes
        self.assertEqual(matched['advancement_unit']['unit_kind'], 'route-planning')
        self.assertEqual(matched['advancement_unit']['gc_count'], 1)
        self.assertEqual(matched['advancement_unit']['source'], 'summary-maximum')
        self.assertEqual(set(other['advancement_unit'].values()), {'unknown'})
        self.assertIsNone(outside['advancement_unit'])  # outside advancement: no advancement fields

    def test_ambiguous_or_out_of_range_maximum_attaches_nowhere(self):
        obs = [dict(summary=summary_lines.parse_line(SUMMARY_FIXTURE[0]))]
        twins = [dict(frame=10, advance_ms=42.45), dict(frame=11, advance_ms=42.52)]
        for spike in summary_lines.attach_fix128(twins, obs, 0.1):
            self.assertEqual(spike['advancement_unit']['source'], 'unknown')
        ranged = [dict(summary=obs[0]['summary'], first_frame=100, last_frame=200)]
        for spike in summary_lines.attach_fix128(self.spikes(), ranged, 0.1)[:2]:
            self.assertEqual(spike['advancement_unit']['source'], 'unknown')
        older = [dict(summary=summary_lines.parse_line(SUMMARY_FIXTURE[1]))]
        for spike in summary_lines.attach_fix128([dict(frame=1, advance_ms=34.229)], older, 0.1):
            self.assertEqual(spike['advancement_unit']['source'], 'unknown')

    def test_analyzer_attaches_through_the_window(self):
        mod = [0.5] * 100
        mod[10], mod[20] = 43.0, 36.0
        w = synthetic.window('c', mod=mod, summary_lines=[SUMMARY_FIXTURE[0]])
        w['frames'][10]['components'] = {'AdvancePopulation': 42.49, 'PopulationRuntime.Update': 43.0}
        w['frames'][20]['components'] = {'PhysicalPopulation.Update': 36.0}
        w['advance_calls'] = [dict(frame=10, duration_ms=42.49, gc_delta=1, optional_stop_ms=0.0)]
        r = run(w)
        spikes = {s['frame']: s for s in r['spikes']}
        self.assertEqual(sorted(spikes), [10, 20])
        self.assertEqual(spikes[10]['advancement_unit']['unit_kind'], 'route-planning')
        self.assertEqual(spikes[10]['advance_call']['gc_delta'], 1)
        self.assertIsNone(spikes[20]['advancement_unit'])
        self.assertEqual(spikes[20]['component'], 'PhysicalPopulation.Update')
        self.assertEqual(r['rows']['M2']['verdict'], 'flag')
        self.assertIn('route-planning', report.render_markdown(r))


class ReportTests(unittest.TestCase):
    def test_cli_writes_json_and_markdown(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp, 'export.json')
            path.write_text(json.dumps(synthetic.export([synthetic.window('c'), synthetic.window('r', build='reference')])), encoding='utf-8')
            out = subprocess.run([sys.executable, str(HERE / 'analyze.py'), str(path), '--json-out', str(Path(tmp, 'r.json')),
                                  '--md-out', str(Path(tmp, 'r.md'))], capture_output=True, text=True)
            self.assertEqual(out.returncode, 0, out.stderr)
            data = json.loads(Path(tmp, 'r.json').read_text(encoding='utf-8'))
            self.assertEqual((data['format'], data['source'], data['contract']), ('perf-gate-report/v1', 'native', 'perf-gate/v1'))
            md = Path(tmp, 'r.md').read_text(encoding='utf-8')
            for heading in ('## Header', '## Row verdicts', '## Windows', '## Invalid windows', '## P4 pairs', '## M2 spikes', '## Stalls'):
                self.assertIn(heading, md)

    def test_fifty_window_export_runs_in_seconds(self):
        e = synthetic.large_export(count=50, frames_per_window=2000)
        started = time.perf_counter()
        r = analyze.analyze([e], CONTRACT)
        report.render_markdown(r)
        self.assertLess(time.perf_counter() - started, 30)
        self.assertEqual(len(r['windows']), 50)
        self.assertEqual(len(r['spikes']), 25)


if __name__ == '__main__':
    unittest.main()
