using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using PersistentCity.Core;
using PersistentCity.Tests.Acceptance;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests.Acceptance
{
    /// <summary>Faults the loop oracles must catch: each makes the run throw <see cref="CityViolation"/>.</summary>
    public enum LoopFault { None,
        /// <summary>The native books lose a paid order at commit, so the resident paid for nothing.</summary>
        LostBooking,
        /// <summary>A refused purchase moves one cent from the wallet to discretionary spending (the wallet stays conserved).</summary>
        DebitOnRefusal }

    /// <summary>The mid-loop states the loop reload arm saves and loads at.</summary>
    internal enum LoopReload {
        /// <summary>A resident who has earned wages travels to the goods shop for a planned need visit.</summary>
        NeedTrip,
        /// <summary>Right after the first paid receipt.</summary>
        AfterPaid }

    /// <summary>GR-4 (J1): the purchase market on an F9c arm. Two shops outside every food scope join the city as player
    /// businesses the market serves (the fixture's own shops are food scopes in city-m): a self-service clothing store whose optional basket trims to the ceiling, and a jewellery store
    /// whose one mandatory line is above every wallet tier's band, so each visit there is refused at the ceiling.</summary>
    internal sealed record CityLoop(LoopFault Fault = LoopFault.None)
    {
        public const string Goods = "shop:loop-goods", Jewellery = "shop:loop-jewellery";
        public const string Clothing = "ba:businesstype_clothingstore", Jewellery_Type = "ba:businesstype_jewelrystore";
        public const int CapacityPerHour = 10;
        public static readonly PurchaseLine[] GoodsBasket =
        {
            new("ba:itemname_shirt", Money.FromCents(2500), false),
            new("ba:itemname_socks", Money.FromCents(600), false),
            new("ba:itemname_jeans", Money.FromCents(4000), false),
        };
        /// <summary>Above the upper tier's band ($160): never inside a ceiling.</summary>
        public static readonly PurchaseLine[] JewelleryBasket = { new("ba:itemname_ring", Money.FromCents(45000), true) };
    }

    internal sealed partial record CityArm
    {
        /// <summary>The purchase market on this arm; null: the F9c arm without one.</summary>
        public CityLoop? Loop { get; init; }

        /// <summary>The loop arms: the synchronously drained loop reference, two cadences (30/32/4 and 120/128/2) and the
        /// reload arm, which saves and loads at every F9c in-flight state and at each <see cref="LoopReload"/> state.</summary>
        public static IEnumerable<CityArm> LoopArms() => new[]
        {
            ReferenceArm with { Name = "loop-reference", Loop = new CityLoop() },
            new CityArm("loop-fps30", false, 30, 32, 4) { Loop = new CityLoop() },
            new CityArm("loop-fps120", false, 120, 128, 2) { Loop = new CityLoop() },
            new CityArm("loop-reload", false, 60, 16, 2, Reloads: true) { Loop = new CityLoop() },
        };
    }

    /// <summary>One request the market opened, as the simulator finished it.</summary>
    internal sealed record LoopPurchase(long Minute, string Resident, string Venue, PurchaseRefusal Refusal, long TotalCents, int NativeCalls);

    /// <summary>What the loop did over one arm.</summary>
    internal sealed class CityLoopStats
    {
        public readonly List<LoopPurchase> Purchases = new();
        /// <summary>Residents seen setting off for a served venue, with the first minute.</summary>
        public readonly Dictionary<string, double> Travelled = new(StringComparer.Ordinal);
        public readonly List<(LoopReload State, double Minute, string Resident)> Reloads = new();
        /// <summary>The need trip reload's resident: wages earned before it, and the plan item it travelled for.</summary>
        public long TripEmploymentUnits;
        public PlanItem? TripItem;
        public long BookedOrders;
        public BigInteger BookedUnits, PaidUnits, DiscretionaryUnits;
        public long Impulses;
        public IEnumerable<LoopPurchase> Bought => Purchases.Where(p => p.Refusal == PurchaseRefusal.None);
    }

    internal sealed partial class CityRun
    {
        public CityLoopStats? Loop;
    }

    /// <summary>The native side of the purchase market: the venues' baskets and capacity, the saved receipt store and the
    /// native books. It lives outside the saved city; a reload copies the receipts as the native save carries them.</summary>
    internal sealed class CityLoopMarket : IPurchaseMarket
    {
        private static readonly string[] Served = { CityLoop.Goods, CityLoop.Jewellery };
        public readonly CityLoop Spec;
        public Dictionary<string, string> Store = new(StringComparer.Ordinal);
        /// <summary>Booked native orders: key, resident, venue and the total in cents.</summary>
        public readonly List<(string Key, string Resident, string Venue, long Cents)> Books = new();
        public CityLoopMarket(CityLoop spec) { Spec = spec; }

        public bool Serves(string venueId) => Array.IndexOf(Served, venueId) >= 0;
        public IReadOnlyList<string> Venues => Served;
        public IDictionary<string, string> Receipts => Store;

        /// <summary>The open request of the running unit, read back when the simulator observes the person.</summary>
        public Opened? Current;
        public Opened? Finished;
        internal sealed class Opened
        {
            public PurchaseRequest Request = null!;
            public Native Native = null!;
            public ResidentBudget? Wallet;
            public long BalanceUnits, DiscretionaryUnits;
            public int BooksBefore;
        }

        public Func<string, ResidentBudget?> WalletOf = _ => null;

        public void Guarded(Action unit)
        {
            Current = null;
            unit();
            var opened = Current; Current = null;
            if (opened == null) return;
            if (Spec.Fault == LoopFault.DebitOnRefusal && opened.Native.Calls == 0 && opened.Wallet != null)
            {
                var accounts = opened.Wallet.Ledger; accounts.Discretionary += Money.FromCents(1);
                opened.Wallet.Balance -= Money.FromCents(1); opened.Wallet.Ledger = accounts;
            }
            Finished = opened;
        }

        public IPurchaseNative? Open(PurchaseRequest request, out PurchaseRefusal refusal, out string reason)
        {
            refusal = PurchaseRefusal.None; reason = "";
            var wallet = WalletOf(request.ResidentId);
            ServedInHour(request, out int served);
            var native = new Native(this, request, served);
            Current = new Opened { Request = request, Native = native, Wallet = wallet, BooksBefore = Books.Count,
                BalanceUnits = wallet?.Balance.Units ?? 0, DiscretionaryUnits = wallet?.Ledger.Discretionary.Units ?? 0 };
            return native;
        }

        private void ServedInHour(PurchaseRequest request, out int served)
        {
            // From the saved receipts, as Runtime counts it, so the hourly count survives a reload.
            PurchaseContract.ServedInHour(Store, request.Minute / 60).TryGetValue(PurchaseContract.Building(request.VenueId), out served);
        }

        public CityLoopMarket Loaded()
        {
            var copy = new CityLoopMarket(Spec) { Store = new Dictionary<string, string>(Store, StringComparer.Ordinal), WalletOf = WalletOf };
            copy.Books.AddRange(Books);
            return copy;
        }

        internal sealed class Native : IPurchaseNative
        {
            private readonly CityLoopMarket market;
            private readonly PurchaseRequest request;
            private (string, string, string, long)? booked;
            public int Calls;
            public Native(CityLoopMarket market, PurchaseRequest request, int served)
            {
                this.market = market; this.request = request; ServedThisHour = served;
                bool goods = request.VenueId == CityLoop.Goods;
                Family = goods ? PurchaseFamily.SelfService : PurchaseFamily.FullService;
                Basket = goods ? CityLoop.GoodsBasket : CityLoop.JewelleryBasket;
            }
            public PurchaseFamily Family { get; }
            public IReadOnlyList<PurchaseLine> Basket { get; }
            public int ServedThisHour { get; }
            public int CapacityThisHour => CityLoop.CapacityPerHour;
            public int? NativeHourlyCount => null;
            public PurchaseNativeOutcome Sell(IReadOnlyList<PurchaseLine> lines)
            {
                Calls++;
                long cents = lines.Sum(l => (long)Math.Round(l.Price.ApproximateCents()));
                booked = (request.Key, request.ResidentId, request.VenueId, cents);
                market.Books.Add(booked.Value);
                return PurchaseNativeOutcome.Processed(lines.ToArray());
            }
            public void Undo()
            {
                if (booked != null) market.Books.Remove(booked.Value);
                booked = null;
            }
            public void Commit()
            {
                if (market.Spec.Fault == LoopFault.LostBooking && booked != null) market.Books.Remove(booked.Value);
            }
        }
    }

    internal sealed partial class CityScenario
    {
        private CityLoopMarket? loop;
        private readonly HashSet<LoopReload> pendingLoopReloads = new();

        partial void LoopPrepare(CityState state)
        {
            if (arm.Loop == null) return;
            // The same shops on every loop arm, next to the fixture's shops; after a load they are already saved.
            void Add(string id, string name, string type, Position at)
            {
                if (state.Places.Any(p => p.Id == id)) return;
                if (state.Food!.Shops.Any(s => s.PlaceId == id)) throw new CityViolation(id + " is a food scope");
                state.Places.Add(new Place { Id = id, Name = name, Kind = PlaceKind.Shop, Capacity = 16, Position = at, BusinessType = type });
            }
            Add(CityLoop.Goods, "Loop Clothing", CityLoop.Clothing, new Position(110, 0, 0));
            Add(CityLoop.Jewellery, "Loop Jewellery", CityLoop.Jewellery_Type, new Position(140, 0, -10));
            if (run.Loop == null)
            {
                run.Loop = new CityLoopStats();
                if (arm.Reloads) foreach (LoopReload s in Enum.GetValues(typeof(LoopReload))) pendingLoopReloads.Add(s);
            }
        }

        partial void LoopAttach()
        {
            if (arm.Loop == null) return;
            // Every reload (F9c's and the loop's) passes here: no purchase may still wait for its check.
            if (loop != null) LoopAllVerified(arm.Name + " reload at " + CityOracles.M(clock));
            loop = loop == null ? new CityLoopMarket(arm.Loop) : loop.Loaded();
            var byId = city.State.Citizens.ToDictionary(c => c.Identity.Id, StringComparer.Ordinal);
            loop.WalletOf = id => byId.TryGetValue(id, out var c) ? c.Wallet : city.State.Citizens.FirstOrDefault(p => p.Identity.Id == id)?.Wallet;
            city.AttachPurchases(loop);
            // The simulator observes the person at the end of the event that bought, after the contract returned.
            city.ActivityObserved += LoopObserved;
        }

        private void LoopObserved(string id, CitizenState person, double minute)
        {
            var stats = run.Loop!;
            if (!person.IsVisitor && person.Phase == CitizenPhase.Travelling && loop!.Serves(person.DestinationId) && !stats.Travelled.ContainsKey(id))
                stats.Travelled[id] = minute;
            var done = loop!.Finished;
            if (done == null || done.Request.ResidentId != id) return;
            loop.Finished = null;
            var result = city.LastPurchase;
            if (result == null || result.Key != done.Request.Key) throw new CityViolation("purchase " + done.Request.Key + " finished without a result");
            LoopVerify(done, result);
            var row = new LoopPurchase(done.Request.Minute, id, done.Request.VenueId, result.Refusal,
                result.IsBought ? (long)Math.Round(result.Total.ApproximateCents()) : 0, done.Native.Calls);
            stats.Purchases.Add(row);
            run.Ledger.Add("pu|" + row.Minute + "|" + row.Resident + "|" + row.Venue + "|" + row.Refusal + "|" + row.TotalCents + "|" + row.NativeCalls
                + "|" + CityOracles.Wallet(done.Wallet) + "|" + (done.Wallet?.Ledger.Discretionary.Units ?? 0));
        }

        /// <summary>One purchase as the oracles see it: a bought one moved exactly its paid total out of the wallet, into
        /// discretionary spending and into one native order; a refused one moved nothing, wrote no receipt and booked nothing,
        /// and a ceiling refusal never reached the native side.</summary>
        private void LoopVerify(CityLoopMarket.Opened done, PurchaseResult result)
        {
            string key = done.Request.Key, context = arm.Name + " purchase " + key;
            var wallet = done.Wallet ?? throw new CityViolation(context + ": no wallet");
            long balance = done.BalanceUnits - wallet.Balance.Units, spent = wallet.Ledger.Discretionary.Units - done.DiscretionaryUnits;
            int booked = loop!.Books.Count - done.BooksBefore;
            if (result.IsBought)
            {
                if (balance != result.Total.Units || spent != result.Total.Units) throw new CityViolation(context + ": paid " + result.Total.ToCentsText() + " but the wallet moved " + balance);
                if (booked != 1 || done.Native.Calls != 1) throw new CityViolation(context + ": paid without exactly one native booking (" + booked + ")");
                if (!loop.Store.TryGetValue(key, out var receipt) || !receipt.StartsWith("paid|" + result.Total.ToCentsText() + "|", StringComparison.Ordinal))
                    throw new CityViolation(context + ": paid without its paid receipt");
                return;
            }
            if (balance != 0 || spent != 0) throw new CityViolation(context + ": refused purchase (" + result.Refusal + ") moved the wallet");
            if (booked != 0) throw new CityViolation(context + ": refused purchase left a native booking");
            if (result.Refusal != PurchaseRefusal.AlreadyRecorded && loop.Store.ContainsKey(key)) throw new CityViolation(context + ": refused purchase left a receipt");
            if (result.Refusal == PurchaseRefusal.Ceiling && done.Native.Calls != 0) throw new CityViolation(context + ": ceiling refusal reached the native side");
        }

        /// <summary>Hourly: the paid receipts, the native books and every resident's discretionary spending hold the same money.</summary>
        partial void LoopCheckpoint(string context)
        {
            if (loop == null) return;
            var stats = run.Loop!;
            stats.PaidUnits = 0;
            foreach (var receipt in loop.Store.Values.Where(v => v.StartsWith("paid|", StringComparison.Ordinal)))
            {
                if (!Money.TryParseCents(receipt.Split('|')[1], Money.CentBits, out var paid)) throw new CityViolation(context + ": unreadable receipt " + receipt);
                stats.PaidUnits += paid.Units;
            }
            stats.BookedUnits = 0;
            foreach (var order in loop.Books) stats.BookedUnits += Money.FromCents(order.Cents).Units;
            stats.DiscretionaryUnits = 0;
            foreach (var person in city.State.Citizens.Where(c => c.Wallet != null)) stats.DiscretionaryUnits += person.Wallet!.Ledger.Discretionary.Units;
            if (stats.PaidUnits != stats.BookedUnits) throw new CityViolation(context + ": paid receipts " + stats.PaidUnits + " != native books " + stats.BookedUnits);
            if (stats.PaidUnits != stats.DiscretionaryUnits) throw new CityViolation(context + ": paid receipts " + stats.PaidUnits + " != resident spending " + stats.DiscretionaryUnits);
            if (loop.Books.Select(b => b.Key).Distinct(StringComparer.Ordinal).Count() != loop.Books.Count) throw new CityViolation(context + ": duplicate native booking");
        }

        partial void LoopMaybeReload(ref bool reloaded)
        {
            if (loop == null || pendingLoopReloads.Count == 0 || city.View.Minute < CityFixture.StartMinute + 30) return;
            var stats = run.Loop!;
            CitizenState? person = null; LoopReload state;
            if (pendingLoopReloads.Contains(LoopReload.NeedTrip) && (person = Traveller()) != null) state = LoopReload.NeedTrip;
            else if (pendingLoopReloads.Contains(LoopReload.AfterPaid) && stats.Bought.Any())
            {
                string last = stats.Bought.Last().Resident;
                person = city.State.Citizens.Single(c => c.Identity.Id == last);
                state = LoopReload.AfterPaid;
            }
            else return;
            if (state == LoopReload.NeedTrip)
            {
                stats.TripEmploymentUnits = person.Wallet!.Ledger.EmploymentIncome.Units;
                stats.TripItem = TripItem(person);
            }
            string id = person.Identity.Id;
            var needs = person.Needs!.Copy(); var plan = person.Plan!.Copy();
            string wallet = CityOracles.Wallet(person.Wallet) + "|" + person.Wallet!.Ledger.Discretionary.Units;
            string where = person.Phase + "|" + person.DestinationId + "|" + person.PlaceId + "|" + CityOracles.M(person.NextEventMinute);
            var receipts = loop.Store.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
            ReloadCity(state.ToString());
            var loaded = city.State.Citizens.Single(c => c.Identity.Id == id);
            string context = arm.Name + " " + state + " reload of " + id;
            if (loaded.Needs == null || !needs.SameAs(loaded.Needs)) throw new CityViolation(context + ": needs changed");
            if (loaded.Plan == null || !plan.SameAs(loaded.Plan)) throw new CityViolation(context + ": plan changed");
            if (CityOracles.Wallet(loaded.Wallet) + "|" + loaded.Wallet!.Ledger.Discretionary.Units != wallet) throw new CityViolation(context + ": wallet changed");
            if (loaded.Phase + "|" + loaded.DestinationId + "|" + loaded.PlaceId + "|" + CityOracles.M(loaded.NextEventMinute) != where) throw new CityViolation(context + ": journey changed");
            // Modelled: the receipts live in the native save's mod data, which this harness carries as a dictionary copy
            // (CityLoopMarket.Loaded), so this holds by construction; the in-game reload check covers the real store.
            if (!receipts.SequenceEqual(loop!.Store.OrderBy(p => p.Key, StringComparer.Ordinal))) throw new CityViolation(context + ": receipts changed");
            stats.Reloads.Add((state, city.View.Minute, id));
            pendingLoopReloads.Remove(state);
            reloaded = true;
        }

        /// <summary>The first resident (by ID) who has earned wages and is travelling to the goods shop for a planned need item.</summary>
        private CitizenState? Traveller()
        {
            CitizenState? best = null;
            foreach (var c in city.State.Citizens)
            {
                if (c.Phase != CitizenPhase.Travelling || c.DestinationId != CityLoop.Goods || c.Wallet == null || c.Needs == null
                    || c.Wallet.Ledger.EmploymentIncome.Units <= 0 || TripItem(c) == null) continue;
                if (best == null || string.CompareOrdinal(c.Identity.Id, best.Identity.Id) < 0) best = c;
            }
            return best;
        }

        private static PlanItem? TripItem(CitizenState person)
        {
            var plan = person.Plan;
            if (plan == null) return null;
            for (int i = 0; i < plan.Items.Count; i++)
                if (plan.Items[i].Kind == PlanItemKind.Need && plan.Items[i].VenueId == CityLoop.Goods && plan.StateOf(i) == PlanItemState.Travelling) return plan.Items[i];
            return null;
        }

        /// <summary>Every opened purchase was read back and verified at its event's observation.</summary>
        private void LoopAllVerified(string context)
        {
            var open = loop!.Finished ?? loop.Current;
            if (open != null) throw new CityViolation(context + ": purchase " + open.Request.Key + " was never verified");
        }

        partial void LoopFinish()
        {
            if (loop == null) return;
            LoopAllVerified(arm.Name + " at the end");
            LoopCheckpoint(arm.Name + " at the end");
            run.Loop!.BookedOrders = loop.Books.Count;
            run.Loop.Impulses = city.ImpulsesInserted;
            if (city.PurchaseFault != null) throw new CityViolation(arm.Name + ": purchase fault " + city.PurchaseFault);
        }
    }
}

