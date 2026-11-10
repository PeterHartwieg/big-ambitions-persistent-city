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
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        handle = kernel.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not handle:
            return ctypes.get_last_error() == 5  # access denied: the process exists
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
    """Machine-wide lock in the OS temp directory (section 3.9). Exclusivity is an OS lock on one byte of the
    lock file, which the kernel drops when the holder exits, so a crashed runner leaves no live lock. The file
    holds the PID and checkout for messages only; unreadable or empty metadata never decides ownership."""

    OFFSET = 4096  # the locked byte lies past the metadata, so other runners can still read who holds it

    def __init__(self, path=None, checkout=ROOT):
        self.path = Path(path or os.environ.get("PC_ACCEPTANCE_LOCK") or Path(tempfile.gettempdir()) / LOCK_NAME)
        self.checkout = str(checkout)
        self.cleared_stale = None
        self.fd = None

    def _lock(self):
        fd = os.open(self.path, os.O_RDWR | os.O_CREAT, 0o644)
        try:
            if os.name == "nt":
                import msvcrt
                os.lseek(fd, self.OFFSET, os.SEEK_SET)
                msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            os.close(fd)
            holder = self._holder()
            raise LockHeld(f"another acceptance runner holds {self.path.name} (pid {holder.get('pid', 'unknown')})")
        self.fd = fd

    def _write(self):
        previous = self._holder()
        if previous.get("pid") not in (None, os.getpid()):
            self.cleared_stale = previous  # its owner is gone: we hold the OS lock
        os.lseek(self.fd, 0, os.SEEK_SET)
        os.write(self.fd, json.dumps({"pid": os.getpid(), "checkout": self.checkout}).encode().ljust(512))

    def acquire(self):
        self._lock()
        self._write()
        return self

    def _holder(self):
        try:
            fd = os.open(self.path, os.O_RDONLY | getattr(os, "O_BINARY", 0))
            try:
                raw = os.read(fd, 512)  # unbuffered: a buffered read would touch the locked byte on Windows
            finally:
                os.close(fd)
            return json.loads(raw.decode("utf-8").strip() or "{}")
        except (OSError, ValueError):
            return {}

    def release(self):
        if self.fd is None:
            return
        # The file stays: unlinking it would let a third runner lock a new file while a second holds the old one.
        os.lseek(self.fd, 0, os.SEEK_SET)
        os.write(self.fd, b" " * 512)
        if os.name == "nt":
            import msvcrt
            os.lseek(self.fd, self.OFFSET, os.SEEK_SET)
            msvcrt.locking(self.fd, msvcrt.LK_UNLCK, 1)
        os.close(self.fd)  # also drops the POSIX flock
        self.fd = None

    def __enter__(self):
        return self.acquire()

    def __exit__(self, *exc):
        self.release()


TEST_PROBE = ("powershell", "-NoProfile", "-NonInteractive", "-Command",
              "Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe' or Name like 'testhost%'\" | "
              "ForEach-Object { [string]$_.ProcessId + [char]9 + $_.Name + [char]9 + $_.CommandLine }")


def busy_test_processes(rows, own=None):
    """Rows of (pid, name, command line). A testhost, or a dotnet host running `test`/vstest at any phase
    (restore, build or run), is a test run."""
    own = os.getpid() if own is None else own
    busy = []
    for pid, name, command in rows:
        if str(pid) == str(own):
            continue
        words = re.findall(r'"[^"]*"|\S+', command or "")
        args = [w.strip('"').lower() for w in words[1:]]
        lowered = (name or "").lower()
        if lowered.startswith("testhost") or "test" in args or any("vstest" in a or "testhost" in a for a in args):
            busy.append(f"{name} {pid}")
    return busy


def other_test_processes():
    """Running testhost / `dotnet test` processes on this machine (CIM on Windows, ps elsewhere)."""
    try:
        if os.name == "nt":
            text = subprocess.run(TEST_PROBE, capture_output=True, text=True, check=False).stdout
            rows = [line.split("	", 2) for line in text.splitlines() if line.count("	") >= 2]
        else:
            text = subprocess.run(["ps", "-A", "-o", "pid=,comm=,args="], capture_output=True, text=True, check=False).stdout
            rows = []
            for line in text.splitlines():
                parts = line.split(None, 2)
                if len(parts) == 3 and ("dotnet" in parts[1] or "testhost" in parts[1]):
                    rows.append(parts)
        return busy_test_processes(rows)
    except OSError:
        return []


