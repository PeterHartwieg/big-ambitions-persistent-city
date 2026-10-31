from pathlib import Path
import sys,json,time,shutil,hashlib,psutil
import rig,run_review as review
S=Path(__file__).parent
def snapshot(label):
 r=rig.probe(label,'f0-state',True)
 print(label,{k:r.get(k) for k in ['frame','inside','entering','address','hidden','viewActive','physical','diagnostics','citizens','fallback']},flush=True)
 return r
def brief(r):return {k:v for k,v in r.items() if k not in ['ids','nativeCars','nativePeople','lighting','bindings']}
def load_snapshot():
 procs=[];counts={'dotnet':0,'testhost':0,'codex':0}
 for p in psutil.process_iter(['pid','name','memory_info','cpu_times']):
  try:
   n=p.info['name'].lower()
   for key in counts:
    if key in n:counts[key]+=1
   procs.append({'pid':p.pid,'name':p.info['name'],'cpuSeconds':sum(p.info['cpu_times'][:2]),'rss':p.info['memory_info'].rss})
  except (psutil.NoSuchProcess,psutil.AccessDenied):pass
 before={p['pid']:p for p in procs};t=time.monotonic();cpu=psutil.cpu_percent(interval=1);after=[]
 for p in psutil.process_iter(['pid','name','cpu_times','memory_info']):
  try:
   if p.pid in before:after.append({'pid':p.pid,'name':p.info['name'],'cpuPercentMachine':(sum(p.info['cpu_times'][:2])-before[p.pid]['cpuSeconds'])/(time.monotonic()-t)/psutil.cpu_count()*100,'rss':p.info['memory_info'].rss})
  except (psutil.NoSuchProcess,psutil.AccessDenied):pass
 return {'utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),'cpuTotalPercent':cpu,'logicalProcessors':psutil.cpu_count(),'counts':counts,'ram':dict(psutil.virtual_memory()._asdict()),'top':sorted(after,key=lambda p:p['cpuPercentMachine'],reverse=True)[:12]}
def wait_load(label):
 start=time.monotonic();samples=[]
 while True:
  running=[{'pid':p.pid,'name':p.info['name']} for p in psutil.process_iter(['name']) if p.info['name'] and any(n in p.info['name'].lower() for n in ['dotnet','testhost'])]
  elapsed=time.monotonic()-start;samples.append({'elapsed':elapsed,'processes':running});rig.record(label+'-load-wait',samples)
  if not running or elapsed>=900:break
  print('Waiting for existing dotnet/testhost',label,round(elapsed),running,flush=True);time.sleep(30)
 rig.record(label+'-load-before',load_snapshot())
def enter(label,address='ba:street_thirdstreet#30'):
 rig.call(label+'-position','teleport',{'who':'player','place':'address:'+address})
 things=rig.call(label+'-list','use',{'op':'list'})['things'];target=next(t for t in things if t.get('address')==address)
 rig.probe(label+'-track-start','f0-track-start',True)
 snapshot(label+'-before-entry');r=rig.call(label+'-enter','use',{'op':'click','id':target['id']});print('ENTER',r,flush=True)
 time.sleep(5);a=snapshot(label+'-inside');rig.probe(label+'-track-stop','f0-track-stop',True)
 return a
def exitshop(label):
 rig.probe(label+'-track-start','f0-track-start',True)
 try:
  r=rig.call(label+'-exit','walk',{'place':'exit'});print('EXIT',r,flush=True);time.sleep(4)
 except RuntimeError as error:
  rig.record(label+'-walk-refusal',{'tooling':str(error)})
 a=snapshot(label+'-outside')
 if a['inside']:
  rig.probe(label+'-native-exit','f0-invoke',True,{'target':'building','method':'ExitFromBuilding','args':[0,True]})
  time.sleep(4);a=snapshot(label+'-native-outside')
 rig.probe(label+'-track-stop','f0-track-stop',True)
 if a['inside']:raise RuntimeError('Native exit did not leave the building')
 return a
def capture(label):
 rig.probe(label+'-capture','capture',True);time.sleep(2)
 p=rig.PROFILE/'ModsLocal/PR3RigGuard/screen-capture.png'
 from PIL import Image
 im=Image.open(p);im.thumbnail((1280,720));im.convert('RGB').save(S/(label+'.jpg'),quality=82)
def measure(label):
 # Reset to the identical original disposable fixture after load contention wait.
 rig.call(label+'-reload','load',{'name':review.NAME});rig.waitcity(label+'-loaded',review.NAME)
 rig.call(label+'-pause','time',{'pause':True});rig.probe(label+'-autosave-off','autosave-off',True);review.wait_ready(label)
 review.advance_to(label+'-clock',10)
 enter(label)
 capture(label+'-inside')
 rig.call(label+'-load-pause','time',{'pause':True})
 wait_load(label)
 rig.call(label+'-load-resume','time',{'speed':1,'pause':False,'keep_needs':True})
 # Primary timing excludes diagnostic per-frame body/scene tracking and screenshots.
 rig.record(label+'-load-start',load_snapshot())
 r=review.measure(label,seconds=60)
 rig.record(label+'-load-after',load_snapshot());snapshot(label+'-after')
 capture(label+'-after')
 return r
if __name__=='__main__':
 command=sys.argv[1];label=sys.argv[2]
 if command=='prepare':
  review.advance_to(label+'-clock',10);snapshot(label+'-street');enter(label);capture(label+'-inside')
 elif command=='measure':measure(label)
 elif command=='exit':exitshop(label)
 elif command=='quit':
  shutil.copy2('C:/BigAmbitions-AgentTests/Player-agent.log',S/(label+'-Player.private.log'));shutil.copy2('C:/BigAmbitions-AgentTests/run.json',S/(label+'-run.private.json'));rig.call(label+'-quit','quit')
