"""Complete a missing archived stress baseline, then publish metrics-only E5d evidence."""
from pathlib import Path
import json
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
data = json.loads((root / "build/e5d/measurements/results.json").read_text(encoding="utf-8"))
if "CF64-limits-baseline" not in data:
    print("Completing the actual archived 20,000-place baseline before publication (foreground).", flush=True)
    subprocess.run([sys.executable, "tools/name-identity-regression/measure.py", "--baseline-only", "CF64-limits"], cwd=root, check=True)
    data = json.loads((root / "build/e5d/measurements/results.json").read_text(encoding="utf-8"))
cases = ["957x2", "957x3", "5000x2", "5000x3", "50000x2", "50000x3", "CF32", "CF64"]
before = {name: data[f"{name}-baseline"]["result"]["before"] for name in cases}
after = {name: data[f"{name}-candidate"]["result"] for name in cases}
for name in ("CF32", "CF64"):
    assert after[name]["scopes"] == 1024, "Full-scope CF audit is required before reporting."
    assert after[name]["places"] == 14000, "CF must stay within its declared shared-growth allocation."
    c = after[name]["composition"]
    assert sum(c[k] for k in ("existingPlaceGrowthChars", "existingNameGrowthChars", "existingScopeGrowthChars", "existingHistoryRowGrowthChars")) == 12582912
stress = data['CF64-limits-candidate']['result']
stress_before = data['CF64-limits-baseline']['result']
for field in ('chars', 'bytes', 'Streaming', 'Semantic'):
    assert stress_before['before'][field] == stress['before'][field], f'20,000-place frozen-24 mismatch: {field}'
assert stress_before['sha256'] == stress['beforeSha256'], '20,000-place source identity mismatch'
production_trees = {name: subprocess.check_output(['git', 'rev-parse', f'HEAD:{name}'], cwd=root, text=True).strip()
                    for name in ('Core', 'Compatibility', 'Persistence', 'Runtime')}
for head in {r['sourceHead'] for r in data.values() if 'sourceHead' in r}:
    for name, tree in production_trees.items():
        assert subprocess.check_output(['git', 'rev-parse', f'{head}:{name}'], cwd=root, text=True).strip() == tree, 'Production source changed during measurement'

def number(n):
    return f"{n:,}"

def triple(v):
    return "/".join(f"{v[k]:.4f}" for k in ("p50", "p95", "max"))

text = "\n## Final matched results\n\nAll figures below are offline .NET 10, seed 11. Bytes equal characters in the ASCII matrix. Baseline latency/memory is the **actual archived writer-24 model**, not the slightly larger current Core model running the frozen reader. Frozen 24 bytes/hashes and both counters match that actual base in every arm.\n\n"
text += "| Fixture | Bytes 24 → 25 | Streaming / semantic 24 → 25 | Retained MiB 24 → 25 |\n| --- | ---: | ---: | ---: |\n"
for name in cases:
    a, b = before[name], after[name]["after"]
    text += f"| {name} | {number(a['bytes'])} → {number(b['bytes'])} | {number(a['Streaming'])}/{number(a['Semantic'])} → {number(b['Streaming'])}/{number(b['Semantic'])} | {a['retainedBytes']/1048576:.3f} → {b['retainedBytes']/1048576:.3f} |\n"
text += "\nThe normal matrix uses 32 scopes covering all 256 names. CF materializes all **1,024 scopes × eight IDs** and charges both scope growth and exact history row structure to the same 12 Mi-character existing-state allowance by reducing only synthetic X padding. Only measured cell bytes consume the E4 cell allowance; its unspent bytes remain reserved. No second allowance is added. Each ordinary migration adds **6,138 bytes and 280 records** (256 names + 24 tuples); full maximum-held cold storage is separate. The provenance bit adds bounded per-history-cell managed memory, included above; it is not serialized.\n\n"
text += "| Fixture | Write 24 → 25, p50/p95/max ms | Read 24 → 25, p50/p95/max ms | Activation 24 → 25, p50/p95/max ms |\n| --- | ---: | ---: | ---: |\n"
for name in cases:
    a, b = before[name]["timing"], after[name]["after"]["timing"]
    text += "| " + name + " | " + " | ".join(f"{triple(a[k])} → {triple(b[k])}" for k in ("write", "read", "activation")) + " |\n"
