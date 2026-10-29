import json,sys
from pathlib import Path
from verify_financial import check
from verify_resident_ledgers import extract,verify
s=Path(__file__).parent
for arm in sys.argv[1:]:
 trace=list(s.glob(arm+'-finance-start-*-financial.jsonl'));assert len(trace)==1,trace
 result=check(trace[0]);out=s/(arm+'-financial-verification.json');out.write_text(json.dumps(result,indent=2))
 initial=next(s.glob(arm+'-finance-initial-*-population.json'),None) or next(s.glob(arm+'-initial-initial-*-population.json'),None) or next(s.glob(arm+'-qa-1-initial-*-population.json'))
 final=next(s.glob(arm+'-final-*-population.json'))
 ledgers=extract(initial,final,out);ledgers['verification']=verify(ledgers);(s/(arm+'-resident-ledgers.json')).write_text(json.dumps(ledgers,indent=2))
 print(arm,{k:result[k] for k in ['verdict','gateA','gateB','gateC']},ledgers['verification'],flush=True)
 assert result['verdict']=='PASS' and ledgers['verification']['verdict']=='PASS'
