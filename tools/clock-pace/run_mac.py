#!/usr/bin/env python3
"""Own, back up, run and restore the isolated Mac rig. Refuses a busy rig; never contacts Windows."""
from pathlib import Path
import argparse, hashlib, importlib.machinery, importlib.util, json, os, plistlib, shutil, subprocess, sys, time, uuid

repo = Path(__file__).resolve().parents[2]
rig = repo.parent / 'big-ambitions-driven-rides/tools/agent-tests'
os.environ['BA_LOCK_OWNER'] = 'persistent-city-clock-pace-mac-20261022'
os.environ['BA_BRIDGE_URL'] = 'http://127.0.0.1:28330'
sys.path.insert(0, str(rig))
from mac_common import ROOT, PROFILE, BUNDLE, lifecycle, control
loader = importlib.machinery.SourceFileLoader('food_mac_agent', str(rig / 'mac-agent'))
spec = importlib.util.spec_from_loader(loader.name, loader)
agent = importlib.util.module_from_spec(spec); loader.exec_module(agent)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, default=repo / 'build/clock-pace-mac-20261022/mac-run')
parser.add_argument('--checkpoint', help='Reuse a retained disposable FT Food Routines checkpoint from a completed fixture setup')
parser.add_argument('--ride-only', action='store_true', help='Check real Driven Rides compatibility using the retained settled checkpoint')
args = parser.parse_args(); output = args.output.resolve()
status = control('status')
if status.get('held') or agent.process():
    print(json.dumps({'deferred': True, 'lock': status, 'running': bool(agent.process())}, indent=2)); sys.exit(75)
if output.exists(): raise RuntimeError('Use a new output directory; retained evidence must not be replaced.')
helper = PROFILE / 'ModsLocal/FoodDemandAuditProbe'
if helper.exists(): raise RuntimeError('A FoodDemandAuditProbe already exists; refusing to replace it.')
source = repo / 'build/clock-pace-mac-20261022/Probe/FoodDemandAuditProbe.dll'
manifest = json.loads((source.parent / 'build-manifest.json').read_text())
candidate = repo / 'build/clock-pace-mac-20261022' / os.environ.get('PC_FOOD_SOURCE','source') / 'build/PersistentCity/PersistentCity.dll'
reference = next(r for r in manifest['references'] if Path(r['path']).name == 'PersistentCity.dll')
if sha(candidate) != reference['sha256']: raise RuntimeError('Candidate differs from compiled probe reference.')
if sha(source) != manifest['output']['sha256']: raise RuntimeError('Probe build receipt mismatch.')
if not (PROFILE / 'ModsLocal/PersistentCity/PersistentCity.dll').is_file() or not (PROFILE / 'ModsLocal/PersistentCity/persistent-city.json').is_file():
    raise RuntimeError('Expected an existing Persistent City rig installation to restore.')
