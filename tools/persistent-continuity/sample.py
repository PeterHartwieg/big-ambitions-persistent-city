#!/usr/bin/env python3
"""Collect read-only continuity evidence on a rig already owned by the campaign; no lifecycle, time or observer changes."""
import argparse
import json
import math
from pathlib import Path
import sys
import time

repo = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(repo / 'tools/neighborhood-economy'))
from mac_rig import MacRig, control

parser = argparse.ArgumentParser(__doc__)
parser.add_argument('campaign', help='Existing directory name under build; matching ownership required')
parser.add_argument('label')
parser.add_argument('--track', action='append', default=[])
parser.add_argument('--seconds', type=int, default=0)
parser.add_argument('--interval', type=float, default=2)
args = parser.parse_args()
if not args.label.replace('-', '').replace('_', '').isalnum() or not 0 <= args.seconds <= 600 or args.interval < 1:
    raise SystemExit('Use a safe label, 0–600 seconds and interval >=1 second')
if len(args.track) > 12:
    raise SystemExit('At most twelve tracked resident IDs')
run = MacRig(args.campaign, physical=True, mode='windowed')
if not run.root.is_dir():
    raise SystemExit('Campaign directory does not exist')
status = control('status')
if not status.get('held') or status.get('owner') != 'persistent-city-' + args.campaign:
    raise SystemExit('Rig must already be owned by this exact campaign; this script never acquires it')
end = time.monotonic() + args.seconds
index = 0
while True:
    result = run.probe('continuity', track=args.track)
    if result['runtimeFailed'] or result['physicalFailed']:
        raise RuntimeError('Runtime/physical adapter failed')
    name = args.label if args.seconds == 0 else args.label + '-' + str(index).zfill(4)
    run.record(name, result)
    cars = [r for r in result['residents'] if r.get('sample', {}).get('mode') == 'Car' and not r['sample']['stationary']]
    focus = result['focus']
    def distance(resident):
        return math.sqrt(sum((resident['logicalPosition'][a] - focus[a]) ** 2 for a in ('x', 'y', 'z')))
    cars.sort(key=distance)
    print(json.dumps({'sample': name, 'minute': result['stateMinute'], 'movingCars': len(cars), 'bodies': len(result['bodies']), 'candidates': [{'id': r['id'], 'position': r['logicalPosition'], 'distance': distance(r), 'remainingMinutes': r['sample']['totalMinutes'] - r['sample']['elapsedMinutes']} for r in cars[:8]]}), flush=True)
    index += 1
    if time.monotonic() >= end:
        break
    time.sleep(min(args.interval, max(0, end - time.monotonic())))
