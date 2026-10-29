from pathlib import Path
import json,re,hashlib,xml.etree.ElementTree as E
s=Path(__file__).parent
rows=json.loads((s/'offline-results.json').read_text())
out={};presence={}
for arm in ['before','after']:
 cmds=[r for r in rows if r['check'].startswith(arm+'-')]
 trx=E.parse(s/(arm+'-tests.trx'));ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
 counters=trx.find('.//t:Counters',ns).attrib
 names=[x.attrib['testName'] for x in trx.findall('.//t:UnitTestResult',ns)]
 warnings=[]
 for r in cmds:
  text=(s/(r['check']+'.log')).read_text(errors='replace')
  warnings.extend(dict(command=r['check'],text=x) for x in text.splitlines() if re.search(r'\bwarning\s+(?:CS|NU)\d+',x))
 out[arm]=dict(commands=len(cmds),passed=sum(r['exit']==0 for r in cmds),failed=[r['check'] for r in cmds if r['exit']],tests=counters,compilerWarnings=warnings,regressionProjects=sum(r['check'].endswith('-regression') for r in cmds),pythonHarnesses=sum(r['check'].endswith('-py') for r in cmds))
 presence[arm]={k:[n for n in names if k in n] for k in ['OptionalStopSchedulingTests','OptionalStopShareTests','OptionalStopHandoffTests','OptionalStopRoutineTests','OptionalStopSettingsTests','BaseMerge_']}
(s/'offline-summary.json').write_text(json.dumps(out,indent=2))
(s/'regression-presence.json').write_text(json.dumps(presence,indent=2))
print(json.dumps(out,indent=2))
assert out['after']['commands']==31 and out['after']['passed']==31
assert int(out['after']['tests']['passed'])==1551
assert all(not r['failed'] and not r['compilerWarnings'] for r in out.values())
