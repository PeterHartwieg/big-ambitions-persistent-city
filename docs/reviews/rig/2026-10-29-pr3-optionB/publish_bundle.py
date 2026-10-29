"""Build the deliberately small, sanitised evidence bundle after cleanup."""
from pathlib import Path
import json,re,shutil,csv,hashlib,statistics
s=Path(__file__).parent;dest=s/'public';dest.mkdir(exist_ok=False)
def readtext(p,errors='strict'):
 b=p.read_bytes()
 return b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig',errors=errors)
home=str(Path.home());profile=str(Path.home()/'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests')
roots=[(profile,'<agent1-profile>'),(str(s),'<private-evidence>'),(str(Path.cwd().parent),'<worktrees>'),(home,'<user-home>'),(r'<private-fixtures>','<private-fixtures>')]
def clean(text):
 for root,label in roots:
  for variant in [root,root.replace('\\','/'),root.replace('\\','\\\\')]:text=re.sub(re.escape(variant),lambda m:label,text,flags=re.I)
 return text
def write(name,text):
 p=dest/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(clean(text),encoding='utf-8',newline='\n')
def read(name):return json.loads(readtext(s/name))
def dump(name,obj):write(name,json.dumps(obj,indent=2)+'\n')
summaries=['builds.json','offline-summary.json','offline-results.json','regression-presence.json','performance-method.json','performance-summary.json','performance-noise.json','performance-interference.json','native-qa-summary.json','autosave-evidence.json','default-rate-gate.json','light25-summary.json','config-summary.json','migration-summary.json','native-codec-summary.json','native-refusal-summary.json','persistence-summary.json','optional-reload-summary.json','event-persistence-cases.json','pending-zero-summary.json','verifier-self-checks.json','rate-audit-inputs.json','rate-audit-verification.json','observer-tooling-correction.json','request-filelock-correction.json','backup-receipt.json','restore-receipt.json','lock-released.json','lease-acquired.json','long-phase-exits.json','native-log-provenance.json','optional-food-separation.json','checkpoint-preserved.json','test-worktrees-unchanged.json','native-final-inventory.json','decrease-comparison.json']
summaries.append('event-detail-verification.json')
summaries.append('conversation-reload-change.json')
for name in summaries:
 if (s/name).exists():write(name,readtext(s/name))
for arm in ['before','after']:
 for suffix in ['financial-verification.json','resident-ledgers.json']:
  name=arm+'-'+suffix;data=(s/name).read_bytes();assert clean(data.decode())==data.decode();(dest/name).write_bytes(data)
 p=next(s.glob(arm+'-finance-start-*-financial.jsonl'));data=p.read_bytes();assert clean(data.decode())==data.decode();(dest/(arm+'-financial.jsonl')).write_bytes(data)
for name in ['rate-audit.csv.gz']:
 shutil.copy2(s/name,dest/name)
rates=read('rate-summary.json');small={}
with (dest/'rates-hourly.csv').open('w',newline='',encoding='utf-8') as f,(dest/'rates-daily.csv').open('w',newline='',encoding='utf-8') as g:
 h=csv.writer(f);d=csv.writer(g);h.writerow(['arm','nativeHour','eligible','scheduledRecipients','share','pendingEnd','drawn','dropped']);d.writerow(['arm','day','eligible','scheduledRecipients','share','openingPending','closingPending','drawn','dropped'])
 for label,r in rates.items():
  for x in r['hourly']:h.writerow([label,x['bin'],x['eligible'],x['scheduledRecipients'],x['share'],x['pendingEnd'],x['drawn'],x['dropped']])
  for x in r.get('daily24h',[]):d.writerow([label]+[x[k] for k in ['day','eligible','scheduledRecipients','share','openingPending','closingPending','drawn','dropped']])
  small[label]={k:v for k,v in r.items() if k not in ['hourly','daily','daily24h','metrics']}
  # Detailed-trace elapsed times contain observer overhead; do not republish as production timing.
