#!/usr/bin/env python3
"""Owned continuity campaign session; always restore the isolated Mac rig on exit.

Accept bounded JSON commands on stdin so observations can guide the next camera
or player position without keeping a game running after the controller exits.
"""
import argparse
import json
import math
from pathlib import Path
import sys
import time

repo = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(repo / 'tools/neighborhood-economy'))
from mac_rig import MacRig, NativeProbeError, agent

parser = argparse.ArgumentParser(__doc__)
parser.add_argument('campaign')
args = parser.parse_args()
run = MacRig(args.campaign, physical=True, mode='windowed')

def brief(result):
    if isinstance(result, dict) and 'decisions' in result and isinstance(result.get('status'), dict):
        return {'decisions': result['decisions'], 'status': brief(result['status'])}
    if isinstance(result, dict) and 'diagnosticSelectedItinerary' in result:
        return {k: v for k, v in result.items() if k not in ('before', 'after', 'route', 'preflightQuote', 'freshQuote')}
    if isinstance(result, dict) and ('furniture' in result or isinstance(result.get('status'), dict)
                                     and 'furniture' in result['status']):
        status = result.get('status', result)
        summary = {k: v for k, v in status.items() if k not in ('furniture', 'schedules', 'nativeEquipmentCaps')}
        summary['furnitureCount'] = len(status['furniture'])
        return ({k: v for k, v in result.items() if k != 'status'} | {'status': summary}
                if 'status' in result else summary)
    if isinstance(result, dict) and 'completedQueries' in result:
        return {k: v for k, v in result.items() if k != 'rows'} | {
            'rows': [{k: v for k, v in row.items() if k not in ('route', 'actualSubwayEvidence')}
                     for row in result.get('rows', [])]}
    if isinstance(result, dict) and 'bodies' in result:
        moving = [r for r in result['residents'] if r.get('sample', {}).get('mode') == 'Car'
                  and not r['sample']['stationary']]
        return {k: result[k] for k in ('stateMinute', 'nativeMinute', 'runtimeFailed', 'physicalFailed',
                'physicalDiagnostics', 'focus', 'hiddenTravel', 'camera')} | {
            'moving': [{'id': r['id'], 'position': r['logicalPosition']} for r in moving[:12]],
            'bodies': result['bodies']}
    if isinstance(result, dict) and ('citizens' in result or 'routines' in result):
        return {k: v for k, v in result.items() if k in ('minute', 'nativeMinute', 'caughtUp', 'citizens', 'scope', 'routines')}
    return result


def health_minute(health):
    day, clock = health.get('day'), health.get('time')
    if type(day) is not int or day < 0 or not isinstance(clock, str):
        raise RuntimeError('Native health has no valid published clock')
    parts = clock.split(':')
    if len(parts) != 2 or not all(p.isdigit() for p in parts):
        raise RuntimeError('Invalid native published clock')
    hour, minute = map(int, parts)
    if not 0 <= hour < 24 or not 0 <= minute < 60:
        raise RuntimeError('Invalid native published clock')
    return day * 1440 + hour * 60 + minute


