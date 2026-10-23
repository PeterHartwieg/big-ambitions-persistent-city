"""Verify isolated native comparisons; never contact the game."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]/'build/food-demand-audit-20261022'
results={}
for name in ['baseline','candidate']:
 start=json.loads((ROOT/(name+'-start.json')).read_text());final=json.loads((ROOT/(name+'-final.json')).read_text());audit=json.loads((ROOT/(name+'-food-audit.json')).read_text())
 count=final['purchases']-start['purchases'];day=int(start['minute']//1440)
 def stock(s,product):
  return sum(c['amount'] for i in s['nativeItems'] for c in i.get('cargoInstances') or [] if c['itemName']==product)
 history=[h for h in final['history'] if h['dayNumber']==day]
 pastries=stock(start,'ba:itemname_croissant')-stock(final,'ba:itemname_croissant')
 bags=stock(start,'ba:itemname_paperbag')-stock(final,'ba:itemname_paperbag')
 # Native item's stock product is read from the observed register, not guessed.
 register=next(i for i in start['nativeItems'] if i['itemName']=='ba:itemname_cashregister')
 bag_product=register['cargoInstances'][0]['itemName'];bags=stock(start,bag_product)-stock(final,bag_product)
 checks={'positive_sales':count>0,'pastries':pastries==count,'bags':bags==count,'orders_settled':not final['orders'],
 'native_daily_customers':sum(h['totalCustomers'] for h in history)==count,'no_uncertain':not final['totals'].get('Uncertain'),
 'return_journeys_finished':not final['outings'],'one_daily_decision_per_resident':len({r['Intent']['Id'] for r in audit['decisions']})==len(audit['decisions'])}
 assert all(checks.values()),(name,checks)
 results[name]={'purchases':count,'nativeRevenue':sum(h['totalRevenue'] for h in history),'checks':checks}
(ROOT/'native-checks.json').write_text(json.dumps(results,indent=2)+'\n');print(json.dumps(results,indent=2))
