"""Print the F7p markdown tables from docs/perf/assets/f7p/matrix.jsonl (no measurement, no dotnet)."""
import json
import pathlib

root = pathlib.Path(__file__).resolve().parents[2]
rows = [json.loads(l)["result"] for l in open(root / "docs" / "perf" / "assets" / "f7p" / "matrix.jsonl", encoding="utf-8")]
order = ["representative-957x2", "representative-5000x2", "representative-50000x2",
         "957x2", "957x3", "5000x2", "5000x3", "churn", "50000x2", "50000x3", "CF32", "CF64", "encoding-max"]
by = {(r["fixture"], r["candidate"]): r for r in rows}
fixtures = [f for f in order if (f, "A") in by]
mib = 1024 * 1024


def n(v):
    return f"{v:,}"


def ms(s):
    return f"{s['p50']:,.1f} / {s['p95']:,.1f} / {s['max']:,.1f} ({s['atLeast30Ms']})"


print("### Size\n")
print("| Fixture | A chars | B chars (÷A) | C chars (÷A) | D chars (÷A) | UTF-8 = chars | gzip(UTF-16) A / B / C / D bytes |")
print("| --- | ---: | ---: | ---: | ---: | --- | ---: |")
for f in fixtures:
    a, b, c, d = (by[(f, x)] for x in "ABCD")
    ascii_ = all(x["payload"]["characters"] == x["payload"]["utf8Bytes"] for x in (a, b, c, d))
    cell = lambda x: f"{n(x['payload']['characters'])} ({x['payload']['characterRatioToWriter25']:.4f})"
    print(f"| {f} | {n(a['payload']['characters'])} | {cell(b)} | {cell(c)} | {cell(d)} | "
          f"{'yes' if ascii_ else 'no: A ' + n(a['payload']['utf8Bytes']) + ' / C ' + n(c['payload']['utf8Bytes'])} | "
          f"{n(a['payload']['gzipUtf16Bytes'])} / {n(b['payload']['gzipUtf16Bytes'])} / {n(c['payload']['gzipUtf16Bytes'])} / {n(d['payload']['gzipUtf16Bytes'])} |")

print("\n### Records, tables and round-trip\n")
print("| Fixture | Semantic records (A) | C table entries (strings / keys / shapes / routes) | C records vs 4,000,000 cap | Fields compared | Differences | Writer-25 bytes after decode | Decode -> re-encode identical |")
print("| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |")
for f in fixtures:
    a, c = by[(f, "A")], by[(f, "C")]
    t = c["payload"]["tables"]
    ok = all(by[(f, x)]["roundTrip"]["writer25Identical"] for x in "ABCD")
    re = all(by[(f, x)]["roundTrip"]["reencodeIdentical"] for x in "ABCD")
    diffs = sum(by[(f, x)]["roundTrip"]["differences"] for x in "ABCD")
    print(f"| {f} | {n(a['source']['semanticRecords'])} | {n(t['entries'])} ({n(t['strings'])} / {t['keys']} / {t['shapes']} / {t['routes']}) | "
          f"{n(c['payload']['recordsAgainstCap'])} | {n(a['roundTrip']['fieldsCompared'])} × 4 | {diffs} | {'identical, all 4' if ok else 'NO'} | {'yes, all 4' if re else 'NO'} |")

