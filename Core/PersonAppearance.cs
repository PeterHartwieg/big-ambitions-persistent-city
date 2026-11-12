using System;

namespace PersistentCity.Core
{
    /// <summary>The native wardrobes a body can wear (the game's AppearanceTag names; uniform, pool and homeless looks are
    /// left to native scenes).</summary>
    public enum Wardrobe { Casual, Formal, Sport, Party }

    /// <summary>What a body looks like: the look seed for the native randomizer and the wardrobe.</summary>
    public readonly struct PersonLook
    {
        public PersonLook(int seed, Wardrobe wardrobe) { Seed = seed; Wardrobe = wardrobe; }
        public int Seed { get; }
        public Wardrobe Wardrobe { get; }
    }

    /// <summary>
    /// GR-9 (G6): appearance beyond the donor seed. Residents drawn from the same RLO donor share its appearance seed; the
    /// look seed mixes that seed with the person ID, so two people never share a look by sharing a donor, and the wardrobe
    /// comes from the person ID too. Gender and age stay the donor's. Derived, never saved: the same person looks the same
    /// after pool reuse, reload or camera movement, and nothing in the saved city changes.
    /// </summary>
    public static class PersonAppearance
    {
        /// <summary>Wardrobe shares in percent, in <see cref="Wardrobe"/> order (casual, formal, sport, party).</summary>
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