def wait_for_dotnet(no_wait, poll=5.0, probe=other_test_processes):
    """Blocks while other test processes run; returns the seconds spent waiting (not runner cost)."""
    started = time.perf_counter()
    hook = os.environ.get("PC_DOTNET_WAIT")
    if hook:
        subprocess.run(hook, shell=True, check=False)
    announced = False
    while True:
        busy = probe()
        if not busy:
            return time.perf_counter() - started if announced or hook else 0.0
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
        self.game = None  # F9b: the build's fingerprint record (build.py --provenance)
        self.tests_built = None  # None: no test build in this run; True/False: its outcome

    def env(self):
        env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", UseSharedCompilation="false", DOTNET_CLI_TELEMETRY_OPTOUT="1",
                   DOTNET_NOLOGO="1")
        if self.dotnet and Path(self.dotnet).parent != Path("."):
            # The selected SDK host comes first on PATH, so tools/build.py's own discovery finds the same SDK.
            env["PATH"] = str(Path(self.dotnet).resolve().parent) + os.pathsep + env.get("PATH", "")
        return env

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


def stage_steps(stage, built=None):
    """A filter stage reuses this run's test build (--no-build) when there was one, else builds itself."""
    if "filter" in stage and not stage.get("steps"):
        trx = f"trx/{stage['id']}/{stage['id']}.trx"
        return [["{dotnet}", "test", "tests/PersistentCity.Tests.csproj"] + (["--no-build"] if built else [])
                + ["--filter", stage["filter"], "--logger", f"trx;LogFileName={stage['id']}.trx",
                   "--results-directory", "{out}/trx/" + stage["id"]]], trx
    return stage.get("steps", []), stage.get("trx")


def stage_outputs(stage):
    """Files and directories under {out} a stage writes; removed before it runs so nothing stale is read."""
    _, trx = stage_steps(stage)
    owned = [stage.get("acceptance"), stage.get("output"), stage.get("provenance"), trx and str(Path(trx).parent),
             "logs/" + stage["id"] + ".log"]
    return [o for o in owned + stage.get("owns", []) if o]


def clean_outputs(out, stage):
    for relative in stage_outputs(stage):
        path = (out / relative).resolve()
        if path == out or out not in path.parents:
            raise ValueError(f"stage {stage['id']} output {relative} is outside the run directory")
        if path.is_dir():
            shutil.rmtree(path)
        elif path.exists():
            path.unlink()


def run_steps(ctx, stage, steps, record):
    """Run each step in the foreground with output to logs/<id>.log; returns the exit codes."""
    log_path = ctx.out / "logs" / (stage["id"] + ".log")
    log_path.parent.mkdir(parents=True, exist_ok=True)
    env = ctx.env()
    codes = []
    child = waited = 0.0
    with open(log_path, "wb") as log:
        for number, argv in enumerate(steps):
            # One probe per stage: our own steps run back to back, so a later step needs no second probe.
            if stage.get("dotnet") and number == 0:
                waited += wait_for_dotnet(ctx.no_wait)
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
    record["metrics"]["waitSeconds"] = round(waited, 3)
    record["evidence"].append(log_path)
    return codes, log_path


def rule_trx(ctx, stage, record):
    terms = filter_terms(stage["filter"]) if "filter" in stage else None
    if "filter" in stage and terms is not None and ctx.unit_trx is not None:
        results = apply_filter(parse_trx(ctx.unit_trx), terms)
        record["reusedFrom"] = "unit"
        record["argv"] = ["(reused unit TRX)", "--filter", stage["filter"]]
        return trx_verdict(record, results, 0, "")
    steps, trx = stage_steps(stage, ctx.tests_built)
    record["argv"] = steps[0] if len(steps) == 1 else steps
    if "filter" in stage and ctx.tests_built is False:
        return "fail", "the test build of this run failed; not run against older binaries"
    codes, log_path = run_steps(ctx, stage, steps, record)
    code = codes[-1] if codes else 0
    record["exit"] = code
    trx_path = ctx.out / trx
    log = log_path.read_text(encoding="utf-8", errors="replace")
    if stage.get("buildsTests"):
        ctx.tests_built = trx_path.is_file()
    if not trx_path.is_file():
        if NO_MATCH in log:
            return "not-run", "filter matched no tests"
        if code == 0:
            return "not-run", "no TRX written"
        return "fail", f"exit {code} and no TRX written (build or host failure; see logs/{stage['id']}.log)"
    record["evidence"].append(trx_path)
    results = parse_trx(trx_path)
    if stage["id"] == "unit":
        ctx.unit_trx = trx_path
    return trx_verdict(record, results, code, log)


NO_MATCH = "No test matches the given testcase filter"


