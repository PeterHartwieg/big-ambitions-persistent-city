#!/usr/bin/env python3
"""Matched rendered Mac runs with normal body limits; no Windows access."""
from mac_rig import *
resume=os.environ.get('PC_RENDER_RESUME')=='1'
cases=[('native-control',False),('candidate-b',True)] if resume else [('candidate-a',True),('native-control',False),('candidate-b',True)]
run=MacRig(os.environ.get('PC_FOOD_CAMPAIGN','neighborhood-render-mac-20261023'),physical=True,mode='windowed',enabled=cases[0][1])
try:
 run.stage('preparing-rendered-mac');run.begin()
 checkpoint='FT Food Routines Mac Neighborhood Daylight 20261023'
 if not resume:
  run.stage('daylight-and-skip')
  initial=run.record('catchup-before',run.load('initial','FT Food Routines Mac Neighborhood one-small Day 2 20261023'))
  run.record('catchup-frames-start',run.probe('frames-start'))
  run.record('catchup-needs-start',run.probe('skip-needs-start'));started=time.monotonic()
  run.call('native-nine-hour-skip','console',{'line':'skiptime 9h true'});time.sleep(2)
  after=run.record('catchup-after',run.ready());run.record('catchup-needs-end',run.probe('skip-needs-end'))
  run.record('catchup-frames',run.probe('frames-stop'))
  run.record('catchup-result',{'seconds':time.monotonic()-started,'start':initial['minute'],'end':after['minute'],
     'finalLag':after['nativeMinute']-after['minute']})
  run.call('save-daylight','save',{'name':checkpoint})
 for label,enabled in cases:
  run.stage(label)
  if run.enabled!=enabled:run.population_mode(enabled)
  started=time.monotonic();run.load(label,checkpoint,enabled)
  run.record(label+'-load-seconds',{'seconds':time.monotonic()-started})
  if not enabled:run.record(label+'-calendar-control',run.probe('clock-only-baseline'))
  run.call(label+'-player-position','teleport',{'who':'player','place':'address:ba:street_thirdstreet#30'})
  run.focus(label)
  run.call(label+'-warm','time',{'pause':False,'speed':1,'keep_needs':True});time.sleep(30)
  frameStart=run.record(label+'-frames-start',run.probe('frames-start'))
  assert frameStart['environment']['focused'],('Rendered player is not focused',label)
  run.record(label+'-resources-before',agent.resources())
  samples=[];end=time.monotonic()+180
  while time.monotonic()<end:
   time.sleep(min(15,max(0,end-time.monotonic())));samples.append(run.probe('frames-status'))
  run.record(label+'-frames',run.probe('frames-stop'));run.record(label+'-samples',samples)
  run.record(label+'-resources-after',agent.resources())
  run.call(label+'-pause','time',{'pause':True,'speed':1,'keep_needs':True})
  if enabled:
   run.record(label+'-state',run.ready());run.record(label+'-economy',run.probe('economy-report'))
 run.stage('fast-forward-and-resume')
 current=run.ready();target=int(current['nativeMinute'])+120
 run.record('fast-forward-frames-start',run.probe('frames-start'))
 run.call('fast-forward-two-hours','time',{'run_until':{'day':target//1440,'time':f'{target%1440//60:02d}:{target%60:02d}'},'speed':1,'keep_needs':True})
 run.probe('fast-forward');end=time.monotonic()+600
 while not agent.health().get('paused'):
  if time.monotonic()>end:raise RuntimeError('Fast-forward timeout')
  time.sleep(2)
 run.probe('normal');run.ready();run.record('fast-forward-frames',run.probe('frames-stop'))
 run.call('resume-physical','time',{'pause':False,'speed':1,'keep_needs':True});time.sleep(15)
 run.record('physical-resumed',run.probe('frames-status'))
 run.call('pause-after-resume','time',{'pause':True,'speed':1,'keep_needs':True});run.ready()
 run.stage('live-hours-and-management')
 run.record('live-hours-check',run.probe('hours-refresh-check'))
 run.record('management-screenshot',run.probe('panel-screenshot'));time.sleep(3)
 shutil.copy2(run.helper/'neighborhood-management.png',run.root/'neighborhood-management.png')
 run.stage('save-and-reload')
 before=run.record('save-reload-before',run.probe('status'));started=time.monotonic()
 checkpoint='FT Food Routines Mac Neighborhood Rendered 20261023'
 run.call('save-rendered','save',{'name':checkpoint});run.record('save-timing',{'seconds':time.monotonic()-started})
 started=time.monotonic();after=run.record('save-reload-after',run.load('reload-rendered',checkpoint))
 run.record('reload-timing',{'seconds':time.monotonic()-started})
 for key in ['openingSavings','income','essentials','householdFood','heldPurchases','arrears']:assert before['economy'][key]==after['economy'][key],('reload changed',key)
 assert before['economy']['balances']+before['economy']['preparedFood']==after['economy']['balances']+after['economy']['preparedFood']
 run.record('render-complete',{'cases':3,'normalSecondsPerCase':180,'platform':'Mac','visibleLimits':{'pedestrians':48,'vehicles':24},'normalProfileTouched':False})
except BaseException as error:
 run.record('error',{'type':type(error).__name__,'error':str(error)[:8000]});raise
finally:run.close()
