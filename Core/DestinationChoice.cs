using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    public static class DestinationChoice
    {
        /// <summary>Small deterministic shortlist for a time-limited optional outing.
        /// All venues can compete; the shortlist bounds subsequent native route work.</summary>
        public static IReadOnlyList<Place> Shortlist(IEnumerable<Place> places, Place origin, PlaceKind kind,
            string choiceKey, int seed, int maximum = 8)
        {
            if (maximum < 1 || maximum > 16) throw new ArgumentOutOfRangeException(nameof(maximum));
            uint key = KeyState(choiceKey, seed);
            var ranked = new List<(Place Place, double Score)>();
            foreach (var place in places)
            {
                if (place.Kind != kind || place.Capacity <= 0) continue;
                double attraction = Position.Finite(place.AttractionWeight) ? Math.Max(.05, Math.Min(20, place.AttractionWeight)) : 1;
                double traffic = place.NativeTrafficIndex < 0 ? 1 : 1 + Math.Min(100, place.NativeTrafficIndex) / 100.0;
                // Short optional windows favour the current neighbourhood more strongly than
                // a permanent work/home assignment. Distance still never proves a route.
                double distance = Position.Distance(origin.Position, place.Position) / 250.0;
                double uniform = (StableRandom.Finish(StableRandom.Append(key, place.Id)) + 1.0) / (uint.MaxValue + 2.0);
                double score = -Math.Log(uniform) * (1 + distance) * (1 + distance) / (attraction * traffic);
                int index = 0;
                while (index < ranked.Count && (score > ranked[index].Score
                    || (score == ranked[index].Score && StringComparer.Ordinal.Compare(place.Id, ranked[index].Place.Id) >= 0))) index++;
                if (index >= maximum) continue;
                ranked.Insert(index, (place, score));
                if (ranked.Count > maximum) ranked.RemoveAt(maximum);
            }
            return ranked.Select(x => x.Place).ToArray();
        }

        // Every eligible venue participates. Distance is a preference, not a cut-off.
        // Independent exponential draws give a deterministic weighted sample without
        // dependence on catalogue order or a nearest-three restriction.
        public static string Choose(IEnumerable<Place> places, Place origin, PlaceKind kind,
            string choiceKey, int seed, Func<Place, bool>? eligible = null)
        {
            uint key = KeyState(choiceKey, seed);
            string best = "";
            double bestScore = double.PositiveInfinity;
            foreach (var place in places.Where(p => p.Kind == kind && p.Capacity > 0))
            {
                if (eligible != null && !eligible(place)) continue;
                Consider(place, origin, key, ref best, ref bestScore);
            }
            return best;
        }

        /// <summary>Choose over a list without an eligibility filter, from a key already continued through
        /// choiceKey + ":" (see KeyState). Same venue as the string overload; no per-venue allocation.</summary>
        internal static string Choose(IReadOnlyList<Place> places, Place origin, PlaceKind kind, uint key)
        {
            string best = "";
            double bestScore = double.PositiveInfinity;
            for (int i = 0; i < places.Count; i++)
            {
                var place = places[i];
                if (place.Kind == kind && place.Capacity > 0) Consider(place, origin, key, ref best, ref bestScore);
            }
            return best;
        }

        private static void Consider(Place place, Place origin, uint key, ref string best, ref double bestScore)
        {
            double attraction = Position.Finite(place.AttractionWeight) ? Math.Max(0.05, Math.Min(20, place.AttractionWeight)) : 1;
            double traffic = place.NativeTrafficIndex < 0 ? 1 : 1 + Math.Min(100, place.NativeTrafficIndex) / 100.0;
            double distance = Position.Distance(origin.Position, place.Position) / 1200.0;
            double weight = attraction * traffic / ((1 + distance) * (1 + distance));
            // StableRandom.Hash(choiceKey + ":" + place.Id, seed), continued from the shared key prefix.
            double uniform = (StableRandom.Finish(StableRandom.Append(key, place.Id)) + 1.0) / (uint.MaxValue + 2.0);
            double score = -Math.Log(uniform) / weight;
            if (score < bestScore || (score == bestScore && StringComparer.Ordinal.Compare(place.Id, best) < 0))
            { bestScore = score; best = place.Id; }
        }

        /// <summary>The hash state after choiceKey + ":"; a null key concatenates as empty, as before.</summary>
        internal static uint KeyState(string? choiceKey, int seed)
            => StableRandom.Append(StableRandom.Append(StableRandom.Begin(seed), choiceKey ?? ""), ':');
    }
}
