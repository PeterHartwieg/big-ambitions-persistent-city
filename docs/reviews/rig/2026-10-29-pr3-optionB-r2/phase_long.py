import subprocess,sys,json,time,hashlib
from pathlib import Path
s=Path(__file__).parent;rows=[]
for script,args in [('light_rate.py',[]),('extend_default.py',[]),('rate_arms.py',['controls']),('rate_arms.py',['decreases']),('pending_zero.py',[]),('metrics_arms.py',[]),('perf100.py',[])]:
 label=Path(script).stem+('-'+args[0] if args else '')
 print('START',label,flush=True);start=time.time()
 with (s/(label+'-driver.log')).open('w',encoding='utf-8') as out:r=subprocess.run([sys.executable,str(s/script),*args],stdout=out,stderr=subprocess.STDOUT)
 rows.append(dict(script=script,args=args,startedUnix=start,endedUnix=time.time(),exit=r.returncode,scriptSha256=hashlib.sha256((s/script).read_bytes()).hexdigest()))
 (s/'long-phase-exits.json').write_text(json.dumps(rows,indent=2));print('END',label,r.returncode,flush=True)
 if r.returncode:raise SystemExit(r.returncode)
