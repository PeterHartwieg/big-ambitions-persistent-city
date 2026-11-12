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
            string text = RloOverrule.Describe(new RloLivingReading(RloLivingState.On), 0, 5, null, true, 4, true);
            Assert.Contains("owned food shops take no native draw, batch or customer", text);
            Assert.Contains("5 replaced customer calculator(s) overruled at owned shops", text);
            Assert.Contains("owned checkout sells private orders only", text);
            Assert.Contains("books no native rent, wage or tax", text);
            Assert.Contains("4 of them RLO's; RLO's own scene people kept", text);
            Assert.Contains("cars RLO holds are never retired", text);
            Assert.Equal("Persistent City RLO overrule: RLO not loaded\nnothing to overrule\n",
                RloOverrule.Describe(new RloLivingReading(RloLivingState.Absent), 0, 0, null, false, 0, false));
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
            else if (shape != "missing field" && shape != "old release") throw new ArgumentException(shape);
            settings.CreateType();
            return assembly;
        }
    }
}
