"""Compare native finances and individual purchases before/after skip batching."""
from pathlib import Path
import json,sys
reference,root=map(Path,sys.argv[1:3]);rows=[]
for day in range(1,4):
 residents=[];states=[]
 for folder in [reference,root]:
  data=json.loads((folder/f'one-small-day-{day}-audit-end-population.json').read_text())
  residents.append({m['ResidentId']:m for m in data['Food']['Residents']})
  states.append(json.loads((folder/f'one-small-day-{day}-final.json').read_text()))
 before,after=residents
 assert before.keys()==after.keys()
 for identity in before:
  for key in ['Purchases','SpentCents','ExistingStopPurchases','Budget']:
   assert before[identity][key]==after[identity][key],('economic divergence',day,identity,key)
 assert states[0]['accounts']==states[1]['accounts'],('native accounts',day)
 assert {s['PlaceId']:s['supply']['ProducerUnits'] for s in states[0]['shops']}=={s['PlaceId']:s['supply']['ProducerUnits'] for s in states[1]['shops']}
 timings=[json.loads((folder/f'one-small-day-{day}-skip-timing.json').read_text()) for folder in [reference,root]]
 rows.append({'testDay':day,'referenceSeconds':timings[0]['seconds'],'candidateSeconds':timings[1]['seconds'],
  'referenceFrames':states[0]['clock']['frames'],'candidateFrames':states[1]['clock']['frames'],
  'candidateAdvanceP95Ms':states[1]['clock']['p95AdvanceMs'],'candidateAdvanceMaxMs':states[1]['clock']['maxAdvanceMs'],
  'economicStateMatches':True})
result={'reference':str(reference),'candidate':str(root),'days':rows,
 'elapsedRatio':sum(r['candidateSeconds'] for r in rows)/sum(r['referenceSeconds'] for r in rows),
 'boundary':'Same Mac background rig, 957-resident checkpoint, all daily purchases, balances, native stock and accounts. Timing includes diagnostic trace capture and polling, excludes load/setup; hidden-skip latency is not normal rendered frame pacing.'}
(root/'skip-equivalence.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
