import sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
import run_review as review
rig=review.rig
arm=sys.argv[1]
rig.call(arm+'-normal','time',{'speed':1,'pause':False,'keep_needs':True})
time.sleep(15)
for i in range(1,4):
 label=f'{arm}-closed-{i}'
 rig.probe(label+'-inspector-reset','inspector-reset',True)
 rig.probe(label+'-inspector-before','inspector',True)
 review.measure(label)
 rig.probe(label+'-inspector-after','inspector',True)
rig.call(arm+'-pause','time',{'pause':True})
rig.probe(arm+'-patches-end','patches',True)
rig.call(arm+'-quit','quit')
