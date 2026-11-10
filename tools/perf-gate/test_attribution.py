"""F4c offline checks: python -m unittest discover -s tools/perf-gate -p test_*.py"""
import copy
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import analyze  # noqa: E402
import attribute  # noqa: E402
import present  # noqa: E402


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


synthetic = _load('synthetic', HERE / 'fixtures' / 'synthetic.py')
st = _load('synthetic_trace', HERE / 'fixtures' / 'present' / 'synthetic_trace.py')
PRESENT_DIR = HERE / 'fixtures' / 'present'
RLO_LINES = (PRESENT_DIR / 'rlo-frame-lines.txt').read_text(encoding='utf-8').splitlines()
CONTRACT = analyze.load_contract()
FREQ = st.QPC_FREQUENCY
SUMMARY = ('[Mod:PersistentCity] Population: 957 citizens; 40 travelling, 0 awaiting a route, 0 awaiting opening; clock 1065.0, lag 0.0 game minutes. '
           'Advancement this observation: 3600 frames, 900 events, 400.0 ms total, {max} ms maximum advance, 12 maximum events in one frame; '
           'Slowest advance: unit {unit} ms (citizen), optional-stop reads 0.9 ms, 1 garbage collections completed during it.')


def trace_of(rows, **kw):
    return present.parse_rows(rows, qpc_frequency=FREQ, **kw)


def joined_session(frames=600, offset_ms=0.0, missing=(), dropped=(), version=2, **session_kw):
    rows, presents = st.session(frames, **session_kw)
    trace = trace_of(st.trace_rows(presents, version, offset_ms, missing=missing, dropped=dropped, seconds_origin_ms=1234.5))
    probe = attribute.frame_rows(dict(frames=rows, qpc_frequency=FREQ))
    return rows, presents, trace, probe


def window_with(wid, rows, build='candidate', **kw):
    w = synthetic.window(wid, build=build, **kw)
    w['frames'] = rows
    w['qpc_frequency'] = FREQ
    return w


def stall(interval, frames=None, presents=(), baseline=16.0, rlo=(), reference=None):
    return dict(interval_ms=interval, frames=frames if frames is not None else [frame(1)], presents=list(presents), baseline_ms=baseline,
                rlo=list(rlo), reference=reference)


def frame(number, mod=0.2, gc=0, components=None, saves=0, scene=False, loading=False, whole=100.0):
    return dict(frame=number, qpc_ms=0.0, whole=whole, mod=mod, gc=gc, components=components if components is not None else {},
                saves=saves, scene=scene, loading=loading, clean=True, minute=1065.0)


def ctx(**kw):
    c = dict(advance={}, owners={}, calls={}, bound=True, seconds=300.0, own_stalls=[100.0])
    c.update(kw)
    return c


