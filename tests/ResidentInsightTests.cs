using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class ResidentInsightTests
{
    private readonly ITestOutputHelper output;
    public ResidentInsightTests(ITestOutputHelper output) { this.output = output; }

    private const string HomeId = "address:Maple%20Street:12:home";
    private const string WorkId = "address:Harbor%20Road:4:work";
    private const string ShopId = "address:Market%20Lane:3:shop";
    private const string ParkId = "outdoor:park:1";

    private static Dictionary<string, Place> Places() => new[] {
        new Place { Id = HomeId, Name = "Maple Street 12", Kind = PlaceKind.Home, Capacity = 4 },
        new Place { Id = WorkId, Name = "Harbor Logistics", Kind = PlaceKind.Work, Capacity = 4, Position = new(1, 0, 0) },
        new Place { Id = ShopId, Name = "Corner Bakery", Kind = PlaceKind.Shop, Position = new(2, 0, 0) },
        new Place { Id = ParkId, Name = "City Park", Kind = PlaceKind.Leisure, Position = new(3, 0, 0) } }
        .ToDictionary(p => p.Id);

    private static (CityState city, CitizenState person, Dictionary<string, Place> places) Fixture(double minute = 1440 + 600)
    {
        var places = Places();
        var person = new CitizenState
        {
            Identity = new CitizenIdentity { Id = "pc:1", Name = "Ada Moreno", Age = 34, Persona = Persona.Commuter },
            HomeId = HomeId, DayPlaceId = WorkId, PlaceId = WorkId, Activity = Activity.Work, Phase = CitizenPhase.AtPlace,
            ActivityUntilMinute = 1440 + 1020, NextEventMinute = 1440 + 1020,
            Preferences = new TravelPreferences { WalkingToleranceMetres = 2000, OwnsCar = false, AllowTransit = true, FareWeightMinutes = 2 }
        };
        var city = new CityState { Minute = minute, Seed = 7, Places = places.Values.ToList(), Citizens = new() { person },
            Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion } };
        return (city, person, places);
    }

    private static ResidentMeal Meal(MealKind kind, MealStatus status, int day = 1, string reason = "") => new ResidentMeal
    { ResidentId = "pc:1", AnchorId = "pc:1", Slot = 0, Day = day, Kind = kind, Status = status, Reason = reason,
      PlaceId = status == MealStatus.Pending || status == MealStatus.Declined || status == MealStatus.Missed ? "" : ShopId,
      ProductId = status == MealStatus.Pending ? "" : FoodProducts.Croissant, DesiredAt = day * 1440 + 480 };

    private static ResidentInsight Capture(CityState city, CitizenState person, ResidentMeal? meal, Dictionary<string, Place> places)
        => ResidentInsight.Capture(city, person, meal, id => places.TryGetValue(id, out var p) ? p : null);

    [Fact]
    public void StoryComesFirstThenDestinationThenTodaysThreeMeals()
    {
        var (city, person, places) = Fixture();
        var insight = Capture(city, person, Meal(MealKind.Lunch, MealStatus.Paid), places);
        string text = insight.Text();
        int story = text.IndexOf(insight.Story, StringComparison.Ordinal);
        int heading = text.IndexOf(insight.Heading, StringComparison.Ordinal);
        int meals = text.IndexOf("Today's meals", StringComparison.Ordinal);
        Assert.Equal(0, story);
        Assert.True(story < heading && heading < meals, text);
        Assert.StartsWith("Ada Moreno, 34, is a commuter.", insight.Story);
        Assert.Contains("Lives at Maple Street 12.", insight.Story);
        Assert.Contains("Works at Harbor Logistics, Harbor Road 4.", insight.Story);
        Assert.Contains("Right now: at work at Harbor Logistics, Harbor Road 4 until 17:00.", insight.Story);
        var lines = text.Split('\n');
        int b = Array.FindIndex(lines, l => l.StartsWith("Breakfast: ")), l = Array.FindIndex(lines, x => x.StartsWith("Lunch: ")),
            d = Array.FindIndex(lines, x => x.StartsWith("Dinner: "));
        Assert.True(meals < text.IndexOf("Breakfast: ", StringComparison.Ordinal) && b < l && l < d, text);
        Assert.Equal(3, insight.Meals.Count);
        Assert.Equal(new[] { MealKind.Breakfast, MealKind.Lunch, MealKind.Dinner }, insight.Meals.Select(m => m.Kind));
    }

    [Fact]
    public void EarlierMealsAreNotRecordedTheLatestIsDescribedAndLaterOnesAreUnknown()
    {
        var (city, person, places) = Fixture();
        var insight = Capture(city, person, Meal(MealKind.Lunch, MealStatus.Paid), places);
        Assert.Equal(ResidentMealState.NotRecorded, insight.Meals[0].State);
        Assert.Contains("not recorded", insight.Meals[0].Text);
        Assert.Equal(ResidentMealState.Bought, insight.Meals[1].State);
        Assert.Equal("bought croissant at Corner Bakery, Market Lane 3", insight.Meals[1].Text);
        Assert.Equal(ResidentMealState.Unknown, insight.Meals[2].State);
        Assert.Equal("unknown", insight.Meals[2].Text);

        var states = new Dictionary<MealStatus, (ResidentMealState state, string text)>
        {
            [MealStatus.Pending] = (ResidentMealState.Unknown, "unknown (deciding)"),
            [MealStatus.Travelling] = (ResidentMealState.InProgress, "on the way to Corner Bakery, Market Lane 3 for croissant"),
            [MealStatus.Serving] = (ResidentMealState.InProgress, "waiting for croissant at Corner Bakery, Market Lane 3"),
            [MealStatus.Declined] = (ResidentMealState.HouseholdFood, "chose food from home instead of buying"),
            [MealStatus.Missed] = (ResidentMealState.Skipped, "skipped: no suitable stop in the meal window"),
            [MealStatus.Uncertain] = (ResidentMealState.Unconfirmed, "purchase of croissant at Corner Bakery, Market Lane 3 not yet confirmed"),
        };
        foreach (var (status, expected) in states)
        {
            var meal = Capture(city, person, Meal(MealKind.Breakfast, status, reason: "No suitable stop in the meal window"), places).Meals[0];
            Assert.Equal(expected.state, meal.State);
            Assert.Equal(expected.text, meal.Text);
        }
        // An unconfirmed purchase is never reported as bought or skipped.
        var uncertain = Capture(city, person, Meal(MealKind.Dinner, MealStatus.Uncertain), places);
        Assert.DoesNotContain(uncertain.Meals, m => m.State == ResidentMealState.Bought || m.State == ResidentMealState.Skipped);
    }

    [Fact]
    public void MealsAreUnknownWithoutCurrentDayEvidence()
    {
        var (city, person, places) = Fixture();
        // Yesterday's dinner says nothing about today.
        var yesterday = Capture(city, person, Meal(MealKind.Dinner, MealStatus.Paid, day: 0), places);
        Assert.All(yesterday.Meals, m => Assert.Equal(ResidentMealState.Unknown, m.State));
        // No record at all.
        var none = Capture(city, person, null, places);
        Assert.All(none.Meals, m => Assert.Equal("unknown", m.Text));
        Assert.Contains("No meal record", none.MealNote);
        // Food demand inactive: never invent meals.
        city.Food = null;
        var inactive = Capture(city, person, Meal(MealKind.Lunch, MealStatus.Paid), places);
        Assert.All(inactive.Meals, m => Assert.Equal(ResidentMealState.Unknown, m.State));
        Assert.Contains("unavailable", inactive.MealNote);
        // The earlier single daily meal is labelled, not mapped onto breakfast/lunch/dinner.
        city.Food = new FoodCommerceState();
        var legacy = Capture(city, person, Meal(MealKind.Legacy, MealStatus.Paid), places);
        Assert.All(legacy.Meals, m => Assert.Equal(ResidentMealState.Unknown, m.State));
        Assert.Contains("single daily meal: bought croissant", legacy.MealNote);
        // A represented consumer's record is never shown as the visible person's own plate.
        var represented = Meal(MealKind.Lunch, MealStatus.Paid); represented.Slot = 1; represented.ResidentId = "consumer:pc%3A1:1";
        city.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion };
        Assert.All(Capture(city, person, represented, places).Meals, m => Assert.Equal(ResidentMealState.Unknown, m.State));
    }

    [Fact]
    public void DestinationAndReasonAreDescribedInWords()
    {
        var (city, person, places) = Fixture(1440 + 500);
        Assert.Equal("Not travelling: at work at Harbor Logistics, Harbor Road 4", Capture(city, person, null, places).Heading);
        Assert.Equal("stays until 17:00", Capture(city, person, null, places).HeadingReason);

        person.Phase = CitizenPhase.Travelling; person.PlaceId = HomeId; person.Activity = Activity.Home;
        person.DestinationId = WorkId; person.DestinationActivity = Activity.Work;
        person.DepartureMinute = 1440 + 495; person.NextEventMinute = 1440 + 512; person.ActivityUntilMinute = 1440 + 990;
        person.Journey = new Route { Legs = new() { new RouteLeg { Mode = TravelMode.Walk, Minutes = 3 }, new RouteLeg { Mode = TravelMode.Bus, Minutes = 14 } } };
        var commute = Capture(city, person, null, places);
        Assert.Equal("Heading to Harbor Logistics, Harbor Road 4, arriving about 08:32", commute.Heading);
        Assert.Equal("for work until 16:30", commute.HeadingReason);
        Assert.Contains("Right now: riding the bus to Harbor Logistics", commute.Story);

        person.DestinationId = ShopId; person.DestinationActivity = Activity.Shop;
        person.MealOuting = new MealOuting { OriginId = WorkId, OriginActivity = Activity.Work };
        var lunch = Meal(MealKind.Lunch, MealStatus.Travelling);
        Assert.Equal("to buy croissant for lunch", Capture(city, person, lunch, places).HeadingReason);
        Assert.Equal("to get something to eat", Capture(city, person, null, places).HeadingReason);

        person.MealOuting.Returning = true; person.DestinationId = WorkId; person.DestinationActivity = Activity.Work;
        Assert.Equal("going back to work after a meal break", Capture(city, person, lunch, places).HeadingReason);

        person.MealOuting = null; person.DestinationId = HomeId; person.DestinationActivity = Activity.Home;
        Assert.Equal("to go home", Capture(city, person, null, places).HeadingReason);
        person.DestinationId = ParkId; person.DestinationActivity = Activity.Leisure;
        var park = Capture(city, person, null, places);
        Assert.Equal("Heading to City Park, arriving about 08:32", park.Heading);
        Assert.Equal("for some free time until 16:30", park.HeadingReason);

        person.Phase = CitizenPhase.WaitingForOpening; person.DestinationId = ShopId; person.DestinationActivity = Activity.Shop;
        var waiting = Capture(city, person, null, places);
        Assert.Equal("to go shopping until 16:30; waiting for it to open", waiting.HeadingReason);
        Assert.Contains("waiting for Corner Bakery, Market Lane 3 to open", waiting.Story);
    }

    [Fact]
    public void AddressesLinkOnlyToNativeStreetAddresses()
    {
        Assert.True(ResidentAddress.TryParse("address:Rue%20de%20la%20Paix%3A2:15:home", out string street, out int number));
        Assert.Equal("Rue de la Paix:2", street); Assert.Equal(15, number);
        foreach (var id in new[] { "", "outdoor:park:1", "address::4:home", "address:Main:x:home", "address:Main:4", "address:Main:-4:home", "address:Main:4:home:extra" })
            Assert.False(ResidentAddress.TryParse(id, out _, out _), id);

        var (city, person, places) = Fixture();
        var insight = Capture(city, person, null, places);
        Assert.True(insight.Home!.CanNavigate);
        Assert.Equal("Maple Street 12", insight.Home.Label);
        Assert.Equal(("Maple Street", 12), (insight.Home.Street, insight.Home.Number));
        Assert.Equal("Harbor Logistics, Harbor Road 4", insight.Work!.Label);
        Assert.Equal("Works at", insight.WorkRole);
        Assert.False(ResidentAddress.From(places[ParkId]).CanNavigate);
        Assert.Equal("City Park", ResidentAddress.From(places[ParkId]).Label);

        person.DayPlaceId = ""; person.Identity.Persona = Persona.Retiree;
        var retired = Capture(city, person, null, places);
        Assert.Null(retired.Work); Assert.Equal("Retired", retired.WorkRole);
        Assert.Contains("no workday", retired.Personality);
    }

    [Fact]
    public void PersonalityUsesOnlyExistingFactsInWords()
    {
        var (city, person, places) = Fixture();
        var words = Capture(city, person, null, places).Personality;
        Assert.Equal(new[] { "keeps a regular workday", "happy to walk far" }, words);
        Assert.DoesNotContain(words, w => w.Any(char.IsDigit));
        person.Identity.Persona = Persona.Student; person.Preferences = new TravelPreferences { WalkingToleranceMetres = 700, AllowTransit = false, FareWeightMinutes = 5 };
        // A represented consumer's legacy taste is not the clicked person's; only the resident's own traits are described.
        var meal = Meal(MealKind.Lunch, MealStatus.Pending); meal.PreferredProductId = FoodProducts.Burger;
        Assert.Equal(new[] { "on a student budget", "often packs lunch", "prefers short walks", "avoids public transport", "minds fares" },
            Capture(city, person, meal, places).Personality);
    }

    private static MealOutcomeRecord Outcome(MealKind kind, MealOutcomeStatus status, MealReasonCode reason = MealReasonCode.Other,
        FoodOutsideOption outside = FoodOutsideOption.None, string place = ShopId) => new MealOutcomeRecord
    { Kind = kind, Status = status, Reason = reason, Outside = outside, At = 1440 + 600,
      PlaceId = status == MealOutcomeStatus.ChoseAlternative ? "" : place, ProductId = status == MealOutcomeStatus.ChoseAlternative ? "" : FoodProducts.Croissant };

    [Fact]
    public void TodaysMealsComeFromRecordedOutcomesAndNeverClaimConsumption()
    {
        var (city, person, places) = Fixture(1440 + 1200);
        var meal = Meal(MealKind.Dinner, MealStatus.Uncertain);
        meal.Today = new ResidentFoodToday { Day = 1, Meals = {
            Outcome(MealKind.Breakfast, MealOutcomeStatus.ChoseAlternative, MealReasonCode.PrefersAlternative, FoodOutsideOption.FundedHouseholdFood),
            Outcome(MealKind.Lunch, MealOutcomeStatus.Skipped, MealReasonCode.QueueTooLong),
            Outcome(MealKind.Dinner, MealOutcomeStatus.Unconfirmed, MealReasonCode.CheckoutUnconfirmed) } };
        string before = Newtonsoft.Json.JsonConvert.SerializeObject(new { city, meal });
        var insight = Capture(city, person, meal, places);
        Assert.Equal(new[] { ResidentMealState.HouseholdFood, ResidentMealState.Skipped, ResidentMealState.Unconfirmed }, insight.Meals.Select(m => m.State));
        Assert.Equal("chose household food", insight.Meals[0].Text);
        Assert.Equal("skipped: the queue was too long", insight.Meals[1].Text);
        Assert.Equal("purchase of croissant at Corner Bakery, Market Lane 3 not yet confirmed", insight.Meals[2].Text);
        Assert.DoesNotContain(insight.Meals, m => m.Text.Contains("ate") || m.Text.Contains("eaten"));
        Assert.DoesNotContain("paid", insight.Meals[2].Text); Assert.DoesNotContain("skipped", insight.Meals[2].Text);
        // Opening the inspector reads only: no outcome, meal or city field changes.
        Assert.Equal(before, Newtonsoft.Json.JsonConvert.SerializeObject(new { city, meal }));
    }

    [Fact]
    public void UpgradedEarlierMealsAreNotRecordedAndAMealUnderWayIsShownLive()
    {
        var (city, person, places) = Fixture();
        var meal = Meal(MealKind.Lunch, MealStatus.Travelling);
        // Upgraded at lunch: breakfast ended before schema 17; a skipped lunch was revived and is under way again.
        meal.Today = new ResidentFoodToday { Day = 1, Unrecorded = 1, Meals = { Outcome(MealKind.Lunch, MealOutcomeStatus.Skipped, MealReasonCode.NoSuitableStop) } };
        var insight = Capture(city, person, meal, places);
        Assert.Equal((ResidentMealState.NotRecorded, "not recorded (before the upgrade)"), (insight.Meals[0].State, insight.Meals[0].Text));
        Assert.Equal(ResidentMealState.InProgress, insight.Meals[1].State);
        Assert.Equal("on the way to Corner Bakery, Market Lane 3 for croissant", insight.Meals[1].Text);
        Assert.Equal((ResidentMealState.Unknown, "unknown"), (insight.Meals[2].State, insight.Meals[2].Text));
        // A finished meal is read from its record, including a shop that has since left the catalogue.
        meal.Status = MealStatus.Paid;
        meal.Today.Meals[0] = Outcome(MealKind.Lunch, MealOutcomeStatus.Bought, MealReasonCode.Bought, place: "address:Gone%20Street:1:shop");
        Assert.Equal("bought croissant at a shop no longer available", Capture(city, person, meal, places).Meals[1].Text);
    }

    [Fact]
    public void OutcomesOfAnEarlierDayAreNotShownAsTodaysAndMissingFoodSaysUnavailable()
    {
        var (city, person, places) = Fixture(2 * 1440 + 300);
        var meal = Meal(MealKind.Dinner, MealStatus.Paid);
        meal.Today = new ResidentFoodToday { Day = 1, Meals = { Outcome(MealKind.Dinner, MealOutcomeStatus.Bought, MealReasonCode.Bought) } };
        var insight = Capture(city, person, meal, places);
        Assert.All(insight.Meals, m => Assert.Equal(ResidentMealState.Unknown, m.State));
        Assert.Equal(1, meal.Today.Day);
        city.Food = null;
        var none = Capture(city, person, meal, places);
        Assert.Contains("Meal data is unavailable", none.MealNote);
        Assert.Null(city.Food);
        // The legacy single daily meal is read from its own record.
        city.Food = new FoodCommerceState();
        var legacy = Meal(MealKind.Legacy, MealStatus.Declined, day: 2);
        legacy.Today = new ResidentFoodToday { Day = 2, Meals = { Outcome(MealKind.Legacy, MealOutcomeStatus.ChoseAlternative, MealReasonCode.PrefersAlternative, FoodOutsideOption.PackedMeal) } };
        Assert.Equal("This city uses the earlier single daily meal: chose a packed meal.", Capture(city, person, legacy, places).MealNote);
    }

    [Fact]
    public void TraitsAreDescribedInWordsWithoutNumbersAndNeutralOrInactiveTraitsStaySilent()
    {
        var (city, person, places) = Fixture();
        // Croissant 4, burger 0, hot dog 2, price 3, queue 1, loyalty 4, travel 4, health 3, quality 3, time pressure 0.
        person.Traits = new ResidentTraits { Version = 1, Bands = "4023144330" };
        var words = Capture(city, person, null, places).Personality;
        Assert.Equal(new[] { "keeps a regular workday", "happy to walk far", "loves croissants", "dislikes burgers", "watches prices",
            "dislikes queues", "happy to travel far for food", "prefers lighter food", "values quality", "takes their time over meals" }, words);
        Assert.DoesNotContain(words, w => w.Any(char.IsDigit));
        // Loyalty has no effect until shop memory exists, so it is never described.
        Assert.DoesNotContain(words, w => w.Contains("loyal") || w.Contains("curious"));
        person.Traits = new ResidentTraits { Version = 1, Bands = "2222222222" };
        Assert.Equal(new[] { "keeps a regular workday", "happy to walk far" }, Capture(city, person, null, places).Personality);
    }

    [Fact]
    public void PickThrottleRunsOnlyAfterMovementAndAtMostOncePerInterval()
    {
        var throttle = new ResidentPickThrottle(0.05, 1f);
        Assert.True(throttle.ShouldPick(0, 100, 100));          // first observation
        for (int frame = 1; frame < 100; frame++)
            Assert.False(throttle.ShouldPick(frame * 0.016, 100, 100)); // still cursor: never
        Assert.True(throttle.ShouldPick(2.0, 120, 100));        // moved
        Assert.False(throttle.ShouldPick(2.01, 140, 100));      // moved again within the interval
        Assert.False(throttle.ShouldPick(2.03, 140, 100));      // stopped, still throttled
        Assert.True(throttle.ShouldPick(2.05, 140, 100));       // pending move picked once the interval elapses
        Assert.False(throttle.ShouldPick(2.2, 140, 100));       // and not again while still
        Assert.False(throttle.ShouldPick(2.3, 140.5f, 100.5f)); // sub-pixel jitter is not movement
        Assert.False(throttle.ShouldPick(2.4, float.NaN, 0));
        throttle.Invalidate();
        Assert.True(throttle.ShouldPick(2.5, 140, 100));
        Assert.Equal(4, throttle.Picks);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResidentPickThrottle(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResidentPickThrottle(0.05, 0));
    }

    [Fact]
    public void NativeStreetKeysAreLocalizedForDisplayButKeptRawForTheMap()
    {
        // GamePlaces names homes by their raw native address, e.g. "ba:street_thirdstreet 30".
        const string home = "address:ba%3Astreet_thirdstreet:30:home", work = "address:ba%3Astreet_pier:7:work";
        var places = new[] {
            new Place { Id = home, Name = "ba:street_thirdstreet 30", Kind = PlaceKind.Home },
            new Place { Id = work, Name = "ba:street_pier 7", Kind = PlaceKind.Work } }.ToDictionary(p => p.Id);
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "pc:9", Name = "Lee", Age = 40, Persona = Persona.Commuter },
            HomeId = home, DayPlaceId = work, PlaceId = home, Activity = Activity.Home, Phase = CitizenPhase.AtPlace };
        var city = new CityState { Minute = 1440 + 600, Places = places.Values.ToList(), Citizens = new() { person } };
        var names = new Dictionary<string, string> { ["ba:street_thirdstreet"] = "Third Street", ["ba:street_pier"] = "Pier Road" };
        var insight = ResidentInsight.Capture(city, person, null, id => places.TryGetValue(id, out var p) ? p : null,
            localize: key => names.TryGetValue(key, out var name) ? name : key);
        Assert.Equal("Third Street 30", insight.Home!.Label);
        Assert.Equal("Pier Road 7", insight.Work!.Label);
        Assert.Equal(("ba:street_thirdstreet", 30), (insight.Home.Street, insight.Home.Number)); // the map link matches the native key
        Assert.Contains("Lives at Third Street 30.", insight.Story);
        Assert.Contains("Works at Pier Road 7.", insight.Story);
        Assert.Contains("at home", insight.Story);
        Assert.DoesNotContain("ba:street", insight.Text());
        Assert.DoesNotContain("ba:street", insight.Story);
        // Without a localizer (and for a blank translation) the raw key remains visible rather than empty.
        Assert.Equal("ba:street_thirdstreet 30", ResidentAddress.From(places[home]).Label);
        Assert.Equal("ba:street_pier 7", ResidentAddress.From(places[work], _ => " ").Label);
    }

    [Fact]
    public void ResettingThePickCountStartsANewWindowWithoutChangingScheduling()
    {
        var throttle = new ResidentPickThrottle(0.05, 1f);
        Assert.True(throttle.ShouldPick(0, 10, 10));
        Assert.True(throttle.ShouldPick(1, 30, 10));
        Assert.False(throttle.ShouldPick(1.01, 50, 10)); // pending move, throttled
        Assert.Equal(2, throttle.Picks);
        throttle.ResetPicks();
        Assert.Equal(0, throttle.Picks);
        Assert.False(throttle.ShouldPick(1.02, 50, 10)); // still throttled after the reset
        Assert.True(throttle.ShouldPick(1.06, 50, 10));  // the pending move survives the reset
        for (int frame = 0; frame < 50; frame++) Assert.False(throttle.ShouldPick(2 + frame * .016, 50, 10)); // still cursor window
        Assert.Equal(1, throttle.Picks);
    }

    // One frame of input as MouseController.Run would see it, with the current cursor position.
    private static NativeMouseAction Frame(InspectorPointerGesture gesture, bool down, bool pressed = false, bool released = false,
        bool nativeHold = false, bool onInspector = false) => gesture.Decide(pressed, released, down, nativeHold, onInspector);

    [Fact]
    public void AClickThatStartsOnTheInspectorNeverReachesTheGame()
    {
        var gesture = new InspectorPointerGesture();
        // The cursor moves into the popup and presses in the same frame: decided from the current position.
        Assert.Equal(NativeMouseAction.Allow, Frame(gesture, down: false, onInspector: false));
        Assert.Equal(NativeMouseAction.Suppress, Frame(gesture, down: true, pressed: true, onInspector: true));
        Assert.True(gesture.Owned);
        // Dragging out of the popup (e.g. a scrollbar) while held stays with the inspector, so no hold-walk starts.
        Assert.Equal(NativeMouseAction.Suppress, Frame(gesture, down: true, onInspector: false));
        // The release outside still belongs to the gesture and ends it, clearing any native hold flags.
        Assert.Equal(NativeMouseAction.SuppressAndReleaseHolds, Frame(gesture, down: false, released: true, onInspector: false));
        Assert.False(gesture.Owned);
        // The next world click is the game's again.
        Assert.Equal(NativeMouseAction.Allow, Frame(gesture, down: true, pressed: true, onInspector: false));
    }

    [Fact]
    public void GesturesThatStartOutsideTheInspectorStayNativeThroughTheirRelease()
    {
        var gesture = new InspectorPointerGesture();
        // The cursor moves out of the popup and presses on the world in the same frame: the game handles it.
        Assert.Equal(NativeMouseAction.Allow, Frame(gesture, down: true, pressed: true, onInspector: false));
        // Hold-walk: the cursor passes over the popup and a walker; the release there is still seen natively,
        // so MouseController clears LeftButtonHeld and the player stops.
        Assert.Equal(NativeMouseAction.Allow, Frame(gesture, down: true, onInspector: true));
        Assert.Equal(NativeMouseAction.Allow, Frame(gesture, down: false, released: true, onInspector: true));
        Assert.False(gesture.Owned);
        // A second button pressed over the inspector during a native hold-walk is not taken over.
        Assert.Equal(NativeMouseAction.Allow, Frame(gesture, down: true, pressed: true, nativeHold: true, onInspector: true));
        Assert.False(gesture.Owned);
    }

    [Fact]
    public void APressAndReleaseInOneFrameEndsTheGestureAtOnce()
    {
        var gesture = new InspectorPointerGesture();
        Assert.Equal(NativeMouseAction.Suppress, Frame(gesture, down: false, pressed: true, released: true, onInspector: true));
        Assert.False(gesture.Owned);
        Assert.Equal(NativeMouseAction.Allow, Frame(gesture, down: false));
        // A lost release (focus change) cannot leave the gesture owned: no button down ends it.
        Assert.Equal(NativeMouseAction.Suppress, Frame(gesture, down: true, pressed: true, onInspector: true));
        Assert.Equal(NativeMouseAction.SuppressAndReleaseHolds, Frame(gesture, down: false));
        Assert.False(gesture.Owned);
        gesture.Reset();
        Assert.False(gesture.Owned);
    }

    [Fact]
    public void PickChoosesTheNearestWalkerIndependentOfOrder()
    {
        Assert.False(ResidentPickAccumulator.Hit(100, 50, 100, 200, 100, 100, 8, out _));   // above the head
        Assert.False(ResidentPickAccumulator.Hit(140, 150, 100, 200, 100, 100, 8, out _));  // beside the body
        Assert.False(ResidentPickAccumulator.Hit(100, 150, 100, 100, 100, 200, 8, out _));  // inverted projection
        Assert.True(ResidentPickAccumulator.Hit(105, 150, 100, 200, 100, 100, 8, out float centred));
        Assert.True(ResidentPickAccumulator.Hit(125, 150, 100, 200, 100, 100, 8, out float edge));
        Assert.True(centred < edge);
        var candidates = new (string id, long generation, float score, float depth)[]
            { ("pc:3", 1, .4f, 10), ("pc:2", 4, .2f, 30), ("pc:9", 2, .2f, 20), ("pc:1", 3, .2f, 20) };
        foreach (var order in new[] { new[] { 0, 1, 2, 3 }, new[] { 3, 2, 1, 0 }, new[] { 2, 0, 3, 1 } })
        {
            var best = new ResidentPickAccumulator();
            foreach (int i in order) best.Consider(candidates[i].id, candidates[i].generation, candidates[i].score, candidates[i].depth);
            Assert.Equal(("pc:1", 3L), (best.Id, best.Generation)); // best score, nearer body, then ordinal ID
        }
    }

    [Fact]
    public void SteadyPickingPathDoesNotAllocate()
    {
        var throttle = new ResidentPickThrottle();
        var ids = Enumerable.Range(0, 48).Select(i => "pc:" + i).ToArray();
        void Frame(int frame)
        {
            // Closed popup, cursor still: the throttle alone decides, nothing else runs.
            throttle.ShouldPick(frame * 0.016, 300, 200);
            // Worst case per pick: 48 walkers (the default pedestrian ceiling).
            var best = new ResidentPickAccumulator();
            for (int i = 0; i < ids.Length; i++)
                if (ResidentPickAccumulator.Hit(300, 200, 280 + i, 260, 281 + i, 140, 8, out float score)) best.Consider(ids[i], i, score, 10 + i);
        }
        for (int i = 0; i < 1000; i++) Frame(i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1000; i < 101000; i++) Frame(i);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void CaptureAndPickingStayWithinTheirOfflineBudget()
    {
        var (city, person, places) = Fixture();
        var meal = Meal(MealKind.Lunch, MealStatus.Travelling);
        person.Phase = CitizenPhase.Travelling; person.DestinationId = ShopId; person.DestinationActivity = Activity.Shop;
        person.MealOuting = new MealOuting { OriginId = WorkId, OriginActivity = Activity.Work };
        Func<string, Place?> find = id => places.TryGetValue(id, out var p) ? p : null;
        for (int i = 0; i < 2000; i++) ResidentInsight.Capture(city, person, meal, find).Text();
        const int captures = 20000;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < captures; i++) ResidentInsight.Capture(city, person, meal, find).Text();
        double captureMicroseconds = watch.Elapsed.TotalMilliseconds * 1000 / captures;

        var ids = Enumerable.Range(0, 48).Select(i => "pc:" + i).ToArray();
        const int picks = 200000;
        watch.Restart();
        for (int n = 0; n < picks; n++)
        {
            var best = new ResidentPickAccumulator();
            for (int i = 0; i < ids.Length; i++)
                if (ResidentPickAccumulator.Hit(300, 200, 280 + i, 260, 281 + i, 140, 8, out float score)) best.Consider(ids[i], i, score, 10 + i);
        }
        double pickMicroseconds = watch.Elapsed.TotalMilliseconds * 1000 / picks;
        output.WriteLine("ResidentInsight.Capture+Text: " + captureMicroseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " us; 48-walker pick scoring: " + pickMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " us");
        // Generous offline ceilings against the 0.20 ms event-frame sub-budget; native cost is measured on the rig.
        Assert.True(captureMicroseconds < 200, "capture " + captureMicroseconds + " us");
        Assert.True(pickMicroseconds < 50, "pick " + pickMicroseconds + " us");
    }

    private sealed class Walking : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => new()
        { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = 4, Mode = TravelMode.Walk } } };
    }
    private sealed class Market : IFoodMarket
    {
        public double EarliestMinute => 0;
        public int Units = 400;
        public readonly List<string> Orders = new();
        public IReadOnlyList<FoodBusiness> Read(double minute) => new[] { new FoodBusiness { Id = "b", PlaceId = ShopId, Name = "Corner Bakery",
            MealsPerHour = 30, Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(300), Units = Units },
                new FoodOffer { ProductId = FoodProducts.Burger, Price = Money.FromCents(600), Units = Units } } } };
        public FoodSale Buy(ResidentMeal meal, double at) { Orders.Add(meal.IntentId); Units--; return new(true, "Fake confirmed"); }
    }
    private static (CitySimulator city, Market market) City()
    {
        var personas = new[] { Persona.Commuter, Persona.Foodie, Persona.Parent, Persona.Student, Persona.Retiree, Persona.Sporty, Persona.Grumpy, Persona.NightOwl };
        var state = Population.Create(Enumerable.Range(0, 24).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Persona = personas[n % personas.Length], Age = 30 + n }).ToArray(),
            Places().Values.Select(p => new Place { Id = p.Id, Name = p.Name, Kind = p.Kind, Position = p.Position,
                Capacity = p.Kind == PlaceKind.Shop ? 1 : 24 }).ToArray(), "inspector", "0.40.4", 11, 1440);
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion,
            Shops = new() { new FoodScope { PlaceId = ShopId, BusinessId = "b", ProductId = FoodProducts.Croissant, AdditionalProductIds = new() { FoodProducts.Burger } } } };
        var market = new Market(); var city = new CitySimulator(state, new Walking()); city.AttachFoodMarket(market);
        return (city, market);
    }

    [Fact]
    public void InspectingChangesNoSimulationEconomyOrSaveState()
    {
        var (plain, plainMarket) = City();
        var (inspected, inspectedMarket) = City();
        var texts = new HashSet<string>();
        for (double minute = 1440; minute <= 1440 * 3; minute += 7)
        {
            while (!plain.AdvanceTo(minute, 64).CaughtUp) { }
            foreach (var person in inspected.State.Citizens.ToArray())
            {
                // Inspect before and after each slice, with a product-name callback, as the popup refresh would.
                var view = inspected.Inspect(person.Identity.Id, id => id.ToUpperInvariant());
                Assert.NotNull(view); texts.Add(view!.Heading + "|" + view.HeadingReason + "|" + string.Join(";", view.Meals.Select(m => m.Text)));
            }
            while (!inspected.AdvanceTo(minute, 64).CaughtUp)
                inspected.Inspect(inspected.State.Citizens[0].Identity.Id);
            if ((int)minute % 140 == 0) Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(inspected.State));
        }
        Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(inspected.State));
        Assert.Equal(plainMarket.Orders, inspectedMarket.Orders);
        Assert.NotEmpty(plainMarket.Orders);
        Assert.Null(inspected.Inspect("not-a-resident"));
        // The fixture exercised real states, not just the idle case.
        Assert.Contains(texts, t => t.StartsWith("Heading to", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("bought", StringComparison.Ordinal));
        output.WriteLine(texts.Count + " distinct inspector views; " + plainMarket.Orders.Count + " orders");
    }
}
