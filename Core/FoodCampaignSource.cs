using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public interface IFoodCampaignAwarenessProvider : IFoodAwarenessProvider
    {
        bool HasEvidence { get; }
        void Prepare(string consumer, int day, double minute, ResidentTraits traits, bool commuteRoutine);
    }

    /// <summary>Event-only publication/index build. A query visits fixed group/day/type buckets and eight
    /// indexed slots, never the business collection. No resident rows or persistent exposure cache.</summary>
    public sealed class FoodCampaignSource : IFoodCampaignAwarenessProvider
    {
        private readonly FoodCommerceState food;
        private readonly FoodMemoryStore references;
        private readonly int seed;
        private readonly FoodCampaignSettings settings;
        private readonly FoodCampaignTargetPolicy policy;
        private List<Entry>?[] index = new List<Entry>?[8 * 8 * 8];
        private List<Entry>?[] scratch = new List<Entry>?[8 * 8 * 8];
        private readonly string?[] selected = new string?[8];
        private readonly int[] bucketKeys = new int[8 * 8 * 8], cumulative = new int[8 * 8 * 8];
        private string preparedConsumer = "";
        private int preparedDay, newestDay = -1;
        private struct Entry
        {
            public string Id;
            public uint Hours;
        }
        public FoodCampaignSource(FoodCommerceState food, int seed, FoodCampaignSettings settings, FoodCampaignTargetPolicy policy)
        {
            this.food = food; this.seed = seed; this.settings = settings; this.policy = policy;
            references = new FoodMemoryStore(food);
            if (settings.Enabled && food.Campaigns.Count != 0) Rebuild();
        }
        public bool HasEvidence => settings.Enabled && settings.ReachProbability > 0 && IndexedEntries > 0;
        public long Admitted { get; private set; }
        public long Refused { get; private set; }
        public long Publications { get; private set; }
        public int IndexedEntries { get; private set; }
        public FoodCampaignGroup ReviewedGroup(int type) => policy.Group(type);

        /// <summary>Only a complete C1 image may enter. Snapshot minute is the actual observed boundary,
        /// never the lagging simulation clock or an inferred campaign start. Retired generations drop here.</summary>
        public void Publish(CampaignSnapshot snapshot, IReadOnlyDictionary<string, string> eligiblePlaces)
        {
            if (!settings.Enabled) return;
            double minute = snapshot.AtMinute;
            if (!Position.Finite(minute) || minute < 0 || minute > 1e9 || minute % 60 != 0 || snapshot.Businesses.Count > 1024)
                throw new ArgumentException("Campaign publication requires an actual hourly boundary and bounded complete image.");
            int hour = (int)(minute / 60);
            var sorted = new List<BusinessCampaignSnapshot>(snapshot.Businesses.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var business in snapshot.Businesses)
            {
                if (business == null || !seen.Add(business.BusinessId) || !eligiblePlaces.ContainsKey(business.BusinessId)
                    || business.Campaigns.Count > 8) throw new ArgumentException("Invalid campaign business scope.");
                sorted.Add(business);
            }
            foreach (var image in food.Campaigns) if (hour <= image.ThroughHour) throw new ArgumentException("Observation already published or backdated.");
            sorted.Sort((a, b) => StringComparer.Ordinal.Compare(a.BusinessId, b.BusinessId));
            var old = new Dictionary<int, FoodCampaignImage>();
            foreach (var image in food.Campaigns) old.Add(image.BusinessRef, image);
            var next = new List<FoodCampaignImage>(sorted.Count);
            foreach (var business in sorted)
            {
                int types = 0, duplicates = 0, observed = 0;
                foreach (var campaign in business.Campaigns)
                {
                    if (!(campaign.TypeCode is int type) || type < 0 || type >= 8) continue;
                    if ((observed & (1 << type)) != 0) duplicates |= 1 << type;
                    observed |= 1 << type;
                    if (business.Status != CampaignReadStatus.Refused && campaign.Status == CampaignReadStatus.Observed
                        && campaign.Enabled == true && campaign.AgencyId != null && CampaignTypeGroups.VerifiedName(type) == campaign.TypeName
                        && campaign.TypeName != null) types |= 1 << type;
                }
                types &= ~duplicates;
                int reference = references.Find(business.BusinessId);
                if (reference < 0 && types == 0) continue;
                if (reference < 0) reference = references.RegisterCampaign(business.BusinessId, eligiblePlaces[business.BusinessId]);
                if (reference < 0) continue; // table saturation cannot fabricate a generation
                old.TryGetValue(reference, out var previous);
                var image = FoodCampaignImage.Observe(previous, reference, hour, types);
                if (image != null) next.Add(image);
            }
            next.Sort((a, b) => a.BusinessRef.CompareTo(b.BusinessRef));
            // Build everything privately. Neither a partially merged image nor partial indexes escape.
            var built = Build(next, out int day, out int entries);
            food.Campaigns = next; scratch = index; index = built; newestDay = day; IndexedEntries = entries; Publications++;
        }
        private List<Entry>?[] Build(List<FoodCampaignImage> images, out int day, out int entries)
        {
            var result = scratch;
            foreach(var bucket in result) bucket?.Clear();
            day = -1; entries = 0;
            foreach (var image in images) day = Math.Max(day, image.Day);
            // Sort business identities once, then all fixed buckets inherit ordinal order. The
            // scratch buckets retain capacity across publications; the live image is never cleared.
            var ordered = new List<KeyValuePair<string,FoodCampaignImage>>(images.Count);
            foreach(var image in images) ordered.Add(new KeyValuePair<string,FoodCampaignImage>(references.Identity(image.BusinessRef),image));
            ordered.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Key,b.Key));
            foreach (var pair in ordered)
            {
                var image=pair.Value; string id=pair.Key;
                for (int age = 0; age < 8; age++) for (int type = 0; type < 8; type++)
                {
                    int group = (int)policy.Group(type); uint hours = image.Mask(day - age, type);
                    if (group == 0 || hours == 0) continue;
                    int key = group * 64 + age * 8 + type;
                    (result[key] ??= new List<Entry>()).Add(new Entry { Id = id, Hours = hours }); entries++;
                }
            }
            return result;
        }
        public void Rebuild()
        {
            if (!settings.Enabled || food.Campaigns.Count == 0) return;
            var built = Build(food.Campaigns, out int day, out int entries);
            scratch=index;index=built;newestDay=day;IndexedEntries=entries;
        }
        private uint Hash(string consumer, int day, int slot, string domain)
        {
            uint hash = StableRandom.Append(StableRandom.Begin(seed), domain);
            hash = StableRandom.AppendDecimal(StableRandom.Append(hash, '|'), consumer.Length);
            hash = StableRandom.Append(StableRandom.Append(hash, ':'), consumer);
            hash = StableRandom.AppendDecimal(StableRandom.Append(hash, '|'), day);
            return StableRandom.AppendDecimal(StableRandom.Append(hash, '|'), slot);
        }
        public void Prepare(string consumer, int day, double minute, ResidentTraits traits, bool commuteRoutine)
        {
            if (!HasEvidence) return;
            preparedConsumer = consumer; preparedDay = day; Array.Clear(selected, 0, 8);
            int membership = (int)FoodCampaignTargetPolicy.Membership(traits, commuteRoutine), count = 0, mass = 0;
            // Fixed 7 group signatures x 8 days x 8 types; no consumer/campaign cross product.
            for (int group = 1; group < 8; group++)
            {
                if ((group & membership) == 0) continue;
                for (int age = 0; age < 8; age++)
                {
                    int exposureDay = newestDay - age, elapsed = day - exposureDay;
                    if (elapsed < 0 || elapsed > settings.RetentionDays) continue;
                    for (int type = 0; type < 8; type++)
                    {
                        int key = group * 64 + age * 8 + type; var bucket = index[key];
                        if (bucket == null) continue;
                        mass += bucket.Count; bucketKeys[count] = key; cumulative[count++] = mass;
                    }
                }
            }
            if (mass == 0) return;
            for (int slot = 0; slot < 8; slot++)
            {
                int draw = (int)(StableRandom.Finish(Hash(consumer, day, slot, "pc:campaign:slot:1")) % (uint)mass);
                int bucket = 0; while (draw >= cumulative[bucket]) bucket++;
                var entry = index[bucketKeys[bucket]]![draw - (bucket == 0 ? 0 : cumulative[bucket - 1])];
                int exposureDay = newestDay - bucketKeys[bucket] % 64 / 8;
                int lastHour = (int)(minute / 60) - exposureDay * 24;
                uint permitted = lastHour < 0 ? 0 : lastHour >= 23 ? 0xffffff : (1u << (lastHour + 1)) - 1;
                // Reach is shared across overlapping groups/types for the same consumer/business/day.
                uint gate = StableRandom.Finish(StableRandom.Append(Hash(consumer, exposureDay, 0, "pc:campaign:reach:1"), entry.Id));
                double probability = Math.Min(1, settings.ReachProbability * settings.TargetMatchMultiplier)
                    * Math.Pow(settings.DailyReachDecay, day - exposureDay);
                bool admitted = (entry.Hours & permitted) != 0 && (gate + .5) / 4294967296.0 < probability;
                for (int previous = 0; previous < slot; previous++) if (selected[previous] == entry.Id) admitted = false;
                if (admitted) { selected[slot] = entry.Id; Admitted++; } else Refused++;
            }
        }
        public string? Reference(string consumer, int day, int slot)
            => HasEvidence && consumer == preparedConsumer && day == preparedDay && slot >= 0 && slot < 8 ? selected[slot] : null;
    }
}
