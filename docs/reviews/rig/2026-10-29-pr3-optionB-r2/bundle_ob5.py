"""Publish only small evidence, with private paths replaced; never include save data or binaries."""
from pathlib import Path
import json,re,shutil,hashlib,gzip,csv,statistics
s=Path(__file__).parent;dest=s/'public';dest.mkdir(exist_ok=False)
home=str(Path.home());profile=str(Path.home()/'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests')
roots=[(profile,'<agent1-profile>'),(str(s),'<private-evidence>'),(str(Path.cwd().parent),'<worktrees>'),(r'<private-fixtures>','<private-fixtures>'),(home,'<user-home>'),(r'<steam-install>','<steam-install>')]
def readtext(p):
 b=p.read_bytes();return b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig',errors='replace')
def clean(t):
 for root,label in roots:
  for variant in [root,root.replace('\\','/'),root.replace('\\','\\\\')]:t=re.sub(re.escape(variant),lambda m:label,t,flags=re.I)
 return t
def write(name,t):
 p=dest/name;p.parent.mkdir(parents=True,exist_ok=True);t=clean(t)
 if p.suffix in ['.cs','.ps1']:t=t.rstrip('\r\n')+('\r\n' if '\r\n' in t else '\n')
 p.write_text(t,encoding='utf-8',newline='\n')
def read(name):return json.loads(readtext(s/name))
def dump(name,obj):write(name,json.dumps(obj,indent=2)+'\n')
summary=['builds.json','baseline-reuse.json','offline-summary.json','offline-results.json','regression-presence.json','performance-method.json','performance-summary.json','performance-noise.json','native-qa-summary.json','autosave-evidence.json','default-rate-gate.json','light25-summary.json','config-summary.json','migration-summary.json','native-codec-summary.json','native-refusal-summary.json','persistence-summary.json','optional-reload-summary.json','event-persistence-cases.json','event-detail-verification.json','pending-zero-summary.json','verifier-self-checks.json','rate-audit-inputs.json','rate-audit-verification.json','backup-receipt.json','restore-receipt.json','lock-released.json','long-phase-exits.json','native-campaign-exits.json','native-log-provenance.json','optional-food-separation.json','checkpoint-preserved.json','test-worktrees-unchanged.json','decrease-comparison.json','acceptance.json','visual-source.json']
for n in summary:
 assert (s/n).exists(),n
 write(n,readtext(s/n))
for n in ['analysis-exits.json','native-final-inventory.json','performance-confirmation-plan.json','performance-confirmation-summary.json','performance-confirmation-noise.json','performance-confirmation-result.json','performance-interference.json','event-persistence-initial-attempts.json','fresh-before-installed.json','fresh-after-installed.json','autosave-confirmation-evidence.json','persistence-chronology.json','tooling-notes.json']:
 write(n,readtext(s/n))
lease=read('lease-acquired.json');dump('lease-acquired.json',{k:lease[k] for k in ['instance','held','owner','purpose','acquired_utc','expires_automatically','pending','legacy_blocked'] if k in lease})
for arm in ['before','after']:
 for suffix in ['financial-verification.json','resident-ledgers.json']:
  write(arm+'-'+suffix,readtext(s/(arm+'-'+suffix)))
 p=next(s.glob(arm+'-finance-start-*-financial.jsonl'));write(arm+'-financial.jsonl',readtext(p))
p=next(s.glob('before-optional-start-*-optional.jsonl'));write('before-optional.jsonl',readtext(p))
shutil.copy2(s/'rate-audit.csv.gz',dest/'rate-audit.csv.gz')
rates=read('rate-summary.json');small={}
with (dest/'rates-hourly.csv').open('w',newline='',encoding='utf-8') as f,(dest/'rates-daily.csv').open('w',newline='',encoding='utf-8') as g:
 h=csv.writer(f);d=csv.writer(g);h.writerow(['arm','nativeHour','eligible','scheduledRecipients','share','pendingEnd','drawn','dropped']);d.writerow(['arm','day','eligible','scheduledRecipients','share','openingPending','closingPending','drawn','dropped'])
 for label,r in rates.items():
  for x in r['hourly']:h.writerow([label,x['bin'],x['eligible'],x['scheduledRecipients'],x['share'],x['pendingEnd'],x['drawn'],x['dropped']])
  for x in r['daily24h']:d.writerow([label]+[x[k] for k in ['day','eligible','scheduledRecipients','share','openingPending','closingPending','drawn','dropped']])
  small[label]={k:v for k,v in r.items() if k not in ['hourly','daily','daily24h','metrics','trimOrder']}
  small[label]['trimOrder']=[x for x in r['trimOrder'] if x['reason']=='trim']
  small[label]['omittedIndividual100PolicyDropRecords']=sum(x['reason']=='100-policy-trim' for x in r['trimOrder'])
