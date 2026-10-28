from pathlib import Path
import json,statistics
s=Path(__file__).parent;rows=json.loads((s/'performance-summary.json').read_text());comparisons=[]
for scope in ['mod','wholeFrame']:
 for metric in ['p50','p95','maximum']:
  a=[r[scope][metric] for r in rows if r['arm']=='before'];b=[r[scope][metric] for r in rows if r['arm']=='after']
  if len(a)<3 or len(b)<3:continue
  spread=max(max(a)-min(a),max(b)-min(b));delta=statistics.median(b)-statistics.median(a)
  comparisons.append(dict(scope=scope,metric=metric,before=a,after=b,beforeMedian=statistics.median(a),afterMedian=statistics.median(b),beforeSpread=max(a)-min(a),afterSpread=max(b)-min(b),largerWithinBuildSpread=spread,medianDelta=delta,afterWorseBeyondObservedSpread=delta>spread,rangeOverlap=max(min(a),min(b))<=min(max(a),max(b))))
valid=len(rows)==6 and all(r['validity']['clean'] and not any(r[k] for k in ['timedExceptions','crossFrameCalls','componentTimedExceptions','componentCrossFrameCalls']) for r in rows)
out=dict(verdict=('INCOMPLETE' if not valid else 'FAIL' if any(c['afterWorseBeyondObservedSpread'] for c in comparisons) else 'PASS'),rule='Descriptive noise check: after median minus before median exceeds the larger full within-build range for that metric. Report all p50/p95/max distributions; three windows per build do not establish causal speedup or statistical equivalence.',windows=rows,comparisons=comparisons)
(s/'performance-noise.json').write_text(json.dumps(out,indent=2));print(json.dumps({k:v for k,v in out.items() if k!='windows'},indent=2))
