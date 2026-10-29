import sys,time,json,shutil,subprocess
from pathlib import Path
import rig
from schema_native_helpers import unload,load
config=rig.PROFILE/'ModsLocal/PersistentCity/persistent-city.json'
original=config.read_bytes();(rig.STAGE/'rate-config-original.private.json').write_bytes(original)
base=json.loads(original)
def configure(label,rate,name='FT Food Routines PR3 OB20261029'):
 unload(label)
 d=dict(base);d.pop('OptionalStopChoicePercentage',None)
 if rate is not None:d['OptionalStopChoicePercentage']=rate
 config.write_text(json.dumps(d,indent=2));load(label,name)
 r=rig.probe(label+'-opening','optional-status',True)
 print(label,'opening',r['audit'],flush=True)
 return r
def skip(label,hours):
 h=rig.health();clock=h['time'].split(':');m=h['day']*1440+int(clock[0])*60+int(clock[1])
 a=rig.probe(label+'-autosave-before','autosave-check',True);assert a['preventAutoSave'] and not a['savingGameInProgress']
 rig.call(label+'-skip','console',{'line':f'skiptime {hours}h true'})
 while True:
  h=rig.health();clock=h['time'].split(':');now=h['day']*1440+int(clock[0])*60+int(clock[1])
  if now>=m+hours*60-1 and h.get('time_skip') is False:break
  time.sleep(.5)
 rig.call(label+'-pause','time',{'pause':True})
 while not rig.probe(label+'-caught-'+str(time.time_ns()),'status')['caughtUp']:time.sleep(.5)
 a=rig.probe(label+'-autosave-after','autosave-check',True);assert a['preventAutoSave'] and not a['savingGameInProgress']
 return rig.probe(label+'-closing','optional-status',True)
def run(label,days,save=None):
 rig.probe(label+'-trace-start','optional-start',True)
 rig.probe(label+'-needs-start','skip-needs-start')
 try:
  for day in range(1,days+1):
   r=skip(label+f'-d{day}',24)
   print(label,day,r['audit'],flush=True)
 finally:
  rig.probe(label+'-needs-end','skip-needs-end')
  end=rig.probe(label+'-trace-end','optional-end',True)
  src=Path(end['output']);shutil.copy2(src,rig.STAGE/src.name)
 if save:
  rig.probe(label+'-pre-save','capture',True)
  rig.call(label+'-save','save',{'name':save})
  rig.probe(label+'-saved','capture',True)
 return r
if __name__=='__main__':
 try:
  mode=sys.argv[1] if len(sys.argv)>1 else 'rates'
  if mode in ['rates','rates-remaining']:
   cases=[('rate25',None),('rate100',100),('rate0',0),('rate10',10),('rate50',50)]
   if mode=='rates-remaining':cases=cases[1:]
   for label,rate in cases:
    configure(label,rate);run(label,10,'FT Food Routines PR3 OB Rate'+str(25 if rate is None else rate))
  elif mode=='decreases':
   for label,opening,target in [('dec100-25',100,25),('dec100-0-25',100,0),('dec50-25',50,25),('dec50-10',50,10)]:
    configure(label,target,'FT Food Routines PR3 OB Rate'+str(opening))
    if target==0:
     run(label+'-zero',2,'FT Food Routines PR3 OB ViaZero');configure(label+'-return',25,'FT Food Routines PR3 OB ViaZero')
    run(label,10,'FT Food Routines PR3 OB '+label+' End')
  elif mode=='controls':
   for rate in [25,10]:
    label='constant'+str(rate);configure(label,rate,'FT Food Routines PR3 OB Rate'+str(rate));run(label,12)
 finally:
  unload('rates-restore');config.write_bytes(original)
