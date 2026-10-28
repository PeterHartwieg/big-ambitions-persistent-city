from pathlib import Path
import json,hashlib
from decimal import Decimal
from verify_financial import check,norm
s=Path(__file__).parent
out={};comparables={}
for label,arm in [('A3','before'),('B3','after')]:
 p=next(s.glob(label+'-finance-finance-start-*-financial.jsonl'))
 result=check(p);(s/(arm+'-financial-diagnostic.json')).write_text(json.dumps(result,indent=2))
 rows=[json.loads(l,parse_float=Decimal) for l in p.read_text(encoding='utf-8-sig').splitlines()]
 a=next(r['value'] for r in rows if r['kind']=='start');b=next(r['value'] for r in rows if r['kind']=='end')
 aggregate={k:v for k,v in result.items() if k not in ['inputSha256','comparison','payments','newNativeTransactions','verdict']}
 payments=result['payments']
 # Compare all measured amounts/identities; sub-minute native timestamps are not money.
 transactions=[{k:v for k,v in t.items() if k!='timestamp'}|{'day':t['timestamp']['Day']} for t in result['newNativeTransactions']]
 shops=[dict(place=x['place'],prices=x['prices'],history=x['history'],dailyIncomes=x['dailyIncomes'],lastDeposit=x['lastDeposit']) for x in b['shops']]
 comparables[arm]=norm(dict(aggregate=aggregate,payments=payments,transactions=transactions,shops=shops,receipts=b['receipts']))
 out[arm]=dict(inputSha256=result['inputSha256'],confirmedSales=result['confirmedSales'],residentPayments=result['residentPaymentSumDollars'],nativeOrders=result['nativeOrderSumDollars'],nativeCashRevenue=result['nativeCashRevenueTransactionsDollars'],nativeCashDelta=result['nativeCashDeltaDollars'],days=result['days'],nativeConservationDiagnostic=result['verdict'],observerFaults=result['observerFaults'],heldCentsAtEnd=result['heldCentsAtEnd'])
def diff(a,b,path=''):
 if isinstance(a,dict) and isinstance(b,dict):
  return sum((diff(a.get(k),b.get(k),path+'/'+k) for k in sorted(set(a)|set(b))),[])
 if isinstance(a,list) and isinstance(b,list):
  if len(a)!=len(b):return [dict(path=path+'/length',before=len(a),after=len(b))]
  return sum((diff(x,y,path+'/'+str(i)) for i,(x,y) in enumerate(zip(a,b))),[])
 return [] if a==b else [dict(path=path,before=a,after=b)]
(s/'financial-compared-values.json').write_text(json.dumps(comparables,indent=2))
differences=diff(comparables['before'],comparables['after'])
out['comparison']='Exact before/after equality of confirmed purchase/receipt identities and amounts, native prices/history/accounts/cash/transaction amounts, item quantities and measured wallet totals; no tolerances. Transaction sub-minute timestamps are excluded.'
out['verdict']='PASS' if not differences and out['before']['confirmedSales']>0 and not out['before']['observerFaults'] and not out['after']['observerFaults'] else 'FAIL'
out['differences']=differences
(s/'financial-equality.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
