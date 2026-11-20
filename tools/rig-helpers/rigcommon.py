import datetime
import json
import os
import subprocess
import time
from pathlib import Path

from rigpaths import absolute

HOME = absolute('~')
RUN = os.environ.get('PC_RIG_RUN', '')
STAGE = absolute(os.environ.get('PC_RIG_STAGE') or ('~/Coding_Projects/.agent-logs/' + RUN))
RIG = absolute(os.environ.get('PC_RIG_CHECKOUT') or '~/Coding_Projects/big-ambitions/big-ambitions-agent-rig')
SUPPORT = absolute(os.environ.get('PC_RIG_SUPPORT') or '~/Library/Application Support/BigAmbitionsAgentTestsMac')
LOG = absolute(os.environ.get('PC_RIG_PLAYER_LOG') or (SUPPORT / 'Player-agent.log'))
FIXTURE = os.environ.get('PC_RIG_FIXTURE', '')
CENSUS = '[Persistent City] gc retained '


def require_lock():
    if not RUN or os.environ.get('BA_LOCK_OWNER') != RUN:
        raise RuntimeError(f'BA_LOCK_OWNER must equal PC_RIG_RUN ({RUN!r})')


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def write(label, data):
    STAGE.mkdir(parents=True, exist_ok=True)
    (STAGE / (label + '.json')).write_text(json.dumps(data, indent=2, default=str) + '\n')
    return data


def bridge(args):
    return subprocess.run(['python3', str(RIG / 'scripts/mac-agent'), *args], cwd=RIG, text=True, capture_output=True)


def run(label, *args, allow=False):
    require_lock()
    print(label, now(), flush=True)
    result = bridge(list(args))
    STAGE.mkdir(parents=True, exist_ok=True)
    (STAGE / (label + '.stdout')).write_text(result.stdout)
    (STAGE / (label + '.stderr')).write_text(result.stderr)
    try:
        data = json.loads(result.stdout)
    except ValueError:
        try:
            data = json.loads(result.stderr)
        except ValueError:
            data = {'stdout': result.stdout, 'stderr': result.stderr}
    write(label, {'utc': now(), 'command': ['mac-agent', *args], 'exit': result.returncode, 'result': data})
    if result.returncode and not allow:
        raise RuntimeError(f'{label}: exit {result.returncode}: {data} {result.stderr}')
    return data


def op(label, kind, args, allow=False):
    return run(label, 'op', kind, json.dumps(args), allow=allow)


def call(label, kind, args=None, allow=False):
    return run(label, 'call', kind, json.dumps(args or {}), allow=allow)


def console(label, line):
    return call(label, 'console', {'line': line})


def auto(label):
    return run(label, 'autosave')


def prop(label, name):
    return op(label, 'pcstate', {'action': 'read', 'property': name})


def wrap():
    return any(p.exists() for p in (STAGE / 'WRAP_UP_NOW.md', HOME / 'Coding_Projects/.agent-logs/WRAP_UP_NOW.md'))


def archive(label):
    if LOG.exists():
        STAGE.mkdir(parents=True, exist_ok=True)
        (STAGE / (label + '-Player.log')).write_bytes(LOG.read_bytes())


def log_size():
    return LOG.stat().st_size if LOG.exists() else 0


def log_since(offset):
    if not LOG.exists():
        return ''
    with LOG.open('rb') as stream:
        stream.seek(offset)
        return stream.read().decode(errors='replace')


def idle(label):
    started, i = time.monotonic(), 0
    while True:
        activity = op(f'{label}-{i}', 'save', {'action': 'read'})['activity']
        if activity.get('in_progress') is False and activity.get('compress_thread_alive') is False:
            return activity
        if time.monotonic() - started > 120:
            raise RuntimeError('Native save did not finish compressing')
        time.sleep(.25)
        i += 1


