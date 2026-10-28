from pathlib import Path
import json,re,hashlib
s=Path(__file__).parent
rows=[];perfs=[];excerpts=[]
for label,arm in [('A1','before'),('B1','after'),('A2','before'),('B2','after'),('A3','before'),('B3','after')]:
 p=s/(label+'-controlled-native-window.log')
 if not p.exists():continue
 text=p.read_text(errors='replace');lines=text.splitlines()
 def inventory(text):
  lines=text.splitlines();indices=[i for i,line in enumerate(lines) if re.match(r'^\w+(?:Exception|Error):',line)]
  exceptions=['\n'.join(lines[i:min(indices[j+1] if j+1<len(indices) else len(lines),i+20)]) for j,i in enumerate(indices)]
  return dict(streetLampNullReferences=sum('StreetLamp' in x and 'ChangeLightSetting' in x and x.startswith('NullReferenceException:') for x in exceptions),foodTrucksDetachedSaveErrors=sum(('FoodTrucks' in x or 'Food Trucks' in x) and 'ERROR' in x and ("isn't attached" in x or 'is not attached' in x) for x in lines),exceptionHeaders=[x.splitlines()[0] for x in exceptions],persistentCityExceptionStacks=sum('PersistentCity.' in x for x in exceptions),modErrors=[x for x in lines if re.search(r'\[Mod:[^]]+\].*\bERROR\b',x)])
 row=dict(run=label,arm=arm,sequence='load/play60s/manual-save/unload/reload/play10s',controlledLogSha256=hashlib.sha256(p.read_bytes()).hexdigest(),**inventory(text))
 full=s/(label+'-Player-agent.log')
 if full.exists():row['fullLog']=dict(sha256=hashlib.sha256(full.read_bytes()).hexdigest(),**inventory(full.read_text(errors='replace')))
 rows.append(row)
 for i,line in enumerate(lines):
  if re.match(r'^\w+(?:Exception|Error):',line) or ('ERROR' in line and '[Mod:' in line):excerpts.append(label+' '+str(i+1)+'\n'+'\n'.join(lines[i:i+15]))
 p=s/(label+'-window-1-stop.json')
 if p.exists():
  r=json.loads(p.read_text(encoding='utf-8-sig'));v=json.loads((s/(label+'-window-1-validity.json')).read_text())
  perfs.append(dict(run=label,arm=arm,seconds=r['realSeconds'],nativeMinutes=r['nativeGameMinutes'],mod=r['combinedModWorkPerUnityFrameMilliseconds'],wholeFrame=r['frameMilliseconds'],environment=r['environment'],validity=v,populationLagMinutes=r['populationLagMinutes'],timedExceptions=r['timedExceptions'],crossFrameCalls=r['crossFrameCalls'],componentTimedExceptions=r['componentTimedExceptions'],componentCrossFrameCalls=r['componentCrossFrameCalls']))
onlyAfter=[]
for key in ['streetLampNullReferences','foodTrucksDetachedSaveErrors']:
 before=any(r[key] or r.get('fullLog',{}).get(key,0) for r in rows if r['arm']=='before');after=any(r[key] or r.get('fullLog',{}).get(key,0) for r in rows if r['arm']=='after')
 if after and not before:onlyAfter.append(key)
errorVerdict='FAIL' if onlyAfter and sum(r['arm']=='before' for r in rows)>=2 and sum(r['arm']=='after' for r in rows)>=2 else ('PASS' if len(rows)>=4 and not onlyAfter else 'INCOMPLETE')
(s/'error-attribution.json').write_text(json.dumps(dict(verdict=errorVerdict,afterOnlySignatures=onlyAfter,runs=rows),indent=2))
(s/'performance-summary.json').write_text(json.dumps(perfs,indent=2))
(s/'native-error-excerpts.txt').write_text('\n\n'.join(excerpts))
print(json.dumps(dict(errorVerdict=errorVerdict,runs=[{k:v for k,v in r.items() if k in ['run','arm','streetLampNullReferences','foodTrucksDetachedSaveErrors','persistentCityExceptionStacks']} for r in rows],timings=[dict(run=r['run'],mod=r['mod'],wholeFrame=r['wholeFrame']) for r in perfs]),indent=2))
