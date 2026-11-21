import argparse
import sys
import time

from rigcommon import (LOG, archive, auto, call, census, console, footprint, game_pid, go, host, idle, loaded_identity, log_size,
                       log_since, now, optional, probe_read, prop, rate, require_lock, rss_kib, run, start_and_load, stop,
                       route_leg_read, track_cost, type_read, wrap, write)

LABEL = 'retention'
FIELDS = ('ownedBodyCombiners', 'liveMergedMeshes', 'unmergedBodyCombiners', 'reservedBodies',
          'liveMergedVertices', 'bakeScratchBytes', 'monoHeapBytes', 'monoUsedBytes')
TYPES = ('Position[]', 'Double[]', 'Int64[]', 'RouteLeg', 'CitySimulator', 'RoadRoutes')
ROUTE_LEGS = ('in_current_journeys', 'outside_current_journeys', 'split_prefix_legs', 'travelling')


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
    start = time.monotonic()
    auto(mark + '-autosave-before')
    try:
        retained = census(mark + '-census')
        console(mark + '-movement', 'pc movement status')
        console(mark + '-population', 'pc population manifest')
        write(mark + '-last-population-summary', last_population_summary())
        heap = probe_read(mark + '-forced', 'heap')
        mono = probe_read(mark + '-mono', 'status')['mono_heap']
        pid = game_pid()
        rss = rss_kib(pid)
        gc = prop(mark + '-mesh', 'GcDiagnostics')
        food = prop(mark + '-rate', 'FoodDiagnostics')
        sample = {'utc': now(), 'start_monotonic': start, 'seconds': time.monotonic() - start,
                  'pid': pid, 'native': food.get('native'), 'rate': food.get('value', {}).get('travelClockRate'),
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


def main(argv=None):
    parser = argparse.ArgumentParser(description='Leak-check L: retained census across repeated clock retimes.')
    parser.add_argument('--build-sha', required=True)
    parser.add_argument('--start', default='15:30')
    parser.add_argument('--cycles', type=int, default=5)
    args = parser.parse_args(argv)
    require_lock()
    if run('retention-previous-status', 'status')['running']:
        archive('retention-prior')
        stop('retention-previous-stop')
    start_and_load('retention-cold')
    loaded_identity(LABEL, args.build_sha)
    go(LABEL, args.start, 1)
    call('retention-position-start', 'teleport', {'who': 'player', 'x': 215, 'z': 0})
    auto('retention-autosave-pre')
    track_cost('retention-cost-before')
    idle('retention-idle-before')
    host('retention-host-start')
    rows = [snapshot('R0')]
    for n in range(1, args.cycles + 1):
        change(f'cycle{n}-1to2', 2)
        road = change(f'cycle{n}-2to1', 1)
        wait_until(road + 30)
        write(f'retention-R{n}-settle', {'seconds_after_road': time.monotonic() - road, 'utc': now()})
        rows.append(snapshot(f'R{n}'))
        series = readings_summary(rows)
        write('retention-series', series)
        print(f'R{n}', {k: v[-1] for k, v in series.items()}, flush=True)
        if n >= 3 and not still_climbing(series):
            break
    clock = call('retention-arrival-clock', 'state')['result']['clock']
    h, m = map(int, clock['time'].split(':'))
    target = (h * 60 + m + 30) % 1440
    go(LABEL, f'{target // 60:02d}:{target % 60:02d}', 1)
    arrived = snapshot('After30GameMinutes')
    write('retention-arrival-series', readings_summary([rows[0], rows[-1], arrived]))
    auto('retention-autosave-post')
    idle('retention-idle-after')
    track_cost('retention-cost-after')
    host('retention-host-end')


if __name__ == '__main__':
    try:
        main(sys.argv[1:])
    except Exception as exc:
        write('retention-failure', {'error': str(exc)})
        raise
    finally:
        archive('retention')
