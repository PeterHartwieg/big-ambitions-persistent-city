"""F4c presentation trace ingestion and QPC join (F4 plan, Decision 6).

Reads a PresentMon-class CSV (1.x or 2.x column names) into presents with a time in milliseconds,
whether the present was displayed, the interval between displayed frames, and GPU busy, CPU busy and
CPU wait where the trace reports them. A field the trace lacks is None and is reported `unknown`.

The join maps each probe frame to at most one present. Probe frames carry the Stopwatch (QPC) time
of their start and their whole-frame interval. The trace may use the same QPC clock, seconds from
trace start, or a clock with a constant offset; the offset is estimated from long intervals that
appear in both series, so it needs no shared epoch. Presents missing from the trace leave their
frame unjoined, and dropped (not displayed) presents stay joined but carry no presented interval.
"""
import bisect
import csv
import heapq
import math
import statistics
from pathlib import Path

UNKNOWN = 'unknown'
# G3: the join must cover at least 99 % of probe frames.
JOIN_MIN_FRACTION = 0.99
# A frame is called GPU-bound when GPU busy covers this share of its presented interval.
GPU_BOUND_SHARE = 0.8
# Offset estimation: anchors are intervals >= this factor x the median frame; a candidate needs this many,
# and at least half of the long present intervals, to land on frames of similar length.
DISTINCT_FACTOR = 2.0
MIN_ANCHOR_HITS = 3
# Refinement tries offsets this many median frames either side, on the first frames of the series.
REFINE_SHIFTS = 3
REFINE_SAMPLE = 10000

QPC_COLUMNS = ('cpustartqpc', 'timeinqpc', 'qpc')
SECONDS_COLUMNS = ('timeinseconds', 'cpustarttime')
DISPLAY_COLUMNS = ('msbetweendisplaychange',)
PRESENTS_COLUMNS = ('msbetweenpresents',)
GPU_BUSY_COLUMNS = ('msgpubusy', 'msgpuactive')
CPU_BUSY_COLUMNS = ('mscpubusy',)
CPU_WAIT_COLUMNS = ('mscpuwait',)
DROPPED_COLUMNS = ('dropped',)
DISPLAYED_COLUMNS = ('msuntildisplayed', 'displayedtime')
NOT_A_NUMBER = ('', 'na', 'n/a', 'nan', '-')


class TraceError(Exception):
    """The trace cannot be read or joined."""


class Present:
    __slots__ = ('t_ms', 'displayed', 'display_ms', 'between_ms', 'gpu_busy_ms', 'cpu_busy_ms', 'cpu_wait_ms')

    def __init__(self, t_ms, displayed=True, display_ms=None, between_ms=None, gpu_busy_ms=None, cpu_busy_ms=None, cpu_wait_ms=None):
        self.t_ms, self.displayed, self.display_ms, self.between_ms = t_ms, displayed, display_ms, between_ms
        self.gpu_busy_ms, self.cpu_busy_ms, self.cpu_wait_ms = gpu_busy_ms, cpu_busy_ms, cpu_wait_ms


class Trace:
    def __init__(self, presents, clock, columns):
        self.presents = presents
        self.clock = clock  # 'qpc' or 'seconds'
        self.columns = columns  # which optional columns the trace had
        self.times = [p.t_ms for p in presents]

    def has(self, field):
        return self.columns.get(field) is not None


def _pick(index, names):
    return next((index[n] for n in names if n in index), None)


def _num(text):
    if text is None:
        return None
    text = text.strip()
    if text.lower() in NOT_A_NUMBER:
        return None
    return float(text)


def read_trace(path, qpc_frequency=None, process_id=None, application=None):
    """Parse a presentation CSV. QPC time columns need qpc_frequency (ticks per second)."""
    with open(path, newline='', encoding='utf-8-sig') as handle:
        return parse_rows(csv.reader(handle), qpc_frequency, process_id, application)


