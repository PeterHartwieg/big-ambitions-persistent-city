#!/usr/bin/env python3
"""Persistent City acceptance runner (F9a; docs/plans/f9-city-acceptance.md, Decisions 1, 2, 5 and 6).

python tools/acceptance/run.py quick [--out DIR] [--dotnet PATH]
python tools/acceptance/run.py full --game-managed <Managed dir> [--price-weight W]
python tools/acceptance/run.py summarize --result <acceptance.json> [--rig FILE ...] [--performance FILE]

Python 3 standard library only. Stages run in the foreground, one at a time, under a machine-wide lock.
Writes {out}/acceptance.json (schema persistentcity-acceptance-1). Offline evidence only: no game, rig or bridge.
Exit 0 unless the summary verdict is fail (1); --strict also exits 3 when incomplete. 2 = usage, 4 = lock held.
"""
import argparse
import datetime
import getpass
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import economy_stage  # noqa: E402

ROOT = HERE.parents[1]
SCHEMA = "persistentcity-acceptance-1"
VERDICTS = ("pass", "fail", "expected-red", "not-run", "incomplete")
FUNCTIONAL = ("semantic", "native")
LOCK_NAME = "persistentcity-acceptance.lock"


class LockHeld(Exception):
    pass


# ---------------------------------------------------------------- lock and process checks

def pid_alive(pid):
    if pid <= 0:
        return False
    if os.name == "nt":
        import ctypes
        kernel = ctypes.windll.kernel32
        handle = kernel.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not handle:
            return False
        code = ctypes.c_ulong()
        try:
            return bool(kernel.GetExitCodeProcess(handle, ctypes.byref(code))) and code.value == 259  # STILL_ACTIVE
        finally:
            kernel.CloseHandle(handle)
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


class RunnerLock:
    """Machine-wide lock in the OS temp directory: created exclusively, holds the PID and checkout,
    and is cleared when its PID is gone (section 3.9)."""

    def __init__(self, path=None, checkout=ROOT):
        self.path = Path(path or os.environ.get("PC_ACCEPTANCE_LOCK") or Path(tempfile.gettempdir()) / LOCK_NAME)
        self.checkout = str(checkout)
        self.cleared_stale = None

    def acquire(self):
        for _ in range(2):
            try:
                fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            except FileExistsError:
                holder = self._holder()
                if holder is not None and pid_alive(holder.get("pid", -1)):
                    raise LockHeld(f"another acceptance runner holds {self.path.name} (pid {holder['pid']})")
                self.cleared_stale = holder
                try:
                    self.path.unlink()
                except FileNotFoundError:
                    pass
                continue
            with os.fdopen(fd, "w", encoding="utf-8") as handle:
                json.dump({"pid": os.getpid(), "checkout": self.checkout}, handle)
            return self
        raise LockHeld(f"could not create {self.path.name}")

    def _holder(self):
        try:
            return json.loads(self.path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return {}

    def release(self):
        holder = self._holder()
        if holder and holder.get("pid") == os.getpid():
            self.path.unlink()

    def __enter__(self):
        return self.acquire()

    def __exit__(self, *exc):
        self.release()


def other_test_processes():
    """Running testhost / `dotnet test` processes on this machine (tasklist on Windows, ps elsewhere)."""
    try:
        if os.name == "nt":
            text = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True, text=True, check=False).stdout
            return [line.split(",")[0].strip('"') + " " + line.split(",")[1].strip('"')
                    for line in text.splitlines() if line.lower().startswith('"testhost')]
        text = subprocess.run(["ps", "-A", "-o", "pid=,args="], capture_output=True, text=True, check=False).stdout
        own = str(os.getpid())
        return [line.strip() for line in text.splitlines()
                if ("testhost" in line or "dotnet test" in line) and line.split()[0] != own]
    except OSError:
        return []


