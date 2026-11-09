using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>
    /// F2d: the model and paint a car body shows. A saved assignment (an older city, or one set outside presentation) is
    /// kept; otherwise both derive from the stable car ID alone, the same values the native adapter used to write on
    /// first acquisition. Nothing is written, so whether a car was ever seen does not change the saved city (E1).
    /// </summary>
    public static class CarAppearance
    {
        /// <summary>The model: the saved one, or the ordinal-sorted distinct ordinary model at Hash(car ID). False
        /// without a saved model and with an empty catalogue. <paramref name="models"/> is read only when needed.</summary>
        public static bool TryModel(CarState car, Func<IReadOnlyList<string>> models, out string model)
        {
            if (car == null) throw new ArgumentNullException(nameof(car));
            model = car.ModelId ?? "";
            if (model.Length != 0) return true;
            var catalogue = models();
            if (catalogue.Count == 0) return false;
            model = catalogue[(int)(Hash(car.Id) % (uint)catalogue.Count)];
            return true;
        }

        /// <summary>The paint seed: the saved one, or 1 + Hash(car ID + ":paint") mod 2147483646 (never 0).</summary>
        public static int Paint(CarState car)
        {
            if (car == null) throw new ArgumentNullException(nameof(car));
            return car.PaintSeed != 0 ? car.PaintSeed : 1 + (int)(Hash(car.Id + ":paint") % 2147483646u);
        }

        /// <summary>32-bit FNV-1a over the UTF-16 code units, as the native adapter has always hashed car IDs.</summary>
        public static uint Hash(string id)
        {
            unchecked
            {
                uint value = 2166136261;
                foreach (char c in id ?? "") value = (value ^ c) * 16777619;
                return value;
            }
        }
    }
}
