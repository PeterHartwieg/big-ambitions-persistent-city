from pathlib import Path
import json,sys,copy,tempfile
s=Path(__file__).parent;sys.path.insert(0,str(s));import verify_financial as v,verify_resident_ledgers as l
p=s/'after-financial.jsonl';source=v.parse(p);checks=[]
for label,mutate in [('one-price-unit',lambda x:x['priceCents'].__setitem__('Units',str(int(x['priceCents']['Units'])+1))),('one-native-bit',lambda x:x['checkout']['newNativeOrders'][0]['entries'][0]['price'].__setitem__('bits',x['checkout']['newNativeOrders'][0]['entries'][0]['price']['bits']+1))]:
 rows=copy.deepcopy(source);sale=next(r['value'] for r in rows if r['kind']=='confirmed');mutate(sale)
 # Captured observer output is entirely integers/strings here.
 with tempfile.TemporaryDirectory() as d:
  tmp=Path(d)/'synthetic-verifier-negative-control.jsonl';tmp.write_text('\n'.join(json.dumps(r,default=str) for r in rows));result=v.check(tmp)
  checks.append({'control':label,'expected':'FAIL','observed':result['verdict'],'passed':result['verdict']=='FAIL'})
ledger=l.read(s/'after-resident-ledgers.json');ledger['rows'][0][1]=v.raw(__import__('fractions').Fraction(ledger['rows'][0][1])+__import__('fractions').Fraction(1,268435456));result=l.verify(ledger);checks.append({'control':'one-wallet-unit','expected':'FAIL','observed':result['verdict'],'passed':result['verdict']=='FAIL'})
(s/'verifier-self-checks.json').write_text(json.dumps(checks,indent=2));print(checks);assert all(x['passed'] for x in checks)
