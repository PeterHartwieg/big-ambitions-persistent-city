from pathlib import Path
from decimal import Decimal
from fractions import Fraction
import json,hashlib
s=Path(__file__).parent
MONEY={'OpeningBalance','Balance','IncomeReceived','EssentialsPaid','HomeFoodPaid','PreparedFoodSpent','HeldPurchase','Arrears','DayAllowanceUsed','Allowance','Price','Spent','Revenue','DailyAllowance'}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'),parse_float=Decimal)
def one(label,suffix):
 p=list(s.glob(label+'-*-'+suffix+'.json'));assert len(p)==1,(label,len(p));return p[0]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def amounts(obj,path=''):
 out={}
 if isinstance(obj,dict):
  for k,v in obj.items():
   name=k[:-5] if k.endswith('Cents') and k[:-5] in MONEY else k;child=path+'/'+name
   if name in MONEY and isinstance(v,(int,Decimal)):out[child]=Fraction(v) if k.endswith('Cents') else Fraction(v,268435456)
   elif k.endswith('Cents'):out[child]=Fraction(v)
   else:out.update(amounts(v,child))
 elif isinstance(obj,list):
  for i,v in enumerate(obj):out.update(amounts(v,path+'/'+str(i)))
 return out
def identities(obj):return [{k:c[k] for k in ['Identity','HomeId','DayPlaceId','LeisureId','ShopId','Preferences']} for c in obj['Citizens']]
rows={}
for name,initial,control in [('schema13','after-qa-1-initial','after-qa-1'),('schema14','schema14-migration-initial','schema14-migration-control')]:
 if not list(s.glob(initial+'-*-population.json')):continue
 source=one(initial,'saved-payload');live=one(initial,'population');presave=one(control+'-pre-save','population');saved=one(control+'-saved','saved-payload');reloaded=one(control+'-reloaded','saved-payload');reloadlive=one(control+'-reloaded','population')
 a,b,c,d,e=map(read,[source,live,presave,saved,reloadlive])
 row=dict(sourceSchema=a['Schema'],migratedSchema=b['Schema'],savedSchema=d['Schema'],reloadedSchema=e['Schema'],moneyFields=len(amounts(a)),exactHistoricalMoneyPreservedOnLoad=amounts(a)==amounts(b),allMoneyPreservedBySaveAndReload=amounts(c)==amounts(d)==amounts(e),identitiesAssignmentsPreserved=identities(a)==identities(b)==identities(e),savedPayloadByteIdenticalAcrossReload=sha(saved)==sha(reloaded),hashes={p.name:sha(p) for p in [source,live,presave,saved,reloaded,reloadlive]})
 row['passed']=all(v for v in row.values() if isinstance(v,bool)) and row['migratedSchema']==row['savedSchema']==row['reloadedSchema']==16
 if name=='schema14':
  receipt_rows=[read(s/(tag+'.json'))['receipts'] for tag in ['before-financial-control-pre-save-finance','schema14-migration-control-pre-save-finance','schema14-migration-control-reloaded-finance']]
  row['historicalReceipts']=len(receipt_rows[0]);row['allHistoricalReceiptKeysAndValuesPreserved']=receipt_rows[0]==receipt_rows[1]==receipt_rows[2]
 row['sourceWrittenByBefore']=sha(one('before-financial-control-saved','saved-payload'))==sha(source) if name=='schema14' else None
 row['schema13WholeCentFixture']=all(v.denominator==1 for v in amounts(a).values()) if name=='schema13' else None
 rows[name]=row
for case in json.loads((s/'schema-cases.json').read_text()):
 if case['expected']!='Loaded':continue
 source=s/case['file'];output=s/(case['file']+'.result')
 if not output.exists():continue
 a,b=read(source),read(output)
 row=dict(sourceSchema=a['Schema'],migratedSchema=b['Schema'],moneyFields=len(amounts(a)),exactHistoricalMoneyPreserved=amounts(a)==amounts(b),identitiesAssignmentsPreserved=identities(a)==identities(b),sourceSha256=sha(source),outputSha256=sha(output))
 row['passed']=row['migratedSchema']==16 and row['exactHistoricalMoneyPreserved'] and row['identitiesAssignmentsPreserved'];rows[case['file'].replace('.private.json','')+'-native-codec']=row
(s/'migration-summary.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2));assert all(r['passed'] for r in rows.values())
