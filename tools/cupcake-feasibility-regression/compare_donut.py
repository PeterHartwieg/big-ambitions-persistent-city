"""Differential donut output proof against the approved reader; no game/rig access."""
import hashlib
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-cupcake/differential"
OUT.mkdir(parents=True, exist_ok=True)
original = (ROOT / "tools/cupcake-feasibility-regression/approved-donut-reader.cs.txt").read_text(encoding="utf-8")
assert hashlib.sha256(original.encode()).hexdigest() == "880d062dcbf9a21af4168ac44b25fd1bfc91311830058a26356c4aeca4b30cf1"
(OUT / "Baseline.cs").write_text(original.replace("DonutFeasibility", "BaselineDonutFeasibility"), encoding="utf-8")
program = (ROOT / "tools/donut-feasibility-regression/Program.cs").read_text()
program = program.replace("using CandidateReader = PersistentCity.Runtime.DonutFeasibilityReader;",
                          "using CandidateReader = ComparisonDonutReader;")
(OUT / "Program.cs").write_text(program, encoding="utf-8")
(OUT / "Comparison.cs").write_text('''using System.Text.Json;
using PersistentCity.Runtime;
static class ComparisonDonutReader
{
    public const string Product = DonutFeasibilityReader.Product;
    public static DonutFeasibilitySnapshot Read(BuildingRegistration registration)
    {
        var before = BaselineDonutFeasibilityReader.Read(registration);
        var after = DonutFeasibilityReader.Read(registration);
        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
            throw new Exception("Donut output differs from approved reader");
        return after;
    }
}
''', encoding="utf-8")
(OUT / "Differential.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../Core/PersistentCity.Core.csproj" />
    <Compile Include="../../../Runtime/DirectStockShowcaseReader.cs" />
    <Compile Include="../../../Runtime/DonutFeasibilityReader.cs" />
  </ItemGroup>
</Project>
''', encoding="utf-8")
env = os.environ.copy()
env.update(DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                "C:/Users/Peter/Coding_Projects/.agent-logs/wait-other-dotnet.ps1", "-Repo", str(ROOT)],
               cwd=ROOT, env=env, check=True)
dotnet = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
subprocess.run([str(dotnet), "run", "--project", str(OUT / "Differential.csproj"), "--configuration", "Release"],
               cwd=ROOT, env=env, check=True)
print("PASS byte-identical JSON outputs for all original donut functional and timed read cases; differential timings are not reader benchmarks")
