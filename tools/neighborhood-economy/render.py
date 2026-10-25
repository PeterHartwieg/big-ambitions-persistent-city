"""Rendered whole-game checks in the owned Windows rig, with matching calendar, scene and graphics."""
from pathlib import Path
import os,sys,subprocess,json,time
repo=Path(__file__).resolve().parents[2]
os.environ.update(PC_FOOD_CAMPAIGN='neighborhood-performance-20261023',PC_FOOD_SOURCE='source',PC_FOOD_SFTP='1',PC_CLOCK_FACTOR='0.3333333333333333',PC_PHYSICAL_ENABLED='1',PC_POPULATION_ENABLED='1',PC_RIG_MODE='windowed')
sys.path.insert(0,str(repo/'tools/food-demand-audit'))
import pc
assert not pc.status()['running']
assert not json.loads(pc.a.locks.lock_action('agent1','status'))['held']
pc.record('acquired',json.loads(pc.a.locks.lock_action('agent1','acquire',purpose='Persistent City rendered neighborhood economy performance and management panel')))
def run(script,*args):
 with (pc.ROOT/(Path(script).stem+'-render.log')).open('a') as log:
  subprocess.run([sys.executable,str(repo/script),*args],cwd=repo,env=os.environ,stdout=log,stderr=subprocess.STDOUT,check=True)
