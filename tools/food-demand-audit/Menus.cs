using System;
using System.Linq;
using BigAmbitions.Items;
using BusinessLayoutSets;
using Helpers;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using PersistentCity.Persistence;
namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private object MenuCatalogue()
        {
            var names = new[] { "croissant", "burger", "hotdog", "pizza", "salad" };
            var items = ItemsGetter.AllItems.ToArray();
            var products = items.Where(i => names.Any(n => i.itemName.EndsWith("_" + n, StringComparison.Ordinal))).ToArray();
            var ids = products.Select(i => i.itemName).ToArray();
            return new {
                products = products.Select(i => new { i.itemName, type = i.type.ToString(), i.wholesalePrice, i.DefaultMarketPrice }),
                producers = items.Where(i => i.producerSettings != null && (i.itemsThatCanShowcase?.Any(ids.Contains) == true || i.producerSettings.itemsToProduce?.Any(ids.Contains) == true)).Select(i => new {
                        i.itemName, type = i.type.ToString(), i.producerSettings.ResourcesRequired, i.producerSettings.itemsToProduce, i.itemsThatCanShowcase,
                        layouts = BusinessLayoutSetHelper.GetAllBusinessLayoutSets().Values.Where(l => l.Items != null
                            && l.Items.Any(x => x.itemName == i.itemName)).Select(l => new { l.BusinessType, l.BuildingSize, l.BuildingVersion }).Distinct().ToArray()
                    }).ToArray(),
                shops = SaveGameManager.Current.BuildingRegistrations.Where(r => r.HasEstablishedBusiness && r.cachedAvailableProducts.Any(ids.Contains))
                    .Select(r => new { placeId = PlaceId(r), r.businessTypeName, r.cachedAvailableProducts,
                        prices = r.retailPrices.Where(p => ids.Contains(p.itemName)).ToArray() }).ToArray()
            };
        }
        private object AddFixtureMenu(BuildingRegistration shop, string[] products, JObject? prices)
        {
            if (Simulation.State.Food != null || shop.BusinessName != "Persistent City food routines"
                || products.Length < 1 || products.Length > 4 || products.Any(p => !FoodProducts.Known(p))
                || products.Distinct().Count() != products.Length) throw new InvalidOperationException("Verified disposable menu required before enrollment.");
            pilotShop = shop;
            var added = new List<object>();
            foreach (var product in products)
            {
                if (product != Product)
                {
                    var candidates = ItemsGetter.AllItems.Where(i => (i.type & ItemType.ShowcaseShelf) != 0
                        && i.producerSettings.ResourcesRequired && i.itemsThatCanShowcase?.Contains(product) == true)
                        .OrderBy(i => i.itemName, StringComparer.Ordinal).ToArray();
                    var producer = candidates.FirstOrDefault(i => BusinessLayoutSetHelper.GetAllBusinessLayoutSets().Values.Any(l =>
                        l.BuildingSize == shop.BuildingCached.BuildingSize && l.BuildingVersion == shop.BuildingCached.BuildingVersion
                        && l.Items != null && l.Items.Any(x => x.itemName == i.itemName && !SerializableVector3.IsZero(x.position))))
                        ?? throw new InvalidOperationException("No native stocked showcase with a valid layout for " + product);
                    var instance = AddItem(producer.itemName, product, 300);
                    foreach (var requirement in ItemHelper.GetMissingRequirements(instance))
                    {
                        if (!(requirement is Furniture.Requirements.HasItemTypeAttached attached))
                            throw new InvalidOperationException("Unsupported producer fixture requirement: " + requirement.GetType().Name);
                        var supportName = attached.GetAllItemsFittingRequirement().OrderBy(n => n, StringComparer.Ordinal).First();
                        var support = ItemHelper.InitializeNewInstance(supportName);
                        support.streetName = shop.StreetName; support.streetNumber = shop.StreetNumber;
                        PlaceFixture(support); shop.AddItemInstanceToBuilding(support);
                        instance.parentId = support.id;
                        support.stackedItems.Add(new AttachableChild { childId = instance.id, childItemName = instance.itemName, attachmentIndex = 0 });
                    }
                    if (ItemHelper.GetMissingRequirements(instance).Any()) throw new InvalidOperationException("Native menu producer requirements remain unmet: " + producer.itemName + ": " + string.Join(",", ItemHelper.GetMissingRequirements(instance).Select(r => r.GetType().Name)));
                    instance.cargoInstances[0].pricePerUnit = ItemsGetter.GetByName(product).wholesalePrice;
                    added.Add(new { product, producer = producer.itemName, id = instance.id, producer.itemsThatCanShowcase, producer.producerSettings.ResourcesRequired });
                }
                float price = (float?)prices?[product] ?? (product == Product ? 10.25f : ItemsGetter.GetByName(product).DefaultMarketPrice);
                if (price <= 0 || price > 100) throw new InvalidOperationException("Fixture price outside diagnostic bounds.");
                var retail = shop.retailPrices.FirstOrDefault(p => p.itemName == product);
                if (retail == null)
                {
                    retail = new RetailPrice();
                    retail.itemName = product; shop.retailPrices.Add(retail);
                }
                retail.price = price;
            }
            if (!products.Contains(Product))
                foreach (var item in shop.itemInstances.Values.Where(i => i.cargoInstances.Any(c => c.itemName == Product)))
                    foreach (var cargo in item.cargoInstances.Where(c => c.itemName == Product)) cargo.amount = 0;
            shop.cachedAvailableProducts = products.ToList(); shop.retailPrices.RemoveAll(p => !products.Contains(p.itemName));
            BusinessHelper.UpdateCustomerCapacity(shop);
            return new { shop = PlaceId(shop), shop.businessTypeName, products, added,
                capitalAtNativeCataloguePrices = shop.itemInstances.Values.Sum(i => i.ItemCached.DefaultMarketPrice),
                openingInventoryAtNativeWholesale = shop.itemInstances.Values.SelectMany(i => i.cargoInstances).Sum(c => c.amount * c.pricePerUnit), prices = shop.retailPrices, supply = FoodSupplyReader.ReadMenu(shop, products) };
        }
        private object MenuContractCheck(string? placeId)
        {
            var state = Simulation.State.Food ?? throw new InvalidOperationException("Enrolled test menu required.");
            var scope = placeId == null ? state.Shops.First() : state.Shops.Single(s => s.PlaceId == placeId);
            var shop = SaveGameManager.Current.BuildingRegistrations.Single(r => PlaceId(r) == scope.PlaceId);
            if (shop.BusinessName != "Persistent City food routines" || !BusinessHelper.IsBusinessOpen(shop))
                throw new InvalidOperationException("Open disposable fixture required.");
            var native = (NativeFoodMarket)typeof(PopulationRuntime).GetField("foodMarket",Fields)!.GetValue(Runtime)!;
            var draw = typeof(NativeFoodMarket).GetMethod("CustomerDraw", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            bool DrawAllowed() => (bool)draw.Invoke(null, new object?[] { shop.Address, null })!;
            bool ownedDraw = DrawAllowed(), soldDraw;
            try { shop.RentedByPlayer = false; soldDraw = DrawAllowed(); }
            finally { shop.RentedByPlayer = true; }
            if (ownedDraw || !soldDraw) throw new InvalidOperationException("Native draw ownership boundary failed.");
            var results = new List<object>(); int resident = state.Shops.IndexOf(scope) * 20;
            ResidentMeal Meal(string product) => new ResidentMeal {
                ResidentId = Simulation.State.Citizens[resident++].Identity.Id, Day = SaveGameManager.Current.Day, Kind = MealKind.Legacy,
                PlaceId = scope.PlaceId, BusinessId = scope.BusinessId, ProductId = product, AllowanceCents = 10000,
                PriceCents = (int)Math.Round(shop.retailPrices.Single(p => p.itemName == product).price * 100.0),
                ServiceMinutes = 60.0 / FoodSupplyReader.Read(shop, product).MealsPerHour!.Value };
            foreach (var product in scope.Products())
            {
                var meal = Meal(product); var before = FoodSupplyReader.ReadMenu(shop, scope.Products());
                var sale = native.Buy(meal, Simulation.State.Minute);
                if (!sale.Paid) throw new InvalidOperationException("Native menu product refused: " + product + " " + sale.Reason);
                var after = FoodSupplyReader.ReadMenu(shop, scope.Products());
                var replay = native.Buy(meal, Simulation.State.Minute);
                if (!replay.Paid) throw new InvalidOperationException("Native menu receipt replay failed.");
                results.Add(new { product, sale.Paid, sale.Reason, before, after, replay = replay.Reason });
            }
            var changed = Meal(scope.Products().Skip(1).First());
            var changedPrice = shop.retailPrices.Single(p => p.itemName == changed.ProductId);
            float original = changedPrice.price; var priceBefore = Status(); FoodSale priceRefusal;
            try { changedPrice.price += 1; priceRefusal = native.Buy(changed, Simulation.State.Minute); }
            finally { changedPrice.price = original; }
            if (priceRefusal.Paid || FoodSaleReceipts.AlreadyPaid(SaveGameManager.Current.modData, changed))
                throw new InvalidOperationException("Changed approved price was not refused.");
            var advertised = shop.cachedAvailableProducts.ToList(); string excluded;
            try { shop.cachedAvailableProducts.Add("ba:itemname_pizza"); excluded = native.ScopeStatus(scope.PlaceId); }
            finally { shop.cachedAvailableProducts = advertised; }
            if (excluded.Length == 0) throw new InvalidOperationException("Unsupported menu edit was not exposed.");
            // Isolated adapter contention: the full wallet debit/refund path is tested separately.
            var counters = shop.itemInstances.Values.Where(i => i.ItemCached.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isfullservicecashregister)).ToArray();
            if (counters.Length == 0) throw new InvalidOperationException("No diagnostic counter.");
            foreach (var counter in counters) counter.cargoInstances[0].amount = 0;
            counters[0].cargoInstances[0].amount = 1;
            var first = Meal(scope.Products().First()); var second = Meal(scope.Products().Skip(1).First());
            var snapshot = Status(); var winner = native.Buy(first, Simulation.State.Minute);
            int receiptCount = SaveGameManager.Current.modData.Count, orderCount = shop.unprocessedCompletedOrders.Count;
            var loser = native.Buy(second, Simulation.State.Minute);
            if (!winner.Paid || loser.Paid || SaveGameManager.Current.modData.Count != receiptCount || shop.unprocessedCompletedOrders.Count != orderCount)
                throw new InvalidOperationException("Last bag contention did not refuse without side effects.");
            return new { ownership = new { ownedDraw, soldDraw }, products = results, changedPrice = new { priceRefusal.Paid, priceRefusal.Reason, before = priceBefore }, changedMenu = excluded, lastBag = new { first = first.ProductId, second = second.ProductId, winner.Paid, loser = loser.Reason,
                noLoserReceipt = !SaveGameManager.Current.modData.ContainsKey("persistentcity:food-sale-v1:" + second.IntentId), before = snapshot, after = Status() },
                boundary = "Direct native adapter checks in a disposable fixture, not generated resident journeys. Core wallet contention is separately regression-tested. Reload base before demand measurement." };
        }
        private object EditMenuForReload()
        {
            var scope = Simulation.State.Food!.Shops.First(s => s.ContainsProduct(FoodProducts.Croissant));
            var shop = SaveGameManager.Current.BuildingRegistrations.Single(r => PlaceId(r) == scope.PlaceId);
            if (shop.BusinessName != "Persistent City food routines") throw new InvalidOperationException("Named disposable fixture required.");
            var showcase = shop.itemInstances.Values.Single(i => i.cargoInstances.Any(c => c.itemName == FoodProducts.Croissant));
            const string unsupported = "ba:itemname_cupcake";
            if (!showcase.ItemCached.itemsThatCanShowcase.Contains(unsupported)) throw new InvalidOperationException("Native showcase cannot hold edit product.");
            var cargo = showcase.cargoInstances.Single(c => c.itemName == FoodProducts.Croissant);
            cargo.itemName = unsupported; cargo.amount = 10; cargo.pricePerUnit = ItemsGetter.GetByName(unsupported).wholesalePrice;
            if (ItemHelper.GetMissingRequirements(showcase).Any()) throw new InvalidOperationException("Changed fixture requirements unmet.");
            shop.cachedAvailableProducts.Remove(FoodProducts.Croissant); shop.cachedAvailableProducts.Add(unsupported);
            shop.retailPrices.Add(new RetailPrice { itemName=unsupported,price=ItemsGetter.GetByName(unsupported).DefaultMarketPrice });
            return new { scope.PlaceId, changed = shop.cachedAvailableProducts, status = Runtime.EconomyOverview };
        }
    }
}
