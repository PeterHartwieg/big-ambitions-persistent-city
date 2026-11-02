"""Frozen offline comparators; no native or rig inputs."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / 'build/food-metadata'
out.mkdir(parents=True, exist_ok=True)
def source(commit, path):
    return subprocess.check_output(['git', 'show', f'{commit}:{path}'], cwd=root, text=True)
choice = source('034bb76', 'Core/FoodChoicePolicy.cs')
choice = choice[choice.index('    public static partial class FoodChoicePolicy'):]
choice = choice.replace('class FoodChoicePolicy', 'class FoodChoicePolicyBefore', 1)
choice = choice.replace('private static readonly double[] TasteRatios', 'private const int MaximumShortlist=8, MaximumOffersPerShop=8;\n        private static readonly double[] TasteRatios', 1)
(out / 'FoodChoicePolicyBefore.cs').write_text('using System;\nusing System.Collections.Generic;\nnamespace PersistentCity.Core\n{\n'+choice)
parser = (root / 'Runtime/FoodProductOverrideSettings.cs').read_text()
# Mechanical before arm: same current policy/budget with the original per-entry HashSet.
parser = parser.replace('int fields=0;', 'var fields=new HashSet<string>(StringComparer.Ordinal);')
parser = parser.replace('if (bit==0 || (fields&bit)!=0) invalid=true;\n                        fields|=bit;', 'if (bit==0 || !fields.Add(field)) invalid=true;')
parser = parser[parser.index('    internal sealed class FoodProductOverrideConverter'):]
parser = parser.replace('class FoodProductOverrideConverter', 'class FoodProductOverrideConverterBefore', 1)
(out / 'FoodProductOverrideConverterBefore.cs').write_text('using System;\nusing System.Collections.Generic;\nusing System.Globalization;\nusing System.Linq;\nusing System.Text;\nusing Newtonsoft.Json;\nusing PersistentCity.Core;\nnamespace PersistentCity.Runtime\n{\n'+parser)
print('Prepared pinned 034bb76 choices and policy-matched pre-bitmask config parser.')
