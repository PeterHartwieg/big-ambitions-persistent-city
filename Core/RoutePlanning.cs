using System;

namespace PersistentCity.Core
{
    /// <summary>One route question raised by a citizen event. The simulator holds its clock at the event's
    /// minute until the request is answered, so an answer that arrives frames later has the same simulation
    /// time as a synchronous one. The request carries either the working copy of the resident or a detached
    /// probe; a null citizen asks for a preference-only walk, as the meal return does today.</summary>
    public sealed class RouteRequest
    {
        public RouteRequest(Place from, Place to, double departureMinute, CitizenState? citizen, TravelPreferences preferences)
        {
            From = from ?? throw new ArgumentNullException(nameof(from));
            To = to ?? throw new ArgumentNullException(nameof(to));
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            DepartureMinute = departureMinute;
            Citizen = citizen;
        }
        public Place From { get; }
        public Place To { get; }
        public double DepartureMinute { get; }
        public CitizenState? Citizen { get; }
        public TravelPreferences Preferences { get; }
        public Route? Result { get; private set; }
        public bool Answered { get; private set; }
        public void Answer(Route? route)
        {
            if (Answered) throw new InvalidOperationException("A route request is answered once.");
            Result = route; Answered = true;
        }
    }

    /// <summary>One request's planning work, resumable across frames. Continue performs at least one unit of
    /// work and keeps going while the budget allows; it returns true once the result is final. Dispose
    /// releases any scratch storage the job borrowed, whether or not it finished.</summary>
    public interface IRoutePlanningJob : IDisposable
    {
        /// <summary>Run at least one unit of work and keep going while the budget allows; true once the result is
        /// final. A job that resumes after the native state it captured was replaced (a road snapshot, public
        /// access object or station list) throws <see cref="RoutePlanningRestartRequired"/> instead of mixing two
        /// snapshots. Earlier answers of the same event and anything the job wrote to the request's resident
        /// (a car allocated at a curb, for example) belong to the abandoned snapshot, so the caller discards
        /// the event's working copy and runs the event again from its first request, as a reload would. A job
        /// begun and driven to completion within one call never throws it.</summary>
        bool Continue(double budgetMilliseconds);
        Route? Result { get; }
    }

    /// <summary>Thrown by <see cref="IRoutePlanningJob.Continue"/> when the job cannot keep the native state it
    /// captured between slices. The caller abandons the whole citizen event, not only this request.</summary>
    public sealed class RoutePlanningRestartRequired : InvalidOperationException
    {
        public RoutePlanningRestartRequired(string message) : base(message) { }
    }

    /// <summary>Opt in to answering citizen requests in budgeted slices. The simulator still drives such a
    /// planner to completion inline from AdvanceTo; only native frame slices leave a job pending.</summary>
    public interface IResumableRoutePlanner : IRoutePlanner
    {
        IRoutePlanningJob Begin(RouteRequest request);
    }
}
