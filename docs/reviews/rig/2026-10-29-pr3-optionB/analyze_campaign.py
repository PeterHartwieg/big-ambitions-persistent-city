from pathlib import Path
import json,re,hashlib,statistics
s=Path(__file__).parent

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
perf=[];logs={};autosave=[]
for pair in range(1,4):
 for arm,i in [('before',pair),('after',pair)]:
  label=arm+'-qa-'+str(i);prefix=arm+'-window-'+str(i)
  stop=s/(prefix+'-stop.json');log=s/('before-all-Player-agent.log' if arm=='before' else 'after-primary-Player-agent.log')
  if stop.exists():
   r=read(stop);v=read(s/(prefix+'-validity.json'))
   perf.append(dict(arm=arm,window=i,seconds=r['realSeconds'],nativeMinutes=r['nativeGameMinutes'],mod=r['combinedModWorkPerUnityFrameMilliseconds'],wholeFrame=r['frameMilliseconds'],populationLagMinutes=r['populationLagMinutes'],timedExceptions=r['timedExceptions'],crossFrameCalls=r['crossFrameCalls'],environment=r['environment'],validity=v,componentTimedExceptions=r.get('componentTimedExceptions'),componentCrossFrameCalls=r.get('componentCrossFrameCalls')))
   a=read(s/(prefix+'-autosave-before.json'));b=read(s/(prefix+'-autosave-after.json'));reload=s/(label+'-reloaded-autosave-off.json');resumed=s/(label+'-resumed-autosave-after.json')
   autosave.append(dict(run=label,before=a,after=b,afterReload=read(reload) if reload.exists() else None,afterResumedPlay=read(resumed) if resumed.exists() else None,validity=v))
  if not log.exists() or pair!=1:continue
  text=log.read_text(errors='replace');lines=text.splitlines();exceptions=[]
  for n,line in enumerate(lines):
   if re.match(r'^(?:[A-Za-z_][\w.]*Exception):',line):
    block=[line]
    for tail in lines[n+1:]:
     if tail.startswith('  at ') or tail.startswith('Rethrow as ') or tail.startswith('---'):block.append(tail)
     else:break
    exceptions.append('\n'.join(block))
  inactive=sorted(set(line for line in lines if 'Not active:' in line or re.search(r'Patch .+: Not active',line)))
  signatures={'streetLampNRE':sum('StreetLamp' in e and 'NullReferenceException' in e for e in exceptions),'foodTrucksDetachedSave':sum('ERROR' in line and "being saved while the mod isn't attached" in line for line in lines),'saveCurrentPositionNRE':sum('SaveCurrentPosition' in e and 'NullReferenceException' in e for e in exceptions),'helperIO':sum('IOException' in e and ('FoodDemandAuditProbe' in e or 'Probe.' in e) for e in exceptions),'persistentCityExceptions':sum(' at PersistentCity.' in e for e in exceptions)}
  logs[label]=dict(arm=arm,sha256=sha(log),activePatchLines=sum(bool(re.search(r'Patch .+: Active\.',line)) for line in lines),inactiveLines=inactive,signatures=signatures,exceptions=exceptions,modErrors=[line for line in lines if re.search(r'\[Mod:[^]]+\].*(?:\bERROR\b|\bError:)',line)])
noise=[]
if len(perf)==6:
 for scope in ['mod','wholeFrame']:
  for metric in ['p50','p95','maximum']:
   a=[x[scope][metric] for x in perf if x['arm']=='before'];b=[x[scope][metric] for x in perf if x['arm']=='after']
   change=statistics.median(b)-statistics.median(a);spread=max(max(a)-min(a),max(b)-min(b))
   noise.append(dict(scope=scope,metric=metric,beforeMedian=statistics.median(a),afterMedian=statistics.median(b),change=change,beforeRange=max(a)-min(a),afterRange=max(b)-min(b),largerSpread=spread,rangesOverlap=max(min(a),min(b))<=min(max(a),max(b)),worseBeyondObservedSpread=change>spread))
(s/'performance-summary.json').write_text(json.dumps(perf,indent=2));(s/'performance-noise.json').write_text(json.dumps(noise,indent=2));(s/'autosave-evidence.json').write_text(json.dumps(autosave,indent=2));(s/'native-qa-summary.json').write_text(json.dumps(logs,indent=2))
print('windows',len(perf),'closed logs',len(logs),'noise',noise)
