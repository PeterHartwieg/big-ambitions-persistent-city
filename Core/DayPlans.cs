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
        /// <summary>Hash of the person ID and day under the city seed. The draws themselves keep their routine keys.</summary>
        public uint Seed { get; }
        public ShiftType Shift { get; }
        /// <summary>Weekday bits (Monday = bit 0) without work. Today every person gets Friday to Sunday; GR-6 varies it.</summary>
        public int DaysOff { get; }
        /// <summary>The workplace the work item was drawn for; a different current workplace replans the work item.</summary>
        public string WorkplaceId { get; }
        public IReadOnlyList<PlanItem> Items => items;
        /// <summary>The workplace's first shift after this day (within eight days), drawn with this plan and saved. The wage
        /// queue, the commute and the next plan all read it, so a shift is drawn once even when the hours change before its
        /// day begins. Null without a workplace or a shift in that range.</summary>
        public PlanItem? NextWork { get; }
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
            // The previous plan's saved next shift is this day's work when it falls on it; it is carried while it lies ahead.
            var prior = person.Plan;
            PlanItem? carried = prior != null && prior.WorkplaceId == person.DayPlaceId && prior.Day < day ? prior.NextWork : null;
            if (carried != null && DayPlan.DayOf(carried) == day) routine.Work = Window(carried);
            DropOverlaps(ref routine);
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
            // GR-3: a due need sends the day's shop or leisure window to a venue that sells its category, most urgent first.
            Place? planHome = null;
            if (catalogue != null && places != null) places.TryGetValue(person.HomeId, out planHome);
            if (routine.HasShop)
            {
                string venue = planHome == null ? routine.ShopVenue : NeedModel.PlanVenue(person, day, catalogue!, planHome, PlaceKind.Shop) ?? routine.ShopVenue;
                items.Add(new PlanItem(PlanItemKind.Need, Activity.Shop, venue, routine.ShopFrom, routine.ShopUntil, routine.ShopUntil, PlanPriority.Optional));
            }
            if (routine.HasLeisure)
            {
                string venue = routine.LeisureVenue;
                if (planHome != null)
                    venue = NeedModel.PlanVenue(person, day, catalogue!, planHome, PlaceKind.Leisure) ?? ChooseLeisure(keys, day, catalogue!, planHome, venue);
                items.Add(new PlanItem(PlanItemKind.Leisure, Activity.Leisure, venue, routine.LeisureFrom, routine.LeisureUntil, routine.LeisureUntil, PlanPriority.Optional));
            }
            items.Sort((a, b) => { int c = a.EarliestStart.CompareTo(b.EarliestStart); return c != 0 ? c : a.Kind.CompareTo(b.Kind); });
            uint seed = StableRandom.Finish(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Append(StableRandom.Begin(citySeed), keys.Id), ":plan:"), day));
            var next = carried != null && DayPlan.DayOf(carried) > day ? carried : DrawNextWork(person, keys.HourShift, day, places);
            return new DayPlan(day, seed, ShiftOf(person), DefaultDaysOff, person.DayPlaceId, items, next);
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

        /// <summary>Minutes kept free before a shift for the trip to work.</summary>
        public const double WorkTravelAllowance = 60;

        /// <summary>Optional items never compete with fixed work: a shop or leisure window that overlaps the shift or the
        /// hour before it is left out, not squeezed. The F5 templates never overlap, so this only acts on fitted shifts.</summary>
        internal static void DropOverlaps(ref RoutineDay routine)
        {
            if (!routine.Work.HasValue) return;
            double from = routine.Work.Value.StartsAt - WorkTravelAllowance, until = routine.Work.Value.EndsAt;
            if (routine.HasShop && routine.ShopFrom < until && routine.ShopUntil > from) routine.ShopFrom = routine.ShopUntil = 0;
            if (routine.HasLeisure && routine.LeisureFrom < until && routine.LeisureUntil > from) routine.LeisureFrom = routine.LeisureUntil = 0;
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
            if (plan == null || plan.Day != day)
            {
                var drawn = Draw(person, keys, day, places, ref quiet);
                drawn.Work = WorkFor(person, keys, day, places);
                DropOverlaps(ref drawn);
                return drawn;
            }
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
            => Planned(person, day, out var work) ? work : WorkOn(person, day, keys.HourShift, places);

        /// <summary>The saved shift of <paramref name="day"/> when a plan already decided it: the plan's own work item, or its
        /// saved next shift (no shift on the days before that one). False when no saved plan covers the day.</summary>
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

        /// <summary>Moves a 480-minute shift into an opening that holds it whole and lets it start on this calendar day: Early
        /// to its opening, Late to its latest start, Day and Night to the nearest fit. It may start before the 04:00 plan
        /// boundary or run past midnight; the previous plan's saved next shift and the overnight lookup carry it across.
        /// A template already inside the hours stays; a day with no long enough opening keeps the template, and the closed
        /// venue is met at arrival.</summary>
        internal static double FitShift(Place workplace, int day, double start, ShiftType shift)
        {
            // Opening hours are only defined within the supported clock; a lookahead near its end keeps the template.
            if (start + 480 + 10080 > MaxMinute) return start;
            double? close = DailyRoutine.NextClose(workplace, start);
            if (close.HasValue && close.Value >= start + 480) return start;
            double midnight = day * 1440.0, best = start, bestDistance = double.PositiveInfinity;
            foreach (var interval in DailyRoutine.Intervals(workplace, midnight))
            {
                // Starts on this calendar day only, so a shift has one start day (its wage day and its plan's day).
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
                if (plan.WorkplaceId != person.DayPlaceId) ReplanWork(person, seed, now, places);
                return;
            }
            if (day < 0) return;
            if (plan != null)
                for (int i = 0; i < plan.Items.Count; i++)
                    if (!DayPlan.Terminal(plan.StateOf(i)) && plan.Items[i].EndsAt > now && plan.Items[i].Kind != PlanItemKind.Meal) return;
            person.Plan = DayPlanner.Generate(person, seed, day, catalogue, places, meals);
            Adopt(person);
        }

        /// <summary>A plan drawn while one of its items already runs (a shift begun before the 04:00 boundary, from the
        /// previous plan's saved next shift) takes that item's state from the person: at the venue it is active, on the
        /// way it is travelling.</summary>
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

        /// <summary>A job change during the day cancels the old work item that has not begun and plans the new workplace's
        /// window for the same day if it has not already ended, cancelling an optional item that has not begun and would
        /// overlap it. The saved next shift is drawn again for the new workplace. Every other item and state is kept.</summary>
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
                if (work.HasValue && state == PlanItemState.Pending && (item.Kind == PlanItemKind.Need || item.Kind == PlanItemKind.Leisure)
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
                else if (!DayPlan.Terminal(state)) plan.Set(i, PlanItemState.Missed, WaitCause(person, plan.Items[i]));
            }
        }

        /// <summary>Why an item still open at its deadline was missed: the person's own wait for it says so (no route, a
        /// closed door); anything else, including a journey still under way, is late.</summary>
        private static PlanMissCause WaitCause(CitizenState person, PlanItem item)
        {
            if (person.DestinationActivity != item.Activity || person.ActivityUntilMinute != item.EndsAt) return PlanMissCause.Late;
            return person.Phase == CitizenPhase.WaitingForRoute ? PlanMissCause.NoRoute
                : person.Phase == CitizenPhase.WaitingForOpening ? PlanMissCause.Closed : PlanMissCause.Late;
        }

        /// <summary>Derives the meal items' states from the food record, the meal's own saved continuation: an outcome on the
        /// plan's day (bought, chose another option, skipped with its reason), the record's in-progress meal (travelling,
        /// served), or a meal the record has already moved past without an outcome (cancelled, a dormant visitor's day).</summary>
        internal static void SyncMeals(CitizenState? person, ResidentMeal record)
        {
            var plan = person?.Plan;
            if (plan == null) return;
            // The record is authoritative, so a state is replaced, never locked: a decline or miss is provisional while
            // the meal can still be retried at a later stop, and that retry's travel, service and outcome replace it.
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
                        default: plan.Restore(i, PlanItemState.Active, PlanMissCause.None); break; // unconfirmed: the receipt is still being proven
                    }
                // Passed without an outcome (a dormant visitor's day); an outcome already derived stays.
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
