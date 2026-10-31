import sys,time,subprocess
from pathlib import Path
import rig,run_review as r,f0_driver as d
S=Path(__file__).parent
r.boot('cand-missing')
rig.probe('cand-missing-loaded-dll','f0-loaded',True)
r.advance_to('cand-missing-clock',10)
d.enter('cand-missing');d.capture('cand-missing-inside')
rig.call('cand-missing-save','save',{'name':'FT Food Routines Locked F0 Missing Saved 20261031'})
d.snapshot('cand-missing-saved');rig.probe('cand-missing-contract','f0-contract',True)
d.exitshop('cand-missing')
rig.call('cand-missing-pause','time',{'pause':True})
subprocess.run(['powershell','-NoProfile','-File',str(S/'Set-Config.ps1'),'-Mode','off'],check=True)
d.measure('cand-off')
rig.call('cand-off-save','save',{'name':'FT Food Routines Locked F0 Off Saved 20261031'})
d.snapshot('cand-off-saved');rig.probe('cand-off-contract','f0-contract',True)
d.exitshop('cand-off')
rig.probe('cand-off-final-patches','patches',True)
print('CANDIDATE OFF COMPLETE',flush=True)
