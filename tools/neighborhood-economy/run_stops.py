"""Bounded multi-day existing-stop campaign; always restore the owned disposable rig."""
from pathlib import Path
import json,os,subprocess,sys,time
repo=Path(__file__).resolve().parents[2]
os.environ.update(PC_FOOD_CAMPAIGN='neighborhood-economy-20261023',PC_FOOD_SOURCE='source',
                  PC_FOOD_SFTP='1',PC_CLOCK_FACTOR='0.3333333333333333',
                  PC_FOOD_SETTLED='FT Food Routines Neighborhood Stops Settled 20261023')
sys.path.insert(0,str(repo/'tools/food-demand-audit'))
import pc
root=pc.ROOT

def stage(name):
 pc.record('stops-stage',{'stage':name,'utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())});print(name,flush=True)
def run(script,*args):
 with (root/(Path(script).stem+'-stops.log')).open('a') as log:
  subprocess.run([sys.executable,str(repo/script),*args],cwd=repo,env=os.environ,stdout=log,stderr=subprocess.STDOUT,check=True)
assert not pc.status()['running']
assert not json.loads(pc.a.locks.lock_action('agent1','status'))['held']
pc.record('acquired',json.loads(pc.a.locks.lock_action('agent1','acquire',purpose='Persistent City existing-stop and neighborhood economy validation')))
try:
 stage('backing-up');run('tools/food-demand-audit/pc.py')
 stage('freezing-and-building-stops');run('tools/food-demand-audit/prepare.py')
 stage('building-stops-helper');run('tools/food-demand-audit/build_probe.py')
 stage('deploying-stops');run('tools/food-demand-audit/session.py','start')
 name='FT Food Routines Settled 20261022'
 for day in range(3):
  label='candidate' if day==2 else 'stops-day-'+str(day+1)
  stage('stops-day-'+str(day+1));run('tools/food-demand-audit/run_audit.py',label,name)
  name='FT Food Routines Neighborhood Stops Day '+str(day+1)+' 20261023'
  pc.record('stops-save-'+str(day+1),pc.a.call('save',{'name':name}))
 stage('settled-reload-and-replay');run('tools/food-demand-audit/check_settled_reload.py')
except BaseException as error:
 pc.record('stops-error',{'type':type(error).__name__,'error':str(error)[:4000]});raise
finally:
 if (root/'baseline.json').exists():
  stage('restoring-stops-rig');run('tools/food-demand-audit/session.py','restore')
 else:
  pc.record('released-before-backup',json.loads(pc.a.locks.lock_action('agent1','release')))
 stage('stops-restored-and-released')
