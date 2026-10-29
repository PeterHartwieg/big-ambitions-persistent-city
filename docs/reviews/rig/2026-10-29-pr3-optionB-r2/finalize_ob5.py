"""Analyze fresh AFTER telemetry and explicitly reused BEFORE evidence; no gameplay mutation."""
from pathlib import Path
import subprocess,sys,json,hashlib
s=Path(__file__).parent;rows=[]
for script,args in [('summarize_offline.py',[]),('analyze_campaign.py',[]),('verify_finance_arms.py',['before','after']),('verify_verifiers.py',[]),('analyze_rates.py',[]),('analyze_decreases.py',[]),('export_rate_audit.py',[]),('verify_rate_audit.py',[]),('default_gate.py',[]),('analyze_migrations.py',[]),('analyze_persistence.py',[]),('verify_events.py',[]),('analyze_optional_reload.py',[]),('verify_optional_food_separation.py',[]),('collect_final.py',[])]:
 with (s/(Path(script).stem+'-analysis.log')).open('w',encoding='utf-8') as out:
  p=subprocess.run([sys.executable,str(s/script),*args],stdout=out,stderr=subprocess.STDOUT)
 rows.append(dict(script=script,args=args,exit=p.returncode))
 (s/'analysis-exits.json').write_text(json.dumps(rows,indent=2));print(script,p.returncode,flush=True)
 if p.returncode:raise SystemExit(p.returncode)
shot=s.parent/'screenshots/persistent-city-pr3-ob5-2026-10-29/default-optional-dwell.png'
source=json.loads((s/'default-visual-dwell-image.json').read_text())
(s/'visual-source.json').write_text(json.dumps(dict(source=source,selected='path (native scene-camera diagnostic render)',bytes=shot.stat().st_size,sha256=hashlib.sha256(shot.read_bytes()).hexdigest(),claim='The normal-speed observer records native approach, dwell, return and continuation; screenshot alone is not a completion census.'),indent=2))
def read(n):
 b=(s/n).read_bytes();return json.loads(b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig'))
rates=read('rate-summary.json');offline=read('offline-summary.json');perf=read('performance-summary.json');noise=read('performance-noise.json');persist=read('persistence-summary.json');events=read('event-persistence-cases.json');detail=read('event-detail-verification.json');native=read('native-final-inventory.json');qa=read('native-qa-summary.json');decreases=read('decrease-comparison.json')
beforeTrace=[json.loads(x) for x in next(s.glob('before-optional-start-*-optional.jsonl')).read_text().splitlines()]
presence=read('regression-presence.json')
checks={
 'beforeAbsence':all(not x['value']['featurePresent'] and not x['value']['stops'] and not x['value']['faults'] for x in beforeTrace),
 'regressionPresence':all(not v for v in presence['before'].values()) and all(v for v in presence['after'].values()),
 'offline':offline['after']['passed']==31 and offline['before']['passed']==30 and not offline['after']['compilerWarnings'],
 'defaultLongRun':read('default-rate-gate.json')['passed'],
 'independentRateAudit':all(v['auditExact'] and v['scheduledUnique']==v['scheduledRecipients'] and not v['observerFaults'] for v in rates.values()),
 'zero':rates['rate0']['scheduledRecipients']==rates['rate0']['drawn']==rates['rate0']['tallyDelta']['Dropped']==0,
 'hundred':rates['rate100']['drawn']==rates['rate100']['eligible'] and rates['rate100']['share']>.25,
 'settings':len(read('config-summary.json'))==8 and all(v['passed'] for v in read('config-summary.json')),
 'decreaseArms':len(decreases)==4 and all(v['auditExact'] and v['trimIdsOrdinallySorted'] and len(v['daily'])==10 for v in decreases.values()),
 'pendingAtZero':read('pending-zero-summary.json')['pendingPreserved'] and all(rates['pending-zero']['tallyDelta'][k]==0 for k in ['Drawn','Realised','HandedOn','PassedOn','Dropped']),
 'migrations':len(read('migration-summary.json'))>=6 and all(v['passed'] for v in read('migration-summary.json').values()),
 'codecRefusals':len(read('native-codec-summary.json'))==13 and all(v['passed'] for v in read('native-codec-summary.json')),
 'nativeRefusals':len(read('native-refusal-summary.json'))==5 and all(v['runtimeFailed'] and v['preservedAcrossSaveReload'] for v in read('native-refusal-summary.json')),
 'finance':all(read(a+'-financial-verification.json')['verdict']=='PASS' and read(a+'-resident-ledgers.json')['verification']['verdict']=='PASS' for a in ['before','after']),
 'persistence':len(persist)==8 and all(v['passed'] for v in persist.values()),
 'transferAndConversationReloads':len(events)==3 and all(v['observed'] and v['saveReloadCompleted'] for v in events) and detail['event-conversation']['pairPassed'],
 'visibleDwellReturn':read('optional-reload-summary.json')['verdict']=='PASS',
 'timingWindows':len(perf)==6 and all(v['validity']['clean'] and v['seconds']>=60 and v['timedExceptions']==0 and v['crossFrameCalls']==0 for v in perf),
 'wholeFrameNoise':not any(v['worseBeyondObservedSpread'] for v in noise if v['scope']=='wholeFrame' and v['metric'] in ['p50','p95']),
 'nativeQA':not any(v['persistentCityExceptionBlocks'] for v in native.values()) and qa['before-qa-1']['inactiveLines']==qa['after-qa-1']['inactiveLines'],
 'checkpointPreserved':read('checkpoint-preserved.json')['unchanged'],
}
result=dict(verdict='PASS' if all(checks.values()) else 'FAIL',checks=checks,cleanup='Pending separate mandatory hash verification and lease release; not yet a final verdict.',testedHead='7a104d6f41d7c92579dcdf2e790274856d31cb29',productionCode='067e8c589e3fa96294cfe03360df479bd822ffab')
confirmation=read('performance-confirmation-result.json')
checks['freshTimingWindows']=confirmation['saveCleanAndNoTimedErrors']
checks['freshWholeFrameNoise']=not confirmation['wholeFrameBeyondSpread']
result['verdict']='PASS' if all(checks.values()) else 'FAIL'
(s/'acceptance.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
