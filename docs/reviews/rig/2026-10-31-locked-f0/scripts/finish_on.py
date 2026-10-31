import sys,time,subprocess,json,shutil
from pathlib import Path
import rig,run_review as r,f0_driver as d
S=Path(__file__).parent
def inv(label,target,method,args):return rig.probe(label,'f0-invoke',True,{'target':target,'method':method,'args':args})
def config(mode):subprocess.run(['powershell','-NoProfile','-File',str(S/'Set-Config.ps1'),'-Mode',mode],check=True)
def reload(label,name=r.NAME,ready=True):
 rig.call(label+'-load','load',{'name':name});rig.waitcity(label+'-loaded',name)
 rig.call(label+'-pause','time',{'pause':True});rig.probe(label+'-autosave-off','autosave-off',True)
 if ready:
  if 'On Saved' in name:
   while True:
    state=rig.probe(label+'-background-'+str(time.time_ns()),'runtime-state')
    if state.get('failed'):raise RuntimeError(state)
    if str(state.get('status','')).startswith('Persistent population active:'):break
    time.sleep(1)
  else:r.wait_ready(label)
 rig.call(label+'-normal','time',{'speed':1,'pause':False,'keep_needs':True})
def track(label,seconds):
 rig.probe(label+'-track-start','f0-track-start',True);rig.probe(label+'-budget-start','f0-budget-start',True)
 time.sleep(seconds)
 rig.probe(label+'-budget-stop','f0-budget-stop',True);rig.probe(label+'-track-stop','f0-track-stop',True);d.snapshot(label+'-state')
track('on-second-market-observation',6)
d.exitshop('on-elevator-transfer')
try:
 d.enter('on-elevator','ba:street_firstavenue#28');d.capture('on-elevator')
except Exception as e:rig.record('on-elevator-tool-result',{'notRunReason':str(e)})
state=d.snapshot('on-elevator-observed')
if state['inside']:d.exitshop('on-elevator-exit')
else:
 try:rig.call('on-elevator-close-ui','ui',{'op':'escape'})
 except Exception:pass
config('tiny');reload('on-tiny');r.advance_to('on-tiny-clock',10);d.enter('on-tiny');track('on-tiny-caps',8)
config('zero');reload('on-zero');r.advance_to('on-zero-clock',10);d.enter('on-zero');track('on-zero-caps',6)
config('on');reload('on-restored');r.advance_to('on-restored-clock',10)
# A real auxiliary scene unload outside must retain existing street body leases.
rig.call('on-aux-pause','time',{'pause':True});d.snapshot('on-aux-before');rig.probe('on-aux-unload','f0-aux',True);time.sleep(2);shutil.copy2(rig.PROFILE/'ModsLocal/PR3RigGuard/f0-aux-completed.json',S/'on-aux-completed.json');d.snapshot('on-aux-after')
rig.call('on-aux-resume','time',{'speed':1,'pause':False,'keep_needs':True})
d.enter('on-failure');rig.probe('on-failure-seam','f0-failclosed',True);time.sleep(3);d.snapshot('on-failure-hidden');time.sleep(3);d.snapshot('on-failure-latched')
d.exitshop('on-failure-exit');d.enter('on-failure-new-entry');d.snapshot('on-failure-recovered')
rig.probe('on-callbacks-before-shutdown','f0-callbacks',True)
rig.probe('on-patches-before-shutdown','patches',True)
inv('on-runtime-shutdown','runtime','Shutdown',[]);time.sleep(2);d.snapshot('on-shutdown-state');rig.probe('on-callbacks-after-shutdown','f0-callbacks',True);rig.probe('on-patches-after-shutdown','patches',True)
config('disabled');reload('on-disabled',ready=False)
time.sleep(5);d.snapshot('on-disabled-state');rig.probe('on-disabled-callbacks','f0-callbacks',True);rig.probe('on-disabled-patches','patches',True)
print('CANDIDATE ON FUNCTIONAL COMPLETE',flush=True)
