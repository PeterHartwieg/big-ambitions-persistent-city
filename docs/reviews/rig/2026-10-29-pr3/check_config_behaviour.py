from pathlib import Path
import json,collections
s=Path(__file__).parent;out={}
for label,expected in [('config-zero',False),('config-hundred',True)]:
 files=list(s.glob(label+'-optional-start-*-optional.jsonl'))
 if not files and (s/(label+'-optional.jsonl')).exists():files=[s/(label+'-optional.jsonl')]
 if not files:continue
 rows=[json.loads(l) for p in files for l in p.read_text().splitlines()];choices={r['value']['key']:r['value'] for r in rows if r['kind']=='choice'}
 stops={(r['value']['id'],r['value']['stop']['VisitMinute']):r['value'] for r in rows if r['kind']=='scheduled'}
 out[label]=dict(distinctChoices=len(choices),accepted=sum(c['accepted'] for c in choices.values()),scheduledRecords=len(stops),allChoicesAtExpectedRate=bool(choices) and all(c['accepted']==expected for c in choices.values()),noStopsAtZero=not stops if not expected else None,faults=rows[-1]['value'].get('faults') if rows[-1]['kind']=='end' else ['window not ended'])
 out[label]['passed']=out[label]['allChoicesAtExpectedRate'] and not out[label]['faults'] and (out[label]['noStopsAtZero'] if not expected else bool(stops))
(s/'config-behaviour-summary.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2));assert len(out)==2 and all(r['passed'] for r in out.values())