def parse_rows(rows, qpc_frequency=None, process_id=None, application=None):
    rows = iter(rows)
    try:
        header = next(rows)
    except StopIteration:
        raise TraceError('empty trace') from None
    index = {name.strip().lower(): i for i, name in enumerate(header)}
    qpc_col, sec_col = _pick(index, QPC_COLUMNS), _pick(index, SECONDS_COLUMNS)
    if qpc_col is not None:
        if not qpc_frequency:
            raise TraceError('trace has QPC times but no QPC frequency')
        clock, time_col, scale = 'qpc', qpc_col, 1000.0 / qpc_frequency
    elif sec_col is not None:
        clock, time_col, scale = 'seconds', sec_col, 1000.0
    else:
        raise TraceError('trace has no time column (' + ', '.join(QPC_COLUMNS + SECONDS_COLUMNS) + ')')
    columns = dict(display=_pick(index, DISPLAY_COLUMNS), between=_pick(index, PRESENTS_COLUMNS), gpu_busy=_pick(index, GPU_BUSY_COLUMNS),
                   cpu_busy=_pick(index, CPU_BUSY_COLUMNS), cpu_wait=_pick(index, CPU_WAIT_COLUMNS), dropped=_pick(index, DROPPED_COLUMNS),
                   displayed=_pick(index, DISPLAYED_COLUMNS))
    pid_col, app_col = index.get('processid'), index.get('application')
    display, between, gpu, cpu_busy, cpu_wait, dropped, displayed = (columns[k] for k in (
        'display', 'between', 'gpu_busy', 'cpu_busy', 'cpu_wait', 'dropped', 'displayed'))
    presents = []
    for row in rows:
        if not row:
            continue
        if process_id is not None and pid_col is not None and row[pid_col].strip() != str(process_id):
            continue
        if application is not None and app_col is not None and row[app_col].strip().lower() != application.lower():
            continue
        t = _num(row[time_col])
        if t is None:
            continue
        display_ms = _num(row[display]) if display is not None else None
        if dropped is not None:
            shown = _num(row[dropped]) in (None, 0.0)
        elif displayed is not None:
            shown = _num(row[displayed]) is not None
        elif display is not None:
            shown = display_ms is not None
        else:
            shown = True
        if display_ms is not None and display_ms <= 0:
            display_ms = None  # PresentMon 1.x writes 0 for the first displayed frame
        presents.append(Present(t * scale, shown, display_ms if shown else None,
                                _num(row[between]) if between is not None else None,
                                _num(row[gpu]) if gpu is not None else None,
                                _num(row[cpu_busy]) if cpu_busy is not None else None,
                                _num(row[cpu_wait]) if cpu_wait is not None else None))
    presents.sort(key=lambda p: p.t_ms)
    return Trace(presents, clock, columns)


# ---------------------------------------------------------------- frames

class Frames:
    """Probe frames of one or more windows as sorted start/end times in milliseconds."""

    def __init__(self, starts_ms, wholes_ms):
        order = sorted(range(len(starts_ms)), key=starts_ms.__getitem__)
        self.order = order
        self.starts = [starts_ms[i] for i in order]
        self.wholes = [wholes_ms[i] for i in order]
        self.ends = [s + w for s, w in zip(self.starts, self.wholes)]


# ---------------------------------------------------------------- offset

def _anchors(intervals, k, floor):
    """Indices of the k largest intervals that are at least floor ms long."""
    top = heapq.nlargest(k, intervals)
    cut = max(top[-1], floor) if top else floor
    return [i for i, v in enumerate(intervals) if v >= cut][:4 * k]


def _similar(a, b):
    return abs(a - b) <= max(2.0, 0.1 * max(a, b))


