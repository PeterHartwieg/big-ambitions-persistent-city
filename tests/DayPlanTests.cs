using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>GR-2 (G2): one saved plan per person per day, drawn once, and the event step as its executor.</summary>
public sealed class DayPlanTests
{
    private const int Seed = 7;
    private const string Id = "person";
    private static int Shift => (int)(StableRandom.Hash(Id + ":hours", Seed) % 31) - 15;
    private const double Monday = 1440;

    private sealed class Routes : IRoutePlanner
    {
        public bool Reachable = true;
        public double Minutes = 12;
        public double? ShopMinutes;
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
            => !Reachable && to.Kind != PlaceKind.Home ? null : new Route
            {
                Legs = new List<RouteLeg> { new() { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
                    Minutes = to.Kind == PlaceKind.Shop ? ShopMinutes ?? Minutes : Minutes } }
            };
    }

    private static CityState City(Persona persona, double minute = Monday, bool work = false, bool shop = false, bool leisure = false)
    {
        var places = new List<Place> { new() { Id = "home", Name = "Home", District = "d", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0, 0, 0) } };
        if (work) places.Add(new Place { Id = "work", Name = "Work", District = "d", Kind = PlaceKind.Work, Capacity = 2, Position = new Position(100, 0, 0) });
        if (work) places.Add(new Place { Id = "work2", Name = "Work 2", District = "d", Kind = PlaceKind.Work, Capacity = 2, Position = new Position(150, 0, 0) });
        if (shop) places.Add(new Place { Id = "shop", Name = "Shop", District = "d", Kind = PlaceKind.Shop, Capacity = 1, Position = new Position(200, 0, 0) });
        if (leisure) places.Add(new Place { Id = "leisure", Name = "Leisure", District = "d", Kind = PlaceKind.Leisure, Capacity = 1, Position = new Position(300, 0, 0) });
        var city = Population.Create(new[] { new CitizenIdentity { Id = Id, Name = "Person", District = "d", Age = 30, Persona = persona } },
            places, "test-population", "0.40.4", Seed, minute);
        if (work) city.Citizens[0].DayPlaceId = "work";
        return city;
    }

    private static (int Index, PlanItem Item) Item(DayPlan plan, PlanItemKind kind)
    {
        for (int i = 0; i < plan.Items.Count; i++) if (plan.Items[i].Kind == kind) return (i, plan.Items[i]);
        throw new InvalidOperationException("No " + kind + " item.");
    }

    private static string Definitions(DayPlan plan) => plan.Day + "/" + plan.Seed + "/" + plan.Shift + "/" + plan.WorkplaceId + ":" + string.Join(";",
        plan.Items.Select(i => i.Kind + "," + i.Activity + "," + i.VenueId + "," + i.EarliestStart + "," + i.LatestStart + "," + i.EndsAt + "," + i.Priority + "," + i.Meal));

    private static Dictionary<string, Place> Index(CityState city) => city.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);

    [Fact]
    public void ThePlanIsDrawnOnceAtTheDayBoundaryFromTheRoutineDrawsAndSaved()
    {
        var city = City(Persona.Foodie, work: true, shop: true);
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(Monday + 600);
        var person = city.Citizens[0];
        var plan = person.Plan!;
        Assert.Equal(1, plan.Day);
        var (workAt, work) = Item(plan, PlanItemKind.Work);
        Assert.Equal(("work", Monday + 510 + Shift, Monday + 990 + Shift), (work.VenueId, work.EarliestStart, work.EndsAt));
        Assert.Equal(PlanPriority.Fixed, work.Priority);
        Assert.Equal(PlanItemState.Active, plan.StateOf(workAt));
        var need = Item(plan, PlanItemKind.Need).Item;
        Assert.Equal(("shop", Monday + 1050 + Shift), (need.VenueId, need.EarliestStart));
        // Drawing again from the same saved inputs gives the same plan, so lookahead before the boundary saw this day.
        var unplanned = CitySimulator.CloneForEvent(person); unplanned.Plan = null;
        var again = DayPlanner.Generate(unplanned, Seed, 1, city.Places, Index(city), meals: false);
        Assert.Equal(Definitions(again), Definitions(plan));
        var fresh = City(Persona.Foodie, work: true, shop: true).Citizens[0];
        Assert.Equal(work.EarliestStart, DailyRoutine.WorkOnDay(fresh, Seed, Monday + 1)!.Value.StartsAt);
        // The same object survives later events of the day: no redraw.
        engine.AdvanceTo(Monday + 1100);
        Assert.Equal(Definitions(plan), Definitions(person.Plan!));
        engine.AdvanceTo(2 * Monday + 600);
        Assert.Equal(2, person.Plan!.Day);
        Assert.Equal(PlanItemState.Active, person.Plan.StateOf(Item(person.Plan, PlanItemKind.Work).Index));
    }

    [Fact]
    public void AReloadMidDayContinuesTheSavedPlanAndChangedInputsWaitForTomorrow()
    {
        var reference = City(Persona.Foodie, work: true, shop: true);
        var engine = new CitySimulator(reference, new Routes());
        engine.AdvanceTo(Monday + 700);
        string saved = SaveCodec.Write(reference);
        var read = SaveWire.Read(saved);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.True(read.State!.Citizens[0].Plan!.SameAs(reference.Citizens[0].Plan!));
        var resumed = new CitySimulator(read.State, new Routes());
        engine.AdvanceTo(3 * Monday);
        resumed.AdvanceTo(3 * Monday);
        Assert.Equal(SaveCodec.Write(engine.State), SaveCodec.Write(resumed.State));

        // After the reload the workplace changes its hours. Today's shift stays as planned; tomorrow's plan fits the new hours.
        var changed = SaveWire.Read(saved).State!;
        var workplace = changed.Places.Single(p => p.Id == "work");
        workplace.OpenMinute = 700; workplace.CloseMinute = 1300;
        var today = Definitions(changed.Citizens[0].Plan!);
        var city = new CitySimulator(changed, new Routes());
        city.AdvanceTo(Monday + 1439);
        var person = changed.Citizens[0];
        Assert.Equal(today, Definitions(person.Plan!));
        Assert.Contains(person.RecentVisits, v => v.Activity == Activity.Work && v.AtMinute < Monday + 700);
        // Tuesday's shift was drawn with Monday's plan, before the change, and stays; Wednesday's is drawn with Tuesday's.
        city.AdvanceTo(2 * Monday + 800);
        var tomorrow = Item(person.Plan!, PlanItemKind.Work).Item;
        Assert.Equal(2 * Monday + 510 + Shift, tomorrow.EarliestStart);
        Assert.Equal((3 * Monday + 700, 3 * Monday + 1180), (person.Plan!.NextWork!.EarliestStart, person.Plan.NextWork.EndsAt));
    }

    [Fact]
    public void ALateArrivalShortensTheItemAndAnArrivalAfterTheDeadlineIsMissedLate()
    {
        var city = City(Persona.Foodie, work: true, shop: true);
        var routes = new Routes { Minutes = 90, ShopMinutes = 60 };
        var engine = new CitySimulator(city, routes);
        engine.AdvanceTo(Monday + 1440 - 1);
        var person = city.Citizens[0];
        var plan = person.Plan!;
        var (workAt, work) = Item(plan, PlanItemKind.Work);
        // Departure at the window start, arrival 90 minutes late: admitted, and the item still ends at its own deadline.
        Assert.Equal(PlanItemState.Done, plan.StateOf(workAt));
        var arrival = person.RecentVisits.Single(v => v.Activity == Activity.Work);
        Assert.Equal(work.EarliestStart + 90, arrival.AtMinute);
        // The shop window is 45 minutes; a 60-minute walk arrives after it.
        var (needAt, _) = Item(plan, PlanItemKind.Need);
        Assert.Equal((PlanItemState.Missed, PlanMissCause.Late), (plan.StateOf(needAt), plan.CauseOf(needAt)));
        // Tomorrow's fixed item is not moved by today's lateness.
        engine.AdvanceTo(2 * Monday + 1000);
        Assert.Equal(2 * Monday + 510 + Shift, Item(person.Plan!, PlanItemKind.Work).Item.EarliestStart);
    }

    [Fact]
    public void AClosedVenueIsMissedClosedAndAnUnreachableOneMissedNoRoute()
    {
        var closed = City(Persona.Foodie, shop: true);
        var shop = closed.Places.Single(p => p.Id == "shop"); shop.OpenMinute = 0; shop.CloseMinute = 0;
        new CitySimulator(closed, new Routes()).AdvanceTo(2 * Monday - 1);
        var plan = closed.Citizens[0].Plan!;
        var (at, _) = Item(plan, PlanItemKind.Need);
        Assert.Equal((PlanItemState.Missed, PlanMissCause.Closed), (plan.StateOf(at), plan.CauseOf(at)));
        Assert.Equal(1, closed.Citizens[0].MissedActivities);

        var unreachable = City(Persona.Foodie, shop: true);
        new CitySimulator(unreachable, new Routes { Reachable = false }).AdvanceTo(2 * Monday - 1);
        plan = unreachable.Citizens[0].Plan!;
        (at, _) = Item(plan, PlanItemKind.Need);
        Assert.Equal((PlanItemState.Missed, PlanMissCause.NoRoute), (plan.StateOf(at), plan.CauseOf(at)));
        Assert.Equal("home", unreachable.Citizens[0].PlaceId);
    }

    [Fact]
    public void AnAttendedItemIsDoneAndAMissedOneIsNotRetriedAfterReload()
    {
        var city = City(Persona.Foodie, shop: true);
        var shop = city.Places.Single(p => p.Id == "shop"); shop.OpenMinute = 0; shop.CloseMinute = 0;
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(Monday + 1050 + Shift + 10);
        var reloaded = SaveWire.Read(SaveCodec.Write(city)).State!;
        // The shop opens again after the save: the missed item stays missed for the rest of its window.
        reloaded.Places.Single(p => p.Id == "shop").CloseMinute = 1440;
        new CitySimulator(reloaded, new Routes()).AdvanceTo(2 * Monday - 1);
        var plan = reloaded.Citizens[0].Plan!;
        var (at, _) = Item(plan, PlanItemKind.Need);
        Assert.Equal(PlanItemState.Missed, plan.StateOf(at));
        Assert.DoesNotContain(reloaded.Citizens[0].RecentVisits, v => v.Activity == Activity.Shop);

        var open = City(Persona.Foodie, shop: true);
        new CitySimulator(open, new Routes()).AdvanceTo(2 * Monday - 1);
        plan = open.Citizens[0].Plan!;
        Assert.Equal(PlanItemState.Done, plan.StateOf(Item(plan, PlanItemKind.Need).Index));
    }

    private static CityState Town()
    {
        var places = new List<Place>();
        for (int i = 0; i < 6; i++) places.Add(new Place { Id = "home" + i, Name = "P", District = "d", Kind = PlaceKind.Home, Capacity = 8, Position = new Position(i * 40, 0, 0) });
        places.Add(new Place { Id = "office", Name = "P", District = "d", Kind = PlaceKind.Work, Capacity = 30, Position = new Position(500, 0, 0), OpenMinute = 600, CloseMinute = 1260 });
        places.Add(new Place { Id = "factory", Name = "P", District = "d", Kind = PlaceKind.Work, Capacity = 30, Position = new Position(0, 500, 0) });
        places.Add(new Place { Id = "school", Name = "P", District = "d", Kind = PlaceKind.Study, Capacity = 30, Position = new Position(250, 250, 0) });
        for (int i = 0; i < 3; i++) places.Add(new Place { Id = "shop" + i, Name = "P", District = "d", Kind = PlaceKind.Shop, Capacity = 10, Position = new Position(100 * i, 300, 0), OpenMinute = 480, CloseMinute = 1140 });
        for (int i = 0; i < 3; i++) places.Add(new Place { Id = "fun" + i, Name = "P", District = "d", Kind = PlaceKind.Leisure, Capacity = 10, Position = new Position(300, 100 * i, 0), OpenMinute = 900, CloseMinute = 1380 });
        var people = Enumerable.Range(0, 40).Select(i => new CitizenIdentity { Id = "p" + i, Name = "P" + i, District = "d", Age = i % 8 == 4 ? 70 : 30, Persona = (Persona)(i % 8) });
        return Population.Create(people, places, "town", "0.40.4", Seed, Monday);
    }

    [Fact]
    public void TwoCadencesReplayTheSamePlansAndStatesExactly()
    {
        var a = new CitySimulator(Town(), new Routes { Minutes = 17 });
        var b = new CitySimulator(Town(), new Routes { Minutes = 17 });
        for (int day = 1; day <= 4; day++)
        {
            double target = Monday * (day + 1);
            a.AdvanceTo(target);
            while (!b.IsCaughtUpTo(target)) b.AdvanceFrameSlice(target, 1);
            Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
        }
        var plans = a.State.Citizens.Select(c => c.Plan).Where(p => p != null).ToList();
        Assert.Equal(a.State.Citizens.Count, plans.Count);
        Assert.Contains(plans, p => p!.Items.Any(i => i.Kind == PlanItemKind.Work && i.VenueId == "office" && i.EarliestStart % 1440 >= 600));
        Assert.Contains(plans, p => Enumerable.Range(0, p!.Items.Count).Any(i => p.StateOf(i) == PlanItemState.Done));
    }

    [Theory]
    [InlineData(Persona.Commuter, 600)]
    [InlineData(Persona.Parent, 600)]
    [InlineData(Persona.NightOwl, 720)]
    public void AShiftFitsInsideTheWorkplaceHoursWithoutShortening(Persona persona, int expected)
    {
        var city = City(persona, work: true);
        var person = city.Citizens[0];
        var index = Index(city);
        var template = DailyRoutine.WorkOnDay(person, Seed, Monday)!.Value;
        Assert.Equal(480, template.EndsAt - template.StartsAt);
        // Open all day: the template stands.
        Assert.Equal(template.StartsAt, DailyRoutine.WorkOnDay(person, Seed, Monday, index)!.Value.StartsAt);
        var work = city.Places.Single(p => p.Id == "work");
        work.OpenMinute = 600; work.CloseMinute = 1200;
        var fitted = DailyRoutine.WorkOnDay(person, Seed, Monday, index)!.Value;
        Assert.Equal((Monday + expected, Monday + expected + 480), (fitted.StartsAt, fitted.EndsAt));
        // Hours too short for a whole shift keep the template; the closed door is met at arrival.
        work.CloseMinute = 1000;
        Assert.Equal(template.StartsAt, DailyRoutine.WorkOnDay(person, Seed, Monday, index)!.Value.StartsAt);
    }

    [Fact]
    public void AJobChangeReplansTodaysWorkItemAndKeepsEveryOtherState()
    {
        var city = City(Persona.Foodie, work: true, shop: true);
        var person = city.Citizens[0];
        var index = Index(city);
        person.Plan = DayPlanner.Generate(person, Seed, 1, city.Places, index, meals: false);
        var (needAt, _) = Item(person.Plan, PlanItemKind.Need);
        person.Plan.Restore(needAt, PlanItemState.Missed, PlanMissCause.Closed);
        person.DayPlaceId = "work2";
        DayPlanExecutor.Ensure(person, Seed, Monday + 300, city.Places, index, meals: false);
        var plan = person.Plan!;
        var works = Enumerable.Range(0, plan.Items.Count).Where(i => plan.Items[i].Kind == PlanItemKind.Work).ToList();
        Assert.Equal(2, works.Count);
        Assert.Equal(PlanItemState.Cancelled, plan.StateOf(works.Single(i => plan.Items[i].VenueId == "work")));
        Assert.Equal(PlanItemState.Pending, plan.StateOf(works.Single(i => plan.Items[i].VenueId == "work2")));
        Assert.Equal("work2", plan.WorkplaceId);
        Assert.Equal("work2", DailyRoutine.WorkOnDay(person, Seed, Monday + 300, index)!.Value.PlaceId);
        Assert.Equal((PlanItemState.Missed, PlanMissCause.Closed), (plan.StateOf(Item(plan, PlanItemKind.Need).Index), plan.CauseOf(Item(plan, PlanItemKind.Need).Index)));
        // Losing the job cancels the new item and leaves no work today.
        person.DayPlaceId = "";
        DayPlanExecutor.Ensure(person, Seed, Monday + 301, city.Places, index, meals: false);
        Assert.Null(DailyRoutine.WorkOnDay(person, Seed, Monday + 301, index));
    }

    [Fact]
    public void MealWindowsAreDrawnIntoThePlanAndReadBackUnchanged()
    {
        var city = City(Persona.Commuter, work: true);
        var person = city.Citizens[0];
        var expected = new[] { MealKind.Breakfast, MealKind.Lunch, MealKind.Dinner }
            .Select(k => (MealRhythm.DesiredAt(person, Seed, 1, k), MealRhythm.ExpiresAt(person, Seed, 1, k))).ToArray();
        person.Plan = DayPlanner.Generate(person, Seed, 1, city.Places, Index(city), meals: true);
        var meals = person.Plan.Items.Where(i => i.Kind == PlanItemKind.Meal).ToList();
        Assert.Equal(new[] { MealKind.Breakfast, MealKind.Lunch, MealKind.Dinner }, meals.Select(m => m.Meal).OrderBy(m => m));
        Assert.Equal(expected, new[] { MealKind.Breakfast, MealKind.Lunch, MealKind.Dinner }
            .Select(k => (MealRhythm.DesiredAt(person, Seed, 1, k), MealRhythm.ExpiresAt(person, Seed, 1, k))).ToArray());
        Assert.All(meals, m => Assert.Equal("", m.VenueId));
    }

    [Fact]
    public void PlansRoundTripOnTheWireAndBrokenPlansAreRefused()
    {
        var town = new CitySimulator(Town(), new Routes { Minutes = 17 });
        town.AdvanceTo(Monday + 1000);
        string payload = SaveCodec.Write(town.State);
        var read = SaveWire.Read(payload);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        for (int i = 0; i < town.State.Citizens.Count; i++)
            Assert.True(read.State!.Citizens[i].Plan!.SameAs(town.State.Citizens[i].Plan!), town.State.Citizens[i].Identity.Id);
        Assert.Equal(payload, SaveCodec.Write(read.State!));

        string Edit(Func<string, string> change)
        {
            var root = JObject.Parse(payload);
            var citizen = root["Citizens"]![0]!;
            citizen["Plan"] = change((string)citizen["Plan"]!);
            return root.ToString(Newtonsoft.Json.Formatting.None);
        }
        string plan = (string)JObject.Parse(payload)["Citizens"]![0]!["Plan"]!;
        var parts = plan.Split('|');
        // A missed item without a cause, a plan for a day that has not begun, a noncanonical minute.
        var item = parts[6].Split(';')[0];
        string missedWithoutCause = item.Substring(0, 4) + "40" + item.Substring(6);
        Assert.Equal(SaveReadStatus.Invalid, SaveWire.Read(Edit(p => p.Replace(item, missedWithoutCause))).Status);
        Assert.Equal(SaveReadStatus.Invalid, SaveWire.Read(Edit(p => "1|5|" + string.Join("|", parts.Skip(2)))).Status);
        var fields = item.Split(',');
        Assert.Equal(4, fields.Length);
        Assert.Equal(SaveReadStatus.Invalid, SaveWire.Read(Edit(p => p.Replace(item, fields[0] + "," + fields[1] + "," + fields[2] + ".0," + fields[3]))).Status);
        // The end is written only when it differs from the latest start.
        Assert.Equal(SaveReadStatus.Invalid, SaveWire.Read(Edit(p => p.Replace(item, item + "," + fields[3]))).Status);
        Assert.Equal(SaveReadStatus.Invalid, SaveWire.Read(Edit(p => p + ";")).Status);
    }

    // ---- Review round 1 ----

    /// <summary>Sol-r1-1, Astra-r1-1: a fitted shift never starts before its plan's 04:00 boundary or after midnight, so the
    /// plan of its own day always holds it; hours that only allow such a shift keep the template.</summary>
    [Theory]
    [InlineData(Persona.Commuter, 120, 600)]
    [InlineData(Persona.Commuter, 1320, 600)]
    [InlineData(Persona.NightOwl, 1320, 600)]
    [InlineData(Persona.NightOwl, 1080, 120)]
    [InlineData(Persona.Parent, 0, 480)]
    [InlineData(Persona.Commuter, 240, 720)]
    public void AFittedShiftStaysInsideItsPlanDay(Persona persona, int open, int close)
    {
        var city = City(persona, work: true);
        var work = city.Places.Single(p => p.Id == "work"); work.OpenMinute = open; work.CloseMinute = close;
        var index = Index(city);
        for (int day = 1; day <= 4; day++)
        {
            var shift = DailyRoutine.WorkOnDay(city.Citizens[0], Seed, day * 1440.0, index)!.Value;
            Assert.InRange(shift.StartsAt, day * 1440.0 + DayPlanner.DayBoundaryMinute, (day + 1) * 1440.0 - 480);
            Assert.Equal(480, shift.EndsAt - shift.StartsAt);
        }
    }

    /// <summary>Sol-r1-1: a shift at the 04:00 boundary is drawn as the previous plan's saved next shift before it begins.
    /// A reload at 03:00 that changes the hours neither moves it nor adds a second visit; the change reaches the next plan
    /// that draws a shift.</summary>
    [Fact]
    public void AShiftAtTheDayBoundaryIsSavedBeforeItBeginsAndNotRepeatedAfterReloadWithNewHours()
    {
        var city = City(Persona.Commuter, work: true);
        var work = city.Places.Single(p => p.Id == "work"); work.OpenMinute = 240; work.CloseMinute = 720;
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(Monday + 180);
        var saved = city.Citizens[0].Plan!;
        Assert.Equal(0, saved.Day);
        Assert.Equal(Monday + 240, saved.NextWork!.EarliestStart);
        var read = SaveWire.Read(SaveCodec.Write(city)).State!;
        var place = read.Places.Single(p => p.Id == "work"); place.OpenMinute = 480; place.CloseMinute = 960;
        var resumed = new CitySimulator(read, new Routes());
        resumed.AdvanceTo(2 * Monday - 1);
        var person = read.Citizens[0];
        var visits = person.RecentVisits.Where(v => v.Activity == Activity.Work && v.AtMinute >= Monday && v.AtMinute < 2 * Monday).ToList();
        // The saved 04:00 shift stands: the resident waits for the new opening, then goes once.
        Assert.Single(visits);
        Assert.Equal(Monday + 480 + 12, visits[0].AtMinute);
        var (at, item) = Item(person.Plan!, PlanItemKind.Work);
        Assert.Equal((Monday + 240, PlanItemState.Done), (item.EarliestStart, person.Plan!.StateOf(at)));
        // Tuesday was drawn with Monday's plan, after the change.
        Assert.Equal(2 * Monday + 480, person.Plan.NextWork!.EarliestStart);
    }

    /// <summary>Sol-r1-2: a shift fitted into the afternoon leaves out the shop visit it would overlap, so the resident is
    /// at work on time.</summary>
    [Fact]
    public void AnOptionalItemThatWouldOverlapAFittedShiftIsLeftOut()
    {
        var city = City(Persona.Foodie, work: true, shop: true);
        var work = city.Places.Single(p => p.Id == "work"); work.OpenMinute = 720; work.CloseMinute = 1230;
        var index = Index(city);
        var plan = DayPlanner.Generate(city.Citizens[0], Seed, 1, city.Places, index, meals: false);
        var shift = Item(plan, PlanItemKind.Work).Item;
        Assert.Equal(Monday + 720, shift.EarliestStart);
        Assert.DoesNotContain(plan.Items, i => i.Kind == PlanItemKind.Need);
        Assert.All(plan.Items.Where(i => i.Priority == PlanPriority.Optional),
            i => Assert.True(i.EndsAt <= shift.EarliestStart - DayPlanner.WorkTravelAllowance || i.EarliestStart >= shift.EndsAt));
        // Lookahead (no plan yet) agrees with the plan.
        Assert.Equal(Activity.Work, DailyRoutine.At(city.Citizens[0], Seed, Monday + 1050 + Shift + 1, index).Activity);
        new CitySimulator(city, new Routes()).AdvanceTo(2 * Monday - 1);
        var person = city.Citizens[0];
        Assert.Equal(Monday + 720 + 12, person.RecentVisits.Single(v => v.Activity == Activity.Work).AtMinute);
        Assert.DoesNotContain(person.RecentVisits, v => v.Activity == Activity.Shop && v.AtMinute < 2 * Monday);
    }

    /// <summary>Astra-r1-4: a work route that never opens is missed for want of a route, not late.</summary>
    [Fact]
    public void AWorkRouteThatNeverOpensIsMissedNoRoute()
    {
        var city = City(Persona.Commuter, work: true);
        new CitySimulator(city, new Routes { Reachable = false }).AdvanceTo(2 * Monday - 1);
        var plan = city.Citizens[0].Plan!;
        var (at, _) = Item(plan, PlanItemKind.Work);
        Assert.Equal((PlanItemState.Missed, PlanMissCause.NoRoute), (plan.StateOf(at), plan.CauseOf(at)));
    }

    /// <summary>Opus-r1-5: a job found in the small hours does not add the previous day's ended shift to its plan.</summary>
    [Fact]
    public void AJobChangeAfterMidnightAddsNoEndedShift()
    {
        var city = City(Persona.Commuter, work: true);
        var person = city.Citizens[0];
        var index = Index(city);
        person.Plan = DayPlanner.Generate(person, Seed, 1, city.Places, index, meals: false);
        person.DayPlaceId = "work2";
        DayPlanExecutor.Ensure(person, Seed, 2 * Monday + 60, city.Places, index, meals: false);
        var plan = person.Plan!;
        Assert.Equal(1, plan.Day);
        Assert.DoesNotContain(plan.Items, i => i.Kind == PlanItemKind.Work && i.VenueId == "work2");
        Assert.Equal("work2", plan.NextWork!.VenueId);
        Assert.Equal(2 * Monday + 510 + Shift, DailyRoutine.WorkOnDay(person, Seed, 2 * Monday + 60, index)!.Value.StartsAt);
    }

    /// <summary>Sol-r1-3: the meal items follow the food record. With no shops every meal ends at home (chose another
    /// option), so by midnight none is still pending; a reload mid-day continues to the same states and bytes.</summary>
    [Fact]
    public void MealItemsFollowTheFoodRecordAndContinueAcrossReload()
    {
        var reference = AttendanceWageTests.Fresh(42, count: 6);
        AttendanceWageTests.Advance(reference, Monday + 700);
        var reloaded = new CitySimulator(SaveWire.Read(SaveCodec.Write(reference.State)).State!, new AttendanceWageTests.Routes());
        reloaded.AttachFoodMarket(new AttendanceWageTests.Market());
        AttendanceWageTests.Advance(reference, 2 * Monday - 1); AttendanceWageTests.Advance(reloaded, 2 * Monday - 1);
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(reloaded.State));
        foreach (var person in reference.State.Citizens)
        {
            var plan = person.Plan!;
            var record = reference.State.Food!.Residents.Single(r => r.ResidentId == person.Identity.Id);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                if (plan.Items[i].Kind != PlanItemKind.Meal) continue;
                Assert.True(DayPlan.Terminal(plan.StateOf(i)), person.Identity.Id + " " + plan.Items[i].Meal + " " + plan.StateOf(i));
                var outcome = record.Today?.Day == plan.Day ? record.Today.Find(plan.Items[i].Meal) : null;
                if (outcome?.Status == MealOutcomeStatus.ChoseAlternative) Assert.Equal(PlanItemState.Cancelled, plan.StateOf(i));
                if (outcome?.Status == MealOutcomeStatus.Bought) Assert.Equal(PlanItemState.Done, plan.StateOf(i));
                if (outcome?.Status == MealOutcomeStatus.Skipped) Assert.Equal(PlanItemState.Missed, plan.StateOf(i));
            }
        }
    }

    [Fact]
    public void MealStatesMapEveryFoodOutcome()
    {
        var city = City(Persona.Commuter, work: true);
        var person = city.Citizens[0];
        person.Plan = DayPlanner.Generate(person, Seed, 1, city.Places, Index(city), meals: true);
        var record = new ResidentMeal { ResidentId = Id, Day = 1, Kind = MealKind.Dinner, Status = MealStatus.Travelling,
            Today = new ResidentFoodToday { Day = 1 } };
        record.Today.Meals.Add(new MealOutcomeRecord { Kind = MealKind.Breakfast, Status = MealOutcomeStatus.Bought });
        record.Today.Meals.Add(new MealOutcomeRecord { Kind = MealKind.Lunch, Status = MealOutcomeStatus.Skipped, Reason = MealReasonCode.ShopClosed });
        DayPlanExecutor.SyncMeals(person, record);
        PlanItemState State(MealKind kind, out PlanMissCause cause)
        {
            var plan = person.Plan!;
            int i = Enumerable.Range(0, plan.Items.Count).Single(x => plan.Items[x].Meal == kind);
            cause = plan.CauseOf(i); return plan.StateOf(i);
        }
        Assert.Equal(PlanItemState.Done, State(MealKind.Breakfast, out _));
        Assert.Equal((PlanItemState.Missed, PlanMissCause.Closed), (State(MealKind.Lunch, out var lunch), lunch));
        Assert.Equal(PlanItemState.Travelling, State(MealKind.Dinner, out _));
        record.Status = MealStatus.Serving; DayPlanExecutor.SyncMeals(person, record);
        Assert.Equal(PlanItemState.Active, State(MealKind.Dinner, out _));
        // A declined dinner (another option chosen) cancels the item; the record moving on to tomorrow leaves it so.
        record.Today.Meals.Add(new MealOutcomeRecord { Kind = MealKind.Dinner, Status = MealOutcomeStatus.ChoseAlternative });
        record.Day = 2; record.Kind = MealKind.Breakfast; record.Status = MealStatus.Pending;
        DayPlanExecutor.SyncMeals(person, record);
        Assert.Equal(PlanItemState.Cancelled, State(MealKind.Dinner, out _));
    }

    /// <summary>Opus-r1-1: with wages on, hours that change after the wage queue read the next shift and before the day's
    /// plan is drawn move the wage window with the plan, so the whole fitted shift is paid; a reload between the plan
    /// and the shift continues to the same bytes.</summary>
    [Fact]
    public void TheWageWindowFollowsThePlanWhenHoursChangeBeforeTheDayIsDrawn()
    {
        CitySimulator Build()
        {
            var city = AttendanceWageTests.Fresh(42, count: 3);
            var work = city.State.Places.Single(p => p.Id == "work"); work.OpenMinute = 600; work.CloseMinute = 1200;
            return city;
        }
        var reference = Build();
        AttendanceWageTests.Advance(reference, Monday + 300);
        var reloaded = new CitySimulator(SaveWire.Read(SaveCodec.Write(reference.State)).State!, new AttendanceWageTests.Routes());
        reloaded.AttachFoodMarket(new AttendanceWageTests.Market());
        AttendanceWageTests.Advance(reference, 3 * Monday); AttendanceWageTests.Advance(reloaded, 3 * Monday);
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(reloaded.State));
        foreach (var person in reference.State.Citizens)
        {
            Assert.Equal(0, EmploymentWages.RecentMisses(person.Accrual, 2));
            Assert.InRange(person.Accrual.AttendedMinutes, 470, 480);
            Assert.All(person.RecentVisits.Where(v => v.Activity == Activity.Work), v => Assert.InRange(v.AtMinute % 1440, 600, 602));
        }
    }

    private static string WithoutPlans(string payload) => System.Text.RegularExpressions.Regex.Replace(payload, "\"Plan\":(null|\"[^\"]*\"),", "");

    /// <summary>Plans change no routine where the workplace's hours hold the shift: with the "Plan" fields taken out, the
    /// writer goldens are byte for byte the pins from before GR-2 (a65e5a46), and the corpus's full.json is the retired one.</summary>
    [Fact]
    public void WithoutThePlanFieldsTheWriterGoldensAreTheBytesFromBeforeDayPlans()
    {
        foreach (var (seed, pin) in new[] { (42, "789f39bc21d2480f2c28609efcf121e5da274a6bb1bce2c3437ad951535bdfc8"),
            (9217, "e3f773e782b70243440b52af7cf9e003cb043b6fb1b7eb77405b8537f031c78e") })
        {
            var wages = AttendanceWageTests.Fresh(seed); AttendanceWageTests.Advance(wages, 1440 + 1100);
            Assert.Equal(pin, EconomicLedgerTests.Sha(WithoutPlans(SaveCodec.Write(wages.State))));
        }
        string visitors = WithoutPlans(SaveCodec.Write(VisitorRoutineTests.Running(2, 30).State));
        Assert.Equal("5d21a927ee4a70069a6f6c3d632e80be721d2f56394c3a0cf1b7bb29b739bf91 14145", EconomicLedgerTests.Sha(visitors) + " " + visitors.Length);
        string full = SaveCodec.Write(ChangeJournalTests.Fixture(5150));
        Assert.Equal(Corpus(Path.Combine("schema-01", "full.json")), full);
        Assert.Equal(Corpus("retired-pre-plan-full.json"), WithoutPlans(full));
    }

    private static string Corpus(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ReaderCorpus", file));

    /// <summary>A city written before day plans (schema 1, no "Plan" field) is an older development city: refused with
    /// "start a new city", never migrated, and every key kept.</summary>
    [Fact]
    public void ACityWrittenBeforeDayPlansIsRefusedWithStartANewCityAndKept()
    {
        string old = Corpus("retired-pre-plan-full.json");
        Assert.StartsWith(SaveCodec.BodyPrefix, old);
        Assert.DoesNotContain("\"Plan\":", old);
        var read = SaveCodec.Read(old);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Null(read.State);
        Assert.Contains("start a new city", read.Error);
        Assert.Contains("before day plans", read.Error);
        Assert.Equal(CityState.CurrentSchema, SaveCodec.DevelopmentSchemaOf(old));
        Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(old)).Message);

        var data = new Dictionary<string, string>(StringComparer.Ordinal) { [SaveStore.StateKey] = old, ["rlo:state"] = "owned by RLO" };
        var keys = new Dictionary<string, string>(data, StringComparer.Ordinal);
        var fresh = CurrentSchemaOnlyTests.City(); fresh.PopulationKey = JObject.Parse(old)["PopulationKey"]!.ToString();
        var session = SaveSession.Open(data, fresh.PopulationKey);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, session.InitialReadResult.Status);
        Assert.False(session.WriteTo(data, fresh).Success); Assert.False(SaveStore.WriteTo(data, fresh).Success);
        Assert.Equal(keys.Count, data.Count);
        foreach (var (key, value) in keys) Assert.Same(value, data[key]);
        string current = Corpus(Path.Combine("schema-01", "full.json"));
        Assert.Contains("\"Plan\":\"1|", current);
        Assert.Null(SaveCodec.DevelopmentSchemaOf(current));
    }
}
