import argparse
import os
import shlex
from pathlib import Path


def absolute(path):
    expanded = Path(os.path.expanduser(os.fspath(path)))
    if str(expanded).startswith('~'):
        raise ValueError(f'cannot expand home in {path!r}')
    return expanded if expanded.is_absolute() else expanded.absolute()


def home():
    return absolute('~')


def stage_mod_command(rig, mod, source, python='python3'):
    rig = absolute(rig)
    return [python, str(rig / 'scripts' / 'install-mac.py'), '--stage-mod', mod, str(absolute(source))]


def stage_mod_line(rig, mod, source, lock_owner):
    return 'BA_LOCK_OWNER=' + shlex.quote(lock_owner) + ' ' + ' '.join(shlex.quote(part) for part in stage_mod_command(rig, mod, source))


def main(argv=None):
    parser = argparse.ArgumentParser(description='Print an install-mac.py --stage-mod command with absolute paths.')
    parser.add_argument('--rig', required=True)
    parser.add_argument('--mod', required=True)
    parser.add_argument('--source', required=True)
    parser.add_argument('--owner', required=True)
    args = parser.parse_args(argv)
    line = stage_mod_line(args.rig, args.mod, args.source, args.owner)
    print(line)
    return line


if __name__ == '__main__':
    main()
