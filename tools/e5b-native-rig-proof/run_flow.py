"""Existing guarded rig flow, plus exact isolated lifecycle backup/restoration. Native runner only."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid
import winreg

PRODUCT='Big Ambitions Agent Tests'
ROOT=Path(r'C:\BigAmbitions-AgentTests')
PROFILE=Path.home()/'AppData/LocalLow/Hovgaard Games'/PRODUCT
REG_PATH=r'Software\Hovgaard Games\Big Ambitions Agent Tests'
REG_NATIVE='HKCU\\'+REG_PATH
REG_EXPORT='HKEY_CURRENT_USER\\'+REG_PATH
TARGETS=(ROOT/'run.json',PROFILE/'PlayerSettings.json')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def utc(): return datetime.now(timezone.utc).isoformat()
def write(path,value): path.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
def registry_exists():
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,REG_PATH): return True
    except FileNotFoundError: return False
def reg(*arguments):
    process=subprocess.run(['reg.exe',*map(str,arguments)],capture_output=True,text=True)
    if process.returncode:
        raise RuntimeError('Isolated registry command failed: '+arguments[0]+' exit '+str(process.returncode))
def verify_reg_scope(path):
    body=path.read_text(encoding='utf-16')
    headers=[line[1:-1] for line in body.splitlines() if line.startswith('[')]
    if not headers or any(name!=REG_EXPORT and not name.startswith(REG_EXPORT+'\\') for name in headers):
        raise RuntimeError('Registry backup contains an unexpected key; import refused')
def no_reparse(path):
    for candidate in (path,*path.parents):
        if candidate.is_symlink() or (hasattr(candidate,'is_junction') and candidate.is_junction()):
            raise RuntimeError('Reparse point in fixed lifecycle target/backup; refused')
def atomic_restore(path,data):
    temporary=path.with_name(path.name+'.e5b-restore-'+uuid.uuid4().hex+'.tmp')
    try:
        with temporary.open('xb') as stream:
            stream.write(data); stream.flush(); os.fsync(stream.fileno())
        os.replace(temporary,path)
    finally:
        temporary.unlink(missing_ok=True)

class Lifecycle:
    def __init__(self,directory,guard):
        self.directory,self.guard,self.manifest=directory,guard,None
    def capture(self):
        with self.guard():
            self.directory.mkdir(parents=True,exist_ok=False)
            files=[]
            for index,path in enumerate(TARGETS):
                no_reparse(path)
                backup=self.directory/f'file-{index}.bin'
                existed=path.is_file()
                digest=sha(path) if existed else None
                if existed:
                    backup.write_bytes(path.read_bytes())
                    if sha(backup)!=digest or sha(path)!=digest:
                        raise RuntimeError('Isolated lifecycle source changed during backup')
                files.append(dict(path=str(path),existed=existed,sha256=digest,backup=backup.name))
            exists=registry_exists()
            exported=self.directory/'original.reg'
            if exists:
                reg('export',REG_NATIVE,exported,'/y'); verify_reg_scope(exported)
                verify=self.directory/'capture-verify.reg'
                reg('export',REG_NATIVE,verify,'/y')
                if sha(verify)!=sha(exported): raise RuntimeError('Isolated registry changed during capture')
            self.manifest=dict(format=1,captured_utc=utc(),product=PRODUCT,files=files,
                registry=dict(key=REG_NATIVE,existed=exists,backup=exported.name,sha256=sha(exported) if exists else None),
                harness_alerts='off',
                autosave_qualification='Exact original isolated file/key bytes retained; no autosave setting meaning/off-state inferred from backup alone')
            write(self.directory/'snapshot.json',self.manifest)
    def restore(self):
        if self.manifest is None: return
        with self.guard():
            manifest=self.manifest
            if [row['path'] for row in manifest['files']]!=list(map(str,TARGETS)) or manifest['registry']['key']!=REG_NATIVE:
                raise RuntimeError('Unexpected lifecycle restoration targets')
            for row in manifest['files']:
                backup=self.directory/row['backup']
                no_reparse(Path(row['path'])); no_reparse(backup)
                if row['existed'] and sha(backup)!=row['sha256']:
                    raise RuntimeError('Corrupt lifecycle backup; restoration refused')
            stored=manifest['registry']; exported=self.directory/stored['backup']
            if stored['existed']:
                if sha(exported)!=stored['sha256']: raise RuntimeError('Corrupt registry backup; restoration refused')
                verify_reg_scope(exported)
            # The caller's real lifecycle reservation verifies ownership and both players stopped.
            # These are exactly the two documented isolated targets, never computed deletion roots.
            failures=[]
            for row in manifest['files']:
                try:
                    path=Path(row['path'])
                    if row['existed']: atomic_restore(path,(self.directory/row['backup']).read_bytes())
                    else: path.unlink(missing_ok=True)
                except OSError: failures.append('file_restore:'+row['path'])
            try:
                if registry_exists(): reg('delete',REG_NATIVE,'/f')
                if stored['existed']: reg('import',exported)
            except RuntimeError: failures.append('isolated_registry_restore')
            comparisons=[]
            for row in manifest['files']:
                path=Path(row['path']); actual=sha(path) if path.is_file() else None
                comparisons.append(dict(path=str(path),expected=row['sha256'],actual=actual,
                    verified=(path.is_file()==row['existed'] and actual==row['sha256'])))
            reg_ok=registry_exists()==stored['existed']
            if stored['existed'] and registry_exists():
                fresh=self.directory/'restored.reg'
                reg('export',REG_NATIVE,fresh,'/y'); reg_ok=sha(fresh)==stored['sha256']
            report=dict(format=1,finished_utc=utc(),files=comparisons,registry_verified=reg_ok,
                failures=failures,verified=all(row['verified'] for row in comparisons) and reg_ok and not failures)
            write(self.directory/'restore.json',report)
            if not report['verified']: raise RuntimeError('Isolated lifecycle restoration/verification failed; retain backup')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('flow',type=Path)
    parser.add_argument('--evidence',type=Path,required=True)
    parser.add_argument('--rig-repo',type=Path,default=Path(r'C:\Users\Peter\Coding_Projects\big-ambitions\big-ambitions-agent-rig'))
    args=parser.parse_args()
    os.environ['HARNESS_ALERTS']='off' # No unsolicited external alerts; every real guard/selfcheck result is retained.
    sys.dont_write_bytecode=True
    sys.path.insert(0,str(args.rig_repo.resolve()/'scripts'))
    import rig_flows
    from local_overlay import safe_output
    from rig_modset import plain
    evidence=rig_flows.evidence_output(args.evidence)
    own=Path(__file__).resolve().parents[2]/'build/e5b/review-r3/rig_test'
    if not evidence.is_relative_to(own.resolve()): parser.error('Evidence must remain in the assigned ignored rig_test directory')
    if evidence.exists() and any(evidence.iterdir()): parser.error('Evidence directory must be new/empty')
    # Validate fixed lifecycle paths against the same reparse-point proof as modset transactions.
    for path in TARGETS: plain(path)
    safe_output(evidence)
    document=json.loads(args.flow.read_text(encoding='utf-8-sig'))
    class OwnedRig(rig_flows.LocalRig):
        def __init__(self):
            super().__init__()
            self.lifecycle=Lifecycle(evidence/'lifecycle',self.file_guard)
            self.selfcheck_index=0
        def selfcheck(self,notify=None):
            # Preserve the exact existing check results before the same failure gate raises.
            # No check is retried here, skipped, relaxed or converted to a success.
            from rig_selfcheck import run
            options={} if notify is None else {'notify':notify}
            checks,mods=run(lock_token=self.credential,**options)
            result=dict(ok=all(row['passed'] for row in checks.values()),checks=checks,mod_inventory=mods)
            self.selfcheck_index+=1
            (evidence/'selfchecks').mkdir(parents=True,exist_ok=True)
            write(evidence/'selfchecks'/('%03d.json'%self.selfcheck_index),rig_flows.redact(result))
            if not result['ok']: raise rig_flows.Infra('Rig selfcheck failed; full individual checks retained in selfchecks/')
            return result
        def start(self):
            try: self.lifecycle.capture()
            except Exception as error:
                write(evidence/'lifecycle-capture-refusal.json',dict(ok=False,error=type(error).__name__,detail=str(error)))
                raise
            super().start()
        def remove_copies(self):
            # Core always calls this after confirmed stop, before release, even without a mods field.
            # Attempt lifecycle restoration independently if ordinary fixture cleanup fails.
            errors=[]
            try: super().remove_copies()
            except Exception: errors.append('fixture_cleanup')
            try: self.lifecycle.restore()
            except Exception as error:
                write(evidence/'lifecycle-restore-refusal.json',dict(ok=False,error=type(error).__name__,detail=str(error)))
                errors.append('lifecycle_restore; inspect lifecycle-restore-refusal.json and retained snapshot')
            if errors: raise rig_flows.Infra('; '.join(errors))
    result=rig_flows.session(document,evidence,rig_factory=OwnedRig)
    result['pc_lifecycle_receipts']=dict(snapshot='lifecycle/snapshot.json',restoration='lifecycle/restore.json',harness_alerts='off')
    write(evidence/'report.json',result)
    print(json.dumps(dict(status=result['status'],exit_code=result['exit_code'],evidence=str(evidence),
                         lifecycle=result['pc_lifecycle_receipts']),indent=2))
    return result['exit_code']
if __name__=='__main__': sys.exit(main())
