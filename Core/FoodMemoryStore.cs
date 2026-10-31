using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>Bounded generation index, rebuilt once on attachment. Slots never renumber or reclaim:
    /// saturation counts missed captures and leaves all memories, commitments and receipts intact.</summary>
    public sealed class FoodMemoryStore
    {
        private const int TableLimit = 2 * 1024 * 1024;
        private readonly FoodCommerceState food;
        private readonly Dictionary<string, BusinessReference> byId = new Dictionary<string, BusinessReference>(StringComparer.Ordinal);
        private readonly Dictionary<int, BusinessReference> bySlot = new Dictionary<int, BusinessReference>();
        private int nextSlot, tableCharacters = 2, tableBytes = 2;
        public long CapacityMisses { get; private set; }

        public FoodMemoryStore(FoodCommerceState food)
        {
            this.food = food;
            foreach (var reference in food.BusinessReferences)
            {
                byId.Add(reference.BusinessId, reference); bySlot.Add(reference.Slot, reference);
                Count(reference.BusinessId, reference.Slot, out int characters, out int bytes);
                tableCharacters += characters; tableBytes += bytes;
                nextSlot = Math.Max(nextSlot, reference.Slot + 1);
            }
            if (food.BusinessReferences.Count != 0) { tableCharacters--; tableBytes--; }
        }

        public int Find(string businessId) => byId.TryGetValue(businessId, out var reference) ? reference.Slot : -1;
        public string Identity(int slot) => bySlot[slot].BusinessId;

        private int Register(string businessId, string placeId)
        {
            if (byId.TryGetValue(businessId, out var existing))
            {
                if (existing.PlaceId != placeId) throw new InvalidOperationException("Business generation changed its place.");
                return existing.Slot;
            }
            Count(businessId, nextSlot, out int characters, out int bytes);
            if (food.BusinessReferences.Count == 0) { characters--; bytes--; }
            if (nextSlot >= 20000 || tableCharacters + characters > TableLimit || tableBytes + bytes > TableLimit)
            { CapacityMisses++; return -1; }
            var reference = new BusinessReference { Slot = nextSlot++, BusinessId = businessId, PlaceId = placeId };
            food.BusinessReferences.Add(reference); byId.Add(businessId, reference); bySlot.Add(reference.Slot, reference);
            tableCharacters += characters; tableBytes += bytes;
            return reference.Slot;
        }

        // Exact default-JSON escaped ID length. The other tuple fields are fixed ASCII (place = "PPP").
        // Includes a separator; caller removes the first separator. No serialization/strings per lookup.
        private static void Count(string id, int slot, out int characters, out int bytes)
        {
            int overhead = 14 + slot.ToString(CultureInfo.InvariantCulture).Length;
            characters = id.Length + overhead; bytes = Encoding.UTF8.GetByteCount(id) + overhead;
            foreach (char c in id)
            {
                int extra = c == '"' || c == '\\' || c == '\b' || c == '\t' || c == '\n' || c == '\f' || c == '\r' ? 1
                    : c < 32 ? 5 : c == '\u0085' || c == '\u2028' || c == '\u2029' ? 5 : 0;
                characters += extra;
                bytes += extra - (c == '\u0085' ? 1 : c == '\u2028' || c == '\u2029' ? 2 : 0);
            }
        }

        private static int Compare(ExperienceWatermark w, ResidentMeal meal)
        {
            int order = w.Day.CompareTo(meal.Day);
            if (order == 0) order = ((int)w.Meal).CompareTo((int)meal.Kind);
            return order == 0 ? w.Context.CompareTo(meal.ContextAttempts) : order;
        }

        public bool CommitStage(ResidentMeal meal, ExperienceStage stage)
        {
            if (stage != ExperienceStage.Committed && stage != ExperienceStage.Reached)
                throw new ArgumentOutOfRangeException(nameof(stage), "Terminal stages require an atomic experience commit.");
            if (meal.ContextAttempts < 1 || meal.ContextAttempts > 4 || meal.BusinessId.Length == 0) return false;
            var watermark = meal.Memory.Watermark;
            int order = watermark == null ? -1 : Compare(watermark, meal);
            if (order > 0 || order == 0 && watermark!.Stage >= stage) return false;
            // Only commitment can establish a new context. Old/pre-B trips remain neutral.
            if (order < 0 && stage != ExperienceStage.Committed) return false;
            if (order == 0 && watermark!.Stage >= ExperienceStage.Failed) return false;
            // These mutable DTOs are simulator-owned; no callback/yield can observe a partial stage.
            // Reuse the one compact watermark after its first allocation.
            if (watermark == null) meal.Memory.Watermark = watermark = new ExperienceWatermark();
            watermark.Day = meal.Day; watermark.Meal = meal.Kind; watermark.Context = meal.ContextAttempts; watermark.Stage = stage;
            return true;
        }

        /// <summary>One main-thread commit: validate the watermark, prepare the bounded update, then publish
        /// memory and watermark together without yielding, callbacks, routes, accounts or native calls.</summary>
        public bool Experience(ResidentMeal meal, ShopExperience cause, int experienceDay, ResidentTraits traits, FoodMemorySettings settings)
        {
            var w = meal.Memory.Watermark;
            if (w == null || Compare(w, meal) != 0 || w.Stage >= ExperienceStage.Failed) return false;
            int slot = Register(meal.BusinessId, meal.PlaceId);
            var entries = meal.Memory.Entries;
            if (slot >= 0)
            {
                int index = -1;
                for (int i = 0; i < entries.Count; i++) if (entries[i].BusinessRef == slot) { index = i; break; }
                ShopMemory? previous = index < 0 || FoodMemoryPolicy.Expired(entries[index], experienceDay, settings) ? null : entries[index];
                int affinity = previous == null ? 0 : FoodMemoryPolicy.EffectiveAffinity(previous, experienceDay, traits, settings);
                affinity = Math.Max(-8, Math.Min(8, affinity + (cause == ShopExperience.Served ? settings.FoodMemoryServedGain : -FoodMemoryPolicy.Penalty(cause, traits, settings))));
                int served = Math.Min(35, (previous?.Served ?? 0) + (cause == ShopExperience.Served ? 1 : 0));
                // Prepare all values before publishing. Existing slot order cannot change on an update.
                var replacement = index >= 0 ? entries[index] : new ShopMemory { BusinessRef = slot };
                if (index < 0)
                    while (entries.Count >= settings.FoodMemoryCapacity)
                    {
                        int victim = 0;
                        for (int i = 1; i < entries.Count; i++)
                            if (FoodMemoryPolicy.CompareEviction(entries[i], Identity(entries[i].BusinessRef), entries[victim], Identity(entries[victim].BusinessRef), experienceDay, traits, settings) < 0) victim = i;
                        entries.RemoveAt(victim);
                    }
                replacement.Day = experienceDay; replacement.Served = served; replacement.Affinity = affinity; replacement.Outcome = cause;
                if (index < 0)
                {
                    int insert = 0;
                    while (insert < entries.Count && entries[insert].BusinessRef < slot) insert++;
                    entries.Insert(insert, replacement);
                }
            }
            w!.Stage = cause == ShopExperience.Served ? ExperienceStage.Served : ExperienceStage.Failed;
            return true;
        }
    }
}
