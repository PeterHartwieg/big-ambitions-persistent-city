import sys,time,shutil,json,subprocess
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent));import rig
folder=rig.PROFILE/'ModsLocal/PR2RigGuard'
def checkpoint(status,label):
 p=folder/('checkpoint-'+status+'.json')
 if p.exists():shutil.move(str(p),str(rig.STAGE/(label+'-prior-checkpoint.json')))
 rig.probe(label+'-armed','arm-checkpoint',True,{'status':status})
 rig.call(label+'-run','time',{'speed':10 if status=='Serving' else 1,'pause':False,'keep_needs':True})
 while not p.exists():rig.health();time.sleep(.2)
 shutil.copy2(p,rig.STAGE/(label+'-checkpoint.json'))
 rig.call(label+'-pause','time',{'pause':True})
 rig.probe(label+'-capture','capture',True)
 rig.probe(label+'-finance','finance-snapshot',True)
 rig.shot(label+'-city')
 return json.loads(p.read_text())
checkpoint('Serving','after-interrupted-serving')
name='FT Food Routines PR2 Serving Rerun20261029'
rig.call('after-interrupted-serving-save','save',{'name':name})
rig.probe('after-interrupted-serving-saved','capture',True)
subprocess.run([sys.executable,str(rig.STAGE/'load.py'),'after-serving-normal',name],check=True)
checkpoint('Paid','after-interrupted-paid')
name='FT Food Routines PR2 Paid Rerun20261029'
rig.call('after-interrupted-paid-save','save',{'name':name})
rig.probe('after-interrupted-paid-saved','capture',True)
subprocess.run([sys.executable,str(rig.STAGE/'load.py'),'after-paid-reload',name],check=True)
rig.probe('after-paid-replay','replay-paid',True)
rig.probe('after-paid-replay-final','capture',True)
print('Serving and Paid checkpoints saved/reloaded; paid receipt replay observed',flush=True)
