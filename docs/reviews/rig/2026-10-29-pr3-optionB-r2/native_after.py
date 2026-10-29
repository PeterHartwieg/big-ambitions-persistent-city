"""Foreground native campaign. Failures remain failures; cleanup is performed separately."""
from pathlib import Path
import subprocess,sys,time,json,hashlib
s=Path(__file__).parent
jobs=[('run_windows.py',['after']),('rate_arms.py',[]),('phase_long.py',[]),('native_matrix.py',[]),('event_persistence.py',[]),('optional_visual.py',[])]
rows=[]
for script,args in jobs:
 label=Path(script).stem
 start=time.time();print('START',label,flush=True)
 with (s/(label+'-campaign-driver.log')).open('w',encoding='utf-8') as out:
  p=subprocess.run([sys.executable,str(s/script),*args],stdout=out,stderr=subprocess.STDOUT)
 rows.append(dict(script=script,args=args,startedUnix=start,endedUnix=time.time(),exit=p.returncode,sha256=hashlib.sha256((s/script).read_bytes()).hexdigest()))
 (s/'native-campaign-exits.json').write_text(json.dumps(rows,indent=2))
 print('END',label,p.returncode,flush=True)
 if p.returncode:raise SystemExit(p.returncode)
