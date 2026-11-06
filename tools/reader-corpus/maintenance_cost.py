"""F7f maintenance cost per frozen reader: source lines, corpus bytes and corpus test time.

Reads the reader registry in tests/ReaderCorpus.cs, counts the physical lines of each reader's Persistence files,
sums corpus fixture bytes by owning reader and, given one or more TRX files from
`dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~ReaderCorpusTests --logger trx`,
sums per-schema test durations by owning reader (median across the TRX files). It starts no dotnet process.
"""
import argparse
import glob
import json
import re
import statistics
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[2]
registry = (root / "tests/ReaderCorpus.cs").read_text(encoding="utf-8")
rows = re.findall(r'new\("(\w+)", (\d+), (\d+), [\w.]+, new\[\] \{([^}]*)\}\)', registry)
if not rows:
    raise SystemExit("reader registry not found in tests/ReaderCorpus.cs")
readers = [{"name": n, "min": int(a), "max": int(b), "sources": re.findall(r'"([^"]+)"', s)} for n, a, b, s in rows]


def lines(path):
    with open(path, encoding="utf-8") as handle:
        return sum(1 for _ in handle)


def owner(schema):
    return next(r for r in readers if r["min"] <= schema <= r["max"])


for reader in readers:
    files = sorted({f for pattern in reader["sources"] for f in glob.glob(str(root / "Persistence" / pattern))})
    reader["files"] = len(files)
    reader["lines"] = sum(lines(f) for f in files)
    reader["corpus_bytes"] = 0
    reader["corpus_files"] = 0
    reader["test_ms"] = []

for folder in sorted((root / "tests/ReaderCorpus").glob("schema-*")):
    reader = owner(int(folder.name.split("-")[1]))
    for payload in folder.glob("*.json"):
        if payload.name != "corpus.json":
            reader["corpus_bytes"] += payload.stat().st_size
            reader["corpus_files"] += 1

parser = argparse.ArgumentParser()
parser.add_argument("--trx", nargs="*", default=[])
parser.add_argument("--json", help="write the table as JSON to this path")
args = parser.parse_args()
facts = []
namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def ms(text):
    h, m, s = text.split(":")
    return (int(h) * 3600 + int(m) * 60 + float(s)) * 1000


for trx in args.trx:
    per_reader = {r["name"]: 0.0 for r in readers}
    fact_ms = 0.0
    for result in ET.parse(trx).getroot().iterfind(".//t:UnitTestResult", namespace):
        name = result.get("testName")
        if "ReaderCorpusTests." not in name:
            continue
        if result.get("outcome") != "Passed":
            raise SystemExit(f"{trx}: {name} {result.get('outcome')}")
        match = re.search(r"\(schema: (\d+)\)$", name)
        if match:
            per_reader[owner(int(match.group(1)))["name"]] += ms(result.get("duration"))
        else:
            fact_ms += ms(result.get("duration"))
    for reader in readers:
        reader["test_ms"].append(per_reader[reader["name"]])
    facts.append(fact_ms)

print("| Reader | Schemas | Files | Lines | Corpus files | Corpus bytes | Corpus test ms (median of %d) |" % len(args.trx))
print("| --- | --- | ---: | ---: | ---: | ---: | ---: |")
for r in readers:
    median = "%.1f" % statistics.median(r["test_ms"]) if r["test_ms"] else "n/a"
    print(f"| {r['name']} | {r['min']}-{r['max']} | {r['files']} | {r['lines']} | {r['corpus_files']} | {r['corpus_bytes']} | {median} |")
if facts:
    print(f"| shared facts (packaging, hash pin) | - | - | - | - | - | {statistics.median(facts):.1f} |")
if args.json:
    Path(args.json).write_text(json.dumps({"readers": readers, "facts_ms": facts}, indent=2) + "\n", encoding="utf-8")