def trx_verdict(record, results, code, log):
    counts = count(results)
    record["metrics"].update(counts)
    if counts["failed"]:
        return "fail", f"{counts['failed']} failed"
    if counts["passed"] == 0:
        if code != 0 and NO_MATCH not in log:
            return "fail", f"exit {code} with no test result (test host crash or abort)"
        return "not-run", "filter matched no tests"
    if code != 0:
        return "fail", f"exit {code} with {counts['passed']} passed"
    return "pass", f"{counts['passed']} passed, {counts['skipped']} skipped"


def rule_exit_zero(ctx, stage, record):
    steps = stage["steps"]
    record["argv"] = steps[0] if len(steps) == 1 else steps
    codes, _ = run_steps(ctx, stage, steps, record)
    record["exit"] = codes[-1] if codes else 0
    ok = all(c == 0 for c in codes)
    if stage.get("buildsTests"):
        ctx.tests_built = ok
    return ("pass", "exit 0") if ok else ("fail", f"exit {record['exit']}")


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
    # F9b (Decision 4): the fingerprint set of the supplied assemblies. "unknown" leaves the build passing; the summary
    # then holds native incomplete (summarize_result).
    record_path = ctx.out / stage["provenance"] if stage.get("provenance") else None
    if record_path is not None and record_path.is_file():
        record["evidence"].append(record_path)
        game = json.loads(record_path.read_text(encoding="utf-8"))
        ctx.game = {"set": game.get("set"), "differing": game.get("differing", []), "assemblies": game.get("assemblies", {})}
        record["digests"].update(ctx.game["assemblies"])
        record["metrics"]["gameFingerprintSet"] = ctx.game["set"]
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
    if stage["rule"] != "provenance":
        clean_outputs(ctx.out, stage)
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
        record["metrics"]["runnerOverheadSeconds"] = round(
            record["seconds"] - record["metrics"]["childSeconds"] - record["metrics"]["waitSeconds"], 3)
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
        "game": {"supplied": False, "managed": None, "set": None, "differing": [], "assemblies": {}},
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
    # F9b (Decision 4): assemblies outside every declared fingerprint set leave native incomplete, never failed.
    game = (result.get("provenance") or {}).get("game") or {}
    if game.get("supplied") and game.get("set") in ("unknown", None):
        reason = ("game assemblies match no declared set; differing: " + ", ".join(game.get("differing") or [])
                  if game.get("set") == "unknown" else "no game fingerprint recorded")
        required.append({"id": "game-fingerprint", "class": "native", "verdict": "incomplete", "reason": reason})
    # Native rows routed from the economy stage stay incomplete until a native record that names the same row
    # (`measures`) reports; an unrelated rig pass never clears them (Decision 2).
    pending = []
    for row in [r for s in stages for r in s.get("routed", []) if r.get("class") == "native"]:
        measured = [s for s in native if s.get("measures") == row["row"]]
        verdict = class_verdict(measured) if measured else row.get("verdict", "incomplete")
        pending.append(dict(row, verdict=verdict, measuredBy=[s["id"] for s in measured]))
        if verdict != "pass":
            required.append({"id": row["row"], "class": "native", "verdict": verdict,
                             "reason": "awaiting " + row.get("proof", "a native measurement")})
    classes = result.setdefault("classes", {})
    classes["semantic"] = {"verdict": class_verdict([s for s in required if s["class"] == "semantic"])}
    classes["native"] = {"verdict": class_verdict([s for s in required if s["class"] == "native"]), "pending": pending}
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
    names = profile = catalogue["profiles"][args.profile]
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
    for name in profile:
        if name not in names:
            stage = by_id[name]
            records.append({"id": name, "class": stage["class"], "required": stage.get("required", stage["class"] in FUNCTIONAL),
                            "argv": [], "exit": None, "seconds": 0.0, "verdict": "not-run", "reason": "omitted by --only",
                            "metrics": {}, "digests": {}, "evidence": []})
            continue
        print(f"[{name}] ...", flush=True)
        record = run_stage(ctx, by_id[name])
        records.append(record)
        print(f"[{name}] {record['verdict']} in {record['seconds']:.1f} s: {record['reason']}", flush=True)
    prov["artifact"]["persistentCityDllSha256"] = ctx.artifact
    if managed is not None and "build" in names:
        prov["game"] = dict(prov["game"], supplied=True, managed="<supplied>", **(ctx.game or {}))
    finished = datetime.datetime.now(datetime.timezone.utc)
    result = {"schema": SCHEMA, "runId": run_id, "profile": args.profile, "onlyStages": names if args.only else None,
              "startedUtc": started.isoformat(timespec="seconds"), "finishedUtc": finished.isoformat(timespec="seconds"),
              "provenance": prov, "stages": records, "classes": {},
              "nativeEvidence": {"status": "not-implemented", "owner": "F9d"}}
    summarize_result(result)
    return write_result(out, result, Sanitizer(out, managed))


