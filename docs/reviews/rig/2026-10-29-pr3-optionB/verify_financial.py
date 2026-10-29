"""Exact rig financial verifier: integer rationals, no tolerance or comparison rounding.

Inputs are FinanceObserver JSONL. Native binary32 values are reconstructed from
their captured bits; resident decimal cents are parsed as Decimal, never float.
The binary32 round-trip comparison is reported separately and cannot pass the
exact equality gate. No game/save writes are performed.
"""
from pathlib import Path
from fractions import Fraction
from decimal import Decimal,localcontext
import json,struct,sys,hashlib

def number(v):
 if isinstance(v,dict) and v.get('type')=='binary32':return Fraction.from_float(struct.unpack('<f',struct.pack('<I',v['bits']))[0])
 if isinstance(v,dict) and v.get('type')=='binary64':return Fraction.from_float(struct.unpack('<d',struct.pack('<q',v['bits']))[0])
 if isinstance(v,dict) and "Units" in v:return Fraction(int(v["Units"]),268435456)
 return Fraction(v)
def moneyfield(obj,name):return obj[name] if name in obj else obj[name+'Cents']

def raw(v):
 # Finite decimal expansion by integer division; never round a rational for output.
 v=number(v);sign='-' if v<0 else '';numerator=abs(v.numerator);whole,remainder=divmod(numerator,v.denominator)
 digits=[];seen=set()
 while remainder:
  if remainder in seen:raise ValueError('Non-terminating rational has no exact finite decimal representation')
  seen.add(remainder);digit,remainder=divmod(remainder*10,v.denominator);digits.append(str(digit))
 return sign+str(whole)+('.'+''.join(digits) if digits else '')

def norm(obj):
 if isinstance(obj,Fraction):return raw(obj)
 if isinstance(obj,Decimal):return str(obj)
 if isinstance(obj,list):return [norm(x) for x in obj]
 if isinstance(obj,dict):return {k:norm(v) for k,v in obj.items()}
 return obj
