import sys,time
from pathlib import Path
import rig,run_review as r,f0_driver as d
S=Path(__file__).parent
def inv(label,target,method,args):return rig.probe(label,'f0-invoke',True,{'target':target,'method':method,'args':args})
rig.call('on-final-normal','time',{'speed':1,'pause':False,'keep_needs':True});d.exitshop('on-final-reload-exit');d.enter('on-final-reentry')
rig.probe('on-callbacks-before-shutdown','f0-callbacks',True);rig.probe('on-patches-before-shutdown','patches',True)
inv('on-runtime-shutdown','runtime','Shutdown',[]);time.sleep(2);d.snapshot('on-shutdown-state');rig.probe('on-callbacks-after-shutdown','f0-callbacks',True);rig.probe('on-patches-after-shutdown','patches',True)
d.exitshop('on-shutdown-exit');time.sleep(5);d.snapshot('on-shutdown-outside')
print('RESTORED FUNCTIONAL CHECKS COMPLETE',flush=True)
