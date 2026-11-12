using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests
{
    [Collection(F8aSerial.Collection)]
    public sealed class NativeFoodContractTests
    {
        private readonly ITestOutputHelper output;
        public NativeFoodContractTests(ITestOutputHelper output) => this.output = output;

        internal static NativeFoodTypes Good() => new NativeFoodTypes
        {
            GameManager = typeof(F8cStandIn.GameManager), RetailBusinessSimulator = typeof(F8cStandIn.RetailBusinessSimulator),
            BusinessSimulator = typeof(F8cStandIn.BusinessSimulator), CustomerEntriesHelper = typeof(F8cStandIn.CustomerEntriesHelper),
            CustomerEntry = typeof(F8cStandIn.CustomerEntry), BuildingRegistration = typeof(F8cStandIn.BuildingRegistration),
            Address = typeof(F8cStandIn.Address), DayOfWeek = typeof(F8cStandIn.DayOfWeekOrdered), CitizenData = typeof(F8cStandIn.CitizenData),
            ItemInstance = typeof(F8cStandIn.ItemInstance), PointOfSale = typeof(F8cStandIn.PointOfSale),
        };

        [Fact]
        public void TheInstalledShapeVerifies()
        {
            Assert.Null(NativeMemberContract.CheckAll(NativeFoodContractSpec.Authority(Good()), out var authority));
            Assert.All(authority, Assert.NotNull);
            Assert.Equal(NativeFoodContractSpec.AuthorityTargets + 2, authority.Length);
            Assert.Null(NativeMemberContract.CheckAll(NativeFoodContractSpec.Checkout(Good()), out var checkout));
            Assert.All(checkout, Assert.NotNull);
            Assert.Equal(typeof(F8cStandIn.BusinessSimulator), authority[6]!.DeclaringType);
            var inventory = NativeHookInventory.Targets.Where(t => t.Owner == NativeHookInventory.FoodAuthority).ToList();
            Assert.Equal(NativeFoodContractSpec.AuthorityTargets, inventory.Count);
            var specs = NativeFoodContractSpec.Authority(Good());
            for (int i = 0; i < inventory.Count; i++)
            {
                Assert.Equal(specs[i].Name, inventory[i].Method);
                Assert.Equal(specs[i].Parameters.Select(p => p.Name), inventory[i].Parameters!);
            }
        }

        public static IEnumerable<object[]> AuthorityVariants()
        {
            object[] V(string label, Action<NativeFoodTypes> change, string expected) => new object[] { label, change, expected };
            yield return V("tick missing", t => t.GameManager = typeof(F8cStandIn.Missing.GameManager), "GameManager.RunMainGameTick(Single) is missing.");
            yield return V("tick other parameters", t => t.GameManager = typeof(F8cStandIn.OtherParameters.GameManager), "GameManager.RunMainGameTick(Single) changed: no method with this parameter list (found RunMainGameTick(Double))");
            yield return V("tick re-typed", t => t.GameManager = typeof(F8cStandIn.Retyped.GameManager), "GameManager.RunMainGameTick(Single) changed: returns Boolean");
            yield return V("hourly renamed", t => t.RetailBusinessSimulator = typeof(F8cStandIn.Renamed.RetailBusinessSimulator), "RetailBusinessSimulator.ProcessAllCustomersFromThisHour() is missing.");
            yield return V("customer other parameters", t => t.RetailBusinessSimulator = typeof(F8cStandIn.OtherParameters.RetailBusinessSimulator), "RetailBusinessSimulator.ProcessCustomer(CustomerEntry) changed: no method with this parameter list (found ProcessCustomer(CustomerEntry, Int32))");
            yield return V("draw re-typed", t => t.CustomerEntriesHelper = typeof(F8cStandIn.Retyped.CustomerEntriesHelper), "CustomerEntriesHelper.GetEntriesByAddress(Address) changed: returns CustomerEntry[]");
            yield return V("generate became instance", t => t.CustomerEntriesHelper = typeof(F8cStandIn.Instance.CustomerEntriesHelper), "CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration, DayOfWeekOrdered) changed: instance method.");
            yield return V("reset other parameters", t => t.BuildingRegistration = typeof(F8cStandIn.OtherParameters.BuildingRegistration), "BuildingRegistration.Reset(Boolean) changed: no method with this parameter list (found Reset())");
            yield return V("registration field renamed", t => t.BusinessSimulator = typeof(F8cStandIn.Renamed.BusinessSimulator), "BusinessSimulator.buildingRegistration is missing.");
            yield return V("hour field re-typed", t => t.BusinessSimulator = typeof(F8cStandIn.Retyped.BusinessSimulator), "BusinessSimulator.currentHour changed: field type Int64, expected Int32.");
        }

        [Theory]
        [MemberData(nameof(AuthorityVariants))]
        public void EachChangedAuthorityMemberGivesANamedStartRefusal(string arm, Action<NativeFoodTypes> change, string expected)
        {
            var types = Good(); change(types);
            var specs = NativeFoodContractSpec.Authority(types);
            Assert.NotNull(NativeMemberContract.CheckAll(specs, out var resolved));
            Assert.Contains(resolved, m => m == null);
            string? refusal = specs.Select(s => NativeMemberContract.Check(s, out _)).FirstOrDefault(r => r != null && r.StartsWith(expected, StringComparison.Ordinal));
            Assert.True(refusal != null, arm + ": " + string.Join(" | ", specs.Select(s => NativeMemberContract.Check(s, out _))));
            string message = NativeFoodContractSpec.AuthorityStartMessage(refusal!);
            Assert.StartsWith("Persistent City did not start: food.authority is off: " + expected, message);
            Assert.Contains("not a damaged save and not an older city", message);
            Assert.Contains("The saved city is kept unchanged.", message);
            var inputs = CapabilityReportTests.Healthy(); inputs.FoodAuthorityRefusal = refusal; inputs.FoodAuthorityInstalled = false;
            var registry = CapabilitySnapshot.Build(inputs);
            Assert.Equal(CapabilityStartStatus.Refused, registry.Start().Status);
            Assert.Equal(CapabilityId.FoodAuthority, registry.Start().Capability);
        }

        public static IEnumerable<object[]> CheckoutVariants()
        {
            object[] V(Action<NativeFoodTypes> change, int index, string expected) => new object[] { change, index, expected };
            yield return V(t => t.CitizenData = typeof(F8cStandIn.Retyped.CitizenData), NativeFoodContractSpec.PriceCheck, "CitizenData.IsPriceAcceptable(String, Single) changed: returns Void");
            yield return V(t => t.RetailBusinessSimulator = typeof(F8cStandIn.OtherParameters.RetailBusinessSimulator), NativeFoodContractSpec.ProcessCustomer, "RetailBusinessSimulator.ProcessCustomer(CustomerEntry) changed");
            yield return V(t => t.RetailBusinessSimulator = typeof(F8cStandIn.Retyped.RetailBusinessSimulator), NativeFoodContractSpec.Producers, "RetailBusinessSimulator.availableProducers changed: field type");
            yield return V(t => t.PointOfSale = typeof(F8cStandIn.ItemInstance), NativeFoodContractSpec.Counters, "RetailBusinessSimulator.pointOfSales changed: field type");
            yield return V(t => t.RetailBusinessSimulator = typeof(F8cStandIn.Renamed.RetailBusinessSimulator), NativeFoodContractSpec.Capacity, "RetailBusinessSimulator.GetMaxCustomerCapacityThisHour() is missing.");
        }

        [Theory]
        [MemberData(nameof(CheckoutVariants))]
        public void EachChangedCheckoutMemberRefusesCheckoutOnly(Action<NativeFoodTypes> change, int index, string expected)
        {
            var types = Good(); change(types);
            Assert.Null(NativeMemberContract.CheckAll(NativeFoodContractSpec.Authority(types).Where(s => s.Declaring != types.RetailBusinessSimulator).ToArray(), out _));
            var specs = NativeFoodContractSpec.Checkout(types);
            string? refusal = NativeMemberContract.Check(specs[index], out var member);
            Assert.Null(member);
            Assert.True(refusal != null && refusal.StartsWith(expected, StringComparison.Ordinal), refusal);
            Assert.NotNull(NativeMemberContract.CheckAll(specs, out _));
            Assert.StartsWith("Excluded: food checkout contract off: ", NativeFoodContractSpec.CheckoutScopeReason(refusal!));
            var inputs = CapabilityReportTests.Healthy(); inputs.FoodCheckoutRefusal = refusal;
            var registry = CapabilitySnapshot.Build(inputs);
            Assert.Equal(CapabilityState.Off, registry.State(CapabilityId.FoodCheckoutShop));
            Assert.Equal(CapabilityStartStatus.Ready, registry.Start().Status);
            Assert.False(registry.IsEnabled(CapabilityScope.ShopOwnership));
        }

        [Fact]
        public void FaultPointShapesAreTheRenameAndSignatureVariants()
        {
            var specs = NativeFoodContractSpec.Checkout(Good());
            Assert.Equal("RetailBusinessSimulator.ProcessCustomerFault(CustomerEntry) is missing.",
                NativeMemberContract.Check(specs[NativeFoodContractSpec.ProcessCustomer].Renamed("ProcessCustomerFault"), out _));
            Assert.StartsWith("RetailBusinessSimulator.ProcessCustomer(CustomerEntry, Int32) changed: no method with this parameter list",
                NativeMemberContract.Check(specs[NativeFoodContractSpec.ProcessCustomer].WithExtraParameter(typeof(int)), out _));
        }


        private const string Place = "address:Main%20St:1:shop", Signature = "business:Main%20St:1:shop:fastfood";

        private static ResidentMeal Meal(string resident, string business) => new()
        {
            ResidentId = resident, Day = 4, Kind = MealKind.Lunch, BusinessId = business, ProductId = FoodProducts.HotDog,
            Price = Money.FromCents(825),
        };

        [Theory]
        [InlineData("RetailBusinessSimulator.ProcessCustomer(CustomerEntry) changed: no method with this parameter list")]
        [InlineData("foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)")]
        public void CheckoutOffWithdrawsAnOwnedShopAtTheNextBoundaryAndKeepsItsReceipts(string refusal)
        {
            const long Loaded = 4 * 24 + 12;
            var saved = new FoodAuthority { PlaceId = Place, Signature = Signature, Desired = true, RequestedHour = 50, FromHour = 51 };
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            var paid = Meal("paid", saved.BusinessId); FoodSaleReceipts.Begin(data, paid); FoodSaleReceipts.Confirm(data, paid);
            var uncertain = Meal("uncertain", saved.BusinessId); FoodSaleReceipts.Begin(data, uncertain); uncertain.Status = MealStatus.Uncertain;
            data[FoodAuthorityStore.Key] = FoodAuthorityStore.Write(new[] { saved });
            var receipts = data.Where(p => FoodSaleReceipts.IsReceiptKey(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();

            var row = FoodAuthorityStore.Read(data[FoodAuthorityStore.Key]).Single();
            Assert.True(row.CanServe && row.OwnsHour(Loaded));
            var mode = new FoodCheckoutMode(refusal);
            Assert.False(mode.Serves); Assert.False(mode.InstallsPricePrefix);
            Assert.StartsWith("Excluded: food checkout contract off: ", mode.ScopeReason);
            Assert.True(new FoodCheckoutMode(null).Observe(true));
            Assert.True(row.Observe(Signature, eligible: mode.Observe(true), Loaded));
            Assert.False(row.Desired); Assert.Equal(Loaded, row.RequestedHour);
            Assert.True(row.OwnsHour(Loaded)); Assert.False(row.CanServe);
            data[FoodAuthorityStore.Key] = FoodAuthorityStore.Write(new[] { row });
            Assert.False(row.Observe(Signature, mode.Observe(true), Loaded));

            row = FoodAuthorityStore.Read(data[FoodAuthorityStore.Key]).Single();
            Assert.False(row.Desired); Assert.Equal(Loaded, row.RequestedHour); Assert.True(row.OwnsHour(Loaded));
            Assert.Equal(FoodContractBoundary.Outcome.None, FoodContractBoundary.Commit(row, null, Loaded, false, true, -1, (_, _) => true));

            Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(data, paid));
            Assert.True(FoodSaleReceipts.AlreadyPaid(data, paid));
            Assert.Equal(FoodReceiptStatus.Pending, FoodSaleReceipts.ReadStatus(data, uncertain));
            Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(data, uncertain));
            Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(data, uncertain));

            Assert.Equal(FoodContractBoundary.Outcome.HandedBack, FoodContractBoundary.Commit(row, null, Loaded + 1, false, true, -1, (_, _) => throw new InvalidOperationException("no gate on a hand-back")));
            Assert.False(row.Managed); Assert.False(row.OwnsHour(Loaded + 1)); Assert.True(row.OwnsHour(Loaded));
            data[FoodAuthorityStore.Key] = FoodAuthorityStore.Write(new[] { row });
            row = FoodAuthorityStore.Read(data[FoodAuthorityStore.Key]).Single();
            Assert.False(row.Managed); Assert.Equal(Loaded + 1, row.UntilHour);
            Assert.False(row.Observe(Signature, mode.Observe(true), Loaded + 2));
            Assert.Equal(FoodContractBoundary.Outcome.None, FoodContractBoundary.Commit(row, null, Loaded + 3, false, true, -1, (_, _) => true));

            Assert.Equal(receipts, data.Where(p => FoodSaleReceipts.IsReceiptKey(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal).ToArray());
        }

        private sealed class CheckoutGateMarket : IFoodMarket, IFoodBusinessAuthority, IFoodStartupValidation
        {
            public readonly WeightedFoodChoiceTests.Market Inner;
            public CheckoutGateMarket(WeightedFoodChoiceTests.Market inner, bool hold) { Inner = inner; Hold = hold; }
            public readonly bool Hold;
            public bool Settled;
            public FoodCheckoutMode Mode = new FoodCheckoutMode(null);
            public int Attempts;
            public double EarliestMinute => 0;
            public IReadOnlyList<FoodBusiness> Read(double at) => Inner.Read(at);
            public bool CanServe(string businessId) => Settled && Mode.Serves;
            public bool AwaitingValidation(string businessId) => Hold && !Settled;
            public FoodSale Buy(ResidentMeal m, double at)
            {
                Attempts++;
                Assert.True(Settled);
                return Inner.Buy(m, at);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)")]
        public void SavedCommitmentWaitsForTheCheckoutDecision(string? refusal)
        {
            foreach (bool hold in new[] { false, true })
            {
                var (prepared, inner, planner) = WeightedFoodChoiceTests.Ready(12, 12);
                var market = new CheckoutGateMarket(inner, hold) { Settled = true };
                var city = new CitySimulator(prepared.State, planner); city.AttachFoodMarket(market, LegacyFoodChoice.Settings());
                var m = city.State.Food!.Residents[0]; double at = m.DesiredAt;
                for (int i = 0; i < 400 && m.Status != MealStatus.Travelling; i++) { at += .25; city.AdvanceTo(at, 1); m = city.State.Food!.Residents[0]; }
                Assert.Equal(MealStatus.Travelling, m.Status);
                string intent = m.IntentId;
                market.Settled = false;
                for (double t = at; t <= at + 6; t += .5) city.AdvanceTo(t, 1);
                m = city.State.Food!.Residents[0];
                if (!hold) { Assert.Equal(MealStatus.Missed, m.Status); continue; }
                Assert.True(m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving, m.Status.ToString());
                Assert.Equal(intent, m.IntentId); Assert.Equal(0, market.Attempts); Assert.Empty(inner.Orders);

                market.Mode = new FoodCheckoutMode(refusal); market.Settled = true;
                while (!city.AdvanceTo(at + 30, 1).CaughtUp) { }
                m = city.State.Food!.Residents[0];
                if (refusal == null)
                {
                    Assert.Equal(MealStatus.Paid, m.Status);
                    Assert.Equal(new[] { intent }, inner.Orders); Assert.Equal(1, market.Attempts);
                }
                else
                {
                    Assert.Equal(MealStatus.Missed, m.Status);
                    Assert.Equal(0, market.Attempts); Assert.Empty(inner.Orders);
                    Assert.Equal(Money.Zero, m.Spent);
                }
            }
        }

        private sealed class WalkRoutes : IRoutePlanner
        {
            public Route Plan(Place from, Place to, double at, TravelPreferences preferences) => new()
            { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = 5, Mode = TravelMode.Walk } } };
        }

        private sealed class CheckoutOffMarket : IFoodMarket, IFoodSaleEvidence, IFoodTicketEvidence
        {
            public int TicketReads, PaidReads;
            public FoodCheckoutMode Checkout = new FoodCheckoutMode(null);
            public readonly Dictionary<string, string> Receipts = new(StringComparer.Ordinal);
            public int LoseFromDay = int.MaxValue, Buys;
            public double EarliestMinute => 0;
            public bool IsConfirmedPaid(ResidentMeal meal) { PaidReads++; return PersistentCity.Runtime.FoodReceiptEvidence.IsConfirmedPaid(true, Receipts, meal); }
            public bool TryConfirmedTicket(ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras)
            { TicketReads++; return PersistentCity.Runtime.FoodReceiptEvidence.TryConfirmedTicket(true, Receipts, meal, out total, out extras); }
            public IReadOnlyList<FoodBusiness> Read(double minute) => !Checkout.Eligible ? Array.Empty<FoodBusiness>() : new[] { new FoodBusiness {
                Id = "b", PlaceId = "shop", Name = "Bakery", MealsPerHour = 60,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(450), Units = 1000 } } } };
            public FoodSale Buy(ResidentMeal meal, double minute)
            {
                Buys++;
                if (!Checkout.Serves) return new FoodSale(false, "Business is no longer owned by Persistent City");
                FoodSaleReceipts.Begin(Receipts, meal);
                bool late = int.Parse(Regex.Match(meal.ResidentId, @"\d+").Value, CultureInfo.InvariantCulture) % 2 == 0;
                if (meal.Day >= LoseFromDay && !late) throw new InvalidOperationException("outcome lost before the receipt was confirmed");
                FoodSaleReceipts.Confirm(Receipts, meal);
                if (meal.Day >= LoseFromDay) throw new InvalidOperationException("receipt confirmed; acknowledgement lost");
                return new FoodSale(true, "native order confirmed");
            }
        }

        [Fact]
        public void CheckoutOffReloadSettlesPaidReceiptsAndKeepsUncertainOnesHeld()
        {
            var state = Population.Create(Enumerable.Range(0, 60).Select(n => new CitizenIdentity { Id = "r" + n, Name = "Resident " + n, Persona = Persona.Foodie, Age = 30 }).ToArray(),
                new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 60 }, new Place { Id = "shop", Name = "Bakery", Kind = PlaceKind.Shop, Position = new(2, 0, 0) } },
                "checkout-off", "0.40.4", 7, 1440);
            foreach (var c in state.Citizens) { c.Phase = CitizenPhase.AtPlace; c.PlaceId = "home"; c.Activity = Activity.Home; c.NextEventMinute = c.ActivityUntilMinute = 2870; }
            state.Food = new FoodCommerceState { Shops = new() { new FoodScope { PlaceId = "shop", BusinessId = "b", ProductId = FoodProducts.Croissant } } };
            var market = new CheckoutOffMarket { LoseFromDay = 2 };
            var city = new CitySimulator(state, new WalkRoutes()); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
            foreach (var meal in city.State.Food!.Residents) meal.Budget!.HomeFoodFundedDay = -1;
            while (!city.AdvanceTo(3 * 1440 - 1, 128).CaughtUp) { }
            var uncertain = city.State.Food.Residents.Where(m => m.Status == MealStatus.Uncertain).ToArray();
            var paidEvidence = uncertain.Where(m => FoodSaleReceipts.ReadStatus(market.Receipts, m) == FoodReceiptStatus.Paid).Select(m => m.ResidentId).ToHashSet();
            var pendingEvidence = uncertain.Where(m => FoodSaleReceipts.ReadStatus(market.Receipts, m) == FoodReceiptStatus.Pending).Select(m => m.ResidentId).ToHashSet();
            Assert.NotEmpty(paidEvidence); Assert.NotEmpty(pendingEvidence);
            Assert.All(uncertain, m => Assert.True(m.Budget!.HeldPurchase > Money.Zero, "uncertain meals hold their funds before the reload"));

            var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal("", read.Error);
            var receipts = market.Receipts.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
            var off = new CheckoutOffMarket { Checkout = new FoodCheckoutMode("RetailBusinessSimulator.ProcessCustomer(CustomerEntry) changed") };
            foreach (var pair in receipts) off.Receipts.Add(pair.Key, pair.Value);
            var purchasesBefore = read.State!.Food!.Residents.ToDictionary(m => m.ResidentId, m => m.Purchases, StringComparer.Ordinal);
            var spentBefore = read.State.Food.Residents.ToDictionary(m => m.ResidentId, m => m.Spent, StringComparer.Ordinal);
            var priceOf = read.State.Food.Residents.ToDictionary(m => m.ResidentId, m => m.Price, StringComparer.Ordinal);
            city = new CitySimulator(read.State!, new WalkRoutes()); city.AttachFoodMarket(off);
            while (!city.AdvanceTo(4 * 1440 - 1, 128).CaughtUp) { }
            Assert.True(off.TicketReads > 0); Assert.Equal(0, off.PaidReads);
            var purchasesAfterReload = city.State.Food!.Residents.ToDictionary(m => m.ResidentId, m => m.Purchases, StringComparer.Ordinal);
            while (!city.AdvanceTo(5 * 1440 - 1, 128).CaughtUp) { }

            var residents = city.State.Food!.Residents;
            var settled = residents.Where(m => paidEvidence.Contains(m.ResidentId)).ToArray();
            var held = residents.Where(m => pendingEvidence.Contains(m.ResidentId)).ToArray();
            Assert.Equal(paidEvidence.Count, settled.Length); Assert.Equal(pendingEvidence.Count, held.Length);
            foreach (var m in settled)
            {
                Assert.Equal(purchasesBefore[m.ResidentId] + 1, m.Purchases);
                Assert.Equal(purchasesAfterReload[m.ResidentId], m.Purchases);
                Assert.Equal(spentBefore[m.ResidentId] + priceOf[m.ResidentId], m.Spent);
                Assert.NotEqual(MealStatus.Uncertain, m.Status);
                Assert.Equal(Money.Zero, m.Budget!.HeldPurchase);
                Assert.True(HouseholdFinance.CashConserved(m.Budget));
            }
            foreach (var m in held)
            {
                Assert.Equal(MealStatus.Uncertain, m.Status);
                Assert.True(m.Budget!.HeldPurchase > Money.Zero);
                Assert.True(HouseholdFinance.CashConserved(m.Budget));
            }
            Assert.Equal(0, off.Buys);
            Assert.Equal(receipts, off.Receipts.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray());
            Assert.Equal("", SaveCodec.Read(SaveCodec.Write(city.State)).Error);
            var any = city.State.Food.Residents.First(m => paidEvidence.Contains(m.ResidentId));
            Assert.False(PersistentCity.Runtime.FoodReceiptEvidence.TryConfirmedTicket(false, off.Receipts, any, out _, out _));
            Assert.False(PersistentCity.Runtime.FoodReceiptEvidence.IsConfirmedPaid(true, null, any));
        }

        [Fact]
        public void CheckoutOffRequestsNoNewOwnership()
        {
            var row = new FoodAuthority { PlaceId = Place };
            Assert.True(row.Observe(Signature, eligible: new FoodCheckoutMode("changed").Observe(true), 100));
            Assert.False(row.Desired); Assert.False(row.Managed);
            for (long hour = 101; hour < 110; hour++)
                Assert.Equal(FoodContractBoundary.Outcome.None, FoodContractBoundary.Commit(row, null, hour, false, true, -1, (_, _) => true));
            Assert.False(row.Managed); Assert.Equal(-1, row.FromHour);
        }



        [Fact]
        public void ContractCheckCost()
        {
            const int Warm = 200, Samples = 5_000;
            var types = Good();
            var times = new double[Samples];
            double tick = 1000.0 / Stopwatch.Frequency;
            for (int i = 0; i < Warm; i++) { NativeMemberContract.CheckAll(NativeFoodContractSpec.Authority(types), out _); NativeMemberContract.CheckAll(NativeFoodContractSpec.Checkout(types), out _); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Samples; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                Assert.Null(NativeMemberContract.CheckAll(NativeFoodContractSpec.Authority(types), out _));
                Assert.Null(NativeMemberContract.CheckAll(NativeFoodContractSpec.Checkout(types), out _));
                times[i] = (Stopwatch.GetTimestamp() - t0) * tick;
            }
            long bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / Samples;
            Array.Sort(times);
            string line = string.Format(CultureInfo.InvariantCulture, "F8c contract check (13 members, once per process): p50={0:0.000} ms p95={1:0.000} ms max={2:0.000} ms bytes/check={3}",
                times[Samples / 2], times[Samples * 95 / 100], times[^1], bytes);
            output.WriteLine(line);
            string? path = PerfGate.Variable("PC_F8_MEASURE");
            if (!string.IsNullOrEmpty(path)) File.AppendAllLines(path, new[] { line });
            PerfGate.Under(times[Samples * 95 / 100], 1, "F8c contract check p95");
        }
    }
}

