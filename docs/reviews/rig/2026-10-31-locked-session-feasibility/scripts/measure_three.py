import sys,time,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
import run_review as review
rig=review.rig
review.boot('fresh')
rig.call('measured-normal','time',{'speed':1,'pause':False,'keep_needs':True})
time.sleep(15)
for i in range(1,4):
    label='locked-window-'+str(i)
    rig.record(label+'-clock-before',rig.health())
    review.measure(label,seconds=60)
    rig.record(label+'-clock-after',rig.health())
rig.call('measurements-paused','time',{'pause':True})
print('All three windows complete',flush=True)