def stage(name):
 pc.record('render-stage',{'stage':name,'utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())});print(name,flush=True)
try:
 for script in ['pc.py','prepare.py','build_probe.py']:
  stage(script);run('tools/food-demand-audit/'+script)
 run('tools/food-demand-audit/session.py','start')
 run('tools/neighborhood-economy/keep_awake.py')
 from protocol import checked,probe_call,health,record,remote,ROOT,PROFILE
 def probe(op,**args):
  r=probe_call(op,**args)
  if 'error' in r:raise RuntimeError(r['error'])
  return r
 def ready(population=True):
  end=time.monotonic()+300
  while time.monotonic()<end:
   h=health()
   if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done' and not h.get('time_skip'):
    checked('pause-ready','time',{'pause':True,'speed':1,'keep_needs':True})
    if not population:return h
    result=probe_call('status',30)
    if 'error' not in result and result.get('caughtUp',False):return result
   time.sleep(2)
  raise RuntimeError('Rendered city did not become ready')
 def stop():
  record('stop-render',{'output':pc.a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'stop'},'agent1')})
  end=time.monotonic()+60
  while pc.status()['running']:
   if time.monotonic()>end:raise RuntimeError('Rig did not stop')
   time.sleep(2)
 def mode(enabled):
  stop()
  script=r'''
$config='PROFILE\ModsLocal\PersistentCity\persistent-city.json';$c=Get-Content -Raw $config|ConvertFrom-Json;$c.Enabled=ENABLED;$c.PhysicalEnabled=$true;$c|ConvertTo-Json|Set-Content -Encoding UTF8 $config
$path='C:\BigAmbitions-AgentTests\agent-install.json';$m=Get-Content -Raw $path|ConvertFrom-Json;foreach($f in $m.files){if($f.path -eq $config){$f.sha256=(Get-FileHash -Algorithm SHA256 $config).Hash.ToLowerInvariant()}};$m|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 $path
'''.replace('PROFILE',PROFILE).replace('ENABLED','$true' if enabled else '$false')
  remote(script,True);record('start-render',json.loads(pc.a.locks.invoke(r'C:\BigAmbitions-AgentTests\Agent-Windows.ps1',{'Action':'start','Mode':'windowed'},'agent1')))
  run('tools/neighborhood-economy/keep_awake.py')
  end=time.monotonic()+240
  while not health():
   if time.monotonic()>end:raise RuntimeError('No windowed bridge')
   time.sleep(2)
 stage('daylight-and-time-skip')
 checked('load-neighborhood','load',{'name':'FT Food Routines Neighborhood one-small Day 2 20261023'})
 initial=record('catchup-before',ready())
 record('catchup-frame-start',probe('frames-start'));started=time.monotonic()
 record('catchup-needs-start',probe('skip-needs-start'))
 checked('native-nine-hour-skip','console',{'line':'skiptime 9h true'})
 time.sleep(2)
 after=record('catchup-after',ready());record('catchup-frames',probe('frames-stop'))
 record('catchup-needs-end',probe('skip-needs-end'))
 record('catchup-result',{'elapsedSeconds':time.monotonic()-started,'startMinute':initial['minute'],'endMinute':after['minute'],'finalLag':after['nativeMinute']-after['minute']})
 checkpoint='FT Food Routines Neighborhood Daylight Performance 20261023'
 checked('save-daylight','save',{'name':checkpoint})
 for label,enabled in [('candidate-a',True),('native-control',False),('candidate-b',True)]:
  stage(label)
  if label!='candidate-a':mode(enabled)
  started=time.monotonic();checked(label+'-load','load',{'name':checkpoint});loaded=ready(enabled)
  record(label+'-load-seconds',{'seconds':time.monotonic()-started})
  if not enabled:record(label+'-calendar-control',probe('clock-only-baseline'))
  checked(label+'-player-position','teleport',{'who':'player','place':'address:ba:street_thirdstreet#30'})
  checked(label+'-warm','time',{'pause':False,'speed':1,'keep_needs':True});time.sleep(30)
  record(label+'-frames-start',probe('frames-start'))
  samples=[];end=time.monotonic()+180
  while time.monotonic()<end:
   time.sleep(min(15,max(0,end-time.monotonic())));samples.append(probe('frames-status'))
  record(label+'-frames',probe('frames-stop'));record(label+'-samples',samples)
  checked(label+'-pause','time',{'pause':True,'speed':1,'keep_needs':True})
  if enabled:
   record(label+'-state',ready());record(label+'-economy',probe('economy-report'))
 stage('fast-forward-and-resume')
 current=ready();minute=int(current['nativeMinute'])+120
 record('fast-forward-frames-start',probe('frames-start'))
 checked('fast-forward-two-hours','time',{'run_until':{'day':minute//1440,'time':f'{minute%1440//60:02d}:{minute%60:02d}'},'speed':1,'keep_needs':True})
 probe('fast-forward');end=time.monotonic()+300
 while not health().get('paused'):
  if time.monotonic()>end:raise RuntimeError('Fast-forward did not finish')
  time.sleep(2)
 probe('normal');ready();record('fast-forward-frames',probe('frames-stop'))
 checked('resume-physical','time',{'pause':False,'speed':1,'keep_needs':True});time.sleep(15)
 record('physical-resumed',probe('frames-status'))
 checked('pause-after-resume','time',{'pause':True,'speed':1,'keep_needs':True});ready()
 stage('live-hours-check')
 record('live-hours-check',probe('hours-refresh-check'))
 stage('management-view')
 record('management-screenshot',probe('panel-screenshot'));time.sleep(3)
 subprocess.run(['scp','-q','-o','BatchMode=yes','homepc:'+pc.HELPER.replace('\\','/')+'/neighborhood-management.png',str(ROOT/'neighborhood-management.png')],check=True,timeout=90)
 stage('save-and-reload')
 before=record('save-reload-before',probe('status'));started=time.monotonic()
 name='FT Food Routines Neighborhood Rendered Candidate 20261023'
 checked('save-rendered-candidate','save',{'name':name});record('save-timing',{'seconds':time.monotonic()-started})
 started=time.monotonic();checked('reload-rendered-candidate','load',{'name':name});after=record('save-reload-after',ready())
 record('reload-timing',{'seconds':time.monotonic()-started})
 for k in ['openingSavings','income','essentials','householdFood','heldPurchases','arrears']:
  assert before['economy'][k]==after['economy'][k],('reload changed',k)
 assert before['economy']['balances']+before['economy']['preparedFood']==after['economy']['balances']+after['economy']['preparedFood']
 record('render-complete',{'cases':3,'normalSecondsPerCase':180,'normalPhysicalLimits':{'pedestrians':48,'vehicles':24},'normalProfileTouched':False})
except BaseException as e:
 pc.record('render-error',{'type':type(e).__name__,'error':str(e)[:6000]});raise
finally:
 if (pc.ROOT/'baseline.json').exists():stage('restoring-rendered-rig');run('tools/food-demand-audit/session.py','restore')
 else:pc.a.locks.lock_action('agent1','release')
 stage('restored-and-released')
