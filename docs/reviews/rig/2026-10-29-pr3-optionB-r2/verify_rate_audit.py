from pathlib import Path
from collections import defaultdict
import gzip,csv,json,math
s=Path(__file__).parent;rows=defaultdict(list)
with gzip.open(s/'rate-audit.csv.gz','rt',encoding='utf-8',newline='') as f:
 for row in csv.DictReader(f):rows[row['arm']].append(row)
inputs=json.loads((s/'rate-audit-inputs.json').read_text());out={}
for label,arm in rows.items():
 n=len(arm);k=sum(int(r['recipients']) for r in arm);drawn=sum(int(r['drawn']) for r in arm);dropped=sum(int(r['dropped']) for r in arm)
 p=k/n;z=1.959963984540054;d=1+z*z/n;a=(p+z*z/(2*n))/d;b=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d
 opening=inputs[label]['opening']['pending'];closing=inputs[label]['closing']['pending'];unique=len({r['visitKey'] for r in arm})
 result=dict(eligible=n,uniqueVisitKeys=unique,scheduledRecipients=k,share=p,wilson95=[0 if k==0 else a-b,a+b],drawn=drawn,dropped=dropped,openingPending=opening,closingPending=closing,auditExact=drawn==k+closing-opening+dropped,target25WithinInterval=a-b<=.25<=a+b)
 assert unique==n and result['auditExact'],(label,result)
 out[label]=result
print(json.dumps(out,indent=2));(s/'rate-audit-verification.json').write_text(json.dumps(out,indent=2))
