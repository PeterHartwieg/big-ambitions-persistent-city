"""F4c presentation trace ingestion and QPC join (F4 plan, Decision 6).

Reads a PresentMon CSV into presents. The two schemas differ and are normalised separately:
- 1.x (TimeInSeconds or QPCTime, msBetweenDisplayChange, Dropped): the row time is the present, and
  msBetweenDisplayChange is the displayed interval ending at this row.
- 2.x (CPUStartQPC, CPUStartQPCTime or CPUStartTime in ms; FrameTime, CPUBusy, CPUWait, GPUBusy,
  DisplayLatency, DisplayedTime): the row time is the start of the frame's CPU work, the frame closes at
  CPUStart + FrameTime, and DisplayedTime is how long the frame stayed on screen, so the interval ending
  at a frame's display is the previous displayed frame's DisplayedTime.
Each present's time is the time that closes its frame. A field the trace lacks is None and is reported
`unknown`; a trace without display timing yields no displayed intervals.

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
# A shift replaces the supplied offset only when its mean interval disagreement is this much lower.
REFINE_MARGIN_MS, REFINE_MARGIN_FRACTION = 0.05, 0.2

QPC_COLUMNS = ('cpustartqpc', 'timeinqpc', 'qpctime', 'qpc')  # QPC ticks
QPC_MS_COLUMNS = ('cpustartqpctime',)  # QPC converted to ms (PresentMon 2.x --qpc_time_ms)
SECONDS_COLUMNS = ('timeinseconds',)  # PresentMon 1.x: seconds since recording start
TRACE_MS_COLUMNS = ('cpustarttime',)  # PresentMon 2.x default: ms since recording start
FRAME_START_COLUMNS = ('cpustartqpc', 'cpustartqpctime', 'cpustarttime', 'cpustartdatetime')
DISPLAY_COLUMNS = ('msbetweendisplaychange',)
PRESENTS_COLUMNS = ('msbetweenpresents',)
FRAME_TIME_COLUMNS = ('frametime',)
GPU_BUSY_COLUMNS = ('gpubusy', 'msgpubusy', 'msgpuactive')
CPU_BUSY_COLUMNS = ('cpubusy', 'mscpubusy')
CPU_WAIT_COLUMNS = ('cpuwait', 'mscpuwait')
DROPPED_COLUMNS = ('dropped',)
DISPLAYED_COLUMNS = ('msuntildisplayed',)
DISPLAYED_TIME_COLUMNS = ('displayedtime',)
DISPLAY_LATENCY_COLUMNS = ('displaylatency',)
NOT_A_NUMBER = ('', 'na', 'n/a', 'nan', '-')


class TraceError(Exception):
    """The trace cannot be read or joined."""


class Present:
    """One trace row. t_ms is the time that closes its frame: the present for PresentMon 1.x, the start of the
    next frame's CPU work (CPUStart + FrameTime) for 2.x, whose rows start at CPUStart. display_ms is the
    displayed interval that ends when this frame is shown, None if it was not shown or the trace lacks it."""
    __slots__ = ('t_ms', 'displayed', 'display_ms', 'between_ms', 'gpu_busy_ms', 'cpu_busy_ms', 'cpu_wait_ms', 'start_ms')

    def __init__(self, t_ms, displayed=True, display_ms=None, between_ms=None, gpu_busy_ms=None, cpu_busy_ms=None, cpu_wait_ms=None, start_ms=None):
        self.t_ms, self.displayed, self.display_ms, self.between_ms = t_ms, displayed, display_ms, between_ms
        self.gpu_busy_ms, self.cpu_busy_ms, self.cpu_wait_ms, self.start_ms = gpu_busy_ms, cpu_busy_ms, cpu_wait_ms, start_ms


class Trace:
    def __init__(self, presents, clock, columns, schema='1.x', display_source=None):
        self.presents = presents
        self.clock = clock  # 'qpc' or 'trace-start'
        self.columns = columns  # which optional columns the trace had
        self.schema = schema  # '1.x' (present times) or '2.x' (frame-start times)
        self.display_source = display_source  # column the displayed intervals come from, None if the trace has none
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
    """Parse a presentation CSV. QPC tick columns need qpc_frequency (ticks per second)."""
    with open(path, newline='', encoding='utf-8-sig') as handle:
        return parse_rows(csv.reader(handle), qpc_frequency, process_id, application)


def _clock(index, qpc_frequency):
    """(clock, time column, ms per unit). All PresentMon 2.x time values are milliseconds; QPC columns are ticks."""
    if 'cpustartdatetime' in index and _pick(index, QPC_COLUMNS + QPC_MS_COLUMNS + TRACE_MS_COLUMNS) is None:
        raise TraceError('trace has CPUStartDateTime only; record with --qpc_time or --qpc_time_ms')
    col = _pick(index, QPC_COLUMNS)
    if col is not None:
        if not qpc_frequency:
            raise TraceError('trace has QPC times but no QPC frequency')
        return 'qpc', col, 1000.0 / qpc_frequency
    col = _pick(index, QPC_MS_COLUMNS)
    if col is not None:
        return 'qpc', col, 1.0
    col = _pick(index, TRACE_MS_COLUMNS)
    if col is not None:
        return 'trace-start', col, 1.0
    col = _pick(index, SECONDS_COLUMNS)
    if col is not None:
        return 'trace-start', col, 1000.0
    raise TraceError('trace has no time column (' + ', '.join(QPC_COLUMNS + QPC_MS_COLUMNS + TRACE_MS_COLUMNS + SECONDS_COLUMNS) + ')')


def parse_rows(rows, qpc_frequency=None, process_id=None, application=None):
    rows = iter(rows)
    try:
        header = next(rows)
    except StopIteration:
        raise TraceError('empty trace') from None
    index = {name.strip().lower(): i for i, name in enumerate(header)}
    clock, time_col, scale = _clock(index, qpc_frequency)
    schema = '2.x' if any(n in index for n in FRAME_START_COLUMNS) else '1.x'
    columns = dict(display=_pick(index, DISPLAY_COLUMNS), between=_pick(index, PRESENTS_COLUMNS), gpu_busy=_pick(index, GPU_BUSY_COLUMNS),
                   cpu_busy=_pick(index, CPU_BUSY_COLUMNS), cpu_wait=_pick(index, CPU_WAIT_COLUMNS), dropped=_pick(index, DROPPED_COLUMNS),
                   displayed=_pick(index, DISPLAYED_COLUMNS), frame_time=_pick(index, FRAME_TIME_COLUMNS),
                   displayed_time=_pick(index, DISPLAYED_TIME_COLUMNS), display_latency=_pick(index, DISPLAY_LATENCY_COLUMNS))
    pid_col, app_col = index.get('processid'), index.get('application')

    def value(row, key):
        col = columns[key]
        return _num(row[col]) if col is not None and col < len(row) else None

    raw = []
    for row in rows:
        if not row:
            continue
        if process_id is not None and pid_col is not None and row[pid_col].strip() != str(process_id):
            continue
        if application is not None and app_col is not None and row[app_col].strip().lower() != application.lower():
            continue
        t = _num(row[time_col])
        if t is not None:
            raw.append((t * scale, row))
    raw.sort(key=lambda item: item[0])
    if schema == '2.x':
        presents, source = _frame_start_presents(raw, columns, value)
    else:
        presents, source = _present_time_presents(raw, columns, value)
    presents.sort(key=lambda p: p.t_ms)
    columns['display'] = source
    return Trace(presents, clock, columns, schema, source)


def _present_time_presents(raw, columns, value):
    """PresentMon 1.x: the row time is the present; msBetweenDisplayChange ends at this row's display."""
    presents = []
    for t, row in raw:
        display_ms = value(row, 'display')
        if columns['dropped'] is not None:
            shown = value(row, 'dropped') in (None, 0.0)
        elif columns['displayed'] is not None:
            shown = value(row, 'displayed') is not None
        elif columns['display'] is not None:
            shown = display_ms is not None
        else:
            shown = True
        if display_ms is not None and display_ms <= 0:
            display_ms = None  # PresentMon 1.x writes 0 for the first displayed frame
        presents.append(Present(t, shown, display_ms if shown else None, value(row, 'between'), value(row, 'gpu_busy'),
                                value(row, 'cpu_busy'), value(row, 'cpu_wait')))
    return presents, 'msBetweenDisplayChange' if columns['display'] is not None else None


