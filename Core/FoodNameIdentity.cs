using System;

namespace PersistentCity.Core
{
    /// <summary>Schema-25 identity/capture capability. This is not a loaded-product catalogue
    /// or an eligibility policy. Wire-local references never enter Core decisions.</summary>
    public static class FoodNameIdentity
    {
        public const string Wire = "2|name=1|held=1|neutral=2,900,100,450";
        // Cold schema-25 contract only until E5c/E5b implement execution. Codec inspection and
        // round trips are supported; early authority bootstrap and simulator startup refuse it.
        public const string DynamicWire = "2|name=1|held=2|neutral=2,900,100,450";
        public const int MaximumNames = 256;
        public const int MaximumNameLength = 256;
        public const int MaximumTableWire = 512 * 1024;
        public const int MaximumHeldWire = 8 * 1024;
        public static bool IsCanonical(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name!.Length > MaximumNameLength) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsControl(c) || c == '|') return false;
                if (char.IsHighSurrogate(c))
                { if (++i >= name.Length || !char.IsLowSurrogate(name[i])) return false; }
                else if (char.IsLowSurrogate(c)) return false;
            }
            return true;
        }
        public static bool AddedProperty(Type type, string? name)
            => type == typeof(CityState) && (name == "ProductNames" || name == "NamePolicyStartsDay")
            || type == typeof(FoodDecisionOffer) && name == "Metadata";
    }
}
