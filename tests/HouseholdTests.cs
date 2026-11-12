using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>GR-6 (G3, G4): nightlife and late shifts, varied days off, and the optional household outing.</summary>
public sealed class HouseholdTests
{
    private static void Run(CitySimulator city, double to) { while (!city.AdvanceTo(to, 128).CaughtUp) { } }

    private static Dictionary<string, Place> Index(CityState state) => state.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);

    /// <summary>A household of the banded city and a day its outing is drawn on.</summary>
    private static (Household Household, HouseholdOuting Outing, int Day) FirstOuting(CitySimulator city, int fromDay = 2)
    {
        var index = Index(city.State);
        foreach (var home in city.State.Places.Where(p => p.Kind == PlaceKind.Home))
        {
            var members = city.State.Citizens.Where(c => !c.IsVisitor && c.HomeId == home.Id).ToList();
            if (members.Count < 2) continue;
            var household = new Household(home.Id, members);
            for (int day = fromDay; day < fromDay + 28; day++)
            {
                var outing = HouseholdOutings.Draw(household, city.State.Seed, day, city.State.Places, index);
                if (outing != null) return (household, outing, day);
            }
        }
        throw new InvalidOperationException("no household outing drawn");
    }

    [Fact]
    public void AnOutingIsDrawnOnceForTheHouseholdAndEveryInvitedMemberCarriesTheSameItem()
    {
        var (city, _) = NeedPurchaseTests.Banded();
        var index = Index(city.State);
        var (household, outing, day) = FirstOuting(city);
        Assert.True(outing.Invited.Count >= 2);
        Assert.Equal(PlanItemKind.Outing, outing.Item.Kind);
        Assert.Equal(outing.Item.EarliestStart + HouseholdOutings.GraceMinutes, outing.Item.LatestStart);
        foreach (var member in household.Members)
        {
            // The same draw from each member's own generation.
            var plan = DayPlanner.Generate(member, city.State.Seed, day, city.State.Places, index, false, null, household);
            var items = plan.Items.Where(i => i.Kind == PlanItemKind.Outing).ToList();
            if (!outing.Invites(member.Identity.Id)) { Assert.Empty(items); continue; }
            if (items.Count == 0) continue; // left out: it overlaps something this member planned first
            var item = Assert.Single(items);
            Assert.Equal((outing.Item.VenueId, outing.Item.EarliestStart, outing.Item.LatestStart, outing.Item.EndsAt),
                (item.VenueId, item.EarliestStart, item.LatestStart, item.EndsAt));
            // It replaced an overlapping leisure visit rather than sitting beside it.
            Assert.DoesNotContain(plan.Items, i => i.Kind == PlanItemKind.Leisure && i.EarliestStart < item.EndsAt && i.EndsAt > item.EarliestStart);
        }
        // Reproducible: the same draw again.
        Assert.Equal(outing.Item.VenueId, HouseholdOutings.Draw(household, city.State.Seed, day, city.State.Places, index)!.Item.VenueId);
    }

    [Fact]
    public void AMemberWhoDoesNotTurnUpIsMissedLateAndTheOutingIsCancelledByRule()
    {
        var (city, _) = NeedPurchaseTests.Banded();
        var index = Index(city.State);
        var (household, outing, day) = FirstOuting(city);
        var invited = household.Members.Where(m => outing.Invites(m.Identity.Id)).ToList();
        foreach (var member in household.Members)
            member.Plan = DayPlanner.Generate(member, city.State.Seed, day, city.State.Places, index, false, null, household);
        invited = invited.Where(m => m.Plan!.Items.Any(i => i.Kind == PlanItemKind.Outing)).ToList();
        Assert.True(invited.Count >= 2, "two members carry the outing");
        var item = outing.Item;
        Assert.Equal(OutingOutcome.Pending, HouseholdOutings.Outcome(household, day));
        // The first member arrives on time; nobody else turns up.
        var arrived = invited[0];
        DayPlanExecutor.Mark(arrived, Activity.Leisure, item.EndsAt, PlanItemState.Active);
        Assert.Equal(OutingOutcome.Pending, HouseholdOutings.Outcome(household, day));
        // At their first event after the latest start, everyone else is missed late and no longer sets off for it.
        foreach (var member in invited.Skip(1))
        {
            DayPlanExecutor.Settle(member, item.LatestStart + 0.5);
            int at = member.Plan!.Items.ToList().FindIndex(i => i.Kind == PlanItemKind.Outing);
            Assert.Equal(PlanItemState.Missed, member.Plan.StateOf(at));
            Assert.Equal(PlanMissCause.Late, member.Plan.CauseOf(at));
            var keys = RoutineKeys.For(member, city.State.Seed);
            var quiet = new DailyRhythm.Lookup(member, keys);
            var routine = DayPlanner.On(member, keys, day, index, ref quiet);
            Assert.False(routine.HasLeisure && routine.LeisureVenue == item.VenueId && routine.LeisureUntil == item.EndsAt);
            // A late arrival is never admitted to the outing.
            DayPlanExecutor.Mark(member, Activity.Leisure, item.EndsAt, PlanItemState.Active);
            Assert.Equal(PlanItemState.Missed, member.Plan.StateOf(at));
        }
        Assert.Equal(OutingOutcome.Cancelled, HouseholdOutings.Outcome(household, day));
        // The arrived member stays as an ordinary visit: their item is not cut and ends done.
        DayPlanExecutor.Settle(arrived, item.LatestStart + 0.5);
        int mine = arrived.Plan!.Items.ToList().FindIndex(i => i.Kind == PlanItemKind.Outing);
        Assert.Equal(PlanItemState.Active, arrived.Plan.StateOf(mine));
        DayPlanExecutor.Settle(arrived, item.EndsAt);
        Assert.Equal(PlanItemState.Done, arrived.Plan.StateOf(mine));
        Assert.Equal(OutingOutcome.Cancelled, HouseholdOutings.Outcome(household, day));

        // With a second member on time the same outing is held.
        foreach (var member in household.Members)
            member.Plan = DayPlanner.Generate(member, city.State.Seed, day, city.State.Places, index, false, null, household);
        DayPlanExecutor.Mark(invited[0], Activity.Leisure, item.EndsAt, PlanItemState.Active);
        DayPlanExecutor.Mark(invited[1], Activity.Leisure, item.EndsAt, PlanItemState.Active);
        Assert.Equal(OutingOutcome.Held, HouseholdOutings.Outcome(household, day));
    }

    [Fact]
    public void InTheRunningCityEveryOutingEndsHeldOrCancelledByTheRuleAndEachMemberPaysFromTheirOwnWallet()
    {
        var (city, market) = NeedPurchaseTests.Banded();
        city.HouseholdOutingsEnabled = true;
        foreach (var person in city.State.Citizens.Where(c => c.Wallet != null))
        {
            person.Needs = new PersonNeeds();
            person.Needs.Add(new NeedState("ba:businesstype_gym", 0, "", 0));
        }
        int held = 0, cancelled = 0, paidOnOutings = 0;
        for (int day = 2; day < 16; day++)
        {
            Run(city, day * 1440.0 + DayPlanner.DayBoundaryMinute + 1430);
            foreach (var home in city.State.Places.Where(p => p.Kind == PlaceKind.Home))
            {
                var members = city.State.Citizens.Where(c => !c.IsVisitor && c.HomeId == home.Id).ToList();
                if (members.Count < 2) continue;
                var household = new Household(home.Id, members);
                var outcome = HouseholdOutings.Outcome(household, day);
                if (outcome == OutingOutcome.None) continue;
                Assert.NotEqual(OutingOutcome.Pending, outcome);
                int arrived = 0;
                foreach (var member in members)
                {
                    var plan = member.Plan!;
                    for (int i = 0; i < plan.Items.Count; i++)
                    {
                        var item = plan.Items[i];
                        if (item.Kind != PlanItemKind.Outing) continue;
                        var state = plan.StateOf(i);
                        Assert.True(state == PlanItemState.Done || state == PlanItemState.Missed || state == PlanItemState.Cancelled, state.ToString());
                        if (state == PlanItemState.Done) arrived++;
                        // Every purchase at the outing is this member's own contract call on their own wallet.
                        var mine = market.Log.Where(l => l.Venue == item.VenueId && l.Minute >= item.EarliestStart && l.Minute < item.EndsAt
                            && l.Resident == member.Identity.Id && l.Refusal == PurchaseRefusal.None).ToList();
                        foreach (var purchase in mine)
                        {
                            paidOnOutings++;
                            Assert.Contains(market.Store, r => r.Key.Contains(Uri.EscapeDataString(member.Identity.Id) + ":") && r.Value.StartsWith("paid|", StringComparison.Ordinal));
                        }
                    }
                }
                Assert.Equal(arrived >= 2 ? OutingOutcome.Held : OutingOutcome.Cancelled, outcome);
                if (outcome == OutingOutcome.Held) held++; else cancelled++;
                // The outing is saved with the plans and reads back exactly.
                if (held + cancelled == 1)
                {
                    string payload = SaveCodec.Write(city.State);
                    var read = SaveWire.Read(payload);
                    Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
                    var byId = read.State!.Citizens.ToDictionary(c => c.Identity.Id);
                    Assert.All(members, m => Assert.True(byId[m.Identity.Id].Plan!.SameAs(m.Plan!)));
                }
            }
        }
        Assert.True(held > 0, "some outing was held");
        Assert.True(paidOnOutings >= 2, "members bought at an outing");
        // Wallets stay individual: each resident's paid receipts equal their own discretionary spending.
        var paid = market.Store.Where(r => r.Value.StartsWith("paid|", StringComparison.Ordinal)).ToList();
        foreach (var person in city.State.Citizens.Where(c => c.Wallet != null))
        {
            string key = ":" + Uri.EscapeDataString(person.Identity.Id) + ":";
            var total = Money.Zero;
            foreach (var receipt in paid.Where(r => r.Key.Contains(key)))
            { Assert.True(Money.TryParseCents(receipt.Value.Split('|')[1], 28, out var amount)); total += amount; }
            Assert.Equal(total, person.Wallet!.Ledger.Discretionary);
        }
    }

    [Fact]
    public void VisitorsGetNoHouseholdAndNeverAnOuting()
    {
        var (city, _) = NeedPurchaseTests.Banded();
        city.HouseholdOutingsEnabled = true;
        Run(city, 1440 * 9);
        var visitors = PersonTable.Schedules(city.State).Where(c => c.IsVisitor).ToList();
        Assert.NotEmpty(visitors);
        Assert.All(visitors, v => Assert.Null(city.HouseholdOf(v)));
        Assert.All(visitors, v => Assert.DoesNotContain(v.Plan?.Items ?? Array.Empty<PlanItem>(), i => i.Kind == PlanItemKind.Outing));
        var resident = city.State.Citizens.First(c => !c.IsVisitor && city.HouseholdOf(c) != null);
        var household = city.HouseholdOf(resident)!;
        Assert.All(household.Members, m => { Assert.False(m.IsVisitor); Assert.Equal(resident.HomeId, m.HomeId); });
        Assert.Throws<ArgumentException>(() => new Household(resident.HomeId, household.Members.Append(visitors[0]).ToList()));
    }

    private static Place Typed(CityState state, string type, int from = 18 * 60, int to = 24 * 60)
    {
        var workplace = state.Places.Where(p => p.Kind == PlaceKind.Work)
            .OrderByDescending(p => state.Citizens.Count(c => !c.IsVisitor && c.DayPlaceId == p.Id)).First();
        workplace.BusinessType = type;
        workplace.WeeklyHours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = from, ToMinute = to }).ToList();
        return workplace;
    }

    [Fact]
    public void WeekendTradingVenuesVaryDaysOffAndSetLateAndNightShifts()
    {
        var (state, _) = WorkplaceCapacityTests.Fresh(3, 5150);
        var index = Index(state);
        var untyped = state.Citizens.First(c => !c.IsVisitor && c.DayPlaceId.Length > 0 && c.Identity.Persona == Persona.Commuter);
        Assert.Equal(DayPlanner.DefaultDaysOff, DayPlanner.DaysOff(untyped, index));
        Assert.Equal(DayPlanner.ShiftOf(untyped), DayPlanner.ShiftOf(untyped, index));

        var club = Typed(state, "ba:businesstype_nightclub", 20 * 60, 24 * 60);
        // A night shift needs eight open hours: the club opens 16:00 to midnight.
        club.WeeklyHours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 16 * 60, ToMinute = 24 * 60 }).ToList();
        var staff = state.Citizens.Where(c => !c.IsVisitor && c.DayPlaceId == club.Id && c.Identity.Persona != Persona.Student && c.Identity.Persona != Persona.Retiree).ToList();
        Assert.True(staff.Count >= 2);
        var masks = new HashSet<int>();
        foreach (var person in staff)
        {
            int mask = DayPlanner.DaysOff(person, index);
            masks.Add(mask);
            Assert.Equal(3, Enumerable.Range(0, 7).Count(d => (mask >> d & 1) != 0));
            Assert.Equal(ShiftType.Night, DayPlanner.ShiftOf(person, index));
            // Drawn once from the person ID: the same on every call and day.
            Assert.Equal(mask, DayPlanner.DaysOff(person, index));
            int worked = 0;
            for (int day = 2; day < 9; day++)
            {
                var keys = RoutineKeys.For(person, state.Seed);
                var work = DayPlanner.WorkOn(person, day, keys.HourShift, index);
                Assert.Equal(DayPlanner.DayOff(person, day, index), !work.HasValue);
                if (!work.HasValue) continue;
                worked++;
                Assert.True(DailyRoutine.Open(club, work.Value.StartsAt) && DailyRoutine.Open(club, work.Value.EndsAt - 1));
            }
            Assert.Equal(4, worked);
        }
        var cinema = Typed(state, "ba:businesstype_cinema", 10 * 60, 23 * 60);
        Assert.All(state.Citizens.Where(c => !c.IsVisitor && c.DayPlaceId == cinema.Id && c.Identity.Persona != Persona.Student),
            p => Assert.Equal(ShiftType.Late, DayPlanner.ShiftOf(p, index)));
        // Across many persons every block shows up.
        var blocks = Enumerable.Range(0, 400).Select(i => WorkSchedules.DaysOff(club, WorkSchedules.DaysOffDraw("p" + i))).Distinct().Count();
        Assert.Equal(7, blocks);
        // A venue closed on Monday and Tuesday puts days off there.
        var weekdays = new Place { Kind = PlaceKind.Work, BusinessType = "ba:businesstype_gym",
            WeeklyHours = Enumerable.Range(2, 5).Select(d => new OpeningWindow { Day = d, FromMinute = 600, ToMinute = 1200 }).ToList() };
        Assert.All(Enumerable.Range(0, 50), i => Assert.True((WorkSchedules.DaysOff(weekdays, WorkSchedules.DaysOffDraw("q" + i)) & 0b11) != 0));
    }

    [Fact]
    public void OnTheEveningBeforeADayOffSomeResidentsGoOutLateToANightlifeVenue()
    {
        var (state, _) = WorkplaceCapacityTests.Fresh(3, 5150);
        var club = new Place { Id = "leisure:club", Name = "Club", Kind = PlaceKind.Leisure, Capacity = 64, Position = new Position(40, 0, -40),
            BusinessType = "ba:businesstype_nightclub",
            WeeklyHours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 20 * 60, ToMinute = 24 * 60 }).ToList() };
        state.Places.Add(club);
        var index = Index(state);
        int thursday = Enumerable.Range(2, 7).First(d => DayPlanner.Weekday(d) == 3), monday = thursday - 3;
        int nights = 0;
        foreach (var person in state.Citizens.Where(c => !c.IsVisitor))
        {
            var plan = DayPlanner.Generate(person, state.Seed, thursday, state.Places, index, false);
            var late = plan.Items.Where(i => i.VenueId == club.Id && i.EarliestStart >= thursday * 1440.0 + DayPlanner.NightlifeStartMinute - 15).ToList();
            if (late.Count == 0) continue;
            nights++;
            Assert.NotEqual(Persona.Parent, person.Identity.Persona);
            Assert.NotEqual(Persona.Retiree, person.Identity.Persona);
            Assert.All(late, i => Assert.Equal(PlanItemKind.Leisure, i.Kind));
        }
        Assert.True(nights > 0, "somebody went out on Thursday night");
        // Not on an evening before a workday: Monday's plans are the plans with the club as an ordinary leisure venue.
        var mondays = state.Citizens.Where(c => !c.IsVisitor).Select(p => DayPlanner.Generate(p, state.Seed, monday, state.Places, index, false)).ToList();
        club.BusinessType = "";
        var ordinary = state.Citizens.Where(c => !c.IsVisitor).Select(p => DayPlanner.Generate(p, state.Seed, monday, state.Places, index, false)).ToList();
        Assert.All(mondays.Zip(ordinary), pair => Assert.True(pair.First.SameAs(pair.Second)));
        // Without a nightlife venue the plans are as before.
        state.Places.Remove(club);
        var plain = Index(state);
        foreach (var person in state.Citizens.Where(c => !c.IsVisitor).Take(20))
            Assert.DoesNotContain(DayPlanner.Generate(person, state.Seed, thursday, state.Places, plain, false).Items, i => i.VenueId == club.Id);
    }
}
