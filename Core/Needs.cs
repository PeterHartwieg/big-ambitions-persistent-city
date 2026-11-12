using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum WalletTier { Working, Middle, Upper }

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
        public bool IsNeutral { get; internal set; }
    }

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
        public int LastImpulseDay { get; internal set; }
        public string ImpulseVenue { get; internal set; }
        public NeedState? Find(string category)
        {
            foreach (var need in needs) if (need.Category == category) return need;
            return null;
        }
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

    public sealed class NeedPlanning
    {
        public NeedPlanning(IEnumerable<Place> venues)
        {
            var list = new List<Place>(venues ?? throw new ArgumentNullException(nameof(venues)));
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Venues = list;
        }
        public IReadOnlyList<Place> Venues { get; }
    }

    public static class NeedModel
    {
        public const int MaximumDeferrals = 3;
        public const int ImpulseBaseBasisPoints = 200, ImpulseNeedBasisPoints = 600;

        private static readonly Dictionary<string, NeedProfile> Rows = new Dictionary<string, NeedProfile>(StringComparer.Ordinal);
        private static readonly Dictionary<string, NeedProfile> Neutrals = new Dictionary<string, NeedProfile>(StringComparer.Ordinal);

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

        public static NeedProfile? For(string? businessType)
        {
            string type = BusinessTypes.Normalize(businessType);
            if (type.Length == 0 || type.Length > BusinessTypes.MaximumNameLength) return null;
            return Rows.TryGetValue(type, out var row) ? row : Neutral(type);
        }

        public static WalletTier Tier(ResidentBudget wallet)
            => wallet.BaseIncomeCents < 15000 ? WalletTier.Working : wallet.BaseIncomeCents < 24000 ? WalletTier.Middle : WalletTier.Upper;

        public static Money Ceiling(NeedProfile profile, ResidentBudget wallet)
            => Money.Min(profile.Band(Tier(wallet)), HouseholdFinance.SpendingLimit(wallet));

        public static NeedState? Ensure(CitizenState person, NeedProfile profile, int day, int seed)
        {
            var needs = person.Needs ??= new PersonNeeds();
            var need = needs.Find(profile.Category);
            if (need != null) return need;
            need = new NeedState(profile.Category, day + (int)(Draw(seed, person.Identity.Id, profile.Category, "first", 0) % (uint)profile.IntervalDays), "", 0);
            return needs.Add(need) ? need : null;
        }

        public static bool Due(NeedState need, int day) => need.NextDueDay <= day;

        public static int Urgency(NeedState need, int day) => Math.Max(0, day - need.NextDueDay) + need.Deferrals;

        public static void Satisfy(CitizenState person, NeedState need, NeedProfile profile, string venue, int day, int seed)
        {
            int spread = profile.SpreadDays;
            int offset = spread == 0 ? 0 : (int)(Draw(seed, person.Identity.Id, need.Category, "next", day) % (uint)(2 * spread + 1)) - spread;
            need.NextDueDay = day + profile.IntervalDays + offset;
            need.LastVenue = venue ?? "";
            need.Deferrals = 0;
        }

        public static void Defer(NeedState need, NeedProfile profile, int day)
        {
            if (need.Deferrals >= MaximumDeferrals) { need.Deferrals = 0; need.NextDueDay = day + profile.IntervalDays; return; }
            need.Deferrals++;
            need.NextDueDay = day + 1;
        }

        public static bool Impulse(CitizenState person, NeedProfile profile, string venue, int day, int seed)
        {
            var needs = person.Needs ??= new PersonNeeds();
            if (needs.LastImpulseDay == day) return false;
            if (ImpulseRate(needs.Find(profile.Category), profile, day) <= Draw(seed, person.Identity.Id, venue, "impulse", day) % 10000) return false;
            needs.LastImpulseDay = day; needs.ImpulseVenue = venue;
            return true;
        }

        public static int ImpulseRate(NeedState? need, NeedProfile profile, int day)
        {
            if (need == null) return ImpulseBaseBasisPoints;
            int until = need.NextDueDay - day;
            if (until <= 0) return ImpulseBaseBasisPoints + ImpulseNeedBasisPoints;
            if (until >= profile.IntervalDays) return ImpulseBaseBasisPoints;
            return ImpulseBaseBasisPoints + ImpulseNeedBasisPoints * (profile.IntervalDays - until) / profile.IntervalDays;
        }

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
                if (plan.StateOf(i) != PlanItemState.Pending && item.EndsAt > now) return null;
                if (item.Priority == PlanPriority.Fixed && item.EarliestStart - DayPlanner.WorkTravelAllowance < ends && item.EndsAt > now) return null;
            }
            return new PlanItem(PlanItemKind.Need, Activity.Shop, venue, now, now, ends, PlanPriority.Optional);
        }

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

        public static string? PlanVenue(CitizenState person, int day, NeedPlanning planning, Place home, PlaceKind kind, double from, double until)
        {
            var needs = person.Needs;
            if (needs == null || planning == null || home == null) return null;
            NeedState? best = null; string? bestVenue = null;
            foreach (var need in needs.Needs)
            {
                if (!Due(need, day) || best != null && Urgency(need, day) <= Urgency(best, day)) continue;
                string? venue = null; double distance = double.MaxValue;
                foreach (var place in planning.Venues)
                {
                    if (place.Kind != kind || BusinessTypes.Normalize(place.BusinessType) != need.Category || !OpenFor(place, from, until)) continue;
                    if (place.Id == need.LastVenue) { venue = place.Id; break; }
                    double d = Position.Distance(home.Position, place.Position);
                    if (d < distance || d == distance && string.CompareOrdinal(place.Id, venue) < 0) { venue = place.Id; distance = d; }
                }
                if (venue != null) { best = need; bestVenue = venue; }
            }
            return bestVenue;
        }

        private static bool OpenFor(Place place, double from, double until)
        {
            var close = DailyRoutine.NextClose(place, from);
            return close.HasValue && close.Value >= until;
        }

        public const double ScheduledVisitMinutes = 45;

        public static PlanItem? ScheduleVisit(CitizenState person, int day, NeedPlanning planning, Place home, PlaceKind kind, IReadOnlyList<PlanItem> planned)
        {
            if (person.Needs == null || planning == null || home == null) return null;
            bool anyDue = false;
            foreach (var need in person.Needs.Needs) if (Due(need, day)) { anyDue = true; break; }
            if (!anyDue) return null;
            for (double start = day * 1440.0 + 600; start <= day * 1440.0 + 1200; start += 30)
            {
                double ends = start + ScheduledVisitMinutes;
                bool clear = true;
                foreach (var item in planned)
                {
                    if (item.Kind == PlanItemKind.Meal) continue;
                    double before = item.Priority == PlanPriority.Fixed ? DayPlanner.WorkTravelAllowance : 30;
                    if (start < item.EndsAt + 30 && ends > item.EarliestStart - before) { clear = false; break; }
                }
                if (!clear) continue;
                string? venue = PlanVenue(person, day, planning, home, kind, start, ends);
                if (venue == null) continue;
                return kind == PlaceKind.Leisure
                    ? new PlanItem(PlanItemKind.Leisure, Activity.Leisure, venue, start, start, ends, PlanPriority.Optional)
                    : new PlanItem(PlanItemKind.Need, Activity.Shop, venue, start, start, ends, PlanPriority.Optional);
            }
            return null;
        }

        public static void EnsureCatalogue(CitizenState person, NeedPlanning planning, int day, int seed)
        {
            if (person.IsVisitor || person.Wallet == null || planning == null) return;
            foreach (var place in planning.Venues)
            {
                var profile = For(place.BusinessType);
                if (profile != null) Ensure(person, profile, day, seed);
            }
        }

        private static uint Draw(int seed, string person, string key, string purpose, int day)
            => StableRandom.Finish(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Append(StableRandom.Append(StableRandom.Append(
                StableRandom.Append(StableRandom.Begin(seed), person), ":need:"), key), ':'), purpose), day));
    }
}
