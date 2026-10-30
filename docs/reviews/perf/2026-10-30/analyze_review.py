"""rig_test evidence reduction. Full raw files stay private; compact summaries are reviewable."""
import csv,gzip,hashlib,json,math,statistics,re,sys
from collections import defaultdict
from pathlib import Path
from prepare_review import STAGE,FIRST_STAGE,ROOT
OUT=Path(__file__).parent

def load(p):
    raw=p.read_bytes()
    return json.loads(raw.decode('utf-16' if raw[:2] in [b'\xff\xfe',b'\xfe\xff'] else 'utf-8-sig'))
def dist(values):
    s=sorted(values);n=len(s)
    return dict(samples=n,mean=sum(s)/n if n else None,**{k:s[min(n-1,int(n*q))] if n else None for k,q in [('p50',.5),('p95',.95),('p99',.99),('maximum',1)]})
def leaf(s):return str(s).replace('\\','/').split('/')[-1]
def csvrows(p):return list(csv.DictReader(p.open(newline='')))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name,v):(OUT/name).write_text(json.dumps(v,indent=2)+'\n')

def analyze():
    windows=[];groups=defaultdict(lambda:dict(wall=[],mod=[],components=defaultdict(list),gc=[0,0,0],seconds=0,windows=[],over33=0,over100=0,mod33=0,mod50=0,mod100=0))
    raw_manifest=[];spikes=[];compact=[]
    captures={p.name:p for stage in [FIRST_STAGE,STAGE] for p in stage.glob('*-stop.json')}
    for p in sorted(captures.values(),key=lambda p:p.name):
        source=p.parent
        r=load(p)
        if 'combinedModWorkPerUnityFrameMilliseconds' not in r:continue
        label=p.stem.removesuffix('-stop');vpath=source/(label+'-validity.json')
        if not vpath.exists():continue
        v=load(vpath);assert v['clean'],label
        mpath=source/leaf(r['perFrameEvidence']);opath=source/leaf(r['observationEvidence'])
        mods=csvrows(mpath);obs=csvrows(opath);freq=r['rawObservations']['stopwatchFrequency']
        assert len(mods)==r['completedUnityFrames'] and len(obs)==r['rawObservations']['Count']
        assert r['stopReason']=='frames-stop' and r['realSeconds']>=60
        assert not any(r.get(k) for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls','droppedModFrames','activeReportCount'])
        assert not r['rawObservations']['Dropped']
        assert not any(r['rawObservations'].get(k,0) for k in ['Duplicates','InvalidOrder','SkippedUnityFrames'])
        assert not v['after']['failed']
        assert all(float(o['timeScale'])==1 and o['paused']=='0' for o in obs),label
        for path in [mpath,opath]:raw_manifest.append(dict(label=label,session='r2' if source==STAGE else 'first',file=path.name,sha256=sha(path),bytes=path.stat().st_size))
        whole=[(int(o['endTicks'])-int(o['startTicks']))*1000/freq for o in obs]
        mod=[float(m['combinedUnionMs']) for m in mods]
        # Existing helper uses floor(n*q), with initial partial wall interval retained historically.
        for field,values in [('frameMilliseconds',whole),('combinedModWorkPerUnityFrameMilliseconds',mod)]:
            d=dist(values)
            for k in ['p50','p95','p99','maximum']:assert abs(d[k]-r[field][k])<.00011,(label,field,k)
        minute_values=[float(o['nativeMinute']) for o in obs if o['nativeMinute']]
        allocations=[float(m['managedDeltaBytes']) for m in mods if m['managedDeltaAvailable']=='1']
        actual_alloc=r['managedCounterKind']=='thread-allocated-bytes'
        allocations_summary=dict(counterKind=r['managedCounterKind'],allocatedBytes=dist(allocations) if actual_alloc else None,totalAllocatedBytes=sum(allocations) if actual_alloc else None,signedHeapDelta=dist(allocations) if not actual_alloc else None,note='Signed live-heap change is not allocated bytes. GC generations may alias on Mono; never add their counts.')
        times=dict(firstMinute=min(minute_values) if minute_values else None,lastMinute=max(minute_values) if minute_values else None)
        comp={c['name']:c['inclusivePerUnityFrameMilliseconds'] for c in r['components']}
        w=dict(label=label,seconds=r['realSeconds'],wholeFrame=r['frameMilliseconds'],modWork=r['combinedModWorkPerUnityFrameMilliseconds'],modOver5=r['combinedFramesOver5ms'],wholeOver33=sum(x>33 for x in whole),wholeOver100=sum(x>100 for x in whole),modOver33=sum(x>33 for x in mod),modOver100=sum(x>100 for x in mod),gc=r['collections'],allocations=allocations_summary,components=comp,environment=r['environment'],physical=r['physical'],lag=r['populationLagMinutes'],times=times,clean=True,extra=v['extraInstrumentation'],attributionCounters={k:r.get(k) for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls','eventMeasurementFailures','eventCrossFrameCalls']})
        windows.append(w)
        w['reportedCollectionsAfterExport']=r['collections']
        w['gc']=[sum(int(float(o['gc'+str(i)+'Delta'] or 0)) for o in obs if o['gcAvailable']=='1') for i in range(3)]
        w['gcInFirstPartial']=[int(float(obs[0]['gc'+str(i)+'Delta'] or 0)) for i in range(3)]
        w['modOver50']=sum(x>50 for x in mod)
        arm,condition=label.rsplit('-',1)[0],label.rsplit('-',1)[1]
        if condition.isdigit():arm,condition=label.rsplit('-',2)[:2]
        else:arm=label
        key=arm+'-'+condition if condition.isdigit() else label
        if not w['extra'] and label.rsplit('-',1)[-1].isdigit():key=label.rsplit('-',1)[0]
        g=groups[key];g['wall']+=whole;g['mod']+=mod;g['seconds']+=r['realSeconds'];g['windows'].append(label)
        g['gc']=[a+b for a,b in zip(g['gc'],w['gc'])]
        g['over33']+=w['wholeOver33'];g['over100']+=w['wholeOver100'];g['mod33']+=w['modOver33'];g['mod50']+=w['modOver50'];g['mod100']+=w['modOver100']
        for name in comp:g['components'][name]+=[float(m[name+'InclusiveMs']) for m in mods]
        byid={int(m['unityFrame']):m for m in mods}
        for index,o in enumerate(obs):
            wall=whole[index]
            gc=[int(float(o['gc'+str(i)+'Delta'])) if o['gc'+str(i)+'Delta'] else 0 for i in range(3)]
            if wall<=33 and not any(gc):continue
            neighbors=[byid[f] for f in range(int(o['priorUnityFrame'])-1,int(o['currentUnityFrame'])+2) if f in byid]
            context=[]
            for m in neighbors:
                components={k:float(val) for k,val in m.items() if k.endswith('InclusiveMs')}
                dominant=max(components,key=components.get)
                context.append(dict(frame=int(m['unityFrame']),modMs=float(m['combinedUnionMs']),dominant=dominant,dominantMs=components[dominant],event={k:m.get(k) for k in ['entryBranch','eventMs','walkingCalculationMs','roadSearchSliceMs','navMeshPathMs','aStarAdvanceMs']},heapDelta=m.get('managedDeltaBytes')))
            spikes.append(dict(window=label,wallMs=wall,gc=gc,priorFrame=int(o['priorUnityFrame']),currentFrame=int(o['currentUnityFrame']),nativeMinute=o['nativeMinute'],partialStart=index==0,neighbors=context))
        # Preserve all main primary per-frame data in compact compressed, joined tables.
        if label.startswith(('workday-','before-','after-')) and not w['extra']:
            import io
            b=io.StringIO();writer=csv.writer(b)
            writer.writerow(['wholeFrameMs','priorUnityFrame','currentUnityFrame','gc0Delta','gc1Delta','gc2Delta','nativeMinute'])
            for index,o in enumerate(obs):writer.writerow([format(whole[index],'.4f')]+[o[k] for k in ['priorUnityFrame','currentUnityFrame','gc0Delta','gc1Delta','gc2Delta']]+[format(float(o['nativeMinute']),'.4f') if o['nativeMinute'] else ''])
            observed=b.getvalue().encode()
            (OUT/(label+'-observations.csv.gz')).write_bytes(gzip.compress(observed,mtime=0))
            keys=['unityFrame','combinedUnionMs','PhysicalPopulation.UpdateInclusiveMs','PopulationRuntime.AdvancePopulationInclusiveMs','PhysicalVehicles.UpdateInclusiveMs','CitySimulator.RefreshTrafficClosuresInclusiveMs','PhysicalTrafficHooks.FixedStepPostfixInclusiveMs','entryBranch','eventMs','navMeshPathMs','aStarAdvanceMs']
            dest=OUT/(label+'-frames.csv.gz')
            b=io.StringIO();writer=csv.writer(b);writer.writerow(['window']+keys)
            for m in mods:writer.writerow([label]+[m.get(k,'') for k in keys])
            dest.write_bytes(gzip.compress(b.getvalue().encode(),mtime=0))
    summaries={}
    for key,g in groups.items():
        summaries[key]=dict(windows=g['windows'],seconds=g['seconds'],wholeFrame=dist(g['wall']),modWork=dist(g['mod']),wholeOver33=g['over33'],wholeOver100=g['over100'],modOver33=g['mod33'],modOver50=g['mod50'],modOver100=g['mod100'],gc=g['gc'],components={k:dist(v) for k,v in g['components'].items()},worstWindowP95=max(w['modWork']['p95'] for w in windows if w['label'] in g['windows']))
    write('windows.json',windows);write('summary.json',summaries);write('spikes-and-gc.json',spikes);write('raw-provenance.json',raw_manifest)
    write('analysis-checks.json',dict(verifiedWindows=len(windows),allClean=True,percentilesRecomputed=True,countsMatch=True,method='floor(n*q), inclusive component percentiles never summed; GC coincidence is not pause attribution'))
    print('Summaries:',len(windows),'windows; evidence MB',sum(p.stat().st_size for p in OUT.rglob('*') if p.is_file())/1e6)

if __name__=='__main__':analyze()