class TraceParsingTests(unittest.TestCase):
    def test_presentmon_2_columns_give_gpu_busy_and_cpu_wait(self):
        trace = present.read_trace(PRESENT_DIR / 'presentmon2-sample.csv', FREQ)
        self.assertEqual(trace.clock, 'qpc')
        self.assertTrue(trace.has('gpu_busy') and trace.has('cpu_wait') and trace.has('display'))
        self.assertEqual(len(trace.presents), 12)
        p = trace.presents[3]
        self.assertIsNotNone(p.gpu_busy_ms)
        self.assertEqual(present.bound_of(p, 16.7), 'cpu')
        self.assertEqual(present.bound_of(present.Present(0.0, gpu_busy_ms=15.0), 16.7), 'gpu')

    def test_presentmon_2_display_stall_survives_ingestion(self):
        # The sample is upstream 2.x output (CPUStartQPC, FrameTime, DisplayedTime); frame 6 took 100 ms.
        trace = present.read_trace(PRESENT_DIR / 'presentmon2-sample.csv', FREQ)
        self.assertEqual((trace.schema, trace.display_source), ('2.x', 'DisplayedTime'))
        intervals = [present.displayed_interval(trace, p) for p in range(len(trace.presents))]
        self.assertIsNone(intervals[0])
        self.assertAlmostEqual(intervals[6], 100.0, delta=1.0)
        self.assertTrue(all(abs(v - 16.667) < 1.5 for i, v in enumerate(intervals) if i not in (0, 6)), intervals)

    def test_presentmon_2_displayed_time_is_the_interval_after_its_frame(self):
        # CPU starts every 16 ms but frame 1 stayed on screen 100 ms: the interval ending at frame 2's display is 100 ms.
        header = ['Application', 'ProcessID', 'CPUStartTime', 'FrameTime', 'CPUBusy', 'CPUWait', 'GPUBusy', 'DisplayedTime']
        body = [['g', '1', str(16.0 * i), '16.0', '10.0', '6.0', '5.0', '100.0' if i == 1 else '16.0'] for i in range(5)]
        trace = present.parse_rows([header] + body)
        self.assertEqual(trace.clock, 'trace-start')
        self.assertEqual([p.t_ms for p in trace.presents], [16.0, 32.0, 48.0, 64.0, 80.0])  # ms, closing at CPUStart + FrameTime
        self.assertEqual([present.displayed_interval(trace, p) for p in range(5)], [None, 16.0, 100.0, 16.0, 16.0])
        self.assertEqual((trace.presents[2].cpu_busy_ms, trace.presents[2].cpu_wait_ms, trace.presents[2].gpu_busy_ms), (10.0, 6.0, 5.0))
        # DisplayedTime NA: not shown, and the next shown frame's interval runs from the last shown frame.
        body[2][-1] = 'NA'
        trace = present.parse_rows([header] + body)
        self.assertFalse(trace.presents[2].displayed)
        self.assertIsNone(present.displayed_interval(trace, 2))
        self.assertEqual(present.displayed_interval(trace, 3), 100.0)

    def test_trace_without_display_timing_gives_no_presented_series(self):
        header = ['Application', 'ProcessID', 'CPUStartTime', 'FrameTime', 'CPUBusy']
        trace = present.parse_rows([header] + [['g', '1', str(16.0 * i), '16.0', '10.0'] for i in range(5)])
        self.assertIsNone(trace.display_source)
        self.assertTrue(all(present.displayed_interval(trace, p) is None for p in range(5)))

    def test_cpu_start_rows_own_their_frame_evidence(self):
        # Varying frame lengths and a distinct GPU busy per frame: frame i must join row i, not row i + 1.
        stalls = {i: 20.0 + (i * 7) % 40 for i in range(0, 300, 3)}
        gpu = {i: 1.0 + i * 0.01 for i in range(300)}
        rows, presents = st.session(300, stalls=stalls, gpu_busy=gpu)
        trace = trace_of(st.trace_rows(presents))
        probe = attribute.frame_rows(dict(frames=rows, qpc_frequency=FREQ))
        frames = present.Frames([f['qpc_ms'] for f in probe], [f['whole'] for f in probe])
        offset, _, _ = present.estimate_offset(frames, trace, hint_ms=0.0)
        joined = present.join(frames, trace, offset)
        self.assertEqual(joined, list(range(300)))
        for i in range(300):
            self.assertAlmostEqual(trace.presents[joined[i]].gpu_busy_ms, gpu[i], places=3)

    def test_presentmon_1_without_gpu_columns_is_unknown(self):
        trace = present.read_trace(PRESENT_DIR / 'presentmon1-sample.csv')
        self.assertEqual(trace.clock, 'trace-start')
        self.assertFalse(trace.has('gpu_busy'))
        self.assertTrue(all(present.bound_of(p, 16.7) == present.UNKNOWN for p in trace.presents))
        self.assertFalse(trace.presents[5].displayed)  # Dropped=1
        self.assertIsNone(present.displayed_interval(trace, 5))
        self.assertIsNone(trace.presents[0].display_ms)  # PresentMon 1.x writes 0 for the first displayed frame

    def test_qpc_trace_needs_a_frequency_and_a_time_column(self):
        rows = st.trace_rows(st.session(5)[1])
        with self.assertRaises(present.TraceError):
            present.parse_rows(rows)
        with self.assertRaises(present.TraceError):
            present.parse_rows([['Application', 'MsBetweenPresents'], ['x', '16']])

    def test_process_filter_drops_other_processes(self):
        _, presents = st.session(20)
        rows = st.trace_rows(presents, extra_process=True)
        self.assertEqual(len(trace_of(rows).presents), 40)
        self.assertEqual(len(trace_of(rows, process_id=4242).presents), 20)


