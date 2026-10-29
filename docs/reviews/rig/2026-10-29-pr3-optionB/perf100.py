import subprocess,sys
from rate_arms import configure,config,original
from schema_native_helpers import unload
import rig
try:
 configure('perf100',100)
 subprocess.run([sys.executable,str(rig.STAGE/'measure.py'),'after100','1'],check=True)
finally:
 unload('perf100-restore');config.write_bytes(original)
