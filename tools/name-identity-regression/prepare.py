"""Export the actual writer-24 base and prepare a matched offline runner; no game input."""
from pathlib import Path
import io
import subprocess
import tarfile
from xml.sax.saxutils import escape

root = Path(__file__).resolve().parents[2]
revision = "034bb7629391ef0fedb61d7122b0d6ee3c451ff9"
out = root / "build/e5d/baseline-runner"
source = out / "source"
source.mkdir(parents=True, exist_ok=True)
archive = subprocess.check_output(["git", "archive", revision, "Core", "Persistence",
                                   "Runtime/PrototypeSettings.cs", "Runtime/InteriorViewState.cs"], cwd=root)
with tarfile.open(fileobj=io.BytesIO(archive)) as bundle:
    bundle.extractall(source, filter="data")
assert "CurrentSchema = 24;" in (source / "Core/Model.cs").read_text(encoding="utf-8")
files = [root / "tools/name-identity-regression/Program.cs"]
files += sorted((root / "tools/save-contract-regression").glob("*.cs"))
files += sorted((source / "Core").glob("*.cs")) + sorted((source / "Persistence").glob("*.cs"))
files += [source / "Runtime/PrototypeSettings.cs", source / "Runtime/InteriorViewState.cs"]
project = """<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<DefineConstants>NAME_SCHEMA24</DefineConstants><StartupObject>NameIdentityProgram</StartupObject></PropertyGroup>
<ItemGroup>"""
project += "\n".join(f'<Compile Include="{escape(p.as_posix())}" />' for p in files)
project += '\n<PackageReference Include="Newtonsoft.Json" Version="13.0.2" /></ItemGroup></Project>\n'
(out / "NameIdentityBaseline.csproj").write_text(project, encoding="utf-8")
print(f"Prepared exact schema-24 base {revision} under build/e5d/baseline-runner.")
