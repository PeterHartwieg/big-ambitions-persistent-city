import re

NUM = r'(-?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)'
INT = r'(-?\d+)'

SEGMENTS = [
    ('population', r'Population: ' + INT + r' citizens; ' + INT + r' travelling, ' + INT + r' awaiting a route, ' + INT
     + r' awaiting opening; clock ' + NUM + r', lag ' + NUM + r' game minutes\.',
     [('citizens', 'i'), ('travelling', 'i'), ('awaiting_route', 'i'), ('awaiting_opening', 'i'), ('clock_minute', 'f'), ('lag_minutes', 'f')]),
    ('advancement', r'Advancement this observation: ' + INT + r' frames, ' + INT + r' events, ' + NUM + r' ms total, ' + NUM
     + r' ms maximum advance, ' + INT + r' maximum events in one frame;',
     [('frames', 'i'), ('events', 'i'), ('total_ms', 'f'), ('max_advance_ms', 'f'), ('max_events', 'i')]),
    ('slowest_advance', r'Slowest advance: unit ' + NUM + r' ms \(([^)]*)\), optional-stop reads ' + NUM + r' ms, ' + INT
     + r' garbage collections completed during it\.',
     [('unit_ms', 'f'), ('unit_kind', 's'), ('optional_stop_ms', 'f'), ('gc_count', 'i')]),
    ('optional_stops', r'Optional-stop reads this observation \(refresh and advancement\): ' + INT + r' calls, ' + NUM + r' ms total, '
     + NUM + r' ms maximum, ' + INT + r' park trigger scans\.',
     [('calls', 'i'), ('total_ms', 'f'), ('max_ms', 'f'), ('park_trigger_scans', 'i')]),
    ('routes', r'Routes: ' + INT + r' road snapshots started \(' + INT + r' for gate changes\), ' + INT
     + r' frames waiting for routes; rebuild reasons \[([^\]]*)\]; slow routing frames ' + INT + r' \(first ' + INT
     + r' logged\); last rebuild (.*?); this planner scene searches ' + INT + r', maximum ' + NUM + r' ms, station captures ' + INT,
     [('road_snapshots', 'i'), ('gate_changes', 'i'), ('waiting_frames', 'i'), ('rebuild_reasons', 's'), ('slow_routing_frames', 'i'),
      ('routing_frame_logs', 'i'), ('last_rebuild', 's'), ('scene_searches', 'i'), ('max_scene_search_ms', 'f'), ('station_captures', 'i')]),
    ('discovery', r'discovery sweeps ' + INT + r', last branch captures ' + INT + r', split hints ' + INT + r', maximum slice ' + NUM
     + r' ms, last sweep ' + NUM + r' s / ' + NUM + r' ms work',
     [('sweeps', 'i'), ('last_branch_captures', 'i'), ('split_hints', 'i'), ('max_slice_ms', 'f'), ('last_sweep_s', 'f'), ('last_sweep_ms', 'f')]),
    ('checks', r'slowest RLO check ' + NUM + r' ms \(RLO contract bindings this session ' + INT + r', ' + NUM + r' ms\), door search '
     + NUM + r' ms, station search ' + NUM + r' ms, preparation frame ' + NUM + r' ms\.',
     [('max_rlo_check_ms', 'f'), ('rlo_bindings', 'i'), ('rlo_binding_ms', 'f'), ('max_door_search_ms', 'f'),
      ('max_station_search_ms', 'f'), ('max_preparation_frame_ms', 'f')]),
    ('closure', r'Closure refresh: ' + INT + r' calls, ' + NUM + r' ms total, ' + NUM + r' ms maximum\.',
     [('calls', 'i'), ('total_ms', 'f'), ('max_ms', 'f')]),
    ('planning', r'Route planning across frames: ' + INT + r' requests, ' + INT + r' slices, ' + INT + r' events completed, ' + INT
     + r' frames ended with a plan pending, ' + NUM + r' ms total, slowest slice ' + NUM + r' ms ending in (\S+) with ' + INT
     + r' NavMesh path queries; event restarts ' + INT,
     [('requests', 'i'), ('slices', 'i'), ('events_completed', 'i'), ('pending_frames', 'i'), ('total_ms', 'f'),
      ('max_slice_ms', 'f'), ('slowest_unit', 's'), ('slowest_unit_navmesh_queries', 'i'), ('event_restarts', 'i')]),
    ('planning_jobs', r'compatibility count ' + INT + r'\), road search restarts ' + INT + r', units ' + INT
     + r', query/unit admission deferrals ' + INT + r', path reserve ms ' + NUM + r'\.',
     [('inline_restarts', 'i'), ('road_search_restarts', 'i'), ('units', 'i'), ('admission_deferrals', 'i'), ('path_reserve_ms', 'f')]),
]
COMPILED = [(name, re.compile(pattern), fields) for name, pattern, fields in SEGMENTS]
PHYSICAL = re.compile(r'(physical=.*)$')


