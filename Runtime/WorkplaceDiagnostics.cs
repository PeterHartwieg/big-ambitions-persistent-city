using System;
using System.Collections.Generic;
using System.Globalization;
using Buildings;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal sealed class NativeWorkplaceAvailability : IWorkplaceAvailability
    {
        private sealed class Workplace
        {
            internal CityBuildingController Controller = null!;
            internal Place Place = null!;
        }
        private readonly Dictionary<string, Workplace> workplaces = new Dictionary<string, Workplace>(StringComparer.Ordinal);
        internal NativeWorkplaceAvailability(IReadOnlyList<Place> places)
        {
            var byId = new Dictionary<string, Place>(StringComparer.Ordinal);
            foreach (var place in places) if (place.Kind == PlaceKind.Work) byId.Add(place.Id, place);
            foreach (var controller in CityManager.Instance.cityBuildingControllers)
            {
                var reg = controller?.buildingRegistration;
                if (reg == null || string.IsNullOrWhiteSpace(reg.StreetName)) continue;
                string id = "address:" + Uri.EscapeDataString(reg.StreetName) + ":" + reg.StreetNumber.ToString(CultureInfo.InvariantCulture) + ":work";
                if (byId.TryGetValue(id, out var place)) workplaces[id] = new Workplace { Controller = controller!, Place = place };
            }
        }
        public bool WorkplaceOpen(string placeId, double minute)
        {
            if (!workplaces.TryGetValue(placeId, out var w) || w.Controller == null) return false;
            var reg = w.Controller.buildingRegistration;
            bool closed = reg == null || reg.temporarilyClosed || !reg.HasEstablishedBusiness || reg.scheduleDays == null;
            int count = 0; bool same = w.Place.OpenMinute == 0 && w.Place.CloseMinute == 0;
            if (!closed)
                foreach (var day in reg!.scheduleDays!)
                    if (day != null && day.isOpen && day.openingHourSlots != null)
                        foreach (var slot in day.openingHourSlots)
                        {
                            if (slot == null) continue;
                            int weekday = (int)day.day - 1, from = slot.startingHour * 60, to = slot.endingHour * 60;
                            if (weekday < 0 || weekday > 6 || from < 0 || to > 1440 || from >= to) continue;
                            if (count >= w.Place.WeeklyHours.Count || w.Place.WeeklyHours[count].Day != weekday
                                || w.Place.WeeklyHours[count].FromMinute != from || w.Place.WeeklyHours[count].ToMinute != to) same = false;
                            count++;
                        }
            if (!same || count != w.Place.WeeklyHours.Count)
            {
                var hours = new List<OpeningWindow>(count);
                if (!closed)
                    foreach (var day in reg!.scheduleDays!)
                        if (day != null && day.isOpen && day.openingHourSlots != null)
                            foreach (var slot in day.openingHourSlots)
                                if (slot != null && (int)day.day >= 1 && (int)day.day <= 7 && slot.startingHour >= 0
                                    && slot.endingHour <= 24 && slot.startingHour < slot.endingHour)
                                    hours.Add(new OpeningWindow { Day = (int)day.day - 1, FromMinute = slot.startingHour * 60, ToMinute = slot.endingHour * 60 });
                w.Place.OpenMinute = w.Place.CloseMinute = 0; w.Place.WeeklyHours = hours;
            }
            int weekdayNow = ((int)(minute / 1440) - 1) % 7; if (weekdayNow < 0) weekdayNow += 7;
            double at = minute % 1440;
            foreach (var hour in w.Place.WeeklyHours)
                if (hour.Day == weekdayNow && hour.FromMinute <= at && at < hour.ToMinute) return true;
            return false;
        }
    }

    public sealed partial class PopulationRuntime
    {
        internal const string AttendanceFixtureCity = "FT-F6e-Attendance";
        private void WorkplaceCommand(string action, string place)
        {
            var save = SaveGameManager.Current;
            if (save?.SaveGameName != AttendanceFixtureCity)
            { Debug.LogWarning("[Persistent City] workplace diagnostics require " + AttendanceFixtureCity + "."); return; }
            if (failed || simulation == null || !ReferenceEquals(saveIdentity, save)
                || (action != "close" && action != "open"))
            { Debug.LogWarning("[Persistent City] use pc workplace close|open <place-id> in an active city."); return; }
            Debug.Log("[Persistent City] logical workplace " + action + " " + place + " accepted="
                + simulation.SetLogicalWorkplaceClosure(place, action == "close")
                + "; fixture input is transient; reapply after load. Held closure days are saved.");
        }
    }
}
