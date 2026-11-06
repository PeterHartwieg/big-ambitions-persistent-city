"""Fail on broken relative links in the repository's Markdown files.

Usage: python tools/docs-links/check.py [repo-root]

Checks inline links and images, [text](target), whose target is a relative path.
External URLs (scheme://, mailto:) and pure #anchors are skipped. Fenced code blocks
and inline code are ignored. Anchors are not checked; only the target file or folder.
A trailing source line reference (File.cs:55 or File.cs:55-60) is ignored. Links that
leave the repository (to a sibling checkout) cannot be checked here and are skipped.
"""
import os
import re
import sys
from urllib.parse import unquote

SKIP_DIRS = {".git", "build", "bin", "obj", "node_modules", ".vs"}
LINK = re.compile(r"!?\[(?:[^\[\]]|\[[^\]]*\])*\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")
FENCE = re.compile(r"^\s*(```|~~~)")
INLINE_CODE = re.compile(r"`[^`]*`")
LINE_SUFFIX = re.compile(r":\d+(?:-\d+)?$")


def markdown_files(root):
    for directory, subdirs, files in os.walk(root):
        subdirs[:] = [d for d in subdirs if d not in SKIP_DIRS]
        for name in files:
            if name.lower().endswith(".md"):
                yield os.path.join(directory, name)


def broken_links(path, root):
    with open(path, encoding="utf-8-sig") as handle:
        lines = handle.read().splitlines()
    in_fence = False
    for number, line in enumerate(lines, 1):
        if FENCE.match(line):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        for match in LINK.finditer(INLINE_CODE.sub("", line)):
            target = match.group(1)
            if re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", target) or target.startswith("#"):
                continue
            file_part = unquote(target.split("#", 1)[0].split("?", 1)[0])
            if not file_part:
                continue
            file_part = LINE_SUFFIX.sub("", file_part)
            resolved = os.path.normpath(os.path.join(os.path.dirname(path), file_part))
            if os.path.relpath(resolved, root).startswith(".."):
                continue
            if not os.path.exists(resolved):
                yield number, target


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", ".."))
    failures = 0
    checked = 0
    for path in sorted(markdown_files(root)):
        checked += 1
        for number, target in broken_links(path, root):
            failures += 1
            print(f"{os.path.relpath(path, root)}:{number}: broken link {target}")
    print(f"{checked} Markdown files checked, {failures} broken relative links")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
