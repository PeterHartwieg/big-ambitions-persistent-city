"""E5e architectural checks in addition to headless behavior/persistence tests."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
registry = (root / 'Core/FoodProductMetadataRegistry.cs').read_text()
api = (root / 'Runtime/FoodProductMetadataApi.cs').read_text()
for token in ['UnityEngine', 'System.Reflection', 'GetField(', 'GetMethod(', 'ProcessCustomer', 'ChangeMoney', 'RegisterModItem', 'UnregisterModItem', 'Action<', 'Func<', 'event ', 'void Update(']:
    assert token not in registry + api, token
assert 'Registry.Publish(catalogue)' in api
assert 'metadataLifetime' in (root / 'Runtime/PersistentCityMod.cs').read_text()
assert 'if (reasons.Count < names.Count)' in registry
assert 'publishedRevision == revision && ReferenceEquals(publishedCatalogue, ready)' in registry
# Freeze the preexisting schema/capture/native authority implementation against this slice's base.
base = '4ee0c8d'
paths = subprocess.check_output(['git','ls-tree','-r','--name-only',base,'--','Persistence'],cwd=root,text=True).splitlines()
paths += ['Core/FoodToday.cs','Core/FoodNameIdentity.cs','Core/LegacyFoodOfferMetadata.cs','Core/FoodWeightedCommerce.cs',
          'Runtime/NativeFoodMarket.cs','Runtime/NativeFoodOwnership.cs','Runtime/FoodSupplyReader.cs']
for path in paths:
    if not path.endswith('.cs'):
        continue
    before = subprocess.check_output(['git','show',f'{base}:{path}'],cwd=root)
    # Source check ignores platform checkout newlines, never semantic changes.
    assert before.replace(b'\r\n',b'\n') == (root/path).read_bytes().replace(b'\r\n',b'\n'), path
print('PASS E5e metadata-only surface, no polling/reflection/callbacks/native writes; schema, held capture and native authority unchanged from 4ee0c8d.')
