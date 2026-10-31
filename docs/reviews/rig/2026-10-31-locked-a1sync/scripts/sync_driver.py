from pathlib import Path
import time,sys,json,shutil,subprocess
import rig,drive,run_review as run,f0_driver as f
S=Path(__file__).parent
CONTENT='FT Food Routines Locked A1 Sync Content 20261031'
RELOAD='FT Food Routines Locked A1 Sync Reload 20261031'
def reload(label,name):
 rig.call(label+'-load','load',dict(name=name));rig.waitcity(label+'-loaded',name)
 while True:
  try:rig.call(label+'-pause','time',dict(pause=True));break
  except RuntimeError as e:
   if not any(x in str(e) for x in ['loading now','at the menu now']):raise
   time.sleep(.5)
 rig.probe(label+'-autosave-off','autosave-off',True);run.wait_ready(label)
 try:rig.call(label+'-escape','ui',dict(op='escape'))
 except RuntimeError as e:
  if 'Nothing is open' not in str(e):raise

def batch(label):
 ids=json.loads((S/'residents.json').read_text())['ids'];v=rig.probe(label,'address-batch',True,dict(residents=ids))
 print('BATCH',label,'minute',v['nativeMinute'],'unchanged',v['payloadIdentical'],flush=True)
 for e in v['entries']:print(e['id'],e['fields']['homeLabel'],flush=True)
 return v

def config(mode):
 if mode=='missing':
  p=rig.PROFILE/'ModsLocal/PersistentCity/persistent-city.json';j=json.loads(p.read_text(encoding='utf-8-sig'));p.write_text(json.dumps({k:v for k,v in j.items() if not k.startswith('Interior')},indent=2))
 else:subprocess.run(['powershell','-NoProfile','-File',str(S/'Set-Config.ps1'),'-Mode',mode],check=True)

def close(label):return rig.probe(label,'inspect-seam',True,dict(action='close'))

def measure(label,opened=False):
 # Load wait happens paused; check again immediately before capture to document races.
 drive.wait_load(label);drive.system(label+'-load-before')
 rig.probe(label+'-autosave-off','autosave-off',True);close(label+'-close')
 rig.call(label+'-normal','time',dict(speed=1,pause=False,keep_needs=True));time.sleep(15)
 if label!='base1':
  target=json.loads((S/'base1-state-before.json').read_text())['minute']
  while rig.probe(label+'-align-'+str(time.time_ns()),'state',True)['minute']<target:time.sleep(.1)
 if opened:rig.probe(label+'-open','inspect-seam',True,dict(resident=json.loads((S/'residents.json').read_text())['ids'][0]))
 rig.probe(label+'-inspector-reset','inspector-reset',True);rig.probe(label+'-format-reset','format-reset',True)
 drive.system(label+'-load-start')
 result=run.measure(label,60)
 rig.call(label+'-pause','time',dict(pause=True));rig.probe(label+'-format-counts','format-counts',True);rig.probe(label+'-inspector-timing','inspector',True)
 drive.system(label+'-load-after');rig.record(label+'-clock-after',rig.health());return result

def finish(label):
 rig.probe(label+'-loaded-dll','loaded',True);rig.probe(label+'-final-patches','patches',True)
 shutil.copy2('C:/BigAmbitions-AgentTests/Player-agent.log',S/(label+'-Player.log'));shutil.copy2('C:/BigAmbitions-AgentTests/run.json',S/(label+'-run.private.json'))
 rig.call(label+'-quit','quit')

def off_function(label):
 run.advance_to(label+'-clock',10)
 # Functional-only busy-shop clock alignment; no additional performance capture.
 if rig.health()['time']<'10:50':
  rig.call(label+'-busy-clock','time',dict(speed=6,run_until=dict(day='5',time='10:50'),keep_needs=True))
  while not rig.health()['paused']:time.sleep(.5)
  rig.call(label+'-busy-pause','time',dict(pause=True));run.wait_ready(label+'-busy')
  rig.call(label+'-busy-normal','time',dict(speed=1,pause=False,keep_needs=True));time.sleep(15)
 f.snapshot(label+'-street');f.enter(label);time.sleep(2);f.snapshot(label+'-held-inside')
 rig.call(label+'-save','save',dict(name='FT Food Routines Locked A1 Sync '+label+' Saved 20261031'))
 rig.probe(label+'-contract','f0-contract',True);f.exitshop(label);time.sleep(5);f.snapshot(label+'-reacquired')
 f.enter(label+'-repeat');f.snapshot(label+'-repeat-held');f.exitshop(label+'-repeat')

if __name__=='__main__':
 arm=sys.argv[1]
 try:
  run.boot(arm);rig.probe(arm+'-environment','environment',True)
  if arm=='base1':
   measure(arm);off_function('base-off');finish(arm)
  elif arm=='cand1':
   reload('candidate-content',CONTENT);drive.export('candidate-content-initial');batch('candidate-inspector-summary');f.capture('candidate-popup')
   close('candidate-close');rig.call('candidate-manual-save','save',dict(name=RELOAD));drive.export('candidate-saved');f.snapshot('candidate-before-reload')
   reload('candidate-roundtrip',RELOAD);drive.export('candidate-roundtrip-initial');batch('candidate-reloaded-inspector-summary');f.snapshot('candidate-after-reload');rig.probe('candidate-save-contract','f0-contract',True)
   reload('cand1-morning',run.NAME);measure(arm);off_function('cand-missing')
   rig.call('cand-missing-pause','time',dict(pause=True));config('off');reload('cand-off-morning',run.NAME);off_function('cand-off');finish(arm)
  elif arm=='base2':measure(arm);off_function('base-off-busy');finish(arm)
  elif arm=='cand2':
   measure(arm);reload('cand-open-morning',run.NAME);measure('cand2-open',True);f.capture('candidate-open-perf');close('cand2-open-close')
   config('missing');reload('cand-missing-busy-morning',run.NAME);off_function('cand-missing-busy')
   rig.call('cand-missing-busy-pause','time',dict(pause=True));config('on');reload('cand-on-morning',run.NAME);run.advance_to('cand-on-clock',10)
   f.snapshot('on-street');f.enter('on-entry');f.capture('on-inside');time.sleep(5);f.snapshot('on-held-inside')
   f.exitshop('on-exit');f.enter('on-reentry');f.exitshop('on-transfer');f.enter('on-second','ba:street_thirdstreet#23');f.capture('on-second-shop');f.exitshop('on-final-exit')
   rig.call('on-post-exit-pause','time',dict(pause=True));batch('post-interior-inspector-summary');f.capture('post-interior-popup');close('post-interior-close');f.snapshot('post-interior-final')
   finish(arm)
 except BaseException as e:
  rig.record('driver-error-'+arm,dict(error=repr(e)));raise
