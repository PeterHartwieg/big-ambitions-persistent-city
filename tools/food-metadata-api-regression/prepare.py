"""Policy-matched before arm for the skipped all-refused claim walk only."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
source = (root / 'Core/FoodProductMetadataRegistry.cs').read_text()
start = source.index('    /// <summary>Host-owned loaded-name evidence')
end = source.index('    /// <summary>Provider-scoped lifetime')
source = source[:start] + source[end:]  # Shared immutable host input, identical in both arms.
assert source.count('if (reasons.Count < names.Count)') == 1
source = source.replace('if (reasons.Count < names.Count)', '')
source = source.replace('providers.Add(providerId, provider); characters += c; bytes += b;',
                        'providers.Add(providerId, provider); characters += c; bytes += b; revision++;')
source = source.replace('if (hadEntries) revision++;', 'revision++;')
# The before arm always increments revision on removal.
source = source.replace('bool hadEntries = provider.Entries.Count > 0;', '')
source = source.replace('namespace PersistentCity.Core', 'using PersistentCity.Core;\nnamespace PersistentCity.Core.Before', 1)
out = root / 'build/food-metadata-api'
out.mkdir(parents=True, exist_ok=True)
(out / 'RegistryBefore.cs').write_text(source)
print('Prepared policy-matched metadata registry before all-refused skip/empty-provider image reuse.')
