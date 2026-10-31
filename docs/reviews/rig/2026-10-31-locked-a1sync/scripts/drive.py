from pathlib import Path
import sys,time,json,shutil,hashlib,psutil
import rig,run_review as run
S=Path(__file__).parent

def load(label):
    run.boot(label)
    rig.probe(label+'-environment','environment',True)

def system(label):
    processes=list(psutil.process_iter(['pid','name','memory_info','cpu_times']))
    prior={p.pid:p.info['cpu_times'].user+p.info['cpu_times'].system for p in processes if p.info['cpu_times']}
    cpu=psutil.cpu_percent(interval=1); cores=psutil.cpu_count()
    rows=[];counts={k:0 for k in ['dotnet','testhost','codex','python']}
    for p in psutil.process_iter(['pid','name','memory_info','cpu_times']):
        try:
            n=p.info['name'].lower()
            for key in counts:
                if key in n:counts[key]+=1
            c=p.info['cpu_times'];v=max(0,c.user+c.system-prior.get(p.pid,c.user+c.system))
            rows.append(dict(pid=p.pid,name=p.info['name'],cpuSecondsDelta=v,ramMiB=p.info['memory_info'].rss/2**20))
        except (psutil.NoSuchProcess,psutil.AccessDenied):pass
    mem=psutil.virtual_memory()
    return rig.record(label,dict(utc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),cpuTotalPercent=cpu,counts=counts,ramAvailableGiB=mem.available/2**30,ramTotalGiB=mem.total/2**30,topCpu=sorted(rows,key=lambda r:r['cpuSecondsDelta'],reverse=True)[:12],processRankingNote='CPU delta ranking; per-process enumeration is not a synchronized CPU percentage sample'))

def wait_load(label):
    start=time.monotonic();polls=[]
    while True:
        active=[dict(pid=p.pid,name=p.info['name']) for p in psutil.process_iter(['name']) if any(s in p.info['name'].lower() for s in ['dotnet','testhost'])]
        polls.append(dict(elapsedSeconds=time.monotonic()-start,active=active))
        rig.record(label+'-load-wait',dict(polls=polls,waitSeconds=time.monotonic()-start,underDotnetLoad=bool(active)))
        if not active or time.monotonic()-start>=900:break
        print('Waiting for external dotnet/testhost:',len(active),'elapsed',round(time.monotonic()-start),flush=True)
        time.sleep(30)

def export(label):
    result=rig.probe(label,'export',True)
    folder=rig.PROFILE/'ModsLocal/PR3RigGuard'
    payload=max(folder.glob('payload-'+label+'-*.json'),key=lambda p:p.stat().st_mtime_ns)
    ident=payload.stem[len('payload-'):]
    for prefix in ['payload-','saved-']:
        src=folder/(prefix+ident+'.json')
        if src.exists():shutil.copy2(src,S/(prefix+label+'.json'))
    return result

def measure(label,opened=False):
    wait_load(label)
    system(label+'-load-before')
    rig.call(label+'-unpause','time',dict(speed=1,pause=False,keep_needs=True))
    time.sleep(15)
    if (S/'base1-state-before.json').exists():
        target=json.loads((S/'base1-state-before.json').read_text())['minute']
        while True:
            current=rig.probe(label+'-align-'+str(time.time_ns()),'state',True)['minute']
            if current>=target:break
            time.sleep(.1)
    if opened:rig.probe(label+'-open','inspect-seam',True)
    rig.probe(label+'-inspector-reset','inspector-reset',True)
    if label!='base1':rig.probe(label+'-counters-before','counters',True)
    if label.startswith('cand2'):rig.probe(label+'-units-before','units',True)
    result=run.measure(label,60)
    rig.call(label+'-pause','time',dict(pause=True))
    system(label+'-load-after')
    if label!='base1':rig.probe(label+'-counters-after','counters',True)
    if label.startswith('cand2'):rig.probe(label+'-units-after','units',True)
    rig.probe(label+'-inspector-timing','inspector',True)
    rig.probe(label+'-status','status')
    rig.record(label+'-clock-after',rig.health())
    return result

if __name__=='__main__':
    arm=sys.argv[1];load(arm)
    if len(sys.argv)>2 and sys.argv[2]=='boot':
        export(arm+'-initial');print('Booted and paused',arm,flush=True)
    else:
        measure(arm)
        export(arm+'-end')
        if arm=='base1':
            t=time.monotonic();r=rig.call('schema16-save','save',dict(name='FT Food Routines Locked A3 Schema16 20261031'))
            rig.record('schema16-save-wall',dict(seconds=time.monotonic()-t,result=r));export('schema16-saved')
        if arm=='base2':
            rig.call('baseline-lunch-until','time',dict(speed=6,run_until=dict(day='5',time='12:30'),keep_needs=True))
            while not rig.health()['paused']:time.sleep(1)
            run.wait_ready('baseline-lunch')
            t=time.monotonic();r=rig.call('schema16-lunch-save','save',dict(name='FT Food Routines Locked A3 Legacy Lunch 20261031'))
            rig.record('schema16-lunch-save-wall',dict(seconds=time.monotonic()-t,result=r));export('schema16-lunch-saved')
            rig.probe('baseline-codec','read-saved',True)
        if not arm.startswith('cand'):
            shutil.copy2(Path('C:/BigAmbitions-AgentTests/Player-agent.log'),S/(arm+'-Player.log'))
            rig.call(arm+'-quit','quit')