class JoinTests(unittest.TestCase):
    STALLS = {50: 120.0, 190: 75.0, 333: 260.0, 470: 90.0, 555: 55.0}

    def test_same_qpc_clock_joins_every_frame(self):
        rows, presents, trace, probe = joined_session(stalls=self.STALLS)
        frames = present.Frames([f['qpc_ms'] for f in probe], [f['whole'] for f in probe])
        offset, source, _ = present.estimate_offset(frames, trace, hint_ms=0.0)
        self.assertAlmostEqual(offset, 0.0, delta=0.5)
        joined = present.join(frames, trace, offset)
        self.assertEqual(joined, list(range(len(rows))))

    def test_join_tolerates_clock_offset(self):
        for offset_true, version in ((37.0, 2), (-412.3, 2), (0.0, 1)):
            with self.subTest(offset=offset_true, version=version):
                rows, presents, trace, probe = joined_session(offset_ms=offset_true, version=version, stalls=self.STALLS)
                frames = present.Frames([f['qpc_ms'] for f in probe], [f['whole'] for f in probe])
                hint = 0.0 if trace.clock == 'qpc' else None
                offset, source, hits = present.estimate_offset(frames, trace, hint_ms=hint)
                self.assertEqual(source, 'estimated')
                if version == 2:
                    self.assertAlmostEqual(offset, -offset_true, delta=1.0)
                else:  # seconds from an origin 1234.5 ms on the probe clock
                    self.assertAlmostEqual(offset, 1234.5, delta=1.0)
                joined = present.join(frames, trace, offset)
                self.assertEqual(joined, list(range(len(rows))), 'each frame joins its own present')

    def test_join_tolerates_missing_and_dropped_presents(self):
        missing = {10, 11, 50, 200, 201, 202, 400}
        dropped = {30, 31, 334, 450}
        rows, presents, trace, probe = joined_session(offset_ms=21.0, missing=missing, dropped=dropped, stalls=self.STALLS)
        frames = present.Frames([f['qpc_ms'] for f in probe], [f['whole'] for f in probe])
        offset, _, _ = present.estimate_offset(frames, trace, hint_ms=0.0)
        joined = present.join(frames, trace, offset)
        kept = [i for i in range(len(rows)) if i not in missing]
        for i in range(len(rows)):
            if i in missing:
                self.assertIsNone(joined[i])
            else:
                self.assertEqual(joined[i], kept.index(i), f'frame {i}')
        # A dropped present stays joined but has no presented interval; the next shown one spans the gap.
        series = attribute.presented_series(probe, joined, trace)
        self.assertEqual(len(series), len(rows) - len(missing) - len(dropped) - 1)  # the first shown present has no interval
        after_drop = next(s for s in series if any(f['frame'] == 1031 for f in s['frames']))
        self.assertEqual([f['frame'] for f in after_drop['frames']], [1030, 1031, 1032])
        self.assertAlmostEqual(after_drop['interval_ms'], sum(r['whole_frame_ms'] for r in rows[30:33]), delta=0.5)

    def test_window_block_reports_join_fraction_and_unknown_gpu(self):
        missing = set(range(0, 600, 50))  # 12 of 600 = 2 %
        rows, presents, trace, probe = joined_session(missing=missing, version=2, stalls=self.STALLS)
        _, block, _ = attribute.join_window(probe, trace, 0.0, 'hint')
        self.assertEqual(block['joined'], 588)
        self.assertFalse(block['established'], 'G3 needs >= 99 % of probe frames joined')
        _, presents_ok = st.session(600, stalls=self.STALLS)
        trace_ok = trace_of(st.trace_rows(presents_ok, 2, gpu=False))
        _, block, _ = attribute.join_window(probe, trace_ok, 0.0, 'hint')
        self.assertTrue(block['established'])
        self.assertEqual((block['gpu_busy'], block['cpu_wait']), ('unknown', 'reported'))  # --no_track_gpu keeps CPUBusy/CPUWait
        self.assertEqual(block['bound']['unknown'], block['presented_intervals'])

    def test_flat_cadence_keeps_the_supplied_offset(self):
        # 18,000 frames of exactly 16 ms on the same QPC clock: every whole-frame shift agrees equally well.
        starts = [5000.0 + 16.0 * i for i in range(18000)]
        frames = present.Frames(starts, [16.0] * 18000)
        trace = present.Trace([present.Present(s + 16.0) for s in starts], 'qpc', {}, display_source='DisplayedTime')
        offset, _, _ = present.estimate_offset(frames, trace, hint_ms=0.0)
        self.assertAlmostEqual(offset, 0.0, delta=0.5)
        self.assertEqual(present.join(frames, trace, offset), list(range(18000)))

    def test_flat_prefix_keeps_the_anchored_offset(self):
        # Three distinctive long frames after frame 10,000 fix the offset; the flat prefix must not move it.
        wholes = [16.0] * 18000
        for i in (10500, 12000, 15000):
            wholes[i] = 120.0 + i / 1000.0
        starts, t = [], 5000.0
        for w in wholes:
            starts.append(t)
            t += w
        frames = present.Frames(starts, wholes)
        trace = present.Trace([present.Present(s + w + 30.0) for s, w in zip(starts, wholes)], 'qpc', {}, display_source='DisplayedTime')
        offset, source, _ = present.estimate_offset(frames, trace, hint_ms=0.0)
        self.assertEqual(source, 'estimated')
        self.assertAlmostEqual(offset, -30.0, delta=0.5)

    def test_near_flat_prefix_keeps_anchor_ownership(self):
        # Alternating 15/17 ms frames with a +/-1 ms present phase over the first 10,000 frames favour another phase
        # there; the three long frames after it still fix the offset, and each frame must join its own present.
        n = 18000
        wholes = [15.0 if i % 2 else 17.0 for i in range(n)]
        for i in (10500, 12000, 15000):
            wholes[i] = 120.0 + i / 1000.0
        starts, t = [], 5000.0
        for w in wholes:
            starts.append(t)
            t += w
        ends = [s + w for s, w in zip(starts, wholes)]
        times = [e + 30.0 + ((1.0 if i % 2 else -1.0) if i < 10000 else 0.0) for i, e in enumerate(ends)]
        times.append(times[-1] + 16.0)  # recording runs one frame past the window
        frames = present.Frames(starts, wholes)
        trace = present.Trace([present.Present(x) for x in times], 'qpc', {}, display_source='DisplayedTime')
        offset, source, _ = present.estimate_offset(frames, trace, hint_ms=0.0)
        self.assertEqual(source, 'estimated')
        self.assertAlmostEqual(offset, -30.0, delta=1.5)
        joined = present.join(frames, trace, offset)
        self.assertEqual(joined, list(range(n)))

    def test_unrelated_trace_without_hint_is_not_joined(self):
        _, _, _, probe = joined_session(stalls=self.STALLS)
        _, other = st.session(600, seed=99)
        trace = present.parse_rows(st.trace_rows(other, 1, seconds_origin_ms=0.0))
        frames = present.Frames([f['qpc_ms'] for f in probe], [f['whole'] for f in probe])
        with self.assertRaises(present.TraceError):
            present.estimate_offset(frames, trace)


