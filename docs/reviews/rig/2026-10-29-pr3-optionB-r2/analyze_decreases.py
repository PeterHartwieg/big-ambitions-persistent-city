from pathlib import Path
import json
s=Path(__file__).parent;rates=json.loads((s/'rate-summary.json').read_text());out={}
for label,control,offset in [('dec100-25','constant25',0),('dec100-0-25','constant25',2),('dec50-25','constant25',0),('dec50-10','constant10',0)]:
 if label not in rates or control not in rates:continue
 r=rates[label];c=rates[control];daily=[]
 for a,b in zip(r['daily24h'],c['daily24h'][offset:]):daily.append(dict(day=a['day'],arm=a,control=b))
 ids=[x['id'] for x in r['trimOrder'] if x['reason']=='trim']
 out[label]=dict(openingPending=r['openingPending'],closingPending=r['closingPending'],dropsByCause=r['dropsByCause'],trimCount=len(ids),trimOrder=ids,trimIdsOrdinallySorted=ids==sorted(ids),daily=daily,control=control,controlDayOffset=offset,auditExact=r['auditExact'],matching='Same FT checkpoint family and native calendar days; native weather and unsaved geometry sampling can differ. No weather, price or safety override was used.')
(s/'decrease-comparison.json').write_text(json.dumps(out,indent=2));print(json.dumps({k:{n:v[n] for n in ['openingPending','closingPending','dropsByCause','trimCount','trimIdsOrdinallySorted','auditExact']} for k,v in out.items()},indent=2))
