"""Dedicated native FT character producer, not a rig_flows scenario/new_game bypass.

Uses the documented bridge new_game operation as the owning lifecycle. Source review is
required before execution. No existing city namespace is cleared, migrated by this tool,
or rewritten; green production code initializes its own state normally in the new city.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import sys
import time
import uuid

from run_flow import Lifecycle, PRODUCT, PROFILE, ROOT, no_reparse, sha, utc, write, atomic_restore

class StartupRefusal(RuntimeError): pass
def startup_phase(health,scene='menu'):
    if health.get('scene')!=scene: raise StartupRefusal('Unexpected native scene before producer operation')
    identity=health.get('agent_instance') or {}
    pointer=identity.get('rig_input') or {}; ops=health.get('rig_ops') or identity.get('rig_ops') or {}
    pointer_state=pointer.get('status'); ops_state=ops.get('status'); reason=ops.get('reason')
    if pointer_state not in {'not started','ready'}: raise StartupRefusal('Required rig input refused/absent/error: '+str(pointer_state))
    ops_starting=ops_state=='disabled' and reason=='not started'
    if not ops_starting and (ops_state!='ready' or reason not in {None,''}):
        raise StartupRefusal('Required rig ops disabled/error: '+str(reason or ops_state))
    if pointer_state=='not started' or ops_starting: return 'not started'
    mouse=health.get('rig_mouse') or {}
    if mouse.get('available') is not True: raise StartupRefusal('Ready input lacks available native mouse')
    coverage=mouse.get('legacy_coverage') or {}
    if coverage.get('queue_dropped')!=0 or coverage.get('scan_problems'):
        raise StartupRefusal('Native startup coverage dropped work or reported scan problems')
    if coverage.get('pending') is True: return 'documented startup scan pending'
    if coverage.get('pending') is not False: raise StartupRefusal('Native coverage readiness shape unavailable')
    return 'ready'
def wait_startup_ready(fetch,alive,sleep=time.sleep,observe=None,scene='menu'):
    while True:
        if not alive(): raise StartupRefusal('Owned player exited before producer operation')
        health=fetch(); phase=startup_phase(health,scene)
        if observe is not None: observe(phase)
        if phase=='ready': return health
        sleep(.25)

class ExistingSaves:
    def __init__(self,directory,guard):
        self.directory,self.guard,self.manifest=directory,guard,None
        self.root=PROFILE/'SaveGames'
        self.owned_character=None
    def inventory(self):
        files=[]; directories=[]; pending=[self.root] if self.root.exists() else []
        while pending:
            folder=pending.pop(); no_reparse(folder); directories.append(folder.relative_to(self.root).as_posix())
            for path in folder.iterdir():
                no_reparse(path)
                if path.is_dir(): pending.append(path)
                elif path.is_file(): files.append(path)
                else: raise RuntimeError('Unsupported entry in isolated SaveGames')
        return sorted(files),sorted(directories)
    def capture(self):
        with self.guard():
            self.directory.mkdir(parents=True,exist_ok=False)
            files,directories=self.inventory(); rows=[]
            for path in files:
                relative=path.relative_to(self.root); backup=self.directory/'originals'/relative
                backup.parent.mkdir(parents=True,exist_ok=True)
                digest=sha(path); shutil.copyfile(path,backup)
                if sha(path)!=digest or sha(backup)!=digest: raise RuntimeError('Preexisting isolated save changed during backup')
                rows.append(dict(relative=relative.as_posix(),sha256=digest,size_bytes=path.stat().st_size))
            self.manifest=dict(format=1,root=str(self.root),captured_utc=utc(),directories=directories,files=rows)
            write(self.directory/'snapshot.json',self.manifest)
    def verify(self):
        if self.manifest is None: return
        with self.guard():
            changed=[]; verified=[]
            for row in self.manifest['files']:
                path=self.root/row['relative']; backup=self.directory/'originals'/row['relative']
                no_reparse(path); no_reparse(backup)
                if sha(backup)!=row['sha256']: raise RuntimeError('Corrupt original-save backup; no overwrite permitted')
                actual=sha(path) if path.is_file() else None
                if actual!=row['sha256']:
                    changed.append(row['relative'])
                    path.parent.mkdir(parents=True,exist_ok=True)
                    atomic_restore(path,backup.read_bytes())
                verified.append(dict(relative=row['relative'],verified=path.is_file() and sha(path)==row['sha256']))
            now,dirs=self.inventory(); original={r['relative'] for r in self.manifest['files']}
            additions=[dict(relative=p.relative_to(self.root).as_posix(),sha256=sha(p),size_bytes=p.stat().st_size)
                       for p in now if p.relative_to(self.root).as_posix() not in original]
            unexpected_additions=[row['relative'] for row in additions if self.owned_character is None
                                  or not (self.root/row['relative']).is_relative_to(self.owned_character)]
            report=dict(format=1,verified=all(row['verified'] for row in verified),unexpected_original_changes=changed,
                        unexpected_additions=unexpected_additions,originals=verified,added_files=additions,
                        added_directories=sorted(set(dirs)-set(self.manifest['directories'])))
            write(self.directory/'verification.json',report)
            if changed or unexpected_additions or not report['verified']: raise RuntimeError('Unexpected isolated-save changes; exact backups retained/restored, producer acceptance refused')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--name',required=True,help='New unique native save name beginning FT E5b ')
    parser.add_argument('--mods',type=Path,required=True,help='Reviewed complete receipt-hashed DLL declaration')
    parser.add_argument('--evidence',type=Path,required=True)
    parser.add_argument('--rig-repo',type=Path,default=Path(r'C:\Users\Peter\Coding_Projects\big-ambitions\big-ambitions-agent-rig'))
    args=parser.parse_args()
    if not args.name.startswith('FT E5b ') or len(args.name)>96 or Path(args.name).name!=args.name or any(c in args.name for c in '<>:"/\\|?*'):
        parser.error('Use a new bounded FT E5b save name without path characters')
    ready_name=args.name+' Ready'
    os.environ['HARNESS_ALERTS']='off'; sys.dont_write_bytecode=True
    sys.path.insert(0,str(args.rig_repo.resolve()/'scripts'))
    import rig_flows as flows
    from rig_fixtures import find_save
    from rig_modset import read_receipt, validate_inputs
    evidence=flows.evidence_output(args.evidence)
    own=Path(__file__).resolve().parents[2]/'build/e5b/review-r3/rig_test'
    if not evidence.is_relative_to(own.resolve()) or evidence.exists(): parser.error('Use a new assigned ignored evidence directory')
    document=dict(mods=json.loads(args.mods.read_text(encoding='utf-8-sig')))
    validate_inputs(document); read_receipt(ROOT,PROFILE)
    for path in (PROFILE/'SaveGames').glob('*/*/*'):
        if path.is_file() and path.suffix.lower() in {'.hsg','.json'} and path.stem in {args.name,ready_name}:
            parser.error('Requested new native name already exists')
    evidence.mkdir(parents=True)
    report=dict(format=1,name='E5b native new-character fixture producer',status='INFRA',exit_code=3,
        owner='e5b-producer-'+uuid.uuid4().hex,requested_name=args.name,ready_name=ready_name,
        started_utc=utc(),harness_alerts='off',performance_window=False,cleanup=[],
        provenance='Dedicated reviewed lifecycle calls documented native new_game from menu; ordinary rig_flows forbidden-step validation is unchanged')
    old_env={key:os.environ.get(key) for key in ['BA_LOCK_OWNER','BA_LOCK_TOKEN']}
    os.environ['BA_LOCK_OWNER']=report['owner']; os.environ.pop('BA_LOCK_TOKEN',None)
    rig=bridge=None; acquired=launch_attempted=stopped=False
    class ProducerRig(flows.LocalRig):
        def __init__(self):
            super().__init__(); self.lifecycle=Lifecycle(evidence/'lifecycle',self.file_guard)
            self.old_saves=ExistingSaves(evidence/'preexisting-saves',self.file_guard)
        def start(self):
            self.lifecycle.capture(); self.old_saves.capture(); super().start()
    try:
        rig=ProducerRig()
        lease=rig.lease('status'); report['lease_before']=lease
        if lease.get('held'): raise flows.Blocked('Existing agent1 owner blocks producer; no takeover or wait expiration')
        try:
            result=rig.lease('acquire')
            if result.get('held') is not True or result.get('owner')!=report['owner']: raise flows.Infra('Producer lease acquisition unconfirmed')
            acquired=True
        except Exception:
            result=rig.lease('status')
            if result.get('held') and result.get('owner')==report['owner']: acquired=True
            else: raise
        probe=rig.probe()
        if probe['normal_running'] or probe['agent_running']: raise flows.Blocked('An existing player blocks producer')
        rig.prepare_inputs(document,evidence,report)
        launch_attempted=True; rig.start()
        bridge=flows.EvidenceBridge(rig,evidence,rig.scenario)
        while True:
            try: health=bridge.health()
            except rig.scenario.BridgeError as error:
                if error.remote or error.restart or not rig.probe()['agent_running']: raise
                time.sleep(.25); continue
            if health.get('scene')=='city': raise flows.Infra('New native character requires verified menu; refusing an existing loaded city')
            if health.get('scene')=='menu': break
            time.sleep(.25)
        report['health_before_new_game']=wait_startup_ready(bridge.health,lambda:rig.probe()['agent_running'],
            observe=lambda phase:report.update(startup_phase=phase))
        def checked_selfcheck(label):
            from rig_selfcheck import run
            def read_checks():
                checks,mods=run(lock_token=rig.credential)
                return dict(ok=all(check['passed'] for check in checks.values()),checks=checks,mod_inventory=mods)
            value=bridge.record({'selfcheck':label},read_checks)
            report[label]=value
            if not value['ok']: raise flows.Infra('Rig selfcheck failed; full individual checks retained in report/transport evidence')
            return value
        checked_selfcheck('selfcheck_before_new_game')
        bridge.autosave_off()
        report['native_new_game']=bridge.record({'producer_lifecycle':'native_new_game','args':{'name':args.name}},
            lambda:rig.bridge.call('new_game',{'name':args.name}))
        while True:
            health=bridge.health(); status=(health.get('load') or {}).get('status')
            if status=='failed': raise flows.Infra('Native new_game reports failed; do not retry mutation')
            if status=='done' and health.get('scene')=='city': break
            if not rig.probe()['agent_running']: raise flows.Infra('Owned producer player exited during native new_game')
            time.sleep(.25)
        bridge.autosave_off(); bridge.call('time',{'pause':True})
        report['health_after_new_game']=wait_startup_ready(bridge.health,lambda:rig.probe()['agent_running'],scene='city')
        checked_selfcheck('selfcheck_after_new_game')
        created=find_save(PROFILE,args.name)
        prior=set(rig.old_saves.manifest['directories'])
        if created.parent.relative_to(PROFILE/'SaveGames').as_posix() in prior:
            raise flows.Infra('Native new_game did not produce an independently new character directory')
        rig.old_saves.owned_character=created.parent
        report['native_initial_save']=dict(name=args.name,path=str(created),sha256=sha(created),character_directory=str(created.parent))
        previous=None
        while True:
            value=bridge.record({'producer_readiness':'runtime'},lambda:rig.ops.call('e5b_proof',{'action':'runtime'}))
            actual=value.get('after') or {}; report['latest_runtime']=actual
            if actual.get('section')!='runtime': raise flows.Infra('Runtime readiness diagnostic refused')
            if actual.get('failed') is True: raise flows.Infra('Actual new-city population initialization failed; preserve native output and refusal')
            if actual.get('simulation_ready') is True: break
            signature=json.dumps(actual,sort_keys=True)
            if signature!=previous: print(json.dumps(dict(runtime=actual),indent=2),flush=True); previous=signature
            if not rig.probe()['agent_running']: raise flows.Infra('Owned player exited during native runtime initialization')
            time.sleep(.5)
        bridge.call('time',{'pause':True}); bridge.autosave_off()
        report['native_ready_save']=bridge.record({'producer_lifecycle':'native_save_ready','args':{'name':ready_name}},
            lambda:rig.bridge.call('save',{'name':ready_name}))
        ready=find_save(PROFILE,ready_name)
        if ready.parent!=created.parent: raise flows.Infra('Ready checkpoint changed native character directory')
        actual=bridge.record({'producer_readiness':'runtime_after_save'},lambda:rig.ops.call('e5b_proof',{'action':'runtime'}))['after']
        report['runtime_after_save']=actual
        if actual.get('failed') is not False or actual.get('saved_schema')!=25 or actual.get('simulation_ready') is not True:
            raise flows.Infra('Native ready save does not prove normal current-schema runtime creation')
        report['fixture']=dict(name=ready_name,path=str(ready),sha256=sha(ready),size_bytes=ready.stat().st_size,
            schema_observed=actual['saved_schema'],provenance='Native new_game, unmodified green initialization and explicit native save; no state injection')
        report.update(status='PASS',exit_code=0)
    except flows.Blocked as error: report.update(status='BLOCKED',exit_code=2,error=str(error))
    except Exception as error:
        report.update(status='INFRA',exit_code=3,error=str(error) if isinstance(error,(flows.Infra,ValueError,StartupRefusal)) else type(error).__name__)
    finally:
        def clean(name,action,menu_pause=False):
            try: action(); report['cleanup'].append(dict(action=name,ok=True)); return True
            except Exception as error:
                reply=getattr(error,'reply',None)
                if menu_pause and getattr(error,'remote',False) and isinstance(reply,dict) and reply.get('error')==flows.MENU_PAUSE_REFUSAL:
                    report['cleanup'].append(dict(action=name,ok=True,skipped=True,reason=flows.MENU_PAUSE_REFUSAL)); return True
                report['cleanup'].append(dict(action=name,ok=False,error=type(error).__name__)); report.update(status='INFRA',exit_code=3); return False
        if acquired:
            if launch_attempted:
                if bridge:
                    bridge.phase='cleanup'
                    clean('mouse release',lambda:bridge.call('mouse',{'op':'release'}))
                    clean('pause',lambda:bridge.call('time',{'pause':True}),menu_pause=True)
                stop_ok=clean('stop',rig.stop)
                if not stop_ok: clean('verified force stop after actual stop failure',lambda:rig.stop(force=True))
                def confirm():
                    while rig.probe()['agent_running']: time.sleep(.25)
                stopped=clean('confirm stopped',confirm)
                if stopped:
                    def log():
                        report['log_checks']=flows.run_log_checks(rig.log(),evidence,rig.logcheck)
                        if not report['log_checks']['ok']: raise flows.Infra('Native log checks fail')
                    clean('log evidence',log)
            else: stopped=True
            if stopped:
                clean('restore mod DLL set',rig.restore_mods)
                clean('verify/preserve preexisting native saves',rig.old_saves.verify)
                clean('restore exact isolated lifecycle',rig.lifecycle.restore)
            clean('end file reservation',rig.end_file_permit)
            clean('release lease',lambda:flows.release_lease(rig))
            def released():
                report['lease_after']=rig.lease('status')
                if report['lease_after'].get('held'): raise flows.Infra('Lease remains held after producer cleanup')
            clean('verify lease free',released)
        report['finished_utc']=utc(); write(evidence/'report.json',flows.redact(report))
        for key,value in old_env.items():
            if value is None: os.environ.pop(key,None)
            else: os.environ[key]=value
    print(json.dumps(dict(status=report['status'],exit_code=report['exit_code'],fixture=report.get('fixture'),evidence=str(evidence)),indent=2))
    return report['exit_code']
if __name__=='__main__': sys.exit(main())