class ClassifierTests(unittest.TestCase):
    """Each class fires only on its own evidence; a frame with none is unattributed."""

    def classes(self, s, c=None):
        return attribute.classify(s, c or ctx())['classes']

    def test_a_frame_with_no_evidence_is_unattributed(self):
        s = attribute.classify(stall(100.0), ctx())
        self.assertEqual(s['classes'], ['unattributed'])
        self.assertEqual(s['evidence'], {})

    def test_each_class_fires_only_on_its_evidence(self):
        quiet = present.Present(0.0, gpu_busy_ms=5.0, cpu_busy_ms=10.0, cpu_wait_ms=0.5)
        rlo_rest = dict(frame=1, frame_ms=100.0, phases={'the rest': 95.0, "outside the engine's steps": 5.0}, saving=False, scene_change=False)
        base = dict(frames=[frame(1)], presents=[quiet], rlo=[rlo_rest], reference=dict(id='r', stalls=[], seconds=300.0))
        cases = {
            'pc-union': dict(frames=[frame(1, mod=60.0, components={'PhysicalPopulation.Update': 60.0})]),
            'gc': dict(frames=[frame(1, gc=1)]),
            'save-load-scene': dict(frames=[frame(1, saves=1)]),
            'native-frame': dict(rlo=[dict(rlo_rest, phases={"scripts' LateUpdate": 90.0, 'the rest': 10.0})]),
            'gpu-present': dict(presents=[present.Present(0.0, gpu_busy_ms=90.0, cpu_busy_ms=10.0, cpu_wait_ms=0.5)]),
            'reference-rate': dict(reference=dict(id='r', stalls=[95.0], seconds=300.0)),
        }
        self.assertEqual(self.classes(stall(100.0, **base)), ['unattributed'])
        for expected, change in cases.items():
            with self.subTest(expected):
                args = dict(base, **change)
                self.assertEqual(self.classes(stall(100.0, **args)), [expected])

    def test_pc_union_needs_half_of_the_excess_over_the_reference_p50(self):
        # interval 100, baseline 16: excess 84, needs 42 ms of union.
        self.assertIn('pc-union', self.classes(stall(100.0, [frame(1, mod=42.0)])))
        self.assertNotIn('pc-union', self.classes(stall(100.0, [frame(1, mod=41.9)])))
        s = attribute.classify(stall(100.0, [frame(1, mod=50.0, components={'AdvancePopulation': 45.0, 'OnGUI': 4.0})]), ctx())
        self.assertEqual(s['evidence']['pc-union']['component'], 'AdvancePopulation')

    def test_whole_minus_union_is_never_pc_union(self):
        # 300 ms frame, 20 ms union: the 280 ms remainder is not PC's, whatever else is known.
        s = attribute.classify(stall(300.0, [frame(1, mod=20.0)]), ctx())
        self.assertEqual(s['classes'], ['unattributed'])
        self.assertEqual(s['not_pc_ms'], 280.0)
        self.assertEqual(s['mod_work_ms'], 20.0)
        # A script phase that contains PC's Update is native only after the union is taken out.
        rlo = dict(frame=1, frame_ms=140.0, phases={"scripts' Update": 131.0, 'physics': 3.0}, saving=False, scene_change=False)
        s = attribute.classify(stall(140.0, [frame(1, mod=120.0)], rlo=[rlo]), ctx())
        self.assertEqual(s['classes'], ['pc-union'])
        s = attribute.classify(stall(140.0, [frame(1, mod=10.0)], rlo=[rlo]), ctx())
        self.assertEqual(s['classes'], ['native-frame'])
        self.assertEqual(s['evidence']['native-frame'][0]['outside_pc_ms'], 121.0)
        # Trace CPU busy minus the union, never the union itself.
        busy = present.Present(0.0, cpu_busy_ms=130.0)
        s = attribute.classify(stall(140.0, [frame(1, mod=100.0)], presents=[busy]), ctx())
        self.assertEqual(s['classes'], ['pc-union'])

    def test_gc_needs_a_probe_count_rise(self):
        self.assertEqual(self.classes(stall(100.0, [frame(1, gc=0)])), ['unattributed'])
        s = attribute.classify(stall(100.0, [frame(1, gc=None)]), ctx())
        self.assertEqual((s['classes'], s['gc']), (['unattributed'], 'unknown'))
        # RLO's collector line is context, not the probe evidence the class needs.
        rlo = attribute.parse_rlo_line(RLO_LINES[2])
        rlo.update(frame=1, saving=False, scene_change=False, phases={})
        self.assertEqual(self.classes(stall(100.0, rlo=[rlo])), ['unattributed'])

    def test_save_load_scene_from_probe_flags_or_rlo(self):
        for f in (frame(1, saves=2), frame(1, scene=True), frame(1, loading=True)):
            self.assertEqual(self.classes(stall(100.0, [f])), ['save-load-scene'])
        rlo = attribute.parse_rlo_line(RLO_LINES[2])
        rlo['frame'] = 1
        s = attribute.classify(stall(512.0, [frame(1, whole=512.0)], rlo=[rlo]), ctx())
        self.assertEqual(s['classes'], ['save-load-scene', 'native-frame'])  # 470 ms loading phase is native work

    def test_present_wait_and_unnamed_phases_are_not_native_frame(self):
        rlo = attribute.parse_rlo_line(RLO_LINES[3])
        rlo['frame'] = 1
        self.assertEqual(self.classes(stall(95.0, rlo=[rlo])), ['unattributed'])
        wait = present.Present(0.0, cpu_wait_ms=70.0)
        self.assertEqual(self.classes(stall(95.0, presents=[wait], rlo=[rlo])), ['gpu-present'])

    def test_reference_rate_needs_same_size_at_similar_rate(self):
        c = ctx(own_stalls=[100.0, 110.0, 95.0, 105.0])  # 4 per 300 s
        same = dict(id='r', stalls=[100.0, 90.0], seconds=300.0)  # 2 per 300 s = half the rate
        rare = dict(id='r', stalls=[100.0], seconds=300.0)
        other_size = dict(id='r', stalls=[400.0, 30.0, 500.0], seconds=300.0)
        self.assertEqual(self.classes(stall(100.0, reference=same), c), ['reference-rate'])
        self.assertEqual(self.classes(stall(100.0, reference=rare), c), ['unattributed'])
        self.assertEqual(self.classes(stall(100.0, reference=other_size), c), ['unattributed'])
        self.assertEqual(self.classes(stall(100.0, reference=None), c), ['unattributed'])

    def test_several_classes_can_apply(self):
        s = attribute.classify(stall(200.0, [frame(1, mod=150.0, gc=1)], presents=[present.Present(0.0, gpu_busy_ms=180.0)]), ctx())
        self.assertEqual(s['classes'], ['pc-union', 'gc', 'gpu-present'])


