from pathlib import Path
import json,re,hashlib,csv
S=Path(__file__).parent;O=S/'public-evidence';runs={};flags=[];unmapped=[]
for arm in ['base1','cand1','base2','cand2']:
 p=S/(arm+'-Player.log')
 if not p.exists():continue
 text=p.read_text(errors='replace');lines=text.splitlines();groups=[]
 for m in re.finditer(r'(?m)^(?:[A-Za-z_][\w.]*\.)?\w*Exception(?::|\s)',text):
  block=text[m.start():].split('\n\n',1)[0].split('\n[',1)[0]
  groups.append(dict(first=block.splitlines()[0],persistentCity='PersistentCity.' in block,rigObserver='PerformanceReview.' in block,stack=[l for l in block.splitlines()[1:] if l.strip().startswith('at ')][:12]))
 errors=[l for l in lines if re.search(r'Persistent City stopped:|Physical population unavailable;|\[Persistent City\].*Not active|InteriorCityView hidden fallback: exception;|inspector (?:click guard|scroll guard|ui block|failed).*unavailable',l,re.I)]
 evidence=[l for l in lines if 'InteriorCityView' in l or 'Physical population active:' in l or 'inspector' in l.lower() and 'Patch' in l]
 runs[arm]=dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),exceptionGroups=groups,persistentCityErrors=errors,ownershipInteriorInspectorMessages=evidence)
 windows={}
 for label in ([arm,arm+'-open'] if arm=='cand2' else [arm]):
  paths=list(S.glob('frames-mod-work-'+label+'-stop-*.csv'))
  if paths:windows[label]={int(x['unityFrame']) for x in csv.DictReader(paths[0].open())}
 for l in lines:
  m=re.search(r'Slow routing frame=(\d+), realtime=([\d.]+), prepared=(\w+), preparation_ms=([\d.]+), closure_ms=([\d.]+)',l)
  if m and float(m[4])+float(m[5])>=30:
   frame=int(m[1]);window=next((n for n,v in windows.items() if frame in v),'outside primary: startup/load/functional setup')
   flags.append(dict(run=arm,unityFrame=frame,realtime=float(m[2]),phase='routing preparation/closure',window=window,minimumObservedModCostMs=float(m[4])+float(m[5]),preparationMs=float(m[4]),closureMs=float(m[5])))
  m=re.search(r'([\d.]+) ms maximum advance',l)
  if m and float(m[1])>=30:unmapped.append(dict(run=arm,phase='background advancement summary',window='broad native-log observation; frame/window not exposed',maximumAdvanceMs=float(m[1])))
result=dict(role='rig_test',runs=runs,noPersistentCityErrors=not any(v['persistentCityErrors'] or any(x['persistentCity'] for x in v['exceptionGroups']) for v in runs.values()),scope='Full retained Player.log per owned process; native/other exception stacks are separate from mod defects. No deliberate exception injection was used.')
(O/'log-checks.json').write_text(json.dumps(result,indent=2))
(O/'outside-window-spikes.json').write_text(json.dumps(dict(role='rig_test',routingLowerBounds=flags,unmappedAdvanceSummaries=unmapped,scope='Measured normal-play totals/spikes are in performance-summary.json. These logged component costs are lower bounds, not whole mod-work totals. Cold startup/load/time-jump/functional work is separate from the normal-play p95 gate; summary maxima lack a frame and are retained as unaligned.'),indent=2))
print('Log runs',len(runs),'no PC errors:',result['noPersistentCityErrors'],'routing flags',len(flags),'unmapped summaries',len(unmapped))
for arm,v in runs.items():print(arm,'exceptions',len(v['exceptionGroups']),'PC',sum(x['persistentCity'] for x in v['exceptionGroups']),'error messages',len(v['persistentCityErrors']))
