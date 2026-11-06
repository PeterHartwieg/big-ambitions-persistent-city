using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    // F5c-lite (city audit F5 #92): the schema-26 person shape. Behaviour is unchanged: residents keep their
    // routines and represented consumers keep today's excursion schedule through their host resident.

    /// <summary>One visitor person (contract B2). No identity, home, car or recent visits are stored: identity is
    /// derived (B6), and arrival evidence is the single <see cref="LastArrival"/> tuple. Holds no money (B4).</summary>
    public sealed class VisitorPerson
    {
        public string Id { get; set; } = "";
        public VisitorStay Stay { get; set; } = new VisitorStay();
        /// <summary>At most one, Commuter only; WorkplaceId is a saved Work/Study place. PersonId equals <see cref="Id"/>.</summary>
        public Employment? Employment { get; set; }
        /// <summary>Transitional outside income source until the person's own employment outcome (never a wage).</summary>
        public ResidentIncomeSource? CarriedIncomeSource { get; set; }
        /// <summary>Latest arrival {place, exact minute, activity}; absent while dormant. Read checks structure only.</summary>
        public VisitRecord? LastArrival { get; set; }
    }

    /// <summary>Person-table rules of writer 26 (plan section 3 Identity and ownership, section 5 Bounds).</summary>
    public static class PersonTable
    {
        /// <summary>Hard bounds, checked before allocation on read and before every write.</summary>
        public const int MaximumPersons = PopulationCensusPolicy.MaximumPersons;
        public const int MaximumResidents = HousingPolicy.MaximumResidents;
        public const int MaximumVisitors = PopulationCensusPolicy.MaximumVisitors;
        public const int MaximumPortals = PopulationCensusPolicy.MaximumStations;
        /// <summary>Writer 26 has one schedule template: today's persona routine in DailyRoutine.</summary>
        public const int PersonaScheduleCode = 0;

        /// <summary>A represented consumer of slot one or two becomes a visitor person when its roster record is
        /// enrolled (section 9 Q3 default: <see cref="PersonPolicy.MigratedConsumerPattern"/>). The list stays
        /// ordinal by ID. Its meals keep today's excursion schedule through its host resident.</summary>
        public static VisitorPerson AddRepresentedVisitor(CityState city, string consumerId)
        {
            var visitor = new VisitorPerson { Id = consumerId, Stay = new VisitorStay {
                Pattern = PersonPolicy.MigratedConsumerPattern, Presence = VisitorPresence.Active,
                FirstDay = (int)Math.Floor(city.Minute / 1440) } };
            int at = FindVisitor(city.Visitors, consumerId);
            if (at >= 0) throw new InvalidOperationException("Visitor person already exists: " + consumerId);
            city.Visitors.Insert(~at, visitor);
            return visitor;
        }

        /// <summary>Binary search over the ordinal visitor list; the complement of the insertion point when absent.</summary>
        public static int FindVisitor(IReadOnlyList<VisitorPerson> visitors, string id)
        {
            int low = 0, high = visitors.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int order = string.CompareOrdinal(visitors[middle].Id, id);
                if (order == 0) return middle;
                if (order < 0) low = middle + 1; else high = middle - 1;
            }
            return ~low;
        }

        /// <summary>The resident's Employment, formalizing DayPlaceId: Study for students, Work otherwise, none when
        /// unassigned or retired.</summary>
        public static Employment? ResidentEmployment(CitizenState resident)
        {
            if (resident.DayPlaceId.Length == 0) return null;
            return new Employment { PersonId = resident.Identity.Id, WorkplaceId = resident.DayPlaceId,
                Kind = resident.Identity.Persona == Persona.Student ? EmploymentKind.Study : EmploymentKind.Work,
                StartDay = resident.EmploymentStartDay, ScheduleCode = PersonaScheduleCode };
        }

        public static int PersonCount(CityState city) => city.Citizens.Count + city.Visitors.Count;
    }
}
