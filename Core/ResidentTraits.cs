using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>Lifestyle traits in generation version 1, each a fixed integer band 0-4 (2 is neutral).
    /// Loyalty/curiosity runs from very curious (0) to very loyal (4); every other trait runs from low to high.</summary>
    public enum ResidentTrait
    {
        CroissantTaste, BurgerTaste, HotDogTaste, PriceSensitivity, QueuePatience,
        LoyaltyCuriosity, TravelWillingness, HealthOrientation, QualityOrientation, TimePressure
    }

    /// <summary>
    /// One consumer's saved lifestyle traits. Bands holds one digit 0-4 per <see cref="ResidentTrait"/>,
    /// in enum order; the mapping is fixed by Version. The compact form keeps a 50,000-resident save small.
    /// Generated once from the saved city seed and the stable resident/consumer ID, then never regenerated:
    /// the saved value is authoritative, and nothing mutable (name, home, job, body, settings) is a key.
    /// Traits are immutable after generation, so event copies of a resident may share the record.
    /// </summary>
    public sealed class ResidentTraits
    {
        public const int CurrentVersion = 1;
        public const int Count = 10;
        public const int Neutral = 2;
        public const int MaximumBand = 4;

        public int Version { get; set; } = CurrentVersion;
        public string Bands { get; set; } = "";

        public int Band(ResidentTrait trait) => Bands[(int)trait] - '0';

        public bool IsValid
        {
            get
            {
                if (Version != CurrentVersion || Bands == null || Bands.Length != Count) return false;
                foreach (char c in Bands) if (c < '0' || c > '0' + MaximumBand) return false;
                return true;
            }
        }

        /// <summary>The product whose taste band a trait holds under version 1, or null for non-taste traits.</summary>
        public static string? TasteProduct(ResidentTrait trait) => trait == ResidentTrait.CroissantTaste ? FoodProducts.Croissant
            : trait == ResidentTrait.BurgerTaste ? FoodProducts.Burger : trait == ResidentTrait.HotDogTaste ? FoodProducts.HotDog : null;

        private static readonly string[] Codes =
        {
            "taste:croissant", "taste:burger", "taste:hotdog", "price", "queue",
            "loyalty", "travel", "health", "quality", "time"
        };

        /// <summary>Deterministic version-1 traits. Each trait has its own hash domain:
        /// "pc:traits:v1|&lt;id length&gt;:&lt;id&gt;|&lt;trait code&gt;", seeded by the saved city seed.</summary>
        public static ResidentTraits Generate(int seed, string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) throw new ArgumentException("A stable resident or consumer ID is required.", nameof(stableId));
            uint prefix = StableRandom.Append(StableRandom.Begin(seed), "pc:traits:v1|");
            prefix = StableRandom.Append(StableRandom.AppendDecimal(prefix, stableId.Length), ':');
            prefix = StableRandom.Append(StableRandom.Append(prefix, stableId), '|');
            var bands = new char[Count];
            for (int i = 0; i < Count; i++)
                bands[i] = (char)('0' + BandFor(StableRandom.Finish(StableRandom.Append(prefix, Codes[i])) % 100));
            return new ResidentTraits { Version = CurrentVersion, Bands = new string(bands) };
        }

        // Fixed version-1 shape: 10% / 20% / 40% / 20% / 10%, so most people are neutral on most traits.
        private static int BandFor(uint percentile) => percentile < 10 ? 0 : percentile < 30 ? 1 : percentile < 70 ? 2 : percentile < 90 ? 3 : 4;

        /// <summary>
        /// Player-facing words for non-neutral traits; never numbers. Loyalty/curiosity is generated but not
        /// described: it has no effect until shop memory exists, and words must not imply behaviour that is absent.
        /// </summary>
        public static IEnumerable<string> Words(ResidentTraits traits, Func<string, string> productName)
        {
            if (traits == null || !traits.IsValid) yield break;
            for (var trait = ResidentTrait.CroissantTaste; trait <= ResidentTrait.HotDogTaste; trait++)
            {
                string plural = Plural(productName(TasteProduct(trait)!));
                switch (traits.Band(trait))
                {
                    case 0: yield return "dislikes " + plural; break;
                    case 1: yield return "not keen on " + plural; break;
                    case 3: yield return "likes " + plural; break;
                    case 4: yield return "loves " + plural; break;
                }
            }
            string? word = Pick(traits.Band(ResidentTrait.PriceSensitivity), "doesn't mind paying more", "watches prices", "very price-conscious");
            if (word != null) yield return word;
            word = Pick(traits.Band(ResidentTrait.QueuePatience), "dislikes queues", "patient in queues", "doesn't mind queues");
            if (word != null) yield return word;
            word = Pick(traits.Band(ResidentTrait.TravelWillingness), "prefers food close by", "happy to travel for food", "happy to travel far for food");
            if (word != null) yield return word;
            word = Pick(traits.Band(ResidentTrait.HealthOrientation), "enjoys hearty food", "prefers lighter food", "health-conscious eater");
            if (word != null) yield return word;
            word = Pick(traits.Band(ResidentTrait.QualityOrientation), "not fussy about quality", "values quality", "insists on quality");
            if (word != null) yield return word;
            word = Pick(traits.Band(ResidentTrait.TimePressure), "takes their time over meals", "often in a hurry", "always in a hurry");
            if (word != null) yield return word;
        }

        private static string? Pick(int band, string low, string high, string veryHigh) => band < Neutral ? low
            : band == Neutral ? null : band == MaximumBand ? veryHigh : high;

        private static string Plural(string name) => name.EndsWith("s", StringComparison.Ordinal) ? name : name + "s";

        public override string ToString() => "v" + Version.ToString(CultureInfo.InvariantCulture) + ":" + Bands;
    }
}