def _frame_start_presents(raw, columns, value):
    """PresentMon 2.x: the row time is the start of the frame's CPU work and the frame lasts FrameTime, until the
    next frame starts. DisplayedTime is how long this frame stayed on screen, so the interval that ends at a
    frame's display is the previous displayed frame's DisplayedTime; DisplayLatency gives each display time."""
    has_shown = columns['displayed_time'] is not None or columns['display_latency'] is not None
    rows = []
    for k, (start, row) in enumerate(raw):
        frame_time = value(row, 'frame_time')
        close = start + frame_time if frame_time is not None else (raw[k + 1][0] if k + 1 < len(raw) else None)
        if close is None:
            continue
        shown_for, latency = value(row, 'displayed_time'), value(row, 'display_latency')
        if columns['displayed_time'] is not None:
            shown = shown_for is not None
        elif columns['display_latency'] is not None:
            shown = latency is not None
        else:
            shown = True
        rows.append((start, close, shown, shown_for, None if latency is None else start + latency, row))
    presents, previous = [], None
    for start, close, shown, shown_for, display_at, row in rows:
        display_ms = None
        if shown and previous is not None:
            _, p_shown_for, p_display_at = previous
            by_latency = display_at - p_display_at if display_at is not None and p_display_at is not None else None
            if p_shown_for is not None and (by_latency is None or abs(p_shown_for - by_latency) <= max(2.0, 0.1 * by_latency)):
                display_ms = p_shown_for
            elif by_latency is not None:
                display_ms = by_latency  # a row between the two displays is missing from the trace
        if display_ms is not None and display_ms <= 0:
            display_ms = None
        presents.append(Present(close, shown, display_ms, value(row, 'between'), value(row, 'gpu_busy'), value(row, 'cpu_busy'),
                                value(row, 'cpu_wait'), start))
        if shown:
            previous = (start, shown_for, display_at)
    if columns['displayed_time'] is not None:
        source = 'DisplayedTime'
    elif columns['display_latency'] is not None:
        source = 'DisplayLatency'
    elif columns['display'] is not None:
        source = 'MsBetweenDisplayChange'
    else:
        source = None
    if not has_shown and columns['display'] is not None:
        # Older 2.x layouts: MsBetweenDisplayChange ends at this row's display, as in 1.x.
        for p, (_, _, _, _, _, row) in zip(presents, rows):
            d = value(row, 'display')
            p.displayed = d is not None
            p.display_ms = d if d is not None and d > 0 else None
    return presents, source


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
    # Quarter-frame steps, so one lands near the true phase. The supplied offset is tried first and kept unless
    # a shift agrees clearly better: on a flat cadence every whole-frame shift scores the same.
    for k in sorted(range(-4 * shifts, 4 * shifts + 1), key=abs):
        d = offset_ms + k * step / 4.0
        joined = join(head, trace, d)
        diffs = [min(abs(head.wholes[f] - (times[p] - times[p - 1])), 50.0) for f, p in enumerate(joined) if p and joined[f - 1] == p - 1]
        if len(diffs) < n // 2:
            continue
        score = math.fsum(diffs) / len(diffs)
        if best is None or score < best[0] - max(REFINE_MARGIN_MS, REFINE_MARGIN_FRACTION * best[0]):
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
    """Displayed interval in ms that ends when present p is shown, or None if p was not shown or the trace has no
    display timing. Present or CPU-start gaps are never substituted for it."""
    present = trace.presents[p]
    return present.display_ms if present.displayed else None


def interval_source(trace):
    return trace.display_source or UNKNOWN
