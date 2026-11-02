"""E5a isolation plus exact incoming-source preservation; no weakening of E3 pins."""
from pathlib import Path
import argparse
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = "034bb7629391ef0fedb61d7122b0d6ee3c451ff9"
OWN = {"Core/FoodEligibilityImage.cs", "Core/LoadedStockEligibility.cs",
       "Runtime/LoadedCatalogueEligibilityReader.cs", "Runtime/LoadedCatalogueLifecycleMembers.cs"}
SYMBOLS = ("LoadedCatalogueEligibilityReader", "LoadedCatalogueLifecycleMembers", "LoadedStockMenuBuilder", "LoadedStockCatalogue")


def scan(sources):
    for path, source in sources.items():
        code = re.sub(r"//[^\n]*|/\*.*?\*/", "", source, flags=re.S)
        if path not in OWN:
            assert not any(symbol in code for symbol in SYMBOLS), "E5a production caller: " + path
            continue
        for forbidden in ("Harmony", "SetUp(", "ProcessCustomer", "SaveGameManager", "ItemCached", "FoodProducts.",
                          "FoodMenuPolicy.", ".HasTag(", "TagRef.", ".tagIndexes", "SetValue", ".Invoke(",
                          "SeasonHelper", "PlayerPrefSettings", "GetItemNameBySeason", "IsAvailableInCurrentSeason",
                          "Addressables", ".RegisterModItem(", ".UnregisterModItem(", ".OnItemsLoaded(",
                          "Task.", "Thread", "Timer", "Update(", "FoodSupplyReader", "NativeFoodMarket", "FoodAuthority"):
            assert forbidden not in code, path + ": " + forbidden
        assert not re.search(r"\b(?:registration|instance|item|c|r|type)\.\w+\s*=(?!=)", code), "Native write: " + path
        if path.startswith("Core/"):
            assert not any(x in code for x in ("UnityEngine", "BigAmbitions", "System.Reflection")), "Impure core"
    code = sources["Runtime/LoadedCatalogueEligibilityReader.cs"]
    assert code.count('GetField("tags",') == 1 and code.count(".GetValue(") == 1
    assert "LoadedStockLimits.CataloguePerUnit" in code and "LoadedStockLimits.FurniturePerUnit" in code


sources = {p.relative_to(ROOT).as_posix(): p.read_text(encoding="utf-8-sig")
           for folder in ("Core", "Runtime", "Persistence", "Compatibility")
           for p in (ROOT / folder).rglob("*.cs") if not {"bin", "obj"}.intersection(p.parts)}
scan(sources)
probes = [("Runtime/Nested/Caller.cs", "new LoadedCatalogueEligibilityReader();"),
          ("Runtime/NativeFoodMarket.cs", "LoadedStockMenuBuilder.Read();")]
probes += [("Runtime/LoadedCatalogueEligibilityReader.cs", snippet) for snippet in
           ("Harmony", "Update(", "item.type = 0;", "f.SetValue(item, 0);", "instance.ItemCached", "TagRef.Itemtag.isbag",
            "SeasonHelper.CurrentSeasonName", "PlayerPrefSettings.SeasonalDecorations", "ItemsGetter.RegisterModItem(item);")]
for path, snippet in probes:
    mutated = dict(sources)
    mutated[path] = mutated.get(path, "") + "\n" + snippet
    try:
        scan(mutated)
    except AssertionError:
        pass
    else:
        raise AssertionError("Surviving source mutation: " + snippet)
# Immutable output DTOs are the intended sibling seam, not scanner activation.
scan(dict(sources, **{"Core/SiblingDtoConsumer.cs": "FoodEligibilityImage image; FoodStockOfferImage offer; FoodHolderResourceImage holder;"}))
parser = argparse.ArgumentParser(__doc__)
parser.add_argument("--audit-base", nargs="?", const=BASE,
                    help="Explicit slice-preservation audit; default guards allow unrelated sibling merges")
base = parser.parse_args().audit_base
if base:
    entries = subprocess.check_output(["git", "ls-tree", "-rz", base], cwd=ROOT).rstrip(b"\0").split(b"\0")
    protected = []
    for entry in entries:
        metadata, raw = entry.split(b"\t", 1)
        path = raw.decode()
        if path.startswith(("Core/", "Runtime/", "Persistence/", "Compatibility/", "tests/", "tools/", "config/")):
            protected.append((path, metadata.split()[2].decode()))
    actual = subprocess.check_output(["git", "hash-object", "--stdin-paths"], cwd=ROOT,
                                    input="".join(path + "\n" for path, _ in protected), text=True).splitlines()
    assert len(actual) == len(protected)
    for (path, expected), observed in zip(protected, actual):
        assert expected == observed, "Incoming source changed: " + path
    assert subprocess.check_output(["git", "hash-object", "docs/plans/e5-dynamic-products.md"], cwd=ROOT).strip() == subprocess.check_output(
        ["git", "rev-parse", base + ":docs/plans/e5-dynamic-products.md"], cwd=ROOT).strip(), "E5 plan belongs to docs sibling"
    print(f"PASS {len(protected)} incoming source blobs unchanged against {base}; E5 plan untouched")
print(f"PASS {len(probes)} isolation mutations rejected; immutable DTO sibling consumer allowed")
