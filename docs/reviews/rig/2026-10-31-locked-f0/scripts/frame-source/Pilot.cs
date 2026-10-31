using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AI.Citizens;
using AI.Customers.CustomerEntries;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Retail.Simulation;
using BusinessLayoutSets;
using Entities;
using HarmonyLib;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private const string Product = NativeFoodMarket.Product;
        private BuildingRegistration? pilotShop;
        private int startDay;
        private string productSource = "", counterSource = "";
        private object SetupPilot(bool enrol = true, BuildingRegistration? selected = null, int skill = 75, int opens = 8, int closes = 18, float priceFactor = 1, float? basePrice = null, string? businessType = null, bool pastry = true)
        {
            if (!Allowed() || !SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines ", StringComparison.Ordinal))
                throw new InvalidOperationException("An explicit disposable food-pilot city is required.");
            if (pilotShop != null) throw new InvalidOperationException("Pilot already set up.");
            
            var homes = Simulation.State.Places.Where(p => p.Kind == PlaceKind.Home).ToArray();
            var places = Simulation.State.Places.ToDictionary(p => p.Id);
            pilotShop = selected ?? CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null)
                .Select(c => c.buildingRegistration).Where(r => !r.RentedByPlayer && r.HasEstablishedBusiness
                    && r.cachedAvailableProducts.Contains(Product) && places.ContainsKey(PlaceId(r))
                    && BusinessTypeHelper.GetData(r.businessTypeName).simulator?.GetType() == typeof(FullServiceBusinessSimulator))
                .OrderByDescending(r => homes.Count(h => Position.Distance(h.Position, places[PlaceId(r)].Position) < 35))
                .ThenBy(r => r.customerCapacity).ThenBy(PlaceId, StringComparer.Ordinal).First();
            if (pilotShop.itemInstances.Count != 0) throw new InvalidOperationException("Fixture refuses a furnished business.");
            if (businessType != null)
            {
                if (businessType != "ba:businesstype_coffeeshop" && businessType != "ba:businesstype_fastfoodrestaurant")
                    throw new InvalidOperationException("Unreviewed fixture business type.");
                pilotShop.businessTypeName = businessType;
            }
            pilotShop.RentedByPlayer = true; pilotShop.AvailableForRent = false; pilotShop.temporarilyClosed = false;
            pilotShop.BusinessName = "Persistent City food routines";
            pilotShop.Layout = null; pilotShop.businessOwnerRivalId = ""; pilotShop.takenOver = true;
            // Rival opening schedules can share native template objects. Give each
            // disposable staffed business its own schedule before adding any shifts.
            pilotShop.ResetScheduleDays();
            // Explicit fixture inventory, not inferred supply at the preexisting rival.
            var counterName = ItemsGetter.GetAllItemNamesByTag(TagRef.Itemtag.isfullservicecashregister)
                .OrderBy(n => n, StringComparer.Ordinal).First();
            var counter = AddItem(counterName, ItemsGetter.GetRandomBag(), 300);
            foreach (var requirement in ItemHelper.GetMissingRequirements(counter))
            {
                if (!(requirement is Furniture.Requirements.HasItemTypeAttached attached))
                    throw new InvalidOperationException("Unsupported fixture requirement: " + requirement.GetType().FullName);
                var supportName = attached.GetAllItemsFittingRequirement().OrderBy(n => n, StringComparer.Ordinal).First();
                var support = ItemHelper.InitializeNewInstance(supportName);
                support.streetName = pilotShop.StreetName; support.streetNumber = pilotShop.StreetNumber;
                PlaceFixture(support);
                pilotShop.AddItemInstanceToBuilding(support);
                counter.parentId = support.id;
                support.stackedItems.Add(new AttachableChild { childId = counter.id, childItemName = counter.itemName, attachmentIndex = 0 });
            }
            if (ItemHelper.GetMissingRequirements(counter).Any()) throw new InvalidOperationException("Native counter requirements remain unmet.");
            var producer = pastry ? AddItem("ba:itemname_bakeryshowcase", Product, 300) : null;
            productSource = producer?.id ?? ""; counterSource = counter.id;
            var employee = RecruitmentHelper.GenerateCandidate(counter.ItemCached.suitableSkills[0], skill, pilotShop.Address);
            if (employee == null) throw new InvalidOperationException("Native recruitment supplied no employee.");
            EmployeeHelper.HireCandidate(employee); employee.satisfaction = 100;
            foreach (var day in pilotShop.scheduleDays)
            {
                day.isOpen = true; day.openingHourSlots = new List<OpeningHourSlot> { new OpeningHourSlot { startingHour = opens, endingHour = closes } };
                day.workShifts.Clear();
                foreach (var opening in day.openingHourSlots)
                    day.workShifts.Add(new WorkShift { type = WorkShiftType.Default, employeeId = employee.id, itemInstanceId = counter.id,
                        startingHour = opening.startingHour, endingHour = opening.endingHour });
            }
            employee.UpdateWeeklyHoursAndDays(); employee.UpdateAssignedWorkStationItems();
            places[PlaceId(pilotShop)].WeeklyHours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = opens * 60, ToMinute = closes * 60 }).ToList();
            pilotShop.cachedAvailableProducts = new List<string> { Product };
            pilotShop.retailPrices.RemoveAll(p => p.itemName != Product);
            pilotShop.unprocessedCompletedOrders.Clear();
            BusinessHelper.UpdateCustomerCapacity(pilotShop);
            startDay = SaveGameManager.Current.Day + 1;
            if (!enrol)
            {
                pilotShop.RentPerDay = pilotShop.BuildingCached.GetBuildingDailyMarketRent();
                pilotShop.orderHistory.Clear();
                foreach (var item in pilotShop.itemInstances.Values)
                    foreach (var cargo in item.cargoInstances)
                        cargo.pricePerUnit = ItemsGetter.GetByName(cargo.itemName).wholesalePrice;
                foreach (var price in pilotShop.retailPrices)
                    price.price = (float)Math.Round((basePrice ?? price.price) * priceFactor, 2, MidpointRounding.AwayFromZero);
            }
            if (enrol) Runtime.StartFoodRoutines(new[] { PlaceId(pilotShop) });
            var setup = new { shop = PlaceId(pilotShop), pilotShop.BusinessName, pilotShop.customerCapacity, startDay, buildingCapacity = pilotShop.BuildingCached.GetCustomerCapacity,
                register = counterName, product = Product, quantity = 300, staffSkill = skill,
                counterTypes = counter.ItemCached.type.ToString(), requirements = ItemHelper.GetMissingRequirements(counter).Select(q => q.GetType().FullName).ToArray(),
                counterScore = employee.GetCustomerSatisfaction(counter), availableEmployee = EmployeeHelper.GetEmployeeAtStationAndHour(pilotShop, counter.id)?.id,
                employeeId = employee.id, counterId = counter.id,
                price = pilotShop.retailPrices.FirstOrDefault(p => p.itemName == Product)?.price,
                initialSupply = FoodSupplyReader.Read(pilotShop!, Product), hours = opens + ":00–" + closes + ":00",
                rentPerDay = pilotShop.RentPerDay, capitalAtNativeCataloguePrices = pilotShop.itemInstances.Values.Sum(i => i.ItemCached.DefaultMarketPrice),
                openingInventoryAtNativeWholesale = pilotShop.itemInstances.Values.SelectMany(i => i.cargoInstances).Sum(c => c.amount * c.pricePerUnit),
                note = "Disposable native fixture: converted unfurnished rival, stocked bakery case and staffed register. Automatic resident routines own demand; no scripted opportunities. Normal game untouched." };
            File.WriteAllText(Path.Combine(Root, "pilot-setup.json"), JsonConvert.SerializeObject(setup, Formatting.Indented));
            return setup;
        }

        private void PlaceFixture(ItemInstance item)
        {
            var building = pilotShop!.BuildingCached;
            var template = BusinessLayoutSetHelper.GetAllBusinessLayoutSets().Values
                .Where(l => l.BuildingSize == building.BuildingSize && l.BuildingVersion == building.BuildingVersion && l.Items != null)
                .OrderBy(l => l.BusinessType, StringComparer.Ordinal).ThenBy(l => l.LayoutName, StringComparer.Ordinal)
                .SelectMany(l => l.Items).FirstOrDefault(i => i.itemName == item.itemName && !SerializableVector3.IsZero(i.position));
            if (template == null) throw new InvalidOperationException("No nonzero native layout position for " + item.itemName);
            item.position = template.position; item.yRotation = ((Quaternion)template.rotation).eulerAngles.y;
        }

        private ItemInstance AddItem(string name, string stocked, int units)
        {
            var item = ItemHelper.InitializeNewInstance(name);
            item.streetName = pilotShop!.StreetName; item.streetNumber = pilotShop.StreetNumber;
            item.cargoInstances = new List<CargoInstance> { new CargoInstance(stocked, units, 1f) };
            PlaceFixture(item);
            pilotShop.AddItemInstanceToBuilding(item);
            return item;
        }

    }
}
