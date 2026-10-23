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
    }

    /// <summary>On-demand current-hour snapshot for one stocked product at a player-owned
    /// full-service shop. Native setup derives its actual eligible furniture/staff constraints.
    /// Never simulates customers, restocks, creates registrations or calls sale methods.
    /// One product only: a shared bag pool must not become independent stock for several offers.</summary>
    public static class FoodSupplyReader
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Producers = typeof(RetailBusinessSimulator).GetField("availableProducers", Hidden)!;
        private static readonly FieldInfo Counters = typeof(RetailBusinessSimulator).GetField("pointOfSales", Hidden)!;
        private static readonly MethodInfo Capacity = typeof(RetailBusinessSimulator).GetMethod("GetMaxCustomerCapacityThisHour", Hidden)!;

        public static FoodSupplySnapshot Read(BuildingRegistration registration, string productId)
        {
            if (registration == null || string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("A shop and one product are required.");
            var save = SaveGameManager.Current ?? throw new InvalidOperationException("No current city.");
            var result = new FoodSupplySnapshot { AtMinute = save.Day * 1440.0 + save.Hour * 60.0 + save.Minute, ProductId = productId };
            if (!registration.RentedByPlayer)
            { result.Evidence = "Rival business: native player-stock and staff accounting does not establish its supply"; return result; }
            var type = BusinessTypeHelper.GetData(registration.businessTypeName);
            if (type?.simulator == null || type.simulator.GetType() != typeof(FullServiceBusinessSimulator))
            { result.Evidence = "Unsupported native business simulator; full-service counter pilot only"; return result; }
            if (registration.temporarilyClosed || !registration.HasEstablishedBusiness)
            { result.Units = 0; result.MealsPerHour = 0; result.Evidence = "Business is closed or not established"; return result; }
            if (registration.itemInstances == null || registration.itemInstances.Count > 2000
                || Producers == null || Counters == null || Capacity == null)
                throw new InvalidOperationException("Unsupported native supply contract.");
            var simulator = ScriptableObject.CreateInstance<FullServiceBusinessSimulator>();
            try
            {
                simulator.SetUp(registration, save.Hour);
                var counters = (List<PointOfSale>)Counters.GetValue(simulator)!;
                var producers = (Dictionary<string, List<ItemInstance>>)Producers.GetValue(simulator)!;
                result.StaffedCounters = counters.Count;
                bool bags = type.HasTag(TagRef.Businesstag.customersneedpaperbags);
                foreach (var counter in counters)
                {
                    var cargo = counter.instance.cargoInstances;
                    if (cargo == null || cargo.Count != 1 || cargo[0].amount < 0)
                    { result.Evidence = "Counter stock is malformed or unobserved"; return result; }
                    if (cargo[0].amount > 0) { result.StockedCounters++; result.BagUnits = checked(result.BagUnits + cargo[0].amount); }
                }
                int capacity = (int)Capacity.Invoke(simulator, Array.Empty<object>())!;
                if (capacity < 0 || capacity > 10000) throw new InvalidOperationException("Native capacity out of range.");
                // The native full-service simulator selects a stocked point of sale even when
                // a business does not separately advertise bags as a requirement.
                result.MealsPerHour = result.StockedCounters == 0 ? 0 : capacity;
                if (producers.TryGetValue(productId, out var productProducers))
                    foreach (var producer in productProducers)
                    {
                        var cargo = producer.cargoInstances;
                        if (!producer.ItemCached.producerSettings.ResourcesRequired || cargo == null || cargo.Count != 1
                            || cargo[0].itemName != productId || cargo[0].amount < 0)
                        { result.Evidence = "Product requires shared/converted resources or unverified producer stock; outside stocked-product pilot"; return result; }
                        result.ProducerUnits = checked(result.ProducerUnits + cargo[0].amount);
                        result.Producers.Add(producer.id);
                    }
                result.Units = bags ? Math.Min(result.ProducerUnits, result.BagUnits) : result.ProducerUnits;
                result.Evidence = "Current-hour native eligible producers, staffed stocked counters and capacity; "
                    + "one selected product, finite observed stock, no assumed replenishment or future staffing";
                return result;
            }
            finally { UnityEngine.Object.Destroy(simulator); }
        }
    }
}
