using System;
using System.Collections;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        private sealed class EventSteps : IEnumerator<RouteRequest>
        {
            internal const int Begin = 0, MealReturn = 1, Probe = 2, CommuteAnswer = 3, OptionalAnswer = 4, RouteAnswer = 5, Done = -1;
            private readonly CitySimulator city;
            internal EventSteps(CitySimulator city) { this.city = city; }
            internal int Stage = Done;
            internal CitizenState Person = null!;
            internal double Now;
            internal RouteRequest? Request;
            internal RoutineSlot? MissedArrival;
            internal bool PaidRefusedArrival, Arrived, Attending, CommittedWait, HomeFallback, EarlyCommute, OptionalSelected;
            internal IEnumerator<Place>? Candidates;
            internal Place? Candidate;
            internal RoutineSlot Slot;
            internal CommuteDecision Commute;
            internal Route? CommuteRoute, OptionalRoute;
            internal double CommuteAt;
            internal readonly OptionalChoice Choice = new OptionalChoice(default);
            internal IEnumerator<RouteRequest>? Optional;
            internal Place? Destination;

            internal EventSteps Start(CitizenState person, double now)
            {
                Clear();
                Person = person; Now = now; Stage = Begin;
                return this;
            }

            public RouteRequest Current => Request!;
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                int stage = Stage; Stage = Done;
                switch (stage)
                {
                    case Begin: return city.StepBegin(this);
                    case MealReturn: city.FinishMealReturn(Person, Now, Request!.Result); return false;
                    case Probe: return city.StepAfterProbe(this);
                    case CommuteAnswer: return city.StepCommuteAnswered(this);
                    case OptionalAnswer: return city.StepNextOptional(this);
                    case RouteAnswer: city.StepDepart(this, Request!.Result); return false;
                    default: return false;
                }
            }

            internal void EndCandidates() { var candidates = Candidates; Candidates = null; Candidate = null; candidates?.Dispose(); }
            internal void EndOptional() { var optional = Optional; Optional = null; optional?.Dispose(); }

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
                Stage = Done;
                try { EndCandidates(); }
                finally { EndOptional(); Clear(); }
            }

            private void Clear()
            {
                Person = null!; Request = null; MissedArrival = null; Candidate = null; Destination = null;
                PaidRefusedArrival = Arrived = Attending = CommittedWait = HomeFallback = EarlyCommute = OptionalSelected = false;
                Slot = default; Commute = default; CommuteRoute = OptionalRoute = null; CommuteAt = double.PositiveInfinity;
                Choice.Slot = default; Choice.Route = null; Choice.Selected = false; Choice.Candidates.Clear(); Choice.Ranked.Clear();
            }
        }
    }
}
