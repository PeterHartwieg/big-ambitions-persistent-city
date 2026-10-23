"""Check native receipt, stock, return and daily-settlement conservation."""
import argparse, json, math
from collections import defaultdict
from pathlib import Path

p = argparse.ArgumentParser(__doc__)
p.add_argument('run', type=Path)
root = p.parse_args().run
load = lambda name: json.loads((root / (name + '.json')).read_text())
pilot, setup, supply = load('pilot-final'), load('pilot-setup'), load('supply-checks')
receipts = pilot['receipts']
assert receipts and all(r.get('paid') and r.get('completed') and r.get('nativeSale') for r in receipts), receipts
assert len({r['intentKey'] for r in receipts}) == len(receipts)
assert len(receipts) <= 3
returns = {d['intentKey']:d['returnedAt'] for d in pilot['decisions'] if 'returnedAt' in d}
expected = defaultdict(lambda: {'customers':0, 'revenue':0})
for r in receipts:
    assert r['foodBefore'] - r['foodAfter'] == 1
    assert r['bagsBefore'] - r['bagsAfter'] == 1
    assert r['ordersAfter'] - r['ordersBefore'] == 1
    assert 0 < r['arrival'] < r['servedAt'] < returns[r['intentKey']]
    day = math.floor(r['servedAt'] / 1440)
    expected[day]['customers'] += 1
    expected[day]['revenue'] += r['price']
history = {h['dayNumber']:h for snapshot in pilot['daily'] for h in snapshot['history']}
for day, values in expected.items():
    h = history[day]
    assert h['totalCustomers'] == values['customers'], (day, h, values)
    assert abs(h['totalRevenue'] - values['revenue']) < .011, (day, h, values)
assert pilot['daily'][-1]['pendingOrders'] == 0
assert pilot['daily'][-1]['productUnits'] == setup['quantity'] - len(receipts)
assert pilot['daily'][-1]['bagUnits'] == setup['quantity'] - len(receipts)
assert pilot['suppressedBatches'] > 0 and pilot['suppressedDraws'] > 0
assert supply['baseline']['Units'] == setup['quantity']
assert supply['baseline']['MealsPerHour'] == setup['customerCapacity'] == 15
assert supply['readUnchanged'] and supply['restored'] and all(supply['checks'].values())
bank = load('bank-final')
assert bank['ok']
latest_day = max(expected)
transactions = [t for t in bank['result']['transactions_today']
                if t['type'] == 'ba:transaction_revenue' and t['day'] == latest_day
                and t['label'] == setup['BusinessName'] + ' Revenue']
assert len(transactions) == 1 and abs(transactions[0]['amount'] - expected[latest_day]['revenue']) < .011
result = {'nativePurchases':len(receipts), 'nativeRevenue':sum(r['price'] for r in receipts),
          'allReturned':True, 'stockAndOrdersConserved':True, 'dailyHistoryMatchesReceipts':True,
          'nativeDemandSuppressedForFixture':True, 'availabilityChecksPassed':True,
          'latestBankRevenueMatches':True, 'settledDays':dict(expected)}
(root / 'native-checks.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result, indent=2))
