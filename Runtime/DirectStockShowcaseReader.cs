using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Retail.Simulation;
using Helpers;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Detached raw field evidence, never sellable supply or a reservation.</summary>
    public abstract class DirectStockFeasibilitySnapshot
    {
        internal DirectStockFeasibilitySnapshot(DirectStockFields fields)
        {
            Refusal = fields.Refusal; Price = fields.Price; RawProductUnits = fields.Stock; RawBagUnits = fields.Bags;
            RawCounters = fields.Counters; Equipment = Array.AsReadOnly(fields.Equipment ?? Array.Empty<string>());
        }
        public abstract string ProductId { get; }
        public string Refusal { get; }
        public bool DirectStockFieldsVerified => Refusal.Length == 0;
        public Money Price { get; }
        public int RawProductUnits { get; }
        public int RawBagUnits { get; }
        public int RawCounters { get; }
        public IReadOnlyList<string> Equipment { get; }
        public string Eligibility => "Unknown: staffing, furniture requirements, native tag-cache agreement and checkout not evaluated";
    }

    /// <summary>Pinned discovery identities only; not the live product catalogue.</summary>
    internal sealed class DirectStockCandidate
    {
        static DirectStockCandidate() { } // Bind descriptors only on an explicit diagnostic call.
        private DirectStockCandidate(string productId) { ProductId = productId; }
        internal string ProductId { get; }
        internal static readonly DirectStockCandidate Donut = new DirectStockCandidate("ba:itemname_donut");
        internal static readonly DirectStockCandidate Cupcake = new DirectStockCandidate("ba:itemname_cupcake");
        internal static readonly DirectStockCandidate Salad = new DirectStockCandidate("ba:itemname_salad");
        internal static readonly DirectStockCandidate Icecream = new DirectStockCandidate("ba:itemname_icecream");
    }

    // Stack result lets candidate facades retain their API with no intermediate snapshot allocation.
    internal readonly struct DirectStockFields
    {
        internal DirectStockFields(string refusal, Money price = default, int stock = 0,
            int bags = 0, int counters = 0, string[]? equipment = null)
        { Refusal = refusal; Price = price; Stock = stock; Bags = bags; Counters = counters; Equipment = equipment; }
        internal readonly string Refusal;
        internal readonly Money Price;
        internal readonly int Stock, Bags, Counters;
        internal readonly string[]? Equipment;
    }

    /// <summary>Explicit main-thread field diagnostic. No scheduled or gameplay caller.
    /// See the E3 per-product records; bags/counters are shared business observations.</summary>
    internal static class DirectStockShowcaseReader
    {
        private static readonly FieldInfo? AuthoredTags;
        // Explicit constructor: metadata is bound only on the first diagnostic call, never eagerly.
        static DirectStockShowcaseReader()
        {
            var field = typeof(TaggedScriptableObject).GetField("tags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(List<string>) && !field.IsStatic) AuthoredTags = field;
        }
        private static List<string>? ReadAuthoredTags(TaggedScriptableObject definition)
        {
            var tags = AuthoredTags!.GetValue(definition) as List<string>;
            return tags != null && tags.Count <= 128 ? tags : null;
        }
        internal static DirectStockFields Read(BuildingRegistration registration, DirectStockCandidate candidate)
        {
            string Product = candidate.ProductId;
            DirectStockFields Refuse(string reason) => new DirectStockFields(reason);
            if (AuthoredTags == null) return Refuse("Unsupported native authoring tag field");
            if (registration == null || !registration.RentedByPlayer || !registration.HasEstablishedBusiness
                || registration.AvailableForRent || registration.temporarilyClosed)
                return Refuse("Not an established open player business");
            var type = BusinessTypeHelper.GetData(registration.businessTypeName);
            var businessTags = type == null ? null : ReadAuthoredTags(type);
            if (type?.simulator == null || type.simulator.GetType() != typeof(FullServiceBusinessSimulator)
                || type.hasEntranceFee || businessTags == null || !businessTags.Contains("ba:businesstag_customersneedpaperbags"))
                return Refuse("Unsupported simulator, entrance fee or bag contract");
            if (ItemsGetter.AllItems == null) return Refuse("Native item catalogue not loaded");
            var product = ItemsGetter.GetByName(Product, true);
            if (product == null || (product.type & ItemType.RetailProduct) == 0
                || (product.type & ItemType.ServiceProduct) != 0)
                return Refuse("Candidate is not a native retail product");
            var menu = registration.cachedAvailableProducts;
            var prices = registration.retailPrices;
            if (menu == null || menu.Count > 128 || prices == null || prices.Count > 128)
                return Refuse("Missing or oversized advertised catalogue");
            int advertised = 0, priced = 0;
            float nativePrice = 0;
            foreach (var id in menu) if (id == Product) advertised++;
            foreach (var entry in prices)
                if (entry != null && entry.itemName == Product) { priced++; nativePrice = entry.price; }
            if (advertised != 1 || priced != 1) return Refuse("Missing or ambiguous advertised product/price");
            Money price;
            try { price = Money.FromNative(nativePrice); }
            catch (ArgumentOutOfRangeException) { return Refuse("Unsupported price range or precision"); }
            if (price == Money.Zero) return Refuse("Zero price");
            var instances = registration.itemInstances;
            if (instances == null || instances.Count > 2000) return Refuse("Missing or oversized furniture collection");
            if (instances.Comparer != EqualityComparer<string>.Default && instances.Comparer != StringComparer.Ordinal)
                return Refuse("Unverified furniture key comparer");
            int stock = 0, bags = 0, counters = 0;
            var equipment = new List<string>();
            foreach (var pair in instances)
            {
                var instance = pair.Value;
                // Ordinal unique dictionary keys plus key == id already prove unique instance IDs.
                if (instance == null || string.IsNullOrEmpty(instance.id) || pair.Key != instance.id)
                    return Refuse("Ambiguous furniture identity/sharing");
                // ItemInstance.ItemCached populates a native cache; look up the loaded definition instead.
                var item = ItemsGetter.GetByName(instance.itemName, true);
                if (item == null) return Refuse("Missing equipment definition");
                var itemTags = ReadAuthoredTags(item);
                if (itemTags == null) return Refuse("Malformed equipment authoring tags");
                bool shelf = (item.type & ItemType.ShowcaseShelf) != 0;
                // Both HasTag and TagRef getters can change native caches. Read authoring strings only.
                bool counter = itemTags.Contains("ba:itemtag_isfullservicecashregister");
                var outputs = item.producerSettings?.itemsToProduce;
                if (outputs != null && outputs.Length > 128) return Refuse("Oversized producer contract");
                bool produces = outputs != null && Array.IndexOf(outputs, Product) >= 0;
                var cargo = instance.cargoInstances;
                bool contains = false;
                if (cargo != null)
                {
                    if (cargo.Count > 128) return Refuse("Oversized cargo collection");
                    foreach (var c in cargo) if (c != null && c.itemName == Product) contains = true;
                }
                if (!shelf && (produces || contains && (item.type & ItemType.EmployeeWorkstation) != 0))
                    return Refuse("Workstation/shared or converted input: unsupported");
                if (!counter && !contains && !produces) continue; // Storage is not a native producer.
                if (counter && (contains || produces || shelf)) return Refuse("Bag/counter and product producer sharing");
                if (item.itemsThatCanShowcase != null && item.itemsThatCanShowcase.Length > 128)
                    return Refuse("Oversized equipment whitelist");
                if (!counter && (!shelf || item.itemsThatCanShowcase == null
                    || Array.IndexOf(item.itemsThatCanShowcase, Product) < 0))
                {
                    if (shelf || produces) return Refuse("Equipment whitelist mismatch");
                    continue; // Product in a storage box contributes no direct stock.
                }
                if (item.producerSettings == null || !item.producerSettings.ResourcesRequired)
                    return Refuse("Unconsumed or unverified producer resources");
                if (cargo == null || cargo.Count != 1 || cargo[0] == null || cargo[0].amount < 0
                    || cargo[0].nestedCargoInstances == null || cargo[0].nestedCargoInstances.Count != 0)
                    return Refuse("Shared, nested or malformed cargo");
                var resource = cargo[0];
                if (counter)
                {
                    var bag = ItemsGetter.GetByName(resource.itemName, true);
                    var bagTags = bag == null ? null : ReadAuthoredTags(bag);
                    if (bagTags == null || !bagTags.Contains("ba:itemtag_isbag")) return Refuse("Counter resource is not a native bag");
                    if (resource.amount > int.MaxValue - bags) return Refuse("Bag stock overflow");
                    bags += resource.amount; counters++;
                }
                else
                {
                    if (resource.itemName != Product) return Refuse("Converted input: resource differs from product");
                    if (outputs != null && outputs.Length > 0 && (outputs.Length != 1 || outputs[0] != Product))
                        return Refuse("Shared producer outputs");
                    if (resource.amount > int.MaxValue - stock) return Refuse("Product stock overflow");
                    stock += resource.amount;
                    equipment.Add(instance.itemName);
                }
            }
            if (equipment.Count == 0) return Refuse("No direct stocked candidate showcase shelf observed");
            if (counters == 0) return Refuse("No bag counter observed");
            return new DirectStockFields("", price, stock, bags, counters, equipment.ToArray());
        }
    }
}
