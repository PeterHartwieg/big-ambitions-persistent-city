#!/usr/bin/env python3
"""Reconcile actual transactions and summarize observed meal timing, without treating offers as sales."""
from pathlib import Path
import collections, json, statistics, sys

root = Path(sys.argv[1])
read = lambda name: json.loads((root / (name + '.json')).read_text())
summary = {'cases': {}, 'scope': '957-resident Mac headless native time skips, one 15/hour croissant shop, $10.25, skill 75. Two days per opening-hours case. No restocking, wider menus, nutrition or rendered-performance claim.'}
for label in ('all-day', 'breakfast-only', 'evening-only'):
    if not (root / (label + '-report.json')).exists(): continue
    records = []; report = read(label + '-report')
    for day in (1, 2):
        prefix = label + '-day-' + str(day)
        start = read(prefix + '-audit-start-population'); end = read(prefix + '-audit-end-population')
        assert start['Food']['MealRhythmVersion'] == end['Food']['MealRhythmVersion'] == 1
        a = {m['ResidentId']: m for m in start['Food']['Residents']}; b = {m['ResidentId']: m for m in end['Food']['Residents']}
        assert a.keys() == b.keys()
        assert all(0 <= b[k]['Purchases'] - a[k]['Purchases'] <= 3 for k in a)
        purchases = sum(b[k]['Purchases'] - a[k]['Purchases'] for k in a)
        spent = sum(b[k]['SpentCents'] - a[k]['SpentCents'] for k in a)
        for key in b:
            old, new = a[key]['Budget'], b[key]['Budget']
            assert new['OpeningBalanceCents'] + new['IncomeReceivedCents'] == sum(new[x] for x in ('BalanceCents', 'EssentialsPaidCents', 'HomeFoodPaidCents', 'PreparedFoodSpentCents', 'HeldPurchaseCents'))
            assert new['HeldPurchaseCents'] == 0
            assert new['PreparedFoodSpentCents'] - old['PreparedFoodSpentCents'] == b[key]['SpentCents'] - a[key]['SpentCents']
            # The first fixture day includes enrollment's current-day posting plus midnight.
            postings = new['LastPostedDay'] - old['LastPostedDay'] if old['LastPostedDay'] >= 0 else 2
            assert new['IncomeReceivedCents'] - old['IncomeReceivedCents'] == postings * new['DailyIncomeCents']
        native_start = read(prefix + '-start'); native_end = read(prefix + '-final')
        native_day = int(start['Minute'] // 1440)
        history = [h for h in end['Food']['History'] if h['Day'] == native_day]
        assert sum(h['Purchases'] for h in history) == purchases
        assert sum(h['RevenueCents'] for h in history) == spent
        assert all(sum(h['PurchaseMeals']) == sum(h['PurchaseHours']) == h['Purchases'] for h in history)
        assert native_start['shops'][0]['supply']['ProducerUnits'] - native_end['shops'][0]['supply']['ProducerUnits'] == purchases
        # Supply.BagUnits counts currently staffed registers and is zero after closing.
        # Raw native cargo retains the actual bag inventory across those midnight snapshots.
        bags = lambda state: sum(cargo['amount'] for item in state['nativeItems'] for cargo in item['cargoInstances'] or [] if cargo['itemName'] == 'ba:itemname_paperbag')
        assert bags(native_start) - bags(native_end) == purchases
        accounts = [row for row in report['accounts'] if row['Day'] == native_day]
        assert len(accounts) == 1 and abs(accounts[0]['Revenue'] - spent / 100) < .05
        receipts = native_end['receipts']
        for key in receipts:
            if int(key.rsplit(':', 1)[1]) == native_day:
                assert receipts[key].startswith('paid|') and any(':' + kind + ':' in key for kind in ('breakfast', 'lunch', 'dinner'))
        assert sum(int(key.rsplit(':', 1)[1]) == native_day for key in receipts) == purchases
        unique = {}; costs = []
        for trace in read(prefix + '-food-audit')['decisions']:
            intent = trace['Intent']; unique[intent['Id']] = intent
            costs.append(trace['ElapsedMilliseconds'])
        meals = collections.Counter(); desired = collections.defaultdict(list)
        for intent in unique.values():
            kind = intent['Id'].split(':')[-2]; meals[kind] += 1; desired[kind].append(intent['DesiredAt'] % 1440)
        records.append({'nativeDay': native_day, 'purchases': purchases, 'byMeal': history[0]['PurchaseMeals'],
            'hours': history[0]['PurchaseHours'], 'nativeRevenue': accounts[0]['Revenue'], 'nativeOperatingProfit': accounts[0]['OperatingProfit'],
            'existingStopPurchases': sum(b[k]['ExistingStopPurchases'] - a[k]['ExistingStopPurchases'] for k in a),
            'uniqueEvaluatedMeals': dict(meals), 'desiredMinuteRanges': {k: [min(v), statistics.median(v), max(v)] for k,v in desired.items()},
            'decisionMilliseconds': {'total': sum(costs), 'p95': sorted(costs)[int(.95 * (len(costs) - 1))], 'max': max(costs)},
            'elapsedSeconds': read(prefix + '-timing')['seconds'], 'residentsBuying': sum(b[k]['Purchases'] > a[k]['Purchases'] for k in a)})
    summary['cases'][label] = records
summary['allCasesComplete'] = len(summary['cases']) == 3
(root / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
