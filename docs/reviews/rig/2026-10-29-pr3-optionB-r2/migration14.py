import subprocess,sys
from pathlib import Path
s=Path(__file__).parent
for script,args in [('load.py',['schema14-migration','FT Food Routines PR3 OB Finance before']),('native_control.py',['schema14-migration-control','FT Food Routines PR3 OB Migrated16'])]:subprocess.run([sys.executable,str(s/script),*args],check=True)