namespace PersistentCity.Tests
{
    /// <summary>GR-4 (J1, plan next-tracks.md "GR-4"): one whole loop in the F9c city at today's population. A resident
    /// earns wages, plans a need visit to a player goods shop, travels there, buys through the purchase contract, pays from
    /// the wallet, and the city reloads mid-loop and continues. The loop arms equal their synchronously drained reference
    /// on every F9c oracle (ledger with every purchase, day saves, native orders, final save); money is conserved across the
    /// paid receipts, the native books and the wallets; a ceiling refusal moves nothing.</summary>
    [Collection(StreetFoodHooks.Collection)]
    public sealed class J1WholeLoopTests : IClassFixture<J1WholeLoopTests.Runs>
    {
        public sealed class Runs
        {
            private readonly Dictionary<(string, string), CityRun> runs = new();
            private readonly F9IntegratedCityTests.Runs checkpoints = new();
            internal CityCheckpoint Checkpoint(string size) => checkpoints.Checkpoint(size);
            internal CityRun Run(string size, string arm)
            {
                lock (runs)
                    return runs.TryGetValue((size, arm), out var run) ? run : runs[(size, arm)] = CityScenario.Run(Checkpoint(size), CityArm.LoopArms().Single(a => a.Name == arm));
            }
        }

