"""Offline allocation comparator; generated sources/projects/logs stay in ignored build/.

Both variants compile the actual adapter and the same behavioral doubles. Only the
shared-material list lifetime differs. Never loads the game or its assets.
"""
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
original = root / "tools/street-proxy-regression/StreetProxyRegression.csproj"
output = root / "build/f1b/buffer-comparison"
output.mkdir(parents=True, exist_ok=True)
native = root / "Runtime/NativeStreetProxyDiagnostics.cs"
source = native.read_text(encoding="utf-8")
assert source.count("var materials = new List<Material>();") == 1
assert source.count("renderer.GetSharedMaterials(materials);") == 1
before = source.replace("var materials = new List<Material>();", "List<Material> materials = null!;")
before = before.replace("renderer.GetSharedMaterials(materials);", "materials = new List<Material>(); renderer.GetSharedMaterials(materials);")
baseline = output / "BeforeNativeStreetProxyDiagnostics.cs"
baseline.write_text(before, encoding="utf-8")
project = ET.parse(original)
ET.SubElement(project.getroot().find("PropertyGroup"), "EnableDefaultCompileItems").text = "false"
for item in project.getroot().iter():
    if item.tag in ("Compile", "ProjectReference") and "Include" in item.attrib:
        resolved = (original.parent / item.attrib["Include"]).resolve()
        item.attrib["Include"] = str(baseline if resolved == native else resolved)
group = ET.SubElement(project.getroot(), "ItemGroup")
for name in ("Program.cs", "Doubles.cs"):
    ET.SubElement(group, "Compile", Include=str(original.parent / name))
generated = output / "Before.csproj"
project.write(generated, encoding="unicode")
dotnet = shutil.which("dotnet") or str(Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe")
wait = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
        "C:/Users/Peter/Coding_Projects/.agent-logs/wait-other-dotnet.ps1", "-Repo", str(root)]
env = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false")
for label, path in (("before", generated), ("after", original)):
    subprocess.run(wait, cwd=root, check=True)
    with (output / (label + ".txt")).open("w", encoding="utf-8") as log:
        subprocess.run([dotnet, "run", "--project", str(path), "--configuration", "Release"],
                       cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    print(label + ":\n" + (output / (label + ".txt")).read_text(encoding="utf-8"), flush=True)
