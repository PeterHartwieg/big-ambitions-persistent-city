from pathlib import Path
import json,hashlib
s=Path(__file__).parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def one(label,suffix):
 p=list(s.glob(label+'-*-'+suffix+'.json'));assert len(p)==1,(label,len(p));return p[0]
checkpoint=s/'optional-dwell-checkpoint.json'
if not checkpoint.exists():
 result={'verdict':'INCOMPLETE','reason':'No bound dwell checkpoint was observed in the selected normal-speed window.'}
else:
 q=read(checkpoint);ident=q['id'];prefix='optional-dwell-control'
 paths=[one(prefix+tag,'population') for tag in ['-pre-save','-saved','-reloaded']]
 docs=[read(p) for p in paths];people=[next(c for c in d['Citizens'] if c['Identity']['Id']==ident) for d in docs]
 finances=[read(s/(prefix+tag+'-finance.json')) for tag in ['-pre-save','-reloaded']]
 saved=one(prefix+'-saved','saved-payload');reload=one(prefix+'-reloaded','saved-payload')
 traces=[json.loads(line) for p in s.glob('default-visual-after-reload-start-*-optional.jsonl') for line in p.read_text().splitlines()]
 physical=[r['value'] for r in traces if r['kind']=='physical' and r['value']['id']==ident]
 before=[json.loads(line) for p in s.glob('default-visual-optional-start-*-optional.jsonl') for line in p.read_text().splitlines()]
 before_physical=[r['value'] for r in before if r['kind']=='physical' and r['value']['id']==ident]
 final=read(one('default-visual-resumed','population'));continued=next(c for c in final['Citizens'] if c['Identity']['Id']==ident)
 receipt_equal=finances[0]['receipts']==finances[1]['receipts']
 wallet_equal=finances[0]['wallets']==finances[1]['wallets']
 result={'resident':ident,'checkpointMinute':q['minute'],'stop':q['stop'],
  'identityUnchanged':people[0]['Identity']==people[1]['Identity']==people[2]['Identity'],
  'choiceUnchanged':people[0]['OptionalStopChoice']==people[1]['OptionalStopChoice']==people[2]['OptionalStopChoice'],
  'stopExactlyPreserved':people[0]['OptionalStop']==people[1]['OptionalStop']==people[2]['OptionalStop'],
  'savedPayloadByteIdenticalAcrossReload':hashlib.sha256(saved.read_bytes()).digest()==hashlib.sha256(reload.read_bytes()).digest(),
  'walletsCountersAndReceiptsUnchangedAcrossReload':wallet_equal and receipt_equal,
  'physicalBeforeReload':before_physical,'physicalAfterReload':physical,
  'sameNativeBodyAcrossReloadedDwellAndReturn':len({(p['bodyGeneration'],p['bodyInstance']) for p in physical})==1 and bool(physical),
  'originalActivityContinued':continued['CompletedJourneys']>people[0]['CompletedJourneys'] or continued['Phase']!=people[0]['Phase'] or continued['PlaceId']!=people[0]['PlaceId'],
  'continuation':{k:continued[k] for k in ['Phase','PlaceId','Activity','CompletedJourneys','NextEventMinute']},
  'dwellObservedBeforeReload':any(p['phase']=='dwell' for p in before_physical),
  'dwellObservedAfterReload':any(p['phase']=='dwell' for p in physical),
  'returnObservedAfterReload':any(p['phase']=='returning' for p in physical),
  'entranceOrWalkContinuationObserved':any(p['phase'] in ['returned-entrance','resumed-walk'] for p in physical),
  'hashes':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in paths+[saved,reload]}}
 required=[v for v in result.values() if isinstance(v,bool)]
 result['verdict']='PASS' if all(required) else 'FAIL'
(s/'optional-reload-summary.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
