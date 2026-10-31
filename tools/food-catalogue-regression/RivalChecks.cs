using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Core;
using PersistentCity.Runtime;

internal static class RivalChecks
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static void Throws(Action read, string name)
    { try { read(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception(name); }
    private static CityBuildingController Shop(int number = 7, bool player = false) => new() {
        buildingRegistration = new() { StreetNumber = number, RentedByPlayer = player,
            retailPrices = new() { new() { itemName = FoodProducts.Burger, price = 7.30f } } } };
    private static string Place(int number = 7) => "address:Test%20Street:" + number + ":shop";
    private static RivalFoodCatalogue Build(params CityBuildingController[] shops)
    { var c = new RivalFoodCatalogue(); Refresh(c, shops); return c; }
    private static void Refresh(RivalFoodCatalogue c, IEnumerable<CityBuildingController> shops)
    { c.BeginRefresh(shops); while (!c.ContinueRefresh()) { } }

    internal static void Run()
    {
        // IL spy guard: fail on every native store or unreviewed native call, including setters.
        // This supplements throwing forbidden API doubles; public native fields cannot intercept writes.
        AssertReadOnlyIL();
        var rival = Shop(); var player = Shop(8, true);
        using var cache = Build(rival, player);
        var first = cache.ReadSelected(new[] { Place(8), Place() });
        Check(first.Businesses.Count == 2 && first.Refused.Count == 0, "player and rival observed separately");
        var r = first.Businesses.Single(x => x.Kind == FoodAlternativeKind.Rival);
        var p = first.Businesses.Single(x => x.Kind == FoodAlternativeKind.Player);
        Check(r.Id == FoodBusinessCatalogue.BusinessIdentity(rival.buildingRegistration) && r.OwnerId == "rival"
            && p.OwnerId == "player" && p.AvailabilityEvidence == FoodAvailabilityEvidenceKind.Unknown, "native identity reused; no invented player supply");
        Check(r.Stock == FoodStockEvidenceKind.Unknown && r.Offers.Single().Stock == FoodStockEvidenceKind.Unknown
            && r.AvailabilityEvidence == FoodAvailabilityEvidenceKind.EstimatedFromPriceAndHours, "unknown stock; estimate label only");
        Check(r.Offers.Single().Price == Money.FromNative(7.30f) && !r.Offers.Single().Price.IsWholeCents(), "binary32 price exact without rounding");
        Check(r.HasReadableDoor && r.Accessible && r.Hours.Single().Day == 0 && r.CustomerServicePercent == 75, "door access hours and saved rating");
        var reg = rival.buildingRegistration;
        reg.retailPrices[0].price = 5.25f; reg.scheduleDays[0].openingHourSlots[0].endingHour = 18;
        reg.satisfaction!.customerService = 20; reg.temporarilyClosed = true; rival.Accessible = false;
        rival.entranceDoors.Clear();
        Refresh(cache, new[] { rival, player });
        var changed = cache.ReadSelected(new[] { Place() }).Businesses.Single();
        Check(changed.TemporarilyClosed && changed.Hours.Single().ToMinute == 1080 && !changed.Accessible
            && !changed.HasReadableDoor && changed.CustomerServicePercent == 20, "closure explicit; schedule retained, live access and rating refreshed");
        Check(r.Offers.Single().Price == Money.FromNative(7.30f) && r.Hours.Single().ToMinute == 1200
            && r.CustomerServicePercent == 75 && r.HasReadableDoor, "native mutation cannot change old detached image");
        reg.retailPrices.Add(new() { itemName = "unreviewed", price = float.NaN });
        Check(cache.ReadSelected(new[] { Place() }).Businesses.Single().UnsupportedProductCount == 1, "unsupported advertised product counted, never offered");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1, 0, 10001, float.Epsilon, 0.00001f })
        {
            reg.retailPrices[0].price = invalid;
            Check(cache.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.InvalidPrice, "bad price refused: " + invalid);
        }
        reg.retailPrices[0].price = 10000;
        Check(cache.ReadSelected(new[] { Place() }).Businesses.Single().Offers.Single().Price == Money.FromCents(1000000), "exact upper price bound");
        reg.retailPrices.Add(new() { itemName = FoodProducts.Burger });
        Check(cache.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.InvalidPrice, "duplicate price refused");
        reg.retailPrices.RemoveAt(2);
        reg.scheduleDays[0].openingHourSlots[0].endingHour = 25;
        Check(cache.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.InvalidHours, "malformed hours refused even closed");
        reg.scheduleDays[0].openingHourSlots[0].endingHour = 20;
        reg.satisfaction.customerService = 101;
        Check(cache.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.InvalidRating, "invalid rating refused");
        reg.satisfaction = null;
        Check(cache.ReadSelected(new[] { Place() }).Businesses.Single().CustomerServicePercent == null, "unknown rating retained");
        Throws(() => cache.ReadSelected(Enumerable.Range(1, 9).Select(Place).ToArray()), "nine-shop detail refused");
        Throws(() => cache.ReadSelected(new[] { Place(), Place() }), "duplicate shortlist refused");
        Check(cache.ReadSelected(new[] { Place(999) }).Refused[Place(999)] == FoodCatalogueRefusal.Missing, "missing place explicit");
        using (var duplicate = Build(Shop(), Shop()))
            Check(duplicate.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.AmbiguousIdentity, "duplicate identities excluded");
        foreach (bool wasPlayer in new[] { false, true })
        {
            var s = Shop(player: wasPlayer); using var owner = Build(s);
            s.buildingRegistration.RentedByPlayer = !wasPlayer;
            Check(owner.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.AmbiguousIdentity, "ownership changes refused before refresh");
            s.buildingRegistration.RentedByPlayer = wasPlayer; Refresh(owner, new[] { s });
            Check(owner.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.AmbiguousIdentity, "ambiguous identity never revived");
        }
        for (int replacement = 0; replacement < 4; replacement++)
        {
            var s = Shop(); using var generations = Build(s);
            if (replacement == 0) s.buildingRegistration = Shop().buildingRegistration;
            if (replacement == 1) s = Shop();
            if (replacement == 2) s.buildingRegistration.retailPrices = new(s.buildingRegistration.retailPrices);
            if (replacement == 3) { Refresh(generations, Array.Empty<CityBuildingController>()); }
            Refresh(generations, new[] { s });
            Check(generations.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.AmbiguousIdentity, "same-day replacement or disappearance refused " + replacement);
        }
        var stable = Shop(); using (var names = Build(stable))
        {
            stable.buildingRegistration.BusinessName = "Different name";
            Check(names.ReadSelected(new[] { Place() }).Businesses.Single().Id == FoodBusinessCatalogue.BusinessIdentity(stable.buildingRegistration), "name never establishes generation");
            stable.buildingRegistration.businessOwnerRivalId = "";
            Check(names.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.AmbiguousIdentity, "missing owner cannot inherit identity");
        }
        var noOwner = Shop(); noOwner.buildingRegistration.businessOwnerRivalId = "";
        using (var unknown = Build(noOwner)) Check(unknown.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.UnknownOwner, "unknown rival owner refused");
        var shuffled = new[] { Shop(10), Shop(2), Shop(9) };
        using (var a = Build(shuffled)) using (var b = Build(shuffled.Reverse().ToArray()))
            Check(a.ReadSelected(new[] { Place(9), Place(2), Place(10) }).Businesses.Select(x => x.Id)
                .SequenceEqual(b.ReadSelected(new[] { Place(10), Place(9), Place(2) }).Businesses.Select(x => x.Id)), "deterministic result order");
        using (var bounded = new RivalFoodCatalogue())
        {
            var shops = Enumerable.Range(0, 20001).Select(n => Shop(n)).ToArray();
            bounded.BeginRefresh(shops);
            Check(!bounded.ContinueRefresh() && bounded.RetainedPlaces == 16, "one unit consumes only sixteen places");
            Throws(() => bounded.ReadSelected(new[] { Place() }), "partial image not published");
            Throws(() => { while (!bounded.ContinueRefresh()) { } }, "20001 places fail closed");
            Check(bounded.RetainedPlaces == 20000 && !bounded.RefreshPending, "overflow bounded and continuation disposed");
            Throws(() => bounded.ReadSelected(new[] { Place() }), "overflow image cannot be read");
        }
        var vacant = Shop(); using (var vacancy = Build(vacant))
        {
            vacant.buildingRegistration.AvailableForRent = true; Refresh(vacancy, new[] { vacant });
            vacant.buildingRegistration.AvailableForRent = false; Refresh(vacancy, new[] { vacant });
            Check(vacancy.ReadSelected(new[] { Place() }).Refused[Place()] == FoodCatalogueRefusal.AmbiguousIdentity, "vacancy cannot transfer old identity");
        }
        using (var city = Build(Shop()))
        {
            var previous = SaveGameManager.Current;
            SaveGameManager.Current = new Save();
            Throws(() => city.ReadSelected(new[] { Place() }), "city reload cannot inherit session identity");
            SaveGameManager.Current = previous;
        }
        using (var interrupted = Build(Shop()))
        {
            interrupted.BeginRefresh(new[] { Shop() });
            var previous = SaveGameManager.Current;
            SaveGameManager.Current = new Save();
            Throws(() => interrupted.ContinueRefresh(), "city reload interrupts continuation");
            Check(!interrupted.RefreshPending, "city change releases enumerator");
            SaveGameManager.Current = previous;
        }
        var canary = Shop();
        string Fingerprint() => System.Text.Json.JsonSerializer.Serialize(canary, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        string before = Fingerprint();
        using (var repeat = Build(canary))
            for (int n = 0; n < 100; n++) { Refresh(repeat, new[] { canary }); repeat.ReadSelected(new[] { Place() }); }
        Check(Fingerprint() == before, "all native double fields (including stock/finance canaries) unchanged after repeated reads");
        Console.WriteLine($"{checks} D1 read-only rival catalogue checks passed. No Unity/game execution.");
        Bench(1024); Bench(20000);
    }

    private static void AssertReadOnlyIL()
    {
        var allowedNativeCalls = new HashSet<string> { "get_HasEstablishedBusiness", "get_Instance", "get_Current", "op_Equality", "op_Inequality" };
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(o => unchecked((ushort)o.Value));
        foreach (var type in new[] { typeof(RivalFoodCatalogue) }.Concat(typeof(RivalFoodCatalogue).GetNestedTypes(BindingFlags.NonPublic)))
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)))
        {
            var il = method.GetMethodBody()?.GetILAsByteArray(); if (il == null) continue;
            for (int i = 0; i < il.Length;)
            {
                ushort value = il[i++]; if (value == 0xfe) value = (ushort)(0xfe00 | il[i++]);
                var op = opcodes[value]; int size = op.OperandType switch {
                    OperandType.InlineNone => 0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2, OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i), _ => 4 };
                if (op.OperandType == OperandType.InlineField)
                {
                    var field = method.Module.ResolveField(BitConverter.ToInt32(il, i))!;
                    bool native = field.DeclaringType!.Namespace == null;
                    Check(!native || (op != OpCodes.Stfld && op != OpCodes.Stsfld && op != OpCodes.Ldflda && op != OpCodes.Ldsflda), "native write or writable field address: " + field);
                    if (native) Check(new[] { "Instance", "Current", "cityBuildingControllers", "building", "buildingRegistration", "StreetName", "StreetNumber", "creationDay", "businessTypeName", "RentedByPlayer", "businessOwnerRivalId", "retailPrices", "AvailableForRent", "satisfaction", "customerService", "itemName", "price", "scheduleDays", "isOpen", "openingHourSlots", "day", "startingHour", "endingHour", "entranceDoors", "doorTransform", "temporarilyClosed", "Day", "Hour", "Minute" }.Contains(field.Name), "unreviewed native field read: " + field);
                }
                if (op.OperandType == OperandType.InlineMethod)
                {
                    var target = method.Module.ResolveMethod(BitConverter.ToInt32(il, i), type.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null)!;
                    if (target.DeclaringType!.Namespace == null) Check(allowedNativeCalls.Contains(target.Name), "unreviewed native call: " + target);
                }
                i += size;
            }
        }
    }

    private static (double Ms, long Bytes) Measure(Action action, int repeats)
    {
        long startBytes = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
        for (int n = 0; n < repeats; n++) action();
        return (Stopwatch.GetElapsedTime(start).TotalMilliseconds / repeats, (GC.GetAllocatedBytesForCurrentThread() - startBytes) / repeats);
    }
    private static void Bench(int count)
    {
        var shops = Enumerable.Range(0, count).Select(n => Shop(n)).ToArray();
        // JIT warm-up outside the samples; cold means empty cache, not cold process/JIT.
        using (var jit = Build(shops)) { jit.ReadSelected(Enumerable.Range(0, 8).Select(Place).ToArray()); }
        var cold = Measure(() => { using var c = Build(shops); }, 10);
        long before = GC.GetTotalMemory(true);
        using var cache = Build(shops);
        long retained = GC.GetTotalMemory(true) - before;
        var warm = Measure(() => Refresh(cache, shops), 30);
        var eight = Enumerable.Range(0, 8).Select(Place).ToArray();
        var detail = Measure(() => cache.ReadSelected(eight), 1000);
        cache.BeginRefresh(shops);
        cache.ContinueRefresh(); // warm the continuation call site outside timing
        var unit = Measure(() => cache.ContinueRefresh(), 32);
        while (!cache.ContinueRefresh()) { }
        GC.KeepAlive(shops);
        Console.WriteLine(FormattableString.Invariant($"D1 PERF {count}: empty-cache build {cold.Ms:F4} ms/{cold.Bytes} B; warm refresh {warm.Ms:F4} ms/{warm.Bytes} B; 16-read unit {unit.Ms:F4} ms/{unit.Bytes} B; eight details {detail.Ms:F4} ms/{detail.Bytes} B; retained {retained} B (GC estimate; native fixtures excluded)."));
    }
}
