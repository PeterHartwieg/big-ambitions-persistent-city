from pathlib import Path
import json,hashlib
s=Path(__file__).parent;out=[]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(x):return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def identities(r):return [{k:c[k] for k in ['Identity','HomeId','DayPlaceId','LeisureId','ShopId','Preferences']} for c in r['Citizens']]
for label in ['A1','B1','A2','B2','A3','B3']:
 a=list(s.glob(label+'-saved-*-population.json'));b=list(s.glob(label+'-reloaded-final-*-population.json'))
 if not a or not b:continue
 x,y=read(a[0]),read(b[0]);pa=next(s.glob(label+'-saved-*-saved-payload.json'));pb=next(s.glob(label+'-reloaded-final-*-saved-payload.json'))
 idsEqual=identities(x)==identities(y);consumersEqual=[r['ResidentId'] for r in x['Food']['Residents']]==[r['ResidentId'] for r in y['Food']['Residents']]
 row=dict(run=label,schemaBefore=x['Schema'],schemaAfter=y['Schema'],citizens=len(y['Citizens']),consumers=len(y['Food']['Residents']),identitiesAndAssignmentsEqual=idsEqual,consumerIdsEqual=consumersEqual,identityDigestBefore=digest(identities(x)),identityDigestAfter=digest(identities(y)),storedPayloadSha256Before=hashlib.sha256(pa.read_bytes()).hexdigest(),storedPayloadSha256After=hashlib.sha256(pb.read_bytes()).hexdigest(),storedPayloadBytesEqual=pa.read_bytes()==pb.read_bytes())
 row['pass']=x['Schema']==y['Schema']==13 and idsEqual and consumersEqual and row['storedPayloadBytesEqual'];out.append(row)
(s/'roundtrip-summary.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
