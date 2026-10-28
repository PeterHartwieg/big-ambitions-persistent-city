"""Extract selected numeric ledger data (not saves), then verify with exact rationals.
Usage: script extract INITIAL_POPULATION FINAL_POPULATION FINANCIAL_RESULT OUTPUT
       script verify OUTPUT
The compact evidence retains all consumers, including non-buyers.
"""
from pathlib import Path
from decimal import Decimal,getcontext
getcontext().prec=100
from fractions import Fraction
import sys,json,hashlib
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'),parse_float=Decimal)
def extract(initial,final,financial):
 a,b=read(initial),read(final);f=read(financial)
 ac={m['ResidentId']:m for m in a['Food']['Residents']};bc={m['ResidentId']:m for m in b['Food']['Residents']}
 assert ac.keys()==bc.keys(),'Consumer identity set changed'
 charges={}
 for row in f['payments']:charges[row['resident']]=charges.get(row['resident'],Decimal(0))+Decimal(row['residentDollars'])*100
 rows=[]
 for ident in sorted(ac):
  x,y=ac[ident],bc[ident];p,q=x['Budget'],y['Budget']
  rows.append([ident]+[str(v) for v in [p['BalanceCents'],q['BalanceCents'],q['IncomeReceivedCents']-p['IncomeReceivedCents'],q['EssentialsPaidCents']-p['EssentialsPaidCents'],q['HomeFoodPaidCents']-p['HomeFoodPaidCents'],q['HeldPurchaseCents']-p['HeldPurchaseCents'],q['PreparedFoodSpentCents']-p['PreparedFoodSpentCents'],y['SpentCents']-x['SpentCents'],charges.get(ident,Decimal(0))]])
 return dict(inputSha256={k:hashlib.sha256(Path(p).read_bytes()).hexdigest() for k,p in [('initialPopulation',initial),('finalPopulation',final),('financialVerification',financial)]},
  unit='cents, exact decimal strings',columns=['resident','balanceBefore','balanceAfter','incomeDelta','essentialsDelta','homeFoodDelta','heldDelta','preparedSpendDelta','spentDelta','auditedCharges'],rows=rows)
def verify(d):
 failures=[];buyers=0
 for row in d['rows']:
  ident=row[0];before,after,income,essentials,home,held,prepared,spent,charges=[Fraction(Decimal(v)) for v in row[1:]]
  actual=before+income-essentials-home-after-held
  if charges>0:buyers+=1
  if not actual==prepared==spent==charges:failures.append(dict(resident=ident,actualDebit=str(actual),prepared=str(prepared),spent=str(spent),charges=str(charges)))
 return dict(consumers=len(d['rows']),buyers=buyers,exactFailures=failures,verdict='PASS' if not failures else 'FAIL')
if __name__=='__main__':
 if sys.argv[1]=='extract':
  d=extract(*sys.argv[2:5]);d['verification']=verify(d);Path(sys.argv[5]).write_text(json.dumps(d,indent=2));print(json.dumps(d['verification']))
 else:print(json.dumps(verify(read(sys.argv[2])),indent=2))
