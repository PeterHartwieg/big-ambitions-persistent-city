import sys,time,subprocess
from pathlib import Path
import rig
arm,index=sys.argv[1],int(sys.argv[2]);label=arm+'-qa-'+str(index)
subprocess.run([sys.executable,str(rig.STAGE/'load.py'),label],check=True)
subprocess.run([sys.executable,str(rig.STAGE/'measure.py'),arm,str(index)],check=True)
subprocess.run([sys.executable,str(rig.STAGE/'native_control.py'),label,'FT Food Routines PR3 Rerun QA '+arm+str(index)+' 20261029'],check=True)
rig.probe(label+'-resumed-autosave-before','autosave-check',True)
rig.call(label+'-resumed-play','time',{'speed':1,'pause':False,'keep_needs':True})
start=time.monotonic()
while time.monotonic()-start<10:time.sleep(.5)
rig.call(label+'-resumed-pause','time',{'pause':True})
a=rig.probe(label+'-resumed-autosave-after','autosave-check',True);assert a['preventAutoSave'] and not a['savingGameInProgress']
rig.probe(label+'-pre-shutdown','capture',True)
rig.record(label+'-completed',{'normalPlayAfterReloadSeconds':time.monotonic()-start,'identicalSequenceCompleteBeforeQuit':True})
print(label+' complete, ready for graceful quit',flush=True)
