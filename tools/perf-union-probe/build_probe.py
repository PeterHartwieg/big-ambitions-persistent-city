"""Build the rig-only F4b union probe DLL with pinned local references. No installation, no packaging.

The probe binds Persistent City by reflection, so it has no PersistentCity.dll reference and the same
artifact runs in candidate and reference mode. The manifest records names and hashes only (no
machine paths), so two builds of the same inputs produce byte-identical manifests.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

MODULE = Path(__file__).resolve().parent
REPO = MODULE.parents[1]
sys.path.insert(0, str(MODULE))
import check_scopes  # noqa: E402

HARMONY_SHA256 = '7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c'  # Lib.Harmony 2.4.2, as tools/build.py
REFERENCES = ['mscorlib', 'System', 'System.Core', 'netstandard', 'BigAmbitions', 'BigAmbitions.ModAPI',
              'HGExtensions', 'HGPlugins', 'UnityEngine.CoreModule', 'Newtonsoft.Json']
SOURCES = ['HarmonyScan.cs', 'Probe.cs', 'ProbeLogic.cs', 'ScopeInventory.cs']
ARTIFACT = 'PerfUnionProbe.dll'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def find_dotnet():
    dotnet = shutil.which('dotnet')
    if not dotnet and os.environ.get('LOCALAPPDATA'):
        local = Path(os.environ['LOCALAPPDATA']) / 'Microsoft/dotnet/dotnet.exe'
        if local.is_file():
            dotnet = str(local)
    if not dotnet:
        raise SystemExit('The probe build requires dotnet in PATH')
    return dotnet


def build(managed, harmony, output):
    output = output.resolve()
    if not output.is_relative_to(REPO / 'build'):
        raise SystemExit('Output must be below this checkout ignored build directory')
    for parent in [output, *output.parents]:
        if parent.is_symlink() or (hasattr(parent, 'is_junction') and parent.is_junction()):
            raise SystemExit('Reparse point output refused')
    managed = managed.resolve()
    references = [managed / (name + '.dll') for name in REFERENCES] + [harmony.resolve()]
    for path in references:
        if not path.is_file():
            raise SystemExit('Missing explicit local reference: ' + str(path))
    if sha(harmony) != HARMONY_SHA256:
        raise SystemExit('0Harmony.dll does not match its pinned checksum')
    problems = check_scopes.check(REPO)
    if problems:
        raise SystemExit('check_scopes failed; the expected scope list does not match the source:\n' + '\n'.join(problems))
    sources = [MODULE / name for name in SOURCES]
    initial = {str(p): sha(p) for p in references + sources}
    dotnet = find_dotnet()
    sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True).splitlines()
    if not sdk or not (match := re.fullmatch(r'(\S+) \[(.*)\]', sdk[-1])):
        raise SystemExit('Cannot locate the existing SDK compiler')
    compiler = Path(match[2]) / match[1] / 'Roslyn/bincore/csc.dll'
    output.mkdir(parents=True, exist_ok=True)
    artifact = output / ARTIFACT
    arguments = ['-nologo', '-nostdlib+', '-target:library', '-langversion:9', '-nullable:disable', '-optimize+',
                 '-warn:4', '-warnaserror+', '-deterministic+', '-debug-', '-pathmap:' + str(MODULE) + '=/PerfUnionProbe',
                 '-out:' + str(artifact)]
    arguments += ['-reference:' + str(p) for p in references] + [str(p) for p in sources]
    # A response file avoids Windows command-line limits and shell interpolation.
    rsp = output / 'compile.rsp'
    rsp.write_text('\n'.join('"' + a.replace('"', '\\"') + '"' for a in arguments) + '\n', encoding='utf-8')
    subprocess.run([dotnet, str(compiler), '-noconfig', '@' + str(rsp)], check=True)
    if any(sha(Path(path)) != digest for path, digest in initial.items()):
        raise SystemExit('Source/reference changed during compilation; artifact is not usable')
    manifest = dict(format=1, tool='perf-union-probe', native_validated=False, packaged=False, compiler_sdk=match[1],
                    expected_scope_hash=check_scopes.inventory_hash(REPO),
                    output=dict(name=ARTIFACT, sha256=sha(artifact)),
                    sources=[dict(name='tools/perf-union-probe/' + p.name, sha256=initial[str(p)]) for p in sources],
                    references=[dict(name=p.name, sha256=initial[str(p)]) for p in references])
    text = json.dumps(manifest, indent=2) + '\n'
    (output / 'build-manifest.json').write_text(text, encoding='utf-8')
    return manifest, text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-managed', type=Path, required=True)
    parser.add_argument('--harmony', type=Path, default=REPO / 'build/PersistentCity/Dependencies/0Harmony.dll',
                        help='Pinned Lib.Harmony 2.4.2 (tools/build.py places it here)')
    parser.add_argument('--output', type=Path, default=REPO / 'build/perf-union-probe')
    args = parser.parse_args()
    _, text = build(args.game_managed, args.harmony, args.output)
    print(text, end='')


if __name__ == '__main__':
    main()
