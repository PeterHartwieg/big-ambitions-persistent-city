using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public static class CarAppearance
    {
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

        public static int Paint(CarState car)
        {
            if (car == null) throw new ArgumentNullException(nameof(car));
            return car.PaintSeed != 0 ? car.PaintSeed : 1 + (int)(Hash(car.Id, ":paint") % 2147483646u);
        }

        public static uint Hash(string id) => Hash(id, "");

        public static uint Hash(string id, string suffix)
        {
            unchecked
            {
                uint value = 2166136261;
                if (id != null) for (int i = 0; i < id.Length; i++) value = (value ^ id[i]) * 16777619;
                if (suffix != null) for (int i = 0; i < suffix.Length; i++) value = (value ^ suffix[i]) * 16777619;
                return value;
            }
        }
    }
}
