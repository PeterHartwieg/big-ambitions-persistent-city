from pathlib import Path
import json,statistics,re,hashlib
s=Path(__file__).parent;rows=[];interference={}
def read(n):return json.loads((s/n).read_text())
for arm in ['before','after']:
 for i in range(1,4):
  label='fresh-'+arm+'-window-'+str(i);r=read(label+'-stop.json');v=read(label+'-validity.json')
  rows.append(dict(arm=arm,window=i,seconds=r['realSeconds'],mod=r['combinedModWorkPerUnityFrameMilliseconds'],wholeFrame=r['frameMilliseconds'],validity=v,timedExceptions=r['timedExceptions'],crossFrameCalls=r['crossFrameCalls'],environment=r['environment']))
for label in ['before','after','fresh-before','fresh-after']:
 for i in range(1,4):
  p=s/(label+'-window-'+str(i)+'-native-window.log')
  if not p.exists():continue
  text=p.read_text(errors='replace');matches=[int(x) for x in re.findall(r'other programs (\d+)%',text)];minutes=[int(x) for x in re.findall(r'other programs held (\d+)%',text)]
  summaries=[x for x in text.splitlines() if 'Session 1 min:' in x]
  interference[label+'-'+str(i)]=dict(logSha256=hashlib.sha256(p.read_bytes()).hexdigest(),slowFrameOtherProgramCpuPercent=dict(samples=len(matches),median=statistics.median(matches) if matches else None,maximum=max(matches) if matches else None),minuteOtherProgramCpuPercent=minutes,minuteSummaries=summaries)
noise=[]
for scope in ['mod','wholeFrame']:
 for metric in ['p50','p95','p99','mean','maximum']:
  before=[r[scope][metric] for r in rows if r['arm']=='before'];after=[r[scope][metric] for r in rows if r['arm']=='after'];change=statistics.median(after)-statistics.median(before);spread=max(max(before)-min(before),max(after)-min(after))
  noise.append(dict(scope=scope,metric=metric,beforeMedian=statistics.median(before),afterMedian=statistics.median(after),change=change,beforeRange=max(before)-min(before),afterRange=max(after)-min(after),largerSpread=spread,rangesOverlap=max(min(before),min(after))<=min(max(before),max(after)),worseBeyondObservedSpread=change>spread))
valid=all(r['validity']['clean'] and r['seconds']>=60 and r['timedExceptions']==r['crossFrameCalls']==0 for r in rows)
result=dict(saveCleanAndNoTimedErrors=valid,wholeFrameBeyondSpread=[r for r in noise if r['scope']=='wholeFrame' and r['worseBeyondObservedSpread']],description='Fresh-process confirmation; all primary measurements retained separately. External CPU evidence is contextual, not automatic exoneration.')
for name,data in [('performance-confirmation-summary.json',rows),('performance-confirmation-noise.json',noise),('performance-interference.json',interference),('performance-confirmation-result.json',result)]:
 (s/name).write_text(json.dumps(data,indent=2))
print(json.dumps(result,indent=2));print(json.dumps(noise,indent=2))