def source_identity(result):
    source = (result.get("provenance") or {}).get("source") or {}
    return {"commit": source.get("commit"), "dirty": source.get("dirty"), "diffSha256": source.get("diffSha256")}


def artifact_hash(result):
    return ((result.get("provenance") or {}).get("artifact") or {}).get("persistentCityDllSha256")


def identity_problem(result, rig):
    """Why rig evidence does not belong to this run's source and artifact, or None when it does."""
    identity, rig_identity = source_identity(result), source_identity(rig)
    if identity["commit"] is None or rig_identity != identity:
        return f"rig source {rig_identity} is not the offline run's source {identity}"
    ours, theirs = artifact_hash(result), artifact_hash(rig)
    if ours and theirs and ours != theirs:
        return f"rig PersistentCity.dll {theirs} is not the offline run's {ours}"
    return None


def package_evidence(stage, src_base, out, prefix):
    """Copy a stage's evidence into the merged run directory and rebase its paths. Returns the problems found."""
    problems, entries = [], []
    src_base = src_base.resolve()
    for entry in stage.get("evidence", []):
        src = (src_base / entry["path"]).resolve()
        target = (Path(prefix) / entry["path"]).as_posix() if prefix else entry["path"]
        dst = (out / target).resolve()
        if src_base not in src.parents or out not in dst.parents:
            problems.append(f"evidence {entry['path']} escapes its run directory")
            continue
        if not src.is_file() or sha256_file(src) != entry.get("sha256"):
            problems.append(f"evidence {entry['path']} is missing or does not match its hash")
            continue
        if src != dst:
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
        entries.append(dict(entry, path=target))
    stage["evidence"] = entries
    return problems


def demote(stage, problems):
    if problems and stage["verdict"] in ("pass", "expected-red"):
        stage["verdict"], stage["reason"] = "incomplete", "; ".join(problems)


def merge(args):
    """summarize: merge rig stage records and F4's performance record into a run result.

    Rig records count only when their provenance names the same source (commit, dirty flag and diff hash) and,
    when both record one, the same PersistentCity.dll hash; otherwise they are incomplete whatever their verdict. Every evidence file is copied under the merged run directory."""
    source = Path(args.result).resolve()
    result = json.loads(source.read_text(encoding="utf-8"))
    if result.get("schema") != SCHEMA:
        raise SystemExit(f"{source.name} is not {SCHEMA}")
    out = Path(args.out).resolve() if args.out else source.parent
    out.mkdir(parents=True, exist_ok=True)
    for stage in result["stages"]:
        demote(stage, package_evidence(stage, source.parent, out, ""))
    imported = result.setdefault("importedEvidence", [])
    for rig_file in args.rig or []:
        rig_path = Path(rig_file).resolve()
        rig = json.loads(rig_path.read_text(encoding="utf-8"))
        if rig.get("schema") != SCHEMA:
            raise SystemExit(f"{rig_path.name} is not {SCHEMA}")
        mismatch = identity_problem(result, rig)
        # Prefixes are numbered after every earlier import, also from earlier summarize runs, so no copy is overwritten.
        used = {entry.get("prefix") for entry in imported}
        index = len(imported)
        while f"rig/{index}-{rig_path.stem}" in used or (out / f"rig/{index}-{rig_path.stem}").exists():
            index += 1
        prefix = f"rig/{index}-{rig_path.stem}"
        imported.append({"file": rig_path.name, "sha256": sha256_file(rig_path), "prefix": prefix,
                         "provenance": rig.get("provenance"), "sameSource": mismatch is None, "stages": []})
        for stage in rig.get("stages", []):
            if stage.get("class") != "native" or stage.get("verdict") not in VERDICTS:
                continue
            stage = dict(stage, rig=True, required=True, importedFrom=prefix)
            imported[-1]["stages"].append({"id": stage["id"], "verdict": stage["verdict"], "reason": stage.get("reason", "")})
            problems = package_evidence(stage, rig_path.parent, out, prefix)
            if mismatch:
                # Foreign evidence neither passes nor fails this run; its own verdict stays in importedEvidence.
                stage["verdict"], stage["reason"] = "incomplete", "; ".join([mismatch] + problems)
            demote(stage, problems)
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
    parser.add_argument("--only", help="comma-separated subset of the profile's stages (a partial run; the others are recorded not-run)")
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
