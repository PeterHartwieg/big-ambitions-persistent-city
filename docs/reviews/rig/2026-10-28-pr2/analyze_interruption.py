from pathlib import Path
from decimal import Decimal
import json,hashlib
s=Path(__file__).parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'),parse_float=Decimal)
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
 onePurchaseOnPaidContinuation=paid['Purchases']==serving['Purchases']+1,priceCents=str(paid['PriceCents']),
 exactWalletDebit=str(serving['Budget']['BalanceCents']-paid['Budget']['BalanceCents']),
 walletDebitEqualsQuote=serving['Budget']['BalanceCents']-paid['Budget']['BalanceCents']==paid['PriceCents'],
 servingHeldCents=str(serving['Budget']['HeldPurchaseCents']),paidHeldCents=str(paid['Budget']['HeldPurchaseCents']),
 samePaidPurchaseCountAfterReload=reloaded['Purchases']==paid['Purchases'],samePaidSpendingAfterReload=reloaded['SpentCents']==paid['SpentCents'],
 savedSchema14PayloadHash=hashlib.sha256(saved.read_bytes()).hexdigest(),payloadByteIdenticalAcrossPaidReload=saved.read_bytes()==loaded.read_bytes(),
 receiptReplayReturnedPaid=replay['Paid'],receiptReplayReason=replay['Reason'],receiptReplayLeftNativeOrdersCashHistoryWalletsReceiptsUnchanged=replay['before']==replay['after'],
 matchingOriginalV2Receipt=marker,day5Purchases=history,day5NativeCustomers=native,day5PaidReceipts=len(paid_receipts),allPurchaseCountsAgree=history==native==len(paid_receipts),
 allConsumersHeldCentsAfterDrain=str(drained['wallets']['heldCents']),ledgerFailuresAfterDrain=drained['wallets']['ledgerFailures'],
 limitations=['Serving and Paid save/reload plus receipt replay. No forced crash inside native ProcessCustomer or between native order and receipt confirmation.',
 'Initial speed-10 continuation left core behind native time and bought nothing. Retained separately; same Serving checkpoint replayed at speed 1 after paused catch-up.'])
checks=[v for v in out.values() if isinstance(v,bool)]
out['verdict']='PASS' if all(checks) and Decimal(out['allConsumersHeldCentsAfterDrain'])==0 and out['ledgerFailuresAfterDrain']==0 else 'FAIL'
(s/'interruption-summary.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
