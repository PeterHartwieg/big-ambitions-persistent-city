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

    /// <summary>One day boundary's fill: the targets latched when the boundary's own events had finished, and the persisted
    /// per-pattern counts (active and dormant persons) as creation proceeds. Never saved: after a reload at the boundary the
    /// same saved state gives the same plan, because only fill units and the new persons' own events run in between.</summary>
    internal sealed class PopulationFillPlan
    {
        internal double Boundary;
        internal VisitorTargets Targets;
        internal long Commuters, Pool;
        internal VisitorFactory? Factory;
        internal long Aggregate => Commuters + Pool;
        internal VisitorPattern? Next => Aggregate >= Targets.VisitorCapacity ? (VisitorPattern?)null
            : Commuters < Targets.CommuterTarget ? VisitorPattern.Commuter
            : Pool < Targets.PoolTarget ? VisitorPattern.DayVisit : (VisitorPattern?)null;
    }

    public sealed partial class CitySimulator
    {
        private PopulationFillCatalogue? fillCatalogue;
        private PopulationFillPlan? fillPlan;
        private static readonly IComparer<VisitorPerson> VisitorOrder = Comparer<VisitorPerson>.Create((a, b) => string.CompareOrdinal(a.Id, b.Id));

        /// <summary>Fill units run since construction (diagnostic, never saved).</summary>
        public long PopulationFillUnits { get; private set; }
        /// <summary>Persons created by fill units since construction (diagnostic, never saved).</summary>
        public long PopulationFillCreated { get; private set; }
        public int LastFillUnitPersons { get; private set; }
        /// <summary>The targets latched at the latest evaluated day boundary, if any.</summary>
        public VisitorTargets? PopulationFillTargets => fillPlan?.Targets;
        public double? PopulationFillBoundary => fillPlan?.Boundary;
        public bool PopulationFillAttached => fillCatalogue != null;

        /// <summary>F5g: fill toward the census targets of <paramref name="catalogue"/> at every later day boundary. Runtime
        /// attaches its load-time catalogue to every current-schema city, so filling is ordinary behaviour, not a switch-over.
        /// A boundary already evaluated in this process keeps its latched targets: a catalogue change moves the target only
        /// at the next day boundary. A refused catalogue detaches the fill (nothing is created, nothing removed).</summary>
        public void AttachPopulationFill(PopulationFillCatalogue? catalogue)
        {
            fillCatalogue = catalogue == null || catalogue.Refused ? null : catalogue;
        }

        private bool FillActive => fillCatalogue != null && State.Food != null && food != null;
        private bool AtDayBoundary => State.Minute > 0 && State.Minute == Math.Floor(State.Minute / 1440) * 1440;
        private double NextDayBoundary => (Math.Floor(State.Minute / 1440) + 1) * 1440;

        // The boundary's plan, latched on first use. Callers evaluate it only once every event at the boundary minute has
        // run (next > boundary), so targets read the residents as the boundary left them.
        private PopulationFillPlan FillPlan()
        {
            if (fillPlan != null && fillPlan.Boundary == State.Minute) return fillPlan;
            long commuters = 0, pool = 0;
            foreach (var visitor in State.Visitors)
                if (visitor.Stay.Pattern == VisitorPattern.DayVisit) pool++; else commuters++;
            fillPlan = new PopulationFillPlan { Boundary = State.Minute, Targets = fillCatalogue!.Targets(State), Commuters = commuters, Pool = pool };
            return fillPlan;
        }

        /// <summary>A fill unit may run now: the clock stands on a day boundary, every event at that minute has run, and the
        /// boundary's plan still has a pattern below its target inside the aggregate capacity.</summary>
        private bool FillUnitDue(double next) => FillActive && AtDayBoundary && next > State.Minute && pending == null
            && food!.HasPendingDecision != true && FillPlan().Next.HasValue;

        /// <summary>Holds the caught-up report while a boundary inside the target is unreached or still has fill work. Placed
        /// last in <see cref="NoEventsThrough"/>, so it evaluates a plan only after the boundary's events have drained.</summary>
        private bool FillHolds(double targetMinute)
        {
            if (!FillActive) return false;
            if (AtDayBoundary && State.Minute <= targetMinute && FillPlan().Next.HasValue) return true;
            return NextDayBoundary <= targetMinute;
        }

        /// <summary>One bounded unit: up to <see cref="PopulationFillPolicy.MaximumPersonsPerUnit"/> persons, commuters first
        /// while below their clamped target, then pool members, never past the aggregate VisitorCapacity. Each person gets the
        /// next <c>pv:&lt;n&gt;</c>, is merged into the ordinal lists, queued, seated against the occupancy index and enrolled
        /// once in the food roster and finances. Existing persons, wallets and obligations are never touched.</summary>
        private void RunFillUnit()
        {
            var plan = FillPlan();
            plan.Factory ??= new VisitorFactory(State, occupancy.Vacant, work => occupancy.Move("", work));
            var added = new List<(VisitorPerson Visitor, VisitorRoutine Routine)>(PopulationFillPolicy.MaximumPersonsPerUnit);
            int sequence = State.NextVisitorSequence;
            // Bounds are checked before anything is seated, so a refusal leaves the occupancy index and sequence untouched.
            long room = Math.Min(Math.Min(PopulationFillPolicy.MaximumPersonsPerUnit, PersonTable.MaximumVisitors - (long)State.Visitors.Count),
                Math.Min(PersonTable.MaximumPersons - (long)PersonTable.PersonCount(State), int.MaxValue - (long)sequence));
            if (room <= 0) throw new InvalidOperationException("Visitor bound exceeded.");
            while (added.Count < room && plan.Next is VisitorPattern pattern)
            {
                added.Add(plan.Factory.Create(sequence++, pattern, State.Minute));
                if (pattern == VisitorPattern.Commuter) plan.Commuters++; else plan.Pool++;
            }
            State.NextVisitorSequence = sequence;
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
            }
            foreach (var (_, routine) in added) food!.EnrolCreated(routine);
            LastFillUnitPersons = added.Count;
            PopulationFillUnits++; PopulationFillCreated += added.Count;
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
