import argparse
import hashlib
import json
import shutil
import sys

from rigpaths import absolute

PROFILE = '~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac'
RIG_SUPPORT = '~/Library/Application Support/BigAmbitionsAgentTestsMac'
PREFERENCES = '~/Library/Preferences/com.hovgaardgames.bigambitions.agenttests.mac.plist'
CONFIG = 'persistent-city.json'


def sha(path):
    return hashlib.sha256(absolute(path).read_bytes()).hexdigest()


def set_package_config(path, overrides):
    path = absolute(path)
    data = json.loads(path.read_text())
    data.update(overrides)
    path.write_text(json.dumps(data, indent=2) + '\n')
    return data


def stage_package(build, package, overrides):
    build, package = absolute(build), absolute(package)
    if not (build / CONFIG).is_file():
        raise FileNotFoundError(f'{build / CONFIG}: built package config missing')
    shutil.copytree(build, package, dirs_exist_ok=True)
    set_package_config(package / CONFIG, overrides)
    return package


def backup_profile(stage, fixture, home='~', profile=PROFILE, rig=RIG_SUPPORT, preferences=PREFERENCES):
    home, stage, profile, rig = absolute(home), absolute(stage), absolute(profile), absolute(rig)
    paths = list((profile / 'ModsLocal/PersistentCity').rglob('*')) + [rig / 'install.json', profile / CONFIG,
             profile / 'agent-frame-cap.json', profile / 'agent-save-mode.json', absolute(preferences)]
    paths += list(profile.glob('*.json')) + list(profile.glob('*.xml')) + list(profile.glob('*.prefs'))
    manifest = {}
    backup = stage / 'backup'
    backup.mkdir(parents=True, exist_ok=True)
    for path in sorted(set(paths)):
        if path.is_dir():
            continue
        key = str(path.relative_to(home))
        manifest[key] = sha(path) if path.exists() else None
        if path.exists():
            target = backup / key
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    rlo = {str(p.relative_to(home)): sha(p) for p in (profile / 'ModsLocal/RealLifeOverhaul').rglob('*') if p.is_file()}
    sealed = {str(p.relative_to(home)): sha(p) for p in (profile / 'SaveGames').rglob('*') if p.is_file() and fixture in p.name}
    (stage / 'pre-stage-manifest.json').write_text(json.dumps({'files': manifest, 'rlo': rlo, 'sealed': sealed}, indent=2))
    return manifest, rlo, sealed


def override(text):
    key, eq, value = text.partition('=')
    if not eq or not key:
        raise argparse.ArgumentTypeError(f'{text!r}: expected KEY=JSON')
    return key, json.loads(value)


def main(argv=None):
    parser = argparse.ArgumentParser(description='Back up the rig profile and stage a Persistent City package for install-mac.py.')
    parser.add_argument('--stage', required=True)
    parser.add_argument('--build', required=True)
    parser.add_argument('--package', required=True)
    parser.add_argument('--fixture', required=True)
    parser.add_argument('--set', dest='overrides', type=override, action='append', default=[])
    parser.add_argument('--package-only', action='store_true')
    args = parser.parse_args(argv)
    overrides = dict([('SaveCostDiagnostics', False)] + args.overrides)
    summary = {}
    if not args.package_only:
        manifest, rlo, sealed = backup_profile(args.stage, args.fixture)
        summary.update(original_files=len(manifest), rlo_files=len(rlo), sealed_files=len(sealed),
                       manifest_sha256=sha(absolute(args.stage) / 'pre-stage-manifest.json'))
    package = stage_package(args.build, args.package, overrides)
    summary.update(package=str(package), dll_sha256=sha(package / 'PersistentCity.dll'), config=overrides)
    print(json.dumps(summary))
    return summary


if __name__ == '__main__':
    main(sys.argv[1:])
