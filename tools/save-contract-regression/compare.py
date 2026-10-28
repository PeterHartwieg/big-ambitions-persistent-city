"""Compare save contract cache against pre-change and current Core/Persistence sources."""
import argparse
import json
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
    result = subprocess.check_output(['dotnet', str(output / 'SaveContractRegression.dll')], text=True)
    (args.output / (name + '.txt')).write_text(result)
    parsed = json.loads(result)
    print(name, parsed)
    results.append({k: v for k, v in parsed.items() if k not in ('writeBytes', 'readBytes')})
if results[0] != results[1]:
    raise SystemExit('Event/save outputs differ between source trees')

print('Baseline/candidate save bytes and refusal outcomes match.')
