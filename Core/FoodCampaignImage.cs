using System;

namespace PersistentCity.Core
{
    /// <summary>One immutable business image: eight types by eight dated 24-hour observation masks.
    /// A set bit proves only an actual enabled/verified C1 observation at that hour.</summary>
    public sealed class FoodCampaignImage
    {
        private readonly uint[] masks;
        public int BusinessRef { get; }
        public int ThroughHour { get; }
        public int Day => ThroughHour / 24;
        public FoodCampaignImage(int businessRef, int throughHour, uint[] activity)
            : this(businessRef, throughHour, activity, false) { }
        private FoodCampaignImage(int businessRef, int throughHour, uint[] activity, bool ownsActivity)
        {
            if (businessRef < 0 || businessRef >= 20000 || throughHour < 0 || throughHour > 1000000000 / 60 || activity.Length != 64)
                throw new ArgumentException("Invalid campaign image header.");
            BusinessRef = businessRef; ThroughHour = throughHour; masks = ownsActivity ? activity : (uint[])activity.Clone();
            bool any = false;
            for (int age = 0; age < 8; age++) for (int type = 0; type < 8; type++)
            {
                uint mask = masks[age * 8 + type]; any |= mask != 0;
                if (mask > 0xffffff || mask != 0 && (Day < age || CampaignTypeGroups.VerifiedName(type) == null)
                    || age == 0 && (mask >> (ThroughHour % 24 + 1)) != 0)
                    throw new ArgumentException("Invalid/unobserved campaign mask.");
            }
            if (!any) throw new ArgumentException("Empty campaign images are not retained.");
        }
        public uint Mask(int day, int type) => day > Day || day < Day - 7 || type < 0 || type >= 8 ? 0 : masks[(Day - day) * 8 + type];
        public string Encode()
        {
            var bytes = new byte[192];
            for (int i = 0; i < 64; i++) { bytes[i * 3] = (byte)masks[i]; bytes[i * 3 + 1] = (byte)(masks[i] >> 8); bytes[i * 3 + 2] = (byte)(masks[i] >> 16); }
            return "1" + MemoryWire.Number(BusinessRef, 3) + MemoryWire.Number(ThroughHour, 5) + Convert.ToBase64String(bytes);
        }
        public static FoodCampaignImage Decode(string wire)
        {
            if (wire == null || wire.Length != 265 || wire[0] != '1') throw new FormatException("Invalid campaign image wire/version.");
            byte[] bytes = Convert.FromBase64String(wire.Substring(9));
            if (bytes.Length != 192 || Convert.ToBase64String(bytes) != wire.Substring(9)) throw new FormatException("Noncanonical campaign image.");
            var activity = new uint[64];
            for (int i = 0; i < 64; i++) activity[i] = (uint)(bytes[i * 3] | bytes[i * 3 + 1] << 8 | bytes[i * 3 + 2] << 16);
            return new FoodCampaignImage(MemoryWire.Number(wire, 1, 3), MemoryWire.Number(wire, 4, 5), activity, true);
        }
        public static FoodCampaignImage? Observe(FoodCampaignImage? previous, int reference, int hour, int enabledTypes)
        {
            if (previous != null && hour <= previous.ThroughHour) throw new ArgumentException("Campaign observations must advance.");
            var activity = new uint[64]; bool any = false; int day = hour / 24;
            for (int age = 0; age < 8; age++) for (int type = 0; type < 8; type++)
            {
                uint mask = previous?.Mask(day - age, type) ?? 0;
                if (age == 0 && (enabledTypes & (1 << type)) != 0) mask |= 1u << (hour % 24);
                activity[age * 8 + type] = mask; any |= mask != 0;
            }
            return any ? new FoodCampaignImage(reference, hour, activity, true) : null;
        }
    }
}
