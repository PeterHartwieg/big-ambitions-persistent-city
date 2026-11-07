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
        /// <summary>Schema 27: this visitor's own routine in the event queue (never serialized with the tuple).</summary>
        public VisitorRoutine? Routine { get; set; }
    }

    /// <summary>Person-table rules of writer 26 (plan section 3 Identity and ownership, section 5 Bounds).</summary>
    public static class PersonTable
    {
        /// <summary>Hard bounds, checked before allocation on read and before every write.</summary>
        public const int MaximumPersons = PopulationCensusPolicy.MaximumPersons;
        public const int MaximumResidents = HousingPolicy.MaximumResidents;
        public const int MaximumVisitors = PopulationCensusPolicy.MaximumVisitors;
        public const int MaximumPortals = PopulationCensusPolicy.MaximumStations;
        /// <summary>First schema with the factor-1 roster rule: one own slot-0 roster record per person.</summary>
        public const int OwnRosterSchema = 26;
        /// <summary>First schema whose visitors run their own routines (F5d): no host, anchor or represented excursion.</summary>
        public const int IndependentSchema = 27;
        /// <summary>Writer 26 has one schedule template: today's persona routine in DailyRoutine.</summary>
        public const int PersonaScheduleCode = 0;

        /// <summary>A represented consumer of slot one or two becomes a visitor person when its roster record is
        /// enrolled (section 9 Q3 default: <see cref="PersonPolicy.MigratedConsumerPattern"/>). Its meals keep today's
        /// excursion schedule through its host resident. Appends; call <see cref="OrderVisitors"/> once enrolment ends.</summary>
        public static VisitorPerson AddRepresentedVisitor(CityState city, string consumerId)
        {
            var visitor = new VisitorPerson { Id = consumerId, Stay = new VisitorStay {
                Pattern = PersonPolicy.MigratedConsumerPattern, Presence = VisitorPresence.Active,
                FirstDay = (int)Math.Floor(city.Minute / 1440) } };
            city.Visitors.Add(visitor);
            return visitor;
        }

        /// <summary>Restores ordinal order after a batch of appends: one O(n log n) sort instead of an O(n) insert per
        /// visitor (100,000 visitors at the 50,000×3 bound). Refuses a duplicate person.</summary>
        public static void OrderVisitors(CityState city)
        {
            city.Visitors.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (int i = 1; i < city.Visitors.Count; i++)
                if (string.CompareOrdinal(city.Visitors[i - 1].Id, city.Visitors[i].Id) == 0)
                    throw new InvalidOperationException("Visitor person already exists: " + city.Visitors[i].Id);
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

        /// <summary>Every scheduled person: residents, then schema-27 visitor routines in ordinal order.</summary>
        public static IEnumerable<CitizenState> Schedules(CityState city)
        {
            foreach (var resident in city.Citizens) yield return resident;
            foreach (var routine in city.VisitorRoutines) yield return routine;
        }
    }
}
