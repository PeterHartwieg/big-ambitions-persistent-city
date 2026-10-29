from pathlib import Path
import sys,json
import verify_financial as f,verify_resident_ledgers as l
s=Path(__file__).parent
for arm in sys.argv[1:] or ['before','after']:
 raw=next(s.glob(arm+'-finance-start-*-financial.jsonl'));result=f.check(raw);target=s/(arm+'-financial-verification.json');target.write_text(json.dumps(result,indent=2))
 initial=next(s.glob(arm+'-finance-initial-*-population.json'));final=next(s.glob(arm+'-final-*-population.json'))
 ledger=l.extract(initial,final,target);ledger['verification']=l.verify(ledger);(s/(arm+'-resident-ledgers.json')).write_text(json.dumps(ledger,indent=2))
 print(arm,result['verdict'],result['gateA'],result['gateB'],result['confirmedSales'],result['nativeOrderSumDollars'],result['residentPaymentSumDollars'],ledger['verification'])
 assert result['verdict']=='PASS' and ledger['verification']['verdict']=='PASS'