write('rate-summary.json',json.dumps(small,separators=(',',':'))+'\n')
dump('bundle-selection.json',dict(rateSummary='Hourly and daily records are separate CSVs. Detailed observer timings are excluded from production timing. Actual settings-trim order is retained; duplicate individual 100-policy drop IDs are omitted. All counts, causes, source trace hashes and the per-admission main-arm audit remain.',fullPrivateRateSummarySha256=hashlib.sha256((s/'rate-summary.json').read_bytes()).hexdigest()))
with (dest/'stops-per-citizen.csv').open('w',newline='',encoding='utf-8') as f:
 w=csv.writer(f);w.writerow(['arm','citizen','eligible','scheduledStops'])
 for label in ['rate25','rate25-continued']:
  for ident,r in read(label+'-per-citizen.json').items():w.writerow([label,ident,r['eligible'],r['scheduledRecipients']])
metrics={}
for rate in [25,100]:
 r=read('metrics'+str(rate)+'-stop.json');values=sorted(r.pop('scheduleMilliseconds'));q=lambda p:values[int((len(values)-1)*p)] if values else None
 r['scheduleMillisecondsSummary']=dict(samples=len(values),p50=q(.5),p95=q(.95),p99=q(.99),maximum=values[-1] if values else None,mean=statistics.mean(values) if values else None);metrics[str(rate)]=r
dump('admission-metrics.json',metrics)
for n in ['after100-window-1-stop.json','after100-window-1-validity.json','after100-window-1-autosave-before.json','after100-window-1-autosave-after.json']:
 write(n,readtext(s/n))
scripts=['rig.py','offline.py','summarize_offline.py','reuse_before.py','build_metadata.py','build_guard.py','RigGuard.cs','FinanceObserver.cs','OptionalObserver.cs','Wait-Lease.ps1','Backup-Rig.ps1','Install.ps1','Launch-Rig.ps1','Restore-Rig.ps1','Release.ps1','fixture.py','load.py','measure.py','run_windows.py','financial_run.py','native_control.py','native_after.py','verify_financial.py','verify_resident_ledgers.py','verify_finance_arms.py','verify_verifiers.py','rate_arms.py','extend_default.py','light_rate.py','phase_long.py','analyze_rates.py','default_gate.py','export_rate_audit.py','verify_rate_audit.py','settings_only.py','pending_zero.py','metrics_arms.py','perf100.py','prepare_fixtures.py','codec_batch.py','schema_native.py','schema_native_helpers.py','migration14.py','native_matrix.py','analyze_migrations.py','event_persistence.py','analyze_persistence.py','optional_visual.py','analyze_optional_reload.py','analyze_campaign.py','verify_events.py','analyze_decreases.py','verify_optional_food_separation.py','collect_final.py','finalize_ob5.py','report_ob5.py','bundle_ob5.py']
scripts.append('cleanup_metadata_ob5.py')
scripts += ['performance_confirmation.py','analyze_confirmation.py','handon_default_retry.py','native_control-v1.py','Install-v1.ps1','evidence_metadata_ob5.py']
scripts.append('validate_public_ob5.py')
for n in scripts:write(n,readtext(s/n))
parts=[]
for r in read('offline-results.json'):parts.append('COMMAND '+r['check']+' EXIT '+str(r['exit'])+'\n'+readtext(s/(r['check']+'.log')))
with gzip.open(dest/'offline-output.txt.gz','wt',encoding='utf-8',newline='\n') as f:f.write(clean('\n\n'.join(parts)))
shot=s.parent/'screenshots/persistent-city-pr3-ob5-2026-10-29/default-optional-dwell.png';assert shot.exists()
(dest/'screenshots').mkdir();shutil.copy2(shot,dest/'screenshots/default-optional-dwell.png')
write('README.md',readtext(s/'report-complete.md'));write('.gitattributes','* -text whitespace=cr-at-eol\n')
files=[p for p in dest.rglob('*') if p.is_file()]
assert not any(p.suffix.lower() in ['.dll','.hsg','.reg','.meta','.private'] for p in files)
for p in files:
 if p.suffix not in ['.png','.gz']:assert not re.search(r'C:[/\\]+Users[/\\]+Peter',p.read_text(errors='replace'),re.I),p.name
dump('file-manifest.json',{str(p.relative_to(dest)).replace('\\','/'):dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in files})
size=sum(p.stat().st_size for p in dest.rglob('*') if p.is_file());assert size<5*1024*1024,size
print('Sanitized bundle',size,'bytes',len(files)+1,'files')