def wait_for_dotnet(no_wait, poll=5.0, probe=other_test_processes):
    hook = os.environ.get("PC_DOTNET_WAIT")
    if hook:
        subprocess.run(hook, shell=True, check=False)
    announced = False
    while True:
        busy = probe()
        if not busy:
            return
        if no_wait:
            raise LockHeld("other test processes are running: " + ", ".join(busy))
        if not announced:
            print("waiting for other test processes:", ", ".join(busy), flush=True)
            announced = True
        time.sleep(poll)


# ---------------------------------------------------------------- helpers

def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def evidence_entry(out, path, retained=False):
    path = Path(path)
    return {"path": path.relative_to(out).as_posix(), "sha256": sha256_file(path), "bytes": path.stat().st_size, "retained": retained}


def find_dotnet(explicit=None):
    if explicit:
        return explicit
    found = shutil.which("dotnet")
    if found is None and os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        local = Path(os.environ["LOCALAPPDATA"]) / "Microsoft/dotnet/dotnet.exe"
        if local.is_file():
            found = str(local)
    return found


def git(*args):
    try:
        return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, check=False).stdout
    except OSError:
        return b""


class Sanitizer:
    """Removes absolute paths, home directory and user name from every string in the result."""

    def __init__(self, out, managed=None):
        self.pairs = []
        for path, label in ((managed, "<supplied>"), (out, "{out}"), (ROOT, "{repo}"), (Path.home(), "~")):
            if path is None:
                continue
            text = str(Path(path).resolve())
            for variant in {text, text.replace("\\", "/"), text.replace("/", "\\")}:
                self.pairs.append((variant, label))
        self.pairs.sort(key=lambda p: -len(p[0]))
        try:
            user = getpass.getuser()
        except Exception:
            user = ""
        self.user = re.compile(r"\b" + re.escape(user) + r"\b", re.IGNORECASE) if len(user) >= 3 else None

    def text(self, value):
        for raw, label in self.pairs:
            value = value.replace(raw, label)
        if self.user is not None:
            value = self.user.sub("<user>", value)
        return value

    def __call__(self, value):
        if isinstance(value, str):
            return self.text(value)
        if isinstance(value, list):
            return [self(v) for v in value]
        if isinstance(value, dict):
            return {self(k): self(v) for k, v in value.items()}
        return value


def read_source_value(spec):
    """Compiled default read from the source at this commit (a literal initialiser)."""
    path = ROOT / spec["file"]
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return None
    match = re.search(spec["pattern"], text)
    if not match:
        return None
    value = match.group(1)
    if spec.get("type") == "number":
        try:
            return int(value) if re.fullmatch(r"-?\d+", value) else float(value)
        except ValueError:
            return value
    return value


# ---------------------------------------------------------------- TRX

def parse_trx(path):
    """Counts and per-test outcomes from a VSTest TRX file."""
    tree = ET.parse(path)
    ns = {"t": tree.getroot().tag.split("}")[0].strip("{")} if tree.getroot().tag.startswith("{") else {}
    q = (lambda tag: "t:" + tag) if ns else (lambda tag: tag)
    names = {}
    for test in tree.getroot().iterfind(".//" + q("UnitTest"), ns):
        method = test.find(q("TestMethod"), ns)
        if method is not None:
            names[test.get("id")] = (method.get("className", ""), method.get("name", ""))
    results = []
    for result in tree.getroot().iterfind(".//" + q("UnitTestResult"), ns):
        cls, name = names.get(result.get("testId"), ("", result.get("testName", "")))
        full = cls + "." + name if cls else name
        results.append({"fullName": full, "name": name, "display": result.get("testName", full), "outcome": result.get("outcome", "")})
    return results


def count(results):
    passed = sum(r["outcome"] == "Passed" for r in results)
    failed = sum(r["outcome"] in ("Failed", "Error", "Timeout", "Aborted") for r in results)
    skipped = sum(r["outcome"] in ("NotExecuted", "Inconclusive", "Pending") for r in results)
    return {"total": len(results), "passed": passed, "failed": failed, "skipped": skipped,
            "failedTests": [r["display"] for r in results if r["outcome"] in ("Failed", "Error", "Timeout", "Aborted")][:20]}


