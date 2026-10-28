"""Compare observed buyer identities across matched initial-city experiments."""
from pathlib import Path
import json,sys
root=Path(sys.argv[1]);summary=json.loads((root/'comparison-summary.json').read_text())
original=summary['cases']['one-small']['fixtures'][0]['shop']
def buyers(label,day):
 start=json.loads((root/f'{label}-day-{day}-audit-start-population.json').read_text())
 end=json.loads((root/f'{label}-day-{day}-audit-end-population.json').read_text())
 before={m['ResidentId']:m['Purchases'] for m in start['Food']['Residents']}
 return {m['ResidentId']:m['PlaceId'] for m in end['Food']['Residents'] if m['Purchases']>before[m['ResidentId']]}
results={}
for name,case in summary['cases'].items():
 rows=[]
 for day in range(1,min(2,len(case['days']))+1):
  baseline=buyers('one-small',day);caseBuyers=buyers(name,day)
  retained=baseline.keys() & caseBuyers.keys()
  rows.append({'testDay':day,'baselineBuyers':len(baseline),'caseBuyers':len(caseBuyers),
   'retainedBuyers':len(retained),'newBuyers':len(caseBuyers.keys()-baseline.keys()),
   'noLongerBought':len(baseline.keys()-caseBuyers.keys()),
   'baselineBuyersWhoChoseAnotherBranch':sum(caseBuyers[k]!=original for k in retained)})
 results[name]=rows
output={'cases':results,'boundary':'Matched persistent identities and same initial city. Different business choices can change later routines. These are observed counterfactual outcomes, not forecasts or measured long-run loyalty.'}
(root/'buyer-comparison.json').write_text(json.dumps(output,indent=2)+'\n')
print(json.dumps(results,indent=2))