class RloLineTests(unittest.TestCase):
    def test_parses_the_9_november_excerpt(self):
        r = attribute.parse_rlo_line(RLO_LINES[0])
        self.assertEqual(r['frame_ms'], 140.0)
        self.assertEqual(r['phases'], {"scripts' Update": 131.0, 'physics': 3.0, 'the rest': 1.0, "outside the engine's steps": 0.0})
        self.assertEqual((r['saving'], r['scene_change'], r['collection'], r['main_thread_ms']), (False, False, False, 141.0))
        self.assertIsNone(attribute.parse_rlo_line(RLO_LINES[4]))
        r = attribute.parse_rlo_line(RLO_LINES[1])
        self.assertEqual(r['phases']['the rest'], 13.0)
        self.assertEqual(r['phases']['UpdateCanvasRectTransform'], 30.0)

    def test_line_attaches_only_to_a_unique_frame_of_matching_duration(self):
        frames = {'w': [frame(1, whole=16.0), frame(2, whole=236.4), frame(3, whole=140.2), frame(4, whole=139.6)]}
        attached, unmatched = attribute.attach_rlo([dict(line=RLO_LINES[1], window='w'), dict(line=RLO_LINES[0], window='w')], frames)
        self.assertEqual(list(attached), [('w', 2)])
        self.assertEqual(unmatched[0]['candidates'], 2)  # 140 ms matches frames 3 and 4: attached to neither
        attached, _ = attribute.attach_rlo([dict(line=RLO_LINES[0], first_frame=4, last_frame=4)], frames)
        self.assertEqual(list(attached), [('w', 4)])
        attached, _ = attribute.attach_rlo([dict(line=RLO_LINES[0], window='w', frame=3)], frames)
        self.assertEqual(list(attached), [('w', 3)])

    def test_unscoped_player_log_line_is_context_only(self):
        # A bare Player.log line may describe a frame outside every window: it never attaches, even to a unique match.
        frames = {'w': [frame(1, whole=16.0), frame(2, whole=236.4)]}
        attached, unmatched = attribute.attach_rlo([dict(line=RLO_LINES[1])], frames)
        self.assertEqual(attached, {})
        self.assertEqual(unmatched[0]['reason'], 'no window or frame range: context only')

    def test_diagnostic_frames_are_rival_candidates(self):
        warm = dict(frame(3, whole=236.0), clean=False)
        frames = {'w': [frame(1, whole=16.0), frame(2, whole=236.4), warm]}
        attached, unmatched = attribute.attach_rlo([dict(line=RLO_LINES[1], window='w')], frames)
        self.assertEqual(attached, {})
        self.assertEqual(unmatched[0]['candidates'], 2)


class Fix128Tests(unittest.TestCase):
    """Observation-level fix-128 fields go to the one matching frame only; non-advancement frames get none."""

    def export(self, summary_max=120.0, second=80.0):
        mod = {100: 121.0, 300: second + 1.0, 400: 70.0}
        rows, presents = st.session(600, stalls={100: 140.0, 300: second + 20.0, 400: 90.0}, mod=mod)
        rows[100]['components'] = {'AdvancePopulation': 120.0}
        rows[300]['components'] = {'AdvancePopulation': second}
        rows[400]['components'] = {'PhysicalPopulation.Update': 70.0}  # outside advancement, scope bound
        w = window_with('c', rows, summary_lines=[SUMMARY.format(max=summary_max, unit=119.5)])
        return synthetic.export([w]), trace_of(st.trace_rows(presents))

    def by_frame(self, report, kind):
        return {s.get('frame', s['frame_numbers'][-1]): s for s in report['stalls'] if s['kind'] == kind}

    def test_fields_are_never_copied_to_a_second_spike(self):
        export, trace = self.export()
        _, report = attribute.attribute(export, {'*': trace})
        m2 = {s['frame']: s for s in report['m2']}
        self.assertEqual(sorted(m2), [1100, 1300, 1400])
        first, second = m2[1100]['advancement']['unit'], m2[1300]['advancement']['unit']
        self.assertEqual((first['unit_kind'], first['unit_ms'], first['source']), ('citizen', 119.5, 'summary-maximum'))
        self.assertEqual(second, dict(unit_kind='unknown', unit_ms='unknown', optional_stop_ms='unknown', gc_count='unknown', source='unknown'))
        for kind in ('whole-frame', 'presented'):
            stalls = self.by_frame(report, kind)
            self.assertEqual(stalls[1100]['advancement'][0]['unit']['source'], 'summary-maximum')
            self.assertEqual(stalls[1300]['advancement'][0]['unit']['source'], 'unknown')
            self.assertIn('pc-union', stalls[1100]['classes'])

    def test_ambiguous_maximum_attaches_nowhere(self):
        export, trace = self.export(second=120.0)
        _, report = attribute.attribute(export, {'*': trace})
        self.assertTrue(all(s['advancement']['unit']['source'] == 'unknown' for s in report['m2'] if 'advancement' in s))

    def test_a_non_advancement_spike_has_no_advancement_fields(self):
        export, trace = self.export()
        _, report = attribute.attribute(export, {'*': trace})
        spike = next(s for s in report['m2'] if s['frame'] == 1400)
        self.assertNotIn('advancement', spike)
        self.assertEqual(spike['component'], 'PhysicalPopulation.Update')
        for kind in ('whole-frame', 'presented'):
            self.assertNotIn('advancement', self.by_frame(report, kind)[1400])

    def test_unbound_advancement_is_unknown_not_absent(self):
        export, trace = self.export()
        for r in export['windows'][0]['frames']:
            r['components'] = {k: v for k, v in r['components'].items() if k != 'AdvancePopulation'}
        export['windows'][0]['probe'] = None
        _, report = attribute.attribute(export, {'*': trace})
        spike = next(s for s in report['m2'] if s['frame'] == 1400)
        self.assertEqual(spike['advancement']['advance_ms'], 'unknown')


