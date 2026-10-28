import pathlib,os,subprocess
root=pathlib.Path.cwd(); stage=pathlib.Path(str(__import__('pathlib').Path(__file__).resolve().parent))
sdk=pathlib.Path(os.environ['LOCALAPPDATA'])/'Microsoft/dotnet'; managed=pathlib.Path(os.environ.get('BA_AGENT_MANAGED',r'C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests_Data\Managed'))
refs=['mscorlib','System','System.Core','System.Numerics','netstandard','BigAmbitions','BigAmbitions.ModAPI','BigAmbitions.AI','BigAmbitions.Items','BigAmbitions.Characters','BigAmbitions.Neighborhoods','HGExtensions','HGPlugins','DayNightCycle','ExternalPlugins','UnityEngine.CoreModule','Newtonsoft.Json']
cmd=[str(sdk/'dotnet.exe'),str(sdk/'sdk/10.0.402/Roslyn/bincore/csc.dll'),'-nologo','-noconfig','-nostdlib+','-target:library','-langversion:9','-optimize+','-warnaserror+','-out:'+str(stage/'PR3RigGuard.dll')]+['-reference:'+str(managed/(n+'.dll')) for n in refs]+['-reference:'+str(root/'build/PersistentCity/PersistentCity.dll'),'-reference:'+str(root/'build/PersistentCity/Dependencies/0Harmony.dll'),str(stage/'RigGuard.cs'),str(stage/'FinanceObserver.cs'),str(stage/'OptionalObserver.cs')]
subprocess.run(cmd,check=True)
