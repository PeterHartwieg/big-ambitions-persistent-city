from pathlib import Path
import re,json,hashlib
import sys
stage=Path(sys.argv[1])
def check(path):
 text=path.read_text(encoding='utf-8',errors='replace');lines=text.splitlines()
 inactive=[x for x in lines if re.search(r'Patch .+: Not active|Not active:',x)]
 errors=[x for x in lines if re.search(r'\[Mod:[^]]+\].*(?:\bERROR\b|\bError:|Exception)',x)]
 exceptions=[]
 for i,l in enumerate(lines):
  if re.search(r'\b\w*Exception\b',l):
   block='\n'.join(lines[i:i+14])
   if 'PersistentCity' in block:exceptions.append(block)
 return dict(logSha256=hashlib.sha256(path.read_bytes()).hexdigest(),activePatchLines=sum(bool(re.search(r'Patch .+: Active\.',x)) for x in lines),inactiveLines=sorted(set(inactive)),inactiveLineCount=len(inactive),modErrors=errors,persistentCityExceptions=exceptions,navMeshWarnings=sum('not close enough to the NavMesh' in x for x in lines),loader='native ModAPI / Player-agent.log; no BepInEx or MelonLoader installation used')
if __name__=='__main__':
 rows={}
 for arm in ['before','after','after-full','after-final']:
  path=stage/(arm+'-Player-agent.log')
  if path.exists():rows[arm]=check(path)
 (stage/'native-log-checks.json').write_text(json.dumps(rows,indent=2))
 print(json.dumps({k:{x:v[x] for x in ['activePatchLines','inactiveLineCount','navMeshWarnings']}|{'modErrors':len(v['modErrors']),'persistentCityExceptions':len(v['persistentCityExceptions'])} for k,v in rows.items()},indent=2))
