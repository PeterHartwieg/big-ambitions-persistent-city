using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{

    public static class PopulationFillPolicy
    {
        public const int MaximumPersonsPerUnit = 64;
        public const int MaximumSkipsPerCommuter = 4096;
        public const int MaximumPersonsPerBoundary = 1024;
        public const int MaximumSkippedSequences = 4 * PopulationCensusPolicy.MaximumVisitors;
    }

    public sealed class PopulationFillCatalogue
    {
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
        public long WorkplaceCapacity { get; }

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

    internal sealed class PopulationFillPlan
    {
        internal double Boundary;
        internal VisitorTargets Targets;
        internal long Commuters, Pool, CreatedHere;
        internal VisitorFactory? Factory;
        internal bool Closed;
        internal bool CommutersPaused;
        internal bool Refused;
        internal long BoundaryLimit = PopulationFillPolicy.MaximumPersonsPerBoundary;
        internal long Aggregate => Commuters + Pool;
        internal VisitorPattern? Next => Closed || Refused || CreatedHere >= BoundaryLimit
            || Aggregate >= Targets.VisitorCapacity ? (VisitorPattern?)null
            : Commuters < Targets.CommuterTarget && !CommutersPaused ? VisitorPattern.Commuter
            : Pool < Targets.PoolTarget ? VisitorPattern.DayVisit : (VisitorPattern?)null;
    }

    public sealed partial class CitySimulator
    {
        private PopulationFillCatalogue? fillCatalogue;
        private PopulationFillPlan? fillPlan;
        private static readonly IComparer<VisitorPerson> VisitorOrder = Comparer<VisitorPerson>.Create((a, b) => string.CompareOrdinal(a.Id, b.Id));

        public long PopulationFillUnits { get; private set; }
        public long PopulationFillCreated { get; private set; }
        public int LastFillUnitPersons { get; private set; }
        public long PopulationFillSkipped { get; private set; }
        public long PopulationFillRefusals { get; private set; }
        public VisitorTargets? PopulationFillTargets => fillPlan?.Targets;
        public double? PopulationFillBoundary => fillPlan?.Boundary;
        public bool PopulationFillAttached => fillCatalogue != null;
        public bool PopulationFillPending => FillUnitDue();
        public int? MeasurementPersonTarget { get; private set; }

        public void SetMeasurementPersonTarget(int? persons)
        {
            if (persons is int n && (n < 1 || n > PersonTable.MaximumPersons))
                throw new ArgumentOutOfRangeException(nameof(persons), "A measurement target is 1 to " + PersonTable.MaximumPersons + " persons.");
            MeasurementPersonTarget = persons;
            fillPlan = null;
        }

        public void AttachPopulationFill(PopulationFillCatalogue? catalogue)
        {
            fillCatalogue = catalogue == null || catalogue.Refused ? null : catalogue;
        }

        private bool FillActive => fillCatalogue != null && State.Food != null && food != null;
        private bool AtDayBoundary => State.Minute > 0 && State.Minute == Math.Floor(State.Minute / 1440) * 1440;
        private double NextDayBoundary => (Math.Floor(State.Minute / 1440) + 1) * 1440;

        private PopulationFillPlan FillPlan()
        {
            double boundary = State.Minute;
            if (fillPlan != null && fillPlan.Boundary == boundary) return fillPlan;
            long commuters = 0, pool = 0, here = 0;
            for (int i = 0; i < State.Visitors.Count; i++)
            {
                if (State.Visitors[i].Stay.Pattern == VisitorPattern.DayVisit) pool++; else commuters++;
                if (State.VisitorRoutines[i].JoinedMinute == boundary) here++;
            }
            var targets = fillCatalogue!.Targets(State);
            if (MeasurementPersonTarget is int persons) targets = VisitorTargets.ForMeasurement(targets, persons - (long)State.Citizens.Count);
            fillPlan = new PopulationFillPlan { Boundary = boundary, Targets = targets, Commuters = commuters, Pool = pool,
                CreatedHere = here, Closed = OrdinaryBoundaryEventsBegan(boundary),
                BoundaryLimit = MeasurementPersonTarget.HasValue ? PersonTable.MaximumVisitors : PopulationFillPolicy.MaximumPersonsPerBoundary };
            return fillPlan;
        }

        private bool OrdinaryBoundaryEventsBegan(double boundary)
        {
            int day = (int)(boundary / 1440);
            foreach (var meal in State.Food!.Residents)
            {
                if (meal.Budget == null || meal.Budget.LastPostedDay < day) continue;
                var owner = FindCitizen(meal.HostId);
                if (owner != null && owner.JoinedMinute < boundary) return true;
            }
            return false;
        }

        private bool FillUnitDue() => FillActive && AtDayBoundary && pending == null && food!.HasPendingDecision != true && FillPlan().Next.HasValue;

        private bool FillHolds(double targetMinute) => FillActive
            && (AtDayBoundary && State.Minute <= targetMinute && FillUnitDue() || NextDayBoundary <= targetMinute);

        private void RunFillUnit()
        {
            var plan = FillPlan();
            plan.Factory ??= new VisitorFactory(State, occupancy.Vacant, work => occupancy.Move("", work));
            var added = new List<(VisitorPerson Visitor, VisitorRoutine Routine)>(PopulationFillPolicy.MaximumPersonsPerUnit);
            int sequence = State.NextVisitorSequence;
            long skippedBefore = (long)sequence - 1 - State.Visitors.Count, skipped = 0;
            long room = Math.Min(Math.Min(PopulationFillPolicy.MaximumPersonsPerUnit, PersonTable.MaximumVisitors - (long)State.Visitors.Count),
                PersonTable.MaximumPersons - (long)PersonTable.PersonCount(State));
            if (room <= 0) { plan.Refused = true; PopulationFillRefusals++; return; }
            while (added.Count < room && plan.Next is VisitorPattern pattern)
            {
                int at = sequence;
                if (pattern == VisitorPattern.Commuter)
                {
                    int limit = PopulationFillPolicy.MaximumSkipsPerCommuter;
                    while (limit-- > 0 && at < int.MaxValue && !plan.Factory.Working(at)) at++;
                    long gaps = skippedBefore + skipped + (at - sequence);
                    if (at == int.MaxValue || !plan.Factory.Working(at) || gaps > PopulationFillPolicy.MaximumSkippedSequences)
                    { plan.CommutersPaused = true; continue; }
                }
                else if (at == int.MaxValue) { plan.Refused = true; PopulationFillRefusals++; break; }
                skipped += at - sequence;
                added.Add(plan.Factory.Create(at, pattern, State.Minute));
                sequence = at + 1;
                plan.CreatedHere++;
                if (pattern == VisitorPattern.Commuter) plan.Commuters++; else plan.Pool++;
            }
            State.NextVisitorSequence = sequence;
            PopulationFillSkipped += skipped;
            PersonTable.InsertVisitors(State, added);
            foreach (var (visitor, routine) in added)
            {
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
