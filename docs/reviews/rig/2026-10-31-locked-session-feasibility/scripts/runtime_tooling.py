"""rig_test build and installation helper; invoked only while the agent1 lease is held."""
import os, json, shutil, subprocess, hashlib, sys, zipfile
from pathlib import Path
from prepare_review import ROOT, STAGE, PROFILE

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def record(n,v): (STAGE/(n+'.json')).write_text(json.dumps(v,indent=2))
def build():
    sdk=Path(os.environ['LOCALAPPDATA'])/'Microsoft/dotnet'
    managed=Path('C:/BigAmbitions-AgentTests/Big Ambitions Agent Tests_Data/Managed')
    refs=['mscorlib','System','System.Core','System.Numerics','netstandard','BigAmbitions','BigAmbitions.ModAPI','BigAmbitions.AI','BigAmbitions.Items','BigAmbitions.Characters','BigAmbitions.Neighborhoods','HGExtensions','HGPlugins','DayNightCycle','ExternalPlugins','UnityEngine.CoreModule','UnityEngine.IMGUIModule','UnityEngine.InputLegacyModule','UnityEngine.ScreenCaptureModule','UnityEngine.AnimationModule','UnityEngine.PhysicsModule','UnityEngine.AIModule','Newtonsoft.Json']
    base=[str(sdk/'dotnet.exe'),str(sdk/'sdk/10.0.402/Roslyn/bincore/csc.dll'),'-nologo','-noconfig','-nostdlib+','-target:library','-langversion:9','-optimize+','-deterministic+','-warnaserror+']+['-reference:'+str(managed/(n+'.dll')) for n in refs]+['-reference:'+str(ROOT/'build/PersistentCity/PersistentCity.dll'),'-reference:'+str(ROOT/'build/PersistentCity/Dependencies/0Harmony.dll')]
    source=STAGE/'frame-source'
    files=['Probe.cs','Pilot.cs','ClockAudit.cs','Neighborhood.cs','Frames.cs','EventMeasurements.cs','EventObservations.cs','FrameObservations.cs','DiagnosticResponses.cs','Menus.cs','Operations.cs','ReturnQuotes.cs']
    subprocess.run(base+['-nullable:enable','-out:'+str(STAGE/'FoodDemandAuditProbe.dll')]+[str(source/f) for f in files],check=True)
    subprocess.run(base+['-out:'+str(STAGE/'ReviewGuard.dll'),str(Path(__file__).parent/'ReviewGuard.cs')],check=True)
    record('tool-builds',dict(production=sha(ROOT/'build/PersistentCity/PersistentCity.dll'),frameHelper=sha(STAGE/'FoodDemandAuditProbe.dll'),guard=sha(STAGE/'ReviewGuard.dll'),sources={f:sha(source/f) for f in files},guardSource=sha(Path(__file__).parent/'ReviewGuard.cs')))

def install(order):
    assert (STAGE/'backup-receipt.json').exists()
    mods=PROFILE/'ModsLocal'
    if not (STAGE/'original-modslocal').exists():
        assert mods.resolve()==PROFILE.resolve()/'ModsLocal'
        shutil.move(mods,STAGE/'original-modslocal')
    elif mods.exists():
        # Preserve each prior test arrangement; no recursive deletion.
        target=STAGE/('mods-previous-'+str(len(list(STAGE.glob('mods-previous-*')))))
        shutil.move(mods,target)
    mods.mkdir()
    shutil.copytree(STAGE/'original-modslocal/RealLifeOverhaul',mods/'RealLifeOverhaul')
    source=ROOT if order=='after' else ROOT
    shutil.copytree(source/'build/PersistentCity',mods/'PersistentCity')
    for name in ['FoodDemandAuditProbe','PR3RigGuard']:(mods/name).mkdir()
    shutil.copy2(STAGE/'FoodDemandAuditProbe.dll',mods/'FoodDemandAuditProbe/FoodDemandAuditProbe.dll')
    shutil.copy2(STAGE/'ReviewGuard.dll',mods/'PR3RigGuard/ReviewGuard.dll')
    (mods/'PR3RigGuard/bridge-link.private').write_text(str(STAGE/'original-modslocal/FoodTrucks/FoodTrucks.dll'))
    receipt_path=Path('C:/BigAmbitions-AgentTests/agent-install.json')
    receipt=json.loads((STAGE/'backup/agent-install.json').read_text(encoding='utf-8-sig'))
    receipt['files']=[r for r in receipt['files'] if 'ModsLocal' not in r['path']]
    for p in mods.rglob('*'):
        if p.is_file():receipt['files'].append(dict(path=str(p),sha256=sha(p)))
    receipt_path.write_text(json.dumps(receipt,indent=2),encoding='utf-8-sig')
    record('installed-'+order,dict(order=order,files=[dict(file=str(p.relative_to(mods)),sha256=sha(p)) for p in mods.rglob('*') if p.is_file()],note='Directory ordering is a request; actual patch/load chronology must be verified.'))
    name='FT Food Routines Locked Feasibility 20261031'
    src=next((STAGE/'backup/profile/SaveGames').glob('**/FT Food Routines Ownership Perf20261027.hsg'))
    relative=src.relative_to(STAGE/'backup/profile')
    dst=(PROFILE/relative).with_name(name+'.hsg')
    shutil.copy2(Path('<private-fixtures>/performance-pc-20261027/morning-base.hsg'),dst)
    meta=json.loads(Path(str(src)+'.meta').read_text(encoding='utf-8-sig'));meta['name']=name
    Path(str(dst)+'.meta').write_text(json.dumps(meta))
    record('fixture-'+order,dict(name=name,sha256=sha(dst),source='retained FT morning-base disposable fixture',sourceSchema='historical fixture; normal production migration only'))
    print('Installed isolated arrangement:',order,flush=True)

if __name__=='__main__':
    if sys.argv[1]=='build':build()
    else:install(sys.argv[1])
