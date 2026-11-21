import argparse
import json
import math
import sys
import time

from rigcommon import (FIXTURE, LOG, NATIVE_TOLERANCE, archive, auto, call, census, console, clock, clock_text, footprint, game_pid, go, host, idle,
                       loaded_identity, log_size, log_since, native_minute, now, optional, pause_at_native, probe_read, prop, rate,
                       require_lock, rss_kib, run, start_and_load, stop, route_leg_read, track_cost, type_read, wait_native, wrap, write)
from rigpaths import absolute

LABEL = 'retention'
CONTROL = 'retention-control'
FIELDS = ('ownedBodyCombiners', 'liveMergedMeshes', 'unmergedBodyCombiners', 'reservedBodies',
          'liveMergedVertices', 'bakeScratchBytes', 'monoHeapBytes', 'monoUsedBytes')
TYPES = ('Position[]', 'Double[]', 'Int64[]', 'RouteLeg', 'CitySimulator', 'RoadRoutes')
ROUTE_LEGS = ('in_current_journeys', 'outside_current_journeys', 'split_prefix_legs', 'travelling')
TARGETS = 'retention-targets'
MISSING_TARGETS = 'Retimed process did not export matched native clock targets'
MATCH_BOUND = .5
DRAIN_MINUTES = 30


def wait_until(deadline):
    while time.monotonic() < deadline:
        if wrap():
            raise RuntimeError('WRAP_UP_NOW')
        time.sleep(max(0, min(.10, deadline - time.monotonic())))


def last_population_summary():
    if not LOG.exists():
        return None
    return next((line for line in reversed(LOG.read_text(errors='replace').splitlines()) if 'Population: ' in line and ' citizens;' in line), None)


def snapshot(key):
    mark = 'retention-' + key
    pid = game_pid()
    if pid is None:
        raise RuntimeError(f'{mark}: run.json has no game pid')
    start = time.monotonic()
    auto(mark + '-autosave-before')
    try:
        minute = native_minute(mark + '-native')
        state = clock(mark)
        retained = census(mark + '-census')
        after_census = native_minute(mark + '-census-native')
        console(mark + '-movement', 'pc movement status')
        console(mark + '-population', 'pc population manifest')
        write(mark + '-last-population-summary', last_population_summary())
        heap = probe_read(mark + '-forced', 'heap')
        mono = probe_read(mark + '-mono', 'status')['mono_heap']
        rss = rss_kib(pid)
        gc = prop(mark + '-mesh', 'GcDiagnostics')
        food = prop(mark + '-rate', 'FoodDiagnostics')
        sample = {'utc': now(), 'start_monotonic': start, 'seconds': time.monotonic() - start,
                  'pid': pid, 'native_minute': minute, 'census_native_minute': after_census,
                  'clock': {k: state.get(k) for k in ('paused', 'day', 'time')}, 'native': food.get('native'), 'rate': food.get('value', {}).get('travelClockRate'),
                  'census': retained, 'heap': heap, 'mono': mono, 'rss_kib': rss, 'footprint': footprint(mark, pid),
                  'mesh': {f: gc.get('value', {}).get(f) for f in FIELDS}}
    finally:
        auto(mark + '-autosave-after')
    return write(mark + '-set', sample)


def immediate():
    auto('retention-F-immediate-autosave-before')
    try:
        retained = census('retention-F-immediate-census')
        food = prop('retention-F-immediate-rate', 'FoodDiagnostics')
        return {'census': retained, 'rate': food['value']['travelClockRate'], 'native': food.get('native')}
    finally:
        auto('retention-F-immediate-autosave-after')


def change(mark, speed, take_immediate=False):
    offset = log_size()
    ack = time.monotonic()
    result = rate('retention-' + mark, speed)
    deadline = ack + 120
    commit = road = report = None
    while time.monotonic() < deadline:
        if wrap():
            raise RuntimeError('WRAP_UP_NOW')
        lines = log_since(offset).splitlines()
        clocks = [line for line in lines if 'Clock retime:' in line]
        roads = [line for line in lines if 'Road snapshot started:' in line and 'reason=clock-rate' in line]
        if clocks and commit is None:
            commit = (time.monotonic(), clocks[0])
        if roads and road is None:
            road = (time.monotonic(), roads[0])
        if take_immediate and commit and report is None:
            report = optional('retention-F-immediate', immediate)
            write('retention-F-immediate-read', report)
            take_immediate = False
        if commit and road:
            write('retention-' + mark + '-markers', {'clock': commit[1], 'clock_seen_monotonic': commit[0],
                                                     'road': road[1], 'road_seen_monotonic': road[0], 'from_command_s': road[0] - ack,
                                                     'session': result.get('session'), 'utc': now()})
            return road[0]
        time.sleep(.06)
    raise RuntimeError(f'speed={speed}: Player.log missing retime completion after {mark}: commit={commit}, road={road}')


