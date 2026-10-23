"""Finish an already running, backed-up baseline, then compare and restore the owned PC rig."""
from pathlib import Path
import os,sys,time,json,subprocess
repo=Path(__file__).resolve().parents[2]
os.environ['PC_FOOD_CAMPAIGN']='routine-venues-20261023'
os.environ['PC_FOOD_SOURCE']='baseline-source'
os.environ['PC_FOOD_SFTP']='1'
os.environ['PC_CLOCK_FACTOR']='0.3333333333333333'
os.environ['PC_FOOD_SETTLED']='FT Food Routines Venues Settled 20261023'
sys.path.insert(0,str(repo/'tools/food-demand-audit'))
import pc
root=pc.ROOT
assert json.loads((root/'baseline.json').read_text())['copyVerified']
assert json.loads(pc.a.locks.lock_action('agent1','status'))['owner']==os.environ['BA_LOCK_OWNER']
def stage(name):
 pc.record('stage',{'stage':name,'utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())});print(name,flush=True)
def run(script,*args):
 with (root/(Path(script).stem+'-pair.log')).open('w') as log:
  subprocess.run([sys.executable,str(repo/script),*args],env=os.environ,cwd=repo,stdout=log,stderr=subprocess.STDOUT,check=True)
try:
 if '--candidate-only' in sys.argv:
  assert not pc.status()['running']
  os.environ['PC_FOOD_SOURCE']='candidate-source'
  stage('building-candidate-helper');run('tools/food-demand-audit/build_probe.py')
  stage('deploying-candidate');run('tools/food-demand-audit/session.py','start')
 else:
  stage('waiting-for-baseline-evidence')
  until=time.monotonic()+1200
  required=['baseline-final.json','baseline-food-audit.json','baseline-audit-start-population.json','baseline-audit-end-population.json']
  while not all((root/n).exists() for n in required):
   if time.monotonic()>until:raise RuntimeError('Baseline evidence timeout')
   time.sleep(5)
  start=json.loads((root/'baseline-start.json').read_text());end=json.loads((root/'baseline-final.json').read_text());audit=json.loads((root/'baseline-food-audit.json').read_text())
  assert len([r for r in audit['routineDiagnostics'] if r['Outcome']=='missed'])==end['routines']['missed']-start['routines']['missed']
  assert not end['orders'] and not end['outings']
  stage('stopping-baseline')
  pc.record('baseline-stop',{'output':pc.a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'stop'},'agent1')})
  until=time.monotonic()+45
  while pc.status()['running']:
   if time.monotonic()>until:raise RuntimeError('Baseline player did not stop')
   time.sleep(2)
  os.environ['PC_FOOD_SOURCE']='candidate-source'
  stage('building-candidate-helper');run('tools/food-demand-audit/build_probe.py')
  stage('deploying-candidate');run('tools/food-demand-audit/update_helper.py','r1')
 stage('running-candidate');run('tools/food-demand-audit/run_audit.py','candidate')
 stage('settled-reload-and-replay');run('tools/food-demand-audit/check_settled_reload.py')
 stage('verifying-comparison');run('tools/routine-venues/verify.py',str(root))
except BaseException as error:
 pc.record('pair-error',{'type':type(error).__name__,'error':str(error)[:4000]});raise
finally:
 stage('restoring-pc-rig');run('tools/food-demand-audit/session.py','restore')
 stage('restored-and-released')
