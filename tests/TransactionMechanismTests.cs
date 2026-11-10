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
        public void TheE5bRefusalTextMapsToTheDynamicCatalogueOff()
        {
            // The Runtime constant and Core's copy are the same text.
            string market = File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "NativeFoodMarket.cs"));
            Assert.Contains("public const string DynamicIntegrationRefusal = \"" + TransactionMechanisms.DynamicIntegrationRefusal + "\";", market);
            var verdict = TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Dynamic, TransactionMechanisms.DynamicIntegrationRefusal + ": coordinator Fallback");
            Assert.Equal("shop.dynamic-catalogue off: E5b dynamic native acquisition off; original three only: coordinator Fallback", verdict.Text);
            Assert.Equal("address:x:1:shop (shop, business:x): shop.dynamic-catalogue off: E5b dynamic native acquisition off; original three only: coordinator Fallback\n"
                + "native evidence: " + TransactionMechanisms.Get(TransactionMechanismId.ShopDynamicCatalogue).NativeEvidence,
                TransactionMechanisms.Describe("address:x:1:shop", "business:x", FoodScopeChannel.Shop, verdict));
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
                Assert.StartsWith("implemented; rig pending (docs/checks/", m.ProofText);
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

        [Fact]
        public void NoProductsIsOneUnsupportedReason()
        {
            var verdict = TransactionMechanisms.ForBusiness(FoodScopeChannel.Shop, Array.Empty<string>(), "");
            Assert.False(verdict.Served); Assert.Null(verdict.Mechanism); Assert.Equal("unsupported: no products in scope", verdict.Text);
        }

        [Fact]
        public void TheBusinessCommandReadsTheExistingScopeStatusOnly()
        {
            string source = CapabilityReportTests.StripComments(File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "CapabilityDiagnostics.cs")));
            Assert.Contains("foodMarket?.ScopeStatus(scope.PlaceId)", source);
            Assert.Contains("vendors.CanServe(scope.BusinessId)", source);
            Assert.Contains("TransactionMechanisms.ForBusiness(scope.Channel, new[] { scope.ProductId }.Concat(scope.ExtraProductIds).ToArray(), status)", source);
            Assert.Contains("if (area == \"capability\") { CapabilityCommand(action, detail); return; }",
                File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "PopulationCensusDiagnostics.cs")));
            // No change to Supported: TransactionMechanisms is not referenced by the market.
            Assert.DoesNotContain("TransactionMechanisms", File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "NativeFoodMarket.cs")));
        }
    }
}
