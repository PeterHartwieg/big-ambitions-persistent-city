#!/usr/bin/env python3
"""Compile the Persistent City prototype against an explicitly supplied local game Managed directory."""
import argparse
import hashlib
from pathlib import Path
import re
import shutil
import subprocess
import urllib.request
import zipfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--game-managed", required=True, type=Path)
parser.add_argument("--json-assembly", type=Path, help="Explicit Newtonsoft.Json reference when the local Managed copy omits it")
args = parser.parse_args()
managed = args.game_managed.resolve()
references = ["mscorlib", "System", "System.Core", "System.Numerics", "System.Runtime.Serialization", "netstandard", "BigAmbitions",
              "BigAmbitions.ModAPI", "BigAmbitions.AI", "BigAmbitions.Items", "BigAmbitions.Characters", "BigAmbitions.Neighborhoods", "BigAmbitions.InteriorDesigner", "HGExtensions", "HGPlugins",
              "UnityEngine.CoreModule", "UnityEngine.IMGUIModule", "UnityEngine.TextRenderingModule", "UnityEngine.AIModule", "UnityEngine.PhysicsModule", "UnityEngine.AnimationModule", "ExternalPlugins", "DayNightCycle"]
for name in references:
    if not (managed / (name + ".dll")).is_file():
        raise SystemExit(f"Required game assembly is missing: {name}.dll")
json_reference = (args.json_assembly or managed / "Newtonsoft.Json.dll").resolve()
if not json_reference.is_file():
    raise SystemExit("Newtonsoft.Json.dll is missing; supply the game's assembly with --json-assembly")
sdk_lines = subprocess.check_output(["dotnet", "--list-sdks"], text=True).splitlines()
match = re.fullmatch(r"(\S+) \[(.*)\]", sdk_lines[-1])
if not match:
    raise SystemExit("Cannot locate the .NET SDK compiler")
csc = Path(match[2]) / match[1] / "Roslyn/bincore/csc.dll"
output = root / "build/PersistentCity"
output.mkdir(parents=True, exist_ok=True)
dependency = output / "Dependencies"
dependency.mkdir(exist_ok=True)
harmony = dependency / "0Harmony.dll"
harmony_hash = "7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c"
if not harmony.exists() or not (dependency / "Harmony-LICENSE.txt").exists():
    archive = root / "build/lib.harmony.2.4.2.nupkg"
    if not archive.exists():
        urllib.request.urlretrieve("https://api.nuget.org/v3-flatcontainer/lib.harmony/2.4.2/lib.harmony.2.4.2.nupkg", archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != "d64592e53090464559fce48612c9ca7c8dc73113841376b7aa3455f46fc5d579":
        raise SystemExit("Harmony archive does not match its pinned checksum")
    with zipfile.ZipFile(archive) as bundle:
        harmony.write_bytes(bundle.read("lib/net472/0Harmony.dll"))
        (dependency / "Harmony-LICENSE.txt").write_bytes(bundle.read("LICENSE"))
if hashlib.sha256(harmony.read_bytes()).hexdigest() != harmony_hash:
    raise SystemExit("Harmony DLL does not match its pinned checksum")
sources = sorted(str(p) for folder in ("Core", "Compatibility", "Persistence", "Runtime") for p in (root / folder).glob("*.cs"))
command = ["dotnet", str(csc), "-nologo", "-noconfig", "-nostdlib+", "-target:library", "-langversion:9",
           "-nullable:enable", "-optimize+", "-warn:4", "-warnaserror+", "-deterministic+", "-out:" + str(output / "PersistentCity.dll")]
command += ["-reference:" + str(managed / (name + ".dll")) for name in references]
command += ["-reference:" + str(json_reference)]
command += ["-reference:" + str(harmony)]
subprocess.run(command + sources, check=True)
shutil.copy2(root / "config/persistent-city.json", output / "persistent-city.json")
if (root / "README.md").exists():
    shutil.copy2(root / "README.md", output / "README.md")
print("Package:", output)
print("DLL SHA256:", hashlib.sha256((output / "PersistentCity.dll").read_bytes()).hexdigest())
print("Game assemblies and RLO are referenced externally; neither is copied into this package.")
