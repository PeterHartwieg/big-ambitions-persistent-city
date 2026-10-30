"""rig_test workday campaign: three clean 60 s windows at each daily peak and noon."""
import sys,time
from pathlib import Path
import run_review as review
from prepare_review import STAGE,FIRST_STAGE
import json
rig=review.rig

def until(label,day,hour,minute):
    target=day*1440+hour*60+minute
    h=rig.health();now=h['day']*1440+int(h['time'].split(':')[0])*60+int(h['time'].split(':')[1])
    assert target>now,(target,now)
    hours=int((target-now)//60)
    if hours:
        rig.call(label+'-coarse','console',{'line':f'skiptime {hours}h true'})
        while True:
            h=rig.health();n=h['day']*1440+int(h['time'].split(':')[0])*60+int(h['time'].split(':')[1])
            if n>=now+hours*60-1 and not h.get('time_skip'):break
            time.sleep(.5)
        time.sleep(2)  # Native time-machine result screen/fade is outside every capture.
        try:rig.call(label+'-skip-close','ui',{'op':'escape'})
        except RuntimeError as e:
            if 'Nothing is open' not in str(e):raise
    rig.call(label+'-fine','time',{'speed':6,'run_until':{'day':str(day),'time':f'{hour:02}:{minute:02}'},'keep_needs':True})
    while not rig.health()['paused']:time.sleep(.5)
    review.wait_ready(label)
    rig.call(label+'-normal','time',{'speed':1,'pause':False,'keep_needs':True})
    time.sleep(15)

def run(arm,detail):
    review.boot(arm)
    review.probe(arm+'-needs-start','skip-needs-start')
    for condition,hour,minute in [('morning',7,0),('normal',12,0),('evening',15,30)]:
        if arm=='workday' and all((FIRST_STAGE/f'{arm}-{condition}-{i}-validity.json').exists() and json.loads((FIRST_STAGE/f'{arm}-{condition}-{i}-validity.json').read_text())['clean'] for i in range(1,4)):
            print('Retaining verified first-run condition',arm,condition,flush=True)
            continue
        # Day 8 is a Monday under the actual Core weekday policy ((day-1)%7).
        until(arm+'-'+condition,8,hour,minute)
        for i in range(1,4):review.measure(f'{arm}-{condition}-{i}')
        if detail:review.measure(f'{arm}-{condition}-detail',extra=True)
        rig.call(arm+'-'+condition+'-pause','time',{'pause':True})
    review.probe(arm+'-patches-end','patches',True)
    if detail:
        try:rig.shot(arm+'-native-view')
        except Exception as e:rig.record(arm+'-native-view-error',dict(error=repr(e)))
    if detail:
        review.probe('workday-save-start','extra-start',True)
        t=time.monotonic()
        r=rig.call('workday-save','save',{'name':'FT Food Routines Performance Review Workday 20261030'})
        rig.record('workday-save-wall',dict(seconds=time.monotonic()-t,result=r))
        review.probe('workday-save-stop','extra-stop',True)
        review.probe('workday-save-autosave','autosave-check',True)
    review.probe(arm+'-needs-end','skip-needs-end')
    rig.call(arm+'-quit','quit')

if __name__=='__main__':
    arm=sys.argv[1]
    try:run(arm,arm=='workday')
    except BaseException as e:
        rig.record('workday-error-'+arm,dict(error=repr(e)))
        try:rig.call(arm+'-error-ui','ui',{'op':'list'})
        except Exception:pass
        raise
