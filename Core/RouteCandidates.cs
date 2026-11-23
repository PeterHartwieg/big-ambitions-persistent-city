namespace PersistentCity.Core
{
    public sealed class RouteCandidates
    {
        private Route? best;
        public double BestScore { get; private set; } = double.PositiveInfinity;
        public bool HasBest => best != null;

        public void Reset() { best = null; BestScore = double.PositiveInfinity; }

        public void Consider(Route? route, Position from, Position to, TravelPreferences preferences, Position? parked)
        {
            if (route == null || !TransportComposition.Valid(route, from, to, preferences, parked)) return;
            double score = route.EstimatedMinutes;
            if (!TransportComposition.IsFaster(score, BestScore)) return;
            best = route; BestScore = score;
        }

        public void ConsiderJoined(Route? first, Route? second, Route? third, Position from, Position to, TravelPreferences preferences, Position? parked)
        {
            if (TransportComposition.TryJoinedScore(first, second, third, from, to, preferences, parked, out double joined)
                && TransportComposition.IsFaster(joined, BestScore))
                Consider(TransportComposition.Join(first, second, third), from, to, preferences, parked);
        }

        public Route? Publish()
        {
            if (best != null && best.IsShared) best = TransportComposition.Copy(best);
            return best;
        }
    }
}
