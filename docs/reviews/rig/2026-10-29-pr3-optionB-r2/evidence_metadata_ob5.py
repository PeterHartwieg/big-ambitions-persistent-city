from pathlib import Path
import json,hashlib
s=Path(__file__).parent
def read(n):
 b=(s/n).read_bytes();return json.loads(b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig'))
def write(n,v):(s/n).write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8',newline='\n')
reuse=read('baseline-reuse.json');reuse['scope']='Primary before native measurements, native finance and schema-14 migration input reused at identical code/DLL SHA. Before offline gate and additional three-window performance confirmation are fresh. Every AFTER measurement is fresh.';write('baseline-reuse.json',reuse)
builds=read('builds.json')
for b in builds:
 b['helper_sha256']=hashlib.sha256((s/b['arm']/'PR3RigGuard.dll').read_bytes()).hexdigest()
 b['timingHelperSha256']='dc939f272b1b451a7eb15dd7c6665e3a74d4a389e3ebfe6bf9d35d15d18696aa'
 b['performanceConfirmation']='Three fresh-process windows; fresh-'+b['arm']+'-installed.json'
 if b['arm']=='before':b['nativeMeasurements']='Primary baseline, finance and schema14 input reused from 7937557; three additional performance confirmation windows rerun fresh at unchanged 7357f78.'
write('builds.json',builds)
autosave=[]
for arm in ['before','after']:
 for i in range(1,4):
  prefix='fresh-'+arm+'-window-'+str(i);qa='fresh-'+arm+'-qa-'+str(i)
  row=dict(build=arm,window=i,before=read(prefix+'-autosave-before.json'),after=read(prefix+'-autosave-after.json'),afterReload=read(qa+'-reloaded-autosave-off.json'),validity=read(prefix+'-validity.json'))
  assert row['validity']['clean'] and all(row[k]['preventAutoSave'] and not row[k]['savingGameInProgress'] for k in ['before','after','afterReload'])
  autosave.append(row)
write('autosave-confirmation-evidence.json',autosave)
persistence=read('persistence-summary.json')
write('persistence-chronology.json',{k:dict(minuteBefore=v['minuteBefore'],minuteAfter=v['minuteAfter'],declinedChoiceClearedOnNewJourney=v['declinedChoiceClearedOnNewJourney'],savedPayloadByteIdentical=v['savedPayloadByteIdentical'],existingVisitRerolls=v['rerolledExistingVisitChoices']) for k,v in persistence.items() if v['declinedChoiceClearedOnNewJourney'] or v['newVisitChoiceChanges']})
write('tooling-notes.json',dict(nativeControlVersions='native_control-v1.py used for primary windows and finance; native_control.py later adds read-only optional-status snapshots for reload chronology. No observer or production logic changed.',installVersions='Install-v1.ps1 made the original AFTER install. Install.ps1 adds an optional receipt label, preserving the original receipts during the fresh-process comparison.',handOnRetry='First normal-speed high-stock50 window had no hand-on and remains INCOMPLETE in event-persistence-initial-attempts.json. Default25 retry observed a real hand-on and performed native save/reload; both trace summaries remain.',analyzerCorrection='A declined false choice clears to null when c117 starts its scheduled journey during the 0.333-game-minute reload advance. Exact scheduled/departure minute, AtPlace-to-Travelling transition, new journey, zero post-load draw/transfer/stop counters and unchanged stored bytes prove a new journey rather than a reroll.',stopKinds={'2':'Break','4':'Conversation (Core ChatOutsideVenue)','5':'WindowShopping','6':'Audience','7':'Resting'},shutdown='One shutdown invocation omitted its log label; installer refused while game running, before any file copy. Corrected graceful shutdown exited 0; no package was changed while running.'))
print('Metadata, source versions, autosave and chronology evidence completed.')