def parse(path):return [json.loads(l,parse_float=Decimal) for l in path.read_text(encoding='utf-8-sig').splitlines() if l.strip()]
def check(path):
 rows=parse(path);starts=[r['value'] for r in rows if r['kind']=='start'];ends=[r['value'] for r in rows if r['kind']=='end']
 assert len(starts)==len(ends)==1,'One closed measurement interval required'
 a,b=starts[0],ends[0];sales=[r['value'] for r in rows if r['kind']=='confirmed'];checkouts=[r['value'] for r in rows if r['kind']=='checkout']
 detail=[];native_sum=Fraction();charge_sum=Fraction()
 for sale in sales:
  q=sale.get('checkout');assert q,'Missing native checkout observation'
  entries=[e for o in q['newNativeOrders'] for e in o['entries'] if e['paid'] and e['itemName']!='ba:itemname_paperbag']
  assert len(entries)==1,'Exactly one new paid non-bag order per confirmed sale required'
  native=number(entries[0]['price']);charge=number(sale['priceCents'])/100
  markers=list(q['receipt'].values());assert len(markers)==1 and markers[0].startswith('paid|'),'Exactly one paid receipt required'
  receipt=Fraction(Decimal(markers[0].split('|')[1]))/100
  spent_delta=(number(moneyfield(sale['budget'],'PreparedFoodSpent'))-number(q['spentBeforeCents']))/100
  native_sum+=native;charge_sum+=charge
  native_bits=entries[0]['price']['bits'];roundtrip_bits=struct.unpack('<I',struct.pack('<f',float(charge)))[0]
  detail.append(dict(sequence=sale['sequence'],intent=sale['IntentId'],resident=sale['ResidentId'],nativePriceBits=native_bits,
   nativeDollars=native,residentDollars=charge,receiptDollars=receipt,preparedLedgerDeltaDollars=spent_delta,
   nativeMinusResidentDollars=native-charge,exactNativeEqualsResident=native==charge,
   walletReceiptChargeAgree=spent_delta==receipt==charge and number(q['heldCents'])==charge*100 and number(moneyfield(sale['budget'],'HeldPurchase'))==0,
   nativeFloatRoundTripIdentity=roundtrip_bits==native_bits))
 def hist(snapshot):
  return {(shop['place'],h['dayNumber']):h for shop in snapshot['shops'] for h in shop['history']}
 ah,bh=hist(a),hist(b)
 days=[]
 for key,h in bh.items():
  old=ah.get(key);count=h['totalCustomers']-(old['totalCustomers'] if old else 0)
  if count<=0:continue
  resident=sum((number(x['priceCents'])/100 for x in sales if x['day']==key[1]),Fraction())
  revenue=number(h['totalRevenue'])-(number(old['totalRevenue']) if old else 0)
  account_start=[r for r in a['accounts'] if r['PlaceId']==key[0] and r['Day']==key[1]]
  account_end=[r for r in b['accounts'] if r['PlaceId']==key[0] and r['Day']==key[1]]
  account=(number(account_end[0]['Revenue'])-(number(account_start[0]['Revenue']) if account_start else 0)) if len(account_end)==1 else None
  days.append(dict(place=key[0],day=key[1],nativeCustomers=count,residentPaymentsDollars=resident,nativeHistoryRevenueDollars=revenue,
   nativeAccountRevenueDollars=account,historyMinusResidentDollars=revenue-resident,
   accountMinusResidentDollars=None if account is None else account-resident,
   exactHistoryEqualsResident=resident==revenue,exactAccountEqualsResident=account is not None and resident==account))
 # Native cash includes wages/resources and other transactions; preserve every raw transaction
 # and its own balance arithmetic. It is not equated to gross food revenue without those costs.
 at=a['transactions'];bt=b['transactions'];new=[t for t in bt if t not in at]
 cash_delta=number(b['cash'])-number(a['cash']);transaction_sum=sum((number(t['amount']) for t in new),Fraction())
 revenue_transactions=[t for t in new if t['transactionType']=='ba:transaction_revenue']
 native_cash_revenue=sum((number(t['amount']) for t in revenue_transactions),Fraction())
 other_cash_transactions=sum((number(t['amount']) for t in new if t not in revenue_transactions),Fraction())
 cash_implied_gross=cash_delta-other_cash_transactions
 for day in days:
  posted=sum((number(t['amount']) for t in revenue_transactions if t['timestamp']['Day']==day['day']),Fraction())
  day['nativeCashRevenueTransactionDollars']=posted
  day['cashRevenueMinusResidentDollars']=posted-day['residentPaymentsDollars']
  day['exactCashRevenueEqualsResident']=posted==day['residentPaymentsDollars']
 wallet_spent_delta=(number(b['wallets']['spentCents'])-number(a['wallets']['spentCents']))/100
 prepared_delta=(number(b['wallets']['preparedCents'])-number(a['wallets']['preparedCents']))/100
 out=dict(inputSha256=hashlib.sha256(path.read_bytes()).hexdigest(),comparison='Gate (a)/(b): exact integer rationals; native captured IEEE bits; resident Int64 units / 2^28 cents or legacy cents. Native running-total drift (c) is recorded, not a mod failure. No tolerance, no rounding.',
  confirmedSales=len(sales),observedCheckouts=len(checkouts),nativeOrderSumDollars=native_sum,residentPaymentSumDollars=charge_sum,
  nativeOrdersMinusResidentDollars=native_sum-charge_sum,walletSpentDeltaDollars=wallet_spent_delta,preparedLedgerDeltaDollars=prepared_delta,
  everyWalletAndReceiptAgrees=all(r['walletReceiptChargeAgree'] for r in detail),
  exactNativePaymentMatches=sum(r['exactNativeEqualsResident'] for r in detail),floatRoundTripMatches=sum(r['nativeFloatRoundTripIdentity'] for r in detail),
  walletsAggregateMatchesPayment=wallet_spent_delta==prepared_delta==charge_sum,
  ledgerFailuresBefore=a['wallets']['ledgerFailures'],ledgerFailuresAfter=b['wallets']['ledgerFailures'],heldCentsAtEnd=b['wallets']['heldCents'],
  nativeCashBefore=a['cash'],nativeCashAfter=b['cash'],nativeCashDeltaDollars=cash_delta,newNativeTransactions=new,newTransactionSumDollars=transaction_sum,
  cashDeltaEqualsTransactionSum=cash_delta==transaction_sum,cashResidualDollars=cash_delta-transaction_sum,
  nativeCashRevenueTransactionsDollars=native_cash_revenue,otherCashTransactionsDollars=other_cash_transactions,
  cashImpliedGrossRevenueDollars=cash_implied_gross,cashRevenueMinusResidentDollars=native_cash_revenue-charge_sum,
  cashImpliedGrossMinusResidentDollars=cash_implied_gross-charge_sum,exactCashRevenueEqualsResident=native_cash_revenue==charge_sum,
  days=days,payments=detail,observerFaults=a['observerFaults']+b['observerFaults'])
 receipt_sum=sum((r['receiptDollars'] for r in detail),Fraction())
 out['receiptSumDollars']=receipt_sum
 out['gateA']='PASS' if sales and all(r['exactNativeEqualsResident'] and r['walletReceiptChargeAgree'] for r in detail) else 'FAIL'
 out['gateB']='PASS' if sales and wallet_spent_delta==prepared_delta==charge_sum==receipt_sum==native_sum else 'FAIL'
 out['gateC']={'reference':'Exact native order sum; native float32 accumulation drift is attributed to the game', 'nativeHistorySumDollars':sum((d['nativeHistoryRevenueDollars'] for d in days),Fraction()), 'historyMinusExactOrderSumDollars':sum((d['nativeHistoryRevenueDollars'] for d in days),Fraction())-native_sum, 'cashRevenueMinusExactOrderSumDollars':native_cash_revenue-native_sum, 'cashImpliedGrossMinusExactOrderSumDollars':cash_implied_gross-native_sum}
 out['verdict']='PASS' if out['gateA']==out['gateB']=='PASS' and sum(d['nativeCustomers'] for d in days)==len(sales) and out['walletsAggregateMatchesPayment'] and not out['observerFaults'] and out['ledgerFailuresBefore']==out['ledgerFailuresAfter']==0 and number(out['heldCentsAtEnd'])==0 else 'FAIL'

 return norm(out)
if __name__=='__main__':
 p=Path(sys.argv[1]);result=check(p);target=Path(sys.argv[2]) if len(sys.argv)>2 else p.with_suffix('.verification.json')
 target.write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ['payments','newNativeTransactions']},indent=2))
 sys.exit(0 if result['verdict']=='PASS' else 1)
