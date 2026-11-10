#!/usr/bin/env python3
"""Native-evidence ledger and rig batch manifest (F9d; docs/plans/f9-city-acceptance.md, Decision 7 and section 7).

python tools/acceptance/rig_manifest.py [--out FILE] [--build-json FILE] [--head REV]
python tools/acceptance/run.py rig-manifest [--out DIR]          (the same, as a runner profile)

Reads tools/acceptance/native-assumptions.json. For each row it lists the first-parent commits since the row's last rig
evidence (or, without evidence, since the commit that added its record) that touched the row's source globs: the row is
stale when there is one, and their number is its debt. A game fingerprint other than the one the evidence was taken on, or
an unknown one, marks every row stale. "Native batch due" when any row's debt reaches the threshold or the fingerprint
changed. The manifest lists the section 7 checks with the checks of stale rows first. A scheduling signal, never a merge
gate. Python 3 standard library only; no game, rig or bridge.
"""
import argparse
import copy
import json
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
LEDGER = HERE / "native-assumptions.json"
SUPPORTED = HERE / "supported-game.json"
SCHEMA = "persistentcity-native-assumptions-1"
MANIFEST_SCHEMA = "persistentcity-rig-manifest-1"
RANK = {"stale": 0, "unproven": 1, "fresh": 2}


def load(path=LEDGER):
    ledger = json.loads(Path(path).read_text(encoding="utf-8"))
    if ledger.get("schema") != SCHEMA:
        raise ValueError(f"{Path(path).name} is not {SCHEMA}")
    return ledger


def declared_set(path=SUPPORTED):
    path = Path(path)
    return json.loads(path.read_text(encoding="utf-8")).get("current") if path.is_file() else None


def glob_regex(pattern):
    """A repository glob: ** spans directories, * and ? stay inside one path segment."""
    out, i = "", 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out, i = out + "(?:.*/)?", i + 3
        elif pattern.startswith("**", i):
            out, i = out + ".*", i + 2
        elif pattern[i] == "*":
            out, i = out + "[^/]*", i + 1
        elif pattern[i] == "?":
            out, i = out + "[^/]", i + 1
        else:
            out, i = out + re.escape(pattern[i]), i + 1
    return re.compile(out + r"\Z")


def git(repo, *args):
    done = subprocess.run(["git", *args], cwd=repo, capture_output=True, text=True, check=False)
    return done.returncode, done.stdout


class History:
    """First-parent history of one head, read once: every first-parent commit with the files it changed against its
    first parent. A row's commits since a revision on that chain are a suffix of it; a revision off the chain (for example
    a side-branch commit) is resolved with its own query."""

    def __init__(self, repo, head="HEAD"):
        self.repo = repo
        code, sha = git(repo, "rev-parse", "--verify", head + "^{commit}")
        if code != 0:
            raise ValueError(f"head {head} is not a commit")
        self.head = sha.strip()
        self.chain = self._log(self.head)  # newest first: [(sha, [files])]
        self.position = {sha: i for i, (sha, _) in enumerate(self.chain)}
        self.queries = 1

    def _log(self, revisions):
        code, text = git(self.repo, "log", "--first-parent", "--diff-merges=first-parent", "--format=%x00%H", "--name-only",
                         "--no-renames", revisions)
        if code != 0:
            raise ValueError(f"git log {revisions} failed")
        commits = []
        for block in text.split("\0")[1:]:
            lines = [l for l in block.splitlines() if l.strip()]
            commits.append((lines[0], lines[1:]))
        return commits

    def since(self, revision):
        """First-parent commits after revision up to the head, or None when revision is not an ancestor of the head."""
        # A commit id (full or abbreviated) on the chain needs no git call.
        if re.fullmatch(r"[0-9a-f]{7,40}", revision):
            hits = [i for i, (sha, _) in enumerate(self.chain) if sha.startswith(revision)]
            if len(hits) == 1:
                return self.chain[:hits[0]]
        code, sha = git(self.repo, "rev-parse", "--verify", "--quiet", revision + "^{commit}")
        if code != 0:
            return None
        sha = sha.strip()
        if sha in self.position:
            return self.chain[:self.position[sha]]
        if git(self.repo, "merge-base", "--is-ancestor", sha, self.head)[0] != 0:
            return None
        self.queries += 1
        return self._log(sha + ".." + self.head)


