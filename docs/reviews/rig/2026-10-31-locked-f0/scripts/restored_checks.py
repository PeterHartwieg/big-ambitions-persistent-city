import sys,time,json,shutil
from pathlib import Path
import rig,run_review as r,f0_driver as d
S=Path(__file__).parent
def inv(label,target,method,args):return rig.probe(label,'f0-invoke',True,{'target':target,'method':method,'args':args})
r.boot('restored-boot');r.advance_to('restored-clock',10)
rig.call('on-aux-pause','time',{'pause':True});d.snapshot('on-aux-before');rig.probe('on-aux-unload','f0-aux',True);time.sleep(2)
shutil.copy2(rig.PROFILE/'ModsLocal/PR3RigGuard/f0-aux-completed.json',S/'on-aux-completed.json');d.snapshot('on-aux-after')
rig.call('on-aux-resume','time',{'speed':1,'pause':False,'keep_needs':True})
d.enter('on-failure');rig.probe('on-failure-seam','f0-failclosed',True);time.sleep(3);d.snapshot('on-failure-hidden');time.sleep(3);d.snapshot('on-failure-latched')
d.exitshop('on-failure-exit');d.enter('on-failure-new-entry');d.snapshot('on-failure-recovered')
# Native office attempt in the fresh morning clock.
d.exitshop('on-morning-office-transfer');office=d.enter('on-morning-office','ba:street_firstavenue#28')
if office['inside']:d.exitshop('on-morning-office-exit')
else:
 try:rig.call('on-morning-office-close-ui','ui',{'op':'escape'})
 except Exception:pass
# Separate owned-state export/native save+reload continuity proof, no timing.
exec(compile((S/'logical_continuity.py').read_text(),str(S/'logical_continuity.py'),'exec'),globals())
rig.call('on-final-normal','time',{'speed':1,'pause':False,'keep_needs':True});d.exitshop('on-final-reload-exit');d.enter('on-final-reentry')
rig.probe('on-callbacks-before-shutdown','f0-callbacks',True);rig.probe('on-patches-before-shutdown','patches',True)
inv('on-runtime-shutdown','runtime','Shutdown',[]);time.sleep(2);d.snapshot('on-shutdown-state');rig.probe('on-callbacks-after-shutdown','f0-callbacks',True);rig.probe('on-patches-after-shutdown','patches',True)
d.exitshop('on-shutdown-exit');time.sleep(5);d.snapshot('on-shutdown-outside')
print('RESTORED FUNCTIONAL CHECKS COMPLETE',flush=True)
