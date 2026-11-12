"""F4c stall attribution (F4 plan, Decisions 6 and 7) and P1-P4 inputs for the F4a analyzer.

python tools/perf-gate/attribute.py <export.json> [--trace <presentmon.csv>] [--trace-for <window>=<csv>]...
    [--qpc-frequency <ticks/s>] [--process-id <pid>] [--player-log <Player.log>]
    --export-out <enriched.json> --json-out <attribution.json> --md-out <attribution.md>

Joins a presentation trace to each window's probe frames (present.py), writes each window's
presented-interval series (`presented_ms`, P1/P2/P4), the class of every presented stall
(`stall_classes`, P3) and of every whole-frame stall (`whole_frame_stall_classes`) into a copy of the
export that analyze.py reads, and writes the stall table with its evidence. A stall gets every class
whose evidence is present, or `unattributed`. Whole-frame time minus the PC union is never PC's.
"""
import argparse
import bisect
import json
import math
import re
import sys
from pathlib import Path

import analyze
import present

UNKNOWN = present.UNKNOWN
ADVANCE = analyze.ADVANCE_COMPONENT
CLASSES = ('pc-union', 'gc', 'save-load-scene', 'native-frame', 'gpu-present', 'reference-rate', 'unattributed')
EVIDENCE_SHARE = 0.5
SIZE_BAND = 1.5
RATE_SHARE = 0.5
RLO_TOLERANCE_MS, RLO_TOLERANCE_FRACTION = 1.5, 0.02
UNNAMED_PHASES = {'the rest', "outside the engine's steps"}
PRESENT_WAIT_PHASES = {'waiting for the last frame to be shown', 'presentafterdraw'}



RLO_FRAME = re.compile(r"Session: a frame of (\d+(?:[.,]\d+)?) ms;")
PHASE_ITEM = re.compile(r"^(.*?\S) (\d+(?:[.,]\d+)?) ms\b")
MAIN_THREAD = re.compile(r"the main thread ran (\d+(?:[.,]\d+)?) ms")


def _float(text):
    return float(text.replace(',', '.'))


def _split_top(text, sep):
    parts, depth, quoted, start, i = [], 0, False, 0, 0
    while i < len(text):
        c = text[i]
        if c == '"':
            quoted = not quoted
        elif not quoted and c == '(':
            depth += 1
        elif not quoted and c == ')':
            depth = max(0, depth - 1)
        elif not quoted and depth == 0 and text.startswith(sep, i):
            parts.append(text[start:i])
            i += len(sep)
            start = i
            continue
        i += 1
    parts.append(text[start:])
    return parts


def parse_rlo_line(line):
    match = RLO_FRAME.search(line)
    if match is None:
        return None
    out = dict(frame_ms=_float(match.group(1)), phases={}, saving=None, scene_change=None, collection=None, main_thread_ms=None)
    for segment in _split_top(line[match.end():], '; '):
        segment = segment.strip()
        if segment.startswith('where it went: '):
            for item in _split_top(segment[len('where it went: '):], ', '):
                m = PHASE_ITEM.match(item.strip())
                if m:
                    out['phases'][m.group(1)] = _float(m.group(2))
        elif segment.startswith('the processor: '):
            m = MAIN_THREAD.search(segment)
            if m:
                out['main_thread_ms'] = _float(m.group(1))
        elif segment.startswith('the collector: '):
            out['collection'] = 'a collection finished in it' in segment
        elif segment.startswith('the game: '):
            out['saving'] = False if 'not saving' in segment else (True if 'saving' in segment else None)
            out['scene_change'] = False if 'no scene loaded or unloaded' in segment else (True if "the scene '" in segment else None)
    return out



def _frequency(window):
    status = (window.get('probe') or {}).get('status') or {}
    return window.get('qpc_frequency') or status.get('stopwatch_frequency') or status.get('stopwatchFrequency')


def _component_names(window):
    status = (window.get('probe') or {}).get('status') or {}
    scopes = status.get('scopes') if isinstance(status.get('scopes'), dict) else {}
    return window.get('component_names') or scopes.get('components') or []


