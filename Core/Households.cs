using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    public sealed class Household
    {
        public Household(string homeId, IReadOnlyList<CitizenState> members)
        {
            HomeId = homeId ?? throw new ArgumentNullException(nameof(homeId));
            var copy = new List<CitizenState>(members ?? throw new ArgumentNullException(nameof(members)));
            foreach (var member in copy)
                if (member == null || member.IsVisitor || member.HomeId != homeId) throw new ArgumentException("A household holds the residents of its own home.", nameof(members));
            copy.Sort((a, b) => string.CompareOrdinal(a.Identity.Id, b.Identity.Id));
            Members = copy;
        }
        public string HomeId { get; }
        public IReadOnlyList<CitizenState> Members { get; }
    }

    public sealed class HouseholdOuting
    {
        internal HouseholdOuting(PlanItem item, IReadOnlyList<string> invited) { Item = item; Invited = invited; }
        public PlanItem Item { get; }
        public IReadOnlyList<string> Invited { get; }
        public bool Invites(string personId)
        {
            for (int i = 0; i < Invited.Count; i++) if (string.Equals(Invited[i], personId, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public enum OutingOutcome { None, Pending, Held, Cancelled }

    public static class HouseholdOutings
    {
        public const double EveningStartMinute = 1170, EveningMinutes = 120, WeekendStartMinute = 900, WeekendMinutes = 150;
        public const double GraceMinutes = 30;
        public const int EveningPercent = 15, WeekendPercent = 40;

        public static HouseholdOuting? Draw(Household household, int citySeed, int day, IReadOnlyList<Place> catalogue,
            IReadOnlyDictionary<string, Place> places)
        {
            if (household == null || catalogue == null || places == null || household.Members.Count < 2 || day < 0) return null;
            if (!places.TryGetValue(household.HomeId, out var home)) return null;
            bool weekend = DayPlanner.Weekend(day);
            uint draw = StableRandom.Finish(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Append(StableRandom.Begin(citySeed), household.HomeId), ":outing:"), day));
            if (draw % 100 >= (uint)(weekend ? WeekendPercent : EveningPercent)) return null;
            double start = day * 1440.0 + (weekend ? WeekendStartMinute : EveningStartMinute);
            double latest = start + GraceMinutes, ends = start + (weekend ? WeekendMinutes : EveningMinutes);
            string key = household.HomeId + ":outing-venue:" + day.ToString(CultureInfo.InvariantCulture);
            string venue = DestinationChoice.Choose(catalogue, home, PlaceKind.Leisure, key, citySeed,
                p => DailyRoutine.Open(p, start) && DailyRoutine.Open(p, latest));
            if (venue.Length == 0) return null;
            var invited = new List<string>(household.Members.Count);
            foreach (var member in household.Members)
                if (Free(member, citySeed, day, start, ends, places)) invited.Add(member.Identity.Id);
            if (invited.Count < 2) return null;
            return new HouseholdOuting(new PlanItem(PlanItemKind.Outing, Activity.Leisure, venue, start, latest, ends, PlanPriority.Wanted), invited);
        }

        private static bool Free(CitizenState member, int citySeed, int day, double start, double ends, IReadOnlyDictionary<string, Place> places)
        {
            if (member.IsVisitor) return false;
            var keys = RoutineKeys.For(member, citySeed);
            for (int d = day - 1; d <= day; d++)
            {
                if (d < 0) continue;
                var work = DayPlanner.WorkFor(member, keys, d, places);
                if (work.HasValue && work.Value.StartsAt - DayPlanner.WorkTravelAllowance < ends && work.Value.EndsAt > start) return false;
            }
            return true;
        }

        public static OutingOutcome Outcome(Household household, int day)
        {
            int invited = 0, arrived = 0, open = 0;
            foreach (var member in household.Members)
            {
                var plan = member.Plan;
                if (plan == null || plan.Day != day) continue;
                for (int i = 0; i < plan.Items.Count; i++)
                {
                    if (plan.Items[i].Kind != PlanItemKind.Outing) continue;
                    invited++;
                    var state = plan.StateOf(i);
                    if (state == PlanItemState.Active || state == PlanItemState.Done) arrived++;
                    else if (!DayPlan.Terminal(state)) open++;
                }
            }
            if (invited == 0) return OutingOutcome.None;
            if (arrived >= 2) return OutingOutcome.Held;
            return open > 0 && arrived + open >= 2 ? OutingOutcome.Pending : OutingOutcome.Cancelled;
        }
    }

    public sealed partial class CitySimulator
    {
        private readonly Dictionary<string, List<CitizenState>> householdMembers = new Dictionary<string, List<CitizenState>>(StringComparer.Ordinal);
        private int householdDay = int.MinValue, householdCitizens = -1;
        private Func<CitizenState, Household?>? householdLookup;

        public bool HouseholdOutingsEnabled { get; set; }

        private Func<CitizenState, Household?>? HouseholdLookup => HouseholdOutingsEnabled ? householdLookup ??= HouseholdOf : null;

        public Household? HouseholdOf(CitizenState person)
        {
            if (person == null || person.IsVisitor || person.HomeId.Length == 0) return null;
            int day = DayPlanner.PlanDay(State.Minute);
            if (day != householdDay || State.Citizens.Count != householdCitizens)
            {
                householdMembers.Clear();
                foreach (var citizen in State.Citizens)
                {
                    if (citizen.IsVisitor || citizen.HomeId.Length == 0) continue;
                    if (!householdMembers.TryGetValue(citizen.HomeId, out var list)) householdMembers.Add(citizen.HomeId, list = new List<CitizenState>(2));
                    list.Add(citizen);
                }
                householdDay = day; householdCitizens = State.Citizens.Count;
            }
            if (!householdMembers.TryGetValue(person.HomeId, out var members) || members.Count < 2) return null;
            var current = new List<CitizenState>(members.Count);
            bool found = false;
            foreach (var member in members)
            {
                bool self = string.Equals(member.Identity.Id, person.Identity.Id, StringComparison.Ordinal);
                found |= self;
                if (self || member.HomeId == person.HomeId) current.Add(self ? person : member);
            }
            return found && current.Count >= 2 ? new Household(person.HomeId, current) : null;
        }
    }
}