def readings_summary(rows):
    paths = {
        'census.bytes': lambda r: r['census'].get('bytes'),
        'census.objects': lambda r: r['census'].get('objects'),
        'census.city_bytes': lambda r: r['census'].get('city_bytes'),
        'census.diagnostic_bytes': lambda r: r['census'].get('diagnostic_bytes'),
        'census.diagnostic_objects': lambda r: r['census'].get('diagnostic_objects'),
        'census.city_objects': lambda r: r['census'].get('city_objects'),
        'forced live': lambda r: r['heap'].get('after_forced_gc_bytes'),
        'mono heap': lambda r: r['mono'].get('now_bytes'),
        'mono used': lambda r: r['mono'].get('now_used_bytes'),
        'RSS': lambda r: r.get('rss_kib'),
        'owned': lambda r: r['mesh']['ownedBodyCombiners'],
        'live': lambda r: r['mesh']['liveMergedMeshes'],
        'unmerged': lambda r: r['mesh']['unmergedBodyCombiners'],
        'live vertices': lambda r: r['mesh']['liveMergedVertices'],
        'bake scratch': lambda r: r['mesh']['bakeScratchBytes'],
        **{t: (lambda r, t=t: type_read(r['census'], t)) for t in TYPES},
        **{'RouteLeg ' + k.replace('_', ' '): (lambda r, k=k: route_leg_read(r['census'], k)) for k in ROUTE_LEGS},
    }
    return {name: [get(r) for r in rows] for name, get in paths.items()}


def still_climbing(series):
    return any(all(x is not None for x in v[1:]) and v[-1] > max(v[1:-1]) for key, v in series.items() if not key.startswith('RouteLeg'))


def checked_targets(checkpoints):
    minutes = [point.get('native_minute') if isinstance(point, dict) else None for point in checkpoints or []]
    valid = minutes and all(isinstance(m, (int, float)) and not isinstance(m, bool) and math.isfinite(m) for m in minutes)
    valid = valid and all(not p.get('paused') or p.get('time') == clock_text(p['native_minute']) for p in checkpoints)
    if not valid or any(b <= a for a, b in zip(minutes, minutes[1:])) or len({p.get('key') for p in checkpoints}) != len(checkpoints):
        raise RuntimeError(MISSING_TARGETS + ': ' + json.dumps(checkpoints, default=str))
    return checkpoints


def export_targets(args, keys, rows, complete):
    checkpoints = checked_targets([{'key': key, 'native_minute': row.get('native_minute'), 'census_native_minute': row.get('census_native_minute'),
                                    'paused': (row.get('clock') or {}).get('paused') is True,
                                    'day': (row.get('clock') or {}).get('day'), 'time': (row.get('clock') or {}).get('time')}
                                   for key, row in zip(keys, rows)])
    return write(TARGETS, {'format': 1, 'build_sha': args.build_sha, 'fixture': FIXTURE, 'start': args.start, 'complete': complete,
                           'checkpoints': checkpoints, 'series': readings_summary(rows)})


def difference(a, b):
    numbers = all(isinstance(x, (int, float)) and not isinstance(x, bool) for x in (a, b))
    return a - b if numbers else None


def matched_series(plan, rows, matches):
    control = readings_summary(rows)
    retimed = plan.get('series') or {}
    keep = [m['matched'] for m in matches]
    return {'keys': [point['key'] for point in plan['checkpoints'][:len(rows)]], 'bound': MATCH_BOUND, 'checkpoints': matches,
            'native_offsets': [difference(row.get('native_minute'), point['native_minute']) for row, point in zip(rows, plan['checkpoints'])],
            'unmatched': [m['key'] for m in matches if not m['matched']],
            'retimed_minus_control': {name: [difference(a, b) if ok else None for a, b, ok in zip(retimed.get(name, []), values, keep)]
                                      for name, values in control.items()}}


def paused_at(label, point):
    current = pause_at_native(label, point['native_minute'])
    return write(label + '-reached', {'utc': now(), 'target': point['native_minute'], 'native_minute': current,
                                      'offset': current - point['native_minute'], 'paused': True, 'tolerance': NATIVE_TOLERANCE})


