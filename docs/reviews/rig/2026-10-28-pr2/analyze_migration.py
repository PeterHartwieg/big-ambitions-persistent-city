from pathlib import Path
from decimal import Decimal
import json,hashlib
s=Path(__file__).parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'),parse_float=Decimal)
def one(label,suffix):
 files=list(s.glob(label+'-*-'+suffix+'.json'));assert len(files)==1,(label,suffix,len(files));return files[0]
def amounts(obj,path=''):
 result={}
 if isinstance(obj,dict):
  for k,v in obj.items():
   child=path+'/'+k
   if k.endswith('Cents'):result[child]=Decimal(v)
   else:result.update(amounts(v,child))
 elif isinstance(obj,list):
  for i,v in enumerate(obj):result.update(amounts(v,path+'/'+str(i)))
 return result
def assignments(obj):return [{k:c[k] for k in ['Identity','HomeId','DayPlaceId','LeisureId','ShopId','Preferences']} for c in obj['Citizens']]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
p13=one('before-control-saved','saved-payload')
pload=one('migration-initial','saved-payload');plive=one('migration-initial','population')
psave=one('migration-control-saved','saved-payload');preload=one('migration-control-reloaded','saved-payload');preloadlive=one('migration-control-reloaded','population')
old=read(p13);live=read(plive);saved=read(psave);reload=read(preloadlive)
old_receipts=read(s/'before-control-reloaded-finance.json')['receipts']
load_receipts=read(s/'migration-control-pre-save-finance.json')['receipts']
reload_receipts=read(s/'migration-control-reloaded-finance.json')['receipts']
rows=dict(sourceSchema=old['Schema'],liveMigratedSchema=live['Schema'],savedSchema=saved['Schema'],reloadedSchema=reload['Schema'],
 sourcePayloadNotRewrittenByLoad=sha(p13)==sha(pload),schema14PayloadByteIdenticalAcrossReload=sha(psave)==sha(preload),
 monetaryFieldsChecked=len(amounts(old)),allHistoricalAmountsPreservedOnLoad=amounts(old)==amounts(live),allHistoricalAmountsPreservedOnSave=amounts(old)==amounts(saved),allHistoricalAmountsPreservedOnReload=amounts(old)==amounts(reload),
 citizens=len(old['Citizens']),identitiesAndAssignmentsPreserved=assignments(old)==assignments(live)==assignments(saved)==assignments(reload),
 consumers=len(old['Food']['Residents']),consumerIdsPreserved=[m['ResidentId'] for m in old['Food']['Residents']]==[m['ResidentId'] for m in reload['Food']['Residents']],
 historicalReceipts=len(old_receipts),allHistoricalReceiptKeysAndValuesPreserved=old_receipts==load_receipts==reload_receipts,
 inputSha256={p.name:sha(p) for p in [p13,pload,plive,psave,preload,preloadlive]})
checks=[v for v in rows.values() if isinstance(v,bool)]
rows['verdict']='PASS' if all(checks) and rows['sourceSchema']==13 and rows['liveMigratedSchema']==rows['savedSchema']==rows['reloadedSchema']==14 else 'FAIL'
(s/'migration-summary.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2))
