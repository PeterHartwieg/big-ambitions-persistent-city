"""Reproduce the approved donut baseline in an ignored folder; foreground only."""
import hashlib
import os
import re
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "build/e3-cupcake/before"
OUT.mkdir(parents=True, exist_ok=True)
original = (ROOT / "tools/cupcake-feasibility-regression/approved-donut-reader.cs.txt").read_text(encoding="utf-8")
assert hashlib.sha256(original.encode()).hexdigest() == "880d062dcbf9a21af4168ac44b25fd1bfc91311830058a26356c4aeca4b30cf1"
(OUT / "DonutFeasibilityReader.cs").write_text(original, encoding="utf-8")
program = (ROOT / "tools/donut-feasibility-regression/Program.cs").read_text(encoding="utf-8")
program = program.replace("#elif SALAD\nusing CandidateReader = PersistentCity.Runtime.SaladFeasibilityReader;\n", "")
program = re.sub(r"#if SALAD\n.*?#endif\n", "", program, flags=re.S)
program = re.sub(r"#if CUPCAKE\n.*?#endif\n", "", program, flags=re.S).replace("CandidateReader", "DonutFeasibilityReader")
assert hashlib.sha256(program.encode()).hexdigest() == "32da8b5d093099da5616ee97d7fda3032eb7f92bc3bef22038853e8d145d21d0"
(OUT / "Program.cs").write_text(program, encoding="utf-8")
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