class EndToEndTests(unittest.TestCase):
    def pair(self, cand_stalls, ref_stalls, cand_mod=None):
        crow, cpres = st.session(18000, stalls=cand_stalls, mod=cand_mod or {})
        rrow, rpres = st.session(18000, seed=5, stalls=ref_stalls, start_ms=2_000_000.0)
        for r in rrow:
            r['mod_work_ms'], r['components'] = 0.0, {}
        c, r = window_with('c', crow), window_with('r', rrow, build='reference')
        export = synthetic.export([c, r])
        traces = {'c': trace_of(st.trace_rows(cpres, offset_ms=13.0)), 'r': trace_of(st.trace_rows(rpres, offset_ms=-8.0))}
        return export, traces

    def test_enriched_export_feeds_p1_to_p4(self):
        export, traces = self.pair({100: 300.0, 5000: 60.0}, {7000: 62.0}, cand_mod={100: 280.0})
        enriched, report = attribute.attribute(export, traces)
        c = enriched['windows'][0]
        self.assertTrue(c['presentation']['established'])
        self.assertGreaterEqual(c['presentation']['joined_fraction'], 0.99)
        self.assertEqual(c['receipts']['presentation_trace'], 'pass')
        self.assertEqual(len(c['presented_ms']), 18000 - 1)
        result = analyze.analyze([enriched], CONTRACT)
        w = next(x for x in result['windows'] if x['id'] == 'c')
        # P1/P2 are now established from the joined series (synthetic frames are 16.67 +/- 0.6 ms, so P1 may fail).
        ordered = sorted(c['presented_ms'])
        self.assertEqual(w['rows']['P1']['value'], analyze.nearest_rank(ordered, 95))
        self.assertEqual(w['rows']['P2']['value'], analyze.nearest_rank(ordered, 99))
        for rid in ('P1', 'P2'):  # the reference's synthetic frames miss P1 too, so P1 may carry the native-baseline label
            row = w['rows'][rid]
            self.assertTrue(row['verdict'] in ('pass', 'fail') or row.get('native_baseline'), row)
        # The 300 ms stall is >= 250 ms and classed pc-union (280 ms union): P3 fails on it.
        self.assertEqual(w['rows']['P3']['verdict'], 'fail')
        self.assertEqual(w['rows']['P3']['reason'], 'severe stall classed pc-union')
        self.assertIn(result['pairs'][0]['verdict'], ('pass', 'fail'))
        self.assertIsNotNone(result['pairs'][0]['value'])
        # Whole-frame stalls in the analyzer's table carry F4c's classes (hook in analyze.stalls_of).
        whole = [s for s in result['stalls'] if s['kind'] == 'whole-frame' and s['window'] == 'c']
        self.assertTrue(whole and all('unclassified (F4c)' not in s['classes'] for s in whole))
        presented = [s for s in result['stalls'] if s['kind'] == 'presented' and s['window'] == 'c']
        self.assertTrue(presented and all('unclassified (F4c)' not in s['classes'] for s in presented))
        # The 60 ms candidate stall has a same-size reference stall at the same rate.
        sixty = next(s for s in report['stalls'] if s['window'] == 'c' and s['kind'] == 'whole-frame' and s['frame'] == 6000)
        self.assertEqual(sixty['classes'], ['reference-rate'])
        self.assertTrue(sixty['baseline_source'].startswith('reference r'))

    def test_severe_unattributed_stall_leaves_p3_open_evidence(self):
        export, traces = self.pair({100: 300.0}, {})
        enriched, report = attribute.attribute(export, traces)
        stall = next(s for s in report['stalls'] if s['window'] == 'c' and s['kind'] == 'presented')
        self.assertEqual(stall['classes'], ['unattributed'])
        self.assertIn(1100, [u['frame'] for u in report['unattributed']])
        w = next(x for x in analyze.analyze([enriched], CONTRACT)['windows'] if x['id'] == 'c')
        self.assertEqual(w['rows']['P3']['verdict'], 'pass')  # one stall per 5 min, not pc-union

    def test_input_export_is_not_changed(self):
        export, traces = self.pair({100: 300.0}, {})
        before = json.dumps(export, sort_keys=True)
        attribute.attribute(export, traces)
        self.assertEqual(json.dumps(export, sort_keys=True), before)

    def test_poor_join_fails_the_presentation_receipt(self):
        crow, cpres = st.session(18000)
        w = window_with('c', crow)
        trace = trace_of(st.trace_rows(cpres, missing=set(range(0, 18000, 50))))
        enriched, _ = attribute.attribute(synthetic.export([w]), {'*': trace})
        self.assertEqual(enriched['windows'][0]['receipts']['presentation_trace'], 'fail')
        self.assertFalse(analyze.analyze([enriched], CONTRACT)['windows'][0]['valid'])

    def test_window_without_qpc_is_not_joined(self):
        w = synthetic.window('c')
        enriched, report = attribute.attribute(synthetic.export([w]), {'*': trace_of(st.trace_rows(st.session(100)[1]))})
        self.assertFalse(report['presentation']['c']['established'])
        self.assertEqual(enriched['windows'][0]['presented_ms'], w['presented_ms'])  # untouched
        # The supplied trace could not be joined: the receipt fails, so the existing series cannot pass P1.
        self.assertEqual(enriched['windows'][0]['receipts']['presentation_trace'], 'fail')
        result = analyze.analyze([enriched], CONTRACT)['windows'][0]
        self.assertFalse(result['valid'])
        self.assertIn('receipt failed: presentation_trace', result['invalid_reasons'])

    def test_offset_failure_fails_the_presentation_receipt(self):
        crow, _ = st.session(18000)
        w = window_with('c', crow)
        _, other = st.session(600, seed=99)
        trace = present.parse_rows(st.trace_rows(other, 1, seconds_origin_ms=0.0))  # unrelated, no shared clock
        enriched, report = attribute.attribute(synthetic.export([w]), {'*': trace})
        self.assertFalse(report['presentation']['c']['established'])
        self.assertIn('clock offset not established', report['presentation']['c']['reason'])
        self.assertEqual(enriched['windows'][0]['receipts']['presentation_trace'], 'fail')
        self.assertFalse(analyze.analyze([enriched], CONTRACT)['windows'][0]['valid'])

    def test_unmatched_reference_gives_no_reference_rate_or_p50(self):
        for change in (dict(checkpoint_minute=1200), dict(game_span_minutes=180.0), dict(reference_pace_ratio=3.0),
                       dict(clean_real_seconds=100.0), dict(aborted=True)):
            with self.subTest(change):
                export, traces = self.pair({100: 300.0, 5000: 60.0}, {7000: 62.0}, cand_mod={100: 280.0})
                export['windows'][1].update(change)
                _, report = attribute.attribute(export, traces)
                sixty = next(s for s in report['stalls'] if s['window'] == 'c' and s['kind'] == 'whole-frame' and s['frame'] == 6000)
                self.assertNotIn('reference-rate', sixty['classes'])
                self.assertIsNone(sixty['baseline_ms'])
                self.assertTrue(sixty['baseline_source'].startswith('none'))
                # A union of more than half the whole interval is pc-union against any p50.
                big = next(s for s in report['stalls'] if s['window'] == 'c' and s['kind'] == 'whole-frame' and s['frame'] == 1100)
                self.assertIn('pc-union', big['classes'])

    def test_union_that_needs_the_reference_p50_is_unresolved_without_one(self):
        # 300 ms frame, 145 ms union: 50 % of 300 - 16.7 but not 50 % of 300.
        export, traces = self.pair({100: 300.0}, {}, cand_mod={100: 145.0})
        export['windows'][1]['checkpoint_minute'] = 1200
        _, report = attribute.attribute(export, traces)
        s = next(s for s in report['stalls'] if s['window'] == 'c' and s['kind'] == 'whole-frame' and s['frame'] == 1100)
        self.assertEqual(s['classes'], ['unattributed'])
        self.assertTrue(s['pc_union'].startswith('unresolved'))
        export, traces = self.pair({100: 300.0}, {}, cand_mod={100: 145.0})
        _, report = attribute.attribute(export, traces)
        s = next(s for s in report['stalls'] if s['window'] == 'c' and s['kind'] == 'whole-frame' and s['frame'] == 1100)
        self.assertIn('pc-union', s['classes'])  # against the matched reference p50

    def test_whole_frame_stall_uses_the_joined_present(self):
        rows, presents = st.session(600, stalls={100: 300.0}, gpu_busy={100: 280.0})
        w = window_with('c', rows)
        _, report = attribute.attribute(synthetic.export([w]), {'*': trace_of(st.trace_rows(presents))})
        whole = next(s for s in report['stalls'] if s['kind'] == 'whole-frame' and s['frame'] == 1100)
        shown = next(s for s in report['stalls'] if s['kind'] == 'presented' and 1100 in s['frame_numbers'])
        self.assertIn('gpu-present', shown['classes'])
        self.assertEqual(whole['classes'], shown['classes'])
        self.assertEqual((whole['gpu_busy_ms'], whole['bound']), (280.0, 'gpu'))

    def test_initial_diagnostic_frame_does_not_hide_the_next_stall(self):
        # The first shown present has no interval; it still closes its frame, so frame 1's 300 ms stall stays in.
        for flag in ('diagnostic', 'warmup'):
            with self.subTest(flag):
                rows, presents = st.session(600, stalls={1: 300.0, 200: 120.0, 333: 260.0, 470: 90.0}, mod={1: 280.0})
                rows[0][flag] = True
                w = window_with('c', rows)
                enriched, report = attribute.attribute(synthetic.export([w]), {'*': trace_of(st.trace_rows(presents))})
                self.assertTrue(report['presentation']['c']['established'])
                stall = next(s for s in report['stalls'] if s['kind'] == 'presented' and s['frame_numbers'] == [1001])
                self.assertIn('pc-union', stall['classes'])
                self.assertEqual(len(enriched['windows'][0]['presented_ms']), 599)

    def test_whole_frame_cpu_evidence_without_display_timing(self):
        rows, presents = st.session(600, stalls={100: 300.0, 200: 120.0, 333: 260.0}, cpu_busy={100: 280.0})
        header = [h for h in st.V2_HEADER if h not in ('DisplayLatency', 'DisplayedTime')]
        full = st.trace_rows(presents)
        keep = [full[0].index(h) for h in header]
        trace = trace_of([[r[k] for k in keep] for r in full])
        enriched, report = attribute.attribute(synthetic.export([window_with('c', rows)]), {'*': trace})
        block = report['presentation']['c']
        self.assertTrue(block['aligned'])
        self.assertFalse(block['established'])
        self.assertEqual(enriched['windows'][0]['receipts']['presentation_trace'], 'fail')
        whole = next(s for s in report['stalls'] if s['kind'] == 'whole-frame' and s['frame'] == 1100)
        self.assertIn('native-frame', whole['classes'])
        self.assertFalse(any(s['kind'] == 'presented' for s in report['stalls']))

    def test_interval_spanning_a_diagnostic_frame_is_left_out(self):
        for flag, version in (('diagnostic', 1), ('warmup', 1), ('diagnostic', 2), ('warmup', 2)):
            with self.subTest(flag=flag, version=version):
                rows, presents = st.session(600, stalls={100: 300.0, 200: 120.0, 333: 260.0, 470: 90.0, 555: 75.0}, mod={100: 280.0})
                rows[100][flag] = True
                w = window_with('c', rows)
                trace = trace_of(st.trace_rows(presents, version, dropped={100}, seconds_origin_ms=0.0))  # its present was not shown
                enriched, report = attribute.attribute(synthetic.export([w]), {'*': trace})
                self.assertTrue(report['presentation']['c']['established'])
                self.assertFalse(any(s['kind'] == 'presented' and 1100 in s['frame_numbers'] for s in report['stalls']))
                self.assertFalse(any(v > 290.0 for v in enriched['windows'][0]['presented_ms']))
                self.assertEqual(len(enriched['windows'][0]['presented_ms']), 600 - 1 - 2)  # first, dropped 100 and the one spanning it

    def test_f4b_probe_rows_with_component_arrays(self):
        rows, presents = st.session(600, stalls={100: 140.0}, mod={100: 120.0})
        for r in rows:
            comps = r.pop('components')
            r['components_ms'] = [comps.get('AdvancePopulation', 0.0), 0.0]
        w = synthetic.window('c')
        del w['frames']
        w['probe'] = dict(status=dict(stopwatch_frequency=FREQ, scopes=dict(components=['AdvancePopulation', 'UpdateInspector'], component_unbound=[])),
                          frames=rows)
        _, report = attribute.attribute(synthetic.export([w]), {'*': trace_of(st.trace_rows(presents))})
        self.assertTrue(report['presentation']['c']['established'])
        m2 = report['m2'][0]
        self.assertEqual((m2['frame'], m2['component']), (1100, 'AdvancePopulation'))

    def test_zero_filled_advancement_is_not_an_advancement_call(self):
        # F4b arrays hold 0 for a bound scope that did not run; a spike only in PhysicalPopulation.Update has no advancement fields.
        rows, presents = st.session(600, stalls={100: 90.0}, mod={100: 70.0})
        for r in rows:
            r.pop('components')
            r['components_ms'] = [0.0, 0.0]
        rows[100]['components_ms'] = [0.0, 70.0]
        w = synthetic.window('c')
        del w['frames']
        w['probe'] = dict(status=dict(stopwatch_frequency=FREQ, scopes=dict(components=['AdvancePopulation', 'PhysicalPopulation.Update'],
                                                                             component_unbound=[])), frames=rows)
        _, report = attribute.attribute(synthetic.export([w]), {'*': trace_of(st.trace_rows(presents))})
        spike = next(s for s in report['m2'] if s['frame'] == 1100)
        self.assertEqual(spike['component'], 'PhysicalPopulation.Update')
        self.assertNotIn('advancement', spike)
        for s in report['stalls']:
            if 1100 in s['frame_numbers']:
                self.assertNotIn('advancement', s)

    def test_cli_writes_export_report_and_markdown(self):
        export, traces = self.pair({100: 300.0}, {})
        rows, presents = st.session(18000, stalls={100: 300.0})
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / 'e.json').write_text(json.dumps(export), encoding='utf-8')
            st.write_csv(st.trace_rows(presents, offset_ms=13.0), tmp / 'c.csv')
            (tmp / 'Player.log').write_text('\n'.join(RLO_LINES), encoding='utf-8')
            out = subprocess.run([sys.executable, str(HERE / 'attribute.py'), str(tmp / 'e.json'), '--trace-for', f"c={tmp / 'c.csv'}",
                                  '--qpc-frequency', str(FREQ), '--player-log', str(tmp / 'Player.log'), '--export-out', str(tmp / 'x.json'),
                                  '--json-out', str(tmp / 'a.json'), '--md-out', str(tmp / 'a.md')], capture_output=True, text=True)
            self.assertEqual(out.returncode, 0, out.stderr)
            md = (tmp / 'a.md').read_text(encoding='utf-8')
            self.assertIn('## Stalls (>= 50 ms)', md)
            self.assertIn('## Unattributed (open evidence)', md)
            enriched = json.loads((tmp / 'x.json').read_text(encoding='utf-8'))
            self.assertTrue(enriched['windows'][0]['presentation']['established'])
            self.assertFalse(enriched['windows'][1]['presentation']['established'])

    def test_ten_minute_join_runs_in_seconds(self):
        rows, presents, missing, dropped = st.long_session(hours=10 / 60)
        w = window_with('c', rows)
        trace = trace_of(st.trace_rows(presents, offset_ms=37.0, missing=missing, dropped=dropped))
        start = time.perf_counter()
        _, report = attribute.attribute(synthetic.export([w]), {'*': trace})
        if os.environ.get('PC_PERF_ASSERTS') == '1':  # wall-clock limits only on request (AGENTS.md)
            self.assertLess(time.perf_counter() - start, 10.0)
        self.assertTrue(report['presentation']['c']['established'])


if __name__ == '__main__':
    unittest.main()
