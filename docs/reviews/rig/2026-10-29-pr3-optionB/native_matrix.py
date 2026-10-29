import subprocess,sys
from pathlib import Path
import rig
from schema_native_helpers import unload,load
s=Path(__file__).parent
unload('migration14-prep')
subprocess.run([sys.executable,str(s/'migration14.py')],check=True)
subprocess.run([sys.executable,str(s/'codec_batch.py')],check=True)
subprocess.run([sys.executable,str(s/'schema_native.py')],check=True)
unload('matrix-restore');load('matrix-restore','FT Food Routines PR3 OB20261029')
subprocess.run([sys.executable,str(s/'settings_only.py')],check=True)
print('Migration/refusal/settings matrix finished',flush=True)