        private readonly Runs runs;
        private readonly ITestOutputHelper output;
        public J1WholeLoopTests(Runs runs, ITestOutputHelper output) { this.runs = runs; this.output = output; }

        public static IEnumerable<object[]> CadenceArms() => new[] { new object[] { "loop-fps30" }, new object[] { "loop-fps120" }, new object[] { "loop-reload" } };

        [Theory, Trait("Size", "city-s"), MemberData(nameof(CadenceArms))]
        public void SmallLoopArmEqualsTheLoopReference(string arm) => ArmEqualsTheReference("city-s", arm);

        [Theory, Trait("Size", "city-m"), MemberData(nameof(CadenceArms))]
        public void MediumLoopArmEqualsTheLoopReference(string arm) => ArmEqualsTheReference("city-m", arm);

        private void ArmEqualsTheReference(string size, string name)
        {
            var reference = runs.Run(size, "loop-reference");
            var arm = runs.Run(size, name);
            CityOracles.Equal(reference, arm);
            Assert.Equal(reference.Loop!.Purchases, arm.Loop!.Purchases);
            Assert.Equal(0, arm.HistoricalClockMisses);
            Assert.Equal(0, arm.HistoricalCheckouts);
            Assert.Equal(reference.Postings, arm.Postings);
            output.WriteLine($"{size} {name}: {arm.Seconds:F2} s tool time; reference {reference.Seconds:F2} s; {arm.Loop.Purchases.Count} purchases, "
                + $"{arm.Loop.Bought.Count()} bought");
        }

