"""Prepare a fresh owned, backed-up neighborhood comparison; leave it paused for the runner."""
from pathlib import Path
import os,sys,subprocess,json,time
repo=Path(__file__).resolve().parents[2]
for key,value in dict(PC_FOOD_CAMPAIGN='neighborhood-finance-20261023',PC_FOOD_SOURCE='source',PC_FOOD_SFTP='1',PC_CLOCK_FACTOR='0.3333333333333333').items():os.environ.setdefault(key,value)
sys.path.insert(0,str(repo/'tools/food-demand-audit'))
import pc
assert not pc.status()['running']
assert not json.loads(pc.a.locks.lock_action('agent1','status'))['held']
pc.record('acquired',json.loads(pc.a.locks.lock_action('agent1','acquire',purpose='Persistent City resident finances and local market comparisons')))
def run(script,*args):
 with (pc.ROOT/(Path(script).stem+'-begin.log')).open('a') as log:
  subprocess.run([sys.executable,str(repo/script),*args],cwd=repo,env=os.environ,stdout=log,stderr=subprocess.STDOUT,check=True)
try:
 for script in ['pc.py','prepare.py','build_probe.py']:
  print(script,flush=True);run('tools/food-demand-audit/'+script)
 run('tools/food-demand-audit/session.py','start')
 from protocol import checked,probe_call,health
 checked('load-pre-food','load',{'name':os.environ.get('PC_PRE_FOOD_SAVE','FT Food Routines 20261022 r4')})
 end=time.monotonic()+300
 while time.monotonic()<end:
  h=health()
  if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done':
   checked('pause-pre-food','time',{'pause':True,'speed':1,'keep_needs':True})
   result=probe_call('neighborhood-candidates',30)
   if 'error' not in result:
    pc.record('candidate-catalogue',result);print(json.dumps(result,indent=2),flush=True);break
  time.sleep(2)
 else:raise RuntimeError('No ready pre-food population')
 if result['existingFoodScope'] is not None:raise RuntimeError('Selected checkpoint already owns food scope')
 pc.record('ready-for-comparison',{'paused':True,'minute':result['minute']})
except BaseException:
 if (pc.ROOT/'baseline.json').exists():run('tools/food-demand-audit/session.py','restore')
 else:pc.a.locks.lock_action('agent1','release')
 raise