write('rate-summary.json',json.dumps(small,separators=(',',':'))+'\n')
with (dest/'stops-per-citizen.csv').open('w',newline='',encoding='utf-8') as f:
 w=csv.writer(f);w.writerow(['arm','citizen','eligible','scheduledStops'])
 for label in ['rate25','rate25-continued']:
  for ident,row in read(label+'-per-citizen.json').items():w.writerow([label,ident,row['eligible'],row['scheduledRecipients']])
metrics={}
for rate in [25,100]:
 name='metrics'+str(rate)+'-stop.json'
 if not (s/name).exists():continue
 r=read(name);values=sorted(r.pop('scheduleMilliseconds'));q=lambda p:values[min(len(values)-1,int((len(values)-1)*p))] if values else None
 r['scheduleMillisecondsSummary']=dict(samples=len(values),p50=q(.5),p95=q(.95),p99=q(.99),maximum=values[-1] if values else None,mean=statistics.mean(values) if values else None);metrics[str(rate)]=r
dump('admission-metrics.json',metrics)
for name in ['after100-window-1-stop.json','after100-window-1-validity.json','after100-window-1-autosave-before.json','after100-window-1-autosave-after.json']:
 if (s/name).exists():write(name,readtext(s/name))
scripts=['rig.py','offline.py','build_guard.py','RigGuard.cs','RigGuard-v2.cs','FinanceObserver.cs','OptionalObserver.cs','OptionalObserver-v1.cs','OptionalObserver-v2.cs','upgrade_observer.py','lean_metrics.py','lighten_observer.py','observer_events_upgrade.py','Wait-Lease.ps1','Backup-Rig.ps1','Install.ps1','Launch-Rig.ps1','Restore-Rig.ps1','Release.ps1','fixture.py','load.py','measure.py','run_windows.py','financial_run.py','native_control.py','quit.py','verify_financial.py','verify_resident_ledgers.py','verify_finance_arms.py','verify_verifiers.py','rate_arms.py','extend_default.py','light_rate.py','phase_long.py','analyze_rates.py','default_gate.py','export_rate_audit.py','verify_rate_audit.py','settings_only.py','pending_zero.py','metrics_arms.py','perf100.py','prepare_fixtures.py','codec_batch.py','schema_native.py','schema_native_helpers.py','migration14.py','native_matrix.py','analyze_migrations.py','event_persistence.py','analyze_persistence.py','optional_visual.py','analyze_optional_reload.py','analyze_campaign.py']
scripts.append('verify_events.py')
scripts.append('inspect_conversation_change.py')
scripts += ['analyze_decreases.py','finish_cleanup_evidence.py','prepare_final_report.py','write_complete_report.py','publish_bundle.py','verify_optional_food_separation.py']
for name in scripts:
 if (s/name).exists():write(name,readtext(s/name))
parts=[]
for r in read('offline-results.json'):
 p=s/(r['check']+'.log');parts.append('COMMAND '+r['check']+' EXIT '+str(r['exit'])+'\n'+readtext(p,errors='replace'))
write('offline-output.txt','\n\n'.join(parts))
shot=Path(r'<user-home>\HART-Builds\screenshots\persistent-city-pr3-ob-2026-10-29\default-optional-dwell.png')
if shot.exists():
 (dest/'screenshots').mkdir();shutil.copy2(shot,dest/'screenshots/default-optional-dwell.png')
write('README.md',(s/'report-complete.md').read_text())
write('.gitattributes','* -text\n')
files=list(dest.rglob('*'));size=sum(p.stat().st_size for p in files if p.is_file());assert size<5*1024*1024,size
assert not any(p.suffix.lower() in ['.dll','.hsg','.reg','.meta','.private'] for p in files if p.is_file())
print('Bundle bytes',size,'files',sum(p.is_file() for p in files))