def frame_rows(window):
    probe = window.get('probe')
    rows = (probe.get('frames') if probe else window.get('frames')) or []
    frequency = _frequency(window)
    names = _component_names(window)
    out = []
    for r in rows:
        comps = r.get('components')
        if comps is None and r.get('components_ms') is not None:
            comps = {n: v for n, v in zip(names, r['components_ms'])}
        qpc_ms = r.get('qpc_ms')
        if qpc_ms is None and r.get('qpc') is not None and frequency:
            qpc_ms = r['qpc'] * 1000.0 / frequency
        out.append(dict(frame=r.get('frame'), qpc_ms=qpc_ms,
                        whole=r['whole_frame_ms'] if 'whole_frame_ms' in r else r['wholeFrameMs'],
                        mod=r['mod_work_ms'] if 'mod_work_ms' in r else r['modWorkMs'],
                        gc=r.get('gc', r.get('gcCollections')), components=comps or {},
                        saves=r.get('saves'), scene=r.get('scene'), loading=r.get('loading'),
                        clean=not r.get('diagnostic') and not r.get('warmup'),
                        minute=r.get('logical_minute', r.get('native_minute'))))
    return out


def advance_calls(window):
    calls = window.get('advance_calls') or ((window.get('probe') or {}).get('calls')) or []
    return {c['frame']: c for c in calls}


def advance_bound(window, frames):
    status = (window.get('probe') or {}).get('status') or {}
    scopes = status.get('scopes') if isinstance(status.get('scopes'), dict) else {}
    if ADVANCE in (scopes.get('component_unbound') or ()):
        return False
    return ADVANCE in (window.get('bound_components') or ()) or ADVANCE in (scopes.get('components') or ()) \
        or any(ADVANCE in f['components'] for f in frames)


def fix128_owners(window, frames, calls, tolerance_ms):
    advance = {}
    for f in frames:
        if not f['clean']:
            continue
        ms = f['components'].get(ADVANCE)
        if not ms and f['frame'] in calls:
            ms = calls[f['frame']].get('duration_ms')
        if ms:
            advance[f['frame']] = ms
    owners = {}
    for index, observation in enumerate(analyze.observations_of(window)):
        slowest = observation['summary'].get('slowest_advance')
        if slowest is None:
            continue
        maximum = observation['summary']['advancement']['max_advance_ms']
        first, last = observation.get('first_frame'), observation.get('last_frame')
        matches = [fr for fr, ms in advance.items() if abs(ms - maximum) <= tolerance_ms
                   and (first is None or fr >= first) and (last is None or fr <= last)]
        if len(matches) == 1:
            owners[matches[0]] = dict(unit_kind=slowest['unit_kind'], unit_ms=slowest['unit_ms'], optional_stop_ms=slowest['optional_stop_ms'],
                                      gc_count=slowest['gc_count'], source='summary-maximum', observation=index, observation_max_ms=maximum)
    return advance, owners


def advancement_of(frame, ctx):
    fr = frame['frame']
    ms = ctx['advance'].get(fr)
    if ms is None and ctx['bound']:
        return None
    call = ctx['calls'].get(fr)
    unit = ctx['owners'].get(fr) or dict(unit_kind=UNKNOWN, unit_ms=UNKNOWN, optional_stop_ms=UNKNOWN, gc_count=UNKNOWN, source=UNKNOWN)
    return dict(frame=fr, advance_ms=UNKNOWN if ms is None else ms,
                advance_source=UNKNOWN if ms is None else ('component' if frame['components'].get(ADVANCE) else 'per-call record'),
                call_gc_delta=call.get('gc_delta', UNKNOWN) if call else UNKNOWN,
                call_optional_stop_ms=call.get('optional_stop_ms', UNKNOWN) if call else UNKNOWN, unit=unit)



def _count_between(ordered, lo, hi):
    return bisect.bisect_right(ordered, hi) - bisect.bisect_left(ordered, lo)


