from pathlib import Path
import json,re
import sys
s=Path(sys.argv[1]);rows={}
for name in ['before','after','after-full','after-final','before-control','after-control']:
 p=s/(name+'-controlled-native-window.log') if name.endswith('-control') else s/(name+'-Player-agent.log');
 if not p.exists():continue
 lines=p.read_text(errors='replace').splitlines();counts={};blocks=[]
 for i,l in enumerate(lines):
  if re.match(r'^\w+(?:Exception|Error):',l):
   stack=[]
   for following in lines[i+1:]:
    if not following.strip():break
    stack.append(following)
   kind='helper File.Move' if any('FoodDemandAuditProbe' in x for x in stack) else 'StreetLamp' if any('StreetLamp' in x for x in stack) else 'SaveCurrentPosition / midnight autosave' if any('SaveCurrentPosition' in x for x in stack) else 'other'
   counts[kind]=counts.get(kind,0)+1;blocks.append({'header':l,'kind':kind,'stack':stack})
 rows[name]={'counts':counts,'blocks':blocks}
(s/'exception-summary.json').write_text(json.dumps(rows,indent=2));print({k:v['counts'] for k,v in rows.items()})
