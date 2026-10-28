using System;
using System.Linq;
using Buildings.Retail.Simulation;
using Entities;
using Helpers;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private bool? skipEnergySetting;
        private object? skipEnergySave;
        private object SkipNeeds(bool begin)
        {
            var save = SaveGameManager.Current;
            if (begin)
            {
                if (skipEnergySetting.HasValue) throw new InvalidOperationException("Skip needs fixture already active.");
                skipEnergySave = save; skipEnergySetting = save.gameVariables.disableEnergy;
                save.gameVariables.disableEnergy = true;
            }
            else
            {
                if (!skipEnergySetting.HasValue || !ReferenceEquals(skipEnergySave, save))
                    throw new InvalidOperationException("Skip needs fixture ownership changed.");
                save.gameVariables.disableEnergy = skipEnergySetting.Value;
                skipEnergySetting = null; skipEnergySave = null;
            }
            return new { active = skipEnergySetting.HasValue, save.gameVariables.disableEnergy,
                note = "Only the disposable test player's needs are suspended during native time skipping; resident finances and demand are unchanged." };
        }
        private BuildingRegistration[] EligibleNeighborhoodShops()
        {
            var places = Simulation.State.Places.ToDictionary(p => p.Id);
            return CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null)
                .Select(c => c.buildingRegistration).Where(r => !r.RentedByPlayer && r.HasEstablishedBusiness
                    && r.cachedAvailableProducts.Contains(Product) && places.ContainsKey(PlaceId(r)) && r.itemInstances.Count == 0
                    && BusinessTypeHelper.GetData(r.businessTypeName).simulator?.GetType() == typeof(FullServiceBusinessSimulator))
                .OrderBy(PlaceId, StringComparer.Ordinal).ToArray();
        }
        private object NeighborhoodCandidates()
        {
            var places = Simulation.State.Places.ToDictionary(p => p.Id);
            var homes = Simulation.State.Citizens.GroupBy(c => c.HomeId).ToDictionary(g => g.Key, g => g.Count());
            return new { minute = Simulation.State.Minute, citizens = Simulation.State.Citizens.Count, existingFoodScope = Simulation.State.Food?.Shops,
                candidates = EligibleNeighborhoodShops().Select(r => new { placeId = PlaceId(r), r.BusinessName, r.customerCapacity, buildingCapacity = r.BuildingCached.GetCustomerCapacity,
                    position = places[PlaceId(r)].Position, district = places[PlaceId(r)].District,
                    rent = r.BuildingCached.GetBuildingDailyMarketRent(),
                    nearbyResidents = homes.Where(h => Position.Distance(places[h.Key].Position, places[PlaceId(r)].Position) < 100).Sum(h => h.Value),
                    prices = r.retailPrices.Where(p => p.itemName == Product).ToArray() }).ToArray() };
        }
        private object CheckLiveHours()
        {
            var scope = Simulation.State.Food!.Shops.First();
            var shop = SaveGameManager.Current.BuildingRegistrations.Single(r => PlaceId(r) == scope.PlaceId);
            if (shop.BusinessName != "Persistent City food routines") throw new InvalidOperationException("Named disposable fixture required.");
            var native = (NativeFoodMarket)typeof(PopulationRuntime).GetField("foodMarket", Fields)!.GetValue(Runtime)!;
            var place = Simulation.State.Places.Single(p => p.Id == scope.PlaceId);
            string before = PersistentCity.Persistence.SaveCodec.Write(Simulation.State);
            var original = shop.scheduleDays.Select(d => d.openingHourSlots).ToArray();
            bool changed;
            try
            {
                foreach (var day in shop.scheduleDays)
                    day.openingHourSlots = new System.Collections.Generic.List<OpeningHourSlot> { new OpeningHourSlot { startingHour = 6, endingHour = 22 } };
                native.RefreshRoutineHours(Simulation.State.Minute, true);
                changed = place.WeeklyHours.Count == 7 && place.WeeklyHours.All(h => h.FromMinute == 360 && h.ToMinute == 1320);
            }
            finally
            {
                for (int i = 0; i < original.Length; i++) shop.scheduleDays[i].openingHourSlots = original[i];
                native.RefreshRoutineHours(Simulation.State.Minute, true);
            }
            bool restored = before == PersistentCity.Persistence.SaveCodec.Write(Simulation.State);
            if (!changed || !restored) throw new InvalidOperationException("Live hours did not update and restore exactly.");
            return new { changed, restored, note = "Temporary edit of the named fixture's native weekly hours reached the same saved place used for routine admission; restored before any game time advanced." };
        }
        private object SetupNeighborhood(JObject request)
        {
            if (!Allowed() || !SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines", StringComparison.Ordinal)
                || Simulation.State.Food != null) throw new InvalidOperationException("Disposable pre-food checkpoint required.");
            var ids = request["shops"]?.Values<string>().Select(id => id ?? throw new InvalidOperationException("Null shop ID.")).ToArray() ?? Array.Empty<string>();
            if (ids.Length < 1 || ids.Length > 4 || ids.Distinct().Count() != ids.Length) throw new InvalidOperationException("Select one to four distinct fixtures.");
            var available = EligibleNeighborhoodShops().ToDictionary(PlaceId);
            if (ids.Any(id => !available.ContainsKey(id))) throw new InvalidOperationException("Selected fixture is not eligible.");
            int skill = (int?)request["skill"] ?? 75, opens = (int?)request["opens"] ?? 8, closes = (int?)request["closes"] ?? 18;
            float factor = (float?)request["priceFactor"] ?? 1;
            if (skill < 10 || skill > 100 || opens < 0 || closes > 24 || closes <= opens || factor < .25 || factor > 5)
                throw new InvalidOperationException("Fixture policy outside diagnostic range.");
            var sharedBefore = ids.SelectMany((id, index) => ids.Skip(index + 1).Select(other => new {
                first = id, second = other,
                list = ReferenceEquals(available[id].scheduleDays, available[other].scheduleDays),
                days = available[id].scheduleDays.Count(d => available[other].scheduleDays.Any(e => ReferenceEquals(d, e))),
                shifts = available[id].scheduleDays.Count(d => available[other].scheduleDays.Any(e => ReferenceEquals(d.workShifts, e.workShifts)))
            })).ToArray();
            // Hold the offer price at the verified single-shop baseline for location
            // comparisons; inherited rival prices differ by district.
            var fixtures = ids.Select((id, index) => { pilotShop = null;
                return SetupPilot(false, available[id], skill, opens, closes, factor, 10.25f,
                    (string?)request["businessTypes"]?[index], request["menus"]?[index] == null || request["menus"]![index]!.Values<string>().Contains(Product)); }).ToArray();
            var menus = ids.Select((id, index) => AddFixtureMenu(available[id],
                request["menus"]?[index]?.Values<string>().Select(p => p!).ToArray() ?? new[] { Product },
                request["prices"] as JObject)).ToArray();
            pilotShop = available[ids[0]];
            Runtime.StartFoodRoutines(ids);
            Runtime.EnableHouseholdFinances();
            var staffing = FixtureStaffing();
            return new { fixtures, menus, sharedBefore, staffing, state = Status(), investmentBoundary = "Fixture equipment and opening inventory valued at native catalogue/wholesale prices, supplied as an explicit test endowment. Native daily rent, wages, resource consumption and sales remain native accounting. No free replenishment." };
        }
        private object ReplayMealReceipts()
        {
            const string prefix = "persistentcity:food-sale-v1:food:";
            var food = Simulation.State.Food!;
            var native = (NativeFoodMarket)typeof(PopulationRuntime).GetField("foodMarket", Fields)!.GetValue(Runtime)!;
            var before = Status();
            var results = new System.Collections.Generic.List<object>();
            var kinds = new System.Collections.Generic.HashSet<string>();
            foreach (var receipt in SaveGameManager.Current.modData.Where(p => FoodSaleReceipts.IsReceiptKey(p.Key) && p.Value.StartsWith("paid|", StringComparison.Ordinal)).ToArray())
            {
                var key = receipt.Key.Substring(prefix.Length).Split(':'); var value = receipt.Value.Split('|');
                if (key.Length != 3 || value.Length != 4 || !Enum.TryParse<MealKind>(key[1], true, out var kind) || kind == MealKind.Legacy || !kinds.Add(kind + ":" + value[3])) continue;
                var scope = food.Shops.Single(s => s.BusinessId == Uri.UnescapeDataString(value[2]) && s.ContainsProduct(Uri.UnescapeDataString(value[3])));
                var meal = new ResidentMeal { ResidentId = Uri.UnescapeDataString(key[0]), Kind = kind,
                    Day = int.Parse(key[2], System.Globalization.CultureInfo.InvariantCulture), PlaceId = scope.PlaceId, BusinessId = scope.BusinessId, ProductId = Uri.UnescapeDataString(value[3]),
                    Price = Money.TryParseCents(value[1], 28, out var agreed) ? agreed : throw new InvalidOperationException("Receipt price is not exact money.") };
                if (!food.Residents.Any(m => m.ResidentId == meal.ResidentId) || !FoodSaleReceipts.AlreadyPaid(SaveGameManager.Current.modData, meal))
                    throw new InvalidOperationException("Replay must identify an existing resident and confirmed saved receipt.");
                var result = native.Buy(meal, Simulation.State.Minute);
                if (!result.Paid) throw new InvalidOperationException("Saved receipt failed to confirm.");
                results.Add(new { kind = kind.ToString(), product = meal.ProductId, intention = meal.IntentId, result.Paid, result.Reason });
            }
            return new { results, before, after = Status() };
        }
        private object FixtureStaffing()
        {
            var registrations = Simulation.State.Food!.Shops.Select(s => SaveGameManager.Current.BuildingRegistrations.Single(r => PlaceId(r) == s.PlaceId)).ToArray();
            return registrations.Select(r => new { shop = PlaceId(r),
                days = r.scheduleDays.Select(d => new { day = (int)d.day, d.isOpen,
                    shifts = d.workShifts.Select(w => new { w.employeeId, w.itemInstanceId, w.startingHour, w.endingHour,
                        employeeExists = EmployeeHelper.GetEmployeeById(w.employeeId, false) != null,
                        stationExists = r.itemInstances.ContainsKey(w.itemInstanceId) }).ToArray() }).ToArray(),
                employees = EmployeeHelper.GetEmployeeInstances().Where(e => e.assignedAddress != null && e.assignedAddress.Equals(r.Address))
                    .Select(e => new { e.id, e.assignedWeeklyHours, e.hourlyWage, available = e.IsEmployeeAvailable() }).ToArray(),
                middayStaff = r.itemInstances.Values.Where(i => i.ItemCached.suitableSkills.Any())
                    .Select(i => new { station = i.id, employee = EmployeeHelper.GetEmployeeAtStationAndHour(r, i.id, 12)?.id }).ToArray()
            }).ToArray();
        }
    }
}
