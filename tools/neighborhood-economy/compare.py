"""Native multi-day market cases, each restored to the same pre-food city and fully accounted fixture."""
from pathlib import Path
import os,sys,subprocess,json,time
repo=Path(__file__).resolve().parents[2]
for key,value in dict(PC_FOOD_CAMPAIGN='neighborhood-finance-20261023',PC_FOOD_SOURCE='source',PC_FOOD_SFTP='1',PC_CLOCK_FACTOR='0.3333333333333333').items():os.environ.setdefault(key,value)
sys.path.insert(0,str(repo/'tools/food-demand-audit'))
from protocol import *
def run(script,*args):
 with (ROOT/(Path(script).stem+'-comparison.log')).open('a') as log:
  subprocess.run([sys.executable,str(repo/script),*args],cwd=repo,env=os.environ,stdout=log,stderr=subprocess.STDOUT,check=True)
def stage(name):
 record('comparison-stage',{'stage':name,'utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())});print(name,flush=True)
def ready():
 end=time.monotonic()+300
 while time.monotonic()<end:
  h=health()
  if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done':
   checked('pause','time',{'pause':True,'speed':1,'keep_needs':True})
   s=probe_call('status',30)
   if 'error' not in s and s.get('caughtUp',False):return s
  time.sleep(2)
 raise RuntimeError('Population not ready')
def checked_probe(op,**args):
 s=probe_call(op,**args)
 if 'error' in s:raise RuntimeError(s['error'])
 return s
small='address:ba%3Astreet_thirdstreet:30:shop'
overlap='address:ba%3Astreet_thirdstreet:46:shop'
separated='address:ba%3Astreet_ninthavenue:20:shop'
large='address:ba%3Astreet_fourthavenue:22:shop'
cases=[('one-small',{'shops':[small]},3),('two-overlapping',{'shops':[small,overlap]},2),
       ('two-separated',{'shops':[small,separated]},2),('larger',{'shops':[large]},2),
       ('higher-price',{'shops':[small],'priceFactor':1.5},2),
       ('lower-service',{'shops':[small],'skill':25},2),
       ('longer-hours',{'shops':[small],'opens':6,'closes':22},2)]
if os.environ.get('PC_EXTRA_LUNCH_CASE')=='1':cases.append(('lunch-hours',{'shops':[small],'opens':10,'closes':14},2))
if os.environ.get('PC_CASES'):
 selected=set(os.environ['PC_CASES'].split(','));assert selected <= {c[0] for c in cases}
 cases=[c for c in cases if c[0] in selected]
def stable(key,value):
 value=json.loads(json.dumps(value))
 if key=='economy':value.pop('minute',None)
 if key=='shops':
  for shop in value:shop['supply'].pop('AtMinute',None)
 return value
try:
 stage('preparing-shared-city')
 start=ready();assert start['scope'] is None
 base=os.environ.get('PC_SHARED_BASE')
 if not base:
  day=int(start['minute']//1440)+1
  checked('advance-base','time',{'run_until':{'day':day,'time':'01:00'},'speed':1,'keep_needs':True})
  checked_probe('fast-forward');end=time.monotonic()+900
  while not health().get('paused'):
   if time.monotonic()>end:raise RuntimeError('Base advance timeout')
   time.sleep(2)
  checked_probe('normal');ready()
  base='FT Food Routines Neighborhood Base 20261023'
  checked('save-shared-base','save',{'name':base})
 else:assert int(start['minute']%1440)<480
 record('shared-base',checked_probe('neighborhood-candidates'))
 for label,args,days in cases:
  stage('setup-'+label)
  checked(label+'-load-base','load',{'name':base});ready()
  setup=record(label+'-setup',checked_probe('neighborhood-setup',**args))
  checkpoint='FT Food Routines Neighborhood '+label+' Start 20261023'
  checked(label+'-save-start','save',{'name':checkpoint})
  for day in range(1,days+1):
   stage(label+'-day-'+str(day))
   run('tools/neighborhood-economy/skip_audit.py' if os.environ.get('PC_USE_SKIP')=='1' else 'tools/food-demand-audit/run_audit.py',label+'-day-'+str(day),checkpoint)
   checkpoint='FT Food Routines Neighborhood '+label+' Day '+str(day)+' 20261023'
   checked(label+'-save-'+str(day),'save',{'name':checkpoint})
  report=record(label+'-report',checked_probe('economy-report'))
  before=record(label+'-before-reload',checked_probe('status'))
  checked(label+'-reload','load',{'name':checkpoint});after=record(label+'-after-reload',ready())
  for key in ['purchases','spendingCents','economy','accounts','shops']:
   assert stable(key,before[key])==stable(key,after[key]),(label,'reload changed',key)
  if any(m['Status']==3 for m in after['active']):
   replay=record(label+'-receipt-replay',checked_probe('replay-paid'))
   assert replay['paid']
   for key in ['purchases','spendingCents','economy','accounts','shops']:
    assert replay['before'][key]==replay['after'][key],(label,'replay changed',key)
  stage('verified-'+label)
 record('comparison-complete',{'cases':[c[0] for c in cases],'days':sum(c[2] for c in cases)})
except BaseException as error:
 record('comparison-error',{'type':type(error).__name__,'error':str(error)[:8000]});raise
finally:
 stage('restoring-comparison-rig');run('tools/food-demand-audit/session.py','restore');stage('restored-and-released')