text += "\n| Fixture | Early header 24 → 25, p50/p95/max ms | Cold full migration ms |\n| --- | ---: | ---: |\n"
for name in cases:
    text += f"| {name} | {triple(before[name]['timing']['early'])} → {triple(after[name]['after']['timing']['early'])} | {after[name]['migrationMs']:.4f} |\n"
text += "\nActivation means full validation plus counting serialization, not native adapter startup. Early-header timings measure the writer's header-first order; arbitrary older property orders remain readable but need not have constant header latency. Native activation/setup/route cost is unmeasured.\n\n"
text += "| Full CF cold maximum | Actual bytes / chars | Streaming / semantic | Retained MiB | Budgeted bytes / chars | Budgeted records |\n| --- | ---: | ---: | ---: | ---: | ---: |\n"
for name in ("CF32", "CF64"):
    r = after[name]; m, c = r["maximumHeld"], r["composition"]
    text += f"| {name}, 64 offers | {number(m['bytes'])} | {number(m['Streaming'])}/{number(m['Semantic'])} | {m['retainedBytes']/1048576:.3f} | {number(c['totalBytes'])} | {number(c['totalRecords'])} |\n"
    text += ""
text += "\nThe complete CF composition includes every outstanding reserve, including the entire extra 540,672 bytes/chars and 320 records on top of the actual image. CF has **14,000 places**, the full 20,000 business references, full histories and 1,024 scopes within one shared 12 Mi existing-state allowance. The separate `CF64-limits` stress exercises all 20,000 places beyond that allowance, without future-reserve clearance; the place limit remains unchanged. CF32 passes the 85% target; CF64 passes hard caps only. These are reproducible specified compositions, not clearance for every simultaneous Unicode, opaque-history and churn maximum. Each cold image round-trips; its early/full execution attempts **refuse**. Cold p50/p95/max, allocations and refusal latencies are in the linked JSON.\n\n"
for name in ("CF32", "CF64"):
    c = after[name]["composition"]
    text += f"- {name}: existing growth {number(c['existingPlaceGrowthChars'])} place + {number(c['existingScopeGrowthChars'])} scope + {number(c['existingHistoryRowGrowthChars'])} history row structure + {number(c['existingNameGrowthChars'])} name = **{number(c['existingGrowthChars'])}** chars; remaining B–E reserve **{number(c['reservedChars'])} chars / {number(c['reservedRecords'])} records**; memory-field headroom **{number(c['memoryHeadroom'])}** chars.\n"