def skip_days(request, label):
    days, name = request.get('days'), request.get('name')
    if type(days) is not int or not 1 <= days <= 3:
        raise ValueError('Skip days must be an integer from1 through3')
    if not isinstance(name, str) or not name.startswith('FT Food Routines '):
        raise ValueError('Exact named disposable food-routine city required')

    def checked_health():
        health = agent.health()
        if health.get('scene') != 'city' or health.get('save') != name:
            raise RuntimeError('The loaded city differs from the requested disposable city')
        if (health.get('load') or {}).get('status') == 'failed':
            raise RuntimeError('Native city load failed')
        health_minute(health)
        if type(health.get('frame')) is not int:
            raise RuntimeError('Native health has no frame number')
        return health

    before = checked_health()
    if before.get('time_skip') is not False:
        raise RuntimeError('A native time skip is running or its state is unknown')
    initial = run.ready(population=True)
    before = checked_health()
    if before.get('time_skip') is not False:
        raise RuntimeError('Native time-skip state changed during preparation')
    start_minute = float(initial['nativeMinute'])
    if not math.isfinite(start_minute) or start_minute < 0 or initial.get('caughtUp') is not True:
        raise RuntimeError('Population was not ready at skip start')
    target = start_minute + days * 1440
    run.record(label + '-start', {'save': name, 'days': days, 'nativeMinute': start_minute,
               'targetNativeMinute': target, 'health': before})
    started = time.monotonic()
    run.call(label + '-console', 'console', {'line': f'skiptime {days}d true'})
    acknowledged = checked_health()['frame']
    deadline = started + 1800
    while True:
        health = checked_health()
        # Console acknowledgement can precede the coroutine AND its health snapshot.
        # A fresh frame alone is insufficient: require the target native day/time.
        if (health['frame'] > acknowledged and health_minute(health) >= math.floor(target)
                and health.get('time_skip') is False):
            native_seconds = time.monotonic() - started
            run.record(label + '-native-ready', {'wallSeconds': native_seconds, 'health': health})
            break
        if time.monotonic() >= deadline:
            raise RuntimeError('Native skip did not publish its target time with time_skip=false: ' + str(health))
        time.sleep(.5)
    final = run.ready(population=True)
    final_health = checked_health()
    final_minute = float(final['nativeMinute'])
    if (final.get('caughtUp') is not True or not math.isfinite(final_minute) or final_minute < target - .1
            or final_health['frame'] < health['frame'] or final_health.get('time_skip') is not False
            or health_minute(final_health) < math.floor(target)):
        raise RuntimeError('Native/population readiness did not retain the requested skip target')
    timing = {'days': days, 'startNativeMinute': start_minute, 'targetNativeMinute': target,
              'finalNativeMinute': final['nativeMinute'], 'nativeReadyWallSeconds': native_seconds,
              'populationReadyWallSeconds': time.monotonic() - started,
              'definition': 'Ordinary skiptime command dispatch through fresh published target time with time_skip=false, then caught-up population. Excludes initial readiness; native completion polling resolution0.5seconds.'}
    run.record(label + '-timing', timing)
    run.record(label + '-final', final)
    return {'timing': timing, 'finalStatus': brief(final)}

try:
    run.stage('owned-campaign-start'); run.begin()
    print(json.dumps({'ready': True}), flush=True)
    for line in sys.stdin:
        request = json.loads(line)
        label = request.get('label', request.get('op', request['action']))
        if not isinstance(label, str) or not label.replace('-', '').replace('_', '').isalnum():
            raise ValueError('Invalid evidence label')
        action = request['action']
        if action == 'close':
            break
        if action == 'load':
            name = request['name']
            if not name.startswith('FT Food Routines '):
                raise ValueError('Named disposable food-routine city required')
            started = time.monotonic()
            result = run.load(label, name, population=request.get('population', True))
            run.record(label + '-timing', {'wallSeconds': time.monotonic() - started,
                       'definition': 'Bridge load request through native scene and requested population readiness; excludes app launch and rig preparation.'})
            run.record(label + '-ready', result)
        elif action == 'skip':
            result = skip_days(request, label)
        elif action == 'probe':
            try:
                result = run.record(label, run.probe(request['op'], **request.get('arguments', {})))
            except NativeProbeError as error:
                result = run.record(label + '-refused', {'ok': False, 'error': str(error)})
        elif action == 'call':
            if request['command'] not in ('time', 'state', 'teleport', 'camera', 'shoot', 'save', 'console', 'use', 'walk'):
                raise ValueError('Unsupported continuity command')
            try:
                result = run.call(label, request['command'], request.get('arguments', {}))
            except Exception as error:
                # A bridge validation refusal (for example non-walkable observer ground)
                # leaves the owned rig available for correction. Transport failures still
                # unwind through finally and restore the rig.
                if not getattr(error, 'remote', False):
                    raise
                result = run.record(label + '-refused', {'ok': False, 'error': str(error)})
        elif action == 'focus':
            result = run.focus(label)
        else:
            raise ValueError('Unknown action')
        print(json.dumps({'label': label, 'result': brief(result)}, default=str), flush=True)
except BaseException as error:
    run.record('error', {'type': type(error).__name__, 'message': str(error)[:4000]})
    raise
finally:
    run.close()
