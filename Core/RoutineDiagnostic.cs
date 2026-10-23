namespace PersistentCity.Core
{
    /// <summary>Optional per-event evidence; no saved payload or periodic resident scan.</summary>
    public sealed class RoutineDiagnostic
    {
        public RoutineDiagnostic(string citizenId, string originId, string destinationId, Activity activity,
            double minute, double deadline, string outcome, string reason)
        { CitizenId = citizenId; OriginId = originId; DestinationId = destinationId; Activity = activity;
          Minute = minute; Deadline = deadline; Outcome = outcome; Reason = reason; }
        public string CitizenId { get; }
        public string OriginId { get; }
        public string DestinationId { get; }
        public Activity Activity { get; }
        public double Minute { get; }
        public double Deadline { get; }
        public string Outcome { get; }
        public string Reason { get; }
    }
}
