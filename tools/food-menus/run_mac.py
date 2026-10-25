#!/usr/bin/env python3
"""Bounded menu comparison and contract checks on the owned isolated Mac only."""
from pathlib import Path
import hashlib,json,os,shutil,sys,time
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'neighborhood-economy'))
from mac_rig import MacRig,agent
campaign=os.environ.get('PC_FOOD_CAMPAIGN','food-menus-mac-20261023')
r=MacRig(campaign,physical=True,mode='windowed')
base='FT Food Routines Neighborhood Base 20261023'
a='address:ba%3Astreet_thirdstreet:30:shop';b='address:ba%3Astreet_thirdstreet:46:shop'
c='ba:itemname_croissant';s='ba:itemname_hotdog';u='ba:itemname_burger'
cafe='ba:businesstype_coffeeshop';fast='ba:businesstype_fastfoodrestaurant'
pair={'shops':[a,b],'menus':[[c,s],[u,s]],'businessTypes':[cafe,fast]}
cases=[('pastry',{'shops':[a],'menus':[[c]],'businessTypes':[cafe]}),
 ('cafe-menu',{'shops':[a],'menus':[[c,s]],'businessTypes':[cafe]}),('different-menus',pair),
 ('higher-burger-price',{**pair,'prices':{u:18.75}})]
def stable(key,value):
 value=json.loads(json.dumps(value))
 if key=='economy':value.pop('minute',None)
 if key=='shops':
  for shop in value:
   shop['supply'].pop('AtMinute',None)
   for supply in shop['menuSupply'].values():supply.pop('AtMinute',None)
 return value
def check_shops(snapshot):
 assert snapshot['diagnostics']['active'], 'Native market inactive'
 for shop in snapshot['shops']:
  scope=next(s for s in snapshot['scope'] if s['PlaceId']==shop['PlaceId'])
  assert shop['exclusion']=='', (shop['PlaceId'],shop['exclusion'])
  assert set(shop['advertisedProducts'])<=set([scope['ProductId']]+scope['AdditionalProductIds'])
def base_hash(label):
 files={name:digest for name,digest in r.old.items() if Path(name).name in [base+'.hsg',base+'.hsg.meta']}
 assert len(files)==2
 from mac_rig import PROFILE
 for name,digest in files.items():assert hashlib.sha256((PROFILE/name).read_bytes()).hexdigest()==digest
 r.record(label+'-base-hash',files)
def skip(label,line):
 start=r.ready();value=line.split()[1];duration=int(value[:-1])*(1440 if value.endswith('d') else 60)
 r.probe('skip-needs-start');r.call(label,'console',{'line':line})
 # Command acknowledgement precedes the coroutine publishing time_skip. Do not
 # accept the still-paused old frame as a completed native skip.
 deadline=time.monotonic()+1800
 while True:
  time.sleep(2);end=r.ready()
  if end['nativeMinute']>=start['nativeMinute']+duration-.1:break
  if time.monotonic()>deadline:raise RuntimeError('Native skip did not reach its requested duration')
 r.probe('skip-needs-end');return end
try:
 r.stage('preparing');r.begin();r.focus('startup');base_hash('contract');r.load('contract-base',base)
 r.record('native-catalogue',r.probe('menu-catalogue'));skip('contract-noon','skiptime 11h true')
 r.record('contract-setup',r.probe('neighborhood-setup',opens=6,closes=22,**pair))
 for index,shop in enumerate([a,b]):r.record('native-contract-'+str(index),r.probe('menu-contract-check',shop=shop))
 r.record('menu-edit',r.probe('menu-edit-reload'))
 changed='FT Food Routines Mac Menu Exclusion 20261023';r.call('save-menu-edit','save',{'name':changed})
 edited=r.record('menu-edit-reload',r.load('menu-edit-reload',changed))
 assert [s['PlaceId'] for s in edited['shops'] if s['exclusion']]==[a]
 report=r.record('menu-edit-report',r.probe('economy-report'))
 assert 'Excluded:' in report['managementText'],'Changed-menu exclusion absent after reload'
 r.stage('contracts-verified')
 for label,arguments in cases:
  r.stage('setup-'+label);base_hash(label);start=r.load(label,base);assert start['scope'] is None
  setup=r.record(label+'-setup',r.probe('neighborhood-setup',opens=6,closes=22,**arguments))
  for fixture in setup['staffing']:
   assert all(d['isOpen'] and d['shifts'] for d in fixture['days'])
   assert all(w['employeeExists'] and w['stationExists'] for d in fixture['days'] for w in d['shifts'])
  for day in (1,2):
   prefix=label+'-day-'+str(day);r.stage(prefix)
   start=r.record(prefix+'-start',r.ready());check_shops(start);r.record(prefix+'-audit-start',r.probe('audit-start'))
   began=time.monotonic();final=r.record(prefix+'-final',skip(prefix+'-skip','skiptime 1d true'))
   check_shops(final)
   r.record(prefix+'-timing',{'seconds':time.monotonic()-began,'gameMinutes':final['nativeMinute']-start['nativeMinute']})
   assert abs(final['nativeMinute']-start['nativeMinute']-1440)<.1
   r.record(prefix+'-audit-end',r.probe('audit-end'))
   for name in ['food-audit.json','audit-start-population.json','audit-end-population.json']:
    shutil.copy2(r.helper/name,r.root/(prefix+'-'+name))
   print(prefix,'purchases',final['purchases']-start['purchases'],flush=True)
  r.record(label+'-report',r.probe('economy-report'))
  saved='FT Food Routines Mac Menus '+label+' 20261023';r.call(label+'-save','save',{'name':saved})
  before=r.record(label+'-before-reload',r.probe('status'));after=r.record(label+'-after-reload',r.load(label+'-reload',saved))
  for key in ['purchases','spendingCents','economy','accounts','shops','receipts']:
   assert stable(key,before[key])==stable(key,after[key]),('reload changed',label,key)
  replay=r.record(label+'-receipt-replay',r.probe('replay-meal-receipts'))
  for key in ['purchases','spendingCents','economy','accounts','shops','receipts']:
   assert replay['before'][key]==replay['after'][key],('replay changed',label,key)
  r.stage('verified-'+label)
 r.record('comparison-complete',{'cases':[x[0] for x in cases],'days':8,'platform':'Mac','mode':r.mode,'physical':True})
 r.stage('rendered-observation');r.record('render-daylight',skip('render-daylight-skip','skiptime 10h true'))
 r.call('render-position','teleport',{'who':'player','place':'address:ba:street_thirdstreet#30'});r.focus('menu-render')
 r.call('render-warm','time',{'pause':False,'speed':1,'keep_needs':True});time.sleep(15)
 r.record('render-frames-start',r.probe('frames-start'))
 for index in range(4):time.sleep(15);r.record('render-sample-'+str(index),r.probe('frames-status'))
 r.record('render-frames',r.probe('frames-stop'));r.call('render-pause','time',{'pause':True,'speed':1,'keep_needs':True});r.ready()
 r.record('management-screenshot',r.probe('panel-screenshot'));time.sleep(3)
 shutil.copy2(r.helper/'neighborhood-management.png',r.root/'management.png')
 r.stage('complete')
except BaseException as error:r.record('error',{'error':str(error)});raise
finally:r.close()
