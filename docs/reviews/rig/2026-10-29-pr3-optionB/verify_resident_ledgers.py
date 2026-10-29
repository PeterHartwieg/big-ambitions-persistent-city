"""Extract selected numeric ledger data (not saves), then verify with exact rationals.
Usage: script extract INITIAL_POPULATION FINAL_POPULATION FINANCIAL_RESULT OUTPUT
       script verify OUTPUT
The compact evidence retains all consumers, including non-buyers.
"""
from pathlib import Path
from decimal import Decimal,getcontext
getcontext().prec=100
from fractions import Fraction
from verify_financial import number,raw,moneyfield
import sys,json,hashlib
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'),parse_float=Decimal)
def extract(initial,final,financial):
 a,b=read(initial),read(final);f=read(financial)
 ac={m['ResidentId']:m for m in a['Food']['Residents']};bc={m['ResidentId']:m for m in b['Food']['Residents']}
 assert ac.keys()==bc.keys(),'Consumer identity set changed'
 charges={}
 for row in f['payments']:charges[row['resident']]=charges.get(row['resident'],Fraction())+Fraction(row['residentDollars'])*100
 rows=[]
 for ident in sorted(ac):
  x,y=ac[ident],bc[ident];p,q=x['Budget'],y['Budget']
  def amount(obj,name):return Fraction(int(obj[name]),268435456) if name in obj else number(obj[name+"Cents"])
  values=[amount(p,'Balance'),amount(q,'Balance')]+[amount(q,n)-amount(p,n) for n in ['IncomeReceived','EssentialsPaid','HomeFoodPaid','HeldPurchase','PreparedFoodSpent']]+[amount(y,'Spent')-amount(x,'Spent'),charges.get(ident,Fraction())]
  rows.append([ident]+[raw(v) for v in values])
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
  d=extract(*sys.argv[2:5]);status=verify(d);d['verification']=status;Path(sys.argv[5]).write_text(json.dumps(d,indent=2))
 else:status=verify(read(sys.argv[2]))
 print(json.dumps(status,indent=2))
 sys.exit(0 if status['verdict']=='PASS' else 1)
