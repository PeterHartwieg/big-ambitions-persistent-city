"""F4a perf-gate analyzer: judge exported gate windows against a frozen contract (perf-gate/v1).

Input: one or more perf-gate-export/v1 JSON files (fields in docs/checks/f4a.md). Output: a
perf-gate-report/v1 JSON object and a Markdown summary (report.py). The analyzer refuses a report
with a missing header field, a contract-version mismatch, mixed offline/native sources, or exports
from different headers. It never averages percentiles and never takes a percentile of percentiles.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import summary_lines

HERE = Path(__file__).resolve().parent
DEFAULT_CONTRACT = HERE / 'contract-v1.json'
REPORT_FORMAT = 'perf-gate-report/v1'
EXPORT_FORMAT = 'perf-gate-export/v1'
PASS, FAIL, NOT_ESTABLISHED, NA, FLAG = 'pass', 'fail', 'not established', 'n/a', 'flag'
NATIVE_BASELINE = '60 FPS not established on this machine (native baseline)'
ADVANCE_COMPONENT = 'AdvancePopulation'
PROBE_FLAG_FIELDS = ['event_dropped', 'frame_dropped', 'timed_exceptions', 'cross_frame_scopes', 'off_main_callbacks']
PROBE_STATUS_FIELDS = PROBE_FLAG_FIELDS + ['error', 'measuring']
UNKNOWN = summary_lines.UNKNOWN


class Refused(Exception):
    """The input cannot produce a report; nothing is written."""


def load_contract(path=DEFAULT_CONTRACT):
    raw = Path(path).read_bytes()
    contract = json.loads(raw.decode('utf-8-sig'))
    contract['_sha256'] = hashlib.sha256(raw).hexdigest()
    contract['_rows'] = {row['id']: row for row in contract['rows']}
    return contract


# ---------------------------------------------------------------- statistics

def nearest_rank(sorted_values, p):
    """Nearest-rank percentile of an ascending list: the value at 1-based rank ceil(p/100*n)."""
    n = len(sorted_values)
    if n == 0:
        return None
    rank = max(1, -(-n * p // 100))  # exact integer ceil for integer p
    return sorted_values[int(rank) - 1]


def distribution(values):
    values = sorted(values)
    if values and (values[0] < 0 or not all(map(math.isfinite, values))):
        raise Refused('invalid timing sample (negative or not finite)')
    if not values:
        return dict(count=0, p50_ms=None, p95_ms=None, p99_ms=None, max_ms=None, mean_ms=None)
    return dict(count=len(values), p50_ms=nearest_rank(values, 50), p95_ms=nearest_rank(values, 95),
                p99_ms=nearest_rank(values, 99), max_ms=values[-1], mean_ms=math.fsum(values) / len(values))


def compare(value, comparator, threshold):
    return {'<': value < threshold, '<=': value <= threshold, '==': value == threshold}[comparator]


def result(verdict, value=None, reason=''):
    return dict(verdict=verdict, value=value, reason=reason)


# ---------------------------------------------------------------- header and sources

def _missing(value):
    return value is None or value == '' or value == [] or value == {}


def validate_header(header, source, contract):
    spec = contract['header']
    if not isinstance(header, dict):
        raise Refused('header missing')
    may_be_na = set(spec['offline_may_be_na']) if source == 'offline' else set()
    missing = []
    for field in spec['required']:
        value = header.get(field)
        if field in may_be_na and value == 'n/a':
            continue
        if _missing(value) or value == 'n/a':
            missing.append(field)
    nested = [('machine', spec['machine_fields'], False), ('settings_read_back', spec['settings_fields'], False),
              ('mod_set', spec['mod_set_fields'], True), ('fixtures', spec['fixture_fields'], True),
              ('rig_capabilities', spec['capability_fields'], True)]
    for field, subfields, is_list in nested:
        value = header.get(field)
        if _missing(value) or value == 'n/a':
            continue
        items = value if is_list else [value]
        if (is_list and not isinstance(value, list)) or (not is_list and not isinstance(value, dict)):
            missing.append(field)
            continue
        for i, item in enumerate(items):
            for sub in subfields:
                if not isinstance(item, dict) or _missing(item.get(sub)):
                    missing.append(f'{field}[{i}].{sub}' if is_list else f'{field}.{sub}')
    if missing:
        raise Refused('header fields missing: ' + ', '.join(missing))
    if header['contract_version'] != contract['contract']:
        raise Refused(f"header contract_version {header['contract_version']!r} is not {contract['contract']!r}")


def collect_sources(exports):
    sources = set()
    for export in exports:
        sources.add(export.get('source'))
        for window in export.get('windows', []):
            sources.add(window.get('source', export.get('source')))
    if None in sources or not sources <= {'native', 'offline'}:
        raise Refused('every export and window needs source=native or source=offline')
    if len(sources) != 1:
        raise Refused('offline and native windows cannot share one verdict')
    return sources.pop()


# ---------------------------------------------------------------- ingestion

CAMEL = ('modWorkMs', 'wholeFrameMs', 'observerMs', 'gcCollections')
SNAKE = ('mod_work_ms', 'whole_frame_ms', 'observer_ms', 'gc')


def _frames(rows):
    """Normalise probe frame rows: camelCase probe export or snake_case, one naming per window."""
    if not rows:
        return []
    mod, whole, observer, gc = CAMEL if 'modWorkMs' in rows[0] else SNAKE
    try:
        return [dict(frame=r.get('frame'), mod_work_ms=r[mod], whole_frame_ms=r[whole], observer_ms=r.get(observer, 0.0),
                     gc=r.get(gc), diagnostic=bool(r.get('diagnostic')), warmup=bool(r.get('warmup')),
                     components=r.get('components') or {}) for r in rows]
    except KeyError as missing:
        raise Refused(f'frame row without {missing}') from None


def ingest_probe(window):
    """Frames from window['probe'] (status, frames[]) or window['frames']; probe status flags become receipts."""
    receipts = dict(window.get('receipts') or {})
    probe = window.get('probe')
    if probe is None:
        rows = window.get('frames') or []
        return _frames(rows), receipts, []
    status = probe.get('status') or {}
    flags = [f'status_field_missing:{f}' for f in PROBE_STATUS_FIELDS if f not in status]
    flags += [f for f in PROBE_FLAG_FIELDS if status.get(f)]
    if status.get('error'):
        flags.append('probe_error')
    if status.get('measuring'):
        flags.append('measurement_not_stopped')
    rows = probe.get('frames') or []
    if len({r.get('frame') for r in rows}) != len(rows):
        flags.append('duplicate_frames')
    if any(r.get('skippedUnityFrames', 0) for r in rows):
        flags.append('unobserved_unity_frames')
    if window.get('build') == 'candidate' and not status.get('timing_coverage_complete', False):
        receipts['probe_coverage'] = 'fail'
    if flags:
        receipts['probe_flags'] = 'fail'
    return _frames(rows), receipts, flags


def observations_of(window):
    result_list = []
    for item in window.get('summary_lines') or []:
        entry = {'line': item} if isinstance(item, str) else dict(item)
        parsed = summary_lines.parse_line(entry['line'])
        if parsed is not None:
            result_list.append(dict(summary=parsed, first_frame=entry.get('first_frame'), last_frame=entry.get('last_frame')))
    return result_list


# ---------------------------------------------------------------- one window

def validity(window, receipts, flags, clean, contract):
    reasons = []
    spec = contract['receipts']
    reference = window.get('build') == 'reference'
    for name in spec['required']:
        value = receipts.get(name)
        if value is None:
            reasons.append(f'receipt missing: {name}')
        elif value == 'fail':
            reasons.append(f'receipt failed: {name}')
        elif value == 'n/a':
            if not (reference and name in spec['reference_may_be_na']):
                reasons.append(f'receipt n/a not allowed: {name}')
        elif value != 'pass':
            reasons.append(f'receipt {name} has unknown value {value!r}')
    reasons += [f'probe flag: {f}' for f in flags]
    if window.get('aborted'):
        reasons.append('aborted')
    if window.get('first_in_process') and contract['windows']['discard_first_window_per_process']:
        reasons.append('first window of the game process (discarded)')
    seconds = window.get('clean_real_seconds')
    if seconds is None or seconds < contract['windows']['min_clean_real_seconds']:
        reasons.append(f"short: {seconds} clean real seconds < {contract['windows']['min_clean_real_seconds']}")
    if not clean:
        reasons.append('no clean frames')
    if window.get('build') not in contract['windows']['builds']:
        reasons.append(f"unknown build {window.get('build')!r}")
    if window.get('arm', 'primary') not in contract['windows']['arms']:
        reasons.append(f"unknown arm {window.get('arm')!r}")
    return reasons


def window_rows(window, clean, mod, presented, contract):
    rows = contract['_rows']
    out = {}
    candidate = window['build'] == 'candidate'
    for rid in ('M1', 'M3', 'M4'):
        row = rows[rid]
        if not candidate:
            out[rid] = result(NA, reason='reference build has no PC mod-work')
            continue
        value = mod[{'p95': 'p95_ms', 'p99': 'p99_ms', 'mean': 'mean_ms'}[row['statistic']]]
        out[rid] = result(PASS if compare(value, row['comparator'], row['threshold']) else FAIL, value)
    for rid in ('P1', 'P2'):
        row = rows[rid]
        if presented is None or presented['count'] == 0:
            out[rid] = result(NOT_ESTABLISHED, reason='no presented-frame series (RIG-PRESENT, F4c)')
            continue
        value = presented[{'p95': 'p95_ms', 'p99': 'p99_ms'}[row['statistic']]]
        out[rid] = result(PASS if compare(value, row['comparator'], row['threshold']) else FAIL, value)
    out['P3'] = p3(window, contract) if candidate else result(NA, reason='reference stalls feed reference-rate (F4c)')
    accounting = window.get('accounting')
    stall_ms = rows['P3']['stall_ms']
    stalls = sum(1 for v in window.get('presented_ms') or [] if v >= stall_ms) + sum(1 for f in clean if f['whole_frame_ms'] >= stall_ms)
    for rid, fn in (('S1', s1), ('S2', s2), ('S3', s3), ('S4', lambda w, a, r: s4(w, a, r, stalls))):
        if not candidate:
            out[rid] = result(NA, reason='reference build has no PC calendar')
        elif accounting is None:
            out[rid] = result(NOT_ESTABLISHED, reason='no accounting records (RIG-PERF-UNION)')
        else:
            out[rid] = fn(window, accounting, rows[rid])
    return out


def p3(window, contract):
    row = contract['_rows']['P3']
    series = window.get('presented_ms')
    if not series:
        return result(NOT_ESTABLISHED, reason='no presented-frame series (RIG-PRESENT, F4c)')
    seconds = window.get('clean_real_seconds') or 0
    stalls = [i for i, v in enumerate(series) if v >= row['stall_ms']]
    per_five = len(stalls) * 300.0 / seconds if seconds > 0 else math.inf
    classes = window.get('stall_classes') or {}
    severe = [i for i in stalls if series[i] >= row['severe_stall_ms']]
    value = dict(stalls=len(stalls), per_5_min=per_five, severe=len(severe))
    if per_five > row['max_stalls_per_5_min']:
        return result(FAIL, value, f"{per_five:.2f} stalls per 5 minutes")
    if any(row['severe_forbidden_class'] in classes.get(str(i), []) for i in severe):
        return result(FAIL, value, 'severe stall classed pc-union')
    if any(str(i) not in classes for i in severe):
        return result(NOT_ESTABLISHED, value, 'severe stall not classified (F4c)')
    return result(PASS, value)


def s1(window, accounting, row):
    misses = accounting.get('clock_misses')
    if misses is None:
        return result(NOT_ESTABLISHED, reason='historicalClockMisses not recorded')
    return result(PASS if compare(misses, row['comparator'], row['threshold']) else FAIL, misses)


def s2(window, accounting, row):
    samples = accounting.get('lag_samples')
    if not samples:
        return result(NOT_ESTABLISHED, reason='no lag samples')
    worst = max(lag for _, lag in samples)
    return result(PASS if compare(worst, row['comparator'], row['threshold']) else FAIL, worst)


def s3(window, accounting, row):
    if window.get('native_speed') not in row['native_speeds']:
        return result(NA, reason=f"native speed {window.get('native_speed')} outside 1-3")
    holds = accounting.get('holds')
    if holds is None:
        return result(NOT_ESTABLISHED, reason='no hold/resume records')
    seconds = window.get('clean_real_seconds') or 0
    durations = [h['end_s'] - h['start_s'] for h in holds]
    total = math.fsum(durations)
    longest = max(durations, default=0.0)
    fraction = total / seconds if seconds > 0 else math.inf
    value = dict(held_s=total, held_fraction=fraction, longest_s=longest)
    ok = fraction <= row['held_fraction_max'] and longest <= row['longest_hold_max_s']
    return result(PASS if ok else FAIL, value)


def s4(window, accounting, row, stalls):
    """Recovery to <= 0.1 game minute within 5 real s after each hold; fail only when samples prove it took longer."""
    holds = accounting.get('holds')
    samples = sorted(accounting.get('lag_samples') or [])
    if holds is None or not samples:
        return result(NOT_ESTABLISHED, reason='needs hold records and lag samples')
    limit, within = row['recovered_lag_minutes'], row['within_real_s']
    worst, open_ = 0.0, []
    for hold in holds:
        if hold.get('ongoing'):
            open_.append(f"hold from {hold['start_s']} s still open at window end")
            continue
        end = hold['end_s']
        recovered = next((t for t, lag in samples if t >= end and lag <= limit), None)
        if recovered is not None:
            worst = max(worst, recovered - end)
        elif any(t > end + within for t, _ in samples):
            return result(FAIL, None, f'lag still above {limit} game minute {within} s after the hold ending at {end} s')
        else:
            open_.append(f'no lag sample within {within} s after the hold ending at {end} s')
    if worst > within:
        return result(FAIL, worst, f'recovery took {worst} s')
    if stalls:
        open_.append(f'{stalls} stalls need stall-time alignment with lag samples (F4c)')
    if open_:
        return result(NOT_ESTABLISHED, worst, '; '.join(open_))
    return result(PASS, worst)


def density(window, contract):
    d = window.get('density')
    if not d:
        return dict(dense='unknown')
    spec = contract['density']
    dense = d.get('median_pedestrians', 0) >= spec['min_median_pedestrians'] and d.get('median_cars', 0) >= spec['min_median_cars']
    return dict(d, dense=dense)


def spikes_of(window, clean, contract, valid):
    threshold = contract['_rows']['M2']['threshold']
    calls = {c['frame']: c for c in window.get('advance_calls') or []}
    spikes = []
    # Absence of advancement is confirmed only when the AdvancePopulation sub-scope is bound in this window.
    bound = ADVANCE_COMPONENT in (window.get('bound_components') or ()) or any(ADVANCE_COMPONENT in f['components'] for f in clean)
    for f in clean:
        if f['mod_work_ms'] < threshold:
            continue
        components = f['components']
        call = calls.get(f['frame'])
        # Advancement evidence: the component sub-scope, else the probe's per-call record (component-unbound).
        advance_ms, advance_source = components.get(ADVANCE_COMPONENT), 'component'
        if advance_ms is None and call is not None:
            advance_ms, advance_source = call.get('duration_ms'), 'per-call record'
        if advance_ms is None:
            advance_source = None if bound else UNKNOWN
        spikes.append(dict(window=window['id'], window_valid=valid, arm=window.get('arm', 'primary'), condition=window.get('condition'),
                           script=window.get('script'), frame=f['frame'], mod_work_ms=f['mod_work_ms'], whole_frame_ms=f['whole_frame_ms'],
                           gc=UNKNOWN if f['gc'] is None else f['gc'],
                           components=components, component=max(components, key=components.get) if components else UNKNOWN,
                           advance_ms=advance_ms, advance_source=advance_source, advance_call=call))
    summary_lines.attach_fix128(spikes, observations_of(window), contract['attribution']['summary_maximum_match_tolerance_ms'])
    return spikes


def stalls_of(window, clean, contract):
    limit = contract['_rows']['P3']['stall_ms']
    out = []
    classes = window.get('stall_classes') or {}
    for i, v in enumerate(window.get('presented_ms') or []):
        if v >= limit:
            out.append(dict(window=window['id'], kind='presented', index=i, interval_ms=v, classes=classes.get(str(i), ['unclassified (F4c)'])))
    for f in clean:
        if f['whole_frame_ms'] >= limit:
            out.append(dict(window=window['id'], kind='whole-frame', frame=f['frame'], interval_ms=f['whole_frame_ms'],
                            mod_work_ms=f['mod_work_ms'], classes=['unclassified (F4c)']))
    return out


def analyze_window(window, contract):
    frames, receipts, flags = ingest_probe(window)
    clean = [f for f in frames if not f['diagnostic'] and not f['warmup']]
    reasons = validity(window, receipts, flags, clean, contract)
    mod = distribution([f['mod_work_ms'] for f in clean])
    whole = distribution([f['whole_frame_ms'] for f in clean])
    presented = distribution(window['presented_ms']) if window.get('presented_ms') else None
    valid = not reasons
    entry = dict(id=window['id'], condition=window.get('condition'), script=window.get('script'), build=window.get('build'),
                 arm=window.get('arm', 'primary'), repetition=window.get('repetition'), native_speed=window.get('native_speed'),
                 checkpoint_minute=window.get('checkpoint_minute'), game_span_minutes=window.get('game_span_minutes'),
                 reference_pace_ratio=window.get('reference_pace_ratio'), clean_real_seconds=window.get('clean_real_seconds'),
                 valid=valid, invalid_reasons=reasons, receipts=receipts, frames=len(frames), clean_frames=len(clean),
                 diagnostic_frames=len(frames) - len(clean), mod_work=mod, whole_frame=whole, presented=presented,
                 gc_collections=UNKNOWN if any(f['gc'] is None for f in clean) else sum(f['gc'] for f in clean),
                 gc_unknown_frames=sum(1 for f in clean if f['gc'] is None), density=density(window, contract),
                 bodies=window.get('bodies'), rows=window_rows(window, clean, mod, presented, contract) if clean else {},
                 summaries=[o['summary'] for o in observations_of(window)])
    return entry, clean, spikes_of(window, clean, contract, valid), stalls_of(window, clean, contract)


# ---------------------------------------------------------------- pairing and pooling

def matched(candidate, reference, contract):
    spec = contract['matching']
    why = []
    if candidate['checkpoint_minute'] is None or candidate['checkpoint_minute'] != reference['checkpoint_minute']:
        why.append('checkpoint minute differs')
    if candidate['game_span_minutes'] is None or reference['game_span_minutes'] is None \
            or abs(candidate['game_span_minutes'] - reference['game_span_minutes']) > spec['game_span_tolerance_minutes']:
        why.append('game-time span differs by more than 1 game minute')
    ratio = reference.get('reference_pace_ratio')
    if ratio is None or abs(ratio - 1.0) > spec['reference_pace_tolerance_fraction']:
        why.append('reference pace outside 2 % of the declared unheld pace')
    return why


def pair_windows(entries, contract):
    """P4 per candidate: the reference with the same condition, script, arm and repetition, if it matches."""
    row = contract['_rows']['P4']
    references, valid_keys = {}, set()
    for e in entries:
        key = (e['condition'], e['script'], e['arm'], e['repetition'])
        if e['valid']:
            if (e['build'],) + key in valid_keys:
                raise Refused(f"two valid {e['build']} windows share condition, script, arm and repetition {key}")
            valid_keys.add((e['build'],) + key)
        if e['build'] == 'reference' and (key not in references or (e['valid'] and not references[key]['valid'])):
            references[key] = e  # a valid rerun replaces an invalid reference with the same label
    pairs = []
    for c in entries:
        if c['build'] != 'candidate':
            continue
        ref = references.get((c['condition'], c['script'], c['arm'], c['repetition']))
        pair = dict(candidate=c['id'], reference=ref['id'] if ref else None)
        if ref is None:
            pair.update(result(NOT_ESTABLISHED, reason='no reference window for this condition, script and repetition'))
        elif not c['valid'] or not ref['valid']:
            pair.update(result(NOT_ESTABLISHED, reason='candidate or reference window invalid'))
        elif matched(c, ref, contract):
            pair.update(result(NOT_ESTABLISHED, reason='; '.join(matched(c, ref, contract))))
        elif not c['presented'] or not ref['presented']:
            pair.update(result(NOT_ESTABLISHED, reason='no presented-frame series (RIG-PRESENT, F4c)'))
        else:
            delta = dict(p95_ms=c['presented']['p95_ms'] - ref['presented']['p95_ms'],
                         p99_ms=c['presented']['p99_ms'] - ref['presented']['p99_ms'])
            ok = delta['p95_ms'] <= row['p95_max_ms'] and delta['p99_ms'] <= row['p99_max_ms']
            pair.update(result(PASS if ok else FAIL, delta))
            for rid in ('P1', 'P2'):
                ref_row = contract['_rows'][rid]
                ref_value = ref['presented'][{'p95': 'p95_ms', 'p99': 'p99_ms'}[ref_row['statistic']]]
                if not compare(ref_value, ref_row['comparator'], ref_row['threshold']):
                    c['rows'][rid] = result(NOT_ESTABLISHED, c['rows'][rid]['value'],
                                            f'{NATIVE_BASELINE}: reference {rid} {ref_value} ms')
                    c['rows'][rid]['native_baseline'] = True
        c['rows']['P4'] = {k: pair[k] for k in ('verdict', 'value', 'reason')}
        pairs.append(pair)
    return pairs


def pooled(entries, clean_by_id, presented_by_id):
    """Pool raw clean frames of valid windows per condition, script, build and arm; percentiles of the pool."""
    groups = {}
    for e in entries:
        if not e['valid']:
            continue
        key = (e['condition'], e['script'], e['build'], e['arm'])
        g = groups.setdefault(key, dict(windows=[], mod=[], presented=[]))
        g['windows'].append(e['id'])
        g['mod'].extend(f['mod_work_ms'] for f in clean_by_id[e['id']])
        g['presented'].extend(presented_by_id.get(e['id']) or [])
    return [dict(condition=k[0], script=k[1], build=k[2], arm=k[3], windows=g['windows'],
                 mod_work=distribution(g['mod']), presented=distribution(g['presented']) if g['presented'] else None)
            for k, g in sorted(groups.items(), key=lambda kv: tuple(str(x) for x in kv[0]))]


# ---------------------------------------------------------------- measurement rows

def measurement_rows(measurements, contract):
    rows = contract['_rows']
    m = measurements or {}
    out = {}

    def samples(rid, key):
        values = (m.get(rid) or {}).get(key)
        return values if values else None

    s5 = m.get('S5')
    if not s5 or not s5.get('hours') or not s5.get('days'):
        out['S5'] = result(NOT_ESTABLISHED, reason='no fastest-traveller hour and day records (G9)')
    else:
        hours = [h['real_s'] / h['unheld_real_s'] for h in s5['hours']]
        days = [d['real_s'] / d['unheld_real_s'] for d in s5['days']]
        ok = max(hours) <= rows['S5']['hour_ratio_max'] and max(days) <= rows['S5']['day_ratio_max'] \
            and s5.get('s1_s2_hold') is True
        out['S5'] = result(PASS if ok else FAIL, dict(max_hour_ratio=max(hours), max_day_ratio=max(days), s1_s2_hold=s5.get('s1_s2_hold')))
    for rid, key in (('L1', 'samples_s'), ('L2', 'samples_ms'), ('L3', 'samples_s')):
        values = samples(rid, key)
        if values is None:
            out[rid] = result(NOT_ESTABLISHED, reason='no samples (G10)')
            continue
        worst = max(values)
        out[rid] = result(PASS if compare(worst, rows[rid]['comparator'], rows[rid]['threshold']) else FAIL, worst)
        if rid == 'L2':
            out[rid]['flagged'] = [v for v in values if v >= rows['L2']['flag_ms']]
    l4 = m.get('L4')
    if not l4 or not l4.get('reference_real_s'):
        out['L4'] = result(NOT_ESTABLISHED, reason='no skip and matched reference duration (G10)')
    else:
        ratio = l4['real_s'] / l4['reference_real_s']
        ok = ratio <= rows['L4']['ratio_max'] and l4.get('s1_s2_hold') is True
        out['L4'] = result(PASS if ok else FAIL, dict(ratio=ratio, s1_s2_hold=l4.get('s1_s2_hold')))
    r1 = m.get('R1')
    days = rows['R1']['soak_game_days']
    if not r1 or len(r1.get('heap_mb_per_day') or []) < days or len(r1.get('m1_p95_per_day') or []) < days:
        out['R1'] = result(NOT_ESTABLISHED, reason=f'needs heap and M1 for each of {days} game days (G14)')
    else:
        heap = r1['heap_mb_per_day'][:days]
        if any(not math.isfinite(h) or h <= 0 for h in heap):
            raise Refused('R1 heap samples must be positive and finite')
        growth = max((b - a) / a for a, b in zip(heap, heap[1:]))
        m1 = r1['m1_p95_per_day'][:days]
        drift = abs(m1[-1] - m1[0]) / m1[0] if m1[0] > 0 else math.inf
        ok = growth <= rows['R1']['heap_growth_per_day_max'] and drift <= rows['R1']['m1_drift_max']
        out['R1'] = result(PASS if ok else FAIL, dict(heap_growth_per_day=growth, m1_drift=drift))
    return out


# ---------------------------------------------------------------- verdicts

def aggregate(entries, contract, arm_filter):
    verdicts = {}
    window_row_ids = [r['id'] for r in contract['rows'] if r['kind'] in ('window', 'stalls', 'delta', 'accounting')]
    for rid in window_row_ids:
        results = [(e['id'], e['rows'].get(rid)) for e in entries
                   if e['valid'] and e['build'] == 'candidate' and arm_filter(e['arm']) and e['rows'].get(rid)]
        results = [(wid, r) for wid, r in results if r['verdict'] != NA]
        fails = [wid for wid, r in results if r['verdict'] == FAIL]
        open_ = [wid for wid, r in results if r['verdict'] == NOT_ESTABLISHED]
        baseline = bool(results) and all(r.get('native_baseline') for _, r in results if r['verdict'] == NOT_ESTABLISHED) and bool(open_)
        if fails:
            verdicts[rid] = dict(verdict=FAIL, reason='fails in ' + ', '.join(fails))
        elif not results:
            verdicts[rid] = dict(verdict=NOT_ESTABLISHED, reason='no valid candidate window')
        elif open_:
            verdicts[rid] = dict(verdict=NOT_ESTABLISHED, native_baseline=baseline,
                                 reason=(NATIVE_BASELINE + ' in ' if baseline else 'not established in ') + ', '.join(open_))
        else:
            verdicts[rid] = dict(verdict=PASS, reason=f'passes in {len(results)} valid windows')
    return verdicts


def m2(spikes, arm_filter):
    count = sum(1 for s in spikes if s['window_valid'] and arm_filter(s['arm']))
    return dict(verdict=FLAG, count=count, reason=f'{count} flagged frames in valid windows')


def dense_street(entries, contract):
    """I4's dense-traffic part: only a valid primary candidate window on a dense script that met both thresholds establishes it."""
    scripts = contract['density']['scripts']
    windows = [e for e in entries if e['valid'] and e['build'] == 'candidate' and e['arm'] == 'primary' and e['script'] in scripts]
    dense = [e['id'] for e in windows if e['density']['dense'] is True]
    if dense:
        return dict(verdict=PASS, reason='dense in ' + ', '.join(dense))
    detail = ', '.join(f"{e['id']}: dense={e['density']['dense']}" for e in windows)
    return dict(verdict=NOT_ESTABLISHED, reason=f"no valid {'/'.join(scripts)} candidate window reached the density thresholds"
                + (f' ({detail})' if detail else ''))


