"""Reproduce the approved donut baseline in an ignored folder; foreground only."""
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
HEAD = "462a99d"
OUT = ROOT / "build/e3-cupcake/before"
OUT.mkdir(parents=True, exist_ok=True)
for source in ("Runtime/DonutFeasibilityReader.cs", "tools/donut-feasibility-regression/Program.cs"):
    (OUT / Path(source).name).write_bytes(subprocess.check_output(
        ["git", "show", f"{HEAD}:{source}"], cwd=ROOT))
(OUT / "Before.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="../../../Core/PersistentCity.Core.csproj" /></ItemGroup>
</Project>
''', encoding="utf-8")
env = os.environ.copy()
env.update(DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                "C:/Users/Peter/Coding_Projects/.agent-logs/wait-other-dotnet.ps1", "-Repo", str(ROOT)],
               cwd=ROOT, env=env, check=True)
dotnet = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
subprocess.run([str(dotnet), "run", "--project", str(OUT / "Before.csproj"), "--configuration", "Release"],
               cwd=ROOT, env=env, check=True)