def _largest(components):
    return max(components, key=components.get) if components else UNKNOWN


def classify(stall, ctx):
    interval = stall['interval_ms']
    frames = stall['frames']
    excess = interval - stall['baseline_ms'] if stall['baseline_ms'] is not None else interval
    need = EVIDENCE_SHARE * excess
    mod = math.fsum(f['mod'] for f in frames)
    classes, evidence = [], {}
    stall.update(excess_ms=excess, mod_work_ms=mod, not_pc_ms=interval - mod)

    if frames and mod > 0 and mod >= need:
        worst = max(frames, key=lambda f: f['mod'])
        classes.append('pc-union')
        evidence['pc-union'] = dict(mod_work_ms=mod, needed_ms=need, frame=worst['frame'], component=_largest(worst['components']),
                                    components=worst['components'])
    elif frames and mod > 0 and stall['baseline_ms'] is None and stall.get('window_p50_ms') is not None \
            and mod >= EVIDENCE_SHARE * (interval - stall['window_p50_ms']):
        stall['pc_union'] = 'unresolved (no valid matched reference p50)'
    gcs = [f['gc'] for f in frames if f['gc'] is not None]
    if any(g > 0 for g in gcs):
        classes.append('gc')
        evidence['gc'] = dict(collections=sum(gcs), frames=[f['frame'] for f in frames if f['gc']], source='probe')
    elif frames and not gcs:
        stall['gc'] = UNKNOWN
    scene = [dict(frame=f['frame'], saves=f['saves'], scene=f['scene'], loading=f['loading'], source='probe')
             for f in frames if (f['saves'] or 0) > 0 or f['scene'] or f['loading']]
    scene += [dict(saving=r['saving'], scene_change=r['scene_change'], source='rlo') for r in stall['rlo'] if r['saving'] or r['scene_change']]
    if scene:
        classes.append('save-load-scene')
        evidence['save-load-scene'] = scene
    native = []
    mod_by_frame = {f['frame']: f['mod'] for f in frames}
    for r in stall['rlo']:
        for name, ms in r['phases'].items():
            if name in UNNAMED_PHASES or name.lower() in PRESENT_WAIT_PHASES:
                continue
            outside_pc = ms - mod_by_frame.get(r['frame'], mod)
            if outside_pc >= need and outside_pc > 0:
                native.append(dict(source='rlo', frame=r['frame'], phase=name, phase_ms=ms, outside_pc_ms=outside_pc))
    for p in stall['presents']:
        if p.cpu_busy_ms is not None and p.cpu_busy_ms - mod >= need and p.cpu_busy_ms - mod > 0:
            native.append(dict(source='trace', cpu_busy_ms=p.cpu_busy_ms, outside_pc_ms=p.cpu_busy_ms - mod))
    if native:
        classes.append('native-frame')
        evidence['native-frame'] = native
    gpu = []
    for p in stall['presents']:
        if p.gpu_busy_ms is not None and p.gpu_busy_ms >= need and p.gpu_busy_ms > 0:
            gpu.append(dict(source='trace', gpu_busy_ms=p.gpu_busy_ms))
        if p.cpu_wait_ms is not None and p.cpu_wait_ms >= need and p.cpu_wait_ms > 0:
            gpu.append(dict(source='trace', present_wait_ms=p.cpu_wait_ms))
    if gpu:
        classes.append('gpu-present')
        evidence['gpu-present'] = gpu
    ref = stall.get('reference')
    if ref and ref['seconds'] and ctx['seconds']:
        lo, hi = interval / SIZE_BAND, interval * SIZE_BAND
        own = _count_between(ctx['own_stalls'], lo, hi)
        theirs = _count_between(ref['stalls'], lo, hi)
        own_rate, ref_rate = own * 300.0 / ctx['seconds'], theirs * 300.0 / ref['seconds']
        if theirs and ref_rate >= RATE_SHARE * own_rate:
            classes.append('reference-rate')
            evidence['reference-rate'] = dict(reference=ref['id'], reference_stalls=theirs, reference_per_5_min=ref_rate,
                                              candidate_stalls=own, candidate_per_5_min=own_rate, size_band_ms=[lo, hi])
    if not classes:
        classes.append('unattributed')
    stall['classes'] = classes
    stall['evidence'] = evidence
    advancement = [a for a in (advancement_of(f, ctx) for f in frames if f['mod'] > 0 or f['frame'] in ctx['advance']) if a]
    if advancement:
        stall['advancement'] = advancement
    return stall