def _value(text, kind):
    if kind == 'i':
        return int(text)
    if kind == 'f':
        return float(text.replace(',', '.'))
    return text


def parse_physical(text):
    result = {}
    head, sep, admission = text.partition(', carAdmission=')
    for part in head.split(', '):
        key, eq, value = part.partition('=')
        if not eq:
            continue
        value = value.strip()
        if value in ('True', 'False'):
            result[key] = value == 'True'
        elif re.fullmatch(INT, value):
            result[key] = int(value)
        else:
            result[key] = value
    if sep:
        result['carAdmission'] = admission.rstrip()
    return result


def parse_line(line):
    if 'Population: ' not in line or 'Advancement this observation: ' not in line:
        return None
    summary = {}
    for name, pattern, fields in COMPILED:
        match = pattern.search(line)
        if match is None:
            summary[name] = None
            continue
        summary[name] = {field: _value(match.group(i + 1), kind) for i, (field, kind) in enumerate(fields)}
    if summary['population'] is None or summary['advancement'] is None:
        return None
    if summary['routes'] is not None:
        reasons = summary['routes']['rebuild_reasons']
        summary['routes']['rebuild_reasons'] = dict(
            (k, int(v)) for k, _, v in (item.partition('=') for item in reasons.split(', ') if item))
    physical = PHYSICAL.search(line)
    if physical:
        summary['physical'] = parse_physical(physical.group(1))
    elif 'Physical population inactive.' in line:
        summary['physical'] = {'physical': False, 'inactive': True}
    else:
        summary['physical'] = None
    return summary


def parse_lines(lines):
    return [parsed for parsed in (parse_line(line) for line in lines) if parsed is not None]


UNKNOWN = 'unknown'


def attach_fix128(spikes, observations, tolerance_ms):
    for spike in spikes:
        if spike.get('advance_ms') is None and spike.get('advance_source') != UNKNOWN:
            spike['advancement_unit'] = None
        else:
            spike['advancement_unit'] = dict(unit_kind=UNKNOWN, unit_ms=UNKNOWN, optional_stop_ms=UNKNOWN,
                                             gc_count=UNKNOWN, source=UNKNOWN)
    for index, observation in enumerate(observations):
        summary = observation['summary']
        slowest = summary.get('slowest_advance')
        if slowest is None:
            continue
        maximum = summary['advancement']['max_advance_ms']
        first, last = observation.get('first_frame'), observation.get('last_frame')
        candidates = [s for s in spikes if s.get('advance_ms') is not None
                      and abs(s['advance_ms'] - maximum) <= tolerance_ms
                      and (first is None or s['frame'] >= first) and (last is None or s['frame'] <= last)]
        if len(candidates) != 1:
            continue
        candidates[0]['advancement_unit'] = dict(unit_kind=slowest['unit_kind'], unit_ms=slowest['unit_ms'],
                                                 optional_stop_ms=slowest['optional_stop_ms'], gc_count=slowest['gc_count'],
                                                 source='summary-maximum', observation=index, observation_max_ms=maximum)
    return spikes
