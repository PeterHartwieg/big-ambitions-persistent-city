#!/usr/bin/env python3
"""Own, back up, run and restore the isolated Mac rig. Refuses a busy rig; never contacts Windows."""
from pathlib import Path
import argparse, hashlib, importlib.machinery, importlib.util, json, os, plistlib, shutil, subprocess, sys, time, uuid

repo = Path(__file__).resolve().parents[2]
rig = repo.parent / 'big-ambitions-driven-rides/tools/agent-tests'
os.environ['BA_LOCK_OWNER'] = 'persistent-city-food-routines-20261022'
os.environ['BA_BRIDGE_URL'] = 'http://127.0.0.1:28330'
sys.path.insert(0, str(rig))
from mac_common import ROOT, PROFILE, BUNDLE, lifecycle, control
loader = importlib.machinery.SourceFileLoader('food_mac_agent', str(rig / 'mac-agent'))
spec = importlib.util.spec_from_loader(loader.name, loader)
agent = importlib.util.module_from_spec(spec); loader.exec_module(agent)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, default=repo / 'build/food-routines-20261022/mac-run')
args = parser.parse_args(); output = args.output.resolve()
status = control('status')
if status.get('held') or agent.process():
    print(json.dumps({'deferred': True, 'lock': status, 'running': bool(agent.process())}, indent=2)); sys.exit(75)
if output.exists(): raise RuntimeError('Use a new output directory; retained evidence must not be replaced.')
helper = PROFILE / 'ModsLocal/FoodRoutineProbe'
if helper.exists(): raise RuntimeError('A FoodRoutineProbe already exists; refusing to replace it.')
source = repo / 'build/food-routines-20261022/Probe/FoodRoutineProbe.dll'
manifest = json.loads((source.parent / 'build-manifest.json').read_text())
candidate = repo / 'build/food-routines-20261022/source-repaired/build/PersistentCity/PersistentCity.dll'
reference = next(r for r in manifest['references'] if Path(r['path']).name == 'PersistentCity.dll')
if sha(candidate) != reference['sha256']: raise RuntimeError('Candidate differs from compiled probe reference.')
if sha(source) != manifest['output']['sha256']: raise RuntimeError('Probe build receipt mismatch.')
if not (PROFILE / 'ModsLocal/PersistentCity/PersistentCity.dll').is_file() or not (PROFILE / 'ModsLocal/PersistentCity/persistent-city.json').is_file():
    raise RuntimeError('Expected an existing Persistent City rig installation to restore.')
control('acquire', purpose='Native routine observation and bounded read-only food route comparison')
output.mkdir(parents=True)
backup = output / 'backup'
prepared = False

def record(name, data):
    (output / (name + '.json')).write_text(json.dumps(data, indent=2) + '\n'); return data

def call(name, command, params=None):
    data = record(name, agent.call(command, params or {})); print(name, flush=True); return data

