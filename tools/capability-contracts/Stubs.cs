using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class GameManager { [MethodImpl(MethodImplOptions.NoInlining)] public void RunMainGameTick(float deltaTimeWithMultiplier) { } }
public sealed class BuildingRegistration { [MethodImpl(MethodImplOptions.NoInlining)] public void Reset(bool resetFields) { } }
public sealed class Address { }
namespace Entities { internal static class Marker { } }
namespace AI.Citizens { public sealed class CitizenData { [MethodImpl(MethodImplOptions.NoInlining)] public bool IsPriceAcceptable(string itemName, float price) => true; } }
namespace AI.Customers.CustomerEntries
{
    public sealed class CustomerEntry { }
    public static class CustomerEntriesHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public static List<CustomerEntry> GetEntriesByAddress(Address address) => new();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration registration, BigAmbitions.DayNightCycle.DayOfWeekOrdered day) { }
    }
}
namespace BigAmbitions.DayNightCycle { public enum DayOfWeekOrdered { Monday } }
namespace BigAmbitions.Items { public sealed class ItemInstance { } }
namespace Helpers.BusinessSimulation { public class BusinessSimulator { protected BuildingRegistration buildingRegistration = null!; protected int currentHour; } }
namespace Buildings.Retail.Simulation
{
    public sealed class PointOfSale { }
    public class RetailBusinessSimulator : Helpers.BusinessSimulation.BusinessSimulator
    {
        private Dictionary<string, List<BigAmbitions.Items.ItemInstance>> availableProducers = new();
        private List<PointOfSale> pointOfSales = new();
        [MethodImpl(MethodImplOptions.NoInlining)] private void ProcessAllCustomersFromThisHour() { }
        [MethodImpl(MethodImplOptions.NoInlining)] private void ProcessCustomer(AI.Customers.CustomerEntries.CustomerEntry customerEntry) { }
        [MethodImpl(MethodImplOptions.NoInlining)] private int GetMaxCustomerCapacityThisHour() => availableProducers.Count + pointOfSales.Count;
    }
}

namespace Changed
{
    public class RetailBusinessSimulator : Helpers.BusinessSimulation.BusinessSimulator
    {
        public static int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)] private void ProcessAllCustomersFromThisHour() { }
        [MethodImpl(MethodImplOptions.NoInlining)] private void ProcessCustomer(AI.Customers.CustomerEntries.CustomerEntry customerEntry, int seat) { Calls++; }
    }
}
