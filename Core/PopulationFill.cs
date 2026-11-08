using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    // F5g (city audit F5 #92, docs/plans/f5-population.md section 3 Visitor lifecycle and section 4 F5g row): at each day
    // boundary the city creates commuter visitors and the persistent day-visitor pool toward the F5a census targets, in
    // bounded units through the one event queue. There is no schema bump, switch-over day or retirement path.

    public static class PopulationFillPolicy
    {
        /// <summary>Persons created by one fill unit (plan section 4: at most 64 persons per unit).</summary>
        public const int MaximumPersonsPerUnit = 64;
        /// <summary>Owner decision (8 Nov 2026): the fill creates only working-persona commuters. Identities are derived from
        /// <c>(seed, pv:&lt;n&gt;)</c> by the frozen identity policy, so a sequence number whose identity is a student or retiree
        /// is skipped for a commuter: the number is used up and never reused. One commuter looks at most this many numbers.</summary>
        public const int MaximumSkipsPerCommuter = 4096;
        /// <summary>Persons one day boundary publishes at most (review Sol-r2-2). Staging runs in units of
        /// <see cref="MaximumPersonsPerUnit"/>; this bounds the one publication step. A larger deficit fills over later days.</summary>
        public const int MaximumPersonsPerBoundary = 1024;
        /// <summary>Skipped sequence numbers a city may hold (the reader's bound too). Commuter creation pauses before it.</summary>
        public const int MaximumSkippedSequences = 4 * PopulationCensusPolicy.MaximumVisitors;
    }

    /// <summary>The census inputs that do not live in the saved city: Runtime's load-time place catalogue and business
    /// facts (area, player ownership, school), as the F5a census reads them. Only the per-place facts the targets need are
    /// copied, so later edits of the caller's lists never reach a fill. A catalogue the census refuses gives no targets.</summary>
    public sealed class PopulationFillCatalogue
    {
        // Catalogue Work place IDs; true when the census counts the place as assignable (not a player-owned business).
        private readonly Dictionary<string, bool> workplaces = new Dictionary<string, bool>(StringComparer.Ordinal);

        public PopulationFillCatalogue(IReadOnlyList<Place> places, IReadOnlyList<CensusWorkplace> workplaces)
        {
            if (places == null) throw new ArgumentNullException(nameof(places));
            if (workplaces == null) throw new ArgumentNullException(nameof(workplaces));
            var census = PopulationCensus.Compute(new PopulationCensusInput { Places = places, Workplaces = workplaces, StationIds = Array.Empty<string>() });
            Refused = census.Refused; RefusalReason = census.RefusalReason;
            if (Refused) return;
            WorkplaceCapacity = census.WorkplaceCapacity;
            var byWork = new Dictionary<string, CensusWorkplace>(StringComparer.Ordinal);
            foreach (var workplace in workplaces) byWork[workplace.WorkPlaceId] = workplace;
            foreach (var place in places)
                if (place.Kind == PlaceKind.Work)
                    this.workplaces[place.Id] = !(byWork.TryGetValue(place.Id, out var live) && live.PlayerOwned && PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces);
        }

        public bool Refused { get; }
        public string RefusalReason { get; } = "";
        /// <summary>The census's assignable workplace capacity of this catalogue.</summary>
        public long WorkplaceCapacity { get; }

        /// <summary>The census targets for <paramref name="city"/> now: this catalogue's capacity, the residents' current
        /// assignments (census ResidentWorkers less those at player-owned businesses) and the saved portals. Equal to
        /// <see cref="PopulationCensus.Compute"/> on the same facts with the saved portals as stations.</summary>
        public VisitorTargets Targets(CityState city)
        {
            if (Refused) throw new InvalidOperationException("The census refused this catalogue: " + RefusalReason);
            long eligible = 0;
            foreach (var person in city.Citizens)
            {
                var persona = person.Identity.Persona;
                if (persona == Persona.Student || persona == Persona.Retiree || person.DayPlaceId.Length == 0) continue;
                if (workplaces.TryGetValue(person.DayPlaceId, out bool assignable) && assignable) eligible++;
            }
            return VisitorTargets.Compute(WorkplaceCapacity, eligible, city.Citizens.Count, city.Portals.Count);
        }
    }

    /// <summary>One day boundary's fill: the targets latched when the clock first stood before the boundary with every
    /// earlier event done, the persisted per-pattern counts (active and dormant persons), and the persons staged so far.
    /// Staged persons are not in the city until the boundary publishes them, so a save during staging stores the city as
    /// it was before the boundary, and the reload stages them again from the same state. Never saved.</summary>
    internal sealed class PopulationFillPlan
    {
        internal double Boundary;
        internal VisitorTargets Targets;
        internal long BaseCommuters, BasePool, Commuters, Pool;
        internal VisitorFactory? Factory;
        internal readonly List<(VisitorPerson Visitor, VisitorRoutine Routine)> Staged = new List<(VisitorPerson, VisitorRoutine)>();
        internal readonly Dictionary<string, int> StagedSeats = new Dictionary<string, int>(StringComparer.Ordinal);
        internal int NextSequence;
        internal long Skipped;
        /// <summary>No working identity was found within the skip bounds: commuter creation pauses for this boundary.</summary>
        internal bool CommutersPaused;
        /// <summary>A hard person bound was reached: this boundary creates nobody more (never reached under the clamp).</summary>
        internal bool Refused;
        internal long Aggregate => Commuters + Pool;
        internal VisitorPattern? Next => Refused || Staged.Count >= PopulationFillPolicy.MaximumPersonsPerBoundary || Aggregate >= Targets.VisitorCapacity ? (VisitorPattern?)null
            : Commuters < Targets.CommuterTarget && !CommutersPaused ? VisitorPattern.Commuter
            : Pool < Targets.PoolTarget ? VisitorPattern.DayVisit : (VisitorPattern?)null;

        /// <summary>The same boundary and targets with nothing staged (a route-planner rebuild drops staged work).</summary>
        internal PopulationFillPlan Restart(int nextSequence) => new PopulationFillPlan { Boundary = Boundary, Targets = Targets,
            BaseCommuters = BaseCommuters, BasePool = BasePool, Commuters = BaseCommuters, Pool = BasePool, NextSequence = nextSequence };
    }

    public sealed partial class CitySimulator
    {
        private PopulationFillCatalogue? fillCatalogue;
        private PopulationFillPlan? fillPlan;
        private static readonly IComparer<VisitorPerson> VisitorOrder = Comparer<VisitorPerson>.Create((a, b) => string.CompareOrdinal(a.Id, b.Id));

        /// <summary>Fill units (staging, at most 64 persons each) run since construction (diagnostic, never saved).</summary>
        public long PopulationFillUnits { get; private set; }
        /// <summary>Persons published by day boundaries since construction (diagnostic, never saved).</summary>
        public long PopulationFillCreated { get; private set; }
        public int LastFillUnitPersons { get; private set; }
        /// <summary>Persons published by the latest boundary publication.</summary>
        public int LastFillPublished { get; private set; }
        /// <summary>Sequence numbers skipped for commuters by published boundaries since construction (diagnostic, never saved).</summary>
        public long PopulationFillSkipped { get; private set; }
        /// <summary>Boundaries whose fill stopped at a hard person bound (diagnostic; unreachable under the capacity clamp).</summary>
        public long PopulationFillRefusals { get; private set; }
        /// <summary>The targets latched at the latest evaluated day boundary, if any.</summary>
        public VisitorTargets? PopulationFillTargets => fillPlan?.Targets;
        public double? PopulationFillBoundary => fillPlan?.Boundary;
        public bool PopulationFillAttached => fillCatalogue != null;
        /// <summary>Persons staged for the coming boundary and not yet in the city.</summary>
        public int PopulationFillStaged => fillPlan != null && fillPlan.Boundary > State.Minute ? fillPlan.Staged.Count : 0;

        /// <summary>F5g: fill toward the census targets of <paramref name="catalogue"/> at every later day boundary. Runtime
        /// attaches its load-time catalogue to every current-schema city, so filling is ordinary behaviour, not a switch-over.
        /// A boundary is filled only as the clock crosses it, so a catalogue change or first attachment moves the target only
        /// at the next day boundary, and a boundary already latched in this process keeps its targets. A refused catalogue
        /// detaches the fill (nothing is created, nothing removed).</summary>
        public void AttachPopulationFill(PopulationFillCatalogue? catalogue)
        {
            fillCatalogue = catalogue == null || catalogue.Refused ? null : catalogue;
        }

        private bool FillActive => fillCatalogue != null && State.Food != null && food != null;
        private double NextDayBoundary => (Math.Floor(State.Minute / 1440) + 1) * 1440;

        /// <summary>The coming boundary's plan, latched on first use: every event before the boundary has run, and nothing
        /// after it has. A boundary the clock has passed is complete; the clock passes it only by its publication.</summary>
        private PopulationFillPlan FillPlan(double boundary)
        {
            if (fillPlan != null && fillPlan.Boundary == boundary) return fillPlan;
            long commuters = 0, pool = 0;
            foreach (var visitor in State.Visitors)
                if (visitor.Stay.Pattern == VisitorPattern.DayVisit) pool++; else commuters++;
            fillPlan = new PopulationFillPlan { Boundary = boundary, Targets = fillCatalogue!.Targets(State), BaseCommuters = commuters,
                BasePool = pool, Commuters = commuters, Pool = pool, NextSequence = State.NextVisitorSequence };
            return fillPlan;
        }

        /// <summary>Holds the caught-up report while a boundary inside the target is not yet crossed (staged or published).</summary>
        private bool FillHolds(double targetMinute) => FillActive && NextDayBoundary <= targetMinute;

        /// <summary>One bounded staging unit for <paramref name="boundary"/>: up to
        /// <see cref="PopulationFillPolicy.MaximumPersonsPerUnit"/> persons, commuters first while below their clamped target,
        /// then pool members, never past the aggregate VisitorCapacity or the boundary's
        /// <see cref="PopulationFillPolicy.MaximumPersonsPerBoundary"/>. The persons are built and seated against the occupancy
        /// index plus earlier staged seats, but nothing enters the city. Returns false when the boundary has nothing more to stage.</summary>
        private bool StageFillUnit(double boundary)
        {
            var plan = FillPlan(boundary);
            if (plan.Next == null) return false;
            plan.Factory ??= new VisitorFactory(State, p => occupancy.Count(p.Id) + Staged(plan, p.Id) < p.Capacity,
                work => plan.StagedSeats[work] = Staged(plan, work) + 1);
            int sequence = plan.NextSequence;
            long skippedBefore = (long)State.NextVisitorSequence - 1 - State.Visitors.Count;
            // Bounds are checked before anything is seated. The capacity clamp keeps them unreachable; if a bound ever bites,
            // this boundary's fill ends with a diagnostic instead of stopping the simulation (review Opus-r1-4).
            long room = Math.Min(Math.Min(PopulationFillPolicy.MaximumPersonsPerUnit, PersonTable.MaximumVisitors - (long)State.Visitors.Count - plan.Staged.Count),
                PersonTable.MaximumPersons - (long)PersonTable.PersonCount(State) - plan.Staged.Count);
            if (room <= 0) { plan.Refused = true; PopulationFillRefusals++; return false; }
            int made = 0;
            while (made < room && plan.Next is VisitorPattern pattern)
            {
                int at = sequence;
                if (pattern == VisitorPattern.Commuter)
                {
                    // Owner decision: commuters are working personas only. Skip numbers whose frozen identity is not one.
                    int limit = PopulationFillPolicy.MaximumSkipsPerCommuter;
                    while (limit-- > 0 && at < int.MaxValue && !plan.Factory.Working(at)) at++;
                    long gaps = skippedBefore + plan.Skipped + (at - sequence);
                    if (at == int.MaxValue || !plan.Factory.Working(at) || gaps > PopulationFillPolicy.MaximumSkippedSequences)
                    { plan.CommutersPaused = true; continue; }
                }
                else if (at == int.MaxValue) { plan.Refused = true; PopulationFillRefusals++; break; }
                plan.Skipped += at - sequence;
                plan.Staged.Add(plan.Factory.Create(at, pattern, boundary));
                sequence = at + 1; made++;
                if (pattern == VisitorPattern.Commuter) plan.Commuters++; else plan.Pool++;
            }
            plan.NextSequence = sequence;
            LastFillUnitPersons = made;
            PopulationFillUnits++;
            return true;
        }

        private static int Staged(PopulationFillPlan plan, string placeId) => plan.StagedSeats.TryGetValue(placeId, out int n) ? n : 0;

        /// <summary>Crosses <paramref name="boundary"/>: the clock moves onto it, the first midnight publishes the city's
        /// capability activation as ordinary advancing does (review Sol-r2-1), and every staged person enters the city at once:
        /// the next sequence, the ordinal merge, the queue, the occupancy index and one roster record and wallet each. No event
        /// at the boundary has run yet, so the boundary's own events see the new persons. At most
        /// <see cref="PopulationFillPolicy.MaximumPersonsPerBoundary"/> persons are published by one boundary.</summary>
        private void PublishBoundary(double boundary)
        {
            traffic?.AdvanceTo(boundary, 0);
            State.Minute = boundary;
            if (!State.Capabilities.Active) State.Capabilities.Active = true;
            var plan = fillPlan != null && fillPlan.Boundary == boundary ? fillPlan : null;
            LastFillPublished = 0;
            if (plan == null || plan.Staged.Count == 0) return;
            var added = new List<(VisitorPerson Visitor, VisitorRoutine Routine)>(plan.Staged);
            State.NextVisitorSequence = plan.NextSequence;
            PersonTable.InsertVisitors(State, added);
            foreach (var (visitor, routine) in added)
            {
                // A negative slot marks a visitor; its current position is looked up by ID (CurrentCitizenSlot), so the
                // positions this merge shifts need no renumbering.
                int index = State.Visitors.BinarySearch(visitor, VisitorOrder);
                var item = new Due(~index) { Citizen = routine, Minute = routine.NextEventMinute };
                if (!due.Add(item)) throw new InvalidOperationException("Duplicate citizen event.");
                citizenEvents.Add(visitor.Id, item);
                activeJourneys.File(routine);
                if (routine.DayPlaceId.Length != 0) occupancy.Move("", routine.DayPlaceId);
            }
            foreach (var (_, routine) in added) food!.EnrolCreated(routine);
            LastFillPublished = added.Count;
            PopulationFillCreated += added.Count; PopulationFillSkipped += plan.Skipped;
            plan.Staged.Clear(); plan.StagedSeats.Clear(); plan.Factory = null;
        }

        /// <summary>Read-only person manifest for <c>pc population manifest</c>: persisted counts per pattern and presence,
        /// the latched targets and capacity, and the next visitor sequence. Never draws or changes state.</summary>
        public string PopulationManifest()
        {
            long commuters = 0, pool = 0, commutersActive = 0, poolActive = 0;
            foreach (var visitor in State.Visitors)
            {
                bool active = visitor.Stay.Presence == VisitorPresence.Active;
                if (visitor.Stay.Pattern == VisitorPattern.DayVisit) { pool++; if (active) poolActive++; }
                else { commuters++; if (active) commutersActive++; }
            }
            string N(long value) => value.ToString(CultureInfo.InvariantCulture);
            var text = new StringBuilder(256)
                .Append("minute=").Append(State.Minute.ToString("R", CultureInfo.InvariantCulture))
                .Append("\nresidents=").Append(N(State.Citizens.Count)).Append(" visitors=").Append(N(State.Visitors.Count))
                .Append(" portals=").Append(N(State.Portals.Count)).Append(" next_visitor_sequence=").Append(N(State.NextVisitorSequence))
                .Append("\ncommuters=").Append(N(commuters)).Append(" active=").Append(N(commutersActive))
                .Append("\nday_visitor_pool=").Append(N(pool)).Append(" active=").Append(N(poolActive))
                .Append("\nfill=").Append(fillCatalogue == null ? "detached" : "attached")
                .Append(" units=").Append(N(PopulationFillUnits)).Append(" created=").Append(N(PopulationFillCreated));
            if (fillPlan != null)
            {
                var t = fillPlan.Targets;
                text.Append("\nboundary=").Append(fillPlan.Boundary.ToString("R", CultureInfo.InvariantCulture))
                    .Append(" commuter_target=").Append(N(t.CommuterTarget)).Append(" pool_target=").Append(N(t.PoolTarget))
                    .Append(" visitor_capacity=").Append(N(t.VisitorCapacity))
                    .Append(" clamped=").Append(t.CommuterClamped ? (t.PoolClamped ? "commuters,pool" : "commuters") : t.PoolClamped ? "pool" : "none");
            }
            return text.ToString();
        }
    }
}
