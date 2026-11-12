using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests
{
    /// <summary>RLO 1.0 overrule (Peter, 12 Nov 2026): Persistent City owns the overlap with RLO's Living population on, off
    /// or toggled; an unreadable switch fails safe; contract-1 CityApi binding is unchanged; F8h sees RLO's non-Harmony
    /// calculator replacement and does not refuse for it. Stand-in RLO assemblies are emitted; no RLO or game DLL is used.</summary>
    [Collection(RloCompatibilityTests.SharedFlareState)]
    public sealed class RloOverruleTests
    {
        // One shop's hour as the native paths see it. The table is the game's static customer list, which RLO's loyalty and
        // interiors code reads and extends directly (CustomerDraw.Entries reads the field, past CustomerEntriesHelper).
        // Every native path asks the real owned-shop gate (RloOverrule.AdmitsNative), as NativeFoodOwnership's prefixes do.
        private sealed class Entry { public readonly List<string> Lines = new List<string>(); public bool Completed; }

        private sealed class ShopHour
        {
            public readonly List<Entry> Table = new List<Entry>();
            public readonly List<string> Sold = new List<string>();
            public bool Owned;
            public int Served;

            // UpdateCustomerEntriesForPlayerBusiness: the native draw, where RLO's replaced calculator scales the count.
            public void Generate(int initialCustomers, double rloScale)
            {
                if (!RloOverrule.AdmitsNative(NativeShopPath.Generation, Owned)) return;
                for (int i = 0; i < (int)Math.Round(initialCustomers * rloScale); i++) { var e = new Entry(); e.Lines.Add("burger"); Table.Add(e); }
            }

            // RLO LoyaltyGame.Basket / InteriorGame.Basket: one more line on native entries still to come.
            public void RloBasket(string product) { foreach (var e in Table) if (!e.Completed) e.Lines.Add(product); }

            public IReadOnlyList<Entry> Draw() => RloOverrule.AdmitsNative(NativeShopPath.Draw, Owned) ? Table : (IReadOnlyList<Entry>)Array.Empty<Entry>();

            // ProcessAllCustomersFromThisHour.
            public void NativeBatch()
            {
                if (!RloOverrule.AdmitsNative(NativeShopPath.Batch, Owned)) return;
                foreach (var e in Table.ToArray()) if (!e.Completed) Process(e, own: false);
            }

            // ProcessCustomer.
            public void Process(Entry e, bool own)
            {
                if (!RloOverrule.AdmitsNative(NativeShopPath.Process, Owned, own)) return;
                e.Completed = true; Served++; Sold.AddRange(e.Lines);
            }

            // NativeFoodMarket.SellLine: one private one-product order per ticket line, never put in the table.
            public void PersistentCityTicket(params string[] lines)
            { foreach (var line in lines) { var e = new Entry(); e.Lines.Add(line); Process(e, own: true); } }

            // A native entry still in the table at an owned shop is offered to ProcessCustomer directly (another caller).
            public void StrayNativeCalls() { foreach (var e in Table.ToArray()) Process(e, own: false); }
        }

        private static ShopHour OwnedHour(bool living)
        {
            var shop = new ShopHour();
            shop.Generate(4, living ? 2.5 : 1.0); // entries drawn before the acquisition boundary stay in the table
            shop.Owned = true;
            shop.Generate(6, living ? 2.5 : 1.0);
            if (living) { shop.RloBasket("loyalty-extra"); shop.RloBasket("interiors-extra"); }
            shop.NativeBatch();
            shop.StrayNativeCalls();
            shop.PersistentCityTicket("burger", "fries");
            shop.PersistentCityTicket("soda");
            return shop;
        }

        [Fact]
        public void LivingOnOwnedShopSellsOnlyPersistentCityTicketsWithNoRloDemandOrBasketLines()
        {
            var shop = OwnedHour(living: true);
            Assert.Equal(3, shop.Served);
            Assert.Equal(new[] { "burger", "fries", "soda" }, shop.Sold);
            Assert.Empty(shop.Draw());
            // The pre-acquisition entries RLO extended are never sold while owned.
            Assert.All(shop.Table, e => Assert.False(e.Completed));
            Assert.Null(RloOverrule.CheckoutRefusal(null, new RloLivingReading(RloLivingState.On)));

            // A native-owned shop stays RLO's and the game's: the scaled draw and the basket lines sell there.
            var native = new ShopHour();
            native.Generate(4, 2.5); native.RloBasket("loyalty-extra"); native.NativeBatch();
            Assert.Equal(10, native.Served);
            Assert.Equal(10, native.Sold.Count(l => l == "loyalty-extra"));
        }

        [Fact]
        public void LivingOffOwnedShopStillSellsAndNeedsNoRloDemand()
        {
            var shop = OwnedHour(living: false);
            Assert.Equal(3, shop.Served);
            Assert.Equal(new[] { "burger", "fries", "soda" }, shop.Sold);
            Assert.Null(RloOverrule.CheckoutRefusal(null, new RloLivingReading(RloLivingState.Off)));
            Assert.Null(RloOverrule.CheckoutRefusal(null, new RloLivingReading(RloLivingState.NotInRelease)));
            Assert.Null(RloOverrule.CheckoutRefusal(null, new RloLivingReading(RloLivingState.Absent)));
        }

        [Fact]
        public void MidSessionLivingToggleIsPickedUpOnTheCoarseCadence()
        {
            var rlo = StandIn.Build("1.0.0", living: true);
            var field = rlo.GetType("RealLifeOverhaul.Settings")!.GetField("LivingPopulation")!;
            field.SetValue(null, true);
            var binding = RloLivingPopulation.Bind(rlo);
            Assert.True(binding.Bound);
            var tracker = new RloLivingTracker(binding.Read);
            Assert.Equal(RloLivingState.On, tracker.Current.State);

            Assert.False(tracker.Poll(0.0));
            field.SetValue(null, false); // RLO's options toggle assigns the field during play
            Assert.False(tracker.Poll(0.5)); // within the cadence: not read again
            Assert.Equal(RloLivingState.On, tracker.Current.State);
            Assert.True(tracker.Poll(1.0));
            Assert.Equal(RloLivingState.Off, tracker.Current.State);
            field.SetValue(null, true);
            Assert.True(tracker.Poll(2.5));
            Assert.Equal(RloLivingState.On, tracker.Current.State);
            Assert.Equal(2, tracker.Toggles);
            Assert.Equal(new[] { "Living population on -> Living population off", "Living population off -> Living population on" }, tracker.Changes);

            // The owned shop is the same in every hour of the session, whatever the switch says.
            foreach (bool on in new[] { true, false, true })
            {
                var shop = OwnedHour(on);
                Assert.Equal(new[] { "burger", "fries", "soda" }, shop.Sold);
            }
            Assert.Null(RloOverrule.CheckoutRefusal(null, tracker.Current));
            Assert.Contains("2 toggle(s) this session", RloOverrule.Describe(tracker.Current, tracker.Toggles, 5, null, true, 3, true));
        }

        [Theory]
        [InlineData("missing field", "missing public static field RealLifeOverhaul.Settings.LivingPopulation")]
        [InlineData("int field", "is Int32, not Boolean")]
        [InlineData("readonly field", "cannot change during play")]
        [InlineData("instance class", "is not a public static class")]
        [InlineData("no settings", "missing type RealLifeOverhaul.Settings")]
        public void UnexpectedLivingSurfaceFailsSafeAndStartsNoOwnedCheckout(string shape, string reason)
        {
            var reading = RloLivingPopulation.Bind(LivingShape(shape)).Read();
            Assert.Equal(RloLivingState.Unknown, reading.State);
            Assert.Contains(reason, reading.Reason);
            string? refusal = RloOverrule.CheckoutRefusal(null, reading);
            Assert.NotNull(refusal);
            Assert.Contains("unknown", refusal);
            Assert.Contains("food.checkout.shop off", RloOverrule.Describe(reading, 0, 0, refusal, true, 0, true));
        }

        [Fact]
        public void ReleasesWithoutALivingPopulationAndMissingRloAreNotUnknown()
        {
            Assert.Equal(RloLivingState.NotInRelease, RloLivingPopulation.Bind(LivingShape("old release")).Read().State);
            Assert.Equal(RloLivingState.Absent, RloLivingPopulation.Bind(null).Read().State);
            Assert.Equal(RloLivingState.Absent, RloLivingPopulation.Bind(typeof(RloOverruleTests).Assembly).Read().State);
        }

        [Fact]
        public void UnknownIsStickyAndAFailedReadIsUnknownNotOff()
        {
            var answers = new Queue<Func<RloLivingReading>>(new Func<RloLivingReading>[]
            {
                () => new RloLivingReading(RloLivingState.On),
                () => throw new InvalidOperationException("surface gone"),
                () => new RloLivingReading(RloLivingState.Off),
            });
            var tracker = new RloLivingTracker(() => answers.Dequeue()());
            Assert.Equal(RloLivingState.On, tracker.Current.State);
            Assert.True(tracker.Poll(0));
            Assert.Equal(RloLivingState.Unknown, tracker.Current.State);
            Assert.Contains("surface gone", tracker.Current.Reason);
            Assert.False(tracker.Poll(5)); // a readable answer later does not lift Unknown this session
            Assert.Equal(RloLivingState.Unknown, tracker.Current.State);
            Assert.Equal(0, tracker.Toggles);
            Assert.NotNull(RloOverrule.CheckoutRefusal(null, tracker.Current));
        }

        [Fact]
        public void ContractOneCityApiStillBindsBesideTheLivingSurfaceAndAnotherVersionIsRefused()
        {
            var rlo = StandIn.Build("1.0.0", living: true);
            var bridge = RloBridge.Find(rlo);
            Assert.NotNull(bridge);
            Assert.Equal(1, bridge!.ContractVersion);
            Assert.True(RloLivingPopulation.Bind(rlo).Bound);

            var error = Assert.Throws<InvalidOperationException>(() => RloBridge.Find(StandIn.Build("1.0.0", contract: 2, living: true)));
            Assert.Contains("contract 2 is not supported", error.Message);
        }

        private static HookPatchedMethod Method(string type, string method, string[] parameters, params (string Owner, HookPatchKind Kind)[] patches)
            => new HookPatchedMethod(type, method, parameters, patches.Select(p => new HookPatch(p.Owner, p.Kind)).ToArray());

        // RLO 1.0's PopulationGame.InstallCalculators: five factory fields given its own subclasses.
        private static readonly HookReplacement[] RloCalculators = new[]
        {
            ("CustomerEntriesCalculator", "PlainCalculator"), ("CustomerEntriesCalculatorRetail", "RetailCalculator"),
            ("CustomerEntriesCalculatorOffice", "OfficeCalculator"), ("CustomerEntriesCalculatorCasino", "CasinoCalculator"),
            ("CustomerEntriesCalculatorCinemaTheater", "CinemaCalculator"),
        }.Select(c => new HookReplacement("CustomerEntriesCalculatorFactory." + c.Item1, c.Item1,
            "RealLifeOverhaul.Population.PopulationGame+" + c.Item2, "RealLifeOverhaul")).ToArray();

        private static HookPatchedMethod[] OwnHooks(params (string Owner, HookPatchKind Kind)[] foreignOnProcessCustomer) => new[]
        {
            Method("RetailBusinessSimulator", "ProcessAllCustomersFromThisHour", Array.Empty<string>(), (NativeHookInventory.FoodAuthority, HookPatchKind.Prefix)),
            Method("RetailBusinessSimulator", "ProcessCustomer", new[] { "CustomerEntry" },
                new[] { (NativeHookInventory.FoodAuthority, HookPatchKind.Prefix) }.Concat(foreignOnProcessCustomer).ToArray()),
            Method("CustomerEntriesHelper", "UpdateCustomerEntriesForPlayerBusiness", new[] { "BuildingRegistration", "DayOfWeekOrdered" },
                (NativeHookInventory.FoodAuthority, HookPatchKind.Prefix)),
        };

        private static readonly string[] Policy =
        {
            "RetailBusinessSimulator.ProcessAllCustomersFromThisHour()", "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)",
            "CustomerEntriesHelper.GetEntriesByAddress(Address)",
            "CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration, DayOfWeekOrdered)",
            "BuildingRegistration.Reset(Boolean)", "CitizenData.IsPriceAcceptable(String, Single)",
        };

        [Fact]
        public void F8hSeesCalculatorReplacementAndDoesNotRefuseCheckoutForIt()
        {
            var report = HookConflictReport.Build(OwnHooks(), Policy, RloCalculators);
            Assert.Equal(5, report.Replacements.Count);
            Assert.Null(report.CheckoutRefusal);
            Assert.Null(RloOverrule.CheckoutRefusal(report.CheckoutRefusal, new RloLivingReading(RloLivingState.On)));
            Assert.Contains("CustomerEntriesCalculatorFactory.CustomerEntriesCalculatorRetail [non-Harmony]: CustomerEntriesCalculatorRetail replaced by RealLifeOverhaul "
                + "RealLifeOverhaul.Population.PopulationGame+RetailCalculator -> overruled at owned food shops, does not refuse food.checkout.shop", report.Text());
            Assert.Contains("0 refusing food.checkout.shop", report.Text());
            Assert.Contains("5 native object(s) replaced without Harmony by RealLifeOverhaul, overruled at owned food shops.", report.ActivationLine());
            Assert.DoesNotContain("food.checkout.shop off", report.ActivationLine());
        }

        [Fact]
        public void AForeignHarmonyPrefixOnAnH1TargetStillRefusesBesideRlo()
        {
            var report = HookConflictReport.Build(OwnHooks(("other.mod", HookPatchKind.Prefix)), Policy, RloCalculators);
            Assert.Equal("foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)", report.CheckoutRefusal);
            Assert.Equal(report.CheckoutRefusal, RloOverrule.CheckoutRefusal(report.CheckoutRefusal, new RloLivingReading(RloLivingState.On)));
            Assert.Equal(report.CheckoutRefusal, RloOverrule.CheckoutRefusal(report.CheckoutRefusal, new RloLivingReading(RloLivingState.Unknown, "x")));
            Assert.Contains("food.checkout.shop off: foreign prefix by other.mod", report.ActivationLine());
            // A foreign observer stays an observer, with or without RLO's replacements.
            Assert.Null(HookConflictReport.Build(OwnHooks(("other.mod", HookPatchKind.Postfix)), Policy, RloCalculators).CheckoutRefusal);
        }

        [Fact]
        public void TheOverruleStatusNamesEveryAreaAndWhatStaysRlos()
        {
            string text = RloOverrule.Describe(new RloLivingReading(RloLivingState.On), 0, 5, null, true, 4, true, 2);
            Assert.Contains("RLO accident switches not bound", RloOverrule.Describe(new RloLivingReading(RloLivingState.On), 0, 5, null, true, 4, true, 0));
            Assert.Contains("owned food shops take no native draw, batch or customer", text);
            Assert.Contains("5 replaced customer calculator(s) overruled at owned shops", text);
            Assert.Contains("owned checkout sells private orders only", text);
            Assert.Contains("books no native rent, wage or tax", text);
            Assert.Contains("4 of them RLO's; RLO's own scene people kept", text);
            Assert.Contains("RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired", text);
            Assert.Equal("Persistent City RLO overrule: RLO not loaded\nnothing to overrule\n",
                RloOverrule.Describe(new RloLivingReading(RloLivingState.Absent), 0, 0, null, false, 0, false));
        }

        [Fact]
        public void AnUnknownSwitchDuringPlayHoldsNewTakeoversButKeepsOwnedShopsAndHandBacks()
        {
            var answers = new Queue<RloLivingReading>(new[] { new RloLivingReading(RloLivingState.On), new RloLivingReading(RloLivingState.Unknown, "surface gone") });
            var tracker = new RloLivingTracker(() => answers.Dequeue());
            Assert.False(RloOverrule.HoldsAcquisitions(tracker.Current.State));
            var owned = new FoodAuthority { PlaceId = "place:a:shop" };
            owned.Observe("sig-a", true, 10);
            Assert.True(owned.Boundary(11, true, false, true, !RloOverrule.HoldsAcquisitions(tracker.Current.State)));
            var leaving = new FoodAuthority { PlaceId = "place:c:shop" };
            leaving.Observe("sig-c", true, 10);
            Assert.True(leaving.Boundary(11, true, false, true, true));

            Assert.True(tracker.Poll(20)); // the switch turns unreadable during play
            bool hold = RloOverrule.HoldsAcquisitions(tracker.Current.State);
            Assert.True(hold);

            // A shop becoming eligible now is not taken over at the next boundary, nor later this session.
            var fresh = new FoodAuthority { PlaceId = "place:b:shop" };
            fresh.Observe("sig-b", true, 21);
            Assert.Equal(FoodBoundaryStep.Acquire, fresh.Pending(22, true, false, true));
            Assert.False(fresh.Boundary(22, true, false, true, !hold));
            Assert.False(fresh.Boundary(40, true, false, true, !RloOverrule.HoldsAcquisitions(tracker.Current.State)));
            Assert.False(fresh.Managed);
            Assert.True(fresh.Deferred);

            // The shop owned before keeps its hours and its own checkout; a hand-back never waits.
            Assert.True(owned.OwnsHour(22));
            Assert.True(RloOverrule.AdmitsNative(NativeShopPath.Process, owned.OwnsHour(22), ownCheckout: true));
            Assert.False(RloOverrule.AdmitsNative(NativeShopPath.Generation, owned.OwnsHour(22)));
            leaving.Observe("sig-c", false, 22);
            Assert.True(leaving.Boundary(23, true, false, true, !hold));
            Assert.False(leaving.Managed);
        }

        [Fact]
        public void TrafficSceneSwitchesAreHeldOffWhileOurCarsHoldTrafficAndGivenBack()
        {
            var rlo = LivingShape("scenes");
            var settings = rlo.GetType("RealLifeOverhaul.Settings")!;
            var accidents = settings.GetField("TrafficAccidents")!; var reckless = settings.GetField("NaturalAccidents")!;
            accidents.SetValue(null, true); reckless.SetValue(null, false); // the player has reckless drivers off
            var scenes = RloTrafficScenes.Bind(rlo);
            Assert.Empty(scenes.Unbound);

            Assert.Equal(2, scenes.Hold());
            SceneHooks.Ended = 0;
            Assert.True(scenes.EndActiveScene()); // an accident already under way when the hold starts is ended
            Assert.Equal(1, SceneHooks.Ended);
            Assert.True(scenes.Holding);
            Assert.False((bool)accidents.GetValue(null)!); // a staged accident under way gets no successor; a reckless drive ends
            Assert.False((bool)reckless.GetValue(null)!);

            reckless.SetValue(null, true); // the player switches reckless drivers on during the hold
            scenes.Hold();                  // the next coarse poll
            Assert.False((bool)reckless.GetValue(null)!);

            scenes.Release();
            Assert.False(scenes.Holding);
            Assert.True((bool)accidents.GetValue(null)!);
            Assert.True((bool)reckless.GetValue(null)!); // the player's latest choice is the one given back

            var partial = RloTrafficScenes.Bind(LivingShape("scenes without reckless"));
            Assert.Single(partial.Holds);
            Assert.Contains(partial.Unbound, u => u.StartsWith("NaturalAccidents", StringComparison.Ordinal));
            Assert.Empty(RloTrafficScenes.Bind(null).Holds);
        }

        [Fact]
        public void AnOptionThePlayerSwitchesOffDuringTheHoldStaysOffAfterRelease()
        {
            var rlo = LivingShape("scenes");
            var settings = rlo.GetType("RealLifeOverhaul.Settings")!;
            var accidents = settings.GetField("TrafficAccidents")!; var reckless = settings.GetField("NaturalAccidents")!;
            accidents.SetValue(null, true); reckless.SetValue(null, true);
            var prefs = new Dictionary<string, int> { ["m:rlo:traffic_accidents"] = 1, ["m:rlo:natural_accidents"] = 1 };
            var scenes = RloTrafficScenes.Bind(rlo, key => prefs.TryGetValue(key, out int v) ? v : (int?)null);
            scenes.Hold();
            Assert.False((bool)accidents.GetValue(null)!);

            // The player saves accidents off during the hold: RLO's callback assigns false (already false) and stores 0.
            prefs["m:rlo:traffic_accidents"] = 0;
            scenes.Hold();
            scenes.Release();
            Assert.False((bool)accidents.GetValue(null)!); // the stored choice wins over the value seen at the hold
            Assert.True((bool)reckless.GetValue(null)!);

            // Without a stored value the value recorded at the hold is given back.
            accidents.SetValue(null, true); prefs.Clear();
            scenes.Hold(); scenes.Release();
            Assert.True((bool)accidents.GetValue(null)!);
        }

        [Fact]
        public void ACarLoadedPastTheHeldSharesIsRetiredOutOfViewAndRloSceneCarsStay()
        {
            // RLO's rival pass-by: TrafficManager.LoadVehicle of an idle pool car, out of view, on a lane of a held group.
            Assert.True(RloOverrule.RetiresNativeCar(active: true, heldGroup: true, presetPath: false, ignored: false, ordinary: true, taxi: false, visible: false));
            Assert.False(RloOverrule.RetiresNativeCar(true, true, false, false, true, false, visible: true));   // never popped in view
            Assert.False(RloOverrule.RetiresNativeCar(true, true, presetPath: true, false, true, false, false)); // a staged accident's wreck or unit
            Assert.False(RloOverrule.RetiresNativeCar(true, true, false, ignored: true, true, false, false));    // a car RLO holds out of traffic
            Assert.False(RloOverrule.RetiresNativeCar(true, heldGroup: false, false, false, true, false, false)); // a group Persistent City does not replace
            Assert.False(RloOverrule.RetiresNativeCar(true, true, false, false, ordinary: false, false, false)); // a modded or service car
            Assert.False(RloOverrule.RetiresNativeCar(true, true, false, false, true, taxi: true, false));
            Assert.False(RloOverrule.RetiresNativeCar(active: false, true, false, false, true, false, false));
        }

        // One native pool slot as PhysicalVehicles reads it.
        private sealed class PoolCar { public bool Active, Held = true, Preset, Ignored, Ordinary = true, Taxi, Visible; public int FramesDriven; }

        private static bool ActiveHeld(PoolCar c) => c.Active && c.Held;

        // The adapter in Unity's frame order. Update: other mods' Update, then ours (one slot a frame, the whole pool every 30
        // frames, out of view only; a slot we retire is forgotten); then coroutines (RLO's SeenDirector.Tick); then LateUpdate
        // (NativeLoadWatch: an active car of a held group that is no leftover is retired before it renders).
        private static void Frame(int frame, List<PoolCar> pool, NativeLoadWatch watch, ref int cursor,
            Action? otherUpdates = null, Action? coroutines = null)
        {
            otherUpdates?.Invoke();
            void Retire(int i)
            {
                var c = pool[i];
                if (RloOverrule.RetiresNativeCar(c.Active, c.Held, c.Preset, c.Ignored, c.Ordinary, c.Taxi, c.Visible)) { c.Active = false; watch.Forget(i); }
            }
            if (frame % 30 == 0) for (int i = 0; i < pool.Count; i++) Retire(i);
            else if (pool.Count > 0) Retire(cursor++ % pool.Count);
            coroutines?.Invoke();
            for (int i = 0; i < pool.Count; i++)
            {
                var c = pool[i];
                if (!watch.IsLoad(i, ActiveHeld(c))) continue;
                if (RloOverrule.RetiresNewNativeLoad(c.Held, c.Preset, c.Ignored, c.Ordinary, c.Taxi)) { c.Active = false; watch.Forget(i); }
                else watch.Accept(i);
            }
            foreach (var c in pool) if (c.Active) c.FramesDriven++; // rendering and the next traffic step see only what is left
        }

        private static NativeLoadWatch Hold(List<PoolCar> pool)
        {
            var watch = new NativeLoadWatch();
            watch.Prime(pool.Count, i => ActiveHeld(pool[i]));
            return watch;
        }

        [Fact]
        public void ACarLoadedJustAfterASweepIsRetiredBeforeItRendersEvenWhenItComesIntoView()
        {
            var pool = Enumerable.Range(0, 40).Select(_ => new PoolCar()).ToList();
            var watch = Hold(pool);
            int cursor = 0;
            for (int f = 1; f <= 30; f++) Frame(f, pool, watch, ref cursor); // frame 30 is a sweep

            // Frame 31, right after the sweep and away from the cursor: the pass-by loads slot 37 and the camera turns to it.
            Frame(31, pool, watch, ref cursor, () => { pool[37].Active = true; pool[37].Visible = true; });
            Assert.False(pool[37].Active);
            Assert.Equal(0, pool[37].FramesDriven);

            // The pool grows for a new car loaded straight into a held group: the new slot is caught too.
            Frame(32, pool, watch, ref cursor, () => pool.Add(new PoolCar { Active = true, Visible = true }));
            Assert.False(pool[40].Active);

            // Loads that are not ours to refuse stay: an RLO scene car (preset path), a car RLO holds, a group we do not
            // replace, a taxi.
            Frame(33, pool, watch, ref cursor, () =>
            {
                pool[1].Active = pool[1].Preset = true; pool[2].Active = pool[2].Ignored = true;
                pool[3].Active = true; pool[3].Held = false; pool[4].Active = pool[4].Taxi = true;
            });
            Assert.True(pool[1].Active && pool[2].Active && pool[3].Active && pool[4].Active);
            for (int f = 34; f <= 70; f++) Frame(f, pool, watch, ref cursor);
            Assert.True(pool[1].Active && pool[2].Active && pool[3].Active && pool[4].Active);
        }

        [Fact]
        public void ASlotRetiredAndReloadedBetweenTwoPassesIsStillCaught()
        {
            // Slot 5 holds a leftover, active and out of view when the hold starts.
            var pool = Enumerable.Range(0, 40).Select(_ => new PoolCar()).ToList();
            pool[5].Active = true;
            var watch = Hold(pool);
            int cursor = 0;
            for (int f = 1; f <= 29; f++) Frame(f, pool, watch, ref cursor, () => { if (cursor % 40 == 5) cursor++; }); // the cursor skips slot 5
            Assert.True(pool[5].Active); // still a leftover; LateUpdate leaves it to the out-of-view step

            // Frame 30: our sweep retires slot 5, then RLO's coroutine reloads that idle slot and it comes into view.
            // Both LateUpdate passes see slot 5 active; it is still a load.
            Frame(30, pool, watch, ref cursor, coroutines: () => { Assert.False(pool[5].Active); pool[5].Active = true; pool[5].Visible = true; });
            Assert.False(pool[5].Active);

            // Without our retirement in between, native drops the leftover (seen inactive) and RLO reloads the slot later.
            pool[6].Active = true; var second = Hold(pool);
            Frame(31, pool, second, ref cursor, () => pool[6].Active = false);
            Frame(32, pool, second, ref cursor, () => { pool[6].Active = true; pool[6].Visible = true; });
            Assert.False(pool[6].Active);
        }

        [Fact]
        public void ACarLoadedInTheHoldsOwnFrameIsCaughtAtTheFirstLateUpdate()
        {
            var pool = Enumerable.Range(0, 10).Select(_ => new PoolCar()).ToList();
            pool[0].Active = true; // a leftover
            var watch = Hold(pool); // PhysicalVehicles.Activate, inside this frame's Update
            int cursor = 1;
            // Later in the same frame RLO's coroutine loads slot 3 in view; then the first LateUpdate.
            Frame(1, pool, watch, ref cursor, coroutines: () => { pool[3].Active = true; pool[3].Visible = true; });
            Assert.False(pool[3].Active);
            Assert.True(pool[0].Active); // the leftover is left to the out-of-view step
            Assert.Throws<InvalidOperationException>(() => new NativeLoadWatch().IsLoad(0, true));
        }

        [Fact]
        public void NeedPurchaseClaimsTheNativeSlotButSellsNoRloBasketLine()
        {
            // A native entry at a native-owned shop: the game drew two lines, RLO's loyalty and interiors added two more.
            var claimed = new List<string> { "shampoo", "conditioner", "loyalty-extra", "interiors-extra" };
            const string own = "shampoo"; // NativeSalePath's own basket: one unit of the first priced product
            foreach (RloLivingState state in Enum.GetValues(typeof(RloLivingState)))
            {
                var basket = RloOverrule.QuotesClaimedLines(state) ? claimed : new List<string> { own };
                if (state == RloLivingState.Absent) Assert.Equal(claimed, basket);
                else Assert.DoesNotContain(basket, line => line.EndsWith("-extra", StringComparison.Ordinal));
            }
        }

        /// <summary>An assembly named RealLifeOverhaul whose Settings has one shape of the Living population switch.</summary>
        private static Assembly LivingShape(string shape)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("RealLifeOverhaul"), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("RealLifeOverhaul");
            if (shape != "old release")
                module.DefineType("RealLifeOverhaul.Population.PopulationDirector", TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class).CreateType();
            if (shape == "no settings") return assembly;
            var attributes = shape == "instance class" ? TypeAttributes.Public | TypeAttributes.Class : TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class;
            var settings = module.DefineType("RealLifeOverhaul.Settings", attributes);
            if (shape == "int field") settings.DefineField("LivingPopulation", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
            else if (shape == "readonly field") settings.DefineField("LivingPopulation", typeof(bool), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);
            else if (shape == "instance class") settings.DefineField("LivingPopulation", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
            else if (shape.StartsWith("scenes", StringComparison.Ordinal))
            {
                settings.DefineField("LivingPopulation", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
                settings.DefineField("TrafficAccidents", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
                if (shape == "scenes") settings.DefineField("NaturalAccidents", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
                // RLO's option ids and Settings.PrefsKey(id), "m:" + mod id + ":" + id.
                foreach (var (name, id) in new[] { ("TrafficAccidentsId", "traffic_accidents"), ("NaturalAccidentsId", "natural_accidents") })
                    settings.DefineField(name, typeof(string), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault).SetConstant(id);
                var key = settings.DefineMethod("PrefsKey", MethodAttributes.Public | MethodAttributes.Static, typeof(string), new[] { typeof(string) });
                var il = key.GetILGenerator();
                il.Emit(OpCodes.Ldstr, "m:rlo:"); il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) })!); il.Emit(OpCodes.Ret);
                // TrafficAccidents.EndActive(): counted in SceneHooks.
                var accidents = module.DefineType("RealLifeOverhaul.TrafficAccidents", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
                var end = accidents.DefineMethod("EndActive", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes).GetILGenerator();
                end.Emit(OpCodes.Call, typeof(SceneHooks).GetMethod(nameof(SceneHooks.EndActive))!); end.Emit(OpCodes.Ret);
                accidents.CreateType();
            }
            else if (shape != "missing field" && shape != "old release") throw new ArgumentException(shape);
            settings.CreateType();
            return assembly;
        }
    }

    /// <summary>What the emitted stand-in TrafficAccidents.EndActive reports to. Public so the emitted assembly can call it.</summary>
    public static class SceneHooks
    {
        public static int Ended;
        public static void EndActive() => Ended++;
    }
}
