using System;

namespace PersistentCity.Core
{
    public enum Wardrobe { Casual, Formal, Sport, Party }

    public readonly struct PersonLook
    {
        public PersonLook(int seed, Wardrobe wardrobe) { Seed = seed; Wardrobe = wardrobe; }
        public int Seed { get; }
        public Wardrobe Wardrobe { get; }
    }

    public static class PersonAppearance
    {
        public static readonly int[] WardrobePercent = { 55, 25, 12, 8 };

        public static PersonLook For(CitizenIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            uint id = CarAppearance.Hash(identity.Id ?? "");
            uint look = Mix(id ^ unchecked((uint)identity.AppearanceSeed));
            int seed = 1 + (int)(look % 2147483646u);
            uint roll = Mix(CarAppearance.Hash((identity.Id ?? "") + ":wardrobe")) % 100u;
            var wardrobe = Wardrobe.Casual;
            uint sum = 0;
            for (int i = 0; i < WardrobePercent.Length; i++)
            {
                sum += (uint)WardrobePercent[i];
                if (roll < sum) { wardrobe = (Wardrobe)i; break; }
            }
            return new PersonLook(seed, wardrobe);
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16;
                return value;
            }
        }
    }
}
