"""Reconstruct the exact three-descriptor donut baseline; foreground, no deadlines.

Pins normalize CRLF and require no feature-branch ancestry. Generated sources/logs
stay under ignored build/e3-frenchfries; no game or rig access.
"""
from pathlib import Path
import hashlib
import json
import os
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-frenchfries/pinned-before-donut"
OUT.mkdir(parents=True, exist_ok=True)
FRIES = '        internal static readonly DirectStockCandidate FrenchFries = new DirectStockCandidate("ba:itemname_frenchfries");\n'
PINS = {
    "Runtime/DirectStockShowcaseReader.cs": "999228241ef6a44a17fb0d26dad4961ab4c9cc25f81354f34dee6dd92e5837de",
    "Runtime/DonutFeasibilityReader.cs": "df432206c1993f4ad50b0950b50ce5bde1a6e8e6c1b184461813774ee11fb2d1",
    "tools/donut-feasibility-regression/Program.cs": "707be922277d715ca17bbf1d38c05c5f608c53ce1c87ef8db545752b67c3f507",
}
for path, expected in PINS.items():
    source = (ROOT / path).read_text(encoding="utf-8")
    if path == "Runtime/DirectStockShowcaseReader.cs":
        assert source.count(FRIES) == 1
        source = source.replace(FRIES, "")
    assert hashlib.sha256(source.encode()).hexdigest() == expected, path
    (OUT / Path(path).name).write_text(source, encoding="utf-8")
project = f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="{escape(str(ROOT / 'Core/PersistentCity.Core.csproj'))}" /></ItemGroup>
</Project>
'''
(OUT / "Before.csproj").write_text(project, encoding="utf-8")
env = os.environ.copy()
env.update(DOTNET_CLI_USE_MSBUILD_SERVER="0", DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="1",
           MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(ROOT)],
               cwd=ROOT, env=env, check=True)
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
result = subprocess.run([dotnet, "run", "--project", str(OUT / "Before.csproj"), "--configuration", "Release",
                         "-p:UseSharedCompilation=false", "--disable-build-servers"],
                        cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
(OUT / "run.log").write_text(result.stdout, encoding="utf-8")
print(result.stdout, end="", flush=True)
if result.returncode:
    raise SystemExit(result.returncode)
data = json.loads(result.stdout.splitlines()[-1])
(OUT / "result.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