control('acquire', purpose='Matched native calendar pace and food demand comparison')
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
        settings.update(Enabled=True, PhysicalEnabled=False, GameClockFactor=1/3 if args.ride_only else 1)
        config.write_text(json.dumps(settings, indent=2) + '\n')
        for p in [target / 'PersistentCity.dll', config]: current['files'][str(p)] = sha(p)
        (ROOT / 'install.json').write_text(json.dumps(current, indent=2) + '\n'); agent.verify()
        record('installation', {'probeSha256': sha(source), 'mods': {str(p.relative_to(PROFILE / 'ModsLocal')): sha(p) for p in (PROFILE / 'ModsLocal').rglob('*.dll')}})
    def probe(op, timeout=180):
        ident = op + '-' + uuid.uuid4().hex[:8]
        with lifecycle('diagnostic'):
            temp = helper / 'request.tmp'; temp.write_text(json.dumps({'id': ident, 'op': op})); temp.replace(helper / 'request.json')
        response = helper / ('response-' + ident + '.json'); deadline = time.monotonic() + timeout
        while not response.exists():
            if time.monotonic() > deadline: raise RuntimeError('Native probe timeout: ' + op)
            time.sleep(.25)
        return json.loads(response.read_text())
    def ready(label,load_name=None):
        deadline=time.monotonic()+300
        # The bridge returns the accepted load before its next health snapshot.
        if load_name is not None:time.sleep(2)
        while True:
            h=agent.health()
            if (h.get('load') or {}).get('status')=='failed':raise RuntimeError(str(h))
            if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done' and (load_name is None or h['load'].get('name')==load_name):break
            if time.monotonic()>deadline:raise RuntimeError('City load timeout')
            time.sleep(1)
        call(label+'-pause','time',{'pause':True,'speed':1,'keep_needs':True})
        while True:
            d=probe('status')
            if 'error' not in d and abs(d['minute']-d['nativeMinute'])<.01:return d
            if time.monotonic()>deadline:raise RuntimeError('Population timeout '+str(d))
            time.sleep(1)
    def run_until(label,day,clock):
        call(label+'-run','time',{'run_until':{'day':day,'time':clock},'speed':1,'keep_needs':True})
        record(label+'-fast',probe('fast-forward'))
        deadline=time.monotonic()+1200;last_progress=0
        while True:
            h=agent.health()
            if h.get('paused') and h.get('day',0)>=day:break
            if time.monotonic()>deadline:raise RuntimeError('Clock timeout')
            if time.monotonic()-last_progress>45:
                print(label,h.get('day'),h.get('time'),flush=True);last_progress=time.monotonic()
            time.sleep(2)
        record(label+'-normal',probe('normal'));return ready(label)
    record('started', agent.start('headless')); print('Mac rig started', flush=True)
    checkpoint='FT Food Routines Clock Base 20261022'
    if args.ride_only:
        checkpoint='FT Food Routines Clock Settled 20261022'
    elif args.checkpoint:
        if not args.checkpoint.startswith('FT Food Routines '):raise RuntimeError('Disposable food fixture required')
        checkpoint=args.checkpoint
    else:
        name='FT Food Routines Clock 20261022'
        call('new-game','new_game',{'name':name});ready('fixture',name)
        setup=record('fixture-setup',probe('setup'))
        if 'error' in setup:raise RuntimeError(str(setup))
        run_until('fixture-settle',setup['startDay'],'01:00')
        call('fixture-save','save',{'name':checkpoint})
    for label,factor in ([] if args.ride_only else [('baseline',1),('candidate',1/3)]):
        if label=='candidate':
            record(label+'-stop',agent.stop())
            with lifecycle():
                settings=json.loads(config.read_text());settings['GameClockFactor']=factor
                config.write_text(json.dumps(settings,indent=2)+'\n')
                current=json.loads((ROOT/'install.json').read_text());current['files'][str(config)]=sha(config)
                (ROOT/'install.json').write_text(json.dumps(current,indent=2)+'\n');agent.verify()
            record(label+'-started',agent.start('headless'))
        call(label+'-load','load',{'name':checkpoint});start=record(label+'-start',ready(label,checkpoint))
        day=int(start['minute']//1440)
        if abs(start['travelClockRate']-factor)>.00001:raise RuntimeError('Wrong native clock rate '+str(start['travelClockRate']))
        preflight=record(label+'-boundary-preflight',run_until(label+'-boundary',day,'01:01'))
        if abs(preflight['travelClockRate']-factor)>.00001:raise RuntimeError('Boundary cap changed the travel clock')
        call(label+'-reload-after-preflight','load',{'name':checkpoint})
        start=record(label+'-start',ready(label+'-reloaded',checkpoint))
        record(label+'-audit-start',probe('audit-start'))
        for hour in [10,12,14,18,23]:
            s=run_until(label+'-'+str(hour),day,f'{hour:02d}:00')
            print(label,hour,'purchases',s['purchases']-start['purchases'],'decisions',s['auditDecisions'],flush=True)
        final=record(label+'-final',run_until(label+'-end',day+1,'01:00'))
        record(label+'-audit-end',probe('audit-end'))
        for filename in ['food-audit.json','audit-start-population.json','audit-end-population.json']:
            shutil.copy2(helper/filename,output/(label+'-'+filename))
        shutil.copy2(ROOT/'Player-agent.log',output/(label+'-Player-agent.log'))
        print(label,'complete',final['purchases']-start['purchases'],'native purchases',flush=True)
    if not args.ride_only:call('settled-save','save',{'name':'FT Food Routines Clock Settled 20261022'})
    call('settled-load','load',{'name':'FT Food Routines Clock Settled 20261022'})
    record('settled-reload',ready('settled','FT Food Routines Clock Settled 20261022'))
    record('paid-replay',probe('replay-paid'))
    # A real taxi ride checks that the two native clock patches do not multiply.
    call('ride-unpause','time',{'pause':False,'speed':1,'keep_needs':True})
    call('ride-outside','teleport',{'who':'player','place':'address:ba:street_thirdstreet#64'})
    locations=record('taxi-locations',probe('taxi-locations'));deadline=time.monotonic()+30
    while not locations.get('stands') and not locations.get('taxis') and time.monotonic()<deadline:
        time.sleep(2);locations=record('taxi-locations',probe('taxi-locations'))
    kind='stand' if locations.get('stands') else 'taxi'
    nearby=(locations.get('stands') or locations.get('taxis') or [None])[0]
    if nearby is None:raise RuntimeError('No active native taxi found for the compatibility check')
    call('ride-near-taxi','teleport',{'who':'player','x':nearby['x']+3,'z':nearby['z']+3})
    folder=PROFILE/'DrivenRides/tests';folder.mkdir(exist_ok=True,parents=True)
    results=folder/'ride-results.jsonl';before_lines=results.read_text().splitlines() if results.exists() else []
    with lifecycle('diagnostic'):
        if (folder/'command.txt').exists():raise RuntimeError('Existing ride command refused')
        (folder/'command.tmp').write_text('ride kind='+kind+' min=300 max=600 seed=73 speed=1 cancel=20\nend\n')
        (folder/'command.tmp').replace(folder/'command.txt')
    samples=[];deadline=time.monotonic()+240;ride_result=None
    while time.monotonic()<deadline:
        s=probe('status');samples.append({'minute':s.get('minute'),'rate':s.get('travelClockRate'),'clock':s.get('clock')})
        lines=results.read_text().splitlines() if results.exists() else []
        if len(lines)>len(before_lines):ride_result=json.loads(lines[-1]);break
        time.sleep(2)
    driving=[s for s in samples if s.get('clock',{}).get('rideDriving')]
    record('driven-rides-clock',{'ride':ride_result,'samples':samples,'passed':len(driving)>=2 and all(abs(s['rate']-1/3)<.00001 for s in driving)})
    call('ride-pause','time',{'pause':True,'speed':1})

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
