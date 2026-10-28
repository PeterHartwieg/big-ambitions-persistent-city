using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests
{
    public sealed class FoodAuthorityTests
    {
        private static FoodAuthority Shop(long hour = 8)
        {
            var row = new FoodAuthority { PlaceId = "address:test:1:shop" };
            row.Observe("business:test:1:3:bakery:player", true, hour); return row;
        }
        [Fact] public void DiscoveryCannotRetroactivelyOwnItsNativeHour()
        {
            var shop = Shop();
            Assert.False(shop.Boundary(8, true, false, true));
            Assert.False(shop.Boundary(9, false, false, true));
            Assert.False(shop.Boundary(9, true, false, false));
            Assert.True(shop.Boundary(9, true, false, true));
            Assert.False(shop.OwnsHour(8)); Assert.True(shop.OwnsHour(9));
        }
        [Fact] public void UnpaidNativeBodiesDeferAcquisitionUntilNaturalDrainAndNextBoundary()
        {
            var shop = Shop();
            for (long hour = 9; hour <= 11; hour++) Assert.False(shop.Boundary(hour, true, true, true));
            Assert.False(shop.CanServe);
            Assert.True(shop.Boundary(12, true, false, true)); Assert.True(shop.CanServe);
            Assert.False(shop.OwnsHour(11));
        }
        [Fact] public void UnsupportedMenuFreezesModImmediatelyButOldBatchRemainsModUntilDrainedBoundary()
        {
            var shop = Shop(); shop.Boundary(9, true, false, true);
            shop.Observe(shop.Signature, false, 10);
            Assert.False(shop.CanServe); Assert.True(shop.OwnsHour(10));
            Assert.False(shop.Boundary(10, true, false, true));
            Assert.False(shop.Boundary(11, false, false, true));
            Assert.True(shop.Boundary(11, true, false, true));
            Assert.True(shop.OwnsHour(10)); Assert.False(shop.OwnsHour(11));
        }
        [Fact] public void SupplyAndStaffOutagesDoNotChangeWholeBusinessEligibility()
        {
            var shop = Shop(); shop.Boundary(9, true, false, true);
            // Eligibility is solely the reviewed menu/business contract; operational outages
            // change offers, not ownership. The policy deliberately accepts no stock/staff input.
            Assert.False(shop.Observe(shop.Signature, true, 10));
            Assert.False(shop.Boundary(11, true, false, true)); Assert.True(shop.CanServe);
        }
        [Fact] public void SaleAndSameAddressResetRetireOldBusinessIdentityWithoutChangingAddress()
        {
            var shop = Shop(); shop.Boundary(9, true, false, true); string old = shop.BusinessId;
            shop.Observe(shop.Signature, false, 9, reset: true);
            Assert.Equal("address:test:1:shop", shop.PlaceId); Assert.NotEqual(old, shop.BusinessId);
            Assert.False(shop.CanServe); Assert.True(shop.OwnsHour(9)); Assert.True(shop.Suspended);
            string reset = shop.BusinessId;
            shop.Observe(shop.Signature, true, 9); Assert.Equal(reset, shop.BusinessId);
            Assert.True(shop.Boundary(10, true, false, true)); Assert.False(shop.Suspended); Assert.True(shop.CanServe);
        }
        [Fact] public void RepeatedReloadPreservesAuthorityAndGenerationBeforeMarketReadiness()
        {
            var shop = Shop(); shop.Boundary(9, true, false, true); string identity = shop.BusinessId;
            for (int i = 0; i < 3; i++)
            {
                shop = Assert.Single(FoodAuthorityStore.Read(FoodAuthorityStore.Write(new[] { shop })));
                Assert.False(shop.Observe(shop.Signature, true, 9));
                Assert.Equal(identity, shop.BusinessId); Assert.True(shop.OwnsHour(9));
                Assert.False(shop.Boundary(9, true, false, false));
            }
        }
        [Fact] public void SupportedMenuEditKeepsGenerationAndCurrentOwnership()
        {
            var shop = Shop(); shop.Boundary(9, true, false, true);
            string identity = shop.BusinessId;
            Assert.False(shop.Observe(shop.Signature, true, 10)); Assert.Equal(identity, shop.BusinessId); Assert.True(shop.CanServe);
        }
        [Fact] public void NativeReturnNeverReplaysPriorOwnedHourAndLaterReacquisitionWaits()
        {
            var shop = Shop(); shop.Boundary(9, true, false, true);
            shop.Observe(shop.Signature, false, 10); shop.Boundary(11, true, false, true);
            shop.Observe(shop.Signature, true, 11);
            Assert.False(shop.Boundary(11, true, false, true)); Assert.False(shop.OwnsHour(11));
            Assert.True(shop.Boundary(12, true, false, true)); Assert.False(shop.OwnsHour(11)); Assert.True(shop.OwnsHour(12));
        }
        [Fact] public void UnknownMalformedAndDuplicateManifestDoesNotMutateOriginalData()
        {
            string valid = FoodAuthorityStore.Write(new[] { Shop() });
            var inputs = new[] { valid.Replace("\"version\":1", "\"version\":2"),
                valid.Replace("\"desired\":true", "\"desired\":\"true\""),
                valid.Replace("\"generation\":1", "\"generation\":0"), valid + "{}",
                valid.Replace("\"version\":1", "\"version\":1,\"version\":1") };
            foreach (string input in inputs)
            {
                var data = new Dictionary<string, string> { [FoodAuthorityStore.Key] = input };
                Assert.ThrowsAny<Exception>(() => FoodAuthorityStore.Read(data[FoodAuthorityStore.Key]));
                Assert.Equal(input, data[FoodAuthorityStore.Key]);
            }
            var parsed = JObject.Parse(valid); ((JArray)parsed["shops"]!).Add(((JArray)parsed["shops"]!)[0].DeepClone());
            Assert.Throws<InvalidDataException>(() => FoodAuthorityStore.Read(parsed.ToString()));
        }
        [Fact] public void RegistryHasNoEightShopAuthorityCeiling()
        {
            var shops = new List<FoodAuthority>();
            for (int i = 0; i < 65; i++) { var row = Shop(); row.PlaceId = "address:test:" + i + ":shop"; shops.Add(row); }
            Assert.Equal(65, FoodAuthorityStore.Read(FoodAuthorityStore.Write(shops)).Count);
        }
    }
}