def assess(ledger, repo=ROOT, head="HEAD", fingerprint=None, current=None):
    """The ledger at one head. fingerprint: the set name this run's build matched ("unknown" when none, None when no
    assemblies were hashed). current: the declared current set (supported-game.json) used when fingerprint is None."""
    history = History(repo, head)
    effective = fingerprint if fingerprint is not None else current
    threshold = ledger.get("debtThreshold", 3)
    rows = []
    for row in ledger["rows"]:
        since = row.get("lastRigHead") or row.get("recordedAt")
        commits = history.since(since) if since else None
        patterns = [glob_regex(p) for p in row["paths"]]
        reasons = []
        if commits is None:
            debt, touched = None, []
            reasons.append(f"{'rig evidence head' if row.get('lastRigHead') else 'record commit'} {since} is not in this history")
        else:
            touched = [sha for sha, files in commits if any(p.match(f) for f in files for p in patterns)]
            debt = len(touched)
            if debt:
                reasons.append(f"{debt} commits touched its paths since {since[:12]}")
        proven = bool(row.get("lastRigHead"))
        fingerprint_stale = effective == "unknown" or (proven and effective is not None and row.get("lastRigFingerprint") != effective)
        if fingerprint_stale:
            reasons.append("game fingerprint unknown" if effective == "unknown"
                           else f"game fingerprint {row.get('lastRigFingerprint')} -> {effective}")
        if not proven:
            reasons.append("no rig evidence yet")
        if fingerprint_stale or commits is None or (proven and debt):
            status = "stale"
        elif not proven:
            status = "unproven"
        else:
            status = "fresh"
        rows.append({"id": row["id"], "status": status, "debt": debt, "since": since, "rigCheck": row.get("rigCheck"),
                     "pendingCapability": row.get("pendingCapability"), "capability": row.get("capability"),
                     "reasons": reasons, "touchedBy": [sha[:12] for sha in touched]})
    fingerprint_changed = any(r["status"] == "stale" and any(x.startswith("game fingerprint") for x in r["reasons"]) for r in rows)
    over = [r["id"] for r in rows if r["debt"] is not None and r["debt"] >= threshold]
    if fingerprint_changed:
        # Decision 7: a game update marks every native assumption stale.
        for r in rows:
            r["status"] = "stale"
    due_reasons = (["game fingerprint changed"] if fingerprint_changed else []) + [f"{i} has debt >= {threshold}" for i in over]
    return {"schema": SCHEMA, "head": history.head, "fingerprint": effective, "debtThreshold": threshold,
            "fingerprintChanged": fingerprint_changed, "batchDue": bool(due_reasons), "batchDueReasons": due_reasons,
            "stale": [r["id"] for r in rows if r["status"] == "stale"],
            "unproven": [r["id"] for r in rows if r["status"] == "unproven"],
            "requiredChecks": required_checks(ledger, rows), "rows": rows, "gitQueries": history.queries}


def required_checks(ledger, rows):
    """The rig checks a native pass needs at this head: every check of a row that is not fresh, in catalogue order."""
    needed = {r["rigCheck"] for r in rows if r["status"] != "fresh" and r["rigCheck"]}
    return [c["id"] for c in ledger.get("checks", []) if c["id"] in needed]


