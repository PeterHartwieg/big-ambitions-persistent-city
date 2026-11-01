"""Fresh-process frozen three-candidate baseline; foreground, no execution deadline."""
import hashlib
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
shared = (ROOT / "Runtime/DirectStockShowcaseReader.cs").read_text(encoding="utf-8")
shared = shared.replace('        internal static readonly DirectStockCandidate Icecream = new DirectStockCandidate("ba:itemname_icecream");\n', '')
shared = shared.replace('        internal static readonly DirectStockCandidate Pizza = new DirectStockCandidate("ba:itemname_pizza");\n', '')
assert hashlib.sha256(shared.encode()).hexdigest() == "999228241ef6a44a17fb0d26dad4961ab4c9cc25f81354f34dee6dd92e5837de"
env = os.environ.copy()
env.update(DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
dotnet = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
for candidate, symbol in (("Donut", ""), ("Cupcake", "CUPCAKE"), ("Salad", "SALAD")):
    out = ROOT / "build/e3-icecream/before" / candidate.lower()
    out.mkdir(parents=True, exist_ok=True)
    (out / "DirectStockShowcaseReader.cs").write_text(shared, encoding="utf-8")
    for name in ("DonutFeasibilityReader", "CupcakeFeasibilityReader", "SaladFeasibilityReader"):
        (out / (name + ".cs")).write_text((ROOT / "Runtime" / (name + ".cs")).read_text(encoding="utf-8"), encoding="utf-8")
    (out / "Program.cs").write_text((ROOT / "tools/donut-feasibility-regression/Program.cs").read_text(encoding="utf-8"), encoding="utf-8")
    (out / "Before.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<DefineConstants>{symbol}</DefineConstants></PropertyGroup><ItemGroup>
<ProjectReference Include="{(ROOT / 'Core/PersistentCity.Core.csproj').as_posix()}" /></ItemGroup></Project>''', encoding="utf-8")
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    "C:/Users/Peter/Coding_Projects/.agent-logs/wait-other-dotnet.ps1", "-Repo", str(ROOT)], cwd=ROOT, env=env, check=True)
    print("BEFORE " + candidate, flush=True)
    subprocess.run([str(dotnet), "run", "--project", str(out / "Before.csproj"), "--configuration", "Release"], cwd=ROOT, env=env, check=True)
