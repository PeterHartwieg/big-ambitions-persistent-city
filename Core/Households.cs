using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>GR-6 (G4): the residents of one home, ordered by ID. Visitors have no household. Each member keeps their own
    /// wallet; a household shares a plan item, never money.</summary>
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

    /// <summary>One household's outing on one day: the shared item and the members it invites.</summary>
    public sealed class HouseholdOuting
    {
        internal HouseholdOuting(PlanItem item, IReadOnlyList<string> invited) { Item = item; Invited = invited; }
        public PlanItem Item { get; }
        /// <summary>Members free in the window, ordered by ID; at least two.</summary>
        public IReadOnlyList<string> Invited { get; }
        public bool Invites(string personId)
        {
            for (int i = 0; i < Invited.Count; i++) if (string.Equals(Invited[i], personId, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public enum OutingOutcome { None, Pending, Held, Cancelled }

    /// <summary>
    /// GR-6 (G4, J0 "Households"). At most one shared item per household per day: an <see cref="PlanItemKind.Outing"/> to one
    /// leisure venue, on a workday evening or a weekend afternoon, drawn when at least two members are free in its window.
    /// The draw is a pure function of the household's saved state (members, their work windows, home, catalogue, day, seed),
    /// so every member's plan generation, each at its own day boundary, finds the same item and window.
    /// Cancelling rule: a member not arrived by the latest start is <c>Missed(Late)</c> (<see cref="DayPlanExecutor.Settle"/>)
    /// and no longer sets off. If fewer than two members arrived, the outing is cancelled and an arrived member stays as an
    /// ordinary visit. The outcome is read from the members' own item states, never written across members, so it does not
    /// depend on which member's event runs first. Each member buys with their own contract call and wallet.
    /// </summary>
    public static class HouseholdOutings
    {
        public const double EveningStartMinute = 1170, EveningMinutes = 120, WeekendStartMinute = 900, WeekendMinutes = 150;
        /// <summary>Minutes after the start a member may still arrive.</summary>
        public const double GraceMinutes = 30;
        /// <summary>Chance in percent that a household with two free members goes out: workday evenings, weekend afternoons.</summary>
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

        /// <summary>A member is free when no shift (with the trip to it) overlaps the outing. Shifts come from the saved plan
        /// where one covers the day, else from the same rules, so every member computes the same answer.</summary>
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

        /// <summary>The household's outing outcome on <paramref name="day"/> from its members' saved item states.</summary>
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

        /// <summary>GR-6: plans draw household outings. Off unless the caller turns it on (Runtime does, from its setting), so
        /// a city without it plans exactly as before. Set it the same way on every load: it decides future plans only.</summary>
        public bool HouseholdOutingsEnabled { get; set; }

        private Func<CitizenState, Household?>? HouseholdLookup => HouseholdOutingsEnabled ? householdLookup ??= HouseholdOf : null;

        /// <summary>GR-6: the household of a resident (null for a visitor or a home with nobody else). The index is derived,
        /// never saved: rebuilt for each plan day and whenever the number of persons changes.</summary>
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
            // An event plans on its working copy of the person; the household holds that copy in the live person's place.
            var current = new List<CitizenState>(members.Count);
            bool found = false;
            foreach (var member in members)
            {
                bool self = string.Equals(member.Identity.Id, person.Identity.Id, StringComparison.Ordinal);
                found |= self;
                // The index is per day; a member who moved home since it was built is no longer in this household.
                if (self || member.HomeId == person.HomeId) current.Add(self ? person : member);
            }
            return found && current.Count >= 2 ? new Household(person.HomeId, current) : null;
        }
    }
}