        [Fact, Trait("Size", "city-s")] public void SmallCityRunsTheWholeLoop() => WholeLoop("city-s", 10, 200);
        [Fact, Trait("Size", "city-m")] public void MediumCityRunsTheWholeLoop() => WholeLoop("city-m", 50, 2000);

        private void WholeLoop(string size, int minimumBought, int minimumFoodOrders)
        {
            var run = runs.Run(size, "loop-reference");
            var loop = run.Loop!;
            var bought = loop.Bought.ToList();
            // Purchases happen at the goods shop, through the contract, and every one was verified as it finished.
            Assert.True(bought.Count >= minimumBought, "bought " + bought.Count);
            Assert.All(bought, p => Assert.Equal(CityLoop.Goods, p.Venue));
            Assert.All(bought, p => Assert.InRange(p.TotalCents, 1, 7100));
            Assert.Equal(bought.Count, loop.BookedOrders);
            Assert.Equal(loop.PaidUnits, loop.BookedUnits);
            Assert.Equal(loop.PaidUnits, loop.DiscretionaryUnits);
            Assert.True(loop.PaidUnits > 0);
            // Food sales still run alongside.
            Assert.True(run.Orders.Count > minimumFoodOrders, "food orders " + run.Orders.Count);
            // The ceiling: every jewellery visit (and a goods visit from a wallet below the cheapest line) is refused before the
            // native side and moved nothing (checked per purchase).
            var ceiling = loop.Purchases.Where(p => p.Refusal == PurchaseRefusal.Ceiling).ToList();
            Assert.NotEmpty(ceiling);
            Assert.All(ceiling, p => Assert.Equal((0, 0L), (p.NativeCalls, p.TotalCents)));
            Assert.Contains(ceiling, p => p.Venue == CityLoop.Jewellery);
            Assert.DoesNotContain(loop.Purchases, p => p.Venue == CityLoop.Jewellery && p.Refusal == PurchaseRefusal.None);
            // A refused need is deferred: the resident's jewellery need is due again the next day.
            var refused = run.State.Citizens.Where(c => c.Needs?.Find(CityLoop.Jewellery_Type)?.Deferrals > 0).ToList();
            Assert.NotEmpty(refused);
            // Residents who bought carry the goods need with its last venue, due again after the interval.
            foreach (var p in bought)
            {
                var need = run.State.Citizens.Single(c => c.Identity.Id == p.Resident).Needs!.Find(CityLoop.Clothing)!;
                Assert.Equal(CityLoop.Goods, need.LastVenue);
                Assert.True(need.NextDueDay > p.Minute / 1440, p.Resident + " due again on " + need.NextDueDay);
            }
            output.WriteLine($"{size} loop: refusals " + string.Join(", ", loop.Purchases.Where(p => p.Refusal != PurchaseRefusal.None)
                .GroupBy(p => p.Venue + " " + p.Refusal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " " + g.Count())));
            output.WriteLine($"{size} loop: goods requests by hour " + string.Join(", ", loop.Purchases.Where(p => p.Venue == CityLoop.Goods)
                .GroupBy(p => p.Minute % 1440 / 60).OrderBy(g => g.Key).Select(g => g.Key + ":00 " + g.Count())));
            output.WriteLine($"{size} loop: {loop.Purchases.Count} requests, {bought.Count} bought ({Money.CentsText(loop.PaidUnits)}), "
                + $"{ceiling.Count} ceiling refusals, {loop.Travelled.Count} residents travelled to a served shop, {loop.Impulses} impulses, {run.Seconds:F2} s");
        }