def filter_terms(expression):
    """Alternatives of FullyQualifiedName~X / Name~X, or None when the filter has other clauses."""
    terms = []
    for part in expression.split("|"):
        match = re.fullmatch(r"(FullyQualifiedName|Name)~([\w.]+)", part.strip())
        if not match:
            return None
        terms.append((match.group(1), match.group(2)))
    return terms


def apply_filter(results, terms):
    def hit(r):
        return any((r["fullName"] if key == "FullyQualifiedName" else r["name"]).find(value) >= 0 for key, value in terms)
    return [r for r in results if hit(r)]


# ---------------------------------------------------------------- stages

class Context:
    def __init__(self, out, dotnet, managed, price_weight, no_wait, settings):
        self.out = out
        self.dotnet = dotnet
        self.managed = managed
        self.price_weight = price_weight
        self.no_wait = no_wait
        self.settings = settings
        self.unit_trx = None
        self.artifact = None

    def expand(self, argv, stage_id):
        values = {"{python}": sys.executable, "{dotnet}": self.dotnet or "dotnet", "{out}": str(self.out),
                  "{managed}": str(self.managed) if self.managed else "", "{id}": stage_id}
        expanded = []
        for arg in argv:
            if arg == "{priceWeightArgs}":
                if self.price_weight is not None:
                    expanded += ["--price-weight", repr(self.price_weight)]
                continue
            for key, value in values.items():
                arg = arg.replace(key, value)
            expanded.append(arg)
        return expanded


def stage_steps(stage):
    if "filter" in stage and not stage.get("steps"):
        trx = f"trx/{stage['id']}/{stage['id']}.trx"
        return [["{dotnet}", "test", "tests/PersistentCity.Tests.csproj", "--no-build", "--filter", stage["filter"],
                 "--logger", f"trx;LogFileName={stage['id']}.trx", "--results-directory", "{out}/trx/" + stage["id"]]], trx
    return stage.get("steps", []), stage.get("trx")


def run_steps(ctx, stage, steps, record):
    """Run each step in the foreground with output to logs/<id>.log; returns the exit codes."""
    log_path = ctx.out / "logs" / (stage["id"] + ".log")
    log_path.parent.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false", DOTNET_CLI_TELEMETRY_OPTOUT="1",
               DOTNET_NOLOGO="1")
    codes = []
    child = 0.0
    with open(log_path, "ab") as log:
        for argv in steps:
            if stage.get("dotnet"):
                wait_for_dotnet(ctx.no_wait)
            command = ctx.expand(argv, stage["id"])
            log.write(("$ " + " ".join(argv) + "\n").encode())
            log.flush()
            started = time.perf_counter()
            try:
                code = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, env=env, check=False).returncode
            except OSError as error:
                log.write(f"cannot start: {error}\n".encode())
                code = 127
            child += time.perf_counter() - started
            codes.append(code)
            if code != 0 and stage["rule"] != "economy":
                break
    record["metrics"]["childSeconds"] = round(child, 3)
    record["evidence"].append(log_path)
    return codes, log_path


def rule_trx(ctx, stage, record):
    terms = filter_terms(stage["filter"]) if "filter" in stage else None
    if "filter" in stage and terms is not None and ctx.unit_trx is not None:
        results = apply_filter(parse_trx(ctx.unit_trx), terms)
        record["reusedFrom"] = "unit"
        record["argv"] = ["(reused unit TRX)", "--filter", stage["filter"]]
        return trx_verdict(record, results, 0, "")
    steps, trx = stage_steps(stage)
    record["argv"] = steps[0] if len(steps) == 1 else steps
    codes, log_path = run_steps(ctx, stage, steps, record)
    code = codes[-1] if codes else 0
    record["exit"] = code
    trx_path = ctx.out / trx
    if not trx_path.is_file():
        log = log_path.read_text(encoding="utf-8", errors="replace")
        if code == 0 or "No test matches the given testcase filter" in log:
            return "not-run", "filter matched no tests" if "filter" in stage else "no TRX written"
        return "fail", f"exit {code} and no TRX written (build or host failure; see logs/{stage['id']}.log)"
    record["evidence"].append(trx_path)
    results = parse_trx(trx_path)
    if stage["id"] == "unit":
        ctx.unit_trx = trx_path
    return trx_verdict(record, results, code, log_path.read_text(encoding="utf-8", errors="replace"))