print("\n### Encode (ms p50 / p95 / max (samples >= 30 ms of 7); allocated bytes median)\n")
print("| Fixture | A | B | C | D | C table pass / column writer p50 | Allocated A / B / C / D | C alloc ÷ C UTF-16 bytes |")
print("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
for f in fixtures:
    r = [by[(f, x)] for x in "ABCD"]
    c = r[2]
    split = c["encodeSplitMs"]
    alloc = " / ".join(n(x["encodeAllocatedBytes"]["median"]) for x in r)
    print(f"| {f} | " + " | ".join(ms(x["encodeMs"]) for x in r)
          + f" | {split['tablePass']['p50']:,.1f} / {split['columnWriter']['p50']:,.1f} | {alloc} | {c['encodeAllocatedBytes']['median'] / c['payload']['utf16StorageBytes']:.2f} |")

print("\n### Decode and memory\n")
print("| Fixture | Decode A | Decode B | Decode C | Decode D | Decode allocated A / C | Retained MiB A / B / C / D | Distinct string instances A / C | Peak working set MiB A / B / C / D |")
print("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
for f in fixtures:
    r = [by[(f, x)] for x in "ABCD"]
    print(f"| {f} | " + " | ".join(ms(x["decodeMs"]) for x in r)
          + f" | {n(r[0]['decodeAllocatedBytes']['median'])} / {n(r[2]['decodeAllocatedBytes']['median'])}"
          + " | " + " / ".join(f"{x['retainedBytesAfterDecode'] / mib:,.1f}" for x in r)
          + f" | {n(r[0]['decodedStrings']['distinctInstances'])} / {n(r[2]['decodedStrings']['distinctInstances'])}"
          + " | " + " / ".join(f"{x['processPeakWorkingSetBytes'] // mib:,}" for x in r) + " |")

print("\n### Format-independent phases (measured once per fixture in candidate A's process; ms p50 / p95 / max (>= 30 ms of 7))\n")
print("| Fixture | Synchronize (F7a recorder) | Validate (F7a recorder) | Capture proxy: detached copy | Copy allocated B | Materialize upper bound (strict writer-25 read) | Activation validate | Simulator construction |")
print("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
for f in fixtures:
    c = by[(f, "A")]["formatIndependentPhases"]
    print(f"| {f} | {ms(c['synchronizeMs'])} | {ms(c['validateMs'])} | {ms(c['captureDetachedCopyMs'])} | {n(c['captureDetachedCopyAllocatedBytes']['median'])} | "
          f"{ms(c['materializeUpperBoundMs'])} | {ms(c['activationValidateMs'])} | {ms(c['simulatorConstructionMs'])} |")

print("\n### Composed synchronous save pause and load per candidate (ms; sum of phase p50s, and sum of phase p95s as an upper bound)\n")
print("Pause = synchronize + capture proxy + validate + encode (publish is about 0.005 ms, F7a). Load = decode + materialize upper bound + activation validate + construction; the materialize term re-parses writer-25 text, so load is an upper bound.\n")
print("| Fixture | Pause A | Pause B | Pause C | Pause D | Load A | Load B | Load C | Load D |")
print("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
def composed(f, x, q):
    c = by[(f, "A")]["formatIndependentPhases"]; r = by[(f, x)]
    pause = c["synchronizeMs"][q] + c["captureDetachedCopyMs"][q] + c["validateMs"][q] + r["encodeMs"][q]
    load = r["decodeMs"][q] + c["materializeUpperBoundMs"][q] + c["activationValidateMs"][q] + c["simulatorConstructionMs"][q]
    return pause, load
for f in fixtures:
    cells = [composed(f, x, "p50") + composed(f, x, "p95") for x in "ABCD"]
    print(f"| {f} | " + " | ".join(f"{v[0]:,.1f} / {v[2]:,.1f}" for v in cells) + " | " + " | ".join(f"{v[1]:,.1f} / {v[3]:,.1f}" for v in cells) + " |")

for f in ["representative-957x2", "50000x2"]:
    a, c = by[(f, "A")], by[(f, "C")]
    print(f"\n### Characters per field family, {f} (A vs C; families >= 0.5 % of A)\n")
    print("| Path | A | C | C ÷ A |")
    print("| --- | ---: | ---: | ---: |")
    paths = sorted(set(a["pathCharacters"]) | set(c["pathCharacters"]))
    for p in paths:
        if p.count(".") + p.count("[") > 3:
            continue
        va, vc = a["pathCharacters"].get(p), c["pathCharacters"].get(p)
        ratio = f"{vc / va:.3f}" if va and vc else ""
        print(f"| `{p}` | {n(va) if va else '<0.5 %'} | {n(vc) if vc else '<0.5 %'} | {ratio} |")
