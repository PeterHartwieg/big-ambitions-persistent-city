"""Compare the identical shipped-product economy fixture at 51ce709 and the candidate."""
from pathlib import Path
import json
import os
import subprocess

root=Path(__file__).resolve().parents[2]
out=root/"build/e4-menu/default-comparison"
out.mkdir(parents=True,exist_ok=True)
dotnet=str(Path(os.environ["LOCALAPPDATA"])/"Microsoft/dotnet/dotnet.exe")
guard=["powershell","-NoProfile","-ExecutionPolicy","Bypass","-File",r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1","-Repo",str(root)]
env=dict(os.environ,MSBUILDDISABLENODEREUSE="1",DOTNET_CLI_USE_MSBUILD_SERVER="0",UseSharedCompilation="false")
outputs=[]
for name,source in [("before",root/"build/e4-baseline"),("after",root)]:
    folder=out/name;folder.mkdir(exist_ok=True)
    project=folder/"Compare.csproj"
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup><ItemGroup>
<Compile Include="{source.as_posix()}/Core/*.cs"/><Compile Include="{source.as_posix()}/Persistence/*.cs"/>
<Compile Include="{root.as_posix()}/tools/save-contract-regression/EconomyScenario.cs"/>
<PackageReference Include="Newtonsoft.Json" Version="13.0.2"/></ItemGroup></Project>''')
    (folder/"Program.cs").write_text('using System; using Newtonsoft.Json; Console.WriteLine(JsonConvert.SerializeObject(new { legacy=EconomyScenario.Run(legacyChoice:true), weighted=EconomyScenario.Run(legacyChoice:false) }));')
    subprocess.run(guard,cwd=root,env=env,check=True)
    result=subprocess.run([dotnet,"run","--project",str(project),"--configuration","Release"],cwd=root,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    (out/f"{name}.log").write_text(result.stdout)
    if result.returncode: raise SystemExit(result.stdout)
    outputs.append(json.loads(result.stdout.strip().splitlines()[-1]))
assert outputs[0]==outputs[1],"Default live-product economy changed; inspect default-comparison logs"
(out/"results.json").write_text(json.dumps(dict(base="51ce709",identical=True,before=outputs[0],after=outputs[1]),indent=2))
print(json.dumps(dict(base="51ce709",identical=True,legacy=outputs[1]["legacy"],weighted=outputs[1]["weighted"])))
