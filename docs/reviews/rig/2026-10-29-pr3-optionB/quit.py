import sys,time,shutil
from pathlib import Path
import rig
label=sys.argv[1]
rig.call(label+'-quit','quit')
# Keep the foreground launcher alive until native shutdown finishes.
while True:
 import subprocess
 p=subprocess.run(['powershell','-NoProfile','-Command',"if(Get-Process -Name 'Big Ambitions Agent Tests' -ErrorAction SilentlyContinue){exit 1}"],stdout=subprocess.DEVNULL)
 if p.returncode==0:break
 time.sleep(.5)
dst=rig.STAGE/(label+'-Player-agent.log');assert not dst.exists();shutil.copy2(r'C:\BigAmbitions-AgentTests\Player-agent.log',dst)
rig.record(label+'-stopped',{'stopped':True})
print(label,'stopped; complete native log retained',flush=True)
