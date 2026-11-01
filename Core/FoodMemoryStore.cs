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
        public long ProtectedInsertions { get; private set; }

        public FoodMemoryStore(FoodCommerceState food)
        {
            this.food = food;
            Synchronize();
        }
        private int indexedCount;
        private void Synchronize()
        {
            while (indexedCount < food.BusinessReferences.Count)
            {
                var reference=food.BusinessReferences[indexedCount++];
                byId.Add(reference.BusinessId, reference); bySlot.Add(reference.Slot, reference);
                Count(reference.BusinessId, reference.Slot, out int characters, out int bytes);
                tableCharacters += characters; tableBytes += bytes;
                if(indexedCount==1) {tableCharacters--;tableBytes--;}
                nextSlot = Math.Max(nextSlot, reference.Slot + 1);
            }
        }

        public BusinessReference? Reference(int slot) { Synchronize(); return bySlot.TryGetValue(slot, out var reference) ? reference : null; }

        public int Find(string businessId) { Synchronize(); return byId.TryGetValue(businessId, out var reference) ? reference.Slot : -1; }
        public string Identity(int slot) { Synchronize(); return bySlot[slot].BusinessId; }

        public int RegisterCampaign(string businessId, string placeId) => Register(businessId, placeId);

        private int Register(string businessId, string placeId)
        {
            Synchronize();
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
            food.BusinessReferences.Add(reference); indexedCount++; byId.Add(businessId, reference); bySlot.Add(reference.Slot, reference);
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

        public bool CommitStage(ResidentMeal meal, ExperienceStage stage, string? businessId = null)
        {
            if (stage != ExperienceStage.Committed && stage != ExperienceStage.Reached)
                throw new ArgumentOutOfRangeException(nameof(stage), "Terminal stages require an atomic experience commit.");
            if (meal.ContextAttempts < 1 || meal.ContextAttempts > 4 || (businessId ?? meal.BusinessId).Length == 0) return false;
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
        public bool Experience(ResidentMeal meal, ShopExperience cause, int experienceDay, ResidentTraits traits, FoodMemorySettings settings,
            string? businessId = null, string? placeId = null)
        {
            var w = meal.Memory.Watermark;
            if (w == null || Compare(w, meal) != 0 || w.Stage >= ExperienceStage.Failed) return false;
            var entries = meal.Memory.Entries;
            string business = businessId ?? meal.BusinessId;
            byId.TryGetValue(business, out var reference);
            int slot = reference?.Slot ?? -1, index = -1;
            for (int i = 0; i < entries.Count; i++) if (entries[i].BusinessRef == slot) { index = i; break; }
            if (cause != ShopExperience.Served && index < 0 && !SafeBadInsertion(meal.Memory, experienceDay, traits, settings))
            {
                // Removing a rival's boost can increase this shop's return odds even if its own
                // multiplier falls. Floors/bounds prevent a universal compensating penalty.
                // Retain the bounded collection and commit the failure once, without a new slot.
                ProtectedInsertions++; w.Stage = ExperienceStage.Failed; return true;
            }
            if (reference != null && reference.PlaceId != (placeId ?? meal.PlaceId))
                throw new InvalidOperationException("Business generation changed its place.");
            if (slot < 0) slot = Register(business, placeId ?? meal.PlaceId);
            if (slot >= 0)
            {
                ShopMemory? previous = index < 0 || FoodMemoryPolicy.Expired(entries[index], experienceDay, settings) ? null : entries[index];
                int affinity = previous == null ? 0 : FoodMemoryPolicy.EffectiveAffinity(previous, experienceDay, traits, settings);
                int change = cause == ShopExperience.Served ? settings.FoodMemoryServedGain : -FoodMemoryPolicy.Penalty(cause, traits, settings);
                affinity = Math.Max(-8, Math.Min(8, affinity + change));
                // A failure must not refresh a still-positive habit's decay or expiry clock:
                // otherwise it can outlive the same memory without that failure.
                bool keepAge = cause != ShopExperience.Served && previous != null && affinity > 0;
                if (keepAge) affinity = Math.Max(-8, Math.Min(8, previous!.Affinity + change));
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
                replacement.Day = keepAge ? previous!.Day : experienceDay;
                replacement.Served = served; replacement.Affinity = affinity; replacement.Outcome = cause;
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

        private bool SafeBadInsertion(ConsumerMemory memory, int day, ResidentTraits traits, FoodMemorySettings settings)
        {
            if (memory.Entries.Count < settings.FoodMemoryCapacity) return true;
            int removed = 0, count = memory.Entries.Count;
            double forgotten = FoodMemoryPolicy.Contribution(memory, -1, day, traits, settings);
            while (count >= settings.FoodMemoryCapacity)
            {
                int victim = -1;
                for (int i = 0; i < memory.Entries.Count; i++)
                    if ((removed & (1 << i)) == 0 && (victim < 0 || FoodMemoryPolicy.CompareEviction(memory.Entries[i], Identity(memory.Entries[i].BusinessRef),
                        memory.Entries[victim], Identity(memory.Entries[victim].BusinessRef), day, traits, settings) < 0)) victim = i;
                // Losing a known competitor's admission can raise return odds even if its scalar
                // was below novelty (or memory weight is zero). Also protect future activation.
                if (FoodMemoryPolicy.SuppliesAwareness(memory.Entries[victim], Math.Max(day, food.AwarenessStartsDay), settings)
                    || FoodMemoryPolicy.Contribution(memory, memory.Entries[victim].BusinessRef, day, traits, settings) > forgotten) return false;
                removed |= 1 << victim; count--;
            }
            return true;
        }
    }
}