def trx_verdict(record, results, code, log):
    counts = count(results)
    record["metrics"].update(counts)
    if counts["failed"]:
        return "fail", f"{counts['failed']} failed"
    if counts["passed"] == 0:
        return "not-run", "filter matched no tests"
    if code != 0:
        return "fail", f"exit {code} with {counts['passed']} passed"
    return "pass", f"{counts['passed']} passed, {counts['skipped']} skipped"


def rule_exit_zero(ctx, stage, record):
    steps = stage["steps"]
    record["argv"] = steps[0] if len(steps) == 1 else steps
    codes, _ = run_steps(ctx, stage, steps, record)
    record["exit"] = codes[-1] if codes else 0
    return ("pass", "exit 0") if all(c == 0 for c in codes) else ("fail", f"exit {record['exit']}")


def rule_build(ctx, stage, record):
    record["argv"] = stage["steps"][0]
    if not ctx.managed:
        return "not-run", "no --game-managed supplied"
    verdict, reason = rule_exit_zero(ctx, stage, record)
    log = (ctx.out / "logs" / (stage["id"] + ".log")).read_text(encoding="utf-8", errors="replace")
    match = re.search(r"DLL SHA256:\s*([0-9a-f]{64})", log)
    if match:
        ctx.artifact = match.group(1)
        record["digests"]["PersistentCity.dll"] = match.group(1)
    elif verdict == "pass":
        return "fail", "build printed no DLL SHA256"
    return verdict, reason


def rule_economy(ctx, stage, record):
    steps = stage["steps"]
    record["argv"] = steps
    codes, _ = run_steps(ctx, stage, steps, record)
    record["exit"] = codes[0] if codes else None
    acceptance = ctx.out / stage["acceptance"]
    compiled = ctx.settings.get("FoodChoicePriceWeight")
    result = economy_stage.assess_file(acceptance, harness_exit=codes[0] if codes else 127,
                                       acceptance_exit=codes[1] if len(codes) > 1 else None,
                                       compiled_weight=compiled, declared_weight=ctx.price_weight)
    if acceptance.is_file():
        record["evidence"].append(acceptance)
        record["digests"]["acceptance.json"] = sha256_file(acceptance)
    record["metrics"].update(result["metrics"])
    record["routed"] = result["routed"]
    record["deviations"] = result["deviations"]
    return result["verdict"], result["reason"]


def assess_movement(data, expected_red):
    """Self-checks must pass; each compared arm must be equal unless listed expected-red."""
    listed = set(expected_red)
    failed_checks = [c["check"] for c in data.get("selfChecks", []) + data.get("e2", []) if not c.get("pass")]
    equal, red, unexpected, green = [], [], [], []
    for arm in data.get("arms", []):
        comparison = arm.get("comparison")
        if not comparison:
            continue
        name = arm["arm"]
        if comparison.get("Identical"):
            equal.append(name)
            if name in listed:
                green.append(name)
        elif name in listed:
            red.append(name)
        else:
            unexpected.append(name)
    seen = {a["arm"] for a in data.get("arms", [])}
    metrics = {"equalArms": equal, "expectedRedArms": red, "unexpectedDifferingArms": unexpected,
               "listedNowGreen": green, "listedNotRun": sorted(listed - seen), "failedSelfChecks": failed_checks}
    notes = [f"movement: listed expected-red arm {name} now equals its reference" for name in green]
    if failed_checks:
        return "fail", "self-checks failed: " + "; ".join(failed_checks), metrics, notes
    if unexpected:
        return "fail", "arms differ from their reference and are not listed expected-red: " + ", ".join(unexpected), metrics, notes
    if red:
        return "expected-red", f"{len(red)} listed arms differ as expected, {len(equal)} equal", metrics, notes
    return "pass", f"{len(equal)} arms equal their reference", metrics, notes


