"""Check native checkpoint/stock/order/receipt conservation, never infer sales from intentions."""
from pathlib import Path
import json, math
root=Path(__file__).resolve().parents[2]/'build/food-routine-sales-20261022'
read=lambda name:json.loads((root/(name+'.json')).read_text())
checks={}
for stage in ['Travelling','Serving','Paid']:
 before,after=read('before-'+stage),read('after-'+stage)
 checks['reload-'+stage]=before['purchases']==after['purchases'] and before['spendingCents']==after['spendingCents'] and before['supply']['ProducerUnits']==after['supply']['ProducerUnits'] and before['supply']['BagUnits']==after['supply']['BagUnits']
 before_ids={(m['ResidentId'],m['Day']) for m in before['active']}
 after_ids={(m['ResidentId'],m['Day']) for m in after['active']}
 checks['resident-ownership-'+stage]=before_ids<=after_ids
 if stage=='Paid':
  checks['paid-receipt-survives']=before['receipts']==after['receipts'] and len(after['receipts'])==after['purchases']
replay=read('duplicate-attempt')
checks['duplicate-replay-blocked']=replay['paid'] and 'already confirms' in replay['reason'] and replay['before']['orders']==replay['after']['orders'] and replay['before']['supply']==replay['after']['supply'] and replay['before']['receipts']==replay['after']['receipts']
final=read('final');count=final['purchases'];initial=read('setup')['initialSupply']
checks['natural-purchases']=count>0
raw_pastries=sum(c['amount'] for i in final['nativeItems'] for c in i['cargoInstances'] if c['itemName']=='ba:itemname_croissant')
raw_bags=sum(c['amount'] for i in final['nativeItems'] if i['itemName']==read('setup')['register'] for c in i['cargoInstances'])
checks['pastry-conservation']=initial['ProducerUnits']-raw_pastries==count
checks['bag-conservation']=initial['BagUnits']-raw_bags==count
checks['no-uncertain']=not final['totals'].get('Uncertain') and all(v.startswith('paid|') for v in final['receipts'].values())
checks['no-pending-orders']=len(final['orders'])==0
checks['daily-native-customers']=sum(h['totalCustomers'] for h in final['history'])==count
receipt_days=[int(key.rsplit(':',1)[1]) for key in final['receipts']]
last_day=max(receipt_days)
checks['native-receipts-match-retained-days']=len(receipt_days)==sum(h['totalCustomers'] for h in final['history'] if h['dayNumber']>=last_day-1)
checks['restocking-still-runs']=final['housekeeping']['restocks']>=1
checks['stock-tasks-still-run']=final['housekeeping']['stockTasks']>0
checks['returns-finish']=not final['outings']
checks['daily-native-revenue']=abs(sum(h['totalRevenue'] for h in final['history'])-final['spendingCents']/100)<count*.0051
report={'checks':checks,'purchases':count,'nativeRevenue':sum(h['totalRevenue'] for h in final['history']), 'perDay':final['history'],'housekeeping':final['housekeeping'],'remainingPastries':raw_pastries,'remainingBags':raw_bags,'retainedReceiptCount':len(receipt_days), 'note':'One fixture, natural automatic buying decisions; native saves and sales. Allowances and break policy remain provisional; physical rendering disabled.'}
(root/'native-checks.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
assert all(checks.values()),checks
