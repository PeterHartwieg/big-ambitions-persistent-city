#!/usr/bin/env python3
"""Compile the Persistent City prototype against an explicitly supplied local game Managed directory."""
import argparse
import hashlib
import json
import os
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
parser.add_argument("--provenance", type=Path, help="Write the game-assembly fingerprint (F9b) to this JSON file; never stops the build")
parser.add_argument("--fingerprints", type=Path, default=root / "tools/acceptance/supported-game.json", help=argparse.SUPPRESS)
args = parser.parse_args()
managed = args.game_managed.resolve()
references = ["mscorlib", "System", "System.Core", "System.Numerics", "System.Runtime.Serialization", "netstandard", "BigAmbitions",
              "BigAmbitions.ModAPI", "BigAmbitions.AI", "BigAmbitions.Items", "BigAmbitions.Seasons", "BigAmbitions.Characters", "BigAmbitions.Neighborhoods", "BigAmbitions.InteriorDesigner", "HGExtensions", "HGPlugins",
              "UnityEngine.CoreModule", "UnityEngine.IMGUIModule", "UnityEngine.UI", "UnityEngine.InputLegacyModule", "BigAmbitions.PlacementSystem", "UnityEngine.TextRenderingModule", "UnityEngine.AIModule", "UnityEngine.PhysicsModule", "UnityEngine.AnimationModule", "ExternalPlugins", "DayNightCycle"]
for name in references:
    if not (managed / (name + ".dll")).is_file():
        raise SystemExit(f"Required game assembly is missing: {name}.dll")
json_reference = (args.json_assembly or managed / "Newtonsoft.Json.dll").resolve()
if not json_reference.is_file():
    raise SystemExit("Newtonsoft.Json.dll is missing; supply the game's assembly with --json-assembly")


def assembly_hashes():
    """SHA-256 per referenced game assembly plus Newtonsoft.Json.dll, keyed by file name only."""
    files = {name + ".dll": managed / (name + ".dll") for name in references}
    files["Newtonsoft.Json.dll"] = json_reference
    return {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in files.items()}


def match_fingerprint(hashes, declared):
    """The declared set every hash matches, else ("unknown", the file names that differ from the closest set)."""
    best = None
    for entry in declared.get("sets", []):
        differing = sorted(n for n in set(hashes) | set(entry["assemblies"]) if hashes.get(n) != entry["assemblies"].get(n))
        if not differing:
            return entry["name"], []
        if best is None or len(differing) < len(best):
            best = differing
    return "unknown", best if best is not None else sorted(hashes)


if args.provenance:
    # Decision 4: read-only hashes of the supplied assemblies. No path is written; an unknown set never stops the build.
    hashes = assembly_hashes()
    declared = json.loads(args.fingerprints.read_text(encoding="utf-8")) if args.fingerprints.is_file() else {}
    game_set, differing = match_fingerprint(hashes, declared)
    args.provenance.parent.mkdir(parents=True, exist_ok=True)
    args.provenance.write_text(json.dumps({"schema": "persistentcity-build-provenance-1", "managed": "<supplied>", "set": game_set,
                                           "differing": differing, "assemblies": hashes}, indent=2) + "\n", encoding="utf-8")
    print("Game fingerprint set:", game_set + (" (differs: " + ", ".join(differing) + ")" if differing else ""))

dotnet = shutil.which("dotnet")
if dotnet is None and os.name == "nt" and os.environ.get("LOCALAPPDATA"):
    local_host = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
    if local_host.is_file():
        dotnet = str(local_host)
if dotnet is None:
    raise SystemExit("Cannot find dotnet. Add the .NET SDK to PATH; Windows also checks %LOCALAPPDATA%/Microsoft/dotnet/dotnet.exe.")
sdk_lines = subprocess.check_output([dotnet, "--list-sdks"], text=True).splitlines()
if not sdk_lines:
    raise SystemExit(f"No .NET SDK is installed for {dotnet}.")
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
command = [dotnet, str(csc), "-nologo", "-noconfig", "-nostdlib+", "-target:library", "-langversion:9",
           "-nullable:enable", "-optimize+", "-warn:4", "-warnaserror+", "-deterministic+", "-out:" + str(output / "PersistentCity.dll")]
command += ["-reference:" + str(managed / (name + ".dll")) for name in references]
command += ["-reference:" + str(json_reference)]
command += ["-reference:" + str(harmony)]
# A response file keeps long worktree paths under the Windows command-line limit.
# csc reads -noconfig only from the command line.
arguments = command[4:] + sources
response = root / "build/csc.rsp"  # Outside the package folder.
response.write_text("".join('"' + a + '"\n' for a in arguments), encoding="utf-8")
subprocess.run(command[:4] + ["@" + str(response)], check=True)
shutil.copy2(root / "config/persistent-city.json", output / "persistent-city.json")
# The native mod loader registers <mod>/Locales; LocalizorManager reads <folder>/<locale>.json (C5e).
locales = output / "Locales"
locales.mkdir(exist_ok=True)
for table in sorted((root / "Runtime/Localization").glob("*.json")):
    shutil.copy2(table, locales / table.name)
if (root / "README.md").exists():
    shutil.copy2(root / "README.md", output / "README.md")
print("Package:", output)
print("DLL SHA256:", hashlib.sha256((output / "PersistentCity.dll").read_bytes()).hexdigest())
print("Game assemblies and RLO are referenced externally; neither is copied into this package.")
