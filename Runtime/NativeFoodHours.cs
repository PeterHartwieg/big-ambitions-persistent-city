using System;
using System.Collections.Generic;
using System.Linq;
using Entities;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket
    {
        private readonly Dictionary<string, Place> routinePlaces = new Dictionary<string, Place>(StringComparer.Ordinal);
        private double lastHoursRefreshMinute = double.NegativeInfinity;
        public long HoursRefreshPasses { get; private set; }

        // Refresh the enrolled slice only. The native menu remains the schedule authority;
        // route planners and the simulator share these existing Place objects and stable IDs.
        public void RefreshRoutineHours(double minute, bool force = false)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current) || (!force && Math.Floor(minute) == lastHoursRefreshMinute)) return;
            lastHoursRefreshMinute = Math.Floor(minute);
            HoursRefreshPasses++;
            foreach (var pair in registrations)
            {
                if (!routinePlaces.TryGetValue(pair.Key, out var place)) continue;
                var reg = pair.Value;
                bool closed = reg.temporarilyClosed || !reg.HasEstablishedBusiness || !GamePlaces.IsRetailShopActivity(reg.businessTypeName);
                int count = 0; bool same = place.OpenMinute == 0 && place.CloseMinute == 0;
                if (!closed)
                foreach (var day in reg.scheduleDays)
                {
                    if (!day.isOpen) continue;
                    foreach (var slot in day.openingHourSlots)
                    {
                        int weekday = (int)day.day - 1, from = slot.startingHour * 60, to = slot.endingHour * 60;
                        if (weekday < 0 || weekday > 6 || from < 0 || to > 1440 || from >= to)
                            throw new InvalidOperationException("Invalid native opening hours for an enrolled food shop.");
                        if (count >= place.WeeklyHours.Count || place.WeeklyHours[count].Day != weekday
                            || place.WeeklyHours[count].FromMinute != from || place.WeeklyHours[count].ToMinute != to) same = false;
                        count++;
                    }
                }
                if (same && count == place.WeeklyHours.Count) continue;
                var hours = new List<OpeningWindow>();
                if (!closed)
                foreach (var day in reg.scheduleDays)
                    if (day.isOpen)
                        foreach (var slot in day.openingHourSlots)
                            hours.Add(new OpeningWindow { Day = (int)day.day - 1, FromMinute = slot.startingHour * 60, ToMinute = slot.endingHour * 60 });
                place.OpenMinute = place.CloseMinute = 0; place.WeeklyHours = hours;
            }
        }
    }
}
