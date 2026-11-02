"""Extend pinned E3 field doubles only; all generated output stays ignored."""
from pathlib import Path
import hashlib
import sys

root = Path(__file__).resolve().parents[2]
original = (root / "tools/donut-feasibility-regression/Program.cs").read_text(encoding="utf-8")
assert hashlib.sha256(original.encode()).hexdigest() == "707be922277d715ca17bbf1d38c05c5f608c53ce1c87ef8db545752b67c3f507"
source = original[original.index("public sealed class BuildingRegistration"):]
source = source.replace("CandidateReader.Product", '"fixture:food"')
source = source.replace('    public sealed class Item : Tags.TaggedScriptableObject\n    {', '''    public sealed class Item : Tags.TaggedScriptableObject
    {
        public string itemName = "";
        public Seasons.SeasonName season;
        public bool isSpecialGift, isSeasonalForSale, isSeasonalItemFromAIBusiness;
        public string itemToReplaceWithWhenOvertakingBusiness = "";
        public Seasons.ItemSeason[]? itemsBySeason;''')
start = source.index("    public static class ItemsGetter")
end = source.index("\n}\nnamespace Buildings", start)
source = source[:start] + '''    public static class ItemsGetter
    {
        public static Dictionary<string, Item> Items = new(StringComparer.Ordinal);
        static readonly Dictionary<string, Item> Mods = new(StringComparer.Ordinal);
        public static IEnumerable<Item>? View;
        public static bool Unready;
        public static long AllReads, Lookups;
        public static IEnumerable<Item>? AllItems { get { AllReads++; return Unready ? null : View ?? Items.Values; } }
        public static Item? GetByName(string name, bool suppressError) { Lookups++; return Items.GetValueOrDefault(name); }
        public static void OnItemsLoaded(IList<Item> items)
        {
            Items.Clear(); Unready = false; View = null;
            foreach (var item in items) Items.TryAdd(item.itemName, item);
            foreach (var item in Mods.Values) Items.TryAdd(item.itemName, item); // native vanilla wins
        }
        public static void RegisterModItem(Item item)
        { if (Items.TryAdd(item.itemName, item)) Mods[item.itemName] = item; }
        public static bool UnregisterModItem(string name) { Mods.Remove(name); return Items.Remove(name); }
        private static void ResetStaticData() { Items.Clear(); Mods.Clear(); Unready = false; View = null; }
    }''' + source[end:]
source = source.replace('    public string businessTypeName = "fixture-full";', '''    public string businessTypeName = "fixture-full";
    public void Reset(bool fields = true) => throw new Exception("Native mutation reached");
    public void AddItemInstanceToBuilding(BigAmbitions.Items.ItemInstance item) => throw new Exception("Native mutation reached");
    public void RemoveItemInstanceFromBuilding(BigAmbitions.Items.ItemInstance item, bool trigger = true) => throw new Exception("Native mutation reached");
    public void OnBusinessTypeChanged() => throw new Exception("Native mutation reached");''')
source += '''
public static class ItemHelper
{
    public static void FillUpShowcaseShelfOrPointOfSale(ItemInstance i) => throw new Exception("Native mutation reached");
    public static bool SubtractFromStock(ItemInstance i, int amount = 1) => throw new Exception("Native mutation reached");
}
public static class ReStockingHelper
{
    public static void RefillSingleItemWithLimit(ItemInstance i) => throw new Exception("Native mutation reached");
    public static int TryAddStockAmount(ItemInstance i, CargoInstance c, int amount) => throw new Exception("Native mutation reached");
}
namespace Helpers
{
    public static class BusinessHelper
    {
        public static void UpdateCachedAvailableProducts(BuildingRegistration r) => throw new Exception("Native mutation reached");
    }
}
namespace Seasons
{
    public enum SeasonName { None, Halloween, Christmas }
    public sealed class ItemSeason { public SeasonName seasonName; public string itemName = ""; }
    public static class SeasonHelper { public static SeasonName CurrentSeasonName => throw new Exception("Native season getter reached"); }
}
public static class PlayerPrefSettings { public static bool SeasonalDecorations => throw new Exception("Native preference reached"); }
'''
source = source.replace('        public string id = "", itemName = "";', '''        public string id = "", itemName = "";
        public void AddToCargo(CargoInstance c, int at = -1) => throw new Exception("Native mutation reached");
        public void RemoveFromCargo(CargoInstance c) => throw new Exception("Native mutation reached");
        public void ReduceFromCargo(CargoInstance c, int n) => throw new Exception("Native mutation reached");
        public void MergeCargo(CargoInstance a, CargoInstance b) => throw new Exception("Native mutation reached");
        public void MergeCargo(CargoInstance a, CargoInstance b, int n) => throw new Exception("Native mutation reached");''')
source = source.replace('        public string itemName = ""; public int amount;', '''        public string itemName = ""; public int amount;
        public void MergeAmount(CargoInstance c, int n) => throw new Exception("Native mutation reached");''')
out = Path(sys.argv[1])
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text("#nullable enable\nusing System;\nusing System.Collections.Generic;\nusing BigAmbitions.Items;\n" + source, encoding="utf-8")
