"""Build a standalone diagnostic DLL only. Root owns invoking this .NET build; no installation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--game-managed', type=Path, required=True)
parser.add_argument('--candidate', type=Path, required=True)
parser.add_argument('--harmony', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
module = Path(__file__).resolve().parent
repo = module.parents[1]
output = args.output.resolve()
if not output.is_relative_to(repo/'build'):
    raise SystemExit('Output must be below this checkout ignored build directory')
for parent in [output, *output.parents]:
    if parent.is_symlink() or (hasattr(parent, 'is_junction') and parent.is_junction()):
        raise SystemExit('Reparse point output refused')
managed = args.game_managed.resolve()
names = ['mscorlib', 'System', 'System.Core', 'netstandard', 'BigAmbitions', 'BigAmbitions.ModAPI',
         'BigAmbitions.AI', 'BigAmbitions.Items', 'BigAmbitions.Characters', 'BigAmbitions.Neighborhoods',
         'HGExtensions', 'HGPlugins', 'DayNightCycle', 'ExternalPlugins', 'UnityEngine.CoreModule', 'Newtonsoft.Json']
references = [managed/(name+'.dll') for name in names]+[args.candidate.resolve(), args.harmony.resolve()]
for path in references:
    if not path.is_file():
        raise SystemExit('Missing explicit local reference: '+str(path))
sources = sorted(module.glob('*.cs'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
initial = {str(p): sha(p) for p in references+sources}
dotnet = shutil.which('dotnet')
if not dotnet and os.environ.get('LOCALAPPDATA'):
    local = Path(os.environ['LOCALAPPDATA'])/'Microsoft/dotnet/dotnet.exe'
    if local.is_file(): dotnet = str(local)
if not dotnet:
    raise SystemExit('The root-owned build requires dotnet in PATH')
sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True).splitlines()
if not sdk or not (match := re.fullmatch(r'(\S+) \[(.*)\]', sdk[-1])):
    raise SystemExit('Cannot locate the existing SDK compiler')
compiler = Path(match[2])/match[1]/'Roslyn/bincore/csc.dll'
output.mkdir(parents=True, exist_ok=True)
arguments = ['-nologo', '-nostdlib+', '-target:library', '-langversion:9',
             '-nullable:disable', '-optimize+', '-warn:4', '-deterministic+',
             '-pathmap:'+str(module)+'=/E5bRigProof', '-out:'+str(output/'E5bNativeRigProof.dll')]
arguments += ['-reference:'+str(p) for p in references]+[str(p) for p in sources]
# Compiler response file avoids Windows command-line limits and shell interpolation.
rsp = output/'compile.rsp'
rsp.write_text('\n'.join('"'+argument.replace('"','\\"')+'"' for argument in arguments)+'\n', encoding='utf-8')
subprocess.run([dotnet, str(compiler), '-noconfig', '@'+str(rsp)], check=True)
if any(sha(Path(path)) != digest for path,digest in initial.items()):
    raise SystemExit('Source/reference changed during compilation; artifact is not usable')
artifact = output/'E5bNativeRigProof.dll'
manifest = dict(format=1, native_validated=False, compiler_sdk=match[1],
    output=dict(path=str(artifact),sha256=sha(artifact)),
    sources=[dict(path=str(p),sha256=initial[str(p)]) for p in sources],
    references=[dict(path=str(p),sha256=initial[str(p)]) for p in references],
    invocation=[dotnet,str(compiler),'-noconfig','@'+str(rsp)])
(output/'build-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps(manifest,indent=2))
