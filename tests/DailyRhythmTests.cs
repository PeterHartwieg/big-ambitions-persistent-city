using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class DailyRhythmTests
{
    private const int Seed = 7;
    private static CitizenState Person(string id = "person", Persona persona = Persona.Commuter) => new()
    {
        Identity = new CitizenIdentity { Id = id, Persona = persona }, HomeId = "home", PlaceId = "home"
    };

    [Fact]
    public void HomeHasNoCalendarOrQuietBoundaryEvent()
    {
        var person = Person(); person.DayPlaceId = "work";
        double evening = 1440 + 1200;
        var next = DailyRoutine.NextWork(person, Seed, evening)!.Value;
        var before = DailyRoutine.At(person, Seed, evening);
        var midnight = DailyRoutine.At(person, Seed, 2 * 1440);
        Assert.Equal(Activity.Home, before.Activity);
        Assert.Equal(next.StartsAt, before.EndsAt);
        Assert.Equal(before.EndsAt, midnight.EndsAt);
        var quiet = DailyRhythm.OnDay(person, Seed, 1);
        Assert.Equal(before.EndsAt, DailyRoutine.At(person, Seed, quiet.StartsAt).EndsAt);
        Assert.Equal(before.EndsAt, DailyRoutine.At(person, Seed, quiet.EndsAt).EndsAt);
    }

    [Fact]
    public void WorkerWithoutOptionalVenuesSleepsAcrossThreeDayWeekend()
    {
        var person = Person(); person.DayPlaceId = "work";
        double thursdayEvening = 4 * 1440 + 1200;
        var next = DailyRoutine.NextWork(person, Seed, thursdayEvening)!.Value;
        Assert.InRange(next.StartsAt, 8 * 1440, 9 * 1440);
        Assert.Equal(next.StartsAt, DailyRoutine.At(person, Seed, thursdayEvening).EndsAt);
        Assert.Equal(next.StartsAt, DailyRoutine.At(person, Seed, 7 * 1440).EndsAt);
        Assert.True(DailyRoutine.NextWork(person, Seed, next.StartsAt)!.Value.StartsAt > next.StartsAt);
    }

    [Fact]
    public void ResidentWithoutActivitiesHasNoDailyWakeup()
    {
        var person = Person(persona: Persona.Retiree);
        Assert.Equal(ResidentLifecycle.Never, DailyRoutine.At(person, Seed, 1440).EndsAt);
        Assert.Equal(ResidentLifecycle.Never, DailyRoutine.At(person, Seed, 4 * 1440).EndsAt);
        Assert.Null(DailyRoutine.NextWork(person, Seed, 1440));
    }

    [Fact]
    public void QuietSpansAreStablePersonalAndPersonaSpecific()
    {
        var normal = Person(); var baseline = DailyRhythm.OnDay(normal, Seed, 1);
        Assert.Equal(baseline.StartsAt, DailyRhythm.OnDay(normal, Seed, 1).StartsAt);
        Assert.True(baseline.EndsAt > 2 * 1440);
        foreach (var persona in new[] { Persona.Parent, Persona.Student, Persona.Retiree })
        {
            var earlier = DailyRhythm.OnDay(Person(persona: persona), Seed, 1);
            Assert.True(earlier.StartsAt < baseline.StartsAt);
            Assert.True(earlier.EndsAt < baseline.EndsAt);
        }
        var owl = DailyRhythm.OnDay(Person(persona: Persona.NightOwl), Seed, 1);
        Assert.True(owl.StartsAt > baseline.StartsAt);
        Assert.True(owl.EndsAt > baseline.EndsAt);
        Assert.True(Enumerable.Range(0, 100).Select(i => DailyRhythm.OnDay(Person("p" + i), Seed, 1).StartsAt).Distinct().Count() > 40);
        var fridayWake = DailyRhythm.OnDay(normal, Seed, 4);
        var fridayLength = fridayWake.EndsAt - fridayWake.StartsAt;
        Assert.Equal(baseline.EndsAt - baseline.StartsAt + 30, fridayLength);
    }

    [Theory]
    [InlineData(Persona.Commuter)] [InlineData(Persona.Student)] [InlineData(Persona.Parent)] [InlineData(Persona.NightOwl)]
    public void QuietPolicyPreservesEveryMinuteOfExistingWorkOrStudyWindow(Persona persona)
    {
        var person = Person(persona: persona); person.DayPlaceId = "work"; person.ShopId = "shop"; person.LeisureId = "leisure";
        var work = DailyRoutine.WorkOnDay(person, Seed, 1440)!.Value;
        for (double minute = work.StartsAt; minute < work.EndsAt; minute += 15)
        {
            var slot = DailyRoutine.At(person, Seed, minute);
            Assert.Equal(persona == Persona.Student ? Activity.Study : Activity.Work, slot.Activity);
            Assert.Equal(work.EndsAt, slot.EndsAt);
        }
    }

    [Fact]
    public void OptionalOutingEndsAtPersonalQuietStart()
    {
        int day = 5;
        var person = Enumerable.Range(0, 1000).Select(i => Person("owl" + i, Persona.NightOwl)).First(candidate =>
        {
            candidate.LeisureId = "leisure";
            int shift = (int)(StableRandom.Hash(candidate.Identity.Id + ":hours", Seed) % 31) - 15;
            double from = day * 1440 + 1320 + shift;
            var quiet = DailyRhythm.OnDay(candidate, Seed, day);
            return quiet.StartsAt > from && quiet.StartsAt < from + 120;
        });
        var bed = DailyRhythm.OnDay(person, Seed, day).StartsAt;
        var before = DailyRoutine.At(person, Seed, bed - 1);
        Assert.Equal(Activity.Leisure, before.Activity); Assert.Equal(bed, before.EndsAt);
        Assert.Equal(Activity.Home, DailyRoutine.At(person, Seed, bed).Activity);
        Assert.Equal(Activity.Home, DailyRoutine.At(person, Seed, bed + 30).Activity);
    }

    private static (CitizenState Person, List<Place> Places) OvernightPerson()
    {
        var places = new List<Place>
        {
            new() { Id = "home", Kind = PlaceKind.Home, Position = new Position(0, 0, 0) },
            new() { Id = "a", Kind = PlaceKind.Leisure, Position = new Position(10, 0, 0) },
            new() { Id = "b", Kind = PlaceKind.Leisure, Position = new Position(0, 0, 10) }
        };
        var person = Enumerable.Range(0, 10000).Select(i => Person("owl" + i, Persona.NightOwl)).First(candidate =>
        {
            candidate.LeisureId = "a";
            var slot = DailyRoutine.At(candidate, Seed, 6 * 1440 - 1);
            return slot.Activity == Activity.Leisure && slot.EndsAt > 6 * 1440
                && Choose(candidate, places, 5) != Choose(candidate, places, 6);
        });
        return (person, places);
    }

    private static string Choose(CitizenState person, List<Place> places, int day) => DestinationChoice.Choose(places, places[0],
        PlaceKind.Leisure, person.Identity.Id + ":leisure-day:" + day, Seed);

    [Fact]
    public void OvernightLeisureUsesOriginatingDaysVenueAfterMidnight()
    {
        var (person, places) = OvernightPerson();
        var before = DailyRoutine.At(person, Seed, 6 * 1440 - 1, places);
        var after = DailyRoutine.At(person, Seed, 6 * 1440, places);
        Assert.Equal(Choose(person, places, 5), before.PlaceId);
        Assert.NotEqual(Choose(person, places, 6), before.PlaceId);
        Assert.Equal(before.PlaceId, after.PlaceId); Assert.Equal(before.EndsAt, after.EndsAt);
    }

    [Theory]
    [InlineData(CitizenPhase.Travelling)] [InlineData(CitizenPhase.WaitingForRoute)] [InlineData(CitizenPhase.WaitingForOpening)]
    public void CommittedOvernightLeisureRetainsDestinationAndDeadline(CitizenPhase phase)
    {
        var (person, places) = OvernightPerson();
        var slot = DailyRoutine.At(person, Seed, 6 * 1440 - 1, places);
        string committed = slot.PlaceId == "a" ? "b" : "a";
        person.Phase = phase; person.DestinationActivity = Activity.Leisure;
        person.DestinationId = committed; person.ActivityUntilMinute = slot.EndsAt;
        var after = DailyRoutine.At(person, Seed, 6 * 1440, places);
        Assert.Equal(committed, after.PlaceId); Assert.Equal(slot.EndsAt, after.EndsAt);
    }

    [Fact]
    public void AttendingOvernightLeisureKeepsVenueAfterClosureShortensDeadline()
    {
        var (person, places) = OvernightPerson();
        var slot = DailyRoutine.At(person, Seed, 6 * 1440 - 1, places);
        person.Phase = CitizenPhase.AtPlace; person.Activity = Activity.Leisure;
        person.PlaceId = slot.PlaceId == "a" ? "b" : "a"; person.ActivityUntilMinute = slot.EndsAt - .5;
        Assert.Equal(person.PlaceId, DailyRoutine.At(person, Seed, 6 * 1440, places).PlaceId);
    }

    [Fact]
    public void PreparationSpreadsAcrossSixHoursAndLongEstimatePullsItEarlier()
    {
        var window = new WorkWindow("work", Activity.Work, 10 * 1440 + 510, 10 * 1440 + 990);
        var times = Enumerable.Range(0, 200).Select(i => DailyRoutine.CommutePreparationAt(Person("p" + i), Seed, window, 0)).ToArray();
        Assert.All(times, time => Assert.InRange(window.StartsAt - time, 480, 840));
        Assert.True(times.Distinct().Count() > 100);
        Assert.True(times.Max() - times.Min() > 300);
        var person = Person();
        Assert.Equal(DailyRoutine.CommutePreparationAt(person, Seed, window, 0), DailyRoutine.CommutePreparationAt(person, Seed, window, 0));
        Assert.True(DailyRoutine.CommutePreparationAt(person, Seed, window, 1200) < times.Min());
        Assert.Equal(0, DailyRoutine.CommutePreparationAt(person, Seed, new WorkWindow("work", Activity.Work, 100, 580), 1200));
    }

    [Fact]
    public void ClockBoundsAndInvalidEstimatesDoNotInventFutureWork()
    {
        var person = Person(); person.DayPlaceId = "work";
        Assert.Null(DailyRoutine.NextWork(person, Seed, ResidentLifecycle.Never));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyRoutine.At(person, Seed, ResidentLifecycle.Never));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyRoutine.NextWork(person, Seed, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyRoutine.CommutePreparationAt(person, Seed,
            new WorkWindow("work", Activity.Work, 1440, 1920), double.PositiveInfinity));
    }
    [Fact]
    public void OptionalAttendanceNeverSpansQuietHoursAcrossAllPersonasAndWeekdays()
    {
        int optionalSlots = 0, overnightSlots = 0;
        foreach (Persona persona in Enum.GetValues<Persona>())
        foreach (int seed in new[] { 7, -19 })
        for (int identity = 0; identity < 4; identity++)
        {
            var person = Person("invariant-" + identity, persona);
            person.ShopId = "shop"; person.LeisureId = "leisure";
            for (double minute = 1440; minute < 8 * 1440; minute += 15)
            {
                var slot = DailyRoutine.At(person, seed, minute);
                Assert.True(slot.EndsAt > minute);
                if (slot.Activity != Activity.Shop && slot.Activity != Activity.Leisure) continue;
                optionalSlots++;
                int day = (int)Math.Floor(minute / 1440);
                if (minute % 1440 < 30) overnightSlots++;
                for (int offset = -2; offset <= 1; offset++)
                {
                    var quiet = DailyRhythm.OnDay(person, seed, day + offset);
                    Assert.False(minute >= quiet.StartsAt && minute < quiet.EndsAt);
                    if (quiet.StartsAt > minute) Assert.True(slot.EndsAt <= quiet.StartsAt);
                }
            }
        }
        Assert.True(optionalSlots > 1000); Assert.True(overnightSlots > 0);
    }

    [Fact]
    public void SparseAndOptionalRoutinesKeepFutureDeadlinesNearClockHorizon()
    {
        foreach (Persona persona in Enum.GetValues<Persona>())
        for (int mask = 0; mask < 8; mask++)
        {
            var person = Person("horizon-" + mask, persona);
            person.DayPlaceId = (mask & 1) != 0 && persona != Persona.Retiree ? "work" : "";
            person.ShopId = (mask & 2) != 0 ? "shop" : "";
            person.LeisureId = (mask & 4) != 0 ? "leisure" : "";
            foreach (double minute in new[] { 0, .001, 1439.999, 1440, 1440.001, 999997920, 999999999.999 })
            {
                var slot = DailyRoutine.At(person, Seed, minute);
                Assert.True(slot.EndsAt > minute); Assert.True(slot.EndsAt <= ResidentLifecycle.Never);
                var repeated = DailyRoutine.At(person, Seed, minute);
                Assert.Equal(slot.PlaceId, repeated.PlaceId); Assert.Equal(slot.Activity, repeated.Activity);
                Assert.Equal(slot.EndsAt, repeated.EndsAt);
            }
        }
    }

}
