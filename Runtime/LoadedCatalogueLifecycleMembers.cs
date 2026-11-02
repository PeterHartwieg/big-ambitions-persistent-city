using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using Helpers;

namespace PersistentCity.Runtime
{
    /// <summary>Read-only discovery, not installed notifications. E5b owns hooks and completeness proof.
    /// The inspected methods are cited in docs/checks/e5a-catalogue-eligibility.md.</summary>
    public static class LoadedCatalogueLifecycleMembers
    {
        public static IReadOnlyList<MethodInfo> Discover()
        {
            const BindingFlags all = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var found = new List<MethodInfo>();
            void Require(Type type, string name, params Type[] arguments)
            {
                var method = type.GetMethod(name, all, null, arguments, null);
                if (method == null) throw new NotSupportedException("Unverified native lifecycle member: " + type.FullName + "." + name);
                found.Add(method);
            }
            Require(typeof(ItemsGetter), "OnItemsLoaded", typeof(IList<Item>));
            Require(typeof(ItemsGetter), "RegisterModItem", typeof(Item));
            Require(typeof(ItemsGetter), "UnregisterModItem", typeof(string));
            Require(typeof(ItemsGetter), "ResetStaticData");
            Require(typeof(BusinessHelper), "UpdateCachedAvailableProducts", typeof(BuildingRegistration));
            Require(typeof(BuildingRegistration), "AddItemInstanceToBuilding", typeof(ItemInstance));
            Require(typeof(BuildingRegistration), "RemoveItemInstanceFromBuilding", typeof(ItemInstance), typeof(bool));
            Require(typeof(BuildingRegistration), "OnBusinessTypeChanged");
            Require(typeof(BuildingRegistration), "Reset", typeof(bool));
            Require(typeof(ItemHelper), "FillUpShowcaseShelfOrPointOfSale", typeof(ItemInstance));
            Require(typeof(ItemHelper), "SubtractFromStock", typeof(ItemInstance), typeof(int));
            Require(typeof(ItemInstance), "AddToCargo", typeof(CargoInstance), typeof(int));
            Require(typeof(ItemInstance), "RemoveFromCargo", typeof(CargoInstance));
            Require(typeof(ItemInstance), "ReduceFromCargo", typeof(CargoInstance), typeof(int));
            Require(typeof(ItemInstance), "MergeCargo", typeof(CargoInstance), typeof(CargoInstance));
            Require(typeof(ItemInstance), "MergeCargo", typeof(CargoInstance), typeof(CargoInstance), typeof(int));
            Require(typeof(CargoInstance), "MergeAmount", typeof(CargoInstance), typeof(int));
            Require(typeof(ReStockingHelper), "RefillSingleItemWithLimit", typeof(ItemInstance));
            Require(typeof(ReStockingHelper), "TryAddStockAmount", typeof(ItemInstance), typeof(CargoInstance), typeof(int));
            return found.AsReadOnly();
        }
    }
}
