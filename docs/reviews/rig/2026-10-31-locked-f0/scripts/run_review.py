"""Foreground native measurement driver. No production changes, saved simulations only advance normally."""
import sys,time,json,re,shutil
from pathlib import Path
from prepare_review import STAGE,PROFILE
sys.path.insert(0,str(STAGE))
import rig

NAME='FT Food Routines Locked F0 20261031'
LOG=Path('C:/BigAmbitions-AgentTests/Player-agent.log')
def probe(label,op,guard=False): return rig.probe(label,op,guard)
def state(label): return probe(label,'state',True)
def wait_ready(label):
    while True:
        r=probe(label+'-runtime-'+str(time.time_ns()),'runtime-state')
        if r.get('failed'):raise RuntimeError(r)
        if r.get('discoveryReady') and str(r.get('status','')).startswith('Persistent population active:'):break
        time.sleep(1)
    while True:
        try:
            r=probe(label+'-caught-'+str(time.time_ns()),'status')
            if r.get('caughtUp'):break
        except RuntimeError as e:
            if 'Population not ready' not in str(e):raise
        time.sleep(1)
    return state(label+'-ready')
def boot(arm):
    while True:
        try:
            h=rig.health()
            if h['scene'] in ['menu','city']:break
        except Exception:pass
        time.sleep(1)
    if h['scene']!='city':rig.call(arm+'-load','load',{'name':NAME})
    rig.waitcity(arm+'-loaded',NAME)
    while rig.health().get('time_skip'):time.sleep(.5)
    rig.call(arm+'-pause','time',{'pause':True})
    probe(arm+'-autosave-off','autosave-off',True)
    r=wait_ready(arm)
    ui=rig.call(arm+'-ui','ui',{'op':'list'})
    for b in ui['buttons']:
        if b['path'].endswith('Campaign menu/Icon button'):rig.call(arm+'-close-campaign','ui',{'op':'click','id':b['id']})
    try:rig.call(arm+'-escape','ui',{'op':'escape'})
    except RuntimeError as e:
        if 'Nothing is open' not in str(e):raise
    probe(arm+'-patches','patches',True)
    print('READY',arm,r,flush=True)

def advance_to(label,hour):
    h=rig.health();hours=int(h['time'].split(':')[0]);delta=(hour-hours)%24
    if delta:
        start=h['day']*1440+hours*60+int(h['time'].split(':')[1])
        rig.call(label+'-skip','console',{'line':f'skiptime {delta}h true'})
        while True:
            h=rig.health();now=h['day']*1440+int(h['time'].split(':')[0])*60+int(h['time'].split(':')[1])
            if now>=start+delta*60-1 and not h.get('time_skip'):break
            time.sleep(.5)
    rig.call(label+'-pause','time',{'pause':True})
    probe(label+'-autosave-off','autosave-off',True)
    wait_ready(label)
    rig.call(label+'-normal','time',{'speed':1,'pause':False,'keep_needs':True})
    time.sleep(15)

def saves():return {str(p.relative_to(PROFILE)):(p.stat().st_size,p.stat().st_mtime_ns) for p in (PROFILE/'SaveGames').rglob('*.hsg')}
def measure(label,seconds=60,extra=False):
    a=probe(label+'-autosave-before','autosave-check',True)
    assert a['preventAutoSave'] and not a['savingGameInProgress']
    h=rig.record(label+'-health-before',rig.health());assert not h['paused']
    before=saves();offset=LOG.stat().st_size
    rig.record(label+'-state-before',state(label+'-state-pre'))
    if extra:probe(label+'-extra-start','extra-start',True)
    start=probe(label+'-start','frames-start')
    print('START',label,flush=True)
    time.sleep(seconds)
    r=probe(label+'-stop','frames-stop')
    if extra:probe(label+'-extra-stop','extra-stop',True)
    b=probe(label+'-autosave-after','autosave-check',True)
    with LOG.open('rb') as f:f.seek(offset);segment=f.read().decode('utf-8','replace')
    (STAGE/(label+'-window.log')).write_text(segment)
    actual=[l for l in segment.splitlines() if re.search(r"Test bridge: (saving as|saved ')|SaveGame.*(?:started|in progress)|AutoSave.*(?:triggered|started)",l,re.I)]
    clean=bool(a['preventAutoSave'] and b['preventAutoSave'] and not b['savingGameInProgress'] and before==saves() and not actual)
    rig.record(label+'-validity',dict(clean=clean,autosaveOffBeforeAfter=a['preventAutoSave'] and b['preventAutoSave'],saveFilesUnchanged=before==saves(),saveActivity=actual,extraInstrumentation=extra,start=start,after=state(label+'-state-post')))
    assert clean,'Saving invalidated the window'
    print('DONE',label,'seconds',r['realSeconds'],'whole',r['frameMilliseconds'],'mod',r['combinedModWorkPerUnityFrameMilliseconds'],flush=True)
    assert r['realSeconds']>=60
    assert not any(r.get(k) for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls','droppedModFrames','activeReportCount'])
    return r

def campaign(arm):
    boot(arm)
    for condition,hour in [('morning',7),('normal',12),('evening',15)]:
        if all((STAGE/f'{arm}-{condition}-{i}-validity.json').exists() for i in range(1,4)):
            print('Retaining completed condition',arm,condition,flush=True);continue
        advance_to(arm+'-'+condition,hour)
        for i in range(1,4):measure(f'{arm}-{condition}-{i}')
        rig.call(arm+'-'+condition+'-end-pause','time',{'pause':True})
        rig.record(arm+'-'+condition+'-end',state(arm+'-'+condition+'-state'))
    if arm=='alone':
        # Extra hooks run separately, leaving the primary comparable windows unchanged.
        rig.call('allocation-unpause','time',{'speed':1,'pause':False,'keep_needs':True})
        measure('alone-allocation-detail',extra=True)
        rig.call('save-pause','time',{'pause':True})
        probe('dedicated-save-start','extra-start',True)
        t=time.monotonic()
        result=rig.call('dedicated-save','save',{'name':'FT Food Routines Performance Review Saved 20261030'})
        rig.record('dedicated-save-wall',dict(seconds=time.monotonic()-t,result=result))
        probe('dedicated-save-stop','extra-stop',True)
        probe('dedicated-save-autosave','autosave-check',True)
        rig.call('dedicated-reload','load',{'name':'FT Food Routines Performance Review Saved 20261030'})
        rig.waitcity('dedicated-reloaded','FT Food Routines Performance Review Saved 20261030')
        rig.call('dedicated-reload-pause','time',{'pause':True})
        probe('dedicated-reload-autosave-off','autosave-off',True)
        wait_ready('dedicated-reload')
    probe(arm+'-final-patches','patches',True)
    rig.call(arm+'-quit','quit')

if __name__=='__main__':
    try:campaign(sys.argv[1])
    except BaseException as e:
        rig.record('campaign-error-'+sys.argv[1],dict(error=repr(e)))
        raise
