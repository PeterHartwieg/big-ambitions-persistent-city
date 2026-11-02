"""Extend only the test Item shape with inspected seasonal fields; never edit old cases."""
from pathlib import Path
import hashlib
import sys

ROOT = Path(__file__).resolve().parents[2]
source = (ROOT / "tools/donut-feasibility-regression/Program.cs").read_text(encoding="utf-8")
assert hashlib.sha256(source.encode()).hexdigest() == "707be922277d715ca17bbf1d38c05c5f608c53ce1c87ef8db545752b67c3f507"
anchor = "    public sealed class Item : Tags.TaggedScriptableObject\n    {\n"
assert source.count(anchor) == 1
source = source.replace(anchor, anchor + """        public Seasons.SeasonName season;
        public bool isSpecialGift, isSeasonalForSale, isSeasonalItemFromAIBusiness;
        public Seasons.ItemSeason[]? itemsBySeason;
""")
source += """
namespace Seasons
{
    public enum SeasonName { None, Halloween, Christmas }
    public sealed class ItemSeason { public SeasonName seasonName; public string itemName = ""; }
    public static class SeasonHelper
    {
        public static SeasonName CurrentSeasonName => throw new Exception("Native season getter reached");
    }
}
public static class PlayerPrefSettings
{
    public static bool SeasonalDecorations => throw new Exception("Native preferences reached");
}
"""
out = Path(sys.argv[1])
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text("#nullable enable\n" + source, encoding="utf-8")
