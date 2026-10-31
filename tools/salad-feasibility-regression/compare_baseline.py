"""Fresh-process timings or output differential against approved cupcake head; foreground only.

Usage: python tools/salad-feasibility-regression/compare_baseline.py before|differential
Generated source stays ignored. No native assembly, game, profile or rig access.
"""
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
HEAD = "682aa77"
mode = sys.argv[1]
assert mode in ("before", "differential")
env = os.environ.copy()
env.update(DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
dotnet = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"

def source(path):
    return subprocess.check_output(["git", "show", f"{HEAD}:{path}"], cwd=ROOT, text=True)

for candidate in ("Donut", "Cupcake"):
    out = ROOT / f"build/e3-salad/{mode}/{candidate.lower()}"
    out.mkdir(parents=True, exist_ok=True)
    program = source("tools/donut-feasibility-regression/Program.cs")
    files = ["DirectStockShowcaseReader", "DonutFeasibilityReader", "CupcakeFeasibilityReader"]
    compile_current = ""
    for name in files:
        body = source(f"Runtime/{name}.cs")
        if mode == "differential":
            for symbol in ("DirectStockFeasibilitySnapshot", "DirectStockShowcaseReader", "DirectStockCandidate",
                           "DirectStockFields", "DonutFeasibility", "CupcakeFeasibility"):
                body = body.replace(symbol, "Baseline" + symbol)
            compile_current += f'<Compile Include="../../../../Runtime/{name}.cs" />'
        (out / f"{name}.cs").write_text(body, encoding="utf-8")
    if mode == "differential":
        program = program.replace(f"using CandidateReader = PersistentCity.Runtime.{candidate}FeasibilityReader;",
                                  "using CandidateReader = ComparisonReader;")
        (out / "Comparison.cs").write_text(f'''using System.Text.Json;
using PersistentCity.Runtime;
static class ComparisonReader
{{
    public const string Product = {candidate}FeasibilityReader.Product;
    public static {candidate}FeasibilitySnapshot Read(BuildingRegistration registration)
    {{
        var before = Baseline{candidate}FeasibilityReader.Read(registration);
        var after = {candidate}FeasibilityReader.Read(registration);
        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
            throw new Exception("{candidate} output differs from approved 682aa77");
        return after;
    }}
}}
''', encoding="utf-8")
    (out / "Program.cs").write_text(program, encoding="utf-8")
    (out / "Check.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <DefineConstants>{'CUPCAKE' if candidate == 'Cupcake' else ''}</DefineConstants>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="../../../../Core/PersistentCity.Core.csproj" />{compile_current}</ItemGroup>
</Project>
''', encoding="utf-8")
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    "C:/Users/Peter/Coding_Projects/.agent-logs/wait-other-dotnet.ps1", "-Repo", str(ROOT)],
                   cwd=ROOT, env=env, check=True)
    print(f"{mode} {candidate} (differential instrumentation is NOT a reader benchmark)", flush=True)
    subprocess.run([str(dotnet), "run", "--project", str(out / "Check.csproj"), "--configuration", "Release"],
                   cwd=ROOT, env=env, check=True)
