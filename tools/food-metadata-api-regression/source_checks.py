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
mod = (root / 'Runtime/PersistentCityMod.cs').read_text()
initialize = mod.split('public void Initialize(ModContext context)', 1)[1].split('private void Update()', 1)[0]
assert initialize.index('try') < initialize.index('FoodProductMetadataApi.BindMainThread()') < initialize.index('catch (Exception')
shutdown = mod.split('public void Shutdown()', 1)[1].split('private void OnDestroy()', 1)[0]
assert shutdown.index('Detach();') < shutdown.index('initialization = null;') < shutdown.index('enabled = false;') < shutdown.index('FoodProductMetadataApi.Unload()')
assert 'if (reasons.Count < names.Count)' in registry
assert 'publishedRevision == revision && ReferenceEquals(publishedCatalogue, ready)' in registry
# Audit this slice's implementation interval, without freezing seams that E5b must change
# after integration. The final E5e diff is also reviewed before delivery.
base = '4ee0c8d'
implementation = '545f589'
paths = subprocess.check_output(['git','ls-tree','-r','--name-only',base,'--','Persistence'],cwd=root,text=True).splitlines()
paths += ['Core/FoodToday.cs','Core/FoodNameIdentity.cs','Core/LegacyFoodOfferMetadata.cs','Core/FoodWeightedCommerce.cs',
          'Runtime/NativeFoodMarket.cs','Runtime/NativeFoodOwnership.cs','Runtime/FoodSupplyReader.cs']
changes = subprocess.check_output(['git','diff','--name-only',base,implementation,'--',*paths],cwd=root,text=True)
assert not changes, changes
print('PASS E5e metadata-only surface, no polling/reflection/callbacks/native writes; schema, held capture and native authority unchanged in 4ee0c8d..545f589. Future E5b integration is not frozen.')