def stop(label):
    data = run(label, 'stop', allow=True)
    result = json.loads((STAGE / (label + '.json')).read_text())
    if result['exit']:
        if data.get('error') == 'stop_timeout' or 'stop --force' in json.dumps(data):
            if run(label + '-stuck-status', 'status')['running']:
                run(label + '-force', 'stop', '--force')
        else:
            raise RuntimeError(f'{label}: {data}')
    if run(label + '-status', 'status')['running'] is not False:
        raise RuntimeError(f'{label}: player still running')


def wait_for_city(label, since=0, required='1000 residents, 2000 visitors'):
    started = time.monotonic()
    while time.monotonic() - started < 180:
        if wrap():
            raise RuntimeError('WRAP_UP_NOW')
        lines = log_since(since).splitlines()
        active = [line for line in lines if 'Persistent population active:' in line and (required is None or required in line)]
        if active:
            return write(label + '-activation', active[-1])
        time.sleep(1)
    raise RuntimeError(f'{label}: no matching Persistent population active line 180 s after load')


def start_and_load(label, recovery=True, retained_after_load=False):
    if not FIXTURE:
        raise RuntimeError('PC_RIG_FIXTURE must name the FT fixture save')
    for i in range(2 if recovery else 1):
        if wrap():
            raise RuntimeError('WRAP_UP_NOW')
        tag = label + ('-retry' if i else '')
        for attempt in range(21):
            resources = run(tag + '-resources-' + str(attempt), 'resources')
            if resources['disk_free_gib'] >= 12 and resources['memory_free_percent'] >= 20:
                start_label = tag + '-start' if not attempt else tag + '-resource-retry-' + str(attempt) + '-start'
                result = run(start_label, 'start', '--mode', 'headless', '--rig-test', allow=True)
                if json.loads((STAGE / (start_label + '.json')).read_text())['exit'] == 0:
                    break
                if 'Insufficient Mac resource headroom' not in json.dumps(result):
                    raise RuntimeError(f'{start_label}: {result}')
            if wrap():
                raise RuntimeError('WRAP_UP_NOW')
            if attempt < 20:
                time.sleep(30)
        else:
            raise RuntimeError('Insufficient Mac resource headroom: ' + json.dumps(resources))
        auto(tag + '-menu-autosave')
        since = log_size()
        loaded = run(tag + '-load', 'load', json.dumps({'name': FIXTURE}), allow=True)
        if 'error' not in loaded:
            auto(tag + '-loaded-autosave')
            if retained_after_load:
                console(tag + '-retained-after-load', 'pc gc retained')
                auto(tag + '-retained-after-load-autosave-after')
            call(tag + '-pause', 'time', {'pause': True})
            wait_for_city(tag, since)
            return
        if i or not recovery or not str(loaded['error']).startswith('ops_load_'):
            raise RuntimeError(f'{tag}-load: {loaded}')
        stop(tag + '-load-recovery-stop')
        archive(tag + '-load-failure')


def clock(label):
    return call(label + '-clock-read', 'state')['result']['clock']


def finish_if_wrapped(label):
    if wrap():
        call(label + '-wrap-pause', 'time', {'pause': True})
        raise RuntimeError('WRAP_UP_NOW')


def go(label, target, speed):
    current = clock(label)
    if current['paused'] and current['time'] == target and current['run_until'] is None:
        return

    def minutes(text):
        h, m = map(int, text.split(':'))
        return 60 * h + m
    day = current['day'] + (minutes(current['time']) >= minutes(target))
    if not (current['run_until'] or '').endswith(' ' + target):
        call(label + '-go-' + target.replace(':', ''), 'time', {'speed': speed, 'run_until': {'day': str(day), 'time': target}, 'keep_needs': True})
    deadline = time.monotonic() + 1800
    while time.monotonic() < deadline:
        finish_if_wrapped(label)
        time.sleep(2)
        current = clock(label)
        if current['paused'] and current['run_until'] is None:
            if current['time'] != target:
                raise RuntimeError(f'Clock target {target} reached {current}')
            return
    raise RuntimeError(f'Clock target {target} timed out: {current}')


