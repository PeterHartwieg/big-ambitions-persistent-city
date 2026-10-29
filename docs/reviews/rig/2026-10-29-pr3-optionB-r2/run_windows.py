import subprocess,sys,time,json
from pathlib import Path
import rig
from schema_native_helpers import unload,load
arm=sys.argv[1]
for i in range(1,4):
 label=f'{arm}-qa-{i}'
 if i>1:
  unload(label);load(label,'FT Food Routines PR3 OB20261029')
 else:
  subprocess.run([sys.executable,str(rig.STAGE/'load.py'),label],check=True)
 subprocess.run([sys.executable,str(rig.STAGE/'measure.py'),arm,str(i)],check=True)
 subprocess.run([sys.executable,str(rig.STAGE/'native_control.py'),label,f'FT Food Routines PR3 OB QA {arm}{i}'],check=True)
 if i==1 and not (rig.STAGE/(arm+'-finance-end.json')).exists():
  unload(arm+'-finance');load(arm+'-finance','FT Food Routines PR3 OB20261029')
  subprocess.run([sys.executable,str(rig.STAGE/'financial_run.py'),arm],check=True)
  subprocess.run([sys.executable,str(rig.STAGE/'native_control.py'),arm+'-finance-save',f'FT Food Routines PR3 OB Finance {arm}'],check=True)
print(arm,'three windows, lifecycle and finance done',flush=True)
