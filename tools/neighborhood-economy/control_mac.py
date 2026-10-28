#!/usr/bin/env python3
"""Repeat only an invalid rendered control, retaining independently valid samples."""
from mac_rig import *

run=MacRig(os.environ['PC_FOOD_CAMPAIGN'],physical=True,mode='windowed',enabled=False)
label='native-control'
try:
 run.stage('preparing-rendered-control');run.begin()
 run.stage(label);started=time.monotonic()
 run.load(label,'FT Food Routines Mac Neighborhood Daylight 20261023',False)
 run.record(label+'-load-seconds',{'seconds':time.monotonic()-started})
 run.record(label+'-calendar-control',run.probe('clock-only-baseline'))
 run.call(label+'-player-position','teleport',{'who':'player','place':'address:ba:street_thirdstreet#30'})
 run.focus(label)
 run.call(label+'-warm','time',{'pause':False,'speed':1,'keep_needs':True});time.sleep(30)
 start=run.record(label+'-frames-start',run.probe('frames-start'))
 assert start['environment']['focused'],'Control player is not focused'
 run.record(label+'-resources-before',agent.resources())
 samples=[];end=time.monotonic()+180
 while time.monotonic()<end:
  time.sleep(min(15,max(0,end-time.monotonic())));sample=run.probe('frames-status');samples.append(sample)
  run.record(label+'-samples',samples)
  assert sample['environment']['focused'],'Control player lost focus; sample is invalid'
 run.record(label+'-frames',run.probe('frames-stop'))
 run.record(label+'-resources-after',agent.resources())
 run.call(label+'-pause','time',{'pause':True,'speed':1,'keep_needs':True})
 run.record('control-complete',{'normalSeconds':180,'populationEnabled':False,'calendarFactor':1/3})
except BaseException as error:
 run.record('error',{'type':type(error).__name__,'error':str(error)[:8000]});raise
finally:run.close()
