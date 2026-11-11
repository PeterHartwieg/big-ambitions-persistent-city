using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>GR-2: the template a work window starts from. Early 07:45, Day 08:30, Late 12:00, Night 22:00, each plus the
    /// person's hour shift and 480 minutes long. The workplace's opening hours on the day move the window (never shorten it).</summary>
    public enum ShiftType { Early, Day, Late, Night }

    /// <summary>Need is today's shop visit (GR-3 gives needs their categories). Outing is reserved for the household outing
    /// (GR-6). Home spans are the gaps between items and are not stored.</summary>
    public enum PlanItemKind { Work, Meal, Need, Leisure, Outing, Home }
    public enum PlanPriority { Fixed, Wanted, Optional }
    public enum PlanItemState { Pending, Travelling, Active, Done, Missed, Cancelled }
    public enum PlanMissCause { None, Closed, Late, NoRoute, Refused }

    /// <summary>One planned item. The definition is immutable once drawn; its state lives in the owning <see cref="DayPlan"/>.
    /// The window is [EarliestStart, LatestStart] for the start; EndsAt is the item's deadline. A late arrival shortens the
    /// item to EndsAt and never moves a later item.</summary>
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
        /// <summary>A place ID, or empty for an item that picks its venue when it starts (meals).</summary>
        public string VenueId { get; }
        public double EarliestStart { get; }
        public double LatestStart { get; }
        public double EndsAt { get; }
        public double Duration => EndsAt - EarliestStart;
        public PlanPriority Priority { get; }
        public MealKind Meal { get; }
    }

    /// <summary>
    /// GR-2: one person's plan for one game day, drawn once at the first event after the person's day boundary (04:00) and
    /// saved. Items are ordered by start. The definitions are shared by event copies; only the per-item states are copied.
    /// </summary>
    public sealed class DayPlan
    {
        private readonly PlanItem[] items;
        private readonly byte[] marks;

        public DayPlan(int day, uint seed, ShiftType shift, int daysOff, string workplaceId, IReadOnlyList<PlanItem> items)
            : this(day, seed, shift, daysOff, workplaceId, Copy(items), null) { }

        private DayPlan(int day, uint seed, ShiftType shift, int daysOff, string workplaceId, PlanItem[] items, byte[]? marks)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (daysOff < 0 || daysOff > 127) throw new ArgumentOutOfRangeException(nameof(daysOff));
            if (items.Length > MaximumItems) throw new ArgumentException("Too many plan items.", nameof(items));
            Day = day; Seed = seed; Shift = shift; DaysOff = daysOff; WorkplaceId = workplaceId ?? "";
            this.items = items; this.marks = marks ?? new byte[items.Length];
        }

        public const int MaximumItems = 8;
        public int Day { get; }
        /// <summary>Hash of the person ID and day under the city seed. The draws themselves keep their routine keys.</summary>
        public uint Seed { get; }
        public ShiftType Shift { get; }
        /// <summary>Weekday bits (Monday = bit 0) without work. Today every person gets Friday to Sunday; GR-6 varies it.</summary>
        public int DaysOff { get; }
        /// <summary>The workplace the work item was drawn for; a different current workplace replans the work item.</summary>
        public string WorkplaceId { get; }
        public IReadOnlyList<PlanItem> Items => items;
        public PlanItemState StateOf(int index) => (PlanItemState)(marks[index] & 15);
        public PlanMissCause CauseOf(int index) => (PlanMissCause)(marks[index] >> 4);

        /// <summary>Restores a saved state; the reader checks that a cause is present exactly for a missed item.</summary>
        public void Restore(int index, PlanItemState state, PlanMissCause cause)
        {
            if ((state == PlanItemState.Missed) != (cause != PlanMissCause.None)) throw new ArgumentException("A missed item needs exactly one cause.");
            marks[index] = (byte)((int)state | (int)cause << 4);
        }

        internal bool Set(int index, PlanItemState state, PlanMissCause cause = PlanMissCause.None)
        {
            var current = StateOf(index);
            if (Terminal(current) || current == state) return false;
            // Never go back: Pending -> Travelling -> Active, and any open item may end.
            if (!Terminal(state) && state < current) return false;
            Restore(index, state, state == PlanItemState.Missed ? (cause == PlanMissCause.None ? PlanMissCause.Late : cause) : PlanMissCause.None);
            return true;
        }

        public static bool Terminal(PlanItemState state) => state >= PlanItemState.Done;

        /// <summary>An event copy: shared definitions, own states.</summary>
        public DayPlan Copy() => new DayPlan(Day, Seed, Shift, DaysOff, WorkplaceId, items, (byte[])marks.Clone());

        internal DayPlan WithItems(string workplaceId, PlanItem[] replacement, byte[] replacementMarks)
            => new DayPlan(Day, Seed, Shift, DaysOff, workplaceId, replacement, replacementMarks);

        public bool SameAs(DayPlan other)
        {
            if (other == null || other.Day != Day || other.Seed != Seed || other.Shift != Shift || other.DaysOff != DaysOff
                || other.WorkplaceId != WorkplaceId || other.items.Length != items.Length) return false;
            for (int i = 0; i < items.Length; i++)
            {
                var a = items[i]; var b = other.items[i];
                if (a.Kind != b.Kind || a.Activity != b.Activity || a.VenueId != b.VenueId || a.EarliestStart != b.EarliestStart
                    || a.LatestStart != b.LatestStart || a.EndsAt != b.EndsAt || a.Priority != b.Priority || a.Meal != b.Meal
                    || marks[i] != other.marks[i]) return false;
            }
            return true;
        }

        private static PlanItem[] Copy(IReadOnlyList<PlanItem> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new PlanItem[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i] ?? throw new ArgumentException("Null plan item.");
            return copy;
        }
    }

    /// <summary>The routine windows of one calendar day, without allocation. A saved plan supplies them for its day; any
    /// other day is drawn on the spot from the same rules, so a day's plan equals what lookahead saw for it.</summary>
    internal struct RoutineDay
    {
        public bool Present;
        public WorkWindow? Work;
        public double ShopFrom, ShopUntil, LeisureFrom, LeisureUntil;
        public string ShopVenue, LeisureVenue;
        /// <summary>The leisure venue was already chosen (a saved plan); otherwise the reader chooses it.</summary>
        public bool LeisureChosen;
        public bool HasShop => ShopUntil > ShopFrom;
        public bool HasLeisure => LeisureUntil > LeisureFrom;
    }

    /// <summary>GR-2 plan generation. Reads only saved state: persona, workplace and its weekly hours, routine venues, the
    /// place catalogue. The draws are today's routine and meal draws, so the windows equal the F5 persona routine wherever
    /// the workplace's hours cover the shift.</summary>
    public static class DayPlanner
    {
        /// <summary>Plans start at 04:00, the quietest hour of the template.</summary>
        public const double DayBoundaryMinute = 240;
        /// <summary>Friday, Saturday and Sunday.</summary>
        public const int DefaultDaysOff = 0b1110000;
        private const double MaxMinute = 1000000000;

        public static int PlanDay(double minute) => (int)Math.Floor((minute - DayBoundaryMinute) / 1440);

        public static ShiftType ShiftOf(CitizenState person) => person.Identity.Persona == Persona.NightOwl ? ShiftType.Late
            : person.Identity.Persona == Persona.Parent ? ShiftType.Early : ShiftType.Day;

        internal static int ShiftStart(ShiftType shift)
            => shift == ShiftType.Early ? 465 : shift == ShiftType.Late ? 720 : shift == ShiftType.Night ? 1320 : 510;

        public static DayPlan Generate(CitizenState person, int citySeed, int day, IReadOnlyList<Place>? catalogue,
            IReadOnlyDictionary<string, Place>? places, bool meals)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            var keys = RoutineKeys.For(person, citySeed);
            var quiet = new DailyRhythm.Lookup(person, keys);
            var routine = Draw(person, keys, day, places, ref quiet);
            var items = new List<PlanItem>(6);
            if (routine.Work.HasValue)
            {
                var w = routine.Work.Value;
                items.Add(new PlanItem(PlanItemKind.Work, w.Activity, w.PlaceId, w.StartsAt, w.EndsAt, w.EndsAt, PlanPriority.Fixed));
            }
            // Meal windows are drawn here once; MealRhythm reads them back for this day. A window the draw leaves empty
            // or negative is not planned, and MealRhythm then draws it itself, with the same result.
            if (meals)
                for (var kind = MealKind.Breakfast; kind <= MealKind.Dinner; kind++)
                {
                    double desired = MealRhythm.DrawDesiredAt(person, citySeed, day, kind);
                    double expires = MealRhythm.DrawExpiresAt(person, citySeed, day, kind, desired);
                    if (desired >= 0 && expires >= desired && Position.Finite(expires))
                        items.Add(new PlanItem(PlanItemKind.Meal, Activity.Home, "", desired, expires, expires, PlanPriority.Wanted, kind));
                }
            if (routine.HasShop)
                items.Add(new PlanItem(PlanItemKind.Need, Activity.Shop, routine.ShopVenue, routine.ShopFrom, routine.ShopUntil, routine.ShopUntil, PlanPriority.Optional));
            if (routine.HasLeisure)
            {
                string venue = routine.LeisureVenue;
                if (catalogue != null && places != null && places.TryGetValue(person.HomeId, out var home))
                    venue = ChooseLeisure(keys, day, catalogue, home, venue);
                items.Add(new PlanItem(PlanItemKind.Leisure, Activity.Leisure, venue, routine.LeisureFrom, routine.LeisureUntil, routine.LeisureUntil, PlanPriority.Optional));
            }
            items.Sort((a, b) => { int c = a.EarliestStart.CompareTo(b.EarliestStart); return c != 0 ? c : a.Kind.CompareTo(b.Kind); });
            uint seed = StableRandom.Finish(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Append(StableRandom.Begin(citySeed), keys.Id), ":plan:"), day));
            return new DayPlan(day, seed, ShiftOf(person), DefaultDaysOff, person.DayPlaceId, items);
        }

        internal static string ChooseLeisure(RoutineKeys keys, int day, IReadOnlyList<Place> catalogue, Place home, string fallback)
        {
            // The choice key is Identity.Id + ":leisure-day:" + day, hashed per venue as key + ":" + venue ID.
            uint key = StableRandom.Append(StableRandom.AppendDecimal(keys.LeisureChoiceDay, day), ':');
            string selected = DestinationChoice.Choose(catalogue, home, PlaceKind.Leisure, key);
            return selected == "" ? fallback : selected;
        }

        /// <summary>The routine windows of <paramref name="day"/>: the saved plan's when it is that day's, else drawn.</summary>
        internal static RoutineDay On(CitizenState person, RoutineKeys keys, int day, IReadOnlyDictionary<string, Place>? places, ref DailyRhythm.Lookup quiet)
        {
            var plan = person.Plan;
            if (plan == null || plan.Day != day) return Draw(person, keys, day, places, ref quiet);
            var result = new RoutineDay { Present = true, ShopVenue = "", LeisureVenue = "", LeisureChosen = true };
            // A job change inside an event is seen at once; the next event replans the work item (DayPlanExecutor).
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
                }
            }
            return result;
        }

        /// <summary>The work window of <paramref name="day"/>: the saved plan's when it is that day's and the workplace is
        /// unchanged, else drawn.</summary>
        internal static WorkWindow? WorkFor(CitizenState person, RoutineKeys keys, int day, IReadOnlyDictionary<string, Place>? places)
        {
            var plan = person.Plan;
            if (plan == null || plan.Day != day || plan.WorkplaceId != person.DayPlaceId) return WorkOn(person, day, keys.HourShift, places);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind == PlanItemKind.Work && plan.StateOf(i) != PlanItemState.Cancelled)
                    return new WorkWindow(item.VenueId, item.Activity, item.EarliestStart, item.EndsAt);
            }
            return null;
        }

        internal static RoutineDay Draw(CitizenState person, RoutineKeys keys, int day, IReadOnlyDictionary<string, Place>? places, ref DailyRhythm.Lookup quiet)
        {
            var result = new RoutineDay { ShopVenue = person.ShopId, LeisureVenue = person.LeisureId };
            int shift = keys.HourShift;
            result.Work = WorkOn(person, day, shift, places);
            bool weekend = Weekend(day);
            // A commuter visitor's stays recur on weekdays only; at weekends it stays off-map at its portal. F5g: a
            // day-visitor pool member stays only on its seeded visit days and spends them at its shop and leisure venue.
            bool dayVisit = person.Visitor?.Stay.Pattern == VisitorPattern.DayVisit;
            if (person.IsVisitor && (dayVisit ? !VisitorSchedules.VisitsOn(keys, day, weekend) : weekend)) return result;
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

        /// <summary>The work window from the shift template and, when the workplace is known, its opening hours.</summary>
        internal static WorkWindow? WorkOn(CitizenState person, int day, int hourShift, IReadOnlyDictionary<string, Place>? places)
        {
            if (Weekend(day) || person.DayPlaceId.Length == 0 || person.Identity.Persona == Persona.Retiree) return null;
            var shift = ShiftOf(person);
            double start = day * 1440.0 + ShiftStart(shift) + hourShift;
            if (places != null && places.TryGetValue(person.DayPlaceId, out var workplace)) start = FitShift(workplace, day, start, shift);
            return new WorkWindow(person.DayPlaceId, person.Identity.Persona == Persona.Student ? Activity.Study : Activity.Work, start, start + 480);
        }

        /// <summary>Moves a 480-minute shift into one opening interval that starts on this day and holds it whole: Early to
        /// its opening, Late to its closing, Day and Night to the nearest fit. A template already inside the hours stays; a
        /// day with no long enough interval keeps the template, and the closed venue is met at arrival.</summary>
        internal static double FitShift(Place workplace, int day, double start, ShiftType shift)
        {
            // Opening hours are only defined within the supported clock; a lookahead near its end keeps the template.
            if (start + 480 + 10080 > MaxMinute) return start;
            double? close = DailyRoutine.NextClose(workplace, start);
            if (close.HasValue && close.Value >= start + 480) return start;
            double midnight = day * 1440.0, best = start, bestDistance = double.PositiveInfinity;
            foreach (var interval in DailyRoutine.Intervals(workplace, midnight))
            {
                if (interval.From < midnight || interval.From >= midnight + 1440 || interval.To - interval.From < 480) continue;
                double candidate = shift == ShiftType.Early ? interval.From : shift == ShiftType.Late ? interval.To - 480
                    : Math.Max(interval.From, Math.Min(interval.To - 480, start));
                double distance = Math.Abs(candidate - start);
                if (distance < bestDistance) { best = candidate; bestDistance = distance; }
            }
            return best;
        }
    }

    /// <summary>
    /// GR-2 executor bookkeeping. The simulator's event step is the executor: at each person event it makes sure the day's
    /// plan exists, closes items whose deadline has passed and records departures, admissions and misses on the item they
    /// belong to. Every change happens inside an event, so item states depend only on event times (cadence-independent).
    /// </summary>
    internal static class DayPlanExecutor
    {
        /// <summary>Draws today's plan once, at the first event at or after the day boundary. A previous plan with an item
        /// still running past the boundary (a night shift) stays until that item ends.</summary>
        internal static void Ensure(CitizenState person, int seed, double now, IReadOnlyList<Place> catalogue,
            IReadOnlyDictionary<string, Place> places, bool meals)
        {
            int day = DayPlanner.PlanDay(now);
            var plan = person.Plan;
            if (plan != null && plan.Day >= day)
            {
                if (plan.WorkplaceId != person.DayPlaceId) ReplanWork(person, seed, places);
                return;
            }
            if (day < 0) return;
            if (plan != null)
                for (int i = 0; i < plan.Items.Count; i++)
                    if (!DayPlan.Terminal(plan.StateOf(i)) && plan.Items[i].EndsAt > now && plan.Items[i].Kind != PlanItemKind.Meal) return;
            person.Plan = DayPlanner.Generate(person, seed, day, catalogue, places, meals);
        }

        /// <summary>A job change during the day cancels the old work item that has not begun and plans the new workplace's
        /// window for the same day. Every other item and state is kept.</summary>
        private static void ReplanWork(CitizenState person, int seed, IReadOnlyDictionary<string, Place> places)
        {
            var plan = person.Plan!;
            var items = new List<PlanItem>(plan.Items.Count + 1);
            var marks = new List<byte>(plan.Items.Count + 1);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var state = plan.StateOf(i);
                if (plan.Items[i].Kind == PlanItemKind.Work && (state == PlanItemState.Pending || state == PlanItemState.Travelling))
                    state = PlanItemState.Cancelled;
                items.Add(plan.Items[i]); marks.Add((byte)((int)state | (int)plan.CauseOf(i) << 4));
            }
            var keys = RoutineKeys.For(person, seed);
            var work = DayPlanner.WorkOn(person, plan.Day, keys.HourShift, places);
            if (work.HasValue && items.Count < DayPlan.MaximumItems)
            {
                var w = work.Value;
                int at = 0;
                while (at < items.Count && items[at].EarliestStart <= w.StartsAt) at++;
                items.Insert(at, new PlanItem(PlanItemKind.Work, w.Activity, w.PlaceId, w.StartsAt, w.EndsAt, w.EndsAt, PlanPriority.Fixed));
                marks.Insert(at, 0);
            }
            person.Plan = plan.WithItems(person.DayPlaceId, items.ToArray(), marks.ToArray());
        }

        /// <summary>Closes routine items whose deadline has passed: an admitted item is done, one never reached is missed late.
        /// Meal items are kept for the food executor.</summary>
        internal static void Settle(CitizenState person, double now)
        {
            var plan = person.Plan;
            if (plan == null) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                if (plan.Items[i].Kind == PlanItemKind.Meal || plan.Items[i].EndsAt > now) continue;
                var state = plan.StateOf(i);
                if (state == PlanItemState.Active) plan.Set(i, PlanItemState.Done);
                else if (!DayPlan.Terminal(state)) plan.Set(i, PlanItemState.Missed, PlanMissCause.Late);
            }
        }

        /// <summary>Records a state on the routine item a slot belongs to (matched by activity and deadline; the venue may be
        /// an optional alternative). Slots outside the saved plan (another day, a retained old window) are ignored.</summary>
        internal static void Mark(CitizenState person, Activity activity, double endsAt, PlanItemState state, PlanMissCause cause = PlanMissCause.None)
        {
            var plan = person.Plan;
            if (plan == null || activity == Activity.Home) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind == PlanItemKind.Meal || item.Activity != activity || item.EndsAt != endsAt) continue;
                if (plan.StateOf(i) == PlanItemState.Cancelled) continue;
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
