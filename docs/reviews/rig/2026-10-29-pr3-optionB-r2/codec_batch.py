import rig,json,hashlib,shutil
from pathlib import Path
rows=[]
for case in json.loads((rig.STAGE/'schema-cases.json').read_text()):
 label='codec-'+case['file'].replace('.private.json','')
 r=rig.probe(label,'codec',True,{'file':case['file']})
 p=rig.PROFILE/'ModsLocal/PR3RigGuard'/r['output'];shutil.copy2(p,rig.STAGE/p.name)
 r.update(case=case['file'],expected=case['expected'],sourceSha256=case['sha256'],outputSha256=hashlib.sha256(p.read_bytes()).hexdigest())
 r['passed']=r['status']==r['expected'] and (r['writeSuccess'] if r['expected']=='Loaded' else not r['writeSuccess'] and r['originalPreserved'] and r['sourceSha256']==r['outputSha256'])
 rows.append(r);(rig.STAGE/'native-codec-summary.json').write_text(json.dumps(rows,indent=2));print(label,r['status'],r['passed'],flush=True);assert r['passed']
