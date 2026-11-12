using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{

    public sealed class VisitorPerson
    {
        public string Id { get; set; } = "";
        public VisitorStay Stay { get; set; } = new VisitorStay();
        public Employment? Employment { get; set; }
        public ResidentIncomeSource? CarriedIncomeSource { get; set; }
        public VisitRecord? LastArrival { get; set; }
        public VisitorRoutine? Routine { get; set; }
    }

    public static class PersonTable
    {
        public const int MaximumPersons = PopulationCensusPolicy.MaximumPersons;
        public const int MaximumResidents = HousingPolicy.MaximumResidents;
        public const int MaximumVisitors = PopulationCensusPolicy.MaximumVisitors;
        public const int MaximumPortals = PopulationCensusPolicy.MaximumStations;
        public const int PersonaScheduleCode = 0;

        public static VisitorPerson AddRepresentedVisitor(CityState city, string consumerId)
        {
            var visitor = new VisitorPerson { Id = consumerId, Stay = new VisitorStay {
                Pattern = PersonPolicy.MigratedConsumerPattern, Presence = VisitorPresence.Active,
                FirstDay = (int)Math.Floor(city.Minute / 1440) } };
            city.Visitors.Add(visitor);
            return visitor;
        }

        public static void OrderVisitors(CityState city)
        {
            city.Visitors.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (int i = 1; i < city.Visitors.Count; i++)
                if (string.CompareOrdinal(city.Visitors[i - 1].Id, city.Visitors[i].Id) == 0)
                    throw new InvalidOperationException("Visitor person already exists: " + city.Visitors[i].Id);
        }

        internal static void InsertVisitors(CityState city, List<(VisitorPerson Visitor, VisitorRoutine Routine)> added)
        {
            if (city.Visitors.Count != city.VisitorRoutines.Count) throw new InvalidOperationException("Every visitor has exactly one routine.");
            if (added.Count == 0) return;
            added.Sort((a, b) => string.CompareOrdinal(a.Visitor.Id, b.Visitor.Id));
            int existing = city.Visitors.Count;
            for (int n = 0; n < added.Count; n++) { city.Visitors.Add(null!); city.VisitorRoutines.Add(null!); }
            int i = existing - 1, j = added.Count - 1, k = existing + added.Count - 1;
            while (j >= 0)
            {
                int order = i < 0 ? -1 : string.CompareOrdinal(city.Visitors[i].Id, added[j].Visitor.Id);
                if (order == 0) throw new InvalidOperationException("Visitor person already exists: " + added[j].Visitor.Id);
                if (order > 0) { city.Visitors[k] = city.Visitors[i]; city.VisitorRoutines[k] = city.VisitorRoutines[i]; i--; }
                else { city.Visitors[k] = added[j].Visitor; city.VisitorRoutines[k] = added[j].Routine; j--; }
                k--;
            }
        }

        public static Employment? ResidentEmployment(CitizenState resident)
        {
            if (resident.DayPlaceId.Length == 0) return null;
            return new Employment { PersonId = resident.Identity.Id, WorkplaceId = resident.DayPlaceId,
                Kind = resident.Identity.Persona == Persona.Student ? EmploymentKind.Study : EmploymentKind.Work,
                StartDay = resident.EmploymentStartDay, ScheduleCode = PersonaScheduleCode };
        }

        public static int PersonCount(CityState city) => city.Citizens.Count + city.Visitors.Count;

        public static IEnumerable<CitizenState> Schedules(CityState city)
        {
            foreach (var resident in city.Citizens) yield return resident;
            foreach (var routine in city.VisitorRoutines) yield return routine;
        }
    }
}
