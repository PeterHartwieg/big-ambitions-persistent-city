"""Fresh-process timings or output differential against approved cupcake head; foreground only.

Usage: python tools/salad-feasibility-regression/compare_baseline.py before|differential
Generated source stays ignored. No native assembly, game, profile or rig access.
"""
import hashlib
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
mode = sys.argv[1]
assert mode in ("before", "differential")
env = os.environ.copy()
env.update(DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
dotnet = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"

APPROVED_SHA256 = {
    "Runtime/DirectStockShowcaseReader.cs": "614616650dc0ce4db364790c6e2ca91ead009922fb31818b6ff41e6afe705540",
    "Runtime/DonutFeasibilityReader.cs": "df432206c1993f4ad50b0950b50ce5bde1a6e8e6c1b184461813774ee11fb2d1",
    "Runtime/CupcakeFeasibilityReader.cs": "971ea51552b7d4d7bd928d699476ef6b75e33935a001c4a7edd81401a0c71429",
    "tools/donut-feasibility-regression/Program.cs": "6cb8277b432e45f750882d28693837a5a2fe52811a913ceaf520059b96779962",
}

def source(path):
    body = (ROOT / path).read_text(encoding="utf-8")
    if path == "Runtime/DirectStockShowcaseReader.cs":
        body = body.replace('        internal static readonly DirectStockCandidate Salad = new DirectStockCandidate("ba:itemname_salad");\n', '')
        body = body.replace('See the E3 per-product records;', 'See e3-donut.md and e3-cupcake.md;')
    elif path == "tools/donut-feasibility-regression/Program.cs":
        body = body.replace('#elif SALAD\nusing CandidateReader = PersistentCity.Runtime.SaladFeasibilityReader;\n', '')
        body = re.sub(r"#if SALAD\n.*?#endif\n", "", body, flags=re.S)
    assert hashlib.sha256(body.encode()).hexdigest() == APPROVED_SHA256[path], path
    return body

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
                           "DirectStockFields", "DonutFeasibilityReader", "DonutFeasibilitySnapshot",
                           "CupcakeFeasibilityReader", "CupcakeFeasibilitySnapshot"):
                body = re.sub(r"\b" + symbol + r"\b", "Baseline" + symbol, body)
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
            throw new Exception("{candidate} output differs from pinned landed cupcake contract (identical to approved 682aa77)");
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
