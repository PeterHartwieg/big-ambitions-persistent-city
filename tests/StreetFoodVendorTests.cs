using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>E6b: the Street Food bridge against a contract-shaped stand-in, and the Core vendor rules (E6 plan 3.2-3.6,
/// section 9 defaults). The stand-in is static, so these tests share one collection and reset it.</summary>
[Collection(StreetFoodHooks.Collection)]
public sealed class StreetFoodVendorTests
{
    private const long Day2 = 2 * 1440;
    private static long At(int hour, int minute = 0) => Day2 + hour * 60 + minute;

    public StreetFoodVendorTests() => StreetFoodHooks.Reset();

    internal sealed class Sampler : IVendorStandSampler
    {
        public int Calls;
        public bool Blocked;
        public bool TrySample(Position stand, double radius, out Position point)
        {
            Calls++;
            point = new Position(stand.X, 0, stand.Z);
            return !Blocked && stand.X < 9000; // far stands are off the walkable mesh
        }
    }

    internal sealed class Rig
    {
        public readonly CitySimulator City;
        public readonly VendorTable Table = new VendorTable();
        public readonly VendorCoordinator Vendors;
        public readonly Sampler Stands = new Sampler();
        public Rig(IVendorApi? api = null, string reason = "", bool bind = true, int seed = 5, IRoutePlanner? planner = null)
        {
            City = WeightedFoodChoiceTests.Ready(seed).city;
            if (planner != null) City = City.RebuildRoutes(planner);
            if (bind && api == null) api = StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi));
            Vendors = new VendorCoordinator(Table, api, reason, City, Stands);
        }
        public long Now { get; private set; }
        public void Run(long minute)
        {
            Now = minute; StreetFoodHooks.Now = minute; StreetFoodHooks.Expire();
            Vendors.Poll(minute);
            while (Vendors.Continue(minute)) { }
            Table.CheckInvariant();
        }
        public void RunTo(long to, int step = 1) { for (long m = Now + step; m <= to; m += step) Run(m); if (Now < to) Run(to); }
        public VendorRow Row(string id) => Table.Find(id) ?? throw new InvalidOperationException("no row " + id);
        public Place Place(string id) => City.FindPlace(Row(id).PlaceId);
        public bool Serves(string id) => Vendors.CanServe(Row(id).BusinessId, Now);
        public ResidentMeal Commit(string id, MealStatus status = MealStatus.Travelling)
        {
            var meal = new ResidentMeal { ResidentId = "committed" + City.State.Food!.Residents.Count, PlaceId = Row(id).PlaceId, BusinessId = Row(id).BusinessId, Status = status };
            meal.AnchorId = meal.ResidentId;
            City.State.Food.Residents.Add(meal);
            return meal;
        }
        public string Shops() => JsonConvert.SerializeObject(City.State.Food!.Shops.Where(s => s.Channel == FoodScopeChannel.Shop));
    }

    // ---- Bridge: bind, validate, copy ----

    [Fact]
    public void BridgeBindsTheContractAndCopiesADetachedValidatedImage()
    {
        var bridge = StreetFoodBridge.Open(typeof(FoodTrucks.ModApi.VendorApi).Assembly, out string reason);
        Assert.NotNull(bridge); Assert.Equal("", reason);
        Assert.Equal(1, bridge!.ContractVersion);
        StreetFoodHooks.Add("A", "cart", 100, FoodProducts.HotDog);
        StreetFoodHooks.Add("B", "truck", 200, FoodProducts.Burger, FoodProducts.Croissant);
        StreetFoodHooks.Open("B", 90);
        var reader = new VendorImageReader(bridge);
        Assert.True(reader.Poll());
        Assert.True(reader.Continue());
        var image = reader.Image;
        Assert.True(image.Ready, image.Reason);
        Assert.Equal(2, image.Count);
        var b = image.Find("B")!;
        Assert.Equal("truck", b.Kind); Assert.True(b.Open); Assert.Equal(1, b.OpeningId); Assert.Equal("", b.OpeningOwner);
        Assert.Equal(2, b.OpenedDay); Assert.Equal(480, b.OpenedMinute); Assert.Equal(2, b.ClosesDay); Assert.Equal(570, b.ClosesMinute);
        Assert.Equal(200f, b.X); Assert.Equal(201f, b.QueueX); Assert.Equal(90f, b.QueueHeading);
        Assert.Equal(2, b.ItemCount); Assert.Equal(FoodProducts.Croissant, b.Items[1]); Assert.Equal(401, b.PriceCents[1]); Assert.Equal(10, b.Units[1]);
        // Detached: Street Food changing its vehicle does not change the copy.
        StreetFoodHooks.Find("B").Units[1] = 0;
        Assert.Equal(10, image.Find("B")!.Units[1]);
    }

    [Fact]
    public void MissingStreetFoodOrMissingApiIsAReasonNotAFault()
    {
        Assert.Null(StreetFoodBridge.Open(null, out string absent));
        Assert.Contains("Street Food is not loaded", absent);
        Assert.Null(StreetFoodBridge.Open(typeof(object).Assembly, out string missing));
        Assert.Equal("Street Food vendor API missing.", missing);
    }

    [Theory]
    [InlineData(typeof(StreetFoodVariants.Version2.FoodTrucks.ModApi.VendorApi), "unsupported contract 2")]
    [InlineData(typeof(StreetFoodVariants.MissingRenew.FoodTrucks.ModApi.VendorApi), "missing or changed method VendorApi.Renew")]
    [InlineData(typeof(StreetFoodVariants.IntPrices.FoodTrucks.ModApi.VendorApi), "missing or changed field VendorApiVehicle.PriceCents")]
    [InlineData(typeof(StreetFoodVariants.IntRevision.FoodTrucks.ModApi.VendorApi), "missing public static Int64 VendorApi.Revision")]
    [InlineData(typeof(StreetFoodVariants.ShiftedStatus.FoodTrucks.ModApi.VendorApi), "VendorApiStatus.Ready is missing or has another value")]
    public void ContractMismatchRefusesBinding(Type api, string message)
    {
        var error = Assert.Throws<InvalidOperationException>(() => StreetFoodBridge.Bind(api));
        Assert.StartsWith("Street Food VendorApi: ", error.Message);
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void ARunningContractChangeIsRefused()
    {
        var bridge = StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi));
        bridge.CheckContractVersion();
        StreetFoodHooks.ContractVersion = 2;
        Assert.Contains("unsupported contract 2", Assert.Throws<InvalidOperationException>(() => bridge.CheckContractVersion()).Message);
    }

    [Theory]
    [InlineData(1, VendorApiStatus.Off, "switched off")]
    [InlineData(2, VendorApiStatus.NotYet, "not loaded its vehicles")]
    [InlineData(3, VendorApiStatus.Refused, "more than 64 vehicles")]
    [InlineData(4, VendorApiStatus.Failed, "reported a fault")]
    public void StatusesOtherThanReadyGiveAnEmptyImageWithAReason(int status, VendorApiStatus expected, string message)
    {
        StreetFoodHooks.Add("A");
        StreetFoodHooks.ReadStatus = status;
        var rig = new Rig();
        string shops = rig.Shops();
        rig.Run(At(8));
        var image = rig.Vendors.Image!;
        Assert.Equal(expected, image.Status); Assert.Equal(0, image.Count);
        Assert.Contains(message, rig.Vendors.Reason);
        Assert.Empty(rig.Table.Rows); Assert.Equal(shops, rig.Shops());
    }

    [Fact]
    public void SixtyFiveVehiclesAreRefusedWholeAndAThrowingReadIsAFault()
    {
        for (int i = 0; i < 65; i++) StreetFoodHooks.Add("v" + i.ToString("D2"));
        var rig = new Rig();
        rig.Run(At(8));
        Assert.Equal(VendorApiStatus.Refused, rig.Vendors.Image!.Status);
        Assert.Empty(rig.Table.Rows);

        StreetFoodHooks.Vehicles.RemoveAt(64); StreetFoodHooks.ThrowOnRead = true; StreetFoodHooks.Revision++;
        rig.Run(At(8, 1));
        Assert.Equal(VendorApiStatus.Failed, rig.Vendors.Image!.Status);
        Assert.Contains("Read threw, which the contract forbids", rig.Vendors.Reason);
    }

    [Theory]
    [InlineData("empty id", "invalid vehicle id")]
    [InlineData("long id", "invalid vehicle id")]
    [InlineData("control id", "invalid vehicle id")]
    [InlineData("kind", "unknown kind")]
    [InlineData("duplicate", "duplicate vehicle id")]
    [InlineData("price", "price of ba:itemname_hotdog is not positive")]
    [InlineData("units", "negative units")]
    [InlineData("position", "invalid position")]
    [InlineData("null items", "null menu arrays")]
    [InlineData("duplicate item", "duplicate menu item")]
    [InlineData("closes", "invalid closing time")]
    public void AnImageThatBreaksTheContractIsRefusedWhole(string fault, string message)
    {
        var a = StreetFoodHooks.Add("A"); StreetFoodHooks.Add("B");
        switch (fault)
        {
            case "empty id": a.Id = ""; break;
            case "long id": a.Id = new string('x', 129); break;
            case "control id": a.Id = "A\n"; break;
            case "kind": a.Kind = "bike"; break;
            case "duplicate": a.Id = "B"; break;
            case "price": a.Prices[0] = 0; break;
            case "units": a.Units[0] = -1; break;
            case "position": a.X = float.NaN; break;
            case "null items": a.NullItems = true; break;
            case "duplicate item": a.Items = new[] { FoodProducts.HotDog, FoodProducts.HotDog }; a.Prices = new long[] { 1, 1 }; a.Units = new[] { 1, 1 }; break;
            case "closes": StreetFoodHooks.Open("A"); a.ClosesDay = a.OpenedDay; a.ClosesMinute = a.OpenedMinute; break;
        }
        var rig = new Rig();
        string shops = rig.Shops();
        rig.Run(At(8, 5));
        Assert.Equal(VendorApiStatus.Failed, rig.Vendors.Image!.Status);
        Assert.Contains(message, rig.Vendors.Reason);
        Assert.Empty(rig.Table.Rows); Assert.Equal(shops, rig.Shops());
    }

    [Fact]
    public void NamesAreEscapedInjectivelyAndBounded()
    {
        Assert.Equal("vendor:cart-1", VendorNames.PlaceId("cart-1"));
        Assert.Equal("streetfood:vehicle:a%3Ab%25c:generation:3", VendorNames.BusinessId("a:b%c", 3));
        Assert.NotEqual(VendorNames.Escape("a:b"), VendorNames.Escape("a%3Ab"));
        Assert.Equal("%C3%A9%F0%9F%9A%9A", VendorNames.Escape("é🚚"));
        Assert.True(VendorNames.ValidId(new string('x', 128)));
        Assert.False(VendorNames.ValidId(new string('x', 129)));
        Assert.False(VendorNames.ValidId("\uD800"));
        Assert.Throws<ArgumentException>(() => VendorNames.Escape(""));
    }

    [Fact]
    public void UnchangedRevisionReadsNothingAndAllocatesNothing()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8));
        Assert.NotNull(rig.Table.Find("A"));
        int reads = StreetFoodHooks.ReadCalls;
        long minute = At(8, 1);
        for (int i = 0; i < 2000; i++) rig.Vendors.Poll(minute); // the lease slot was renewed at 08:00; nothing is due at 08:01; warm-up past tiered JIT
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) rig.Vendors.Poll(minute);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(reads, StreetFoodHooks.ReadCalls);
        Assert.False(rig.Vendors.Continue(minute));
    }

    // ---- Missing or unsupported API ----

    [Fact]
    public void MissingOrUnsupportedApiRefusesVehicleScopesAndLeavesShopsUntouched()
    {
        string unsupported = "";
        try { StreetFoodBridge.Bind(typeof(StreetFoodVariants.Version2.FoodTrucks.ModApi.VendorApi)); }
        catch (InvalidOperationException error) { unsupported = error.Message; }
        foreach (var reason in new[] { StreetFoodBridge.MissingReason, unsupported })
        {
            StreetFoodHooks.Reset(); StreetFoodHooks.Add("A"); StreetFoodHooks.Open("A");
            var rig = new Rig(reason: reason, bind: false);
            string shops = rig.Shops(); int scopes = rig.City.State.Food!.Shops.Count;
            rig.Run(At(9)); rig.RunTo(At(12), 5);
            Assert.Equal(reason, rig.Vendors.Reason);
            Assert.Empty(rig.Table.Rows);
            Assert.False(rig.Vendors.CanServe(VendorNames.BusinessId("A", 1), At(12)));
            Assert.Equal(scopes, rig.City.State.Food.Shops.Count); Assert.Equal(shops, rig.Shops());
            Assert.Empty(StreetFoodHooks.Calls);
        }
        Assert.Contains("unsupported contract 2", unsupported);
    }

    // ---- Ownership: request, acquire at a later opening, serve ----

    [Fact]
    public void CrewOpeningInALaterHourIsAcquiredPublishedAndServed()
    {
        StreetFoodHooks.Add("A", "cart", 100);
        var rig = new Rig();
        rig.Run(At(8, 5));
        var a = rig.Row("A");
        Assert.True(a.Authority.Desired); Assert.Equal(Day2 / 60 + 8, a.Authority.RequestedHour);
        Assert.Contains("request A", StreetFoodHooks.Calls); Assert.Contains("renew", StreetFoodHooks.Calls);

        // An opening in the request hour stays Street Food's; the request stays recorded.
        rig.RunTo(At(8, 30)); StreetFoodHooks.Open("A", 30); rig.Run(At(8, 30));
        Assert.False(rig.Serves("A")); Assert.False(a.PlacePublished); Assert.Equal(-1, a.Authority.FromHour);
        rig.RunTo(At(9)); StreetFoodHooks.Close("A"); rig.Run(At(9));

        // The next opening, at 09:30, is PC's.
        rig.RunTo(At(9, 30)); StreetFoodHooks.Open("A", 120); rig.Run(At(9, 30));
        Assert.Equal(Day2 / 60 + 9, a.Authority.FromHour); Assert.Equal(2, a.Authority.OpeningId);
        Assert.True(rig.Serves("A"));
        var place = rig.Place("A");
        Assert.Equal(PlaceKind.Shop, place.Kind); Assert.Equal(100, place.Position.X); Assert.Equal(0, place.Position.Y);
        var window = Assert.Single(place.WeeklyHours);
        Assert.Equal((VendorRules.Weekday(2), 570, 690), (window.Day, window.FromMinute, window.ToMinute));
        Assert.DoesNotContain(rig.City.State.Places, p => p.Id == a.PlaceId); // never a saved city place
        var scope = rig.City.State.Food!.Shops.Single(s => s.PlaceId == a.PlaceId);
        Assert.Equal(FoodScopeChannel.Vehicle, scope.Channel); Assert.Equal(a.BusinessId, scope.BusinessId);
        Assert.Equal(FoodProducts.HotDog, scope.ProductId); Assert.Equal(FoodAlternativeKind.Player, scope.AlternativeKind);
        Assert.All(rig.City.State.Food.Shops.Where(s => s.PlaceId != a.PlaceId), s => Assert.Equal(FoodScopeChannel.Shop, s.Channel));

        // Closing ends the window; residents arriving later find it closed.
        rig.RunTo(At(11, 30)); StreetFoodHooks.Close("A"); rig.Run(At(11, 30));
        Assert.False(rig.Serves("A")); Assert.Empty(rig.Place("A").WeeklyHours);
        Assert.True(a.Authority.Managed); // ownership continues for the next opening
    }

    [Fact]
    public void AGrantThatBreaksTheLaterHourRuleIsHandedBackAtOnce()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5));
        StreetFoodHooks.Open("A"); StreetFoodHooks.Find("A").Owner = VendorNames.Owner; // a faulty Street Food grant at 08:05
        rig.Run(At(8, 6));
        var a = rig.Row("A");
        Assert.False(rig.Serves("A")); Assert.False(a.PlacePublished);
        Assert.Contains("request hour", a.Reason);
        Assert.Contains("release A", StreetFoodHooks.Calls);
        Assert.Equal("", StreetFoodHooks.Find("A").Owner); // Street Food resumes its own customers
        Assert.True(a.Authority.Desired); Assert.True(a.Authority.Requested); // and the next opening is requested again
    }

    [Theory]
    [InlineData(false, false, "player-opened window stays with Street Food")]
    [InlineData(true, true, "city event or catering opening stays with Street Food")]
    public void SectionNineDefaultsKeepPlayerAndEventOpeningsWithStreetFood(bool crew, bool cityEvent, string reason)
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5));
        rig.RunTo(At(9, 10)); StreetFoodHooks.Open("A", 60, crew, cityEvent); rig.Run(At(9, 10));
        var a = rig.Row("A");
        Assert.False(rig.Serves("A")); Assert.False(a.PlacePublished); Assert.Equal(reason, a.Reason);
        Assert.Equal("", StreetFoodHooks.Find("A").Owner); // Street Food keeps its own queue for this opening
        rig.RunTo(At(10, 10)); StreetFoodHooks.Close("A"); rig.Run(At(10, 10));

        // The crew opening afterwards gets residents.
        rig.RunTo(At(11, 5)); StreetFoodHooks.Open("A", 60); rig.Run(At(11, 5));
        Assert.True(rig.Serves("A")); Assert.True(a.PlacePublished);
    }

    [Fact]
    public void WholeVehicleRefusalKeepsTheVehicleWithStreetFoodWithItsReason()
    {
        StreetFoodHooks.Add("A");
        StreetFoodHooks.Add("B", "truck", 200, FoodProducts.Burger, "ba:itemname_kabob");
        var nine = StreetFoodHooks.Add("C", "truck", 300, Enumerable.Range(0, 9).Select(i => "ba:itemname_item" + i).ToArray());
        var rig = new Rig();
        rig.Run(At(8, 5));
        Assert.NotNull(rig.Table.Find("A")); Assert.Null(rig.Table.Find("B")); Assert.Null(rig.Table.Find("C"));
        var refusals = rig.Vendors.Refusals.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("menu item not executable: ba:itemname_kabob", refusals["B"]);
        Assert.Equal("menu has more than 8 items", refusals["C"]);
        Assert.True(rig.Vendors.Image!.Find("C")!.MenuOverflow);
        Assert.DoesNotContain("request B", StreetFoodHooks.Calls); Assert.DoesNotContain("request C", StreetFoodHooks.Calls);
        Assert.DoesNotContain(rig.City.State.Food!.Shops, s => s.Channel == FoodScopeChannel.Vehicle); // none before an accepted opening
    }

    [Fact]
    public void AStandWithNoWalkablePointIsRefusedAndTheOpeningStaysWithStreetFood()
    {
        StreetFoodHooks.Add("A", "cart", 9500);
        var rig = new Rig();
        rig.Run(At(8, 5));
        rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A"); rig.Run(At(9, 5));
        var a = rig.Row("A");
        Assert.Equal("no walkable point within 2 m of the stand", a.Reason);
        Assert.False(a.PlacePublished); Assert.False(rig.Serves("A"));
        Assert.Equal("", StreetFoodHooks.Find("A").Owner);
        Assert.Equal(1, rig.Stands.Calls);
    }

    // ---- Stands ----

    [Theory]
    [InlineData(MealStatus.Travelling)]
    [InlineData(MealStatus.Serving)]
    [InlineData(MealStatus.Pending)]
    public void AStandMoveWaitsForCommittedResidentsAndAppliesAfter(MealStatus committed)
    {
        StreetFoodHooks.Add("A", "cart", 100);
        var rig = new Rig();
        rig.Run(At(8, 5));
        rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 60); rig.Run(At(9, 5));
        Assert.True(rig.Serves("A"));
        var meal = rig.Commit("A", committed);

        // Stop 1 closes, stop 2 opens 400 m away while a resident still walks to stop 1.
        rig.RunTo(At(10, 5)); StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A", 60, x: 500); rig.Run(At(10, 5));
        var a = rig.Row("A");
        Assert.NotNull(a.DeferredStand);
        Assert.Equal(100, rig.Place("A").Position.X); Assert.Empty(rig.Place("A").WeeklyHours); // old position, closed
        Assert.False(rig.Serves("A"));
        Assert.Equal("stand move waits for committed residents", a.Reason);

        rig.RunTo(At(10, 30), 5);
        Assert.NotNull(a.DeferredStand);
        meal.Status = MealStatus.Declined; // arrived, found it closed, kept their money
        rig.RunTo(At(10, 40), 5);
        Assert.Null(a.DeferredStand);
        Assert.Equal(500, rig.Place("A").Position.X); Assert.Single(rig.Place("A").WeeklyHours);
        Assert.True(rig.Serves("A"));
    }

    [Fact]
    public void AReopeningAtTheSameSpotIsNotAMove()
    {
        StreetFoodHooks.Add("A", "cart", 100);
        var rig = new Rig();
        rig.Run(At(8, 5));
        rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 30); rig.Run(At(9, 5));
        rig.Commit("A");
        rig.RunTo(At(10, 5)); StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A", 30); rig.Run(At(10, 5));
        Assert.Null(rig.Row("A").DeferredStand); Assert.True(rig.Serves("A"));
        Assert.Equal(2, rig.Row("A").Stand!.OpeningId);
    }

    [Fact]
    public void AStandChangeInvalidatesTheAwarenessImage()
    {
        StreetFoodHooks.Add("A", "cart", 3, FoodProducts.Croissant);
        var rig = new Rig();
        rig.City.InspectFoodAwareness("r");
        Assert.True(rig.City.FoodAwarenessCurrent);
        rig.Run(At(8, 5));
        rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 60); rig.Run(At(9, 5));
        Assert.False(rig.City.FoodAwarenessCurrent);
        rig.City.InspectFoodAwareness("r"); // bounded preparation finishes the rebuild
        Assert.True(rig.City.FoodAwarenessCurrent);
        rig.RunTo(At(10, 5)); StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A", 60, x: 50); rig.Run(At(10, 5));
        Assert.False(rig.City.FoodAwarenessCurrent);
        Assert.Equal(50, rig.City.FindPosition(rig.Row("A").PlaceId)!.Value.X);
    }

    [Fact]
    public void ARouteRebuildKeepsVendorPlaces()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A"); rig.Run(At(9, 5));
        var rebuilt = rig.City.RebuildRoutes(new WeightedFoodChoiceTests.Planner());
        Assert.Equal(rig.City.FindPosition(rig.Row("A").PlaceId), rebuilt.FindPosition(rig.Row("A").PlaceId));
    }

    // ---- Release, lease, handback ----

    [Fact]
    public void ReleaseWaitsUntilPcWorkHasDrained()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 180); rig.Run(At(9, 5));
        var meal = rig.Commit("A", MealStatus.Serving);

        // The menu changes to something PC cannot execute: no new captures at once, release only after the drain.
        var v = StreetFoodHooks.Find("A"); v.Items = new[] { "ba:itemname_kabob" }; StreetFoodHooks.Revision++;
        rig.Run(At(9, 20));
        var a = rig.Row("A");
        Assert.False(a.Authority.Desired); Assert.False(rig.Serves("A"));
        Assert.DoesNotContain("release A", StreetFoodHooks.Calls);
        Assert.Equal(VendorNames.Owner, v.Owner);
        rig.RunTo(At(9, 40), 5);
        Assert.DoesNotContain("release A", StreetFoodHooks.Calls);

        meal.Status = MealStatus.Paid;
        rig.RunTo(At(9, 50), 5);
        Assert.Contains("release A", StreetFoodHooks.Calls);
        Assert.Equal("", v.Owner); Assert.Equal(Day2 / 60 + 9, a.Authority.UntilHour); Assert.False(a.Authority.Managed);
    }

    [Fact]
    public void TheLeaseIsRenewedEveryFifteenMinutesAndAStoppedPcGetsTheVehicleTakenBack()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 360); rig.Run(At(9, 5));
        StreetFoodHooks.Calls.Clear();
        rig.RunTo(At(12, 5));
        Assert.Equal(12, StreetFoodHooks.Calls.Count(c => c == "renew"));
        Assert.True(rig.Serves("A")); Assert.True(rig.Vendors.Lease.Valid(At(12, 5)));

        // PC stops advancing for 70 minutes: the lease runs out and Street Food takes the opening back by itself.
        StreetFoodHooks.Now = At(13, 15); StreetFoodHooks.Expire();
        Assert.Equal("", StreetFoodHooks.Find("A").Owner);
        rig.Run(At(13, 15));
        var a = rig.Row("A");
        Assert.False(rig.Serves("A")); Assert.Equal(-1, a.Authority.OpeningId); Assert.Empty(rig.Place("A").WeeklyHours);
        Assert.Equal(Day2 / 60 + 13, a.Authority.RequestedHour); // asked again after the lapse
        rig.RunTo(At(14, 10)); StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A", 60); rig.Run(At(14, 10));
        Assert.True(rig.Serves("A"));
    }

    [Fact]
    public void ReleaseAllHandsEverythingBackAndStops()
    {
        StreetFoodHooks.Add("A"); StreetFoodHooks.Add("B", x: 300);
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A"); StreetFoodHooks.Open("B"); rig.Run(At(9, 5));
        Assert.True(rig.Serves("A")); Assert.True(rig.Serves("B"));
        rig.Vendors.ReleaseAll(At(9, 30), "city refused");
        Assert.Contains("release all", StreetFoodHooks.Calls);
        Assert.All(StreetFoodHooks.Vehicles, v => Assert.Equal("", v.Owner));
        Assert.False(rig.Serves("A")); Assert.False(rig.Serves("B"));
        Assert.All(rig.Table.Rows, r => Assert.Equal(Day2 / 60 + 9, r.Authority.UntilHour));
        Assert.False(rig.Vendors.Poll(At(9, 31)));
        Assert.Equal("city refused", rig.Vendors.Reason);
    }

    // ---- Retirement and capacity ----

    [Fact]
    public void AMissingVehicleRetiresThenCompactsToATombstoneOrIsRemovedAndReturnsAsTheNextGeneration()
    {
        StreetFoodHooks.Add("A"); StreetFoodHooks.Add("B", x: 300);
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A"); StreetFoodHooks.Open("B"); rig.Run(At(9, 5));
        var a = rig.Row("A"); var b = rig.Row("B");
        var food = rig.City.State.Food!;
        var meal = rig.Commit("A", MealStatus.Uncertain);
        food.BusinessReferences.Add(new BusinessReference { Slot = 0, BusinessId = a.BusinessId, PlaceId = a.PlaceId });
        food.History.Add(new FoodShopDay { Day = 2, PlaceId = b.PlaceId, BusinessId = b.BusinessId });

        StreetFoodHooks.Remove("A"); StreetFoodHooks.Remove("B"); rig.Run(At(9, 30));
        Assert.Equal(VendorRowState.Retired, a.State); Assert.Equal(VendorRowState.Retired, b.State);
        Assert.Equal((0, 2, 0), (rig.Table.Live, rig.Table.Retired, rig.Table.Tombstones));
        Assert.Empty(rig.Place("A").WeeklyHours);

        // An uncertain receipt and a retained history row keep the rows.
        Assert.Equal(0, rig.Vendors.Compact());
        meal.Status = MealStatus.Paid;
        Assert.Equal(0, rig.Vendors.Compact(row => row == a)); // pending receipt
        Assert.Equal(1, rig.Vendors.Compact());                 // A: remembered -> tombstone
        Assert.Equal(VendorRowState.Tombstone, a.State);
        Assert.Null(rig.City.FindPosition(a.PlaceId)); Assert.DoesNotContain(food.Shops, s => s.PlaceId == a.PlaceId);
        food.History.Clear();
        Assert.Equal(1, rig.Vendors.Compact());                 // B: nothing names it -> removed
        Assert.Null(rig.Table.Find("B")); Assert.DoesNotContain(food.Shops, s => s.PlaceId == b.PlaceId);
        Assert.Equal((0, 0, 1), (rig.Table.Live, rig.Table.Retired, rig.Table.Tombstones));

        // A returns: same place id, next generation; the old memory reference stays valid.
        StreetFoodHooks.Add("A"); rig.Run(At(9, 40));
        Assert.Equal(VendorRowState.Live, a.State); Assert.Equal(2, a.Generation);
        Assert.Equal("streetfood:vehicle:A:generation:2", a.BusinessId); Assert.Equal("vendor:A", a.PlaceId);
        rig.RunTo(At(10, 5)); StreetFoodHooks.Open("A"); rig.Run(At(10, 5));
        Assert.True(rig.Serves("A"));
        Assert.Equal(a.BusinessId, food.Shops.Single(s => s.PlaceId == a.PlaceId).BusinessId);
    }

    [Fact]
    public void TheCapacityInvariantHoldsWhenEveryLiveVehicleDisappearsAtOnce()
    {
        for (int i = 0; i < 64; i++) StreetFoodHooks.Add("a" + i.ToString("D2"), x: i * 10);
        var rig = new Rig();
        rig.Run(At(8, 5));
        Assert.Equal(64, rig.Table.Live);
        Assert.Equal("vendor table full: 64 live vehicles", rig.Table.AdmissionRefusal(null));

        StreetFoodHooks.Vehicles.Clear(); StreetFoodHooks.Revision++;
        for (int i = 0; i < 64; i++) StreetFoodHooks.Add("b" + i.ToString("D2"), x: i * 10);
        rig.Run(At(8, 10));
        Assert.Equal((64, 64), (rig.Table.Live, rig.Table.Retired));

        // One more vehicle sold and one bought: live + retired stays 128 and the newcomer is refused.
        StreetFoodHooks.Remove("b00"); StreetFoodHooks.Add("c00"); rig.Run(At(8, 15));
        Assert.Equal((63, 65), (rig.Table.Live, rig.Table.Retired));
        Assert.Null(rig.Table.Find("c00"));
        Assert.Equal("vendor table full: 128 live and retired vehicles", rig.Vendors.Refusals.Single(p => p.Key == "c00").Value);

        // A retired vehicle that returns keeps live + retired the same and is admitted.
        StreetFoodHooks.Remove("c00"); StreetFoodHooks.Add("b00"); rig.Run(At(8, 20));
        Assert.Equal((64, 64), (rig.Table.Live, rig.Table.Retired));
        Assert.Equal(2, rig.Row("b00").Generation);

        // Retired rows nothing names compact away.
        Assert.Equal(64, rig.Vendors.Compact());
        Assert.Equal((64, 0, 0), (rig.Table.Live, rig.Table.Retired, rig.Table.Tombstones));
    }

    [Fact]
    public void RowsAreNotRetiredWhileStreetFoodIsAbsentOrOff()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A"); rig.Run(At(9, 5));
        StreetFoodHooks.ReadStatus = 1; StreetFoodHooks.Revision++; rig.Run(At(9, 10));
        Assert.Equal(VendorRowState.Live, rig.Row("A").State);
        Assert.False(rig.Serves("A")); Assert.Empty(rig.Place("A").WeeklyHours);
        StreetFoodHooks.ReadStatus = 0; StreetFoodHooks.Revision++; rig.Run(At(9, 15));
        Assert.True(rig.Serves("A")); Assert.Single(rig.Place("A").WeeklyHours);
    }

    [Fact]
    public void AnotherKindUnderTheSameIdBecomesTheNextGenerationAfterRelease()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A"); rig.Run(At(9, 5));
        StreetFoodHooks.Find("A").Kind = "truck"; StreetFoodHooks.Revision++;
        rig.Run(At(9, 10));
        var a = rig.Row("A");
        Assert.False(rig.Serves("A")); Assert.Contains("release A", StreetFoodHooks.Calls);
        rig.RunTo(At(9, 20), 5);
        Assert.Equal(2, a.Generation); Assert.Equal("truck", a.Kind);
        Assert.Equal(a.BusinessId, rig.City.State.Food!.Shops.Single(s => s.PlaceId == a.PlaceId).BusinessId);
    }

    // ---- Channel and cadence ----

    [Fact]
    public void TheChannelIsPartOfTheVendorSaveShapeOnly()
    {
        var scope = new FoodScope { PlaceId = "s", BusinessId = "b", ProductId = FoodProducts.Croissant };
        Assert.Equal(FoodScopeChannel.Shop, scope.Channel);
        scope.UseChannel(FoodScopeChannel.Vehicle);
        Assert.Equal(FoodScopeChannel.Vehicle, scope.Channel);
        Assert.Throws<ArgumentOutOfRangeException>(() => scope.UseChannel((FoodScopeChannel)2));
        var city = WeightedFoodChoiceTests.Ready(5).city;
        // E6c: the vendor writer saves the channel; every existing scope writes Shop. The frozen writer 29 has no channel.
        string payload = SaveCodec.Write(city.State);
        Assert.Contains("\"Channel\":0,", payload);
        Assert.DoesNotContain("\"Channel\":1", payload);
        Assert.DoesNotContain("Channel", DevelopmentChain.Write29(city.State));
    }

    [Fact]
    public void AMarketThatCannotTellChannelsNeverShortlistsVehicleScopes()
    {
        // 32 vehicle scopes nearer than the only shop would take most of the eight weighted shortlist slots if offered.
        for (int seed = 0; seed < 6; seed++)
        {
            var (city, _, _) = WeightedFoodChoiceTests.Ready(seed, shops: 1);
            city.State.Food!.AwarenessStartsDay = 99; // the pre-awareness shortlist ranks every scope
            var table = new VendorTable();
            for (int i = 0; i < 32; i++)
            {
                var row = table.Admit("near" + i.ToString("D2"), "cart");
                row.Stand = new VendorStand { OpeningId = 1, Position = new Position(1 + i * 0.1, 0, 0), OpenedDay = 1, OpenedMinute = 0, ClosesDay = 1, ClosesMinute = 1400, Published = true };
                var menu = new VendorVehicle { ItemCount = 1 }; menu.Items[0] = FoodProducts.Croissant;
                city.PublishVendorPlace(row, menu);
            }
            var meal = city.State.Food.Residents.Single();
            while (!city.AdvanceTo(meal.DesiredAt, 1).CaughtUp) { }
            Assert.Equal("b0", meal.BusinessId);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(15)]
    public void ResultsDoNotDependOnTheUpdateCadence(int step)
    {
        string Script(int cadence)
        {
            StreetFoodHooks.Reset();
            StreetFoodHooks.Add("A", x: 100); StreetFoodHooks.Add("B", x: 300);
            var rig = new Rig();
            rig.Run(At(8, 0));
            void Until(long minute) => rig.RunTo(minute, cadence);
            Until(At(9, 5)); StreetFoodHooks.Open("A", 60); StreetFoodHooks.Open("B", 120, crew: false); Until(At(9, 30));
            Until(At(10, 5)); StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A", 60, x: 500); Until(At(10, 30));
            Until(At(11, 30)); StreetFoodHooks.Close("B"); StreetFoodHooks.Open("B", 60); Until(At(12, 0));
            Until(At(12, 15)); StreetFoodHooks.Remove("A"); Until(At(13, 0));
            return string.Join("\n", rig.Table.Rows.Select(r => string.Join("|", r.VehicleId, r.State, r.Generation, r.Authority.FromHour,
                r.Authority.OpeningId, r.Stand?.OpeningId, r.Stand?.Position.X, r.Stand?.Published, r.PlacePublished,
                rig.City.FindPosition(r.PlaceId)?.X, r.Reason)));
        }
        string every = Script(1);
        Assert.Contains("A|Retired|1|", every); Assert.Contains("B|Live|1|", every);
        Assert.Equal(every, Script(step));
    }

    // ---- Review round 1 regressions ----

    private sealed class RecordingPlanner : IRoutePlanner, IFoodWalkingPace
    {
        public readonly List<(string From, Position At)> Requests = new();
        public double WalkingMetresPerMinute => 120;
        public Route? Plan(Place a, Place b, double at, TravelPreferences p)
        {
            Requests.Add((a.Id, a.Position));
            return new Route { Legs = new() { new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Walk, Minutes = 1 } } };
        }
    }

    [Fact]
    public void APhysicalResidentEatingAtTheStandPinsItAndReturnsFromTheStandTheyVisited()
    {
        StreetFoodHooks.Add("A", "cart", 100);
        var planner = new RecordingPlanner();
        var rig = new Rig(planner: planner);
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 60); rig.Run(At(9, 5));
        var a = rig.Row("A");
        // Checkout succeeded: the meal is Paid, but the resident is still at the stand eating, on a meal outing.
        var person = rig.City.State.Citizens.Single();
        var meal = rig.City.State.Food!.Residents.Single(m => m.ResidentId == person.Identity.Id);
        meal.Status = MealStatus.Paid; meal.PlaceId = a.PlaceId; meal.BusinessId = a.BusinessId;
        person.MealOuting = new MealOuting { OriginId = person.HomeId, OriginActivity = Activity.Home, ResumeUntil = person.NextEventMinute + 600, ResumeEvent = person.NextEventMinute + 600 };
        person.PlaceId = a.PlaceId; person.Activity = Activity.Shop; person.Phase = CitizenPhase.AtPlace;
        person.ActivityUntilMinute = person.NextEventMinute;

        rig.RunTo(At(10, 5)); StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A", 60, x: 500); rig.Run(At(10, 5));
        Assert.NotNull(a.DeferredStand);
        Assert.Equal(100, rig.City.FindPosition(a.PlaceId)!.Value.X);

        // The eating interval ends: the return route starts from the stand the resident visited.
        while (!rig.City.AdvanceTo(person.NextEventMinute, 1).CaughtUp) { }
        var back = Assert.Single(planner.Requests, r => r.From == a.PlaceId);
        Assert.Equal(100, back.At.X);
        Assert.NotEqual(a.PlaceId, person.PlaceId);

        // Once the resident has left, the move applies.
        rig.RunTo(At(10, 20), 5);
        Assert.Null(a.DeferredStand);
        Assert.Equal(500, rig.City.FindPosition(a.PlaceId)!.Value.X);
    }

    [Fact]
    public void AnUnconfirmedHandbackIsRetriedAndBlocksNewRequestsWhileAnotherVehicleKeepsTheLease()
    {
        StreetFoodHooks.Add("A"); StreetFoodHooks.Add("B", x: 300);
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 300); StreetFoodHooks.Open("B", 300); rig.Run(At(9, 5));
        Assert.True(rig.Serves("A")); Assert.True(rig.Serves("B"));

        // A turns ineligible and Street Food fails its release; B stays eligible and keeps the shared lease renewed.
        StreetFoodHooks.FailRelease.Add("A");
        var v = StreetFoodHooks.Find("A"); v.Items = new[] { "ba:itemname_kabob" }; StreetFoodHooks.Revision++;
        StreetFoodHooks.Calls.Clear();
        rig.RunTo(At(10, 5));
        var a = rig.Row("A");
        Assert.True(a.Authority.HandbackPending);
        Assert.False(rig.Serves("A")); Assert.True(rig.Serves("B"));
        Assert.Equal(VendorNames.Owner, v.Owner); // Street Food still reserves the opening for PC
        Assert.True(StreetFoodHooks.Calls.Count(c => c == "release A") >= 10); // retried every 5 minutes, no new revision
        Assert.DoesNotContain("request A", StreetFoodHooks.Calls);
        Assert.True(rig.Vendors.Lease.Valid(At(10, 5)));

        // Street Food recovers: the retried handback goes through and the opening is Street Food's again.
        StreetFoodHooks.FailRelease.Clear();
        rig.RunTo(At(10, 15), 5);
        Assert.False(a.Authority.HandbackPending);
        Assert.Equal("", v.Owner);
        Assert.DoesNotContain("request A", StreetFoodHooks.Calls);
    }

    [Fact]
    public void AFailedHandbackOfAFaultyGrantIsNotFollowedByANewRequestUntilConfirmed()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5));
        StreetFoodHooks.FailRelease.Add("A");
        StreetFoodHooks.Open("A"); StreetFoodHooks.Find("A").Owner = VendorNames.Owner; // faulty same-hour grant
        StreetFoodHooks.Calls.Clear();
        rig.Run(At(8, 6));
        var a = rig.Row("A");
        Assert.True(a.Authority.HandbackPending); Assert.False(a.Authority.Requested); Assert.False(rig.Serves("A"));
        Assert.DoesNotContain("request A", StreetFoodHooks.Calls);
        StreetFoodHooks.FailRelease.Clear();
        rig.RunTo(At(8, 15), 5);
        Assert.False(a.Authority.HandbackPending); Assert.True(a.Authority.Requested);
        Assert.Contains("request A", StreetFoodHooks.Calls);
    }

    [Fact]
    public void AFrozenVehicleScopeKeepsItsChannel()
    {
        var scope = new FoodScope { PlaceId = "vendor:A", BusinessId = "streetfood:vehicle:A:generation:1", ProductId = FoodProducts.HotDog };
        scope.UseChannel(FoodScopeChannel.Vehicle);
        Assert.Equal(FoodScopeChannel.Vehicle, FoodCommerce.FreezeScope(scope).Channel);
        Assert.Equal(FoodScopeChannel.Shop, FoodCommerce.FreezeScope(new FoodScope { PlaceId = "s", BusinessId = "b", ProductId = FoodProducts.HotDog }).Channel);
    }

    [Fact]
    public void AReturningRetiredVehicleWaitsForResidentsCommittedToTheOldGeneration()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A"); rig.Run(At(9, 5));
        var meal = rig.Commit("A", MealStatus.Travelling);
        StreetFoodHooks.Remove("A"); rig.Run(At(9, 10));
        var a = rig.Row("A");
        Assert.Equal(VendorRowState.Retired, a.State);

        StreetFoodHooks.Add("A"); rig.Run(At(9, 15));
        Assert.Equal(VendorRowState.Retired, a.State); Assert.Equal(1, a.Generation);
        Assert.Equal("returning vehicle waits for committed residents", rig.Vendors.Refusals.Single(p => p.Key == "A").Value);
        Assert.Equal(meal.BusinessId, rig.City.State.Food!.Shops.Single(s => s.PlaceId == a.PlaceId).BusinessId);

        meal.Status = MealStatus.Declined;
        rig.RunTo(At(9, 25), 5);
        Assert.Equal(VendorRowState.Live, a.State); Assert.Equal(2, a.Generation);
        Assert.Equal(a.BusinessId, rig.City.State.Food.Shops.Single(s => s.PlaceId == a.PlaceId).BusinessId);
    }

    [Fact]
    public void ANotReadyImageIsReadAgainEveryFifteenMinutesWithoutANewRevision()
    {
        StreetFoodHooks.Add("A");
        StreetFoodHooks.ReadStatus = 2;
        var rig = new Rig();
        rig.Run(At(8, 0));
        Assert.Equal(VendorApiStatus.NotYet, rig.Vendors.Image!.Status);
        int reads = StreetFoodHooks.ReadCalls;
        StreetFoodHooks.ReadStatus = 0; // Street Food recovers without changing its revision
        rig.RunTo(At(8, 14));
        Assert.Equal(reads, StreetFoodHooks.ReadCalls);
        Assert.Empty(rig.Table.Rows);
        rig.Run(At(8, 15));
        Assert.Equal(reads + 1, StreetFoodHooks.ReadCalls);
        Assert.True(rig.Vendors.Image!.Ready);
        Assert.NotNull(rig.Table.Find("A"));
    }

    // ---- Review round 2 regressions ----

    [Fact]
    public void AConfirmedRetriedHandbackFollowedByAFailedRequestNeverServesTheOldOpening()
    {
        StreetFoodHooks.Add("A"); StreetFoodHooks.Add("B", x: 300);
        var rig = new Rig();
        rig.Run(At(8, 5));
        // A's crew opening at 09:05 is granted, but its stand has no walkable point (a refusal the snapshot does not
        // carry) and Street Food fails the handback. B keeps the shared lease alive.
        rig.Stands.Blocked = true; StreetFoodHooks.FailRelease.Add("A");
        rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 120); rig.Run(At(9, 5));
        rig.Stands.Blocked = false; StreetFoodHooks.Open("B", 120); rig.Run(At(9, 6));
        var a = rig.Row("A");
        Assert.True(a.Authority.HandbackPending); Assert.Equal(VendorNames.Owner, StreetFoodHooks.Find("A").Owner);

        // The retry succeeds and the next ownership request fails. The retry runs on the snapshot taken before the
        // handback, which still shows A owned by PC: that opening must not be accepted.
        StreetFoodHooks.FailRelease.Clear(); StreetFoodHooks.FailRequest.Add("A");
        rig.Run(At(9, 10));
        Assert.Equal(VendorNames.Owner, rig.Vendors.Image!.Find("A")!.OpeningOwner); // the stale snapshot
        Assert.False(a.Authority.HandbackPending);
        Assert.Equal("", StreetFoodHooks.Find("A").Owner);
        Assert.False(rig.Serves("A")); Assert.Equal(-1, a.Authority.OpeningId); Assert.False(a.PlacePublished);
        rig.RunTo(At(9, 40), 5);
        Assert.False(rig.Serves("A")); Assert.False(a.Authority.Requested); Assert.True(rig.Serves("B"));
    }

    [Fact]
    public void APendingHandbackAloneDoesNotRenewTheLeaseSoStreetFoodTakesTheVehicleBack()
    {
        StreetFoodHooks.Add("A");
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 300); rig.Run(At(9, 5));
        Assert.True(rig.Serves("A"));
        StreetFoodHooks.FailRelease.Add("A"); // Street Food never confirms the release
        var v = StreetFoodHooks.Find("A"); v.Items = new[] { "ba:itemname_kabob" }; StreetFoodHooks.Revision++;
        rig.Run(At(9, 10));
        var a = rig.Row("A");
        Assert.True(a.Authority.HandbackPending);
        StreetFoodHooks.Calls.Clear();
        rig.RunTo(At(10, 30));
        Assert.DoesNotContain("renew", StreetFoodHooks.Calls);
        Assert.Equal("", v.Owner); // the lease ran out and Street Food took the opening back
        Assert.False(a.Authority.HandbackPending);
        Assert.False(rig.Serves("A"));
    }

    [Fact]
    public void AConfirmedRetriedHandbackSendsNoFurtherCallsFromTheStaleSnapshot()
    {
        StreetFoodHooks.Add("A"); StreetFoodHooks.Add("B", x: 300);
        var rig = new Rig();
        rig.Run(At(8, 5)); rig.RunTo(At(9, 5)); StreetFoodHooks.Open("A", 300); StreetFoodHooks.Open("B", 300); rig.Run(At(9, 5));
        StreetFoodHooks.FailRelease.Add("A");
        var v = StreetFoodHooks.Find("A"); v.Items = new[] { "ba:itemname_kabob" }; StreetFoodHooks.Revision++;
        rig.Run(At(9, 10));
        StreetFoodHooks.FailRelease.Clear(); StreetFoodHooks.Calls.Clear();
        rig.Run(At(9, 15)); // the retry, from the snapshot taken before the handback
        Assert.Equal(1, StreetFoodHooks.Calls.Count(c => c == "release A"));
        Assert.False(rig.Row("A").Authority.HandbackPending);
    }
}
