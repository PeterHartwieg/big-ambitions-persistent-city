import pathlib,os,subprocess,sys
root=pathlib.Path(sys.argv[1]); stage=pathlib.Path(__file__).parent
sdk=pathlib.Path(os.environ['LOCALAPPDATA'])/'Microsoft/dotnet'; managed=pathlib.Path(r'C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests_Data\Managed')
refs=['mscorlib','System','System.Core','System.Numerics','netstandard','BigAmbitions','BigAmbitions.ModAPI','BigAmbitions.AI','BigAmbitions.Items','BigAmbitions.Characters','BigAmbitions.Neighborhoods','HGExtensions','HGPlugins','DayNightCycle','ExternalPlugins','UnityEngine.CoreModule','Newtonsoft.Json']
cmd=[str(sdk/'dotnet.exe'),str(sdk/'sdk/10.0.402/Roslyn/bincore/csc.dll'),'-nologo','-noconfig','-nostdlib+','-target:library','-langversion:9','-optimize+','-warnaserror+','-out:'+str(stage/'PR2RigGuard.dll')]+['-reference:'+str(managed/(n+'.dll')) for n in refs]+['-reference:'+str(root/'build/PersistentCity/PersistentCity.dll'),'-reference:'+str(root/'build/PersistentCity/Dependencies/0Harmony.dll'),str(stage/'RigGuard.cs'),str(stage/'FinanceObserver.cs')]
subprocess.run(cmd,check=True)
