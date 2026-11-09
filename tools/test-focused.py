"""Run only the unit tests in test files this branch changed.

Usage: python tools/test-focused.py [--base origin/codex/persistent-city] [--list]

Changed files are the branch's commits since the merge base with --base plus
uncommitted and untracked files. Every class declared in a changed tests/**/*.cs
file is passed to `dotnet test --filter`. With no changed test files nothing runs.

This is the focused run for implementers, fix rounds and the harness. It does not
take the machine-wide dotnet lock (wait-other-dotnet.ps1); only the full suite does.
"""
import argparse
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
PROJECT = "tests/PersistentCity.Tests.csproj"
CLASS = re.compile(r"^\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial)\s+)*class\s+(\w+)", re.M)


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=True).stdout


def changed_test_files(base):
    merge_base = git("merge-base", base, "HEAD").strip()
    names = set(git("diff", "--name-only", merge_base).split())
    names.update(git("ls-files", "--others", "--exclude-standard").split())
    return sorted(n for n in names
                  if n.startswith("tests/") and n.endswith(".cs") and os.path.isfile(os.path.join(ROOT, n)))


def test_classes(files):
    classes = set()
    for name in files:
        with open(os.path.join(ROOT, name), encoding="utf-8-sig") as handle:
            classes.update(CLASS.findall(handle.read()))
    return sorted(classes)


def dotnet():
    local = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "dotnet", "dotnet.exe")
    return local if os.path.isfile(local) else "dotnet"


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--base", default="origin/codex/persistent-city")
    parser.add_argument("--list", action="store_true", help="print the filter and exit")
    args = parser.parse_args()

    files = changed_test_files(args.base)
    classes = test_classes(files)
    if not classes:
        print("test-focused: no changed test classes; nothing to run (the full suite runs once before merge)")
        return 0
    expression = "|".join(f"FullyQualifiedName~.{c}." for c in classes)
    print(f"test-focused: {len(classes)} classes from {len(files)} changed test files")
    if args.list:
        print(expression)
        return 0
    return subprocess.run([dotnet(), "test", PROJECT, "--filter", expression], cwd=ROOT).returncode


if __name__ == "__main__":
    sys.exit(main())
