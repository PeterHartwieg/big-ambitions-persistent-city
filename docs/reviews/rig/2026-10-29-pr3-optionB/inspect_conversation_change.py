from pathlib import Path
import json
s=Path(__file__).parent;tag='event-conversation-control';out={}
for sub in ['pre-save','saved','reloaded']:
 p=next(s.glob(tag+'-'+sub+'-*-population.json'));d=json.loads(p.read_text());c=next(c for c in d['Citizens'] if c['Identity']['Id']=='c395')
 out[sub]=dict(minute=d['Minute'],citizen={k:c.get(k) for k in ['Phase','Activity','PlaceId','NextEventMinute','ActivityUntilMinute','OptionalStopChoice','OptionalStop']},lastVisit=c['RecentVisits'][-1])
(s/'conversation-reload-change.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