def rule_movement(ctx, stage, record):
    record["argv"] = stage["steps"][0]
    codes, _ = run_steps(ctx, stage, stage["steps"], record)
    record["exit"] = codes[-1] if codes else None
    output = ctx.out / stage["output"]
    if not output.is_file():
        return "fail", f"exit {record['exit']} and no {stage['output']} written"
    record["evidence"].append(output)
    data = json.loads(output.read_text(encoding="utf-8"))
    expectations = json.loads((ROOT / stage["expectations"]).read_text(encoding="utf-8"))
    verdict, reason, metrics, notes = assess_movement(data, expectations.get("expectedRed", []))
    record["metrics"].update(metrics)
    record["notes"] = notes
    for arm in data.get("arms", []):
        final = (arm.get("result") or {}).get("FinalSaveSha256")
        if final:
            record["digests"][arm["arm"]] = final
    if record["exit"] not in (0, None) and verdict != "fail":
        return "fail", f"harness exited {record['exit']}"
    return verdict, reason


RULES = {"trx": rule_trx, "exit-zero": rule_exit_zero, "build": rule_build, "economy": rule_economy, "movement": rule_movement}


def run_stage(ctx, stage):
    record = {"id": stage["id"], "class": stage["class"], "required": stage.get("required", stage["class"] in FUNCTIONAL),
              "argv": stage.get("steps", [[]])[0] if stage.get("steps") else [], "exit": None, "seconds": 0.0,
              "verdict": "not-run", "reason": "", "metrics": {}, "digests": {}, "evidence": []}
    if stage.get("rig"):
        record["rig"] = True
    started = time.perf_counter()
    if stage["rule"] == "provenance":
        record["verdict"], record["reason"] = "pass", "recorded in provenance"
    else:
        try:
            record["verdict"], record["reason"] = RULES[stage["rule"]](ctx, stage, record)
        except LockHeld:
            raise
        except Exception as error:  # a broken stage is a failed stage, never a crashed runner
            record["verdict"], record["reason"] = "fail", f"runner error: {type(error).__name__}: {error}"
    record["seconds"] = round(time.perf_counter() - started, 3)
    if "childSeconds" in record["metrics"]:
        record["metrics"]["runnerOverheadSeconds"] = round(record["seconds"] - record["metrics"]["childSeconds"], 3)
    record["evidence"] = [evidence_entry(ctx.out, p) for p in record["evidence"] if Path(p).is_file()]
    return record


# ---------------------------------------------------------------- provenance

def provenance(catalogue, dotnet, profile_stages, price_weight):
    status = git("status", "--porcelain")
    dirty = bool(status.strip())
    diff = git("diff", "HEAD", "--binary") if dirty else b""
    source = {"commit": git("rev-parse", "HEAD").decode().strip() or None,
              "branch": git("rev-parse", "--abbrev-ref", "HEAD").decode().strip() or None,
              "dirty": dirty, "diffSha256": hashlib.sha256(diff).hexdigest() if dirty else None,
              "untrackedFiles": sum(line.startswith(b"??") for line in status.splitlines())}
    sdk = None
    if dotnet:
        try:
            sdk = subprocess.run([dotnet, "--version"], capture_output=True, text=True, check=False).stdout.strip() or None
        except OSError:
            sdk = None
    host = {"os": platform.system(), "architecture": platform.machine(), "dotnetSdk": sdk, "python": platform.python_version()}
    settings = {spec["name"]: read_source_value(spec) for spec in catalogue.get("settings", [])}
    config = ROOT / "config/persistent-city.json"
    overrides = {"FoodChoicePriceWeight": price_weight} if price_weight is not None else {}
    fixtures = [f for f in catalogue.get("fixtures", [])
                if f["id"] in profile_stages or (f["id"] == "f5h-reference" and "population" in profile_stages)]
    return {
        "source": source,
        "host": host,
        "artifact": {"persistentCityDllSha256": None},
        "fixtures": fixtures,
        "settings": {"configSha256": sha256_file(config) if config.is_file() else None,
                     "compiledDefaults": settings, "declaredOverrides": overrides},
        "capabilities": {"offline": {spec["name"]: read_source_value(spec) for spec in catalogue.get("capabilities", [])},
                         "native": None},
    }


