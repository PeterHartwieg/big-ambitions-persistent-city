#!/usr/bin/env python3
"""Own, back up, run and restore the isolated Mac rig. Refuses a busy rig; never contacts Windows."""
from pathlib import Path
import argparse, hashlib, importlib.machinery, importlib.util, json, os, plistlib, shutil, subprocess, sys, time, uuid

repo = Path(__file__).resolve().parents[2]
rig = repo.parent / 'big-ambitions-driven-rides/tools/agent-tests'
os.environ['BA_LOCK_OWNER'] = 'persistent-city-food-comparison-20261021'
os.environ['BA_BRIDGE_URL'] = 'http://127.0.0.1:28330'
sys.path.insert(0, str(rig))
from mac_common import ROOT, PROFILE, BUNDLE, lifecycle, control
loader = importlib.machinery.SourceFileLoader('food_mac_agent', str(rig / 'mac-agent'))
spec = importlib.util.spec_from_loader(loader.name, loader)
agent = importlib.util.module_from_spec(spec); loader.exec_module(agent)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, default=repo / 'build/food-native-comparison-20261021/mac-run')
args = parser.parse_args(); output = args.output.resolve()
status = control('status')
if status.get('held') or agent.process():
    print(json.dumps({'deferred': True, 'lock': status, 'running': bool(agent.process())}, indent=2)); sys.exit(75)
if output.exists(): raise RuntimeError('Use a new output directory; retained evidence must not be replaced.')
helper = PROFILE / 'ModsLocal/FoodComparisonProbe'
if helper.exists(): raise RuntimeError('A FoodComparisonProbe already exists; refusing to replace it.')
source = repo / 'build/food-native-comparison-20261021/Probe/FoodComparisonProbe.dll'
manifest = json.loads((source.parent / 'build-manifest.json').read_text())
if sha(source) != manifest['output']['sha256']: raise RuntimeError('Probe build receipt mismatch.')
control('acquire', purpose='Native food-demand/price-acceptance comparison on a fresh FT city')
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
        (ROOT / 'install.json').write_text(json.dumps(current, indent=2) + '\n'); agent.verify()
        record('installation', {'probeSha256': sha(source), 'mods': {str(p.relative_to(PROFILE / 'ModsLocal')): sha(p) for p in (PROFILE / 'ModsLocal').rglob('*.dll')}})
    record('started', agent.start('background')); print('Mac rig started', flush=True)
    name = 'FT Food Economy Comparison ' + uuid.uuid4().hex[:7]
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
    # A second capture checks that repeated read/quote probes preserve the same state.
    for iteration in range(2):
        ident = 'capture-' + str(iteration) + '-' + uuid.uuid4().hex[:8]
        with lifecycle('diagnostic'):
            temp = helper / 'request.tmp'; temp.write_text(json.dumps({'id': ident, 'op': 'capture'})); temp.replace(helper / 'request.json')
        response = helper / ('response-' + ident + '.json'); until = time.monotonic() + 90
        while not response.exists():
            if time.monotonic() > until: raise RuntimeError('Native probe timeout; inspect owned run log.')
            time.sleep(.25)
        data = json.loads(response.read_text()); record('native-' + str(iteration), data)
        if 'error' in data: raise RuntimeError(data['error'])
        print('Native capture', iteration, 'markets', len(data.get('markets', [])), flush=True)
    record('after-health', agent.health())
    a = json.loads((output / 'native-0.json').read_text()); b = json.loads((output / 'native-1.json').read_text())
    record('replay-check', {'sameNativeMarketAndQuotes': a == b})
    if a != b: raise RuntimeError('Repeated paused native capture differs; investigate before interpreting.')
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
