#!/usr/bin/env python3
"""Meal timing comparison on the owned isolated Mac. Never contacts Windows."""
from pathlib import Path
import json, os, shutil, sys, time
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'neighborhood-economy'))
from mac_rig import MacRig

campaign = os.environ.get('PC_FOOD_CAMPAIGN', 'meal-rhythm-mac-20261023')
run = MacRig(campaign, mode='headless')
base = 'FT Food Routines Neighborhood Base 20261023'
shop = 'address:ba%3Astreet_thirdstreet:30:shop'
cases = [('all-day', 6, 22), ('breakfast-only', 6, 10), ('evening-only', 17, 22)]

def stable(key, value):
    value = json.loads(json.dumps(value))
    if key == 'economy': value.pop('minute', None)
    if key == 'shops':
        for item in value: item['supply'].pop('AtMinute', None)
    return value

try:
    run.stage('preparing-mac'); run.begin()
    for label, opens, closes in cases:
        run.stage('setup-' + label)
        start = run.load(label, base); assert start['scope'] is None
        setup = run.record(label + '-setup', run.probe('neighborhood-setup', shops=[shop], opens=opens, closes=closes))
        for fixture in setup['staffing']:
            assert len(fixture['days']) == 7
            assert all(d['isOpen'] and d['shifts'] for d in fixture['days'])
            assert all(w['employeeExists'] and w['stationExists'] for d in fixture['days'] for w in d['shifts'])
        for day in (1, 2):
            prefix = label + '-day-' + str(day); run.stage(prefix)
            start = run.record(prefix + '-start', run.ready())
            run.record(prefix + '-audit-start', run.probe('audit-start'))
            run.record(prefix + '-skip-needs-start', run.probe('skip-needs-start'))
            started = time.monotonic()
            run.call(prefix + '-native-skip', 'console', {'line': 'skiptime 1d true'}); time.sleep(2)
            final = run.record(prefix + '-final', run.ready())
            run.record(prefix + '-skip-needs-end', run.probe('skip-needs-end'))
            run.record(prefix + '-timing', {'seconds': time.monotonic() - started, 'gameMinutes': final['nativeMinute'] - start['nativeMinute']})
            assert abs(final['nativeMinute'] - start['nativeMinute'] - 1440) < .1
            run.record(prefix + '-audit-end', run.probe('audit-end'))
            for name in ['food-audit.json', 'audit-start-population.json', 'audit-end-population.json']:
                shutil.copy2(run.helper / name, run.root / (prefix + '-' + name))
            print(prefix, 'purchases', final['purchases'] - start['purchases'], flush=True)
        run.record(label + '-report', run.probe('economy-report'))
        checkpoint = 'FT Food Routines Mac Meals ' + label + ' 20261023'
        run.call(label + '-save', 'save', {'name': checkpoint})
        if label == 'all-day':
            before = run.record(label + '-before-reload', run.probe('status'))
            after = run.record(label + '-after-reload', run.load(label + '-reload', checkpoint))
            for key in ['purchases', 'spendingCents', 'economy', 'accounts', 'shops', 'receipts']:
                assert stable(key, before[key]) == stable(key, after[key]), ('reload changed', key)
        replay = run.record(label + '-receipt-replay', run.probe('replay-meal-receipts'))
        for key in ['purchases', 'spendingCents', 'economy', 'accounts', 'shops', 'receipts']:
            assert replay['before'][key] == replay['after'][key], ('receipt replay changed', label, key)
        run.stage('verified-' + label)
    run.record('comparison-complete', {'cases': [c[0] for c in cases], 'days': 6, 'platform': 'Mac', 'mode': run.mode})
except BaseException as error:
    run.record('error', {'type': type(error).__name__, 'error': str(error)[:8000]}); raise
finally:
    run.close()
