"""rig_test public evidence packer. No backups, raw saves, binary assemblies or lease token are published."""
from pathlib import Path
import json,gzip,hashlib,shutil,re
from PIL import Image
S=Path(__file__).parent
R=Path.cwd();OUT=R/'docs/reviews/rig/2026-10-30-pr4-a1-inspector'
OUT.mkdir(parents=True,exist_ok=True)

def clean(t):
 for a,b in [(str(R),'<repo>'),(str(R).replace('\\','/'),'<repo>'),(str(S),'<private-stage>'),(str(S).replace('\\','/'),'<private-stage>'),(str(Path.home()),'<user-home>'),(str(Path.home()).replace('\\','/'),'<user-home>')]:
  t=t.replace(a,b).replace(a.replace('\\','\\\\'),b)
 return t

def copy_text(src,dest):
 b=src.read_bytes();text=b.decode('utf-16' if b[:2] in [b'\xff\xfe',b'\xfe\xff'] else 'utf-8-sig');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(clean(text),encoding='utf-8')
for name in ['offline.json','ui-blocker.json','backup-receipt.json','backup-root-manifest.json','restore-receipt.json','lock-released.json','performance-summary.json','performance-noise.json','windows-summary.json','raw-provenance.json','log-patch-summary.json','tool-builds.json','installed-before.json','installed-after.json','after-inspector-initial.json']:
 if not (S/name).exists():raise RuntimeError('Missing required evidence '+name)
 copy_text(S/name,OUT/name)
for name in ['rig.py','ReviewGuard.cs','run_closed.py','run_review.py','runtime_tooling.py','analyze_closed.py','Backup-Rig.ps1','Restore-Rig.ps1','Release.ps1','Launch-Rig.ps1','prepare_review.py','bundle_evidence.py']:
 copy_text(S/name,OUT/'scripts'/name)
copy_text(S/'frame-source/Frames.cs',OUT/'scripts/Frames.cs')
for p in S.glob('frames-*.csv'):
 if any('-closed-'+str(i)+'-stop-' in p.name for i in range(1,4)):
  (OUT/(p.name+'.gz')).write_bytes(gzip.compress(p.read_bytes(),mtime=0))
for arm in ['before','after']:
 src=S/'backup/profile/FoodTrucksTest/shots'/('pr4-'+arm+'-normal-game.png')
 # Captures are retained from the test profile, outside the private backup.
 src=S/'tested-profile/FoodTrucksTest/shots'/('pr4-'+arm+'-normal-game.png')
 dest=OUT/'screenshots'/(arm+'-closed.jpg');dest.parent.mkdir(exist_ok=True)
 Image.open(src).convert('RGB').save(dest,quality=80,optimize=True)
manifest=[dict(file=str(p.relative_to(OUT)).replace('\\','/'),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in OUT.rglob('*') if p.is_file() and p.name!='file-manifest.json']
(OUT/'file-manifest.json').write_text(json.dumps(manifest,indent=2))
print('Evidence bytes',sum(p.stat().st_size for p in OUT.rglob('*') if p.is_file()))