def manifest(ledger, state):
    """Section 7 checks for the next batch: pinned first, then by the worst row status (stale, unproven, fresh) and the
    largest debt, then catalogue order; pinned last at the end. Rows without a rig check are listed as unscheduled."""
    by_check = {}
    for row in state["rows"]:
        if row["rigCheck"]:
            by_check.setdefault(row["rigCheck"], []).append(row)
    entries = []
    for order, check in enumerate(ledger.get("checks", [])):
        rows = by_check.get(check["id"], [])
        status = min((r["status"] for r in rows), key=RANK.get, default=None)
        debt = max((r["debt"] or 0 for r in rows), default=0)
        entries.append(dict(check, order=order, rows=[r["id"] for r in rows], status=status, debt=debt,
                            due=status in ("stale", "unproven")))
    def key(entry):
        pin = {"first": 0, "last": 2}.get(entry.get("pinned"), 1)
        return (pin, RANK.get(entry["status"], 3), -entry["debt"], entry["order"])
    entries.sort(key=key)
    for entry in entries:
        entry.pop("order")
    return {"schema": MANIFEST_SCHEMA, "head": state["head"], "fingerprint": state["fingerprint"], "batchDue": state["batchDue"],
            "batchDueReasons": state["batchDueReasons"], "checks": entries,
            "unscheduled": [{"row": r["id"], "status": r["status"], "debt": r["debt"], "pendingCapability": r["pendingCapability"]}
                            for r in state["rows"] if not r["rigCheck"]]}


def matches_check(stage, check):
    return stage.get("id") == check or check in (stage.get("checks") or [])


def update(ledger, passed_stages, head, fingerprint, evidence):
    """A copy of the ledger with this head recorded for every row whose rig check passed. Returns (ledger, row ids)."""
    updated, ids = copy.deepcopy(ledger), []
    if not fingerprint or fingerprint == "unknown":
        return updated, ids
    for row in updated["rows"]:
        check = row.get("rigCheck")
        if check and any(matches_check(s, check) for s in passed_stages):
            row["lastRigHead"], row["lastRigFingerprint"], row["lastRigEvidence"] = head, fingerprint, evidence
            ids.append(row["id"])
    return updated, ids


def render(doc):
    lines = [f"native batch due: {'yes (' + '; '.join(doc['batchDueReasons']) + ')' if doc['batchDue'] else 'no'}",
             f"head {doc['head'][:12]}, game fingerprint {doc['fingerprint']}"]
    for i, c in enumerate(doc["checks"], 1):
        lines.append(f"{i:2}. {c['id']:6} {c['status'] or '-':8} debt {c['debt']:3}  {c['mode']:10} {c['title']}"
                     + (f"  [{', '.join(c['rows'])}]" if c["rows"] else ""))
    for u in doc["unscheduled"]:
        lines.append(f"    unscheduled: {u['row']} ({u['status']}, debt {u['debt']}), needs {u['pendingCapability']}")
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--ledger", default=str(LEDGER))
    parser.add_argument("--supported", default=str(SUPPORTED))
    parser.add_argument("--repo", default=str(ROOT))
    parser.add_argument("--head", default="HEAD")
    parser.add_argument("--build-json", help="build.py --provenance record whose fingerprint set to assess against")
    parser.add_argument("--fingerprint", help="fingerprint set name to assess against (default: the declared current set)")
    parser.add_argument("--out", help="write the manifest JSON here (a directory gets rig-manifest.json)")
    args = parser.parse_args(argv)
    ledger = load(args.ledger)
    fingerprint = (json.loads(Path(args.build_json).read_text(encoding="utf-8")).get("set") if args.build_json
                   else args.fingerprint or None)
    state = assess(ledger, args.repo, args.head, fingerprint, declared_set(args.supported))
    doc = dict(manifest(ledger, state), ledger=state)
    print(render(doc))
    if args.out:
        out = Path(args.out)
        if out.is_dir() or not out.suffix:
            out.mkdir(parents=True, exist_ok=True)
            out = out / "rig-manifest.json"
        out.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
        print("manifest:", out.name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