# ---------------------------------------------------------------- verdicts

def class_verdict(records):
    if not records:
        return "not-run"
    verdicts = [r["verdict"] for r in records]
    if "fail" in verdicts:
        return "fail"
    if "not-run" in verdicts or "incomplete" in verdicts:
        return "incomplete"
    return "pass"


def summarize_result(result):
    """Rebuild classes and summary. Only semantic and native required stages decide the verdict."""
    stages = result["stages"]
    required = [s for s in stages if s.get("required") and s["class"] in FUNCTIONAL]
    native = [s for s in stages if s["class"] == "native"]
    if not any(s.get("rig") for s in native):
        required.append({"id": "rig", "class": "native", "verdict": "not-run", "reason": "rig results not merged"})
    classes = result.setdefault("classes", {})
    classes["semantic"] = {"verdict": class_verdict([s for s in required if s["class"] == "semantic"])}
    classes["native"] = {"verdict": class_verdict([s for s in required if s["class"] == "native"]),
                         "pending": [r for s in stages for r in s.get("routed", []) if r.get("class") == "native"]}
    classes.setdefault("performance", {"verdict": "not-supplied", "reference": None})
    classes["gameplay"] = {"verdict": "not-assessed"}
    failed = [s["id"] for s in required if s["verdict"] == "fail"]
    incomplete = [s["id"] for s in required if s["verdict"] in ("not-run", "incomplete")]
    verdict = "fail" if failed else "incomplete" if incomplete else "pass"
    deviations = [d for s in stages for d in s.get("deviations", [])]
    result["summary"] = {"verdict": verdict, "required": [s["id"] for s in required], "failed": failed,
                         "incomplete": incomplete, "deviations": deviations,
                         "notes": [n for s in stages for n in s.get("notes", [])]}
    return result


def write_result(out, result, sanitize):
    result = sanitize(result)
    path = out / "acceptance.json"
    path.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return path, result


def run_profile(args, catalogue):
    names = catalogue["profiles"][args.profile]
    if args.only:
        wanted = args.only.split(",")
        unknown = [n for n in wanted if n not in names]
        if unknown:
            raise SystemExit(f"--only: {', '.join(unknown)} not in profile {args.profile}")
        names = [n for n in names if n in wanted]
    by_id = {s["id"]: s for s in catalogue["stages"]}
    started = datetime.datetime.now(datetime.timezone.utc)
    dotnet = find_dotnet(args.dotnet)
    commit = git("rev-parse", "--short=8", "HEAD").decode().strip() or "nogit"
    run_id = started.strftime("%Y%m%dT%H%M%SZ") + "-" + commit
    out = Path(args.out or ROOT / "build/acceptance" / run_id).resolve()
    out.mkdir(parents=True, exist_ok=True)
    managed = Path(args.game_managed).resolve() if args.game_managed else None
    prov = provenance(catalogue, dotnet, names, args.price_weight)
    ctx = Context(out, dotnet, managed, args.price_weight, args.no_wait, prov["settings"]["compiledDefaults"])
    records = []
    for name in names:
        print(f"[{name}] ...", flush=True)
        record = run_stage(ctx, by_id[name])
        records.append(record)
        print(f"[{name}] {record['verdict']} in {record['seconds']:.1f} s: {record['reason']}", flush=True)
    prov["artifact"]["persistentCityDllSha256"] = ctx.artifact
    finished = datetime.datetime.now(datetime.timezone.utc)
    result = {"schema": SCHEMA, "runId": run_id, "profile": args.profile, "onlyStages": names if args.only else None,
              "startedUtc": started.isoformat(timespec="seconds"), "finishedUtc": finished.isoformat(timespec="seconds"),
              "provenance": prov, "stages": records, "classes": {},
              "nativeEvidence": {"status": "not-implemented", "owner": "F9d"}}
    summarize_result(result)
    return write_result(out, result, Sanitizer(out, managed))


