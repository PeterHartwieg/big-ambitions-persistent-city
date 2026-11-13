using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum ShiftType { Early, Day, Late, Night }

    public enum PlanItemKind { Work, Meal, Need, Leisure, Outing, Home }
    public enum PlanPriority { Fixed, Wanted, Optional }
    public enum PlanItemState { Pending, Travelling, Active, Done, Missed, Cancelled }
    public enum PlanMissCause { None, Closed, Late, NoRoute, Refused }

    public sealed class PlanItem
    {
        public PlanItem(PlanItemKind kind, Activity activity, string venueId, double earliestStart, double latestStart, double endsAt,
            PlanPriority priority, MealKind meal = MealKind.Legacy)
        {
            if (!Position.Finite(earliestStart) || earliestStart < 0 || !(latestStart >= earliestStart) || !(endsAt >= latestStart) || !Position.Finite(endsAt))
                throw new ArgumentOutOfRangeException(nameof(earliestStart), "A plan item window must be ordered and finite.");
            Kind = kind; Activity = activity; VenueId = venueId ?? ""; EarliestStart = earliestStart; LatestStart = latestStart; EndsAt = endsAt;
            Priority = priority; Meal = meal;
        }
        public PlanItemKind Kind { get; }
        public Activity Activity { get; }
        public string VenueId { get; }
        public double EarliestStart { get; }
        public double LatestStart { get; }
        public double EndsAt { get; }
        public double Duration => EndsAt - EarliestStart;
        public PlanPriority Priority { get; }
        public MealKind Meal { get; }
    }

    public sealed class DayPlan
    {
        private readonly PlanItem[] items;
        private readonly byte[] marks;

        public DayPlan(int day, uint seed, ShiftType shift, int daysOff, string workplaceId, IReadOnlyList<PlanItem> items, PlanItem? nextWork = null)
            : this(day, seed, shift, daysOff, workplaceId, Copy(items), null, nextWork) { }

        private DayPlan(int day, uint seed, ShiftType shift, int daysOff, string workplaceId, PlanItem[] items, byte[]? marks, PlanItem? nextWork)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (daysOff < 0 || daysOff > 127) throw new ArgumentOutOfRangeException(nameof(daysOff));
            if (items.Length > MaximumItems) throw new ArgumentException("Too many plan items.", nameof(items));
            if (nextWork != null && (nextWork.Kind != PlanItemKind.Work || DayOf(nextWork) <= day))
                throw new ArgumentException("The next shift is a work item after the plan's day.", nameof(nextWork));
            Day = day; Seed = seed; Shift = shift; DaysOff = daysOff; WorkplaceId = workplaceId ?? "";
            this.items = items; this.marks = marks ?? new byte[items.Length]; NextWork = nextWork;
        }

        internal static int DayOf(PlanItem item) => (int)Math.Floor(item.EarliestStart / 1440);

        public const int MaximumItems = 8;
        public int Day { get; }
        public uint Seed { get; }
        public ShiftType Shift { get; }
        public int DaysOff { get; }
        public string WorkplaceId { get; }
        public IReadOnlyList<PlanItem> Items => items;
        public PlanItem? NextWork { get; }
        public PlanItemState StateOf(int index) => (PlanItemState)(marks[index] & 15);
        public PlanMissCause CauseOf(int index) => (PlanMissCause)(marks[index] >> 4);

        public void Restore(int index, PlanItemState state, PlanMissCause cause)
        {
            if ((state == PlanItemState.Missed) != (cause != PlanMissCause.None)) throw new ArgumentException("A missed item needs exactly one cause.");
            marks[index] = (byte)((int)state | (int)cause << 4);
        }

        internal bool Set(int index, PlanItemState state, PlanMissCause cause = PlanMissCause.None)
        {
            var current = StateOf(index);
            if (Terminal(current) || current == state) return false;
            if (!Terminal(state) && state < current) return false;
            Restore(index, state, state == PlanItemState.Missed ? (cause == PlanMissCause.None ? PlanMissCause.Late : cause) : PlanMissCause.None);
            return true;
        }

        public static bool Terminal(PlanItemState state) => state >= PlanItemState.Done;

        public DayPlan Copy() => new DayPlan(Day, Seed, Shift, DaysOff, WorkplaceId, items, (byte[])marks.Clone(), NextWork);

        internal DayPlan WithItems(string workplaceId, PlanItem[] replacement, byte[] replacementMarks, PlanItem? nextWork)
            => new DayPlan(Day, Seed, Shift, DaysOff, workplaceId, replacement, replacementMarks, nextWork);

        public bool SameAs(DayPlan other)
        {
            if (other == null || other.Day != Day || other.Seed != Seed || other.Shift != Shift || other.DaysOff != DaysOff
                || other.WorkplaceId != WorkplaceId || other.items.Length != items.Length
                || (NextWork == null) != (other.NextWork == null) || NextWork != null && !Same(NextWork, other.NextWork!)) return false;
            for (int i = 0; i < items.Length; i++)
                if (!Same(items[i], other.items[i]) || marks[i] != other.marks[i]) return false;
            return true;
        }

        private static bool Same(PlanItem a, PlanItem b) => a.Kind == b.Kind && a.Activity == b.Activity && a.VenueId == b.VenueId
            && a.EarliestStart == b.EarliestStart && a.LatestStart == b.LatestStart && a.EndsAt == b.EndsAt && a.Priority == b.Priority && a.Meal == b.Meal;

        private static PlanItem[] Copy(IReadOnlyList<PlanItem> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new PlanItem[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i] ?? throw new ArgumentException("Null plan item.");
            return copy;
        }
    }

    internal struct RoutineDay
    {
        public bool Present;
        public WorkWindow? Work;
        public double ShopFrom, ShopUntil, LeisureFrom, LeisureUntil;
        public string ShopVenue, LeisureVenue;
        public bool LeisureChosen;
        public bool HasShop => ShopUntil > ShopFrom;
        public bool HasLeisure => LeisureUntil > LeisureFrom;
    }

    public static class DayPlanner
    {
        public const double DayBoundaryMinute = 240;
        public const int DefaultDaysOff = 0b1110000;
        private const double MaxMinute = 1000000000;

        public static int PlanDay(double minute) => (int)Math.Floor((minute - DayBoundaryMinute) / 1440);

        public static ShiftType ShiftOf(CitizenState person) => person.Identity.Persona == Persona.NightOwl ? ShiftType.Late
            : person.Identity.Persona == Persona.Parent ? ShiftType.Early : ShiftType.Day;

        internal static int ShiftStart(ShiftType shift)
            => shift == ShiftType.Early ? 465 : shift == ShiftType.Late ? 720 : shift == ShiftType.Night ? 1320 : 510;

        public static DayPlan Generate(CitizenState person, int citySeed, int day, IReadOnlyList<Place>? catalogue,
            IReadOnlyDictionary<string, Place>? places, bool meals, NeedPlanning? needs = null, Household? household = null)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            var keys = RoutineKeys.For(person, citySeed);
            var quiet = new DailyRhythm.Lookup(person, keys);
            var routine = Draw(person, keys, day, places, ref quiet);
            var prior = person.Plan;
            PlanItem? carried = prior != null && prior.WorkplaceId == person.DayPlaceId && prior.Day < day ? prior.NextWork : null;
            if (carried != null && DayPlan.DayOf(carried) == day) routine.Work = Window(carried);
            Place? planHome = null;
            if (catalogue != null && places != null) places.TryGetValue(person.HomeId, out planHome);
            if (planHome != null) Nightlife(person, keys, day, catalogue!, places!, planHome, ref routine, ref quiet);
            DropOverlaps(ref routine);
            var items = new List<PlanItem>(6);
            if (routine.Work.HasValue)
            {
                var w = routine.Work.Value;
                items.Add(new PlanItem(PlanItemKind.Work, w.Activity, w.PlaceId, w.StartsAt, w.EndsAt, w.EndsAt, PlanPriority.Fixed));
            }
            if (meals)
                for (var kind = MealKind.Breakfast; kind <= MealKind.Dinner; kind++)
                {
                    double desired = MealRhythm.DrawDesiredAt(person, citySeed, day, kind);
                    double expires = MealRhythm.DrawExpiresAt(person, citySeed, day, kind, desired);
                    if (desired >= 0 && expires >= desired && Position.Finite(expires))
                        items.Add(new PlanItem(PlanItemKind.Meal, Activity.Home, "", desired, expires, expires, PlanPriority.Wanted, kind));
                }
            if (needs != null) NeedModel.EnsureCatalogue(person, needs, day, citySeed);
            bool planNeeds = needs != null && planHome != null;
            PlanItem? shopFallback = null, leisureFallback = null;
            if (routine.HasShop)
            {
                string? venue = planNeeds ? NeedModel.PlanVenue(person, day, needs!, planHome!, PlaceKind.Shop, routine.ShopFrom, routine.ShopUntil) : null;
                var drawn = new PlanItem(PlanItemKind.Need, Activity.Shop, venue ?? routine.ShopVenue, routine.ShopFrom, routine.ShopUntil, routine.ShopUntil, PlanPriority.Optional);
                if (venue != null || !planNeeds) items.Add(drawn); else shopFallback = drawn;
            }
            if (routine.HasLeisure)
            {
                string venue = routine.LeisureVenue;
                string? needVenue = planNeeds ? NeedModel.PlanVenue(person, day, needs!, planHome!, PlaceKind.Leisure, routine.LeisureFrom, routine.LeisureUntil) : null;
                if (planHome != null) venue = needVenue ?? (routine.LeisureChosen ? venue : ChooseLeisure(keys, day, catalogue!, planHome, venue));
                var drawn = new PlanItem(PlanItemKind.Leisure, Activity.Leisure, venue, routine.LeisureFrom, routine.LeisureUntil, routine.LeisureUntil, PlanPriority.Optional);
                if (needVenue != null || !planNeeds) items.Add(drawn); else leisureFallback = drawn;
            }
            if (planNeeds)
            {
                var others = new List<PlanItem>(items);
                if (leisureFallback != null) others.Add(leisureFallback);
                if (!routine.HasShop || shopFallback != null)
                {
                    var shop = NeedModel.ScheduleVisit(person, day, needs!, planHome!, PlaceKind.Shop, others);
                    if (shop != null) { items.Add(shop); others.Add(shop); }
                    else if (shopFallback != null) { items.Add(shopFallback); others.Add(shopFallback); }
                }
                if (leisureFallback != null) others.Remove(leisureFallback);
                if (!routine.HasLeisure || leisureFallback != null)
                {
                    var leisure = NeedModel.ScheduleVisit(person, day, needs!, planHome!, PlaceKind.Leisure, others);
                    if (leisure != null) items.Add(leisure);
                    else if (leisureFallback != null) items.Add(leisureFallback);
                }
            }
            if (household != null && planHome != null)
            {
                var outing = HouseholdOutings.Draw(household, citySeed, day, catalogue!, places!);
                if (outing != null && outing.Invites(person.Identity.Id)) AddOuting(items, outing.Item);
            }
            items.Sort((a, b) => { int c = a.EarliestStart.CompareTo(b.EarliestStart); return c != 0 ? c : a.Kind.CompareTo(b.Kind); });
            uint seed = StableRandom.Finish(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Append(StableRandom.Begin(citySeed), keys.Id), ":plan:"), day));
            var next = carried != null && DayPlan.DayOf(carried) > day ? carried : DrawNextWork(person, keys.HourShift, day, places);
            return new DayPlan(day, seed, ShiftOf(person, places), DaysOff(person, places), person.DayPlaceId, items, next);
        }

        private static void AddOuting(List<PlanItem> items, PlanItem outing)
        {
            if (items.Count >= DayPlan.MaximumItems) return;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                double before = item.Priority == PlanPriority.Fixed ? WorkTravelAllowance : 0;
                if (item.Kind != PlanItemKind.Meal && item.Kind != PlanItemKind.Leisure && item.EarliestStart - before < outing.EndsAt && item.EndsAt > outing.EarliestStart) return;
            }
            items.RemoveAll(item => item.Kind == PlanItemKind.Leisure && item.EarliestStart < outing.EndsAt && item.EndsAt > outing.EarliestStart);
            items.Add(outing);
        }

        public const double NightlifeStartMinute = 1290, NightlifeMinutes = 150;

        internal static void Nightlife(CitizenState person, RoutineKeys keys, int day, IReadOnlyList<Place> catalogue,
            IReadOnlyDictionary<string, Place> places, Place home, ref RoutineDay routine, ref DailyRhythm.Lookup quiet)
        {
            var persona = person.Identity.Persona;
            if (person.IsVisitor || !routine.Present || persona == Persona.Parent || persona == Persona.Retiree || !DayOff(person, day + 1, places)) return;
            uint draw = StableRandom.Finish(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Append(StableRandom.Begin(keys.Seed), keys.Id), ":nightlife:"), day));
            if (draw % 3 != 0) return;
            double from = day * 1440.0 + NightlifeStartMinute + keys.HourShift;
            double until = quiet.OptionalEndsAt(from, Math.Min(MaxMinute, from + NightlifeMinutes));
            if (until <= from) return;
            string venue = DestinationChoice.Choose(catalogue, home, PlaceKind.Leisure,
                keys.Id + ":nightlife-venue:" + day.ToString(System.Globalization.CultureInfo.InvariantCulture), keys.Seed,
                p => WorkSchedules.IsNightlife(p.BusinessType) && DailyRoutine.Open(p, from));
            if (venue.Length == 0) return;
            routine.LeisureFrom = from; routine.LeisureUntil = until; routine.LeisureVenue = venue; routine.LeisureChosen = true;
        }

        internal static PlanItem? DrawNextWork(CitizenState person, int hourShift, int day, IReadOnlyDictionary<string, Place>? places)
        {
            for (int offset = 1; offset <= 8; offset++)
            {
                var w = WorkOn(person, day + offset, hourShift, places);
                if (w.HasValue) return WorkItem(w.Value);
            }
            return null;
        }

        internal static PlanItem WorkItem(WorkWindow w) => new PlanItem(PlanItemKind.Work, w.Activity, w.PlaceId, w.StartsAt, w.EndsAt, w.EndsAt, PlanPriority.Fixed);
        internal static WorkWindow Window(PlanItem item) => new WorkWindow(item.VenueId, item.Activity, item.EarliestStart, item.EndsAt);

        public const double WorkTravelAllowance = 60;

        internal static void DropOverlaps(ref RoutineDay routine)
        {
            if (!routine.Work.HasValue) return;
            double from = routine.Work.Value.StartsAt - WorkTravelAllowance, until = routine.Work.Value.EndsAt;
            if (routine.HasShop && routine.ShopFrom < until && routine.ShopUntil > from) routine.ShopFrom = routine.ShopUntil = 0;
            if (routine.HasLeisure && routine.LeisureFrom < until && routine.LeisureUntil > from) routine.LeisureFrom = routine.LeisureUntil = 0;
        }

        internal static string ChooseLeisure(RoutineKeys keys, int day, IReadOnlyList<Place> catalogue, Place home, string fallback)
        {
            uint key = StableRandom.Append(StableRandom.AppendDecimal(keys.LeisureChoiceDay, day), ':');
            string selected = DestinationChoice.Choose(catalogue, home, PlaceKind.Leisure, key);
            return selected == "" ? fallback : selected;
        }

        internal static RoutineDay On(CitizenState person, RoutineKeys keys, int day, IReadOnlyDictionary<string, Place>? places, ref DailyRhythm.Lookup quiet)
        {
            var plan = person.Plan;
            if (plan == null || plan.Day != day)
            {
                var drawn = Draw(person, keys, day, places, ref quiet);
                drawn.Work = WorkFor(person, keys, day, places);
                DropOverlaps(ref drawn);
                return drawn;
            }
            var result = new RoutineDay { Present = true, ShopVenue = "", LeisureVenue = "", LeisureChosen = true };
            bool replan = plan.WorkplaceId != person.DayPlaceId;
            if (replan) result.Work = WorkOn(person, day, keys.HourShift, places);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                if (plan.StateOf(i) == PlanItemState.Cancelled) continue;
                var item = plan.Items[i];
                switch (item.Kind)
                {
                    case PlanItemKind.Work:
                        if (!replan) result.Work = new WorkWindow(item.VenueId, item.Activity, item.EarliestStart, item.EndsAt);
                        break;
                    case PlanItemKind.Need:
                        result.ShopFrom = item.EarliestStart; result.ShopUntil = item.EndsAt; result.ShopVenue = item.VenueId; break;
                    case PlanItemKind.Leisure:
                        result.LeisureFrom = item.EarliestStart; result.LeisureUntil = item.EndsAt; result.LeisureVenue = item.VenueId; break;
                    case PlanItemKind.Outing:
                        if (plan.StateOf(i) == PlanItemState.Missed) break;
                        result.LeisureFrom = item.EarliestStart; result.LeisureUntil = item.EndsAt; result.LeisureVenue = item.VenueId; break;
                }
            }
            return result;
        }

        internal static WorkWindow? WorkFor(CitizenState person, RoutineKeys keys, int day, IReadOnlyDictionary<string, Place>? places)
            => Planned(person, day, out var work) ? work : WorkOn(person, day, keys.HourShift, places);

        internal static bool Planned(CitizenState person, int day, out WorkWindow? work)
        {
            work = null;
            var plan = person.Plan;
            if (plan == null || plan.WorkplaceId != person.DayPlaceId || day < plan.Day) return false;
            if (plan.Day == day)
            {
                for (int i = 0; i < plan.Items.Count; i++)
                    if (plan.Items[i].Kind == PlanItemKind.Work && plan.StateOf(i) != PlanItemState.Cancelled) { work = Window(plan.Items[i]); break; }
                return true;
            }
            if (plan.NextWork == null) return false;
            int next = DayPlan.DayOf(plan.NextWork);
            if (day > next) return false;
            if (day == next) work = Window(plan.NextWork);
            return true;
        }

        internal static RoutineDay Draw(CitizenState person, RoutineKeys keys, int day, IReadOnlyDictionary<string, Place>? places, ref DailyRhythm.Lookup quiet)
        {
            var result = new RoutineDay { ShopVenue = person.ShopId, LeisureVenue = person.LeisureId };
            int shift = keys.HourShift;
            result.Work = WorkOn(person, day, shift, places);
            bool weekend = person.IsVisitor ? Weekend(day) : DayOff(person, day, places);
            bool dayVisit = person.Visitor?.Stay.Pattern == VisitorPattern.DayVisit;
            if (person.IsVisitor && !VisitorSchedules.PresentOn(keys, day, dayVisit ? VisitorPattern.DayVisit : VisitorPattern.Commuter)) return result;
            result.Present = true;
            double midnight = day * 1440.0;
            bool late = person.Identity.Persona == Persona.NightOwl;
            double shopFrom = midnight + (late ? 1230 : weekend ? 660 : 1050) + shift;
            double shopUntil = Math.Min(MaxMinute, shopFrom + 45);
            if (person.ShopId.Length > 0 && shopUntil > shopFrom
                && (dayVisit || person.Identity.Persona == Persona.Foodie || RoutineKeys.Draw(keys.ShopDay, day) % 3 != 0))
            {
                double until = quiet.OptionalEndsAt(shopFrom, shopUntil);
                if (until > shopFrom) { result.ShopFrom = shopFrom; result.ShopUntil = until; }
            }
            double leisureFrom = midnight + (late ? 1320 : weekend ? 840 : 1140) + shift;
            double leisureUntil = Math.Min(MaxMinute, leisureFrom + (weekend ? 120 : 75));
            if (person.LeisureId.Length > 0 && leisureUntil > leisureFrom
                && (dayVisit || weekend || person.Identity.Persona == Persona.Sporty || late || RoutineKeys.Draw(keys.LeisureDay, day) % 2 == 0))
            {
                double until = quiet.OptionalEndsAt(leisureFrom, leisureUntil);
                if (until > leisureFrom) { result.LeisureFrom = leisureFrom; result.LeisureUntil = until; }
            }
            return result;
        }

        internal static bool Weekend(int day) => (DefaultDaysOff >> Weekday(day) & 1) != 0;
        internal static int Weekday(int day) => ((day - 1) % 7 + 7) % 7;

        public static int DaysOff(CitizenState person, IReadOnlyDictionary<string, Place>? places) => DaysOff(person, Workplace(person, places));

        private static int DaysOff(CitizenState person, Place? workplace)
            => person.IsVisitor || workplace == null || workplace.BusinessType.Length == 0 ? DefaultDaysOff
                : WorkSchedules.DaysOffFor(workplace, person.Identity.Id ?? "");

        private static Place? Workplace(CitizenState person, IReadOnlyDictionary<string, Place>? places)
            => places != null && person.DayPlaceId.Length != 0 && places.TryGetValue(person.DayPlaceId, out var workplace) ? workplace : null;

        public static bool DayOff(CitizenState person, int day, IReadOnlyDictionary<string, Place>? places)
            => (DaysOff(person, places) >> Weekday(day) & 1) != 0;

        public static ShiftType ShiftOf(CitizenState person, IReadOnlyDictionary<string, Place>? places) => ShiftOf(person, Workplace(person, places));

        private static ShiftType ShiftOf(CitizenState person, Place? workplace)
        {
            var shift = workplace == null || person.Identity.Persona == Persona.Student ? null : WorkSchedules.ShiftAt(workplace, person.Identity.Id ?? "");
            return shift ?? ShiftOf(person);
        }

        internal static WorkWindow? WorkOn(CitizenState person, int day, int hourShift, IReadOnlyDictionary<string, Place>? places)
        {
            if (person.DayPlaceId.Length == 0 || person.Identity.Persona == Persona.Retiree) return null;
            var workplace = Workplace(person, places);
            if ((DaysOff(person, workplace) >> Weekday(day) & 1) != 0) return null;
            var shift = ShiftOf(person, workplace);
            double start = day * 1440.0 + ShiftStart(shift) + hourShift;
            if (workplace != null)
            {
                if (WorkplaceStaffing.IsWorkforceVenue(workplace))
                {
                    var staffed = WorkplaceStaffing.ShiftStart(workplace, day, shift);
                    if (!staffed.HasValue) return null;
                    start = staffed.Value;
                }
                else start = FitShift(workplace, day, start, shift);
            }
            return new WorkWindow(person.DayPlaceId, person.Identity.Persona == Persona.Student ? Activity.Study : Activity.Work, start, start + 480);
        }

        internal static double FitShift(Place workplace, int day, double start, ShiftType shift)
        {
            if (start + 480 + 10080 > MaxMinute) return start;
            double? close = DailyRoutine.NextClose(workplace, start);
            if (close.HasValue && close.Value >= start + 480) return start;
            double midnight = day * 1440.0, best = start, bestDistance = double.PositiveInfinity;
            foreach (var interval in DailyRoutine.Intervals(workplace, midnight))
            {
                double from = Math.Max(interval.From, midnight), last = Math.Min(interval.To - 480, midnight + 1439);
                if (last < from) continue;
                double candidate = shift == ShiftType.Early ? from : shift == ShiftType.Late ? last
                    : Math.Max(from, Math.Min(last, start));
                double distance = Math.Abs(candidate - start);
                if (distance < bestDistance) { best = candidate; bestDistance = distance; }
            }
            return best;
        }
    }

    public static class WorkSchedules
    {
        public const string School = "ba:businesstype_school";
        private static readonly HashSet<string> NightlifeTypes = new HashSet<string>(StringComparer.Ordinal)
        { "ba:businesstype_nightclub", "ba:businesstype_casino" };
        private static readonly HashSet<string> EveningTypes = new HashSet<string>(StringComparer.Ordinal)
        { "ba:businesstype_cinema", "ba:businesstype_theater" };

        public static bool IsNightlife(string? type) => type != null && NightlifeTypes.Contains(type);
        public static bool IsEvening(string? type) => type != null && EveningTypes.Contains(type);

        public static bool VariesDaysOff(Place workplace) => (OpenWeekdays(workplace) & DayPlanner.DefaultDaysOff) != 0;

        private static int OpenWeekdays(Place workplace)
        {
            if (workplace.Kind != PlaceKind.Work || workplace.BusinessType.Length == 0 || workplace.BusinessType == School) return 0;
            int open = 0;
            var hours = workplace.WeeklyHours;
            for (int i = 0; i < hours.Count; i++)
                if (hours[i].Day >= 0 && hours[i].Day <= 6 && hours[i].ToMinute != hours[i].FromMinute) open |= 1 << hours[i].Day;
            return open;
        }

        internal static uint DaysOffDraw(string id)
            => StableRandom.Finish(StableRandom.Append(StableRandom.Append(StableRandom.Begin(0), id), ":days-off"));

        private static readonly int[] Blocks = { 0b0000111, 0b0001110, 0b0011100, 0b0111000, 0b1110000, 0b1100001, 0b1000011 };

        internal static int DaysOff(Place workplace, uint draw) => DaysOff(workplace, OpenWeekdays(workplace), draw);

        internal static int DaysOffFor(Place workplace, string personId)
        {
            int open = OpenWeekdays(workplace);
            return (open & DayPlanner.DefaultDaysOff) == 0 ? DayPlanner.DefaultDaysOff : DaysOff(workplace, open, DaysOffDraw(personId));
        }

        private static int DaysOff(Place workplace, int open, uint draw)
        {
            if ((open & DayPlanner.DefaultDaysOff) == 0) return DayPlanner.DefaultDaysOff;
            int best = int.MaxValue, count = 0;
            for (int start = 0; start < 7; start++)
            {
                int lost = Bits(Blocks[start] & open);
                if (lost < best) { best = lost; count = 1; } else if (lost == best) count++;
            }
            int pick = (int)(draw % (uint)count);
            for (int start = 0; start < 7; start++)
                if (Bits(Blocks[start] & open) == best && pick-- == 0) return Blocks[start];
            return DayPlanner.DefaultDaysOff;
        }

        private static int Bits(int mask) { int n = 0; for (; mask != 0; mask &= mask - 1) n++; return n; }

        internal static ShiftType? ShiftAt(Place workplace, string personId)
        {
            if (workplace.Kind != PlaceKind.Work) return null;
            if (IsNightlife(workplace.BusinessType)) return ShiftType.Night;
            if (IsEvening(workplace.BusinessType)) return ShiftType.Late;
            if (WorkplaceStaffing.IsWorkforceVenue(workplace)) return WorkplaceStaffing.Rota(personId);
            return null;
        }
    }

    internal static class DayPlanExecutor
    {
        internal static void Ensure(CitizenState person, int seed, double now, IReadOnlyList<Place> catalogue,
            IReadOnlyDictionary<string, Place> places, bool meals, NeedPlanning? needs = null, Func<CitizenState, Household?>? households = null)
        {
            int day = DayPlanner.PlanDay(now);
            var plan = person.Plan;
            if (plan != null && plan.Day >= day)
            {
                if (plan.WorkplaceId != person.DayPlaceId) ReplanWork(person, seed, now, places);
                return;
            }
            if (day < 0) return;
            if (plan != null)
                for (int i = 0; i < plan.Items.Count; i++)
                    if (!DayPlan.Terminal(plan.StateOf(i)) && plan.Items[i].EndsAt > now && plan.Items[i].Kind != PlanItemKind.Meal) return;
            person.Plan = DayPlanner.Generate(person, seed, day, catalogue, places, meals, needs, households?.Invoke(person));
            Adopt(person);
        }

        private static void Adopt(CitizenState person)
        {
            var plan = person.Plan!;
            if (person.Activity == Activity.Home && person.DestinationActivity == Activity.Home) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind == PlanItemKind.Meal || item.EndsAt != person.ActivityUntilMinute) continue;
                if (person.Phase == CitizenPhase.AtPlace && person.Activity == item.Activity && person.PlaceId != person.HomeId)
                    plan.Set(i, PlanItemState.Active);
                else if (person.Phase == CitizenPhase.Travelling && person.DestinationActivity == item.Activity)
                    plan.Set(i, PlanItemState.Travelling);
            }
        }

        private static void ReplanWork(CitizenState person, int seed, double now, IReadOnlyDictionary<string, Place> places)
        {
            var plan = person.Plan!;
            var keys = RoutineKeys.For(person, seed);
            var work = DayPlanner.WorkOn(person, plan.Day, keys.HourShift, places);
            if (work.HasValue && work.Value.EndsAt <= now) work = null;
            var items = new List<PlanItem>(plan.Items.Count + 1);
            var marks = new List<byte>(plan.Items.Count + 1);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                var state = plan.StateOf(i);
                bool open = state == PlanItemState.Pending || state == PlanItemState.Travelling;
                if (item.Kind == PlanItemKind.Work && open && item.EndsAt > now) state = PlanItemState.Cancelled;
                if (work.HasValue && state == PlanItemState.Pending && (item.Kind == PlanItemKind.Need || item.Kind == PlanItemKind.Leisure || item.Kind == PlanItemKind.Outing)
                    && item.EarliestStart < work.Value.EndsAt && item.EndsAt > work.Value.StartsAt - DayPlanner.WorkTravelAllowance)
                    state = PlanItemState.Cancelled;
                items.Add(item); marks.Add((byte)((int)state | (int)plan.CauseOf(i) << 4));
            }
            if (work.HasValue && items.Count < DayPlan.MaximumItems)
            {
                var w = work.Value;
                int at = 0;
                while (at < items.Count && items[at].EarliestStart <= w.StartsAt) at++;
                items.Insert(at, DayPlanner.WorkItem(w));
                marks.Insert(at, 0);
            }
            person.Plan = plan.WithItems(person.DayPlaceId, items.ToArray(), marks.ToArray(),
                DayPlanner.DrawNextWork(person, keys.HourShift, plan.Day, places));
        }

        internal static void Settle(CitizenState person, double now)
        {
            var plan = person.Plan;
            if (plan == null) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind == PlanItemKind.Outing && now > item.LatestStart && item.EndsAt > now
                    && !DayPlan.Terminal(plan.StateOf(i)) && plan.StateOf(i) != PlanItemState.Active)
                    plan.Set(i, PlanItemState.Missed, PlanMissCause.Late);
                if (plan.Items[i].Kind == PlanItemKind.Meal || plan.Items[i].EndsAt > now) continue;
                var state = plan.StateOf(i);
                if (state == PlanItemState.Active) plan.Set(i, PlanItemState.Done);
                else if (!DayPlan.Terminal(state)) plan.Set(i, PlanItemState.Missed, WaitCause(person, plan.Items[i]));
            }
        }

        private static PlanMissCause WaitCause(CitizenState person, PlanItem item)
        {
            if (person.DestinationActivity != item.Activity || person.ActivityUntilMinute != item.EndsAt) return PlanMissCause.Late;
            return person.Phase == CitizenPhase.WaitingForRoute ? PlanMissCause.NoRoute
                : person.Phase == CitizenPhase.WaitingForOpening ? PlanMissCause.Closed : PlanMissCause.Late;
        }

        internal static void SyncMeals(CitizenState? person, ResidentMeal record)
        {
            var plan = person?.Plan;
            if (plan == null) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind != PlanItemKind.Meal) continue;
                bool current = record.Day == plan.Day && record.Kind == item.Meal;
                var outcome = record.Today != null && record.Today.Day == plan.Day ? record.Today.Find(item.Meal) : null;
                if (current && record.Status == MealStatus.Travelling) plan.Restore(i, PlanItemState.Travelling, PlanMissCause.None);
                else if (current && (record.Status == MealStatus.Serving || record.Status == MealStatus.Uncertain)) plan.Restore(i, PlanItemState.Active, PlanMissCause.None);
                else if (outcome != null)
                    switch (outcome.Status)
                    {
                        case MealOutcomeStatus.Bought: plan.Restore(i, PlanItemState.Done, PlanMissCause.None); break;
                        case MealOutcomeStatus.ChoseAlternative: plan.Restore(i, PlanItemState.Cancelled, PlanMissCause.None); break;
                        case MealOutcomeStatus.Skipped: plan.Restore(i, PlanItemState.Missed, MealCause(outcome.Reason)); break;
                        default: plan.Restore(i, PlanItemState.Active, PlanMissCause.None); break;
                    }
                else if (!current && !DayPlan.Terminal(plan.StateOf(i)) && (record.Day > plan.Day || record.Day == plan.Day && record.Kind > item.Meal))
                    plan.Restore(i, PlanItemState.Cancelled, PlanMissCause.None);
            }
        }

        private static PlanMissCause MealCause(MealReasonCode reason)
        {
            switch (reason)
            {
                case MealReasonCode.ShopClosed: case MealReasonCode.ServiceUnavailable: case MealReasonCode.BusinessWithdrew: return PlanMissCause.Closed;
                case MealReasonCode.CheckoutRefused: case MealReasonCode.MoneyRange: case MealReasonCode.FundsUnavailable: return PlanMissCause.Refused;
                default: return PlanMissCause.Late;
            }
        }

        internal static void Mark(CitizenState person, Activity activity, double endsAt, PlanItemState state, PlanMissCause cause = PlanMissCause.None,
            string? placeId = null)
        {
            var plan = person.Plan;
            if (plan == null || activity == Activity.Home) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind == PlanItemKind.Meal || item.Activity != activity || item.EndsAt != endsAt) continue;
                if (plan.StateOf(i) == PlanItemState.Cancelled) continue;
                if (item.Kind == PlanItemKind.Outing && placeId != null && !string.Equals(placeId, item.VenueId, StringComparison.Ordinal)
                    && (state == PlanItemState.Travelling || state == PlanItemState.Active))
                { plan.Set(i, PlanItemState.Missed, PlanMissCause.NoRoute); return; }
                plan.Set(i, state, cause);
                return;
            }
        }

        internal static PlanMissCause CauseOf(string reason)
        {
            switch (reason)
            {
                case "route-wait-expired": case "unreachable": case "optional-search-exhausted": return PlanMissCause.NoRoute;
                case "opening-wait-expired": case "arrival-no-opening": case "no-opening-in-window": case "workplace-closed":
                case "no-feasible-optional-venue": return PlanMissCause.Closed;
                case "paid-visit-unaffordable": return PlanMissCause.Refused;
                default: return PlanMissCause.Late;
            }
        }
    }
}
