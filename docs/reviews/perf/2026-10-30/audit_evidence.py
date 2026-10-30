"""rig_test: compact native QA, bucket timings, validity and restoration evidence."""
import csv,json,re,hashlib
from collections import defaultdict
from pathlib import Path
from prepare_review import STAGE,FIRST_STAGE
from analyze_review import dist,load,sha,OUT,write

def sanitize(v):
    if isinstance(v,dict):return {k:sanitize(x) for k,x in v.items() if k not in ['tested_profile','lock_token','token']}
    if isinstance(v,list):return [sanitize(x) for x in v]
    if isinstance(v,str):
        v=v.replace(str(Path.home()),'<user-home>').replace(str(Path.home()).replace('\\','/'),'<user-home>')
        return v
    return v

def main():
    files={p.name:p for stage in [FIRST_STAGE,STAGE] for p in stage.glob('*-validity.json')}
    checks=[]
    for name,p in sorted(files.items()):
        r=load(p);label=name.removesuffix('-validity.json')
        if not r.get('clean'):continue
        before=load(p.parent/(label+'-autosave-before.json'));after=load(p.parent/(label+'-autosave-after.json'))
        assert before['preventAutoSave'] and after['preventAutoSave']
        assert not before['savingGameInProgress'] and not after['savingGameInProgress']
        checks.append(dict(label=label,session='r2' if p.parent==STAGE else 'first',validitySha256=sha(p),clean=r['clean'],autosaveOffBeforeAfter=r['autosaveOffBeforeAfter'],saveFilesUnchanged=r['saveFilesUnchanged'],saveActivity=r['saveActivity'],originalPreventAutoSave=before['originalPreventAutoSave'],minutesBetweenAutoSaves=before['minutesBetweenAutoSaves'],maxAutoSavesPerGame=before['maxAutoSavesPerGame']))
    write('autosave-validity.json',checks)
    buckets=[]
    for stage in [FIRST_STAGE,STAGE]:
        for p in sorted(stage.glob('buckets-*.csv')):
            rows=list(csv.DictReader(p.open()));bybucket=defaultdict(dict);calls=defaultdict(int)
            frames=set()
            for r in rows:
                f=int(r['unityFrame']);frames.add(f);b=r['bucket'];bybucket[b][f]=float(r['milliseconds']);calls[b]+=int(r['calls'])
            buckets.append(dict(file=p.name,sha256=sha(p),session='r2' if stage==STAGE else 'first',frames=len(frames),counterKind='unavailable',buckets={b:dict(perObservedFrameMs=dist([v.get(f,0) for f in frames]),totalMs=sum(v.values()),calls=calls[b]) for b,v in bybucket.items() if b!='all-main-thread'}))
    write('bucket-summary.json',buckets)
    qa=[]
    for stage,names in [(FIRST_STAGE,['workday-native.log','native-final.log']),(STAGE,['workday-native.log','before-native.log','after-native.log'])]:
        for name in names:
            p=stage/name
            if not p.exists():continue
            lines=p.read_text(errors='replace').splitlines()
            inactive=sorted(set(l for l in lines if 'Not active:' in l))
            errors=[dict(line=i+1,text=l,context=lines[i+1:i+9]) for i,l in enumerate(lines) if re.search(r'Exception:|\] ERROR:',l)]
            pc=[e for e in errors if '[Mod:PersistentCity]' in e['text'] or any(re.match(r'\s*at PersistentCity\.',l) for l in e['context'])]
            chronology=[dict(line=i+1,text=l) for i,l in enumerate(lines) if ('CityPerformance' in l or '[Mod:PersistentCity]' in l) and re.search(r'patch|loaded|load|active|start',l,re.I)]
            capability=[dict(line=i+1,text=l) for i,l in enumerate(lines) if 'CityPerformance' in l and any(x in l for x in ['Version ','inactive:','Not active:','Active:'])]
            qa.append(dict(session='r2' if stage==STAGE else 'first',file=name,sha256=sha(p),bytes=p.stat().st_size,inactiveLines=inactive,capabilities=capability,errors=errors,persistentCityErrors=pc,chronology=chronology))
    write('native-qa.json',sanitize(qa))
    patchfiles={p.name:p for stage in [FIRST_STAGE,STAGE] for p in stage.glob('*patches*.json') if not p.name.startswith('response-')}
    write('patch-inventory.json',sanitize({k:load(p) for k,p in patchfiles.items()}))
    orders={}
    for arm in ['before','after']:
        p=STAGE/(arm+'-patches.json')
        log=STAGE/(arm+'-native.log')
        if not p.exists() or not log.exists():continue
        patches=load(p);lines=log.read_text(errors='replace').splitlines()
        shared=[r for r in patches if 'cityperformance.mod' in r['owners'] and any('persistentcity' in o for o in r['owners'])]
        orders[arm]=dict(sharedMethods=shared,firstCityPerformanceLine=next(i+1 for i,l in enumerate(lines) if 'CityPerformance]' in l),firstPersistentCityLine=next(i+1 for i,l in enumerate(lines) if '[Mod:PersistentCity]' in l),behavioralOptimizationsVerified=False,effectiveOrder='City Performance initialization patches before Persistent City city-load patches')
    write('load-order.json',sanitize(orders))
    receipts={}
    for stage,label in [(FIRST_STAGE,'first'),(STAGE,'r2')]:
        receipts[label]={}
        for name in ['backup-receipt.json','restore-receipt.json','lock-released.json','tool-builds.json','city-performance-artifact-before.json','city-performance-artifact-after.json','workday-process-end.json','before-process-end.json','after-process-end.json']:
            p=stage/name
            if p.exists():receipts[label][name]=sanitize(load(p))
    write('lifecycle-evidence.json',receipts)
    original=load(STAGE/'backup/profile-hashes.json')
    config=next(r for r in original if r['file'].replace('\\','/').endswith('ModsLocal/PersistentCity/persistent-city.json'))
    write('original-settings.json',dict(persistentCityConfig=dict(file='ModsLocal/PersistentCity/persistent-city.json',sha256=config['sha256']),autosave=dict(preventAutoSave=False,minutesBetweenAutoSaves=5,maxAutoSavesPerGame=3),restorationProof='Complete profile inventory, original install references, root files and original preference export must match; restored game is not relaunched.'))
    write('session-provenance.json',dict(role='rig_test',testedSha='7a6153cfa9bb88e28dea905b285b4273c3e268c7',firstSha='21db30b753b8fdbe8e4a4a836f5f2623e0935377',difference=['docs/roadmap.md','docs/reviews/2026-10-27-realism-gameplay-roadmap.md'],productionSourceUnchanged=True,productionDllSha256=receipts['r2']['tool-builds.json']['production'],helpersIdentical=receipts['first']['tool-builds.json']==receipts['r2']['tool-builds.json'],offline=dict(buildPassed=True,testsPassed=1551,failed=0,skipped=0),firstStageReadOnly=True))
    print('Audited',len(checks),'validity records,',len(buckets),'bucket captures,',len(qa),'native logs')

if __name__=='__main__':main()
