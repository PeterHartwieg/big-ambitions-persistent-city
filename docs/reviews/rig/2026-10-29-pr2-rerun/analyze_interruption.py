from pathlib import Path
from decimal import Decimal
from fractions import Fraction
from verify_financial import raw
import json,hashlib
s=Path(__file__).parent
MONEY={'OpeningBalance','Balance','IncomeReceived','EssentialsPaid','HomeFoodPaid','PreparedFoodSpent','HeldPurchase','Arrears','DayAllowanceUsed','Allowance','Price','Spent','Revenue','DailyAllowance'}
def normalize(obj):
 if isinstance(obj,dict):
  if set(obj)=={'Units'}:return Fraction(int(obj['Units']),268435456)
  return {(k+'Cents' if k in MONEY else k):(Fraction(v,268435456) if k in MONEY and isinstance(v,int) else normalize(v)) for k,v in obj.items()}
 if isinstance(obj,list):return [normalize(v) for v in obj]
 return obj
def read(p):return normalize(json.loads(p.read_text(encoding='utf-8-sig'),parse_float=Decimal))
def one(label,suffix):
 files=list(s.glob(label+'-*-'+suffix+'.json'));assert len(files)==1,(label,suffix,files);return files[0]
serving=read(s/'after-interrupted-serving-checkpoint.json')['meal'];paid=read(s/'after-interrupted-paid-checkpoint.json')['meal']
replay=read(s/'after-paid-replay.json');drained=read(s/'after-interrupted-drained-finance.json')
saved=one('after-interrupted-paid-saved','saved-payload');loaded=one('after-paid-reload-initial','saved-payload')
reload_state=read(one('after-paid-reload-initial','population'));resume=read(one('after-serving-normal-initial','population'))
source=next(m for m in resume['Food']['Residents'] if m['ResidentId']==serving['ResidentId'])
reloaded=next(m for m in reload_state['Food']['Residents'] if m['ResidentId']==paid['ResidentId'])
key='persistentcity:food-sale-v2:'+serving['IntentId']
receipts=drained['receipts'];marker=receipts[key]
state=read(one('after-interrupted-drained','population'))
history=sum(h['Purchases'] for h in state['Food']['History'] if h['Day']==5)
native=sum(h['totalCustomers'] for sh in drained['shops'] for h in sh['history'] if h['dayNumber']==5)
paid_receipts=[v for k,v in receipts.items() if k.endswith(':5') and v.startswith('paid|')]
out=dict(servingResident=serving['ResidentId'],intent=serving['IntentId'],servingStatus=serving['Status'],paidStatus=paid['Status'],
 sameIntentionContinued=serving['IntentId']==paid['IntentId'],servingResumePreservesPrice=source['PriceCents']==serving['PriceCents'],
 onePurchaseOnPaidContinuation=paid['Purchases']==serving['Purchases']+1,priceCents=raw(paid['PriceCents']),
 exactWalletDebit=raw(serving['Budget']['BalanceCents']-paid['Budget']['BalanceCents']),
 walletDebitEqualsQuote=serving['Budget']['BalanceCents']-paid['Budget']['BalanceCents']==paid['PriceCents'],
 servingHeldCents=raw(serving['Budget']['HeldPurchaseCents']),paidHeldCents=raw(paid['Budget']['HeldPurchaseCents']),
 samePaidPurchaseCountAfterReload=reloaded['Purchases']==paid['Purchases'],samePaidSpendingAfterReload=reloaded['SpentCents']==paid['SpentCents'],
 savedSchema14PayloadHash=hashlib.sha256(saved.read_bytes()).hexdigest(),payloadByteIdenticalAcrossPaidReload=saved.read_bytes()==loaded.read_bytes(),
 receiptReplayReturnedPaid=replay['Paid'],receiptReplayReason=replay['Reason'],receiptReplayLeftNativeOrdersCashHistoryWalletsReceiptsUnchanged=replay['before']==replay['after'],
 matchingOriginalV2Receipt=marker,day5Purchases=history,day5NativeCustomers=native,day5PaidReceipts=len(paid_receipts),allPurchaseCountsAgree=history==native==len(paid_receipts),
 allConsumersHeldCentsAfterDrain=raw(drained['wallets']['heldCents']),ledgerFailuresAfterDrain=drained['wallets']['ledgerFailures'],
 limitations=['Serving and Paid save/reload plus receipt replay. No forced crash inside native ProcessCustomer or between native order and receipt confirmation.',
 'Serving checkpoint reached at speed 10; saved/reloaded and continued at speed 1. No forced crash inside native checkout.'])
checks=[v for v in out.values() if isinstance(v,bool)]
out['verdict']='PASS' if all(checks) and Decimal(out['allConsumersHeldCentsAfterDrain'])==0 and out['ledgerFailuresAfterDrain']==0 else 'FAIL'
(s/'interruption-summary.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
