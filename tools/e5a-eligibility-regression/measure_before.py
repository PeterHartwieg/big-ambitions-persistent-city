"""Reconstruct only E5a's three allocation improvements in ignored build storage."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
here = Path(__file__).resolve().parent
out = root / "build/e5a/before"
core_out = out / "core"
core_out.mkdir(parents=True, exist_ok=True)
current = (root / "Core/LoadedStockEligibility.cs").read_text(encoding="utf-8")
before = current
transforms = [
    ('        private static readonly IReadOnlyList<string> EmptyNames = Array.AsReadOnly(Array.Empty<string>());\n', ''),
    (' : values.Length == 0 ? EmptyNames : values.Length > LoadedStockLimits.Rows', ' : values.Length > LoadedStockLimits.Rows'),
    ('identities = uniqueOrdinalDictionaryKeys ? null : new HashSet<string>(StringComparer.Ordinal);',
     'identities = new HashSet<string>(StringComparer.Ordinal);'),
    ('            holders.Capacity = furnitureCount;\n', ''),
]
for old, new in transforms:
    assert before.count(old) == 1, old
    before = before.replace(old, new)
(core_out / "BeforeStockEligibility.cs").write_text(before, encoding="utf-8")
core = ET.parse(root / "Core/PersistentCity.Core.csproj")
properties = core.getroot().find("PropertyGroup")
ET.SubElement(properties, "EnableDefaultCompileItems").text = "false"
sources = ET.SubElement(core.getroot(), "ItemGroup")
for path in sorted((root / "Core").glob("*.cs")):
    ET.SubElement(sources, "Compile", Include=str(core_out / "BeforeStockEligibility.cs" if path.name == "LoadedStockEligibility.cs" else path))
core_project = core_out / "BeforeCore.csproj"
core.write(core_project, encoding="utf-8", xml_declaration=True)

project = ET.parse(here / "E5aEligibilityRegression.csproj")
properties = project.getroot().find("PropertyGroup")
ET.SubElement(properties, "EnableDefaultCompileItems").text = "false"
for node in project.getroot().iter("ProjectReference"):
    node.set("Include", str(core_project))
for node in project.getroot().iter("Compile"):
    include = node.get("Include")
    if not include.startswith("obj/"):
        node.set("Include", str((here / include).resolve()))
ET.SubElement(project.getroot().find("ItemGroup"), "Compile", Include=str(here / "Program.cs"))
for node in project.getroot().iter("Exec"):
    node.set("Command", f'python "{here / "generate_doubles.py"}" "$(MSBuildProjectDirectory)/obj/NativeDoubles.cs"')
project_file = out / "E5aEligibilityRegression.csproj"
project.write(project_file, encoding="utf-8", xml_declaration=True)

guard = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         r"C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1", "-Repo", str(root)]
dotnet = str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
subprocess.run(guard, cwd=root, env=env, check=True)
command = [dotnet, "run", "--project", str(project_file), "--configuration", "Release", "--disable-build-servers"]
log = out / "run.log"
with log.open("w", encoding="utf-8") as stream:
    result = subprocess.run(command, cwd=root, env=env, stdout=stream, stderr=subprocess.STDOUT)
if result.returncode:
    print(log.read_text(encoding="utf-8"), flush=True)
    raise SystemExit(result.returncode)
data = json.loads(log.read_text(encoding="utf-8").splitlines()[-1])
receipt = dict(command=command, exit=result.returncode, transforms=transforms,
               sourceSha256=hashlib.sha256(current.encode()).hexdigest(),
               beforeSha256=hashlib.sha256(before.encode()).hexdigest(),
               logSha256=hashlib.sha256(log.read_bytes()).hexdigest(), result=data)
(out / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"checks": data["checks"], "onePass": data["measurements"]["onePass"], "receipt": "build/e5a/before/receipt.json"}))