def merge(args):
    """summarize: merge rig stage records and F4's performance record into a run result."""
    source = Path(args.result).resolve()
    result = json.loads(source.read_text(encoding="utf-8"))
    if result.get("schema") != SCHEMA:
        raise SystemExit(f"{source.name} is not {SCHEMA}")
    out = Path(args.out).resolve() if args.out else source.parent
    out.mkdir(parents=True, exist_ok=True)
    for rig_file in args.rig or []:
        rig = json.loads(Path(rig_file).read_text(encoding="utf-8"))
        if rig.get("schema") != SCHEMA:
            raise SystemExit(f"{Path(rig_file).name} is not {SCHEMA}")
        for stage in rig.get("stages", []):
            if stage.get("class") != "native" or stage.get("verdict") not in VERDICTS:
                continue
            stage = dict(stage, rig=True, required=True)
            result["stages"] = [s for s in result["stages"] if s["id"] != stage["id"]] + [stage]
    if args.performance:
        record = json.loads(Path(args.performance).read_text(encoding="utf-8"))
        result.setdefault("classes", {})["performance"] = {"verdict": record.get("verdict", "not-supplied"),
                                                           "reference": record.get("reference")}
    result["mergedFrom"] = result.get("runId")
    result["profile"] = "summarize"
    summarize_result(result)
    return write_result(out, result, Sanitizer(out))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("profile", choices=["quick", "full", "summarize"])
    parser.add_argument("--out", help="result directory (default build/acceptance/<runId>)")
    parser.add_argument("--catalogue", default=str(HERE / "stages.json"))
    parser.add_argument("--dotnet", help=".NET SDK host (default: PATH, then %%LOCALAPPDATA%%/Microsoft/dotnet on Windows)")
    parser.add_argument("--game-managed", help="game Managed directory; without it the build stage is not-run")
    parser.add_argument("--price-weight", type=float, help="declared FoodChoicePriceWeight override (a deviation; never a pass)")
    parser.add_argument("--no-wait", action="store_true", help="refuse instead of waiting for other test processes")
    parser.add_argument("--only", help="comma-separated subset of the profile's stages (a partial run; unlisted stages are absent)")
    parser.add_argument("--strict", action="store_true", help="exit 3 when the verdict is incomplete")
    parser.add_argument("--lock", help="lock file (default <temp>/" + LOCK_NAME + ")")
    parser.add_argument("--result", help="summarize: the run's acceptance.json")
    parser.add_argument("--rig", action="append", help="summarize: rig results in the same schema (class native)")
    parser.add_argument("--performance", help="summarize: F4 performance record")
    args = parser.parse_args(argv)
    if args.profile == "summarize" and not args.result:
        parser.error("summarize needs --result")
    catalogue = json.loads(Path(args.catalogue).read_text(encoding="utf-8"))
    try:
        with RunnerLock(args.lock) as lock:
            if lock.cleared_stale is not None:
                print("cleared a stale acceptance lock", flush=True)
            path, result = merge(args) if args.profile == "summarize" else run_profile(args, catalogue)
    except LockHeld as error:
        print("refused:", error, file=sys.stderr)
        return 4
    summary = result["summary"]
    print(f"summary: {summary['verdict']} (failed {summary['failed']}, incomplete {summary['incomplete']}); "
          f"performance {result['classes']['performance']['verdict']}; gameplay not-assessed")
    print("result:", path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else path.name,
          "sha256", sha256_file(path))
    if summary["verdict"] == "fail":
        return 1
    if summary["verdict"] == "incomplete" and args.strict:
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())