def overall(row_verdicts):
    judged = {rid: v for rid, v in row_verdicts.items() if rid != 'M2'}
    if any(v['verdict'] == FAIL for v in judged.values()):
        return FAIL
    blocking = [rid for rid, v in judged.items() if v['verdict'] != PASS and not v.get('native_baseline')]
    return NOT_ESTABLISHED if blocking else PASS


def analyze(exports, contract):
    if not exports:
        raise Refused('no export')
    for export in exports:
        if export.get('format') != EXPORT_FORMAT:
            raise Refused(f"export format {export.get('format')!r} is not {EXPORT_FORMAT}")
    source = collect_sources(exports)
    header = exports[0].get('header')
    for export in exports:
        validate_header(export.get('header'), source, contract)
        if export.get('header') != header:
            raise Refused('exports carry different headers; one report covers one batch header')
    entries, spikes, stalls, clean_by_id, presented_by_id = [], [], [], {}, {}
    seen = set()
    measurements = {}
    for export in exports:
        for k, v in (export.get('measurements') or {}).items():
            if k in measurements:
                raise Refused(f'measurement {k} appears in more than one export')
            measurements[k] = v
        for window in export.get('windows', []):
            if window.get('id') in seen or window.get('id') is None:
                raise Refused(f"duplicate or missing window id {window.get('id')!r}")
            seen.add(window['id'])
            entry, clean, window_spikes, window_stalls = analyze_window(window, contract)
            entries.append(entry)
            clean_by_id[entry['id']] = clean
            presented_by_id[entry['id']] = window.get('presented_ms')
            spikes += window_spikes
            stalls += window_stalls
    pairs = pair_windows(entries, contract)
    primary = aggregate(entries, contract, lambda arm: arm == 'primary')
    primary['M2'] = m2(spikes, lambda arm: arm == 'primary')
    primary['dense_street'] = dense_street(entries, contract)
    primary.update(measurement_rows(measurements, contract))
    secondary_ids = set(contract['windows']['secondary_arm_rows'])
    secondary = {rid: v for rid, v in aggregate(entries, contract, lambda arm: arm != 'primary').items() if rid in secondary_ids}
    secondary['M2'] = m2(spikes, lambda arm: arm != 'primary')
    verdict = overall(primary)
    baseline = any(v.get('native_baseline') for v in primary.values())
    return dict(format=REPORT_FORMAT, contract=contract['contract'], contract_sha256=contract['_sha256'], source=source,
                header=header, overall=verdict, sixty_fps=NATIVE_BASELINE if baseline else None,
                rows={rid: primary[rid] for rid in list(contract['_rows']) + ['dense_street'] if rid in primary},
                secondary_arm_rows=secondary, windows=entries, pairs=pairs,
                pooled=pooled(entries, clean_by_id, presented_by_id), spikes=spikes, stalls=stalls,
                limits=['Mod-work is the probe union and includes nested probe cost; observerMs measures only part of it.',
                        'Whole-frame time is observer-to-observer wall time, reported separately; whole minus union is never attributed to PC.',
                        'Stall classes come from F4c; until then stalls are listed unclassified.',
                        'Fix-128 summary fields describe only each observation\'s maximum advancement call.'])


def main(argv=None):
    import report as report_writer
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('exports', nargs='+', type=Path)
    parser.add_argument('--contract', type=Path, default=DEFAULT_CONTRACT)
    parser.add_argument('--json-out', type=Path, required=True)
    parser.add_argument('--md-out', type=Path, required=True)
    args = parser.parse_args(argv)
    contract = load_contract(args.contract)
    try:
        exports = [json.loads(p.read_text(encoding='utf-8-sig')) for p in args.exports]
        report = analyze(exports, contract)
    except Refused as refusal:
        print('REFUSED: ' + str(refusal), file=sys.stderr)
        return 2
    report_writer.write(report, args.json_out, args.md_out)
    print(f"{report['overall']}: {args.json_out} {args.md_out}")
    return 0


if __name__ == '__main__':
    sys.exit(main())
