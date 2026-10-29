"""Reuse only native baseline evidence at the unchanged, verified before SHA."""
from pathlib import Path
import hashlib,json,shutil,subprocess
s=Path(__file__).parent
old=s.parent/'persistent-city-pr3-ob-rig'
expected='7357f78ca13edd5200e7e708950ffbc7a6ade1fe'
root=Path.cwd().parent/'rig-pr3-before'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==expected
build=json.loads((old/'before-build.json').read_text())
assert build['sha']==expected
assert hashlib.sha256((root/'build/PersistentCity/PersistentCity.dll').read_bytes()).hexdigest()==build['dll_sha256']
rows=[]
for p in old.glob('before-*'):
    if not p.is_file() or p.suffix not in ['.json','.jsonl','.log']:continue
    if (s/p.name).exists():continue
    shutil.copy2(p,s/p.name)
    rows.append(dict(file=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
# Native schema-14 save produced by the baseline, needed for a fresh AFTER migration.
profile=Path.home()/'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests'
for p in (old/'tested-profile/SaveGames').rglob('FT Food Routines PR3 OB Finance before*'):
    dest=profile/p.relative_to(old/'tested-profile')
    assert not dest.exists(),str(dest)
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(p,dest)
    rows.append(dict(file=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),purpose='baseline-written schema14 migration input'))
(s/'baseline-reuse.json').write_text(json.dumps(dict(sha=expected,dll_sha256=build['dll_sha256'],sourceEvidenceCommit='7937557935ef658c8410d2f2070a21058311e307',scope='All before native measurements reused; before offline gate rerun. Every after measurement is new.',files=rows),indent=2))
print('Reused unchanged baseline native artifacts:',len(rows))
