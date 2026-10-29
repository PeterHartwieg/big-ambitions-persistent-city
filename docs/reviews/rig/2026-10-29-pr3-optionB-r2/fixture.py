import pathlib,json,shutil,hashlib
profile=pathlib.Path.home()/'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests'
stage=pathlib.Path(r'<private-evidence>')
src=next(profile.glob('SaveGames/**/FT Food Routines Ownership Perf20261027.hsg'))
name='FT Food Routines PR3 OB20261029'
dst=src.with_name(name+'.hsg');assert not dst.exists()
shutil.copy2(r'<private-fixtures>\performance-pc-20261027\morning-base.hsg',dst)
meta=json.loads(pathlib.Path(str(src)+'.meta').read_text(encoding='utf-8-sig'));meta['name']=name;meta['day']=5
pathlib.Path(str(dst)+'.meta').write_text(json.dumps(meta))
(stage/'fixture.json').write_text(json.dumps({'name':name,'path':str(dst),'sha256':hashlib.sha256(dst.read_bytes()).hexdigest()},indent=2))
print('Copied FT fixture',name,hashlib.sha256(dst.read_bytes()).hexdigest())
