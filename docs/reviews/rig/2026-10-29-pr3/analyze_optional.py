from pathlib import Path
import json,sys,math,collections
s=Path(__file__).parent
out={}
for arm in sys.argv[1:] or ['before','after']:
 files=list(s.glob(arm+'-optional-start-*-optional.jsonl'))
 if not files and (s/(arm+'-optional.jsonl')).exists():files=[s/(arm+'-optional.jsonl')]
 rows=[json.loads(l) for p in files for l in p.read_text().splitlines()]
 choices={r['value']['key']:r['value'] for r in rows if r['kind']=='choice'}
 n=len(choices);k=sum(v['accepted'] for v in choices.values());z=1.959963984540054
 if n:
  p=k/n;center=(p+z*z/(2*n))/(1+z*z/n);width=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/(1+z*z/n);interval=[center-width,center+width]
 else:interval=None
 scheduled={(r['value']['id'],r['value']['stop']['VisitMinute']):r['value'] for r in rows if r['kind']=='scheduled'}
 out[arm]=dict(distinctEligibleVisitChoices=n,acceptedChoices=k,acceptedShare=k/n if n else None,wilson95=interval,expected25WithinInterval=interval[0]<=.25<=interval[1] if n else None,realisedStops=len(scheduled),realisedShare=len(scheduled)/n if n else None,kinds=dict(collections.Counter(r['stop']['Kind'] for r in scheduled.values())),physicalTransitions=dict(collections.Counter(r['value']['phase'] for r in rows if r['kind']=='physical')),notes='Choices precede geometry/duration/occupancy checks; accepted choice does not guarantee a stop. Food-owned places excluded. Before feature absent means no denominator, not a 0/0 percentage.')
(s/'optional-summary.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
