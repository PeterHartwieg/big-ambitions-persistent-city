"""Summarize completed native cases; missing cases remain pending rather than passing."""
from pathlib import Path
import json,sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('build/neighborhood-finance-20261023')
labels={'one-small':3,'two-overlapping':2,'two-separated':2,'larger':2,'higher-price':2,'lower-service':2,'longer-hours':2,'lunch-hours':2}
summary={'cases':{},'pending':[], 'boundary':'Native isolated player, persistent individual budgets, one optional prepared-food intention per resident per day. Furniture and opening stock are an explicit test endowment valued at native prices; cash return on investment is not observed. No free restocking. Results are directional and conditional on this city, offer and horizon.'}
for label,days in labels.items():
 if not (root/(label+'-report.json')).exists():summary['pending'].append(label);continue
 setup=json.loads((root/(label+'-setup.json')).read_text())
 report=json.loads((root/(label+'-report.json')).read_text())
 records=[]
 for day in range(1,days+1):
  prefix=root/(label+'-day-'+str(day))
  start=json.loads(Path(str(prefix)+'-audit-start-population.json').read_text())
  end=json.loads(Path(str(prefix)+'-audit-end-population.json').read_text())
  assert start['Food']['HouseholdFinanceVersion']==1 and end['Food']['HouseholdFinanceVersion']==1
  for meal in end['Food']['Residents']:
   b=meal['Budget'];assert b['OpeningBalanceCents']+b['IncomeReceivedCents']==b['BalanceCents']+b['EssentialsPaidCents']+b['HomeFoodPaidCents']+b['PreparedFoodSpentCents']+b['HeldPurchaseCents']
   assert b['HeldPurchaseCents']==0,('uncertain funds',label,meal['ResidentId'])
  a={m['ResidentId']:m for m in start['Food']['Residents']};b={m['ResidentId']:m for m in end['Food']['Residents']}
  assert a.keys()==b.keys(),('population changed during short comparison',label)
  purchases=sum(b[k]['Purchases']-a[k]['Purchases'] for k in a)
  assert all(0<=b[k]['Purchases']-a[k]['Purchases']<=1 for k in a)
  existing=sum(b[k]['ExistingStopPurchases']-a[k]['ExistingStopPurchases'] for k in a)
  spent=sum(b[k]['SpentCents']-a[k]['SpentCents'] for k in a)
  debit=sum(b[k]['Budget']['PreparedFoodSpentCents']-a[k]['Budget']['PreparedFoodSpentCents'] for k in a)
  assert debit==spent
  nativeDay=int(start['Minute']//1440)
  history=[h for h in end['Food']['History'] if h['Day']==nativeDay]
  assert sum(h['Purchases'] for h in history)==purchases
  assert sum(h['RevenueCents'] for h in history)==spent
  nativeStart=json.loads(Path(str(prefix)+'-start.json').read_text())
  nativeEnd=json.loads(Path(str(prefix)+'-final.json').read_text())
  stockBefore={s['PlaceId']:s['supply']['ProducerUnits'] for s in nativeStart['shops']}
  stockAfter={s['PlaceId']:s['supply']['ProducerUnits'] for s in nativeEnd['shops']}
  for place,units in stockBefore.items():
   sold=sum(h['Purchases'] for h in history if h['PlaceId']==place)
   assert units-stockAfter[place]==sold,('native product stock',label,nativeDay,place)
  accounts=[r for r in report['accounts'] if r['Day']==nativeDay]
  assert len(accounts)==len(setup['fixtures']),('missing native settlement',label,nativeDay)
  nativeRevenue=sum(r['Revenue'] for r in accounts)
  assert abs(nativeRevenue-spent/100)<.05
  records.append({'day':nativeDay,'purchases':purchases,'existingStopPurchases':existing,'revenue':nativeRevenue,
    'nativeOperatingProfit':sum(r['OperatingProfit'] for r in accounts),'nativeCosts':sum(r['Resources']+r['Wages']+r['Rent']+r['OtherOperatingCosts'] for r in accounts),
    'history':history,'accounts':accounts})
  timing=Path(str(prefix)+'-skip-timing.json')
  records[-1]['runStyle']='native-time-skip' if timing.exists() else 'ordinary-fast-forward'
  if timing.exists():records[-1]['skipTiming']=json.loads(timing.read_text())
 summary['cases'][label]={'fixtures':setup['fixtures'],'days':records,'purchases':sum(r['purchases'] for r in records),
   'existingStopPurchases':sum(r['existingStopPurchases'] for r in records),'nativeOperatingProfit':sum(r['nativeOperatingProfit'] for r in records),
   'openingEquipmentAndInventory':sum(r['capitalAtNativeCataloguePrices']+r['openingInventoryAtNativeWholesale'] for r in setup['fixtures'])}
provenance=root/'baseline-provenance.json'
if provenance.exists():summary['baselineProvenance']=json.loads(provenance.read_text())
provenance=root/'case-provenance.json'
if provenance.exists():summary['caseProvenance']=json.loads(provenance.read_text())
if 'one-small' in summary['cases']:
 baseline=summary['cases']['one-small'];original=baseline['fixtures'][0]['shop']
 reference=baseline['days'][:2];basePurchases=sum(d['purchases'] for d in reference)/len(reference)
 baseProfit=sum(d['nativeOperatingProfit'] for d in reference)/len(reference)
 summary['comparisons']={}
 for name,case in summary['cases'].items():
  days=case['days'][:2];count=len(days)
  purchases=sum(d['purchases'] for d in days)/count
  profit=sum(d['nativeOperatingProfit'] for d in days)/count
  branches={f['shop']:sum(h['Purchases'] for d in days for h in d['history'] if h['PlaceId']==f['shop'])/count for f in case['fixtures']}
  summary['comparisons'][name]={'comparisonDays':count,'dailyPurchases':purchases,'changeInDailyPurchases':purchases-basePurchases,
    'dailyNativeOperatingProfit':profit,'changeInDailyNativeOperatingProfit':profit-baseProfit,'dailyPurchasesByShop':branches,
    'originalShopDailyPurchases':branches.get(original),'equippedCapacityPerHour':sum(f['customerCapacity'] for f in case['fixtures'])}
(root/'comparison-summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps({'completed':list(summary['cases']),'pending':summary['pending'], 'purchases':{k:v['purchases'] for k,v in summary['cases'].items()}},indent=2))