        [Fact, Trait("Size", "city-s")] public void SmallReloadMidLoopContinuesIt() => ReloadMidLoop("city-s");
        [Fact, Trait("Size", "city-m")] public void MediumReloadMidLoopContinuesIt() => ReloadMidLoop("city-m");

        private void ReloadMidLoop(string size)
        {
            var run = runs.Run(size, "loop-reload");
            var loop = run.Loop!;
            // Both loop states were saved and loaded (needs, plan, journey, wallet and receipts checked on the loaded city).
            Assert.Equal(new[] { LoopReload.NeedTrip, LoopReload.AfterPaid }, loop.Reloads.Select(r => r.State).OrderBy(s => s));
            foreach (CityReload state in Enum.GetValues(typeof(CityReload))) Assert.Contains(run.Reloads, r => r.State == state);
            var trip = loop.Reloads.Single(r => r.State == LoopReload.NeedTrip);
            // The one resident: earned wages, planned the need visit, was on the way at the reload, then bought and paid.
            Assert.True(loop.TripEmploymentUnits > 0);
            Assert.NotNull(loop.TripItem);
            Assert.Equal((PlanItemKind.Need, CityLoop.Goods), (loop.TripItem!.Kind, loop.TripItem.VenueId));
            Assert.True(loop.TripItem.EndsAt > trip.Minute, "the item was still ahead at the reload");
            var paid = loop.Purchases.FirstOrDefault(p => p.Resident == trip.Resident && p.Minute >= trip.Minute);
            Assert.NotNull(paid);
            Assert.Equal((CityLoop.Goods, PurchaseRefusal.None), (paid!.Venue, paid.Refusal));
            Assert.True(paid.Minute <= loop.TripItem.EndsAt, "bought on the trip it reloaded in");
            var person = run.State.Citizens.Single(c => c.Identity.Id == trip.Resident);
            Assert.Equal(CityLoop.Goods, person.Needs!.Find(CityLoop.Clothing)!.LastVenue);
            output.WriteLine($"loop reloads: " + string.Join(", ", loop.Reloads.Select(r => r.State + "@" + CityOracles.M(r.Minute) + " " + r.Resident))
                + $"; {trip.Resident} earned {Money.CentsText(loop.TripEmploymentUnits)} before the trip and paid {paid.TotalCents} cents at {paid.Minute}");
        }

        public static IEnumerable<object[]> Faults() => new[]
        {
            new object[] { LoopFault.LostBooking, "without exactly one native booking" },
            new object[] { LoopFault.DebitOnRefusal, "moved the wallet" },
        };

        /// <summary>The loop oracles see a broken purchase: a lost native booking and a refusal that debits.</summary>
        [Theory, Trait("Size", "city-s"), MemberData(nameof(Faults))]
        public void TheLoopOraclesCatchABrokenPurchase(LoopFault fault, string message)
        {
            var spec = CityArm.LoopArms().Single(a => a.Name == "loop-fps120") with { Name = "loop-" + fault, Loop = new CityLoop(fault) };
            var error = Assert.Throws<CityViolation>(() => CityScenario.Run(runs.Checkpoint("city-s"), spec));
            Assert.Contains(message, error.Message);
            output.WriteLine(error.Message);
        }
    }
}
