import sys,time,json,pathlib,re
sys.path.insert(0,r'<USER>\HART-Builds\persistent-city-pr1-rig-rerun');import rig
arm=sys.argv[1]
def saves():return {str(p.relative_to(rig.PROFILE)):[p.stat().st_size,p.stat().st_mtime_ns] for p in (rig.PROFILE/'SaveGames').rglob('*.hsg')}
rig.call(arm+'-normal','time',{'speed':1,'pause':False,'keep_needs':True})
start=time.monotonic()
while time.monotonic()-start<10:time.sleep(.5)
for i in range(1,2):
 label=arm+'-window-'+str(i)
 a=rig.probe(label+'-autosave-before','autosave-check',True);assert a['preventAutoSave'] and not a['savingGameInProgress']
 h=rig.record(label+'-health-before',rig.health());assert h['paused']==False
 logpath=pathlib.Path(r'C:\BigAmbitions-AgentTests\Player-agent.log');logoffset=logpath.stat().st_size
 files=saves();rig.record(label+'-save-files-before',files)
 rig.probe(label+'-start','frames-start');print(label+' started',flush=True)
 start=time.monotonic()
 while time.monotonic()-start<60:time.sleep(.5)
 r=rig.probe(label+'-stop','frames-stop')
 b=rig.probe(label+'-autosave-after','autosave-check',True)
 unchanged=files==saves()
 with logpath.open('rb') as f:f.seek(logoffset);segment=f.read().decode('utf-8','replace')
 (rig.STAGE/(label+'-native-window.log')).write_text(segment)
 actual=[line for line in segment.splitlines() if re.search(r"Test bridge: (saving as|saved ')|SaveGame.*(?:started|in progress)|AutoSave.*(?:triggered|started)",line,re.I)]
 clean=b['preventAutoSave'] and not b['savingGameInProgress'] and not actual and unchanged
 rig.record(label+'-validity',{'autosaveOffBeforeAfter':a['preventAutoSave'] and b['preventAutoSave'],'saveFilesUnchanged':unchanged,'broadSavingMentionDelta':b['saveLogMessages']-a['saveLogMessages'],'actualSaveLines':actual,'clean':clean})
 print(label,r['realSeconds'],r['combinedModWorkPerUnityFrameMilliseconds'],'clean',clean,flush=True)
 if not clean:raise RuntimeError('Save activity invalidates window')
rig.call(arm+'-end-pause','time',{'pause':True});rig.probe(arm+'-timing-end','capture',True)
rig.probe(arm+'-accounting-start','status')
print(arm+' timed windows complete',flush=True)
