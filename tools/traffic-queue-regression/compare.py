"""Compare traffic queue observations against pre-change and current Core/Persistence sources."""
import argparse
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--baseline', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
results = []
for name, source in [('baseline', args.baseline.resolve()), ('candidate', root)]:
    output = (args.output / name).resolve()
    subprocess.run(['dotnet', 'build', str(Path(__file__).parent), '-c', 'Release', '-o', str(output),
                    '-p:SourceRoot=' + str(source)], check=True, capture_output=True)
    result = subprocess.check_output(['dotnet', str(output / 'TrafficQueueRegression.dll')], text=True)
    (args.output / (name + '.txt')).write_text(result)
    results.append(result)
if results[0] != results[1]:
    raise SystemExit('Traffic outputs differ between source trees')
print(results[1], end='')
print('Baseline/candidate traffic observations and captures match.')
