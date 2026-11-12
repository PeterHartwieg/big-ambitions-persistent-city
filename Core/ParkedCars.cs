using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct ParkedCar
    {
        public ParkedCar(string carId, string ownerId, Position at, double distance)
        { CarId = carId; OwnerId = ownerId; At = at; Distance = distance; }
        public string CarId { get; }
        public string OwnerId { get; }
        public Position At { get; }
        public double Distance { get; }
    }

    public static class ParkedCars
    {
        public static bool TryParked(CitizenState person, double journeyElapsedMinutes, out Position at)
        {
            at = default;
            if (person?.Car == null || !person.Preferences.OwnsCar) return false;
            at = person.Car.ParkedPosition;
            if (person.Phase == CitizenPhase.Travelling && person.Journey != null)
            {
                double elapsed = Math.Max(0, journeyElapsedMinutes), start = 0;
                int first = -1, last = -1;
                var legs = person.Journey.Legs;
                for (int i = 0; i < legs.Count; i++) if (legs[i].Mode == TravelMode.Car) { if (first < 0) first = i; last = i; }
                if (first >= 0)
                {
                    double blockStart = 0, blockEnd = 0;
                    for (int i = 0; i < legs.Count; i++)
                    {
                        if (i == first) blockStart = start;
                        start += legs[i].Minutes;
                        if (i == last) blockEnd = start;
                    }
                    if (elapsed >= blockStart && elapsed < blockEnd) return false;
                    if (elapsed >= blockEnd) at = legs[last].To;
                }
            }
            return at.IsFinite;
        }

        public static List<ParkedCar> Nearest(IEnumerable<ParkedCar> cars, Position focus, double radius, int maximum)
        {
            var chosen = new List<ParkedCar>();
            if (maximum <= 0 || !focus.IsFinite) return chosen;
            foreach (var car in cars)
            {
                double distance = Position.Distance(car.At, focus);
                if (distance <= radius) chosen.Add(new ParkedCar(car.CarId, car.OwnerId, car.At, distance));
            }
            chosen.Sort((a, b) =>
            {
                int order = a.Distance.CompareTo(b.Distance);
                return order != 0 ? order : string.CompareOrdinal(a.CarId, b.CarId);
            });
            if (chosen.Count > maximum) chosen.RemoveRange(maximum, chosen.Count - maximum);
            return chosen;
        }
    }
}