try:
    with lifecycle():
        agent.verify(); backup.mkdir()
        subprocess.run(['cp', '-cR', str(PROFILE), str(backup / 'profile')], check=True)
        shutil.copy2(ROOT / 'install.json', backup / 'install.json')
        subprocess.run(['defaults', 'export', BUNDLE, str(backup / 'preferences.plist')], check=True)
        old = {str(p.relative_to(PROFILE)): sha(p) for p in PROFILE.rglob('*') if p.is_file()}
        if not all(sha(backup / 'profile' / n) == h for n, h in old.items()): raise RuntimeError('Backup differs from current rig profile.')
        record('before-files', old)
        prepared = True
        helper.mkdir(); shutil.copy2(source, helper / source.name)
        dependencies = helper / 'Dependencies'; dependencies.mkdir()
        harmony = repo / 'build/PersistentCity/Dependencies/0Harmony.dll'
        shutil.copy2(harmony, dependencies / harmony.name)
        current = json.loads((ROOT / 'install.json').read_text())
        for p in helper.rglob('*.dll'): current['files'][str(p)] = sha(p)
        target = PROFILE / 'ModsLocal/PersistentCity'
        target.mkdir(exist_ok=True)
        shutil.copy2(candidate, target / 'PersistentCity.dll')
        config = target / 'persistent-city.json'
        settings = json.loads(config.read_text())
        settings.update(Enabled=True, PhysicalEnabled=False)
        config.write_text(json.dumps(settings, indent=2) + '\n')
        for p in [target / 'PersistentCity.dll', config]: current['files'][str(p)] = sha(p)
        (ROOT / 'install.json').write_text(json.dumps(current, indent=2) + '\n'); agent.verify()
        record('installation', {'probeSha256': sha(source), 'mods': {str(p.relative_to(PROFILE / 'ModsLocal')): sha(p) for p in (PROFILE / 'ModsLocal').rglob('*.dll')}})
    record('started', agent.start('headless')); print('Mac rig started', flush=True)
    name = 'FT Food Routine Comparison ' + uuid.uuid4().hex[:7]
    call('new-game', 'new_game', {'name': name})
    until = time.monotonic() + 300
    while True:
        health = agent.health(); load = health.get('load') or {}
        if load.get('status') == 'failed': raise RuntimeError(str(load))
        if health.get('scene') == 'city' and load.get('status') == 'done' and load.get('name') == name: break
        if time.monotonic() > until: raise RuntimeError('New city load timeout')
        time.sleep(1)
    call('paused', 'time', {'pause': True, 'speed': 1, 'keep_needs': True})
    record('before-health', agent.health())
    def probe(op, timeout=180):
        ident = op + '-' + uuid.uuid4().hex[:8]
        with lifecycle('diagnostic'):
            temp = helper / 'request.tmp'; temp.write_text(json.dumps({'id': ident, 'op': op})); temp.replace(helper / 'request.json')
        response = helper / ('response-' + ident + '.json'); deadline = time.monotonic() + timeout
        while not response.exists():
            if time.monotonic() > deadline: raise RuntimeError('Native probe timeout: ' + op)
            time.sleep(.25)
        return json.loads(response.read_text())
    deadline = time.monotonic() + 240
    while True:
        ready = probe('status')
        if 'error' not in ready:
            begin = probe('begin')
            if 'error' not in begin: break
        if time.monotonic() > deadline: raise RuntimeError('Population not ready: ' + str(ready))
        time.sleep(2)
    record('begin', begin)
    if not begin.get('populationUnchanged'): raise RuntimeError('Observation changed population on attachment')
    first_day = int(begin['minute'] // 1440) + 1
    end_day = first_day + 1
    call('run', 'time', {'run_until': {'day': end_day, 'time': '00:00'}, 'speed': 1, 'keep_needs': True})
    if record('native-fast-forward', probe('fast-forward')).get('nativeFastForward') is not True:
        raise RuntimeError('Native fast-forward was not confirmed.')
    deadline = time.monotonic() + 1800
    last_day = None
    while True:
        health = agent.health()
        if health.get('day') != last_day:
            print('Observed game day', health.get('day'), health.get('time'), flush=True); last_day = health.get('day')
        if health.get('day', 0) >= end_day and health.get('paused'): break
        if time.monotonic() > deadline: raise RuntimeError('Native observation did not reach its target')
        time.sleep(2)
    call('paused-end', 'time', {'pause': True, 'speed': 1})
    if record('native-normal', probe('normal')).get('nativeFastForward') is not False:
        raise RuntimeError('Native normal-speed restore was not confirmed.')
    deadline = time.monotonic() + 300
    while True:
        ready = probe('status')
        if 'error' not in ready and ready['minute'] >= end_day * 1440: break
        if time.monotonic() > deadline: raise RuntimeError('Population catch-up incomplete: ' + str(ready))
        time.sleep(2)
    time.sleep(2)
    result = record('native-result', probe('finish', 600))
    if 'error' in result: raise RuntimeError(result['error'])
    if not result.get('marketUnchanged') or not result.get('populationUnchanged'): raise RuntimeError('Paused state changed during replay')
    print(json.dumps(result, indent=2), flush=True)
    record('after-health', agent.health())

finally:
    # Never restore files while a game process may still be writing them.
    if agent.process():
        try:
            record('stop', agent.stop())
        except RuntimeError as error:
            record('shutdown-wait', {'reason': str(error)})
            # Unity can withdraw its bridge before its shutdown process disappears.
            # Wait for verified exit; never restore beneath a live process.
            if not agent.wait_stopped(30): raise
            record('stop', {'stopped': True, 'extendedShutdownWait': True})
    if prepared:
        with lifecycle():
            if agent.process(): raise RuntimeError('Game still running; retain ownership and backup.')
            if (ROOT / 'Player-agent.log').exists(): shutil.copy2(ROOT / 'Player-agent.log', output / 'Player-agent.log')
            if helper.exists(): shutil.move(str(helper), output / 'probe-output')
            restored = []
            for n, h in old.items():
                p = PROFILE / n
                if not p.exists() or sha(p) != h:
                    p.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(backup / 'profile' / n, p); restored.append(n)
            if not all(sha(PROFILE / n) == h for n, h in old.items()): raise RuntimeError('Profile restoration did not verify.')
            shutil.copy2(backup / 'install.json', ROOT / 'install.json'); agent.verify()
            after_preferences = output / 'preferences-after.plist'
            subprocess.run(['defaults', 'export', BUNDLE, str(after_preferences)], check=True)
            before = plistlib.loads((backup / 'preferences.plist').read_bytes()); after = plistlib.loads(after_preferences.read_bytes())
            for key in set(after) - set(before): subprocess.run(['defaults', 'delete', BUNDLE, key], check=True, stdout=subprocess.DEVNULL)
            subprocess.run(['defaults', 'import', BUNDLE, str(backup / 'preferences.plist')], check=True)
            subprocess.run(['defaults', 'export', BUNDLE, str(output / 'preferences-restored.plist')], check=True)
            if plistlib.loads((output / 'preferences-restored.plist').read_bytes()) != before: raise RuntimeError('Preferences restoration failed.')
            extras = [str(p.relative_to(PROFILE)) for p in PROFILE.rglob('*') if p.is_file() and str(p.relative_to(PROFILE)) not in old]
            record('cleanup', {'preexistingFilesVerified': len(old), 'restoredFiles': restored, 'newFilesRetained': extras,
                'preferencesRestored': True, 'manifestVerified': True, 'probeRemoved': not helper.exists(), 'normalProfileTouched': False})
    record('released', control('release'))
