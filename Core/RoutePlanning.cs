using System;

namespace PersistentCity.Core
{
    public interface IRouteEventInputs
    {
        void BeginRouteEvent();
        void EndRouteEvent();
    }
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

    public interface IRoutePlanningJob : IDisposable
    {
        bool Continue(double budgetMilliseconds);
        Route? Result { get; }
    }

    public sealed class RoutePlanningRestartRequired : InvalidOperationException
    {
        public RoutePlanningRestartRequired(string message) : base(message) { }
    }

    public interface IResumableRoutePlanner : IRoutePlanner
    {
        IRoutePlanningJob Begin(RouteRequest request);
    }
}
