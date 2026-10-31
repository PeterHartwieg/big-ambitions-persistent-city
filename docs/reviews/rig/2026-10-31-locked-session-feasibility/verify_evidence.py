"""Verify retained native evidence; this does not launch the game or run an offline suite."""
from pathlib import Path
import csv, gzip, hashlib, json, math

root = Path(__file__).parent
summary = json.loads((root / 'performance-summary.json').read_text())

def distribution(values):
    values = sorted(values)
    n = len(values)
    return dict(samples=n, mean=sum(values)/n, p50=values[int(n*.5)],
                p95=values[int(n*.95)], maximum=values[-1])

def equal(actual, expected):
    for key, value in actual.items():
        assert math.isclose(value, expected[key], rel_tol=1e-10, abs_tol=1e-8), (key, value, expected[key])

whole, mod = [], []
for window in summary['windows']:
    mod_name, wall_name = window['rawFiles']
    with gzip.open(root/(mod_name+'.gz'), 'rt') as stream:
        mod_rows = list(csv.DictReader(stream))
    with gzip.open(root/(wall_name+'.gz'), 'rt') as stream:
        wall_rows = list(csv.DictReader(stream))
    frequency = window['rawObservations']['stopwatchFrequency']
    m = [float(row['combinedUnionMs']) + float(row['PopulationRuntime.OnGUIInclusiveMs']) for row in mod_rows]
    w = [(int(row['endTicks'])-int(row['startTicks']))*1000/frequency for row in wall_rows]
    equal(distribution(m), window['modWorkIncludingGuiMs'])
    equal(distribution(w), window['wholeFrameMs'])
    assert len(m) == len(w)-1
    assert all(int(row['nextObservedUnityFrame']) == int(row['unityFrame'])+1 for row in mod_rows)
    assert window['realSeconds'] >= 60 and window['validity']['clean']
    assert window['validity']['autosaveOffBeforeAfter'] and window['validity']['saveFilesUnchanged']
    assert not window['validity']['saveActivity'] and not any(window['attributionErrors'].values())
    assert sum(value >= 30 for value in m) == len(window['modSpikesAtLeast30ms'])
    whole += w
    mod += m
equal(distribution(whole), summary['pooled']['wholeFrameMs'])
equal(distribution(mod), summary['pooled']['modWorkIncludingGuiMs'])
assert sum(value >= 30 for value in mod) == 1
manifest = json.loads((root/'file-manifest.json').read_text())
for entry in manifest:
    path = root/entry['file']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], path
assert sum(path.stat().st_size for path in root.rglob('*') if path.is_file()) < 3_000_000
print('PASS: three clean native windows, distributions, spike, manifest, size')