def _p50(values):
    return analyze.nearest_rank(sorted(values), 50) if values else None


def presented_series(frames, joined, trace):
    series, pending = [], []
    order = sorted(range(len(frames)), key=lambda i: frames[i]['qpc_ms'])
    for i in order:
        pending.append(frames[i])
        p = joined[i]
        if p is None:
            continue
        if not trace.presents[p].displayed:
            continue
        interval = present.displayed_interval(trace, p)
        if interval is not None and all(f['clean'] for f in pending):
            series.append(dict(interval_ms=interval, present=trace.presents[p], frames=pending))
        pending = []
    return series


def join_window(frames, trace, offset, source):
    joined = present.join(present.Frames([f['qpc_ms'] for f in frames], [f['whole'] for f in frames]), trace, offset)
    clean = [i for i, f in enumerate(frames) if f['clean']]
    hits = sum(1 for i in clean if joined[i] is not None)
    shown = [joined[i] for i in clean if joined[i] is not None]
    first = min(frames, key=lambda f: f['qpc_ms'])
    last = max(frames, key=lambda f: f['qpc_ms'])
    covers = bool(trace.times) and trace.times[0] + offset <= first['qpc_ms'] + first['whole'] + max(2.0, 0.5 * first['whole']) \
        and trace.times[-1] + offset >= last['qpc_ms'] + last['whole'] - max(2.0, 0.5 * last['whole'])
    series = presented_series(frames, joined, trace) if trace.display_source else []
    bounds = {'gpu': 0, 'cpu': 0, UNKNOWN: 0}
    for s in series:
        bounds[present.bound_of(s['present'], s['interval_ms'])] += 1
    block = dict(offset_ms=offset, offset_source=source, clock=trace.clock, interval_source=present.interval_source(trace),
                 clean_frames=len(clean), joined=hits, joined_fraction=hits / len(clean) if clean else 0.0,
                 not_displayed=sum(1 for p in shown if not trace.presents[p].displayed), trace_covers_window=covers,
                 presented_intervals=len(series), gpu_busy='reported' if trace.has('gpu_busy') else UNKNOWN,
                 cpu_wait='reported' if trace.has('cpu_wait') else UNKNOWN, bound=bounds)
    block['aligned'] = covers and block['joined_fraction'] >= present.JOIN_MIN_FRACTION
    block['established'] = bool(trace.display_source) and block['aligned']
    if not trace.display_source:
        block['reason'] = 'trace has no display timing (DisplayedTime, DisplayLatency or msBetweenDisplayChange)'
    return series, block, joined


def attach_rlo(lines, frames_by_window):
    index = sorted((f['whole'], wid, f['frame']) for wid, frames in frames_by_window.items() for f in frames)
    keys = [k[0] for k in index]
    attached, unmatched = {}, []
    for entry in lines:
        parsed = parse_rlo_line(entry['line'])
        if parsed is None:
            continue
        wid, frame = entry.get('window'), entry.get('frame')
        if frame is None:
            ms = parsed['frame_ms']
            if wid is None and entry.get('first_frame') is None and entry.get('last_frame') is None:
                unmatched.append(dict(line=entry['line'][:160], frame_ms=ms, window=None, candidates=None,
                                      reason='no window or frame range: context only'))
                continue
            tol = max(RLO_TOLERANCE_MS, RLO_TOLERANCE_FRACTION * ms)
            lo, hi = bisect.bisect_left(keys, ms - tol), bisect.bisect_right(keys, ms + tol)
            first, last = entry.get('first_frame'), entry.get('last_frame')
            matches = [index[i] for i in range(lo, hi) if (wid is None or index[i][1] == wid)
                       and (first is None or index[i][2] >= first) and (last is None or index[i][2] <= last)]
            if len(matches) != 1:
                unmatched.append(dict(line=entry['line'][:160], frame_ms=ms, window=wid, candidates=len(matches)))
                continue
            _, wid, frame = matches[0]
        parsed['frame'] = frame
        attached.setdefault((wid, frame), []).append(parsed)
    return attached, unmatched


