from pathlib import Path
import json,hashlib,subprocess
s=Path(__file__).parent
rows=[]
for arm in ['before','after']:
 root=Path.cwd().parent/('rig-pr3-'+arm)
 b=json.loads((s/(arm+'-build.json')).read_text())
 p=root/'build/PersistentCity/PersistentCity.dll'
 assert b['dll_sha256']==hashlib.sha256(p.read_bytes()).hexdigest()
 b.update(arm=arm,helper_sha256=hashlib.sha256((s/'after/PR3RigGuard.dll').read_bytes()).hexdigest() if arm=='after' else None,nativeMeasurements='fresh rerun' if arm=='after' else 'reused from evidence commit 7937557935ef658c8410d2f2070a21058311e307 at identical code and DLL SHA')
 if arm=='after':b['productionCodeCommit']='067e8c589e3fa96294cfe03360df479bd822ffab'
 rows.append(b)
(s/'builds.json').write_text(json.dumps(rows,indent=2))
(s/'performance-method.json').write_text(json.dumps(dict(rule='For whole-frame p50 and p95, compare median(after)-median(before) with the larger full within-build range. A positive change beyond that range fails. Retain all p99/max separately. Descriptive noise rule, not an equivalence test.',baseline='Three native before windows reused at unchanged 7357f78 and identical DLL from evidence commit 7937557935ef658c8410d2f2070a21058311e307; every after window rerun at 7a104d6.',sequence='Identical checkpoint, normal speed, ten-second warmup, 60-second sample, manual saves and reloads outside measurement. Three before historical windows versus three new after windows.',diagnostics='Detailed optional traces and admission probes disabled. No concurrent rig offline builds or tests during primary after windows; other system workloads remain uncontrolled.'),indent=2))
print(json.dumps(rows,indent=2))
