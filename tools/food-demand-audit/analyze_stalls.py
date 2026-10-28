#!/usr/bin/env python3
"""Inspect raw helper wall intervals and neighboring mod frames; never infer GPU frame ownership."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path


def number(row, key, available=None):
    if available is not None and row.get(available) not in ('1', 'true', 'True'):
        return None
    raw = row.get(key, '')
    if raw == '':
        return None
    value = float(raw)
    if not math.isfinite(value):
        raise ValueError('Nonfinite ' + key)
    return value


def analyze(observations, mod_frames, frequency, threshold=50.0, limit=30, context=2):
    if frequency <= 0 or not math.isfinite(frequency):
        raise ValueError('Positive monotonic clock frequency required')
    if not 0 <= context <= 10 or not 1 <= limit <= 1000 or threshold <= 0 or not math.isfinite(threshold):
        raise ValueError('Invalid context, limit or threshold')
    mods = {}
    for row in mod_frames:
        frame = int(row['unityFrame'])
        if frame in mods:
            raise ValueError('Duplicate completed mod frame')
        mods[frame] = row
    intervals, seen = [], set()
    previous_end = None
    previous_frame = None
    gaps = 0
    for row in observations:
        start, end = int(row['startTicks']), int(row['endTicks'])
        prior, current = int(row['priorUnityFrame']), int(row['currentUnityFrame'])
        if end < start or (previous_end is not None and start < previous_end):
            raise ValueError('Overlapping or reversed observation interval')
        if current in seen or current <= prior or (previous_frame is not None and current <= previous_frame):
            raise ValueError('Duplicate or reversed observation frame')
        seen.add(current)
        discontinuous = previous_end is not None and (start != previous_end or prior != previous_frame)
        gap = current - prior != 1
        gaps += int(gap or discontinuous)
        previous_end, previous_frame = end, current
        elapsed = (end - start) * 1000.0 / frequency
        intervals.append((elapsed, row, gap, discontinuous))
    slow = sorted((x for x in intervals if x[0] > threshold), key=lambda x: x[0], reverse=True)
    events = []
    for elapsed, row, gap, discontinuous in slow[:limit]:
        prior, current = int(row['priorUnityFrame']), int(row['currentUnityFrame'])
        requested = sorted(set(range(prior-context, prior+context+1)) | set(range(current-context, current+context+1)))
        neighbors = []
        for frame in requested:
            if frame not in mods:
                continue
            m = mods[frame]
            neighbors.append({'unityFrame': frame,
                'combinedUnionMs': number(m, 'combinedUnionMs'),
                'populationMs': number(m, 'populationMs'), 'authorityMs': number(m, 'authorityMs'),
                'inclusiveComponents': {k: number(m, k) for k in m if k.endswith('InclusiveMs')},
                'slowestEvent': None if not m.get('eventSlices') else {
                    'citizen': m.get('slowestCitizen'), 'entryBranch': m.get('entryBranch'),
                    **{k: number(m, k) for k in ('eventMinute', 'eventSlices', 'planCalls', 'eventMs',
                        'walkingCalculationMs', 'roadSearchSliceMs', 'navMeshPathMs', 'aStarAdvanceMs', 'eventFailures', 'eventSlicesDropped')}},
                'managedDelta': {'bytes': number(m, 'managedDeltaBytes', 'managedDeltaAvailable'),
                    'kind': m.get('managedCounterKind', 'unavailable'), 'scope': 'between-first-work-observations'},
                'observedBounds': {k: int(m[k]) if m.get(k) else None for k in ('firstObservedTicks', 'closedAtTicks', 'nextObservedUnityFrame')}})
        events.append({'wallIntervalMs': elapsed, 'startTicks': int(row['startTicks']), 'endTicks': int(row['endTicks']),
            'priorUnityFrame': prior, 'currentUnityFrame': current, 'frameGap': gap, 'intervalDiscontinuity': discontinuous,
            'gcDeltas': [number(row, 'gc'+str(i)+'Delta', 'gcAvailable') for i in range(3)],
            'allocationBytesObserved': number(row, 'allocationBytes', 'allocationAvailable'),
            'latestTimingObservation': {'cpuMs': number(row, 'cpuLatestMs', 'cpuAvailable'),
                'gpuMs': number(row, 'gpuLatestMs', 'gpuAvailable'),
                'sourceTimestamp': row.get('timingSourceTimestamp') if row.get('timingTimestampAvailable') in ('1','true','True') else None,
                'repeated': row.get('timingRepeated') in ('1','true','True'), 'frameOwnership': 'unknown'},
            'nativeMinute': number(row, 'nativeMinute', 'nativeMinuteAvailable'),
            'timeScale': number(row, 'timeScale'), 'paused': row.get('paused'),
            'neighborModFrames': neighbors, 'missingNeighborModFrames': [f for f in requested if f not in mods]})
    return {'retainedObservations': len(intervals), 'thresholdMs': threshold, 'intervalsOverThreshold': len(slow),
        'maxRetainedWallIntervalMs': max((i[0] for i in intervals), default=None), 'gappedOrDiscontinuousIntervals': gaps,
        'eventsEmitted': len(events), 'eventsOmittedByDisplayLimit': max(0, len(slow)-limit), 'events': events,
        'limitations': ['Input CSVs must come from the same capture; identical frame IDs alone do not establish this.',
            'Wall intervals span successive Probe.Update observations, not exact render/presentation boundaries.',
            'Neighbor mod frames are context, not exact attribution; never subtract their inclusive or nested times from this interval.',
            'CPU/GPU values are latest asynchronously observed samples with unknown frame ownership. Zero is distinct from unavailable.',
            'A GC count change shows a collection occurred, not its pause duration or who allocated the memory.',
            'Inspect the capture report for capacity drops, lifecycle termination, report contamination and partial first/last frames. Retained CSV rows alone cannot prove capture completeness.']}


def analyze_capture(observations, mod_frames, report, observation_path, mod_path, threshold=50.0, limit=30, context=2):
    def leaf(value):
        return str(value).replace(chr(92), '/').rsplit('/', 1)[-1]
    expected_observation = leaf(report.get('observationEvidence', ''))
    expected_mod = leaf(report.get('perFrameEvidence', ''))
    if not expected_observation.startswith('frames-observations-') or not expected_mod.startswith('frames-mod-work-'):
        raise ValueError('Report must identify both exported CSVs')
    if expected_observation.removeprefix('frames-observations-') != expected_mod.removeprefix('frames-mod-work-'):
        raise ValueError('Report CSV request IDs differ')
    if expected_observation != leaf(observation_path) or expected_mod != leaf(mod_path):
        raise ValueError('CSV names do not match this capture report')
    raw = report.get('rawObservations') or {}
    if raw.get('Count') != len(observations) or report.get('completedUnityFrames') != len(mod_frames):
        raise ValueError('CSV counts do not match this capture report')
    frequency = float(raw['stopwatchFrequency'])
    result = analyze(observations, mod_frames, frequency, threshold, limit, context)
    dispatches = report.get('diagnosticDispatches', [])
    report_frames = report.get('activeReportUnityFrames', [])
    first_start = int(observations[0]['startTicks']) if observations else None
    stop_ticks = report.get('captureStoppedTicks')
    for event in result['events']:
        prior, current = event['priorUnityFrame'], event['currentUnityFrame']
        event['partialStart'] = bool(raw.get('initialIntervalPartial') and event['startTicks'] == first_start)
        event['diagnosticDispatches'] = [d for d in dispatches if prior <= d['unityFrame'] < current]
        event['activeReportFrames'] = [f for f in report_frames if prior <= f < current]
        event['observerContaminated'] = bool(event['partialStart'] or event['diagnosticDispatches'] or event['activeReportFrames'])
        event['observerContaminationUnknown'] = bool(
            (report.get('diagnosticDispatchDropped', 0) or 0) > 0
            and (not dispatches or event['endTicks'] >= dispatches[-1]['ticks'])
            or (report.get('activeReportIdsDropped', 0) or 0) > 0
            and (not report_frames or current >= report_frames[-1]))
        event['neighborFramesBeyondCaptureStop'] = [n['unityFrame'] for n in event['neighborModFrames']
            if stop_ticks is not None and n['observedBounds']['closedAtTicks'] is not None and n['observedBounds']['closedAtTicks'] > stop_ticks]
        event['latestTimingObservation']['zeroMayBeUnmeasured'] = event['latestTimingObservation']['cpuMs'] == 0 or event['latestTimingObservation']['gpuMs'] == 0
    whole_count = report.get({50.0:'framesOver50ms',100.0:'framesOver100ms',250.0:'framesOver250ms'}.get(threshold, ''))
    result['capture'] = {'stopReason': report.get('stopReason'), 'samplerError': report.get('sampleError'),
        'rawRowsDropped': raw.get('Dropped'), 'rawCapacity': raw.get('Capacity'),
        'initialIntervalPartial': raw.get('initialIntervalPartial'), 'outstandingPrimaryScopes': raw.get('outstandingPrimaryScopes'),
        'discardedLifecycleTailTicks': raw.get('discardedLifecycleTailTicks'),
        'diagnosticDispatchCount': report.get('diagnosticDispatchCount'), 'diagnosticDispatchDropped': report.get('diagnosticDispatchDropped'),
        'activeReportCount': report.get('activeReportCount'), 'activeReportIdsDropped': report.get('activeReportIdsDropped'),
        'wholeCaptureIntervalsOverThreshold': whole_count,
        'wholeCaptureMinusRetainedSlowIntervals': None if whole_count is None else whole_count-result['intervalsOverThreshold'],
        'reportAndCsvNamesAndCountsMatch': True, 'csvContentCryptographicallyBoundByReport': False}
    result['timestampFrequency'] = frequency
    result['limitations'].append('Names and counts bind selection to the stop report, but the report contains no CSV content hashes; input hashes are recorded by this analyzer.')
    result['limitations'].append('Zero CPU/GPU means returned zero; it may represent an unavailable measurement rather than a measured zero-duration frame.')
    return result


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--observations', type=Path, required=True)
    parser.add_argument('--mod-frames', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True, help='frames-stop JSON from this capture')
    parser.add_argument('--threshold', type=float, default=50)
    parser.add_argument('--limit', type=int, default=30)
    parser.add_argument('--context', type=int, default=2)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    with args.observations.open(newline='') as a, args.mod_frames.open(newline='') as b:
        result = analyze_capture(list(csv.DictReader(a)), list(csv.DictReader(b)), json.loads(args.report.read_text()), args.observations, args.mod_frames, args.threshold, args.limit, args.context)
    result['inputs'] = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in (args.observations, args.mod_frames, args.report)}
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(str(result['intervalsOverThreshold']) + ' slow retained intervals; ' + str(result['eventsEmitted']) + ' emitted with neighboring mod context')


if __name__ == '__main__':
    main()
