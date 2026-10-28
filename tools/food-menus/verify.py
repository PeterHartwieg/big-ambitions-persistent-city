#!/usr/bin/env python3
"""Reconcile the bounded native menu comparison. Never infers sales from offer counts."""
from pathlib import Path
import collections,json,statistics,sys
root=Path(sys.argv[1]);read=lambda n:json.loads((root/(n+'.json')).read_text())
partial='--partial' in sys.argv
if not partial:
 complete=read('comparison-complete');assert complete['days']==8 and len(complete['cases'])==4
base=read('pastry-base-hash');assert len(base)==2
summary={'complete':not partial,'baseHashes':base,'cases':{},'platform':'Mac','scope':'Two days per case, native time skips; stocked fixture endowments with native wholesale consumption costs. Not closed-economy or fun evidence.'}
for label in ['pastry','cafe-menu','different-menus','higher-burger-price']:
 if partial and not (root/(label+'-report.json')).exists():continue
 assert read(label+'-base-hash')==base
 assert (root/(label+'-report.json')).exists()
 report=read(label+'-report');days=[]
 for day in (1,2):
  prefix=label+'-day-'+str(day);start=read(prefix+'-audit-start-population');end=read(prefix+'-audit-end-population')
  a={m['ResidentId']:m for m in start['Food']['Residents']};b={m['ResidentId']:m for m in end['Food']['Residents']};assert a.keys()==b.keys()
  purchases=sum(b[k]['Purchases']-a[k]['Purchases'] for k in a);spent=sum(b[k]['SpentCents']-a[k]['SpentCents'] for k in a)
  for key,new in b.items():
   old=a[key];budget=new['Budget']
   assert 0<=new['Purchases']-old['Purchases']<=3
   assert budget['HeldPurchaseCents']==0
   assert budget['OpeningBalanceCents']+budget['IncomeReceivedCents']==sum(budget[x] for x in ['BalanceCents','EssentialsPaidCents','HomeFoodPaidCents','PreparedFoodSpentCents','HeldPurchaseCents'])
   assert budget['PreparedFoodSpentCents']-old['Budget']['PreparedFoodSpentCents']==new['SpentCents']-old['SpentCents']
  nativeStart=read(prefix+'-start');nativeEnd=read(prefix+'-final');nativeDay=int(start['Minute']//1440)
  for snapshot in [nativeStart,nativeEnd]:
   assert snapshot['diagnostics']['active']
   assert len(snapshot['shops'])==len(snapshot['scope'])
   for shop in snapshot['shops']:
    scope=next(s for s in snapshot['scope'] if s['PlaceId']==shop['PlaceId'])
    assert shop['exclusion']==''
    assert set(shop['advertisedProducts'])<=set([scope['ProductId']]+scope['AdditionalProductIds'])
  history=[h for h in end['Food']['History'] if h['Day']==nativeDay]
  assert sum(h['Purchases'] for h in history)==purchases and sum(h['RevenueCents'] for h in history)==spent
  rows={h['PlaceId']:h for h in history};before={s['PlaceId']:s for s in nativeStart['shops']}
  for shop in nativeEnd['shops']:
   place=shop['PlaceId'];row=rows[place];sold={p['ProductId']:p for p in row['ProductSales']}
   assert sum(p['Purchases'] for p in sold.values())==row['Purchases']
   assert sum(p['RevenueCents'] for p in sold.values())==row['RevenueCents']
   assert all(sum(p['PurchaseMeals'][i] for p in sold.values())==row['PurchaseMeals'][i] for i in range(4))
   for product,supply in shop['menuSupply'].items():
    count=sold.get(product,{}).get('Purchases',0)
    assert before[place]['menuSupply'][product]['ProducerUnits']-supply['ProducerUnits']==count,('stock',label,day,place,product)
   bags=lambda sh:sum(c['amount'] for i in sh['nativeItems'] for c in i['cargoInstances'] or [] if c['itemName']=='ba:itemname_paperbag')
   assert bags(before[place])-bags(shop)==row['Purchases'],('bags',label,day,place)
   assert sum(row['PurchaseMeals'])==sum(row['PurchaseHours'])==row['Purchases']
  accounts=[s for s in report['accounts'] if s['Day']==nativeDay]
  assert len(accounts)==len(before) and abs(sum(s['Revenue'] for s in accounts)-spent/100)<.05
  expectedResources=0
  wholesale={p['itemName']:p['wholesalePrice'] for p in read('native-catalogue')['products']}
  for shop in before.values():
   row=rows[shop['PlaceId']]
   bagPrice=next(c['pricePerUnit'] for i in shop['nativeItems'] for c in i['cargoInstances'] or [] if c['itemName']=='ba:itemname_paperbag')
   expectedResources+=row['Purchases']*bagPrice+sum(p['Purchases']*wholesale[p['ProductId']] for p in row['ProductSales'])
  assert abs(sum(s['Resources'] for s in accounts)-expectedResources)<.1,('native wholesale consumption',label,day)
  receipts={k:v for k,v in nativeEnd['receipts'].items() if int(k.rsplit(':',1)[1])==nativeDay}
  assert len(receipts)==purchases and all(v.startswith('paid|') for v in receipts.values())
  decisions=read(prefix+'-food-audit')['decisions'];costs=sorted(t['ElapsedMilliseconds'] for t in decisions)
  routeCosts=[t['ElapsedMilliseconds'] for t in decisions if t['RouteCalls']>0];noRouteCosts=[t['ElapsedMilliseconds'] for t in decisions if t['RouteCalls']==0]
  for t in decisions:
   if t['RouteChecks'] and all(v in ['Closed or too late','Service unavailable','Out of stock','Over allowance'] for v in t['RouteChecks'].values()):assert t['RouteCalls']==0
  days.append({'day':nativeDay,'purchases':purchases,'revenue':spent/100,'nativeOperatingProfit':sum(s['OperatingProfit'] for s in accounts),
    'nativeResources':sum(s['Resources'] for s in accounts),'history':history,'accounts':accounts,'decisions':len(costs),
    'decisionTotalMs':sum(costs),'decisionP95Ms':costs[int(.95*(len(costs)-1))] if costs else 0,'decisionMaxMs':max(costs,default=0),
    'maxWithRoutesMs':max(routeCosts,default=0),'maxWithoutRoutesMs':max(noRouteCosts,default=0),'routeCalls':sum(t['RouteCalls'] for t in decisions),
    'knownServiceUnavailable':sum(v=='Service unavailable' for t in decisions for v in t['RouteChecks'].values()),
    'observedStaffedShopDecisions':sum(b.get('MealsPerHour',0)>0 for t in decisions for b in t['Businesses'] if b.get('MealsPerHour') is not None),
    'timing':read(prefix+'-timing')})
 summary['cases'][label]={'days':days,'setup':read(label+'-setup')['menus'],'dailyPurchases':sum(d['purchases'] for d in days)/len(days)}
(root/('partial-summary.json' if partial else 'comparison-summary.json')).write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:{'purchases':[d['purchases'] for d in v['days']],'profit':[round(d['nativeOperatingProfit'],2) for d in v['days']]} for k,v in summary['cases'].items()},indent=2))