def follow_targets(plan):
    rows, matches, running = [], [], False
    for point in checked_targets(plan.get('checkpoints')):
        label = CONTROL + '-' + point['key'] + '-wait'
        target, passed = point['native_minute'], False
        if point.get('paused'):
            paused_at(label, point)
            running = False
        else:
            before = native_minute(label + '-start')
            passed = before > target + NATIVE_TOLERANCE
            if not running:
                rate(label + '-run', 1)
                running = True
            wait_native(label, target)
        rows.append(snapshot('control-' + point['key']))
        row = rows[-1]
        offset, census_offset = row['native_minute'] - target, row['census_native_minute'] - target
        observed = (row.get('clock') or {}).get('paused') is True
        retimed_census = point.get('census_native_minute')
        census_gap = difference(row['census_native_minute'], retimed_census)
        census_gap = census_gap if census_gap is not None and math.isfinite(census_gap) else None
        if point.get('paused'):
            matched = observed and census_gap is not None and all(math.floor(m + NATIVE_TOLERANCE) == math.floor(target + NATIVE_TOLERANCE)
                                                                  for m in (row['native_minute'], row['census_native_minute'], retimed_census))
        else:
            matched = (not observed and -NATIVE_TOLERANCE <= offset and census_offset <= MATCH_BOUND
                       and census_gap is not None and abs(census_gap) <= MATCH_BOUND)
        matches.append({'key': point['key'], 'paused': bool(point.get('paused')), 'observed_paused': observed, 'passed_at_start': passed,
                        'offset': offset, 'census_offset': census_offset, 'census_gap': census_gap, 'matched': matched})
        write('retention-control-series', readings_summary(rows))
        write('retention-matched', matched_series(plan, rows, matches))
    return rows


def prepare(prefix, args, start):
    require_lock()
    if run(prefix + '-previous-status', 'status')['running']:
        archive(prefix + '-prior')
        stop(prefix + '-previous-stop')
    start_and_load(prefix + '-cold')
    loaded_identity(prefix, args.build_sha)
    go(prefix, start, 1)
    call(prefix + '-position-start', 'teleport', {'who': 'player', 'x': 215, 'z': 0})
    auto(prefix + '-autosave-pre')
    track_cost(prefix + '-cost-before')
    idle(prefix + '-idle-before')
    host(prefix + '-host-start')


def finish(prefix):
    auto(prefix + '-autosave-post')
    idle(prefix + '-idle-after')
    track_cost(prefix + '-cost-after')
    host(prefix + '-host-end')


def control(args):
    plan = json.loads(absolute(args.control).read_text())
    checked_targets(plan.get('checkpoints'))
    if plan.get('build_sha') != args.build_sha:
        raise RuntimeError(f"control targets are for build {plan.get('build_sha')}, not {args.build_sha}")
    if plan.get('fixture') != FIXTURE:
        raise RuntimeError(f"control targets are for fixture {plan.get('fixture')!r}, not {FIXTURE!r}")
    prepare(CONTROL, args, plan.get('start') or args.start)
    rows = follow_targets(plan)
    finish(CONTROL)
    return rows


def drain(rows):
    target = rows[-1]['native_minute'] + DRAIN_MINUTES
    current = native_minute('retention-drain-start')
    if current > target + NATIVE_TOLERANCE:
        raise RuntimeError(f'retention-drain: native clock {current} already passed the drain target {target}')
    current = pause_at_native(LABEL + '-drain', target)
    write('retention-drain-reached', {'utc': now(), 'target': target, 'native_minute': current, 'offset': current - target,
                                      'paused': True, 'tolerance': NATIVE_TOLERANCE})
    return snapshot('After30GameMinutes')


def main(argv=None):
    parser = argparse.ArgumentParser(description='Leak-check L: retained census across repeated clock retimes.')
    parser.add_argument('--build-sha', required=True)
    parser.add_argument('--start', default='15:30')
    parser.add_argument('--cycles', type=int, default=5)
    parser.add_argument('--control', help='retention-targets.json from the retimed process; runs the 1x control at its native minutes')
    args = parser.parse_args(argv)
    if args.control:
        return control(args)
    prepare(LABEL, args, args.start)
    rows, keys = [snapshot('R0')], ['R0']
    export_targets(args, keys, rows, False)
    for n in range(1, args.cycles + 1):
        change(f'cycle{n}-1to2', 2)
        road = change(f'cycle{n}-2to1', 1)
        wait_until(road + 30)
        write(f'retention-R{n}-settle', {'seconds_after_road': time.monotonic() - road, 'utc': now()})
        rows.append(snapshot(f'R{n}'))
        keys.append(f'R{n}')
        export_targets(args, keys, rows, False)
        series = readings_summary(rows)
        write('retention-series', series)
        print(f'R{n}', {k: v[-1] for k, v in series.items()}, flush=True)
        if n >= 3 and not still_climbing(series):
            break
    arrived = drain(rows)
    write('retention-arrival-series', readings_summary([rows[0], rows[-1], arrived]))
    export_targets(args, keys + ['After30GameMinutes'], rows + [arrived], True)
    finish(LABEL)
    return rows + [arrived]


if __name__ == '__main__':
    try:
        main(sys.argv[1:])
    except Exception as exc:
        write('retention-failure', {'error': str(exc)})
        raise
    finally:
        archive('retention')