def rate(label, speed, pause=False):
    return call(label, 'time', {'speed': speed, 'pause': pause, 'keep_needs': True})


def optional(label, operation):
    try:
        return operation()
    except Exception as exc:
        if str(exc) == 'WRAP_UP_NOW':
            raise
        write(label + '-error', {'error': str(exc)})
        print(label, exc, flush=True)
        return None


def probe_read(label, action, **args):
    reply = op(label, 'perf_union', dict(action=action, **args))
    after = reply.get('after', {})
    if not after or after.get('refusal'):
        raise RuntimeError(f'op perf_union {action}: {reply}')
    return after


def track_cost(label):
    data = prop(label, 'SaveCostDiagnostics')
    if data.get('value', {}).get('requested') is not False:
        raise RuntimeError(f'SaveCostDiagnostics was not off: {data}')


def command_data(label, line, prefix, timeout=30):
    offset = log_size()
    reply = console(label + '-console', line)
    deadline = time.monotonic() + timeout
    while True:
        lines = (reply.get('result', {}).get('output') or []) + log_since(offset).splitlines()
        for entry in lines:
            if entry.startswith(prefix + '{'):
                return write(label, json.loads(entry[len(prefix):]))
        if time.monotonic() >= deadline:
            raise RuntimeError(f'console {line!r}: missing {prefix!r} JSON in bridge reply and Player.log')
        time.sleep(.1)


def census(label):
    return command_data(label, 'pc gc retained', CENSUS)


def type_read(row, key):
    if key in row.get('type_counts', {}):
        return row['type_counts'][key]
    return next((item['objects'] for item in row.get('top_types', []) if item['name'] == key), None)


def game_pid():
    return json.loads((SUPPORT / 'run.json').read_text())['pid']


def rss_kib(pid):
    return int(subprocess.check_output(['ps', '-p', str(pid), '-o', 'rss='], text=True).strip())


def footprint(label, pid):
    args = ['vmmap', '-summary', str(pid)]
    try:
        result = subprocess.run(args, text=True, capture_output=True, timeout=15)
    except subprocess.TimeoutExpired as exc:
        write(label + '-vmmap-error', {'command': args, 'error': str(exc)})
        return None
    STAGE.mkdir(parents=True, exist_ok=True)
    (STAGE / (label + '.vmmap')).write_text(result.stdout + result.stderr)
    write(label + '-vmmap', {'command': args, 'exit': result.returncode})
    return next((line.split(':', 1)[1].strip() for line in result.stdout.splitlines() if line.startswith('Physical footprint:')), None)


def loaded_identity(label, build_sha=None):
    lines = [line for line in LOG.read_text(errors='replace').splitlines()
             if 'staged_mod name=' in line or 'Persistent population active:' in line or ('Real Life Overhaul' in line and 'initialised' in line)]
    write(label + '-identity', lines)
    if not any('name=PersistentCity' in line and (build_sha is None or build_sha in line) for line in lines):
        raise RuntimeError(f'{label}: staged PersistentCity build {build_sha} not in Player.log')
    if not any('Real Life Overhaul' in line and 'initialised' in line for line in lines):
        raise RuntimeError(f'{label}: Real Life Overhaul did not initialise')
    return lines


def host(label):
    commands = {'uptime': ['uptime'], 'memory_pressure': ['memory_pressure', '-Q'], 'top': ['sh', '-c', 'ps -Arco pid,pcpu,comm | head -8']}
    data = {'utc': now()}
    for key, command in commands.items():
        try:
            data[key] = subprocess.run(command, text=True, capture_output=True, timeout=15).stdout
        except (OSError, subprocess.TimeoutExpired) as exc:
            data[key] = 'error: ' + str(exc)
    run_file = SUPPORT / 'run.json'
    data['rig_run'] = {k: v for k, v in json.loads(run_file.read_text()).items() if k in ('pid', 'mode', 'rig_test', 'caffeinate_pid')} if run_file.exists() else None
    return write(label, data)