def _rlo_entries(window, extra):
    out = []
    for item in window.get('rlo_lines') or []:
        entry = {'line': item} if isinstance(item, str) else dict(item)
        entry['window'] = window['id']
        out.append(entry)
    return out + extra


def _entries(windows, contract):
    out = {}
    for w in windows:
        try:
            out[w['id']] = analyze.analyze_window(w, contract)[0]
        except analyze.Refused:
            out[w['id']] = None
    return out


def _reference_for(window, windows, entries, contract):
    key = (window.get('condition'), window.get('script'), window.get('arm', 'primary'), window.get('repetition'))
    own = entries.get(window['id'])
    if own is None:
        return None
    for w in windows:
        if w.get('build') != 'reference' or (w.get('condition'), w.get('script'), w.get('arm', 'primary'), w.get('repetition')) != key:
            continue
        entry = entries.get(w['id'])
        if entry is not None and entry['valid'] and not analyze.matched(own, entry, contract):
            return w
    return None


def attribute(export, traces=None, player_log_lines=(), contract=None):
    contract = contract or analyze.load_contract()
    stall_ms = contract['_rows']['P3']['stall_ms']
    tolerance = contract['attribution']['summary_maximum_match_tolerance_ms']
    traces = traces or {}
    enriched = dict(export, windows=[dict(w, receipts=dict(w.get('receipts') or {})) for w in export.get('windows', [])])
    windows = enriched.get('windows', [])
    frames_by_window = {w['id']: frame_rows(w) for w in windows}

    by_trace = {}
    for w in windows:
        trace = traces.get(w['id'], traces.get('*'))
        if trace is not None:
            by_trace.setdefault(id(trace), (trace, []))[1].append(w['id'])
    presentation, series_by_window, joined_by_window = {}, {}, {}
    for trace, ids in by_trace.values():
        usable = [i for i in ids if frames_by_window[i] and all(f['qpc_ms'] is not None for f in frames_by_window[i])]
        for i in set(ids) - set(usable):
            presentation[i] = dict(established=False, reason='probe frames lack QPC times')
        if not usable:
            continue
        all_frames = [f for i in usable for f in frames_by_window[i]]
        try:
            offset, source, _ = present.estimate_offset(
                present.Frames([f['qpc_ms'] for f in all_frames], [f['whole'] for f in all_frames]), trace,
                hint_ms=0.0 if trace.clock == 'qpc' else None)
        except present.TraceError as error:
            for i in usable:
                presentation[i] = dict(established=False, reason=str(error))
            continue
        for i in usable:
            series, presentation[i], joined = join_window(frames_by_window[i], trace, offset, source)
            if trace.display_source:
                series_by_window[i] = series
            joined_by_window[i] = (trace, joined)

    for w in windows:
        wid = w['id']
        if wid not in presentation:
            continue
        receipts = w['receipts']
        if not presentation[wid]['established']:
            if receipts.get('presentation_trace') in (None, 'pass'):
                receipts['presentation_trace'] = 'fail'
        elif receipts.get('presentation_trace') is None:
            receipts['presentation_trace'] = 'pass'
        if wid in series_by_window:
            w['presented_ms'] = [s['interval_ms'] for s in series_by_window[wid]]
    entries = _entries(windows, contract)

    rlo, unmatched = attach_rlo([e for w in windows for e in _rlo_entries(w, [])] +
                                [dict(line=l) for l in player_log_lines if 'Session: a frame of' in l], frames_by_window)

    stats = {}
    for w in windows:
        frames = frames_by_window[w['id']]
        clean = [f for f in frames if f['clean']]
        series = series_by_window.get(w['id'])
        stats[w['id']] = dict(whole=[f['whole'] for f in clean], presented=[s['interval_ms'] for s in series] if series is not None else None,
                              seconds=w.get('clean_real_seconds') or 0)

    stalls, m2, unattributed = [], [], []
    m2_threshold = contract['_rows']['M2']['threshold']
    for w in windows:
        wid = w['id']
        frames = frames_by_window[wid]
        calls = advance_calls(w)
        bound = advance_bound(w, frames)
        advance, owners = fix128_owners(w, frames, calls, tolerance)
        candidate = w.get('build') == 'candidate'
        ref = _reference_for(w, windows, entries, contract) if candidate else None
        ref_stats = stats.get(ref['id']) if ref else None
        base = dict(window=wid, condition=w.get('condition'), script=w.get('script'), build=w.get('build'), arm=w.get('arm', 'primary'))
        ctx = dict(advance=advance, owners=owners, calls=calls, bound=bound, seconds=stats[wid]['seconds'])

        def baseline(kind):
            theirs = ref_stats[kind] if ref_stats else None
            if theirs:
                return _p50(theirs), f"reference {ref['id']} p50"
            if candidate:
                return None, 'none (no valid matched reference window)'
            return _p50(stats[wid][kind] or []), 'window p50 (reference window)'

        def reference(kind):
            if not ref_stats or ref_stats[kind] is None:
                return None
            return dict(id=ref['id'], stalls=sorted(v for v in ref_stats[kind] if v >= stall_ms), seconds=ref_stats['seconds'])

        series = series_by_window.get(wid)
        classes_presented = {}
        if series is not None:
            base_ms, base_src = baseline('presented')
            own_p50 = _p50(stats[wid]['presented'])
            ctx['own_stalls'] = sorted(s['interval_ms'] for s in series if s['interval_ms'] >= stall_ms)
            presented_reference = reference('presented')
            for i, s in enumerate(series):
                if s['interval_ms'] < stall_ms:
                    continue
                stall = dict(base, kind='presented', index=i, interval_ms=s['interval_ms'], frames=s['frames'], presents=[s['present']],
                             baseline_ms=base_ms, baseline_source=base_src, window_p50_ms=own_p50, rlo=[r for f in s['frames'] for r in rlo.get((wid, f['frame']), [])],
                             reference=presented_reference, bound=present.bound_of(s['present'], s['interval_ms']),
                             gpu_busy_ms=_known(s['present'].gpu_busy_ms), cpu_wait_ms=_known(s['present'].cpu_wait_ms))
                classify(stall, ctx)
                classes_presented[str(i)] = stall['classes']
                stalls.append(stall)
        base_ms, base_src = baseline('whole')
        own_p50 = _p50(stats[wid]['whole'])
        ctx['own_stalls'] = sorted(v for v in stats[wid]['whole'] if v >= stall_ms)
        whole_reference = reference('whole')
        trace, joined = joined_by_window.get(wid, (None, None))
        use_join = joined is not None and presentation[wid].get('aligned', False)
        classes_whole = {}
        for k, f in enumerate(frames):
            if not f['clean'] or f['whole'] < stall_ms:
                continue
            presents = [trace.presents[joined[k]]] if use_join and joined[k] is not None else []
            stall = dict(base, kind='whole-frame', frame=f['frame'], interval_ms=f['whole'], frames=[f], presents=presents,
                         baseline_ms=base_ms, baseline_source=base_src, window_p50_ms=own_p50, rlo=rlo.get((wid, f['frame']), []), reference=whole_reference)
            if presents:
                stall.update(bound=present.bound_of(presents[0], f['whole']), gpu_busy_ms=_known(presents[0].gpu_busy_ms),
                             cpu_wait_ms=_known(presents[0].cpu_wait_ms))
            classify(stall, ctx)
            classes_whole[str(f['frame'])] = stall['classes']
            stalls.append(stall)
        for f in frames:
            if f['clean'] and f['mod'] >= m2_threshold:
                entry = dict(base, frame=f['frame'], mod_work_ms=f['mod'], whole_frame_ms=f['whole'], gc=UNKNOWN if f['gc'] is None else f['gc'],
                             component=_largest(f['components']), components=f['components'], game_minute=f['minute'])
                adv = advancement_of(f, ctx)
                if adv:
                    entry['advancement'] = adv
                m2.append(entry)

        if series is not None:
            w['stall_classes'] = classes_presented
        w['whole_frame_stall_classes'] = classes_whole
        w['presentation'] = presentation.get(wid, dict(established=False, reason='no presentation trace for this window'))

    for s in stalls:
        covered = s.pop('frames')
        s['frame_numbers'] = [f['frame'] for f in covered]
        s['game_minute'] = covered[-1]['minute'] if covered else None
        s.pop('presents')
        s['rlo'] = [dict(frame=r['frame'], frame_ms=r['frame_ms'], phases=r['phases']) for r in s['rlo']]
        if s['classes'] == ['unattributed']:
            unattributed.append(s)
    report = dict(format='perf-gate-attribution/v1', contract=contract['contract'], presentation=presentation, stalls=stalls,
                  unattributed=[dict(window=s['window'], kind=s['kind'], frame=s.get('frame', s['frame_numbers'][-1] if s['frame_numbers'] else None),
                                     index=s.get('index'), interval_ms=s['interval_ms'], game_minute=s['game_minute'],
                                     condition=s['condition'], mod_work_ms=s['mod_work_ms']) for s in unattributed],
                  m2=m2, rlo_unmatched=unmatched,
                  limits=['Whole-frame and presented time minus the PC union is never attributed to PC.',
                          'Classes are not exclusive; a stall carries every class whose evidence is present, else unattributed.',
                          'Fix-128 summary fields attach only to the one frame matching an observation maximum; other advancement frames are unknown.',
                          'GPU-bound or CPU-bound is stated only when the trace reports GPU busy; otherwise unknown.',
                          'RLO lines carry no frame number; a line attaches only to a unique frame of matching duration within its window or '
                          'frame range. Player.log lines without either are context only (rlo_unmatched).',
                          'Without a valid, matched reference window there is no reference p50 or reference-rate: the excess is the whole '
                          'interval, and a stall whose union would meet the share only against a reference p50 is marked unresolved.'])
    return enriched, report


