import os,subprocess,pathlib,json,time,hashlib,re
stage=pathlib.Path(__file__).parent
after=pathlib.Path.cwd();before=after.parent/'rig-pr3-before'
dotnet=str(pathlib.Path(os.environ['LOCALAPPDATA'])/'Microsoft/dotnet/dotnet.exe')
os.environ['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+os.environ['PATH']
managed=os.environ['BA_GAME_MANAGED']
results=[]
def run(label,cmd,cwd):
 started=time.time()
 with (stage/(label+'.log')).open('w',encoding='utf-8') as f:p=subprocess.run(cmd,cwd=cwd,stdout=f,stderr=subprocess.STDOUT)
 results.append(dict(check=label,exit=p.returncode,seconds=round(time.time()-started,2),command=cmd))
 (stage/'offline-results.json').write_text(json.dumps(results,indent=2));print(label,p.returncode,flush=True)
for arm,root in [('before',before),('after',after)]:
 run(arm+'-build',['python','tools/build.py','--game-managed',managed],root)
 run(arm+'-tests',[dotnet,'test','tests/PersistentCity.Tests.csproj','--logger','trx;LogFileName='+str(stage/(arm+'-tests.trx'))],root)
 run(arm+'-optional-test-list',[dotnet,'test','tests/PersistentCity.Tests.csproj','--no-build','--list-tests','--filter','FullyQualifiedName~OptionalStop|FullyQualifiedName~BaseMerge_'],root)
 for project in sorted((root/'tools').glob('*regression/*.csproj')):
  run(arm+'-'+project.parent.name,[dotnet,'run','--project',str(project),'--configuration','Release'],root)
 for folder in ['native-command-regression','physical-selection-regression','vehicle-ownership-regression','optional-stop-regression']:
  script=root/'tools'/folder/'run.py'
  if script.exists():run(arm+'-'+folder+'-py',['python',str(script)],root)
 run(arm+'-simulate',[dotnet,'run','--project','tools/simulate/Simulate.csproj','--','28',str(stage/(arm+'-demo'))],root)
 run(arm+'-food-routines',[dotnet,'run','--project','tools/food-routines/FoodRoutines.csproj','--configuration','Release','--',str(stage/(arm+'-food-routines-demo')),'957'],root)
 run(arm+'-food-demand',[dotnet,'run','--project','tools/food-demand/FoodDemand.csproj','--',str(stage/(arm+'-food-demand-demo'))],root)
 dll=root/'build/PersistentCity/PersistentCity.dll'
 (stage/(arm+'-build.json')).write_text(json.dumps(dict(sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),dll_sha256=hashlib.sha256(dll.read_bytes()).hexdigest()),indent=2))