#pragma warning disable CS0169, CS0414, CA1822, IDE0060
namespace PersistentCity.Tests.F8cStandIn
{
    public sealed class CustomerEntry { }
    public sealed class Address { }
    public enum DayOfWeekOrdered { Monday }
    public sealed class ItemInstance { }
    public sealed class PointOfSale { }
    public sealed class BuildingRegistration { public void Reset(bool resetFields) { } }
    public sealed class GameManager { public void RunMainGameTick(float deltaTimeWithMultiplier) { } }
    public class BusinessSimulator { protected BuildingRegistration buildingRegistration = null!; protected int currentHour; }
    public class RetailBusinessSimulator : BusinessSimulator
    {
        private Dictionary<string, List<ItemInstance>> availableProducers = new();
        private List<PointOfSale> pointOfSales = new();
        private void ProcessAllCustomersFromThisHour() { }
        private void ProcessCustomer(CustomerEntry customerEntry) { }
        private int GetMaxCustomerCapacityThisHour() => availableProducers.Count + pointOfSales.Count;
    }
    public static class CustomerEntriesHelper
    {
        public static List<CustomerEntry> GetEntriesByAddress(Address address) => new();
        public static void UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration registration, DayOfWeekOrdered day) { }
    }
    public sealed class CitizenData { public bool IsPriceAcceptable(string itemName, float price) => true; }

    namespace Missing { public sealed class GameManager { } }
    namespace OtherParameters
    {
        public sealed class GameManager { public void RunMainGameTick(double delta) { } }
        public sealed class BuildingRegistration { public void Reset() { } }
        public class RetailBusinessSimulator : BusinessSimulator
        {
            private Dictionary<string, List<ItemInstance>> availableProducers = new();
            private List<PointOfSale> pointOfSales = new();
            private void ProcessAllCustomersFromThisHour() { }
            private void ProcessCustomer(CustomerEntry customerEntry, int seat) { }
            private int GetMaxCustomerCapacityThisHour() => availableProducers.Count + pointOfSales.Count;
        }
    }
    namespace Renamed
    {
        public class BusinessSimulator { protected BuildingRegistration registration = null!; protected int currentHour; }
        public class RetailBusinessSimulator : PersistentCity.Tests.F8cStandIn.BusinessSimulator
        {
            private void ProcessAllCustomersThisHour() { }
            private void ProcessCustomer(CustomerEntry customerEntry) { }
            private int GetMaxCustomerCapacity() => 0;
        }
    }
    namespace Retyped
    {
        public sealed class GameManager { public bool RunMainGameTick(float delta) => true; }
        public class BusinessSimulator { protected BuildingRegistration buildingRegistration = null!; protected long currentHour; }
        public static class CustomerEntriesHelper
        {
            public static CustomerEntry[] GetEntriesByAddress(Address address) => Array.Empty<CustomerEntry>();
            public static void UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration registration, DayOfWeekOrdered day) { }
        }
        public sealed class CitizenData { public void IsPriceAcceptable(string itemName, float price) { } }
        public class RetailBusinessSimulator : PersistentCity.Tests.F8cStandIn.BusinessSimulator
        {
            private Dictionary<string, ItemInstance[]> availableProducers = new();
            private void ProcessCustomer(CustomerEntry customerEntry) { }
        }
    }
    namespace Instance
    {
        public sealed class CustomerEntriesHelper
        {
            public static List<CustomerEntry> GetEntriesByAddress(Address address) => new();
            public void UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration registration, DayOfWeekOrdered day) { }
        }
    }
}
#pragma warning restore CS0169, CS0414, CA1822, IDE0060
