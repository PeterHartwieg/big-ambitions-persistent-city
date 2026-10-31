"""Compare the save contract against pre-change and current Core/Persistence sources."""
import argparse
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--baseline', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--bench', action='store_true', help='also measure save size/time at 957 and 5,000 residents')
parser.add_argument('--ceiling', action='store_true', help='with --bench, also measure the 50,000-resident codec ceiling')
parser.add_argument('--dotnet', default='dotnet')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
extra = (['--bench'] if args.bench else []) + (['--ceiling'] if args.ceiling else [])
# Fields that differ by design when the schema changes. Equality covers refusals and the economy scenario
# (same seed, same orders, purchases, money and meal state outside the new schema's fields).
by_design = ('writeBytes', 'readBytes', 'bench', 'hot', 'schema', 'digest', 'length')
results = []
for name, source in [('baseline', args.baseline.resolve()), ('candidate', root)]:
    output = (args.output / name).resolve()
    subprocess.run([args.dotnet, 'build', str(Path(__file__).parent), '-c', 'Release', '-o', str(output),
                    '-p:SourceRoot=' + str(source)], check=True, capture_output=True)
    result = subprocess.check_output([args.dotnet, str(output / 'SaveContractRegression.dll')] + extra, text=True)
    (args.output / (name + '.txt')).write_text(result)
    parsed = json.loads(result)
    print(name, json.dumps(parsed, indent=1))
    results.append(parsed)
same_schema = results[0]['schema'] == results[1]['schema']
keys = [k for k in results[0] if k not in by_design or (same_schema and k in ('digest', 'length'))]
differences = [k for k in keys if results[0][k] != results[1].get(k)]
if differences:
    raise SystemExit('Outputs differ between source trees: ' + ', '.join(differences))
print('Baseline/candidate refusal outcomes and economy match' + (' (save bytes too).' if same_schema else
      '; schema ' + str(results[0]['schema']) + ' -> ' + str(results[1]['schema']) + ' save bytes differ by design.'))
