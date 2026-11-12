using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>GR-3 wallet tier, from the person's drawn base income (the household draw's income groups). It sets the
    /// neutral spending band of a need.</summary>
    public enum WalletTier { Working, Middle, Upper }

    /// <summary>One need category: interval and spread in days and a spending band per wallet tier. The category is the
    /// loaded catalogue's business type name (<see cref="Place.BusinessType"/>), so a modded type is a category too.</summary>
    public sealed class NeedProfile
    {
        internal NeedProfile(string category, int intervalDays, int spreadDays, long workingCents, long middleCents, long upperCents)
        {
            if (intervalDays < 1 || spreadDays < 0 || spreadDays >= intervalDays) throw new ArgumentOutOfRangeException(nameof(intervalDays));
            Category = category; IntervalDays = intervalDays; SpreadDays = spreadDays;
            bands = new[] { Money.FromCents(workingCents), Money.FromCents(middleCents), Money.FromCents(upperCents) };
        }
        private readonly Money[] bands;
        public string Category { get; }
        public int IntervalDays { get; }
        public int SpreadDays { get; }
        public Money Band(WalletTier tier) => bands[(int)tier];
        /// <summary>True for a category no table row describes.</summary>
        public bool IsNeutral { get; internal set; }
    }

    /// <summary>One person's memory of one need: when it is due next, where it was last met, and how many times in a row it
    /// was deferred.</summary>
    public sealed class NeedState
    {
        public NeedState(string category, int nextDueDay, string lastVenue, int deferrals)
        {
            if (string.IsNullOrEmpty(category) || category.Length > BusinessTypes.MaximumNameLength) throw new ArgumentException("Invalid need category.", nameof(category));
            if (nextDueDay < 0) throw new ArgumentOutOfRangeException(nameof(nextDueDay));
            if (deferrals < 0 || deferrals > NeedModel.MaximumDeferrals) throw new ArgumentOutOfRangeException(nameof(deferrals));
            Category = category; NextDueDay = nextDueDay; LastVenue = lastVenue ?? ""; Deferrals = deferrals;
        }
        public string Category { get; }
        public int NextDueDay { get; internal set; }
        public string LastVenue { get; internal set; }
        public int Deferrals { get; internal set; }
        public NeedState Copy() => new NeedState(Category, NextDueDay, LastVenue, Deferrals);
    }

    /// <summary>GR-3: a resident's needs, saved with the person. Null until the purchase path first meets a category for
    /// this person, so a city without purchases saves none.</summary>
    public sealed class PersonNeeds
    {
        public const int MaximumNeeds = 16;
        private readonly List<NeedState> needs = new List<NeedState>();
        public PersonNeeds(int lastImpulseDay = -1, string impulseVenue = "")
        {
            if (lastImpulseDay < -1) throw new ArgumentOutOfRangeException(nameof(lastImpulseDay));
            LastImpulseDay = lastImpulseDay; ImpulseVenue = lastImpulseDay < 0 ? "" : impulseVenue ?? "";
        }
        public IReadOnlyList<NeedState> Needs => needs;
        /// <summary>The day of the person's last impulse; at most one per day. -1 for none.</summary>
        public int LastImpulseDay { get; internal set; }
        /// <summary>The venue of that impulse: a visit there on that day buys even when the need is not due.</summary>
        public string ImpulseVenue { get; internal set; }
        public NeedState? Find(string category)
        {
            foreach (var need in needs) if (need.Category == category) return need;
            return null;
        }
        /// <summary>Adds a need in ordinal category order (the wire's order). Refused past <see cref="MaximumNeeds"/>.</summary>
        public bool Add(NeedState need)
        {
            if (need == null) throw new ArgumentNullException(nameof(need));
            if (Find(need.Category) != null) throw new ArgumentException("Duplicate need category.", nameof(need));
            if (needs.Count >= MaximumNeeds) return false;
            int at = 0;
            while (at < needs.Count && string.CompareOrdinal(needs[at].Category, need.Category) < 0) at++;
            needs.Insert(at, need);
            return true;
        }
        public PersonNeeds Copy()
        {
            var copy = new PersonNeeds(LastImpulseDay, ImpulseVenue);
            foreach (var need in needs) copy.needs.Add(need.Copy());
            return copy;
        }
        public bool SameAs(PersonNeeds? other)
        {
            if (other == null || other.LastImpulseDay != LastImpulseDay || other.ImpulseVenue != ImpulseVenue || other.needs.Count != needs.Count) return false;
            for (int i = 0; i < needs.Count; i++)
            {
                var a = needs[i]; var b = other.needs[i];
                if (a.Category != b.Category || a.NextDueDay != b.NextDueDay || a.LastVenue != b.LastVenue || a.Deferrals != b.Deferrals) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// GR-3 need model (J0 "Need model"). A satisfied need is due again after its interval plus or minus a seeded spread; a
    /// deferred one (missed, refused, wallet short) comes due the next day with higher urgency, up to three times, then
    /// waits a full interval. Native customer counts are never read here: they calibrate, they do not drive.
    /// </summary>
    public static class NeedModel
    {
        public const int MaximumDeferrals = 3;
        /// <summary>Impulse rates in basis points (1/10000): the base rate while passing an open business, and the most a
        /// need about to come due adds.</summary>
        public const int ImpulseBaseBasisPoints = 200, ImpulseNeedBasisPoints = 600;

        // Only rows this repository has evidence for would go here; every type, modded ones included, is neutral today.
        private static readonly Dictionary<string, NeedProfile> Rows = new Dictionary<string, NeedProfile>(StringComparer.Ordinal);
        private static readonly Dictionary<string, NeedProfile> Neutrals = new Dictionary<string, NeedProfile>(StringComparer.Ordinal);

        /// <summary>The neutral default: interval 7 days, spread 3 days, band by wallet tier ($40, $80, $160).</summary>
        public static NeedProfile Neutral(string category)
        {
            lock (Neutrals)
            {
                if (!Neutrals.TryGetValue(category, out var profile))
                {
                    profile = new NeedProfile(category, BusinessTypes.NeutralIntervalDays, BusinessTypes.NeutralSpreadDays, 4000, 8000, 16000) { IsNeutral = true };
                    if (Neutrals.Count < 1024) Neutrals.Add(category, profile);
                }
                return profile;
            }
        }

        /// <summary>The profile of a business type, or null for a place that sells nothing (no type).</summary>
        public static NeedProfile? For(string? businessType)
        {
            string type = BusinessTypes.Normalize(businessType);
            if (type.Length == 0 || type.Length > BusinessTypes.MaximumNameLength) return null;
            return Rows.TryGetValue(type, out var row) ? row : Neutral(type);
        }

        public static WalletTier Tier(ResidentBudget wallet)
            => wallet.BaseIncomeCents < 15000 ? WalletTier.Working : wallet.BaseIncomeCents < 24000 ? WalletTier.Middle : WalletTier.Upper;

        /// <summary>The contract's ceiling for this need: the smaller of the wallet's spending limit and the need's band.</summary>
        public static Money Ceiling(NeedProfile profile, ResidentBudget wallet)
            => Money.Min(profile.Band(Tier(wallet)), HouseholdFinance.SpendingLimit(wallet));

        /// <summary>The person's state for this category, created on first sight with a seeded first due day inside one
        /// interval from today, so a new city's needs do not all fall due at once. Null when the person already holds
        /// <see cref="PersonNeeds.MaximumNeeds"/> other categories.</summary>
        public static NeedState? Ensure(CitizenState person, NeedProfile profile, int day, int seed)
        {
            var needs = person.Needs ??= new PersonNeeds();
            var need = needs.Find(profile.Category);
            if (need != null) return need;
            need = new NeedState(profile.Category, day + (int)(Draw(seed, person.Identity.Id, profile.Category, "first", 0) % (uint)profile.IntervalDays), "", 0);
            return needs.Add(need) ? need : null;
        }

        public static bool Due(NeedState need, int day) => need.NextDueDay <= day;

        /// <summary>Days overdue plus deferrals: the order needs are met in when several are due.</summary>
        public static int Urgency(NeedState need, int day) => Math.Max(0, day - need.NextDueDay) + need.Deferrals;

        /// <summary>The need was met at <paramref name="venue"/> on <paramref name="day"/>.</summary>
        public static void Satisfy(CitizenState person, NeedState need, NeedProfile profile, string venue, int day, int seed)
        {
            int spread = profile.SpreadDays;
            int offset = spread == 0 ? 0 : (int)(Draw(seed, person.Identity.Id, need.Category, "next", day) % (uint)(2 * spread + 1)) - spread;
            need.NextDueDay = day + profile.IntervalDays + offset;
            need.LastVenue = venue ?? "";
            need.Deferrals = 0;
        }

        /// <summary>The need was due and not met today (missed, refused, wallet short): due again tomorrow, up to three
        /// times; the fourth deferral waits a full interval.</summary>
        public static void Defer(NeedState need, NeedProfile profile, int day)
        {
            if (need.Deferrals >= MaximumDeferrals) { need.Deferrals = 0; need.NextDueDay = day + profile.IntervalDays; return; }
            need.Deferrals++;
            need.NextDueDay = day + 1;
        }

        /// <summary>
        /// One seeded impulse draw for this person, venue and day while passing an open business. The rate is the base rate
        /// raised by how close the venue's need is to due (full raise when due). At most one impulse per person per day:
        /// a true result records the day.
        /// </summary>
        public static bool Impulse(CitizenState person, NeedProfile profile, string venue, int day, int seed)
        {
            var needs = person.Needs ??= new PersonNeeds();
            if (needs.LastImpulseDay == day) return false;
            if (ImpulseRate(needs.Find(profile.Category), profile, day) <= Draw(seed, person.Identity.Id, venue, "impulse", day) % 10000) return false;
            needs.LastImpulseDay = day; needs.ImpulseVenue = venue;
            return true;
        }

        /// <summary>The impulse rate in basis points for a need state (null: never seen, base rate only).</summary>
        public static int ImpulseRate(NeedState? need, NeedProfile profile, int day)
        {
            if (need == null) return ImpulseBaseBasisPoints;
            int until = need.NextDueDay - day;
            if (until <= 0) return ImpulseBaseBasisPoints + ImpulseNeedBasisPoints;
            if (until >= profile.IntervalDays) return ImpulseBaseBasisPoints;
            return ImpulseBaseBasisPoints + ImpulseNeedBasisPoints * (profile.IntervalDays - until) / profile.IntervalDays;
        }

        /// <summary>The <see cref="PlanItemKind.Need"/> item an impulse inserts: an optional visit of
        /// <paramref name="minutes"/> starting now, only when it ends before the next pending fixed item begins (less the
        /// travel allowance), no other need item is still open (the routine keeps one shop visit a day) and the plan has room.
        /// Null when it does not fit; the plan is never squeezed.</summary>
        public static PlanItem? ImpulseItem(DayPlan plan, string venue, double now, double minutes)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (string.IsNullOrEmpty(venue) || !(minutes > 0) || !Position.Finite(now) || plan.Items.Count >= DayPlan.MaximumItems) return null;
            double ends = now + minutes;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (DayPlan.Terminal(plan.StateOf(i))) continue;
                if (item.Kind == PlanItemKind.Need) return null;
                // Any open item already under way or overlapping is in the way; a later fixed item bounds the visit.
                if (plan.StateOf(i) != PlanItemState.Pending && item.EndsAt > now) return null;
                if (item.Priority == PlanPriority.Fixed && item.EarliestStart - DayPlanner.WorkTravelAllowance < ends && item.EndsAt > now) return null;
            }
            return new PlanItem(PlanItemKind.Need, Activity.Shop, venue, now, now, ends, PlanPriority.Optional);
        }

        /// <summary>Inserts an impulse item in start order; false when it does not fit.</summary>
        public static bool InsertImpulse(CitizenState person, string venue, double now, double minutes)
        {
            var plan = person.Plan;
            if (plan == null) return false;
            var item = ImpulseItem(plan, venue, now, minutes);
            if (item == null) return false;
            var items = new List<PlanItem>(plan.Items.Count + 1);
            var marks = new List<byte>(plan.Items.Count + 1);
            for (int i = 0; i < plan.Items.Count; i++) { items.Add(plan.Items[i]); marks.Add((byte)((int)plan.StateOf(i) | (int)plan.CauseOf(i) << 4)); }
            int at = 0;
            while (at < items.Count && items[at].EarliestStart <= item.EarliestStart) at++;
            items.Insert(at, item); marks.Insert(at, 0);
            person.Plan = plan.WithItems(plan.WorkplaceId, items.ToArray(), marks.ToArray(), plan.NextWork);
            return true;
        }

        private static uint Draw(int seed, string person, string key, string purpose, int day)
            => StableRandom.Finish(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Append(StableRandom.Append(StableRandom.Append(
                StableRandom.Append(StableRandom.Begin(seed), person), ":need:"), key), ':'), purpose), day));
    }
}
