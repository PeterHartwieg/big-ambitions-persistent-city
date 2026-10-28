import sys,subprocess
from pathlib import Path
s=Path(__file__).parent
arm=sys.argv[1]
for script,args in [('load.py',[arm+'-finance']),('financial_run.py',[arm]),('native_control.py',[arm+'-financial-control','FT Food Routines PR3 '+('Schema14' if arm=='before' else 'Schema16')+' 20261029'])]:
 subprocess.run([sys.executable,str(s/script),*args],check=True)
print('Paired financial replay and saved reload complete',arm,flush=True)
