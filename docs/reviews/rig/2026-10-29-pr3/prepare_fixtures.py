from pathlib import Path
import json,re,hashlib,shutil
s=Path(__file__).parent;profile=Path.home()/'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests';guard=profile/'ModsLocal/PR3RigGuard'
source=next(s.glob('before-qa-1-initial-*-saved-payload.json')).read_text(encoding='utf-8-sig')
current=next(s.glob('before-qa-1-initial-*-population.json')).read_text(encoding='utf-8-sig')
assert json.loads(source)['Schema']==13 and json.loads(current)['Schema']==14
(s/'schema13-fixture.private.json').write_text(source);(s/'schema14-fixture.private.json').write_text(current)
def label(t,n):
 t=re.sub(r'"Schema":(?:13|14),',f'"Schema":{n},',t)
 if n>=15:t=t.replace(',"Phase":',(',"OptionalStop":null'+(',"OptionalStopChoice":null' if n>=16 else '')+',"Phase":'))
 return t
cases=[]
for schema in [15,16]:
 exact=label(current,schema);legacy=label(source,schema)
 variants={'exact':exact,'legacy-exact':legacy,'legacy-twelve-digit':re.sub(r'"PriceCents":\d+', '"PriceCents":1025.279998779297',legacy,count=1),'mixed':exact.replace('"Arrears":0','"ArrearsCents":0',1),'hex':re.sub(r'"Price":\d+','"Price":0x10',exact,count=1),'exponent':re.sub(r'"Price":\d+','"Price":1e2',exact,count=1)}
 for tag,text in variants.items():
  name=f'schema{schema}-{tag}.private.json';(s/name).write_text(text);shutil.copy2(s/name,guard/name)
  cases.append(dict(file=name,schema=schema,expected='Loaded' if tag in ['exact','legacy-exact'] else 'Invalid',sha256=hashlib.sha256(text.encode()).hexdigest()))
future=label(current,16).replace('"Schema":16,','"Schema":17,');name='schema17-future.private.json';(s/name).write_text(future);shutil.copy2(s/name,guard/name);cases.append(dict(file=name,schema=17,expected='Future',sha256=hashlib.sha256(future.encode()).hexdigest()))
(s/'schema-cases.json').write_text(json.dumps(cases,indent=2));print('Prepared',len(cases),'strict schema cases; raw population fixtures private')
