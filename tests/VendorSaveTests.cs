using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class VendorSaveTests
{
    private const long Day2 = 2 * 1440;
    private static long At(int hour, int minute = 0) => Day2 + hour * 60 + minute;

    public VendorSaveTests() => StreetFoodHooks.Reset();

    internal sealed class Rig
    {
        private readonly WeightedFoodChoiceTests.Planner planner;
        private readonly WeightedFoodChoiceTests.Market market;
        public readonly StreetFoodVendorTests.Sampler Stands = new StreetFoodVendorTests.Sampler();
        public CitySimulator City;
        public VendorCoordinator Vendors;
        public long Now { get; private set; }
        public Rig(int seed = 5)
        {
            (City, market, planner) = WeightedFoodChoiceTests.Ready(seed);
            Vendors = Coordinator();
        }
        private VendorCoordinator Coordinator()
            => new VendorCoordinator(City.State.Vendors, StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi)), "", City, Stands);
        public VendorTable Table => City.State.Vendors;
        public FoodCommerceState Food => City.State.Food!;
        public int Day => (int)(City.State.Minute / 1440);
        public void Run(long minute)
        {
            Now = minute; StreetFoodHooks.Now = minute; StreetFoodHooks.Expire();
            Vendors.Poll(minute);
            while (Vendors.Continue(minute)) { }
            Table.CheckInvariant();
        }
        public void RunTo(long to, int step = 5) { for (long m = Now + step; m <= to; m += step) Run(m); if (Now < to) Run(to); }
        public VendorRow Row(string id) => Table.Find(id) ?? throw new InvalidOperationException("no row " + id);

        public string Reload()
        {
            string payload = SaveWire.Text(City.State);
            var read = SaveWire.Read(payload);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(payload, SaveWire.Text(read.State!));
            City = new CitySimulator(read.State!, planner);
            City.AttachFoodMarket(market);
            Vendors = Coordinator();
            return payload;
        }

        public int Remember(VendorRow row)
        {
            int slot = new FoodMemoryStore(Food).RegisterCampaign(row.BusinessId, row.PlaceId);
            Assert.True(slot >= 0);
            var entries = Food.Residents[0].Memory.Entries;
            entries.Add(new ShopMemory { BusinessRef = slot, Day = Day, Served = 1, Affinity = 1, Outcome = ShopExperience.Served });
            entries.Sort((a, b) => a.BusinessRef.CompareTo(b.BusinessRef));
            return slot;
        }

        public FoodShopDay History(VendorRow row)
        {
            var day = new FoodShopDay { Day = Day, PlaceId = row.PlaceId, BusinessId = row.BusinessId };
            Food.History.Add(day);
            return day;
        }

        public void OpenAll(long from, params string[] ids)
        {
            foreach (string id in ids) if (!StreetFoodHooks.Vehicles.Any(v => v.Id == id)) StreetFoodHooks.Add(id, x: 10 + StreetFoodHooks.Vehicles.Count);
            RunTo(from);
            RunTo(from + 60);
            foreach (string id in ids) StreetFoodHooks.Open(id);
            Run(Now + 1);
        }
    }

    private static string Sha(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    private static JObject Wire(string payload) => SaveWire.Parse(payload);
    private static string Text(JObject root) => root.ToString(Formatting.None);
    private static JArray Rows(JObject root) => (JArray)((JArray)root["Vendors"]!)[1];
    private static void Refused(string payload, string message)
    {
        var read = SaveWire.Read(payload);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains(message, read.Error);
    }
    private static void WriteRefused(CityState state, string message)
        => Assert.Contains(message, Assert.Throws<SaveValidationException>(() => SaveWire.Text(state)).Message);

    private static VendorVehicle Menu(params string[] items)
    {
        var v = new VendorVehicle { ItemCount = items.Length };
        for (int i = 0; i < items.Length; i++) { v.Items[i] = items[i]; v.PriceCents[i] = 400 + i; v.Units[i] = 10; }
        return v;
    }

    internal static CitySimulator Table(int live, int retired, int tombstones, bool stands = true, int seed = 5, int idLength = 0)
    {
        var city = WeightedFoodChoiceTests.Ready(seed).city;
        Fill(city, live, retired, tombstones, stands, idLength);
        return city;
    }

    internal static void Fill(CitySimulator city, int live, int retired, int tombstones, bool stands = true, int idLength = 0, bool references = false)
    {
        var table = city.State.Vendors;
        string Id(string prefix, int i) { string id = prefix + i.ToString("D4"); return idLength > 0 ? id.PadRight(idLength, 'x') : id; }
        for (int i = 0; i < tombstones; i++) { var row = table.Admit(Id("t", i), ""); row.Generation = 1 + i % 3; table.Move(row, VendorRowState.Tombstone); }
        void Add(string id, VendorRowState state, int i)
        {
            var row = table.Admit(id, i % 2 == 0 ? "cart" : "truck");
            if (stands)
            {
                row.Stand = new VendorStand { OpeningId = 1 + i, Position = new Position(10.5 + i, 0, -3.25), OpenedDay = 2, OpenedMinute = 540, ClosesDay = 2, ClosesMinute = 660, Published = state == VendorRowState.Live };
                city.PublishVendorPlace(row, Menu(FoodProducts.HotDog, FoodProducts.Burger));
                row.PlacePublished = true;
                row.Authority = state == VendorRowState.Live
                    ? new VendorAuthority { Desired = true, RequestedHour = 56, FromHour = 57, OpeningId = 1 + i }
                    : new VendorAuthority { FromHour = 57, UntilHour = 60 };
            }
            table.Move(row, state);
        }
        for (int i = 0; i < retired; i++) Add(Id("r", i), VendorRowState.Retired, i);
        for (int i = 0; i < live; i++) Add(Id("l", i), VendorRowState.Live, i);
        table.CheckInvariant();
        if (!references) return;
        var food = city.State.Food!;
        int slot = food.BusinessReferences.Count == 0 ? 0 : food.BusinessReferences.Max(r => r.Slot) + 1;
        foreach (var row in table.Rows) food.BusinessReferences.Add(new BusinessReference { Slot = slot++, BusinessId = row.BusinessId, PlaceId = row.PlaceId });
    }

    private static string LiveRow(string id) => "[0,\"" + id + "\",\"cart\",1,\"vendor:" + id + "\",null,null,[false,-1,-1,-1,false,-1,false]]";
    private static string RetiredRow(string id) => "[1,\"" + id + "\",\"cart\",1,\"vendor:" + id + "\",null,null,[false,-1,-1,-1,false,-1,false]]";
    private static string TombstoneRow(string id, int generation = 1) => "[2,\"" + id + "\"," + generation + ",\"vendor:" + id + "\",\"streetfood:vehicle:" + id + ":generation:" + generation + "\"]";

    private static string WithRows(string payload, IEnumerable<string> rows)
    {
        string table = "\"Vendors\":" + Wire(payload)["Vendors"]!.ToString(Formatting.None);
        Assert.Contains(table, payload);
        return payload.Replace(table, "\"Vendors\":[1,[" + string.Join(",", rows) + "]]");
    }


    [Fact]
    public void EveryRowStateRoundTripsFieldForFieldAndRebuildsVendorPlaces()
    {
        var city = Table(3, 2, 4);
        var stand = city.State.Vendors.Find("l0001")!;
        stand.DeferredStand = new VendorStand { OpeningId = 9, Position = new Position(400.125, 1, 2), OpenedDay = 3, OpenedMinute = 600, Published = false };
        stand.Authority.HandbackPending = true; stand.Authority.Suspended = true;
        string payload = SaveWire.Text(city.State);
        var read = SaveWire.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveWire.Text(read.State!));
        var before = city.State.Vendors; var after = read.State!.Vendors;
        Assert.Equal((before.Live, before.Retired, before.Tombstones), (after.Live, after.Retired, after.Tombstones));
        Assert.Equal(JsonConvert.SerializeObject(before.Rows), JsonConvert.SerializeObject(after.Rows));
        Assert.DoesNotContain(read.State.Places, p => VendorNames.IsVendorPlace(p.Id));
        Assert.DoesNotContain("\"Id\":\"vendor:", payload);
        var loaded = new CitySimulator(read.State, new WeightedFoodChoiceTests.Planner());
        foreach (var row in after.Rows)
        {
            Assert.Equal(row.State != VendorRowState.Tombstone, row.PlacePublished);
            Assert.Equal(row.State != VendorRowState.Tombstone ? row.Stand!.Position : (Position?)null, loaded.FindPosition(row.PlaceId));
        }
        Assert.Equal(400.125, after.Find("l0001")!.DeferredStand!.Position.X);
        Assert.Equal(new Position(11.5, 0, -3.25), loaded.FindPosition("vendor:l0001"));
        Assert.Single(loaded.FindPlace("vendor:l0000").WeeklyHours);
        Assert.Empty(loaded.FindPlace("vendor:r0000").WeeklyHours);
    }

    [Fact]
    public void ARouteRebuildKeepsTheRestoredVendorPlaces()
    {
        var city = Table(1, 0, 0);
        var read = SaveWire.Read(SaveWire.Text(city.State)).State!;
        var loaded = new CitySimulator(read, new WeightedFoodChoiceTests.Planner());
        var place = loaded.FindPlace("vendor:l0000");
        var rebuilt = loaded.RebuildRoutes(new WeightedFoodChoiceTests.Planner());
        Assert.Same(place, rebuilt.FindPlace("vendor:l0000"));
    }


    [Fact]
    public void SixtyFourLiveRowsSaveAndSixtyFiveAreRefused()
    {
        var city = Table(64, 0, 0);
        string payload = SaveWire.Text(city.State);
        Assert.Equal(SaveReadStatus.Loaded, SaveWire.Read(payload).Status);
        var ids = city.State.Vendors.Rows.Select(r => r.VehicleId).ToList();
        string over = WithRows(SaveWire.Text(Table(0, 0, 0).State), ids.Append("zz").Select(LiveRow));
        Refused(over, "Live vendor rows exceed the bound of 64.");
        Assert.Equal(SaveReadStatus.Loaded, SaveWire.Read(WithRows(SaveWire.Text(Table(0, 0, 0).State), ids.Select(LiveRow))).Status);
    }

    [Fact]
    public void OneHundredTwentyEightLiveAndRetiredRowsSaveAnd129AreRefused()
    {
        var city = Table(64, 64, 0);
        string payload = SaveWire.Text(city.State);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        string empty = SaveWire.Text(Table(0, 0, 0).State);
        var rows = Enumerable.Range(0, 64).Select(i => LiveRow("l" + i.ToString("D3"))).Concat(Enumerable.Range(0, 65).Select(i => RetiredRow("r" + i.ToString("D3"))));
        Refused(WithRows(empty, rows.OrderBy(r => r.Substring(4), StringComparer.Ordinal)), "Live and retired vendor rows exceed the bound of 128.");
        Refused(WithRows(empty, Enumerable.Range(0, 129).Select(i => RetiredRow("r" + i.ToString("D3")))), "Live and retired vendor rows exceed the bound of 128.");
    }

    [Fact]
    public void OneThousandTwentyFourTombstonesSaveAnd1025AreRefused()
    {
        var city = Table(0, 0, 1024);
        string payload = SaveWire.Text(city.State);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(1024, read.State!.Vendors.Tombstones);
        string empty = SaveWire.Text(Table(0, 0, 0).State);
        Refused(WithRows(empty, Enumerable.Range(0, 1025).Select(i => TombstoneRow("t" + i.ToString("D4")))), "Vendor tombstones exceed the bound of 1,024.");
    }

    [Fact]
    public void TheFullTableOf1152RowsSavesAndEveryRowHasAVIndex()
    {
        var city = Table(64, 64, 1024);
        var food = city.State.Food!;
        int slot = 0;
        foreach (var row in city.State.Vendors.Rows) food.BusinessReferences.Add(new BusinessReference { Slot = slot++, BusinessId = row.BusinessId, PlaceId = row.PlaceId });
        string payload = SaveWire.Text(city.State);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveWire.Text(read.State!));
        var cells = ((JArray)Wire(payload)["Food"]!["BusinessReferences"]!).Select(r => (string)r[3]!).ToList();
        Assert.Equal(SaveCodec.MaxVendorRows, cells.Count);
        Assert.Equal("v00", cells[0]); Assert.Equal("v" + MemoryWire.Number(1151, 2), cells[^1]);
        Assert.True(cells.Select((c, i) => c == "v" + MemoryWire.Number(i, 2)).All(x => x));
        Assert.Equal(food.BusinessReferences.Select(r => r.PlaceId), read.State!.Food!.BusinessReferences.Select(r => r.PlaceId));
    }


    [Fact]
    public void SixtyThreeReferencedRetiredRowsAndTwoSoldLiveVehiclesLeave65RetiredRowsThatSaveAndReload()
    {
        var rig = new Rig();
        var first = Enumerable.Range(0, 63).Select(i => "a" + i.ToString("D2")).ToArray();
        rig.OpenAll(At(8, 5), first);
        foreach (string id in first) rig.History(rig.Row(id));
        foreach (string id in first) StreetFoodHooks.Remove(id);
        rig.Run(rig.Now + 5);
        Assert.Equal((0, 63), (rig.Table.Live, rig.Table.Retired));
        rig.OpenAll(rig.Now + 5, "b0", "b1");
        Assert.Equal((2, 63), (rig.Table.Live, rig.Table.Retired));
        foreach (string id in new[] { "b0", "b1" }) rig.History(rig.Row(id));
        rig.Reload();
        StreetFoodHooks.Remove("b0"); StreetFoodHooks.Remove("b1");
        rig.Run(rig.Now + 5);
        Assert.Equal((0, 65, 0), (rig.Table.Live, rig.Table.Retired, rig.Table.Tombstones));
        Assert.Equal(0, rig.Vendors.Compact());
        rig.Reload();
        Assert.Equal((0, 65, 0), (rig.Table.Live, rig.Table.Retired, rig.Table.Tombstones));
    }

    [Fact]
    public void ANewVehicleIsRefusedAt128LiveAndRetiredAndTheCitySavesAndReloads()
    {
        var rig = new Rig();
        var a = Enumerable.Range(0, 64).Select(i => "a" + i.ToString("D2")).ToArray();
        rig.OpenAll(At(8, 5), a);
        foreach (string id in a) rig.History(rig.Row(id));
        foreach (string id in a) StreetFoodHooks.Remove(id);
        var b = Enumerable.Range(0, 64).Select(i => "b" + i.ToString("D2")).ToArray();
        foreach (string id in b) StreetFoodHooks.Add(id, x: 500);
        rig.Run(rig.Now + 5);
        Assert.Equal((64, 64), (rig.Table.Live, rig.Table.Retired));
        StreetFoodHooks.Remove("b00"); StreetFoodHooks.Add("c00");
        rig.Run(rig.Now + 5);
        Assert.Equal((63, 65), (rig.Table.Live, rig.Table.Retired));
        Assert.Equal("vendor table full: 128 live and retired vehicles", rig.Vendors.Refusals.Single(p => p.Key == "c00").Value);
        Assert.Null(rig.Table.Find("c00"));
        rig.Reload();
        Assert.Equal((63, 65), (rig.Table.Live, rig.Table.Retired));
        Assert.Equal("vendor table full: 128 live and retired vehicles", rig.Table.AdmissionRefusal(null));
    }

    [Fact]
    public void AVehicleRemembersAcrossTombstoneAndReturnAsGeneration2()
    {
        var rig = new Rig();
        rig.OpenAll(At(8, 5), "A");
        var a = rig.Row("A");
        int slot1 = rig.Remember(a);
        var history = rig.History(a);
        rig.Reload();

        StreetFoodHooks.Remove("A"); rig.Run(rig.Now + 5);
        Assert.Equal(VendorRowState.Retired, rig.Row("A").State);
        Assert.Equal(0, rig.Vendors.Compact());
        rig.Reload();
        rig.Food.History.RemoveAll(h => h.PlaceId == "vendor:A");
        Assert.Equal(1, rig.Vendors.Compact());
        Assert.Equal(VendorRowState.Tombstone, rig.Row("A").State);
        Assert.Null(rig.City.FindPosition("vendor:A"));
        Assert.DoesNotContain(rig.Food.Shops, s => s.PlaceId == "vendor:A");
        string payload = rig.Reload();
        Assert.Contains(TombstoneRow("A"), payload);
        AssertSlot(rig, slot1, "streetfood:vehicle:A:generation:1");

        rig.OpenAll(rig.Now + 5, "A");
        a = rig.Row("A");
        Assert.Equal((VendorRowState.Live, 2), (a.State, a.Generation));
        Assert.True(rig.Vendors.CanServe(a.BusinessId, rig.Now));
        int slot2 = rig.Remember(a);
        payload = rig.Reload();
        AssertSlot(rig, slot1, "streetfood:vehicle:A:generation:1");
        AssertSlot(rig, slot2, "streetfood:vehicle:A:generation:2");
        Assert.Equal(2, ((JArray)Wire(payload)["Food"]!["BusinessReferences"]!).Count(r => (string)r[3]! == "v00"));

        var food = rig.Food;
        food.BusinessReferences.Add(new BusinessReference { Slot = slot2 + 1, BusinessId = "streetfood:vehicle:A:generation:3", PlaceId = "vendor:A" });
        WriteRefused(rig.City.State, "names another business or generation");
        food.BusinessReferences.RemoveAt(food.BusinessReferences.Count - 1);
        string forged = payload.Replace("\"streetfood:vehicle:A:generation:2\",\"v00\"", "\"streetfood:vehicle:A:generation:3\",\"v00\"");
        Assert.NotEqual(payload, forged);
        Refused(forged, "names another business or generation");
    }

    [Fact]
    public void AReturnedVehicleWithoutANewExperienceRetiresAgainAndBecomesATombstoneBecauseGeneration1IsRemembered()
    {
        var rig = new Rig();
        rig.OpenAll(At(8, 5), "A");
        int slot1 = rig.Remember(rig.Row("A"));
        StreetFoodHooks.Remove("A"); rig.Run(rig.Now + 5);
        Assert.Equal(1, rig.Vendors.Compact());
        Assert.Equal(VendorRowState.Tombstone, rig.Row("A").State);
        rig.Reload();

        rig.OpenAll(rig.Now + 5, "A");
        Assert.Equal(2, rig.Row("A").Generation);
        var meal = rig.Food.Residents[0];
        meal.PlaceId = "vendor:A"; meal.BusinessId = rig.Row("A").BusinessId; meal.ProductId = FoodProducts.HotDog;
        rig.Reload();
        meal = rig.Food.Residents[0];
        StreetFoodHooks.Remove("A"); rig.Run(rig.Now + 5);
        Assert.Equal(VendorRowState.Retired, rig.Row("A").State);
        Assert.Equal(0, rig.Vendors.Compact());
        rig.Reload();
        meal = rig.Food.Residents[0];
        meal.PlaceId = meal.BusinessId = meal.ProductId = "";
        Assert.Equal(1, rig.Vendors.Compact());
        var row = rig.Row("A");
        Assert.Equal((VendorRowState.Tombstone, 2), (row.State, row.Generation));
        string payload = rig.Reload();
        Assert.Contains(TombstoneRow("A", 2), payload);
        AssertSlot(rig, slot1, "streetfood:vehicle:A:generation:1");
    }

    private static void AssertSlot(Rig rig, int slot, string business)
    {
        var reference = rig.Food.BusinessReferences.Single(r => r.Slot == slot);
        Assert.Equal((business, "vendor:A"), (reference.BusinessId, reference.PlaceId));
        Assert.Contains(rig.Food.Residents[0].Memory.Entries, e => e.BusinessRef == slot);
        Assert.Equal(slot, new FoodMemoryStore(rig.Food).Find(business));
    }

    [Fact]
    public void VehiclesChurnedPast64KeepTheCitySaveable()
    {
        var rig = new Rig();
        long at = At(8, 5);
        int remembered = 0;
        for (int round = 0; round < 6; round++)
        {
            var ids = Enumerable.Range(0, 40).Select(i => "v" + round + "-" + i.ToString("D2")).ToArray();
            rig.OpenAll(at, ids);
            Assert.Equal(40, rig.Table.Live);
            for (int i = 0; i < ids.Length; i += 10) { rig.Remember(rig.Row(ids[i])); remembered++; }
            rig.Reload();
            foreach (string id in ids) StreetFoodHooks.Remove(id);
            rig.Run(rig.Now + 5);
            Assert.Equal(40, rig.Table.Retired);
            rig.Reload();
            rig.Vendors.Compact();
            Assert.Equal((0, 0, remembered), (rig.Table.Live, rig.Table.Retired, rig.Table.Tombstones));
            rig.Reload();
            rig.Food.Residents[0].Memory.Entries.Clear();
            at = rig.Now + 60;
        }
        Assert.Equal(24, remembered);
        Assert.Equal(remembered, rig.Food.BusinessReferences.Count(r => VendorNames.IsVendorPlace(r.PlaceId)));
        rig.Reload();
        Assert.Equal(remembered, rig.Table.Tombstones);
    }

    [Fact]
    public void ADeferredStandMoveSurvivesReloadAndAppliesAfterTheCommittedResidentResolves()
    {
        var rig = new Rig();
        rig.OpenAll(At(8, 5), "A");
        Assert.True(rig.Vendors.CanServe(rig.Row("A").BusinessId, rig.Now));
        var meal = rig.Food.Residents[0];
        meal.PlaceId = "vendor:A"; meal.BusinessId = rig.Row("A").BusinessId; meal.ProductId = FoodProducts.HotDog;
        rig.RunTo(rig.Now + 60); StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A", 120, x: 500); rig.Run(rig.Now + 1);
        var row = rig.Row("A");
        Assert.NotNull(row.DeferredStand);
        Assert.Equal(500, row.DeferredStand!.Position.X);
        string payload = rig.Reload();
        row = rig.Row("A");
        Assert.NotNull(row.DeferredStand); Assert.Equal(500, row.DeferredStand!.Position.X);
        Assert.False(row.Stand!.Published);
        Assert.NotEqual(500, rig.City.FindPosition("vendor:A")!.Value.X);
        Assert.Empty(rig.City.FindPlace("vendor:A").WeeklyHours);
        rig.RunTo(rig.Now + 15);
        Assert.NotNull(rig.Row("A").DeferredStand);
        rig.Food.Residents[0].PlaceId = rig.Food.Residents[0].BusinessId = rig.Food.Residents[0].ProductId = "";
        rig.RunTo(rig.Now + 15);
        row = rig.Row("A");
        Assert.Null(row.DeferredStand);
        Assert.Equal(500, rig.City.FindPosition("vendor:A")!.Value.X);
        Assert.Single(rig.City.FindPlace("vendor:A").WeeklyHours);
        Assert.True(rig.Vendors.CanServe(row.BusinessId, rig.Now));
        rig.Reload();
        Assert.Null(rig.Row("A").DeferredStand);
        Assert.Contains(",500.0,", payload);
    }


    private static (CityState State, string Payload) Referenced()
    {
        var city = Table(1, 1, 1);
        var food = city.State.Food!;
        food.BusinessReferences.Add(new BusinessReference { Slot = 0, BusinessId = "b0", PlaceId = "s0" });
        int slot = 1;
        foreach (var row in city.State.Vendors.Rows) food.BusinessReferences.Add(new BusinessReference { Slot = slot++, BusinessId = row.BusinessId, PlaceId = row.PlaceId });
        return (city.State, SaveWire.Text(city.State));
    }

    [Fact]
    public void VendorPlacesUseTheVFormAndCityPlacesKeepTheirIndex()
    {
        var (state, payload) = Referenced();
        var cells = ((JArray)Wire(payload)["Food"]!["BusinessReferences"]!).Select(r => (string)r[3]!).ToArray();
        int city = state.Places.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToList().IndexOf("s0");
        Assert.Equal(new[] { MemoryWire.Number(city, 3), "v00", "v01", "v02" }, cells);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(new[] { "s0", "vendor:l0000", "vendor:r0000", "vendor:t0000" }, read.State!.Food!.BusinessReferences.Select(r => r.PlaceId));
    }

    [Theory]
    [InlineData("v03", "Unknown vendor business place reference.")]
    [InlineData("vZZ", "Invalid vendor business place reference.")]
    [InlineData("vw0", "Noncanonical base-36 alphabet.")]
    [InlineData("v0", "Invalid business place reference.")]
    [InlineData("v000", "Invalid business place reference.")]
    public void AVIndexOutsideTheTableIsRefused(string cell, string message)
    {
        var (_, payload) = Referenced();
        Refused(payload.Replace(",\"v02\"]", ",\"" + cell + "\"]"), message);
    }

    [Fact]
    public void AVIndexAtAnotherRowWithTheWrongBusinessIsRefused()
    {
        var (_, payload) = Referenced();
        Refused(payload.Replace(",\"v02\"]", ",\"v00\"]"), "names another business or generation");
    }

    [Fact]
    public void ACityIndexNamingAVendorPlaceIsRefused()
    {
        var (state, payload) = Referenced();
        var root = Wire(payload);
        var forged = ((JArray)root["Places"]!).First(p => (string)p["Id"]! == "s0").DeepClone();
        forged["Id"] = "vendor:zz";
        ((JArray)root["Places"]!).Add(forged);
        var ids = ((JArray)root["Places"]!).Select(p => (string)p["Id"]!).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var reference = ((JArray)root["Food"]!["BusinessReferences"]!)[0];
        reference[2] = "streetfood:vehicle:zz:generation:1"; reference[3] = MemoryWire.Number(ids.IndexOf("vendor:zz"), 3);
        Refused(Text(root), "A city business place reference names a vendor place.");
        state.Places.Add(new Place { Id = "vendor:zz", Name = "Forged", Kind = PlaceKind.Shop });
        WriteRefused(state, "A city place uses the vendor place prefix.");
    }

    [Fact]
    public void AReferenceToAVendorPlaceWithAnotherBusinessIdIsRefused()
    {
        foreach (string business in new[] { "b9", "streetfood:vehicle:l0001:generation:1", "streetfood:vehicle:l0000:generation:0",
            "streetfood:vehicle:l0000:generation:01", "streetfood:vehicle:l0000:generation:+1", "streetfood:vehicle:l0000" })
        {
            var (state, _) = Referenced();
            state.Food!.BusinessReferences[1].BusinessId = business;
            WriteRefused(state, "names another business or generation");
        }
        var (_, payload) = Referenced();
        Refused(payload.Replace("\"streetfood:vehicle:l0000:generation:1\",\"v00\"", "\"other:business\",\"v00\""), "names another business or generation");
    }


    [Fact]
    public void VehicleIdsOf128UnitsRoundTripAnd129AreRefused()
    {
        var city = Table(1, 1, 1, idLength: 128);
        Assert.All(city.State.Vendors.Rows, r => Assert.Equal(128, r.VehicleId.Length));
        string payload = SaveWire.Text(city.State);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveWire.Text(read.State!));
        string id = new string('x', 129);
        Refused(WithRows(SaveWire.Text(Table(0, 0, 0).State), new[] { TombstoneRow(id) }), "Invalid vendor vehicle id.");
        string unicode = "Café é 🚚";
        var escaped = Table(0, 0, 0);
        var row = escaped.State.Vendors.Admit(unicode, ""); escaped.State.Vendors.Move(row, VendorRowState.Tombstone);
        string text = SaveWire.Text(escaped.State);
        Assert.Equal(unicode, SaveWire.Read(text).State!.Vendors.Rows.Single().VehicleId);
        Refused(WithRows(text, new[] { "[2,\"a\\u0001\",1,\"vendor:a%01\",\"streetfood:vehicle:a%01:generation:1\"]" }), "Invalid vendor vehicle id.");
    }

    [Theory]
    [InlineData("中", 128)]
    [InlineData(" ", 128)]
    [InlineData("\ud83d\ude9a", 64)]
    [InlineData("é", 128)]
    public void MaximumLengthEscapedIdsRoundTripWithScopeMealPersonReferenceAndTombstone(string unit, int repeat)
    {
        string id = string.Concat(Enumerable.Repeat(unit, repeat)), other = "t" + id.Substring(unit.Length);
        Assert.Equal(128, id.Length); Assert.True(VendorNames.ValidId(id));
        Assert.Equal("vendor:" + VendorNames.Escape(id), VendorNames.PlaceId(id));
        Assert.Equal("streetfood:vehicle:" + VendorNames.Escape(id) + ":generation:7", VendorNames.BusinessId(id, 7));
        Assert.True(VendorNames.PlaceId(id).Length <= VendorNames.MaximumPlaceIdLength);
        Assert.True(VendorNames.BusinessId(id, int.MaxValue).Length <= VendorNames.MaximumBusinessIdLength);
        if (unit == "中")
        {
            Assert.Equal(VendorNames.MaximumPlaceIdLength, VendorNames.PlaceId(id).Length);
            Assert.Equal(VendorNames.MaximumBusinessIdLength, VendorNames.BusinessId(id, int.MaxValue).Length);
        }
        var city = WeightedFoodChoiceTests.Ready(5).city;
        var state = city.State;
        var table = state.Vendors;
        var tomb = table.Admit(other, ""); tomb.Generation = 2; table.Move(tomb, VendorRowState.Tombstone);
        var row = table.Admit(id, "cart");
        row.Stand = new VendorStand { OpeningId = 1, Position = new Position(10, 0, 0), OpenedDay = 2, OpenedMinute = 540, ClosesDay = 2, ClosesMinute = 660, Published = true };
        city.PublishVendorPlace(row, Menu(FoodProducts.HotDog)); row.PlacePublished = true;
        var food = state.Food!;
        food.BusinessReferences.Add(new BusinessReference { Slot = 0, BusinessId = row.BusinessId, PlaceId = row.PlaceId });
        food.BusinessReferences.Add(new BusinessReference { Slot = 1, BusinessId = VendorNames.BusinessId(other, 1), PlaceId = tomb.PlaceId });
        var person = state.Citizens.Single();
        var meal = food.Residents.Single(m => m.ResidentId == person.Identity.Id);
        meal.Status = MealStatus.Paid; meal.PlaceId = row.PlaceId; meal.BusinessId = row.BusinessId; meal.ProductId = FoodProducts.HotDog;
        person.MealOuting = new MealOuting { OriginId = person.HomeId, OriginActivity = Activity.Home, ResumeUntil = person.NextEventMinute + 600, ResumeEvent = person.NextEventMinute + 600 };
        person.PlaceId = row.PlaceId; person.Activity = Activity.Shop; person.Phase = CitizenPhase.AtPlace;
        person.ActivityUntilMinute = person.NextEventMinute;
        int day = (int)(state.Minute / 1440);
        meal.Today = new ResidentFoodToday { Day = day };
        meal.Today.Meals.Add(new MealOutcomeRecord { Kind = MealKind.Legacy, Status = MealOutcomeStatus.Bought, Reason = MealReasonCode.ReceiptConfirmed,
            PlaceId = row.PlaceId, ProductId = FoodProducts.HotDog, At = (int)state.Minute });
        string payload = SaveWire.Text(state);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveWire.Text(read.State!));
        Assert.Equal(new[] { other, id }.OrderBy(x => x, StringComparer.Ordinal), read.State!.Vendors.Rows.Select(r => r.VehicleId));
        Assert.Contains("\"" + VendorNames.PlaceId(id) + "\"", payload);
        Assert.Equal(row.PlaceId, read.State.Citizens.Single().PlaceId);
        Assert.Equal(row.PlaceId, read.State.Food!.Residents[0].Today!.Meals.Single().PlaceId);
        Assert.Equal(new Position(10, 0, 0), new CitySimulator(read.State, new WeightedFoodChoiceTests.Planner()).FindPosition(row.PlaceId));
        food.BusinessReferences.Add(new BusinessReference { Slot = 2, BusinessId = new string('b', 513), PlaceId = "s0" });
        WriteRefused(state, "Invalid Business reference identity.");
    }

    [Theory]
    [InlineData(28)]
    [InlineData(128)]
    public void AMaximumLengthVendorIdentitySurvivesReloadAndAwarenessRebuilds(int units)
    {
        var (city, market, planner) = WeightedFoodChoiceTests.Ready(5);
        string id = new string('中', units);
        var row = city.State.Vendors.Admit(id, "cart");
        row.Stand = new VendorStand { OpeningId = 1, Position = new Position(15, 0, 0), OpenedDay = 1, OpenedMinute = 0, ClosesDay = 1, ClosesMinute = 1380, Published = true };
        city.PublishVendorPlace(row, Menu(FoodProducts.Croissant)); row.PlacePublished = true;
        Assert.True(row.PlaceId.Length > 256);
        var read = SaveWire.Read(SaveWire.Text(city.State));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, planner);
        loaded.AttachFoodMarket(market);
        double target = 4320;
        while (!loaded.AdvanceTo(target, 64).CaughtUp) { }
        Assert.Equal(target, loaded.State.Minute);
        loaded.InvalidateFoodAwareness();
        target += 1440;
        while (!loaded.AdvanceTo(target, 64).CaughtUp) { }
        Assert.Equal(target, loaded.State.Minute);
        var again = SaveWire.Read(SaveWire.Text(loaded.State));
        Assert.True(again.Status == SaveReadStatus.Loaded, again.Error);
    }


    [Fact]
    public void AResidentAtWalkingToOrRecentlyAtAStandSavesAndReloads()
    {
        foreach (string arm in new[] { "at", "travelling", "visited" })
        {
            var city = Table(1, 1, 0);
            var state = city.State;
            var person = state.Citizens.Single();
            var meal = state.Food!.Residents.Single(m => m.ResidentId == person.Identity.Id);
            var live = state.Vendors.Find("l0000")!;
            var stand = city.FindPlace(live.PlaceId);
            if (arm == "at")
            {
                meal.Status = MealStatus.Paid; meal.PlaceId = live.PlaceId; meal.BusinessId = live.BusinessId; meal.ProductId = FoodProducts.HotDog;
                person.MealOuting = new MealOuting { OriginId = person.HomeId, OriginActivity = Activity.Home, ResumeUntil = person.NextEventMinute + 600, ResumeEvent = person.NextEventMinute + 600 };
                person.PlaceId = live.PlaceId; person.Activity = Activity.Shop; person.Phase = CitizenPhase.AtPlace;
                person.ActivityUntilMinute = person.NextEventMinute;
            }
            else if (arm == "travelling")
            {
                var home = city.FindPlace(person.HomeId);
                meal.Status = MealStatus.Travelling; meal.PlaceId = live.PlaceId; meal.BusinessId = live.BusinessId; meal.ProductId = FoodProducts.HotDog;
                meal.Allowance = Money.FromCents(1000); meal.Price = Money.FromCents(450); meal.ServiceMinutes = 5; meal.Deadline = meal.ExpiresAt;
                person.MealOuting = new MealOuting { OriginId = person.HomeId, OriginActivity = Activity.Home, ResumeUntil = person.NextEventMinute + 600, ResumeEvent = person.NextEventMinute + 600 };
                person.DestinationId = live.PlaceId; person.DestinationActivity = Activity.Shop; person.Phase = CitizenPhase.Travelling;
                person.Journey = new Route { Legs = { new RouteLeg { From = home.Position, To = stand.Position, Mode = TravelMode.Walk, Minutes = 5 } } }.Commit();
                person.DepartureMinute = state.Minute; person.NextEventMinute = state.Minute + 5; person.ActivityUntilMinute = state.Minute + 30;
            }
            else person.RecentVisits.Add(new VisitRecord { AtMinute = state.Minute, PlaceId = "vendor:r0000", Activity = Activity.Shop });
            string payload = SaveWire.Text(state);
            var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, arm + ": " + read.Error);
            Assert.Equal(payload, SaveWire.Text(read.State!));
            var loaded = read.State!.Citizens.Single();
            if (arm == "at") Assert.Equal(live.PlaceId, loaded.PlaceId);
            else if (arm == "travelling") Assert.Equal(live.PlaceId, loaded.DestinationId);
            else
            {
                Assert.Equal("vendor:r0000", loaded.RecentVisits[^1].PlaceId);
                var visits = (JArray)((JArray)Wire(payload)["Citizens"]!)[0]["RecentVisits"]!;
                Assert.Equal("v01", ((string)visits.Last()!).Substring(17, 3));
            }
            Assert.NotNull(new CitySimulator(read.State, new WeightedFoodChoiceTests.Planner()).FindPosition(live.PlaceId));
        }
    }

    [Theory]
    [InlineData("v02", "Invalid visit place/activity code.")]
    [InlineData("vZZ", "Invalid visit place/activity code.")]
    public void AVisitVIndexOutsideTheTableIsRefused(string cell, string message)
    {
        var state = Table(1, 1, 0).State;
        state.Citizens.Single().RecentVisits.Add(new VisitRecord { AtMinute = state.Minute, PlaceId = "vendor:r0000", Activity = Activity.Shop });
        string payload = SaveWire.Text(state);
        string visit = (string)((JArray)((JArray)Wire(payload)["Citizens"]!)[0]["RecentVisits"]!).Last()!;
        Refused(payload.Replace(visit, visit.Substring(0, 17) + cell + visit.Substring(20)), message);
    }

    [Fact]
    public void ARecentVisitKeepsTheRetiredRowAndItsPlaceUntilItAgesOut()
    {
        var rig = new Rig();
        rig.OpenAll(At(8, 5), "A");
        var person = rig.City.State.Citizens.Single();
        person.RecentVisits.Add(new VisitRecord { AtMinute = rig.City.State.Minute, PlaceId = "vendor:A", Activity = Activity.Shop });
        StreetFoodHooks.Remove("A"); rig.Run(rig.Now + 5);
        Assert.Equal(VendorRowState.Retired, rig.Row("A").State);
        Assert.Equal(0, rig.Vendors.Compact());
        rig.Reload();
        Assert.NotNull(rig.City.FindPosition("vendor:A"));
        rig.City.State.Citizens.Single().RecentVisits.Clear();
        Assert.Equal(1, rig.Vendors.Compact());
        Assert.Null(rig.Table.Find("A"));
        rig.Reload();
    }

    [Fact]
    public void AVisitorAtAStandWritesTheVendorFormInItsRoutineAndArrival()
    {
        var (state, market) = WorkplaceCapacityTests.Fresh();
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(market);
        double target = state.Minute + 30 * 60;
        while (!city.AdvanceTo(target, 64).CaughtUp) { }
        Fill(city, 1, 0, 0);
        var live = state.Vendors.Rows.Single();
        var routine = state.VisitorRoutines.First(r => r.Phase == CitizenPhase.AtPlace && r.PlaceId != r.HomeId && r.MealOuting == null && r.OptionalStop == null);
        routine.Activity = Activity.Shop;
        var visitor = state.Visitors[state.VisitorRoutines.IndexOf(routine)];
        var arrival = new VisitRecord { AtMinute = state.Minute, PlaceId = live.PlaceId, Activity = Activity.Shop };
        routine.PlaceId = live.PlaceId; routine.RecentVisits.Clear(); routine.RecentVisits.Add(arrival); visitor.LastArrival = arrival;
        string payload = SaveWire.Text(state);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveWire.Text(read.State!));
        int at = state.VisitorRoutines.IndexOf(routine);
        Assert.Equal(live.PlaceId, read.State!.VisitorRoutines[at].PlaceId);
        Assert.Equal(live.PlaceId, read.State.Visitors[at].LastArrival!.PlaceId);
        Assert.Contains("|v00|", (string)((JArray)Wire(payload)["VisitorRoutines"]!)[at]["T"]!);
        string tuple = (string)((JArray)Wire(payload)["Visitors"]!)[at]!;
        Assert.Equal("v00", tuple.Substring(tuple.Length - 4, 3));
    }

    [Fact]
    public void DuplicateOrUnorderedVehicleIdsAreRefused()
    {
        string empty = SaveWire.Text(Table(0, 0, 0).State);
        Refused(WithRows(empty, new[] { TombstoneRow("a"), TombstoneRow("a") }), "Duplicate or unordered vendor vehicle id.");
        Refused(WithRows(empty, new[] { TombstoneRow("b"), TombstoneRow("a") }), "Duplicate or unordered vendor vehicle id.");
        Refused(WithRows(empty, new[] { LiveRow("a"), RetiredRow("a") }), "Duplicate or unordered vendor vehicle id.");
    }

    [Theory]
    [InlineData("[2,\"a\",1,\"vendor:b\",\"streetfood:vehicle:a:generation:1\"]", "Vendor place differs from its vehicle id.")]
    [InlineData("[2,\"a\",2,\"vendor:a\",\"streetfood:vehicle:a:generation:1\"]", "Vendor business differs from its vehicle id and generation.")]
    [InlineData("[2,\"a\",0,\"vendor:a\",\"streetfood:vehicle:a:generation:0\"]", "Invalid vendor generation.")]
    [InlineData("[3,\"a\",1,\"vendor:a\",\"streetfood:vehicle:a:generation:1\"]", "Unknown vendor row state.")]
    [InlineData("[2,\"a\",1,\"vendor:a\",\"streetfood:vehicle:a:generation:1\",0]", "Surplus vendor row slot.")]
    [InlineData("[0,\"a\",\"van\",1,\"vendor:a\",null,null,[false,-1,-1,-1,false,-1,false]]", "Unknown vendor kind.")]
    [InlineData("[0,\"a\",\"cart\",1,\"vendor:a\",null,null,[false,-1,-1,-2,false,-1,false]]", "Invalid vendor until hour.")]
    [InlineData("[0,\"a\",\"cart\",1,\"vendor:a\",null,null,[false,-1,-1,-1,false,-1]]", "Invalid vendor handback flag.")]
    [InlineData("[0,\"a\",\"cart\",01,\"vendor:a\",null,null,[false,-1,-1,-1,false,-1,false]]", "Invalid state")]
    public void MalformedRowsAreRefused(string row, string message)
        => Refused(WithRows(SaveWire.Text(Table(0, 0, 0).State), new[] { row }), message);


    [Fact]
    public void AVehicleScopeWithoutItsRowIsRefused()
    {
        var city = Table(1, 0, 0);
        string payload = SaveWire.Text(city.State);
        Refused(WithRows(payload, Array.Empty<string>()), "A vehicle scope has no live or retired vendor row with a stand.");
        city.State.Vendors.Remove(city.State.Vendors.Rows[0]);
        WriteRefused(city.State, "A vehicle scope has no live or retired vendor row with a stand.");
    }

    [Fact]
    public void ARowWithAStandButNoScopeIsRefused()
    {
        var city = Table(1, 0, 0);
        string payload = SaveWire.Text(city.State);
        var root = Wire(payload);
        var shops = (JArray)root["Food"]!["Shops"]!;
        shops.Where(s => ((string)s["PlaceId"]!).StartsWith("vendor:", StringComparison.Ordinal)).ToList().ForEach(s => s.Remove());
        Refused(Text(root), "A vendor row with a stand has no vehicle scope.");
        city.State.Food!.Shops.RemoveAll(s => s.Channel == FoodScopeChannel.Vehicle);
        WriteRefused(city.State, "A vendor row with a stand has no vehicle scope.");
    }

    [Fact]
    public void AMealNamingAMissingVendorPlaceIsRefused()
    {
        var state = Table(1, 0, 1).State;
        var meal = state.Food!.Residents[0];
        meal.PlaceId = "vendor:l0000"; meal.BusinessId = "streetfood:vehicle:l0000:generation:1"; meal.ProductId = FoodProducts.HotDog;
        string payload = SaveWire.Text(state);
        Assert.Equal(SaveReadStatus.Loaded, SaveWire.Read(payload).Status);
        foreach (string missing in new[] { "vendor:nobody", "vendor:t0000" })
        {
            meal.PlaceId = missing;
            WriteRefused(state, "Meal lost its historical business/place identity.");
        }
        meal.PlaceId = "vendor:l0000";
        state.Food.History.Add(new FoodShopDay { Day = 1, PlaceId = "vendor:nobody", BusinessId = "x" });
        WriteRefused(state, "Invalid market history ownership.");
    }

    [Fact]
    public void ChannelAndPlaceMustAgree()
    {
        var state = Table(1, 0, 0).State;
        var vehicle = state.Food!.Shops.Single(s => s.Channel == FoodScopeChannel.Vehicle);
        var shop = state.Food.Shops.First(s => s.Channel == FoodScopeChannel.Shop);
        string payload = SaveWire.Text(state);
        shop.UseChannel(FoodScopeChannel.Vehicle);
        WriteRefused(state, "A vehicle scope names a city place.");
        shop.UseChannel(FoodScopeChannel.Shop); vehicle.UseChannel(FoodScopeChannel.Shop);
        WriteRefused(state, "A shop scope names a vendor place.");
        vehicle.UseChannel(FoodScopeChannel.Vehicle); vehicle.BusinessId = "streetfood:vehicle:l0000:generation:2";
        WriteRefused(state, "differs from its vendor row's business");
        var root = Wire(payload);
        var shops = (JArray)root["Food"]!["Shops"]!;
        var cityScope = shops.First(s => (string)s["PlaceId"]! == "s0");
        cityScope["Channel"] = 1; Refused(Text(root), "A vehicle scope names a city place.");
        cityScope["Channel"] = 2; Refused(Text(root), "Unknown food scope channel.");
        cityScope["Channel"] = 0;
        shops.First(s => ((string)s["PlaceId"]!).StartsWith("vendor:", StringComparison.Ordinal))["Channel"] = 0;
        Refused(Text(root), "A shop scope names a vendor place.");
    }

    [Fact]
    public void AStandWithoutItsVendorPlaceIsRefused()
    {
        var state = Table(1, 0, 0).State;
        var row = state.Vendors.Rows[0];
        string payload = SaveWire.Text(state);
        var root = Wire(payload);
        var wireRow = (JArray)Rows(root)[0];
        wireRow[6] = wireRow[5]; wireRow[5] = JValue.CreateNull();
        Refused(Text(root), "A deferred vendor stand needs the stand it replaces.");
        row.PlacePublished = false;
        WriteRefused(state, "A vendor place exists exactly while its row has a stand.");
        row.PlacePublished = true;
        var stand = row.Stand; row.Stand = null; row.DeferredStand = stand;
        WriteRefused(state, "A vendor place exists exactly while its row has a stand.");
        row.Stand = stand; row.DeferredStand = null;
        row.Stand!.ClosesDay = 1;
        WriteRefused(state, "Invalid vendor stand closing time.");
    }

    [Fact]
    public void ATombstoneKeepsOnlyItsIdentityOrIsRefused()
    {
        var city = Table(0, 0, 1);
        var row = city.State.Vendors.Rows[0];
        row.Kind = "cart";
        WriteRefused(city.State, "A vendor tombstone keeps only its identity.");
        row.Kind = ""; row.Authority.Desired = true;
        WriteRefused(city.State, "A vendor tombstone keeps only its identity.");
    }

    [Fact]
    public void AVendorTableIsRefusedWhenItIsNotAnArrayOrHasAnotherVersion()
    {
        string payload = SaveWire.Text(Table(0, 0, 0).State);
        Refused(payload.Replace("\"Vendors\":[1,[]]", "\"Vendors\":null"), "");
        Refused(payload.Replace("\"Vendors\":[1,[]]", "\"Vendors\":[2,[]]"), "Invalid vendor table version.");
        Refused(payload.Replace("\"Vendors\":[1,[]]", "\"Vendors\":[1,[],0]"), "Surplus vendor table slot.");
        Refused(payload.Replace(",\"Vendors\":[1,[]]", ""), "");
    }


    private static void RefusedAndPreserved(string payload, SaveReadStatus status, string? message = null)
    {
        var replacement = CurrentSchemaOnlyTests.City();
        var read = SaveWire.Read(payload);
        Assert.True(read.Status == status, read.Status + ": " + read.Error); Assert.Null(read.State);
        if (message != null) Assert.Contains(message, read.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "kept", [FoodAuthorityStore.Key] = "manifest" };
        var before = new Dictionary<string, string>(data);
        Assert.Equal(status, SaveSession.Open(data, replacement.PopulationKey).InitialReadResult.Status);
        Assert.Equal(status, SaveStore.ReadFrom(data, replacement.PopulationKey).Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Same(payload, data[SaveStore.StateKey]);
        Assert.Equal(before, data);
    }

    [Fact]
    public void AnOlderDevelopmentCityIsRefusedWithTheNewCityStatusAndKept()
    {
        foreach (int older in new[] { 29, 36, 37 })
        {
            string payload = SaveWire.Text(Table(1, 1, 1).State)
                .Replace("\"Schema\":" + CityState.CurrentSchema + ",", "\"Schema\":" + older + ",");
            RefusedAndPreserved(payload, SaveReadStatus.DevelopmentSchema, "start a new city");
            Assert.Equal(older, SaveWire.Read(payload).SourceSchema);
            DevelopmentChain.RequireRefusedCapabilities(payload);
        }
    }


    internal static IEnumerable<(string Name, Func<CityState> Build)> GoldenFixtures() => new (string, Func<CityState>)[]
    {
        ("shops-only", () => WeightedFoodChoiceTests.Ready(5, shops: 3).city.State),
        ("vendors-all-states", () => { var c = Table(3, 2, 4); c.State.Vendors.Find("l0001")!.DeferredStand = new VendorStand { OpeningId = 9, Position = new Position(400.125, 1, 2), OpenedDay = 3, OpenedMinute = 600 }; return c.State; }),
        ("vendors-at-bounds", () => { var c = WeightedFoodChoiceTests.Ready(5).city; Fill(c, 64, 64, 1024, references: true); return c.State; }),
    };

    [Theory]
    [InlineData("shops-only", "e39fbfe32157c405cb53ed40530e6233d0ff8cd365535c1c902390699c2c2f0f", 3631)]
    [InlineData("vendors-all-states", "3fa4c8cde1c8ecf7373b35868f3798ce7e3ed3afba3071f6c719e3e306ba6a33", 5072)]
    [InlineData("vendors-at-bounds", "37be50a816517507f40fecae04a7f98b8c03a99ac32f4851d088718f623aaad3", 174735)]
    public void GoldenVendorWriterBytesArePinned(string name, string sha256, int characters)
    {
        string payload = SaveCodec.Write(GoldenFixtures().Single(f => f.Name == name).Build());
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        string actual = Sha(payload) + " " + payload.Length;
        Assert.True(sha256 + " " + characters == actual, "golden " + name + ": " + actual);
    }

    [Fact]
    public void GoldenFixturesAreDeterministic()
    {
        foreach (var (_, build) in GoldenFixtures()) Assert.Equal(SaveWire.Text(build()), SaveWire.Text(build()));
    }


    [Fact]
    public void TheSnapshotCopiesTheVendorTableAndTheChannel()
    {
        var city = Table(3, 2, 4);
        var state = city.State;
        string expected = SaveWire.Text(state);
        var snapshot = SaveCodec.Capture(state);
        Assert.NotSame(state.Vendors, snapshot.Wire.Vendors);
        for (int i = 0; i < state.Vendors.Rows.Count; i++)
        {
            Assert.NotSame(state.Vendors.Rows[i], snapshot.Wire.Vendors.Rows[i]);
            if (state.Vendors.Rows[i].Stand != null) Assert.NotSame(state.Vendors.Rows[i].Stand, snapshot.Wire.Vendors.Rows[i].Stand);
        }
        var live = state.Vendors.Find("l0002")!;
        live.Authority.Desired = false; live.Stand!.Published = false;
        state.Food!.Shops.Single(s => s.PlaceId == "vendor:l0000").UseChannel(FoodScopeChannel.Shop);
        Assert.Equal(expected, SaveWire.Body(SaveCodec.Encode(snapshot)));
    }
}
