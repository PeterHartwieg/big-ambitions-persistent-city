using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>A parked car to show: its stable car ID, its owner and where it stands.</summary>
    public readonly struct ParkedCar
    {
        public ParkedCar(string carId, string ownerId, Position at, double distance)
        { CarId = carId; OwnerId = ownerId; At = at; Distance = distance; }
        public string CarId { get; }
        public string OwnerId { get; }
        public Position At { get; }
        public double Distance { get; }
    }

    /// <summary>
    /// GR-9 (G7): parked cars become objects. A car stands at its saved logical curb position unless its owner is inside
    /// the car block of the committed journey (from the start of the first car leg to the end of the last), where the car
    /// is driving or being parked. Between car legs it already stands at the last car leg's end, as
    /// <see cref="CitySimulator.CarPositionOf"/> reports. Reads only: the saved car is never written, so showing a parked
    /// car, or not, leaves the city unchanged.
    /// </summary>
    public static class ParkedCars
    {
        /// <summary>Where the car stands now, or false while it is in its car block (or the person owns no car).</summary>
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

        /// <summary>The cars to show: within <paramref name="radius"/> of the focus, nearest first, then ordinal car ID, at
        /// most <paramref name="maximum"/>. The same city and focus give the same list, whatever the roster order.</summary>
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
