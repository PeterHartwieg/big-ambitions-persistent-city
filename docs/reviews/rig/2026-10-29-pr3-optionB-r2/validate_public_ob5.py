"""Read-only validation of the portable public evidence bundle."""
from pathlib import Path
import json,sys,hashlib,re,gzip,csv,collections
sys.dont_write_bytecode=True
def validate(root):
 manifest=json.loads((root/'file-manifest.json').read_text())
 files={p.relative_to(root).as_posix():p for p in root.rglob('*') if p.is_file() and not ('__pycache__' in p.parts and p.suffix=='.pyc')}
 assert set(files)==set(manifest)|{'file-manifest.json'}
 for name,info in manifest.items():
  p=files[name];assert p.stat().st_size==info['bytes'] and hashlib.sha256(p.read_bytes()).hexdigest()==info['sha256'],name
 total=sum(p.stat().st_size for p in files.values());assert total<5_000_000,total
 for name,p in files.items():
  assert p.suffix.lower() not in ['.dll','.hsg','.reg','.meta','.private'],name
  if p.suffix.lower()=='.png':continue
  text=gzip.decompress(p.read_bytes()).decode('utf-8') if p.suffix=='.gz' else p.read_text(errors='strict')
  assert not re.search(r'[A-Za-z]:[\\/]+Users[\\/]+',text,re.I),name
  assert not re.search(r'/(?:Users|home)/Peter/',text,re.I),name
 readme=(root/'README.md').read_text()
 for target in re.findall(r'\]\((?!https?://)([^)]+)\)',readme):assert (root/target.split('#')[0]).exists(),target
 sys.path.insert(0,str(root))
 import verify_financial as f,verify_resident_ledgers as l
 money={}
 for arm in ['before','after']:
  r=f.check(root/(arm+'-financial.jsonl'));ledger=l.read(root/(arm+'-resident-ledgers.json'));lv=l.verify(ledger)
  assert r['verdict']==lv['verdict']=='PASS'
  assert r['inputSha256']==json.loads((root/(arm+'-financial-verification.json')).read_text())['inputSha256']
  money[arm]=dict(orders=r['confirmedSales'],sum=r['nativeOrderSumDollars'],ledgers=lv['consumers'],buyers=lv['buyers'])
 audit=collections.defaultdict(list)
 with gzip.open(root/'rate-audit.csv.gz','rt',encoding='utf-8',newline='') as stream:
  for row in csv.DictReader(stream):
   assert int(row['drawn'])==int(row['recipients'])+int(row['pendingAfter'])-int(row['pendingBefore'])+int(row['dropped']),row['visitKey']
   audit[row['arm']].append(row)
 inputs=json.loads((root/'rate-audit-inputs.json').read_text());rates={}
 for label,rows in audit.items():
  n=len(rows);assert len({r['visitKey'] for r in rows})==n
  drawn=sum(int(r['drawn']) for r in rows);stops=sum(int(r['recipients']) for r in rows);drops=sum(int(r['dropped']) for r in rows)
  assert drawn==stops+inputs[label]['closing']['pending']-inputs[label]['opening']['pending']+drops
  rates[label]=dict(eligible=n,stops=stops,drawn=drawn,dropped=drops)
 acceptance=json.loads((root/'acceptance.json').read_text());assert acceptance['verdict']=='PASS' and all(acceptance['checks'].values())
 return dict(verdict='PASS',files=len(files),bytes=total,manifestVerified=True,privacyScan=True,relativeLinks=True,exactFinancialRecheck=money,independentPerAdmissionAndGlobalChanceAudit=rates,acceptanceChecks=len(acceptance['checks']))
if __name__=='__main__':print(json.dumps(validate(Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).parent),indent=2))
