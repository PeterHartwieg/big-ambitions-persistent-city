"""Predeclared fresh-process confirmation after an unexplained historical-baseline tail shift."""
import sys,subprocess,json
from pathlib import Path
import rig
from schema_native_helpers import unload,load
arm=sys.argv[1];assert arm in ['before','after']
label='fresh-'+arm
for i in range(1,4):
 tag=label+'-qa-'+str(i)
 if i==1:subprocess.run([sys.executable,str(rig.STAGE/'load.py'),tag],check=True)
 else:unload(tag);load(tag,'FT Food Routines PR3 OB20261029')
 subprocess.run([sys.executable,str(rig.STAGE/'measure.py'),label,str(i)],check=True)
 subprocess.run([sys.executable,str(rig.STAGE/'native_control.py'),tag,'FT Food Routines PR3 OB Fresh '+arm+str(i)],check=True)
print(label,'three confirmation windows completed',flush=True)
