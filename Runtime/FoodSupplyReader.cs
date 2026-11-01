using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Retail.Simulation;
using Helpers;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed class FoodSupplySnapshot
    {
        public double AtMinute { get; set; }
        public string ProductId { get; set; } = "";
        public int? Units { get; set; }
        public int? MealsPerHour { get; set; }
        public int StaffedCounters { get; set; }
        public int StockedCounters { get; set; }
        public int ProducerUnits { get; set; }
        public int BagUnits { get; set; }
        public List<string> Producers { get; set; } = new List<string>();
        public string Evidence { get; set; } = "Unverified native supply";
        public string AvailabilityReason { get; set; } = "";
    }

    /// <summary>On-demand current-hour snapshot for one stocked product at a player-owned
    /// full-service shop. Native setup derives its actual eligible furniture/staff constraints.
    /// Never simulates customers, restocks, creates registrations or calls sale methods.
    /// Menu stock is read in one setup. Bags are shared and rechecked at checkout, never multiplied by menu size.</summary>
    public static class FoodSupplyReader
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Producers = typeof(RetailBusinessSimulator).GetField("availableProducers", Hidden)!;
        private static readonly FieldInfo Counters = typeof(RetailBusinessSimulator).GetField("pointOfSales", Hidden)!;
        private static readonly MethodInfo Capacity = typeof(RetailBusinessSimulator).GetMethod("GetMaxCustomerCapacityThisHour", Hidden)!;
        private static readonly double MillisecondsPerTimestamp = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        /// <summary>Native simulator setups this reader has made, and their total time including stock inspection (diagnostic).</summary>
        public static long Setups { get; private set; }
        public static double SetupMilliseconds { get; private set; }

        public static FoodSupplySnapshot Read(BuildingRegistration registration, string productId)
            => ReadMenu(registration, new[] { productId })[productId];

        public static IReadOnlyDictionary<string, FoodSupplySnapshot> ReadMenu(BuildingRegistration registration, IEnumerable<string> products)
        {
            // Native callers already capture an immutable ordered menu array. Reuse it rather than
            // rebuilding a hash set and array on every refresh. Refuse duplicate/oversized menus.
            var ids = products as string[] ?? products.Take(FoodMenuPolicy.MaximumOffers+1).ToArray();
            if (registration == null || ids.Length == 0 || ids.Length > FoodMenuPolicy.MaximumOffers || ids.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("A shop and one to eight products are required.");
            for(int i=0;i<ids.Length;i++) for(int j=0;j<i;j++)
                if(string.Equals(ids[i],ids[j],StringComparison.Ordinal)) throw new ArgumentException("Duplicate menu product.");
            var save = SaveGameManager.Current ?? throw new InvalidOperationException("No current city.");
            var result = ids.ToDictionary(id => id, id => new FoodSupplySnapshot {
                AtMinute = save.Day * 1440.0 + save.Hour * 60.0 + save.Minute, ProductId = id }, StringComparer.Ordinal);
            void Evidence(string message) { foreach (var r in result.Values) r.Evidence = message; }
            if (!registration.RentedByPlayer)
            { Evidence("Rival business: native player-stock and staff accounting does not establish its supply"); return result; }
            var type = BusinessTypeHelper.GetData(registration.businessTypeName);
            if (type?.simulator == null || type.simulator.GetType() != typeof(FullServiceBusinessSimulator))
            { Evidence("Unsupported native business simulator; full-service counters only"); return result; }
            if (registration.temporarilyClosed || !registration.HasEstablishedBusiness)
            {
                foreach (var r in result.Values) { r.Units = 0; r.MealsPerHour = 0; }
                Evidence("Business is closed or not established"); return result;
            }
            if (registration.itemInstances == null || registration.itemInstances.Count > 2000
                || Producers == null || Counters == null || Capacity == null)
                throw new InvalidOperationException("Unsupported native supply contract.");
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Setups++;
            var simulator = ScriptableObject.CreateInstance<FullServiceBusinessSimulator>();
            try
            {
                simulator.SetUp(registration, save.Hour);
                var counters = (List<PointOfSale>)Counters.GetValue(simulator)!;
                var producers = (Dictionary<string, List<ItemInstance>>)Producers.GetValue(simulator)!;
                bool bags = type.HasTag(TagRef.Businesstag.customersneedpaperbags);
                int bagUnits = 0, stocked = 0;
                foreach (var counter in counters)
                {
                    var cargo = counter.instance.cargoInstances;
                    if (cargo == null || cargo.Count == 0) continue;
                    if (cargo.Count != 1 || cargo[0].amount < 0
                        || ItemsGetter.GetByName(cargo[0].itemName)?.HasTag(TagRef.Itemtag.isbag) != true)
                    { Evidence("Counter stock is malformed or unobserved"); return result; }
                    if (cargo[0].amount > 0) { stocked++; bagUnits = checked(bagUnits + cargo[0].amount); }
                }
                int capacity = (int)Capacity.Invoke(simulator, Array.Empty<object>())!;
                if (capacity < 0 || capacity > 10000) throw new InvalidOperationException("Native capacity out of range.");
                foreach (var r in result.Values)
                {
                    r.StaffedCounters = counters.Count; r.StockedCounters = stocked; r.BagUnits = bagUnits;
                    r.MealsPerHour = bags && stocked == 0 ? 0 : capacity;
                    r.AvailabilityReason = counters.Count == 0 ? "No staffed counter"
                        : bags && stocked == 0 ? "Out of stock" : "";
                    bool supported = true;
                    if (producers.TryGetValue(r.ProductId, out var productProducers))
                        foreach (var producer in productProducers)
                        {
                            var cargo = producer.cargoInstances;
                            if (!producer.ItemCached.producerSettings.ResourcesRequired || cargo == null || cargo.Count != 1
                                || cargo[0].itemName != r.ProductId || cargo[0].amount < 0
                                || (producer.ItemCached.type & ItemType.ShowcaseShelf) != 0
                                    && producer.ItemCached.itemsThatCanShowcase?.Contains(r.ProductId) != true)
                            { supported = false; break; }
                            r.ProducerUnits = checked(r.ProducerUnits + cargo[0].amount);
                            r.Producers.Add(producer.id);
                        }
                    if (!supported)
                    { r.Evidence = "Shared/converted resources or unverified product stock; offer excluded"; continue; }
                    r.Units = bags ? Math.Min(r.ProducerUnits, bagUnits) : r.ProducerUnits;
                    r.Evidence = "Current native eligible producers, staffed counters and shared bag pool; one stock unit per product; checkout revalidates contention";
                }
                return result;
            }
            finally
            {
                UnityEngine.Object.Destroy(simulator);
                SetupMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
            }
        }
    }
}
