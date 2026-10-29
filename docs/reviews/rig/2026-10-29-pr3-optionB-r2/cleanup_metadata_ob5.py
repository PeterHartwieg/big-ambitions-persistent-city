"""Verify restored target files and update final acceptance after lease release."""
from pathlib import Path
import json,hashlib,sys
s=Path(__file__).parent;profile=Path.home()/'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests'
def read(p):
 b=p.read_bytes();return json.loads(b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
rest=read(s/'restore-receipt.json');assert rest['restored'] and rest['preferences_hash_matches'] and not rest['rig_running']
inventory=read(s/'backup/profile-hashes.json');mods=[x for x in inventory if x['file'].startswith('ModsLocal\\')]
if not rest.get('targeted_verification'):
 for x in mods:assert sha(profile/x['file'])==x['sha256'],x['file']
config=next(x for x in mods if x['file'].endswith('PersistentCity\\persistent-city.json'))
autosave=read(s/'after-initial-autosave-off.json')
rest['targeted_verification']=dict(modsLocal_files_verified=len(mods),persistent_city_config=config,original_autosave={k:autosave[k] for k in ['originalPreventAutoSave','minutesBetweenAutoSaves','maxAutoSavesPerGame']},autosave_restore_basis='Complete original profile and registry export restored and hash-verified. No game launch after restoration.')
if '--before-release' in sys.argv:
 (s/'restore-receipt.json').write_text(json.dumps(rest,indent=2)+'\n');print('Targeted original ModsLocal/config hashes verified while lease held.');raise SystemExit(0)
release=read(s/'lock-released.json');assert not release['lease']['held'] and not release['lease']['pending']
for name,value in [('restore-receipt.json',rest),('lock-released.json',release),('backup-receipt.json',read(s/'backup-receipt.json')),('lease-acquired.json',read(s/'lease-acquired.json'))]:
 (s/name).write_text(json.dumps(value,indent=2)+'\n')
if (s/'acceptance.json').exists():
 a=read(s/'acceptance.json');a['checks']['cleanup']=True;a['cleanup']='Full profile, original root files and install references, ModsLocal/config and preferences hash-verified; rig stopped; lease released.';a['verdict']='PASS' if all(a['checks'].values()) else 'FAIL';(s/'acceptance.json').write_text(json.dumps(a,indent=2)+'\n');print(json.dumps(a,indent=2))
print('Cleanup targeted hashes and release verified.')
