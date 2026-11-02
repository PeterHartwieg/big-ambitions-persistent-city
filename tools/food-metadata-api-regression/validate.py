"""Required full offline matrix, sequential foreground subprocesses, no execution deadlines."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
out = root / 'build/food-metadata-api-validation'
out.mkdir(parents=True,exist_ok=True)
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false')
# The required E5c validator writes its historical manifest; preserve that record verbatim.
historical = [root / 'docs/checks/e5c-neutral-metadata-results.json', root / 'docs/checks/e5c-name-taste-process-proof.json']
original = {p:p.read_bytes() for p in historical if p.exists()}
process_proof = None
steps = [('full-required-matrix',[sys.executable,'tools/food-metadata-regression/validate.py']),
         ('e5a-source',[sys.executable,'tools/e5a-eligibility-regression/source_checks.py']),
         ('e5e-source',[sys.executable,'tools/food-metadata-api-regression/source_checks.py'])]
results = []
try:
    for name,command in steps:
        print(f'{name}: foreground start',flush=True)
        path = out / (name+'.log')
        with path.open('wb') as log:
            code = subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT).returncode
        results.append(dict(name=name,command=[Path(command[0]).name]+command[1:],exit=code,
                            sha256=hashlib.sha256(path.read_bytes()).hexdigest(),log=path.relative_to(root).as_posix()))
        (out/'results.json').write_text(json.dumps(results,indent=2)+'\n')
        print(f'{name}: exit {code}',flush=True)
        if code:
            print('\n'.join(path.read_text(errors='replace').splitlines()[-40:]),flush=True)
            raise SystemExit(code)
    process_proof = json.loads(historical[1].read_text())
finally:
    for path,content in original.items():
        path.write_bytes(content)
manifest = {'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'nativeEvidence':False,'steps':results,
            'nested':{},'seededTasteProcessProof':process_proof}
for name,path in [('d3a','build/d3a/gates/results.json'),('d4','build/d4-validation/results.json')]:
    rows=json.loads((root/path).read_text())
    for row in rows.values() if isinstance(rows,dict) else rows:
        assert row['exit']==0,row
        row['command']=[s.replace(str(root),'<repo>') for s in row['command']]
        row['command'][0]=Path(row['command'][0]).name
        row['sha256']=hashlib.sha256((root/row['log']).read_bytes()).hexdigest()
    manifest['nested'][name]=rows
(root/'docs/checks/e5e-metadata-api-results.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('PASS E5e required matrix; hashed command manifest in docs/checks/e5e-metadata-api-results.json',flush=True)