encoding = data["encoding-candidate"]["result"]
timing = encoding["timing"]["read"]
saved = 8285000 - timing["allocatedBytes"]
text += f"\nFinal encoding win: **8,285,000 → {number(timing['allocatedBytes'])} B/read**, saving **{number(saved)} B ({saved/8285000:.1%})**. Read p50/p95/max **30.0958/33.0111/33.0111 → {triple(timing)} ms**. These shared-PC sequential samples establish an allocation win, not a causal CPU/native-gate claim. Before/final ≥30 ms reads: **4/7 → {timing['spikes30']}/7**. Final write allocation is **{number(encoding['timing']['write']['allocatedBytes'])} B/call**.\n\n"
text += f"Matched allocation fixture: **256 × 256 UTF-16 units** (253 non-ASCII units and a three-digit suffix per name), canonical save **{number(encoding['chars'])} chars / {number(encoding['bytes'])} UTF-8 B**; escaped spelling **{number(encoding['escapedChars'])} chars/B**, table including field overhead **{number(encoding['escapedTableChars'])} chars/B**. Counts **{encoding['counts']['Item1']}/{encoding['counts']['Item2']}** retain the legacy scope-string gap. Both spellings round-trip to identical names/output.\n\n"
maximum = data['encoding-max-candidate']['result']
text += f"Separate true maximum-escaping fixture: **256 unique names, each 256 non-ASCII UTF-16 units**, all six-character JSON escapes; canonical save **{number(maximum['chars'])} chars / {number(maximum['bytes'])} UTF-8 B**, escaped save **{number(maximum['escapedChars'])} chars/B**, escaped table including field overhead **{number(maximum['escapedTableChars'])} chars/B**. Both representations pass and round-trip identically. This is a distinct bound fixture, not the matched pre-win allocation sample. Its complete timings, allocations and spike counts are in the JSON.\n\n"
churn = data["churn-candidate"]["result"]
text += f"Churn: **{number(churn['rows'])} rows / {number(churn['cells'])} cells**, 2,048 generations/day; **{number(churn['before']['bytes'])} → {number(churn['after']['bytes'])} B**, counters **{number(churn['after']['Streaming'])}/{number(churn['after']['Semantic'])}**. All retained generations survive; no hidden 1,024/day cap.\n\n"
text += "| Separate 20,000-place stress | Bytes 24 → 25 | Streaming / semantic 24 → 25 | Retained MiB 24 → 25 |\n| --- | ---: | ---: | ---: |\n"
a, b = stress_before['before'], stress['after']
text += f"| 50,000 x3, 64 visits, 32 scopes | {number(a['bytes'])} → {number(b['bytes'])} | {number(a['Streaming'])}/{number(a['Semantic'])} → {number(b['Streaming'])}/{number(b['Semantic'])} | {a['retainedBytes']/1048576:.3f} → {b['retainedBytes']/1048576:.3f} |\n\n"
text += "| Operation | 24 → 25, p50/p95/max ms |\n| --- | ---: |\n"
for key in ('write', 'read', 'activation', 'early'):
    text += f"| {key} | {triple(a['timing'][key])} → {triple(b['timing'][key])} |\n"
text += f"\nCold migration: **{stress['migrationMs']:.4f} ms**. All bulk write/read/activation samples in both arms reach 30 ms (3/3 each); early headers do not. This current-contract stress is outside the shared CF existing-state allowance and grants no future-reserve clearance. Full source bytes/hashes match the actual archived base; the migration projects back identically.\n\n"
for key in ("record-boundary", "input-boundary", "migration-record-limit"):
    result = data[f"{key}-candidate"]["result"]
    text += f"- `{key}`: `{json.dumps(result, ensure_ascii=True, separators=(',', ':'))}`\n"
text += f"- `20,000-place independent caps`: `{json.dumps(stress['limits'], ensure_ascii=True, separators=(',', ':'))}`\n"
text += "\n## Spike flags and qualification\n\nEvery matrix bulk write/read/full-activation sample reaches 30 ms in both arms (3/3 per operation); every single cold migration and all cold maximum-held bulk operations are likewise flagged. These are save/startup batches, never ordinary-play mod-work p95. Early-header samples remain below 30 ms. The complete operation-by-operation spike counts, allocation totals, cold refusal timings, timestamps and peaks are in the metrics JSON. Managed GC collection counts were not captured; native/Mono GC and frame costs remain unmeasured.\n\n"
text += "[Metrics-only results, including every p50/p95/max and spike count](e5d-name-identity-save-results.json). No ordinary-play or whole-frame sample was collected. The next combined rig batch must measure mod-work p95 <5 ms and every ≥30 ms spike separately from whole-frame cost/stalls; the four parked hotspots remain parked.\n"
p = root / "docs/perf/e5d-name-identity-save.md"
prefix = p.read_text(encoding="utf-8").split("\n## Final matched results")[0]
prefix = prefix.replace("Final matched figures and complete matrix results are recorded below after\nthe foreground measurement run.", "Final matched figures and complete matrix results follow.")
p.write_text(prefix + text, encoding="utf-8")
summary = {"baseline": "034bb7629391ef0fedb61d7122b0d6ee3c451ff9", "candidateCode": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(), "productionTrees": production_trees,
           "nativeEvidence": False, "cases": data}
(root / "docs/perf/e5d-name-identity-save-results.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
print("Published E5d metrics-only tables and JSON.")
