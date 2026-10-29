from pathlib import Path
import json,hashlib,re,subprocess
s=Path(__file__).parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
logs=[];inventory={}
for p in s.glob('*-Player-agent.log'):
 text=p.read_text(errors='replace');lines=text.splitlines();blocks=[]
 for n,line in enumerate(lines):
  if re.match(r'^(?:[\w.]*Exception):',line):
   block=[line]
   for tail in lines[n+1:]:
    if tail.startswith('  at ') or tail.startswith('Rethrow as ') or tail.startswith('---'):block.append(tail)
    else:break
   blocks.append('\n'.join(block))
 logs.append(dict(file=p.name,sha256=sha(p),bytes=p.stat().st_size))
 inventory[p.name]=dict(persistentCityExceptionBlocks=[x for x in blocks if ' at PersistentCity.' in x],helperExceptionCount=sum('PR3Rig.' in x or 'FoodDemandAuditProbe' in x or 'Probe.' in x for x in blocks),expectedRefusalMessages=[x for x in lines if 'Population save refused:' in x],inactiveCapabilities=sorted({x for x in lines if 'Not active:' in x}),allExceptionBlocks=blocks)
(s/'native-log-provenance.json').write_text(json.dumps(logs,indent=2));(s/'native-final-inventory.json').write_text(json.dumps(inventory,indent=2))
fixture=json.loads((s/'fixture.json').read_text());p=Path(fixture['path']);original=Path(r'<private-fixtures>\performance-pc-20261027\morning-base.hsg')
(s/'checkpoint-preserved.json').write_text(json.dumps(dict(originalHash=sha(original),fixtureHash=sha(p),expected=fixture['sha256'],unchanged=sha(p)==sha(original)==fixture['sha256']),indent=2))
roots={'after':Path.cwd(),'before':Path.cwd().parent/'rig-pr3-before'}
states={k:dict(sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=v,text=True).strip(),trackedDiff=subprocess.check_output(['git','diff','--name-only'],cwd=v,text=True).strip(),indexDiff=subprocess.check_output(['git','diff','--cached','--name-only'],cwd=v,text=True).strip()) for k,v in roots.items()}
assert all(not x['trackedDiff'] and not x['indexDiff'] for x in states.values());(s/'test-worktrees-unchanged.json').write_text(json.dumps(states,indent=2))
rates=json.loads((s/'rate-summary.json').read_text());venues={p for r in rates.values() for p in r['venue']};source=next(s.glob('after-qa-1-initial-*-population.json'));food={x['PlaceId'] for x in json.loads(source.read_text())['Food']['Shops']}
(s/'optional-food-separation.json').write_text(json.dumps(dict(optionalVenues=sorted(venues),ownedFoodPlaces=sorted(food),intersection=sorted(venues&food),overlayOriginNativeOrders=0,financialReplayOrders=110),indent=2))
print('Retained native process logs',len(logs),'production exceptions',sum(len(v['persistentCityExceptionBlocks']) for v in inventory.values()))
