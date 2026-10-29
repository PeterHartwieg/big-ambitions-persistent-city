"""Finalize metadata from retained campaign evidence; never launch the game."""
from pathlib import Path
import hashlib, json, subprocess, datetime

s = Path(__file__).parent
worktrees = Path(r'<worktrees>')
profile = Path.home() / 'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests'

def read(p):
    b = p.read_bytes()
    return json.loads(b.decode('utf-16' if b.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig'))

def write(name, value):
    (s/name).write_text(json.dumps(value, indent=2)+'\n', encoding='utf-8')

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

for name in ['cleanup-preflight.json','restore-receipt.json','lock-released.json','release-receipt.json']:
    write(name, read(s/name))

rest = read(s/'restore-receipt.json')
assert rest['restored'] and rest['preferences_hash_matches'] and not rest['rig_running']
release = read(s/'lock-released.json')
assert not release['lease']['held'] and not release['lease']['pending']

inventory = read(s/'backup/profile-hashes.json')
selected = []
for entry in inventory:
    if entry['file'].startswith('ModsLocal\\') or entry['file'] == 'PlayerSettings.json':
        current = profile/entry['file']
        actual = sha(current)
        assert actual == entry['sha256'], entry['file']
        selected.append({'file':entry['file'], 'sha256':actual})
autosave = read(s/'autosave-evidence.json')[0]['before']
rest['targeted_verification'] = {
    'modsLocal_files_verified':sum(x['file'].startswith('ModsLocal\\') for x in selected),
    'persistent_city_config':next(x for x in selected if x['file'].endswith('persistent-city.json')),
    'original_autosave':{k:autosave[k] for k in ['originalPreventAutoSave','minutesBetweenAutoSaves','maxAutoSavesPerGame']},
    'autosave_restore_basis':'Full profile inventory and original registry export restored byte-for-byte; no post-restore game launch.',
    'cleanup_only':True,
    'measurements_rerun':False,
}
write('restore-receipt.json',rest)

logs = sorted(set(s.glob('*Player-agent.log')) | {s/'native-final.log'})
write('native-log-provenance.json',{
    'recorded_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'note':'Hashes of complete retained log snapshots; snapshots may overlap. Raw logs remain private.',
    'files':[{'file':p.name,'bytes':p.stat().st_size,'sha256':sha(p)} for p in logs if p.exists()],
})
trees=[]
for arm in ['before','after']:
    tree=worktrees/('rig-pr3-'+arm)
    def git(*args):
        return subprocess.check_output(['git','-C',str(tree),*args],text=True).strip()
    status=git('status','--porcelain=v1','--untracked-files=no')
    assert not status, (arm,status)
    trees.append({'arm':arm,'head':git('rev-parse','HEAD'),'tracked_changes':status})
write('test-worktrees-unchanged.json',trees)
print('Final cleanup metadata verified; no game launch or measurement.')
