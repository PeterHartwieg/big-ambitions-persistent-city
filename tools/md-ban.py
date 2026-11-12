import argparse
import os
import shlex
import subprocess
import sys

LABEL = "docs-approved"
BASE = "origin/codex/persistent-city"


def markdown(path):
    return path.lower().endswith((".md", ".mdx"))


def run(cmd, cwd):
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit("markdown ban: " + " ".join(cmd[:2]) + " failed: " + result.stderr.strip())
    return result.stdout


def violations(changes):
    found = []
    for status, path, old in changes:
        if not markdown(path):
            continue
        if status in ("added", "copied") or (status == "renamed" and not markdown(old)):
            found.append(path if status == "added" else old + " -> " + path)
    return found


def local_changes(root, base):
    result = subprocess.run(["git", "merge-base", base, "HEAD"], cwd=root, capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit("markdown ban: no merge base with %s; run `git fetch origin codex/persistent-city` or pass --base: %s"
                 % (base, result.stderr.strip()))
    merge_base = result.stdout.strip()
    names = {"A": "added", "C": "copied", "R": "renamed"}
    changes = []
    fields = run(["git", "diff", "--name-status", "-z", "-M", "-C", merge_base], root).split("\0")
    i = 0
    while i < len(fields) and fields[i]:
        code = fields[i][0]
        if code in "RC":
            changes.append((names[code], fields[i + 2], fields[i + 1]))
            i += 3
        else:
            changes.append((names.get(code, "other"), fields[i + 1], ""))
            i += 2
    for path in run(["git", "ls-files", "-z", "--others", "--exclude-standard"], root).split("\0"):
        if path:
            changes.append(("added", path, ""))
    return changes


def pr_changes(root, number, gh):
    labels = run(gh + ["pr", "view", str(number), "--json", "labels", "--jq", ".labels[].name"], root).split()
    if LABEL in labels:
        return None
    out = run(gh + ["api", "--paginate", "repos/{owner}/{repo}/pulls/%d/files" % number,
               "--jq", '.[] | [.status, .filename, (.previous_filename // "")] | @tsv'], root)
    changes = []
    for line in out.splitlines():
        if line:
            status, path, old = (line.split("\t") + ["", ""])[:3]
            changes.append((status, path, old))
    return changes


def main():
    parser = argparse.ArgumentParser(prog="md-ban.py")
    parser.add_argument("--pr", type=int)
    parser.add_argument("--base", default=BASE)
    parser.add_argument("--root", default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
    args = parser.parse_args()
    if args.pr is not None:
        changes = pr_changes(args.root, args.pr, shlex.split(os.environ.get("PC_MD_BAN_GH", "gh")))
        if changes is None:
            print("markdown ban: PR #%d has the %s label" % (args.pr, LABEL))
            return 0
    else:
        changes = local_changes(args.root, args.base)
    found = violations(changes)
    if found:
        print("markdown ban: new markdown files need Peter's %s label on the PR: %s" % (LABEL, ", ".join(found)))
        return 1
    print("markdown ban: no new markdown files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