def estimate_offset(frames, trace, hint_ms=None, anchors=12, scoring=64):
    """Offset d (ms) such that a present at t closes the frame ending near t + d.

    Candidate offsets pair the longest frame intervals with present-to-present intervals of
    similar length; each candidate is scored by how many of the longest present intervals land on a
    frame end with a similar interval. Returns (offset, source, score)."""
    times = trace.times
    if len(times) < 2 or not frames.ends:
        if hint_ms is None:
            raise TraceError('too few presents or frames to estimate the clock offset')
        return hint_ms, 'hint', 0
    gaps = [b - a for a, b in zip(times, times[1:])]  # gap i closes at present i+1
    # Only intervals well above the typical frame are distinctive enough to align two clocks.
    floor = DISTINCT_FACTOR * statistics.median(frames.wholes)
    frame_top = _anchors(frames.wholes, anchors, floor)
    present_top = _anchors(gaps, anchors, floor)
    scorers = _anchors(gaps, scoring, floor)
    ends, wholes = frames.ends, frames.wholes

    def score(d):
        hits, residuals = 0, []
        for i in scorers:
            t = times[i + 1] + d
            j = bisect.bisect_left(ends, t)
            best = min((k for k in (j - 1, j) if 0 <= k < len(ends)), key=lambda k: abs(ends[k] - t))
            tol = max(2.0, 0.25 * wholes[best])
            if abs(ends[best] - t) <= tol and _similar(wholes[best], gaps[i]):
                hits += 1
                residuals.append(ends[best] - t)
        return hits, residuals

    best = None
    for f in frame_top:
        for i in present_top:
            if not _similar(frames.wholes[f], gaps[i]):
                continue
            d = ends[f] - times[i + 1]
            hits, residuals = score(d)
            key = (hits, -abs(d - hint_ms) if hint_ms is not None else 0.0)
            if best is None or key > best[0]:
                best = (key, d, residuals)
    if best is None or best[0][0] < max(MIN_ANCHOR_HITS, len(scorers) // 2):
        if hint_ms is None:
            raise TraceError('no long intervals match between trace and probe frames; clock offset not established')
        return refine_offset(frames, trace, hint_ms), 'hint, refined', 0 if best is None else best[0][0]
    (hits, _), d, residuals = best
    return refine_offset(frames, trace, d + statistics.median(residuals)), 'estimated', hits


def refine_offset(frames, trace, offset_ms, shifts=REFINE_SHIFTS, sample=REFINE_SAMPLE):
    """Pick the quarter-frame shift of offset_ms whose joined present gaps agree best with the frame
    intervals, then centre the residual. A one-frame misalignment still joins nearly every frame, so
    agreement of intervals, not the join count, decides it."""
    n = min(len(frames.ends), sample)
    if n < 2:
        return offset_ms
    head = Frames(frames.starts[:n], frames.wholes[:n])
    step = statistics.median(head.wholes)
    times = trace.times
    best = None
    for k in range(-4 * shifts, 4 * shifts + 1):  # quarter-frame steps, so one lands near the true phase
        d = offset_ms + k * step / 4.0
        joined = join(head, trace, d)
        diffs = [min(abs(head.wholes[f] - (times[p] - times[p - 1])), 50.0) for f, p in enumerate(joined) if p and joined[f - 1] == p - 1]
        if len(diffs) < n // 2:
            continue
        score = math.fsum(diffs) / len(diffs)
        if best is None or score < best[0]:
            best = (score, d, joined)
    if best is None:
        return offset_ms
    _, d, joined = best
    residuals = [head.ends[f] - (times[p] + d) for f, p in enumerate(joined) if p is not None]
    return d + statistics.median(residuals)


# ---------------------------------------------------------------- join

def join(frames, trace, offset_ms):
    """Map each frame (in frames.order) to the present nearest its end, at most one each way.

    Returns a list, per original frame index, of a present index or None."""
    times, ends, wholes = trace.times, frames.ends, frames.wholes
    n = len(ends)
    result = [None] * n
    lo = bisect.bisect_left(times, (frames.starts[0] if n else 0.0) - offset_ms - 1000.0)
    hi = bisect.bisect_right(times, (ends[-1] if n else 0.0) - offset_ms + 1000.0)
    claimed = {}
    j = 0
    for p in range(lo, hi):
        t = times[p] + offset_ms
        while j + 1 < n and ends[j + 1] <= t:
            j += 1
        best = j
        if j + 1 < n and abs(ends[j + 1] - t) < abs(ends[j] - t):
            best = j + 1
        distance = abs(ends[best] - t)
        if distance > max(2.0, 0.5 * wholes[best]):
            continue
        prior = claimed.get(best)
        if prior is None or distance < prior[1]:
            claimed[best] = (p, distance)
    for k, (p, _) in claimed.items():
        result[frames.order[k]] = p
    return result


def bound_of(present, interval_ms):
    """'gpu' or 'cpu' when the trace gives GPU busy; otherwise 'unknown' (Decision 6)."""
    if present is None or present.gpu_busy_ms is None or not interval_ms:
        return UNKNOWN
    return 'gpu' if present.gpu_busy_ms >= GPU_BOUND_SHARE * interval_ms else 'cpu'


def displayed_interval(trace, p):
    """Interval in ms between displayed frames that closes at present p, or None if p was not shown."""
    present = trace.presents[p]
    if not present.displayed:
        return None
    if present.display_ms is not None:
        return present.display_ms
    # No display-change column: fall back to the gap since the previous displayed present.
    q = p - 1
    while q >= 0 and not trace.presents[q].displayed:
        q -= 1
    return None if q < 0 else present.t_ms - trace.presents[q].t_ms


def interval_source(trace):
    return 'display-change' if trace.has('display') else 'between-displayed-presents'
