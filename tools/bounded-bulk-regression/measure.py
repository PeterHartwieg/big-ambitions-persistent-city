"""Matched F3 component/replay measurements. Sequential, foreground, no execution deadlines."""
from pathlib import Path
import io
import json
import os
import platform
import shutil
import subprocess
import tarfile
import datetime

root = Path(__file__).resolve().parents[2]
out = root / 'docs/perf/assets/f3-bounded-bulk'
out.mkdir(parents=True, exist_ok=True)
baseline = root / 'build/f3-baseline'
def extract(ref, paths, target):
    target.mkdir(parents=True, exist_ok=True)
    archive = subprocess.check_output(['git', 'archive', ref, *paths], cwd=root)
    with tarfile.open(fileobj=io.BytesIO(archive)) as contents:
        for member in contents.getmembers():
            if member.isfile():
                destination = (target / member.name).resolve()
                if target.resolve() not in destination.parents:
                    raise RuntimeError('Archive path escaped baseline directory')
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(contents.extractfile(member).read())
extract('bd99eb3', ['Core', 'Persistence'], baseline)
dotnet = str(Path(os.environ['LOCALAPPDATA']) / 'Microsoft/dotnet/dotnet.exe')
wait = ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
        r'C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1', '-Repo', str(root)]
environment = os.environ.copy()
environment.update(MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0', UseSharedCompilation='false')
metadata = dict(baseline='bd99eb3', head=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
                working_diff=True, utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                host=platform.platform(), runtime=subprocess.check_output([dotnet, '--version'], text=True).strip(),
                note='Uncommitted F3 implementation; Core/Persistence baseline extracted read-only, no branch switch; no native/whole-frame evidence')
(out / 'metadata.json').write_text(json.dumps(metadata, indent=2) + '\n')
def run(name, project, extra=(), arguments=()):
    subprocess.run(wait, cwd=root, check=True)
    command = [dotnet, 'run', '--project', project, '--configuration', 'Release', *extra]
    if arguments:
        command += ['--', *arguments]
    log = root / 'build' / ('f3-' + name + '.log')
    with log.open('w', encoding='utf-8') as stream:
        result = subprocess.run(command, cwd=root, env=environment, stdout=stream, stderr=subprocess.STDOUT)
    if result.returncode:
        print(log.read_text(encoding='utf-8')[-6000:], flush=True)
        raise SystemExit(result.returncode)
    rows = [json.loads(line) for line in log.read_text(encoding='utf-8').splitlines() if line.startswith('{')]
    (out / (name + '.jsonl')).write_text(''.join(json.dumps(row) + '\n' for row in rows), encoding='utf-8')
    print(name + ': ' + str(len(rows)) + ' observations', flush=True)
    return rows
project = 'tools/bounded-bulk-regression/BoundedBulkRegression.csproj'
for suffix, arguments in [('postings', ()), ('integrated', ('--integrated',))]:
    before = run('before-' + suffix, project, ['-p:SourceRoot=' + str(baseline)], arguments)
    after = run('after-' + suffix, project, ['-p:SourceRoot=' + str(root)], arguments)
    if suffix == 'integrated':
        for old, new in zip(before, after):
            for key in ['consumers', 'sales', 'purchases', 'routes', 'reads', 'saveSha256', 'finalBacklogGameMinutes']:
                if old[key] != new[key]:
                    raise RuntimeError('Replay changed ' + key)
# Isolate the tiny capture-buffer win from the admission change: same current fixture,
# first F3 implementation (already bounded) versus the pre-sized final implementation.
capture_baseline = root / 'build/f3-capture-before-capacity'
extract('0f632b3', ['Core', 'Runtime'], capture_baseline)
shutil.copytree(root / 'tools/campaign-reader-regression', capture_baseline / 'tools/campaign-reader-regression',
                dirs_exist_ok=True, ignore=shutil.ignore_patterns('bin', 'obj'))
run('before-capacity-campaign', str(capture_baseline / 'tools/campaign-reader-regression/CampaignReaderRegression.csproj'))
run('campaign', 'tools/campaign-reader-regression/CampaignReaderRegression.csproj')
run('routes', 'tools/road-ceiling-regression/RoadCeilingRegression.csproj')
