using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests
{
    /// <summary>F8f (docs/plans/f8-capability-boundaries.md D5, rules M1-M3): transaction mechanisms as capabilities, and the
    /// mapping `pc capability business &lt;place&gt;` prints from the existing scope status. Supported is unchanged.</summary>
    [Collection(F8aSerial.Collection)]
    public sealed class TransactionMechanismTests
    {
        private const string Dumplings = "ba:itemname_dumplings";
        private static readonly string[] Historical = { FoodProducts.HotDog, FoodProducts.Croissant };
        private static readonly string[] Dynamic = { FoodProducts.HotDog, Dumplings };

        /// <summary>The scope statuses NativeFoodMarket and the vendor market report today, by family.</summary>
        public static IEnumerable<object[]> Statuses()
        {
            yield return new object[] { FoodScopeChannel.Shop, Historical, "", "shop.fullservice-bag.v1", true };
            yield return new object[] { FoodScopeChannel.Shop, Dynamic, "", "shop.dynamic-catalogue", true };
            yield return new object[] { FoodScopeChannel.Shop, Dynamic, TransactionMechanisms.DynamicIntegrationRefusal + ": capability failed: stamps", "shop.dynamic-catalogue", false };
            yield return new object[] { FoodScopeChannel.Shop, Historical, "Excluded: unsupported menu or business type", null!, false };
            yield return new object[] { FoodScopeChannel.Shop, Historical, "Waiting for a safe native accounting boundary", null!, false };
            yield return new object[] { FoodScopeChannel.Shop, Historical, "Waiting: saved food contract is validated after load; no sale before", null!, false };
            yield return new object[] { FoodScopeChannel.Shop, Historical, NativeFoodContractSpec.CheckoutScopeReason("RetailBusinessSimulator.ProcessCustomer(CustomerEntry) changed"), null!, false };
            yield return new object[] { FoodScopeChannel.Shop, Historical, "Excluded: menu stock contract is unverified", null!, false };
            yield return new object[] { FoodScopeChannel.Vehicle, Historical, "", "vehicle.streetfood-api.v1", true };
            yield return new object[] { FoodScopeChannel.Vehicle, Dynamic, "", "vehicle.streetfood-api.v1", true };
            yield return new object[] { FoodScopeChannel.Vehicle, Historical, "Street Food (FoodTrucks) VendorApi is not loaded", null!, false };
        }

        [Theory]
        [MemberData(nameof(Statuses))]
        public void EveryScopedBusinessHasOneMechanismOrOneUnsupportedReason(FoodScopeChannel channel, string[] products, string status, string? mechanism, bool served)
        {
            var verdict = TransactionMechanisms.ForBusiness(channel, products, status);
            Assert.Equal(served, verdict.Served);
            Assert.Equal(mechanism, verdict.Mechanism?.Name);
            // M1: served means one mechanism and no reason; not served means exactly one reason.
            if (served) { Assert.NotNull(verdict.Mechanism); Assert.Equal("", verdict.Reason); }
            else Assert.Equal(status, verdict.Reason);
            Assert.DoesNotContain("compatible", verdict.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("verified by rig", verdict.Text);
        }

        [Fact]
        public void VerifiedByRigWithoutAResultLinkIsRefused()
        {
            var e = Assert.Throws<ArgumentException>(() => TransactionMechanism.Define(TransactionMechanismId.ShopFullServiceBag, "shop.fullservice-bag.v1", "evidence", MechanismProof.VerifiedByRig, "docs/checks/a5-integrated-acceptance.md"));
            Assert.Contains("\"verified by rig\" needs a rig result link (M2)", e.Message);
            Assert.Throws<ArgumentException>(() => TransactionMechanism.Define(TransactionMechanismId.ShopFullServiceBag, "x", "evidence", MechanismProof.Implemented, ""));
            Assert.Throws<ArgumentException>(() => TransactionMechanism.Define(TransactionMechanismId.ShopFullServiceBag, "x", "evidence", MechanismProof.Implemented, "docs/checks/x.md", "rig/result.json"));
            var verified = TransactionMechanism.Define(TransactionMechanismId.ShopFullServiceBag, "x", "evidence", MechanismProof.VerifiedByRig, "docs/checks/x.md", "https://example.invalid/rig/result");
            Assert.Equal("verified by rig (https://example.invalid/rig/result)", verified.ProofText);

            // Every defined mechanism uses a status word and links a checks file that exists; none claims a rig result.
            Assert.Equal(new[] { "shop.fullservice-bag.v1", "vehicle.streetfood-api.v1", "shop.dynamic-catalogue", "research.agency-bill.v1" }, TransactionMechanisms.All.Select(m => m.Name));
            Assert.Equal(Enum.GetValues<TransactionMechanismId>(), TransactionMechanisms.All.Select(m => m.Id));
            foreach (var m in TransactionMechanisms.All)
            {
                Assert.True(File.Exists(Path.Combine(CapabilityReportTests.Root(), m.EvidenceLink)), m.EvidenceLink);
                Assert.NotEqual(MechanismProof.VerifiedByRig, m.Proof);
                Assert.StartsWith("implemented; rig pending (tests/", m.ProofText);
            }
        }

        [Fact]
        public void MetadataRegistrationGrantsNoMechanism()
        {
            var before = new[] { TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Dynamic, "Excluded: menu product is not classed Food"),
                TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Dynamic, TransactionMechanisms.DynamicIntegrationRefusal + ": coordinator Warming"),
                TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Historical, "") }.Select(v => v.Text).ToArray();
            // An E5e provider classes the dynamic product Food and a historical one NonFood.
            var registry = new FoodProductMetadataRegistry(); registry.BindPublicationThread();
            Assert.True(registry.TryCreateProvider("test:f8f", out var provider, out _));
            using (provider)
            {
                Assert.True(provider.TryRegisterProductMetadata(Dumplings, new FoodProductMetadataInput { MealClass = MealClass.Food }, 1, out _));
                Assert.True(provider.TryRegisterProductMetadata(FoodProducts.HotDog, new FoodProductMetadataInput { MealClass = MealClass.NonFood }, 1, out _));
                var resolver = FoodProductMetadataResolver.Empty.WithApi(registry.Publish(new FoodProductMetadataLoadedNames(new[] { Dumplings, FoodProducts.HotDog })));
                Assert.NotNull(resolver);
                var after = new[] { TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Dynamic, "Excluded: menu product is not classed Food"),
                    TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Dynamic, TransactionMechanisms.DynamicIntegrationRefusal + ": coordinator Warming"),
                    TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Historical, "") }.Select(v => v.Text).ToArray();
                Assert.Equal(before, after);
            }
            // M3 by construction: the mapping takes no metadata or policy input at all.
            var parameters = typeof(TransactionMechanisms).GetMethod(nameof(TransactionMechanisms.ForBusiness))!.GetParameters().Select(p => p.ParameterType).ToArray();
            Assert.Equal(new[] { typeof(FoodScopeChannel), typeof(IReadOnlyCollection<string>), typeof(string) }, parameters);
        }

        /// <summary>Astra-r1-6: `pc capability business` cost on the 957x2 economy city, Core part (scope lookup through the
        /// city view, mechanism mapping and text). The Runtime adds one ReadOnlyScopeStatus, a few dictionary reads.</summary>
        [Fact]
        public void BusinessCommandCostOnThe957x2City()
        {
            var (state, market) = PersistentCity.EconomyInterventions.EconomyFixture.Build(23, 957);
            var city = PersistentCity.EconomyInterventions.EconomyFixture.Attach(state, market, new PersistentCity.EconomyInterventions.EconomyRoutes(market), fresh: true);
            const int Samples = 5_000;
            var times = new double[Samples];
            double tick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            string text = "";
            for (int i = -500; i < Samples; i++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var scope = city.View.Food!.ScopeList.FirstOrDefault(x => x.PlaceId == "shop-far") ?? throw new InvalidOperationException();
                var verdict = TransactionMechanisms.ForBusiness(scope.Channel, new[] { scope.ProductId }.Concat(scope.ExtraProductIds).ToArray(), "");
                text = TransactionMechanisms.Describe(scope.PlaceId, scope.BusinessId, scope.Channel, verdict);
                if (i >= 0) times[i] = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * tick;
            }
            Array.Sort(times);
            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture, "F8f pc capability business (Core part, 957x2 city): p50={0:0.0000} ms p95={1:0.0000} ms max={2:0.0000} ms",
                times[Samples / 2], times[Samples * 95 / 100], times[^1]);
            string? path = PerfGate.Variable("PC_F8_MEASURE");
            if (!string.IsNullOrEmpty(path)) File.AppendAllLines(path, new[] { line });
            Assert.StartsWith("shop-far (shop, b-far): shop.fullservice-bag.v1", text);
            PerfGate.Under(times[Samples * 95 / 100], 5, "F8f business command p95");
        }

        [Fact]
        public void NoProductsIsOneUnsupportedReason()
        {
            var verdict = TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Array.Empty<string>(), "");
            Assert.False(verdict.Served); Assert.Null(verdict.Mechanism); Assert.Equal("unsupported: no products in scope", verdict.Text);
        }

    }
}