def _known(value):
    return UNKNOWN if value is None else value



def _ms(v):
    return v if isinstance(v, str) else ('-' if v is None else f'{v:.3f}')


def render_markdown(report):
    lines = [f"# Stall attribution ({report['contract']})", '', '## Presentation join', '',
             '| Window | Established | Joined / clean frames | Offset ms (source) | Intervals | GPU busy | CPU wait | Bound gpu / cpu / unknown | Reason |',
             '| --- | --- | --- | --- | ---: | --- | --- | --- | --- |']
    for wid, p in report['presentation'].items():
        if 'joined' not in p:
            lines.append(f"| {wid} | no | - | - | - | - | - | - | {p.get('reason', '')} |")
            continue
        b = p['bound']
        lines.append(f"| {wid} | {'yes' if p['established'] else 'no'} | {p['joined']} / {p['clean_frames']} ({p['joined_fraction']:.4f}) | "
                     f"{p['offset_ms']:.3f} ({p['offset_source']}) | {p['presented_intervals']} | {p['gpu_busy']} | {p['cpu_wait']} | "
                     f"{b['gpu']} / {b['cpu']} / {b[UNKNOWN]} | {'' if p['trace_covers_window'] else 'trace does not cover the window'} |")
    lines += ['', '## Stalls (>= 50 ms)', '', '| Window | Kind | Frame or index | Interval ms | Baseline ms | PC union ms | Not PC ms | Minute | Classes |',
              '| --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- |']
    for s in report['stalls']:
        lines.append(f"| {s['window']} | {s['kind']} | {s.get('frame', s.get('index'))} | {_ms(s['interval_ms'])} | {_ms(s['baseline_ms'])} | "
                     f"{_ms(s['mod_work_ms'])} | {_ms(s['not_pc_ms'])} | {s['game_minute']} | {', '.join(s['classes'])} |")
    if not report['stalls']:
        lines.append('| none | | | | | | | | |')
    lines += ['', '## Unattributed (open evidence)', '']
    lines += [f"- `{u['window']}` {u['kind']} frame {u['frame']}: {_ms(u['interval_ms'])} ms at minute {u['game_minute']} ({u['condition']})"
              for u in report['unattributed']] or ['None.']
    lines += ['', '## M2 frames', '', '| Window | Frame | Mod-work ms | Component | GC | Advance ms (source) | Call GC / optional-stop ms | Slowest unit (source) |',
              '| --- | ---: | ---: | --- | --- | --- | --- | --- |']
    for s in report['m2']:
        a = s.get('advancement')
        if a is None:
            adv, call, unit = 'none (outside advancement)', '-', 'n/a'
        else:
            u = a['unit']
            adv, call = f"{_ms(a['advance_ms'])} ({a['advance_source']})", f"{a['call_gc_delta']} / {a['call_optional_stop_ms']}"
            unit = f"{u['unit_kind']} {u['unit_ms']} ms ({u['source']})"
        lines.append(f"| {s['window']} | {s['frame']} | {_ms(s['mod_work_ms'])} | {s['component']} | {s['gc']} | {adv} | {call} | {unit} |")
    if not report['m2']:
        lines.append('| none | | | | | | | |')
    lines += ['', '## Limits', ''] + [f'- {limit}' for limit in report['limits']] + ['']
    return '\n'.join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('export', type=Path)
    parser.add_argument('--trace', type=Path, help='presentation CSV for every window')
    parser.add_argument('--trace-for', action='append', default=[], metavar='WINDOW=CSV', help='presentation CSV for one window')
    parser.add_argument('--qpc-frequency', type=float)
    parser.add_argument('--process-id')
    parser.add_argument('--application')
    parser.add_argument('--player-log', type=Path, action='append', default=[])
    parser.add_argument('--export-out', type=Path, required=True)
    parser.add_argument('--json-out', type=Path, required=True)
    parser.add_argument('--md-out', type=Path, required=True)
    args = parser.parse_args(argv)
    export = json.loads(args.export.read_text(encoding='utf-8-sig'))
    status_freq = next((((w.get('probe') or {}).get('status') or {}).get('stopwatch_frequency') for w in export.get('windows', [])
                        if ((w.get('probe') or {}).get('status') or {}).get('stopwatch_frequency')), None)
    frequency = args.qpc_frequency or status_freq or next((w.get('qpc_frequency') for w in export.get('windows', []) if w.get('qpc_frequency')), None)
    cache, traces = {}, {}

    def load(path):
        if path not in cache:
            cache[path] = present.read_trace(path, frequency, args.process_id, args.application)
        return cache[path]
    try:
        if args.trace:
            traces['*'] = load(args.trace)
        for item in args.trace_for:
            wid, _, path = item.partition('=')
            traces[wid] = load(Path(path))
    except present.TraceError as error:
        print('REFUSED: ' + str(error), file=sys.stderr)
        return 2
    log_lines = [l for p in args.player_log for l in p.read_text(encoding='utf-8', errors='replace').splitlines()]
    enriched, report = attribute(export, traces, log_lines)
    for path in (args.export_out, args.json_out, args.md_out):
        path.parent.mkdir(parents=True, exist_ok=True)
    args.export_out.write_text(json.dumps(enriched) + '\n', encoding='utf-8')
    args.json_out.write_text(json.dumps(report, indent=1, default=str) + '\n', encoding='utf-8')
    args.md_out.write_text(render_markdown(report), encoding='utf-8')
    print(f"{len(report['stalls'])} stalls, {len(report['unattributed'])} unattributed: {args.export_out} {args.json_out} {args.md_out}")
    return 0


if __name__ == '__main__':
    sys.exit(main())
