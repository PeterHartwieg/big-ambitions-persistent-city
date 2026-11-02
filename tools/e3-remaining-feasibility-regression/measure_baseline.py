"""Build the frozen incoming six-candidate reader once; fresh-process measurements."""
from pathlib import Path
import hashlib
import os
import subprocess
import sys
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-remaining-batch/baseline"
OUT.mkdir(parents=True, exist_ok=True)
source = subprocess.check_output(["git", "show", "a1b993a:Runtime/DirectStockShowcaseReader.cs"], cwd=ROOT).decode().replace("\r\n", "\n")
assert hashlib.sha256(source.encode()).hexdigest() == "239650040cb06dde4f0d8030e659c906a5fe09c8b37ea7be9dc763162ddb880d"
(OUT / "DirectStockShowcaseReader.cs").write_text(source, encoding="utf-8")
subprocess.run([sys.executable, str(Path(__file__).with_name("generate_doubles.py")), str(OUT / "ApprovedNativeDoubles.cs")], check=True)
includes = [ROOT / "tools/e3-remaining-feasibility-regression/Program.cs"]
includes += [ROOT / f"Runtime/{name}FeasibilityReader.cs" for name in ("Donut", "Cupcake", "Salad", "Icecream", "FrenchFries", "Pizza")]
project = """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<DefineConstants>BASELINE</DefineConstants><StartupObject>RemainingRegression</StartupObject>
</PropertyGroup><ItemGroup>"""
project += f'<ProjectReference Include="{escape(str(ROOT / "Core/PersistentCity.Core.csproj"))}" />'
project += "".join(f'<Compile Include="{escape(str(path))}" />' for path in includes)
project += "</ItemGroup></Project>\n"
(OUT / "Baseline.csproj").write_text(project, encoding="utf-8")
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER="0", DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="1",
           MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(ROOT)], cwd=ROOT, env=env, check=True)
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
result = subprocess.run([dotnet, "run", "--project", str(OUT / "Baseline.csproj"), "--configuration", "Release", "--", "measure"],
                        cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
(OUT / "run.log").write_text(result.stdout, encoding="utf-8")
print(result.stdout, end="", flush=True)
raise SystemExit(result.returncode)
