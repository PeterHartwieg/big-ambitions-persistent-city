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
using Entities;
using HarmonyLib;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;

namespace FoodNativePilotProbe
{
    // Rig-only integration harness. Never included in the normal mod package.
    public sealed partial class Probe
    {
        private const string Product = "ba:itemname_croissant";
        private const string PatchId = "persistentcity.isolated.foodpilot";
        private static BuildingRegistration? pilotShop;
        private static bool approving;
        private static float approvedPrice;
        private static int suppressedBatches, suppressedDraws;
        private readonly JArray receipts = new JArray(), decisions = new JArray(), daily = new JArray();
        private FoodRoutineCapture? household;
        private CitySimulator? pilotObservationSource;
        private FoodSupplySnapshot? supply;
        private FoodBusiness? pilotBusiness;
        private CitizenState[]? candidates;
        private int candidateIndex, triggerDay = -1, startDay, lastDaily = -1;
        private bool armed;
        private CitizenState? buyer;
        private FoodConsumer? buyerPolicy;
        private double arrival, serveAt;
        private bool bought;
        private string intentKey = "";
        private string productSource = "", counterSource = "";
        private double maxRouteMs, maxReadMs;

        private object SetupPilot()
        {
            if (!Allowed() || !SaveGameManager.Current.SaveGameName.StartsWith("FT Food Native Pilot ", StringComparison.Ordinal))
                throw new InvalidOperationException("An explicit disposable food-pilot city is required.");
            if (pilotShop != null) throw new InvalidOperationException("Pilot already set up.");
            using (var observer = Runtime.ObserveFoodRoutines()) household = observer.Finish();
            var homes = Simulation.State.Places.Where(p => p.Kind == PlaceKind.Home).ToArray();
            var places = Simulation.State.Places.ToDictionary(p => p.Id);
            pilotShop = CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null)
                .Select(c => c.buildingRegistration).Where(r => !r.RentedByPlayer && r.HasEstablishedBusiness
                    && r.cachedAvailableProducts.Contains(Product) && places.ContainsKey(PlaceId(r))
                    && BusinessTypeHelper.GetData(r.businessTypeName).simulator?.GetType() == typeof(FullServiceBusinessSimulator))
                .OrderByDescending(r => homes.Count(h => Position.Distance(h.Position, places[PlaceId(r)].Position) < 35))
                .ThenBy(r => r.customerCapacity).ThenBy(PlaceId, StringComparer.Ordinal).First();
            if (pilotShop.itemInstances.Count != 0) throw new InvalidOperationException("Fixture refuses a furnished business.");
            var harmony = new Harmony(PatchId);
            harmony.Patch(AccessTools.Method(typeof(RetailBusinessSimulator), "SimulateCurrentHour"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(SuppressNativeBatch)) { priority = Priority.First });
            harmony.Patch(AccessTools.Method(typeof(CustomerEntriesHelper), "GetEntriesByAddress"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(SuppressNativeDraw)) { priority = Priority.First });
            harmony.Patch(AccessTools.Method(typeof(CitizenData), "IsPriceAcceptable", new[] { typeof(string), typeof(float) }),
                prefix: new HarmonyMethod(typeof(Probe), nameof(ApproveResidentPrice)) { priority = Priority.First });
            pilotShop.RentedByPlayer = true; pilotShop.AvailableForRent = false; pilotShop.temporarilyClosed = false;
            pilotShop.BusinessName = "Persistent City food pilot";
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
                pilotShop.itemInstances.Add(support.id, support);
                counter.parentId = support.id;
                support.stackedItems.Add(new AttachableChild { childId = counter.id, childItemName = counter.itemName, attachmentIndex = 0 });
            }
            if (ItemHelper.GetMissingRequirements(counter).Any()) throw new InvalidOperationException("Native counter requirements remain unmet.");
            var producer = AddItem("ba:itemname_bakeryshowcase", Product, 300);
            productSource = producer.id; counterSource = counter.id;
            var employee = RecruitmentHelper.GenerateCandidate(counter.ItemCached.suitableSkills[0], 75, pilotShop.Address);
            if (employee == null) throw new InvalidOperationException("Native recruitment supplied no employee.");
            EmployeeHelper.HireCandidate(employee); employee.satisfaction = 100;
            foreach (var day in pilotShop.scheduleDays)
            {
                day.isOpen = true; day.openingHourSlots = new List<OpeningHourSlot> { new OpeningHourSlot { startingHour = 8, endingHour = 18 } };
                day.workShifts.Clear();
                foreach (var opening in day.openingHourSlots)
                    day.workShifts.Add(new WorkShift { employeeId = employee.id, itemInstanceId = counter.id,
                        startingHour = opening.startingHour, endingHour = opening.endingHour });
            }
            employee.UpdateWeeklyHoursAndDays(); employee.UpdateAssignedWorkStationItems();
            places[PlaceId(pilotShop)].WeeklyHours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 480, ToMinute = 1080 }).ToList();
            pilotShop.cachedAvailableProducts = new List<string> { Product };
            pilotShop.retailPrices.RemoveAll(p => p.itemName != Product);
            pilotShop.unprocessedCompletedOrders.Clear();
            BusinessHelper.UpdateCustomerCapacity(pilotShop);
            if (CustomerEntriesHelper.GetEntriesByAddress(pilotShop.Address).Count != 0)
                throw new InvalidOperationException("Native fixture customer draw was not suppressed.");
            startDay = SaveGameManager.Current.Day + 1;
            armed = true;
            pilotObservationSource = Simulation;
            pilotObservationSource.ActivityObserved += OnPilotActivity;
            var setup = new { shop = PlaceId(pilotShop), pilotShop.BusinessName, pilotShop.customerCapacity, startDay,
                register = counterName, product = Product, quantity = 300, staffSkill = 75,
                counterTypes = counter.ItemCached.type.ToString(), requirements = ItemHelper.GetMissingRequirements(counter).Select(q => q.GetType().FullName).ToArray(),
                counterScore = employee.GetCustomerSatisfaction(counter), availableEmployee = EmployeeHelper.GetEmployeeAtStationAndHour(pilotShop, counter.id)?.id,
                initialSupply = ReadSupply(), hours = "Controlled daily 08:00–18:00",
                note = "Disposable native fixture: converted unfurnished rival, stocked bakery case and staffed register. One controlled home-origin opportunity per day; no native demand on this shop. Normal game untouched." };
            File.WriteAllText(Path.Combine(Root, "pilot-setup.json"), JsonConvert.SerializeObject(setup, Formatting.Indented));
            return setup;
        }

        private ItemInstance AddItem(string name, string stocked, int units)
        {
            var item = ItemHelper.InitializeNewInstance(name);
            item.streetName = pilotShop!.StreetName; item.streetNumber = pilotShop.StreetNumber;
            item.cargoInstances = new List<CargoInstance> { new CargoInstance(stocked, units, 1f) };
            pilotShop.itemInstances.Add(item.id, item);
            return item;
        }

        private static bool SuppressNativeBatch(RetailBusinessSimulator __instance)
        {
            if (pilotShop == null || !ReferenceEquals(AccessTools.Field(typeof(Helpers.BusinessSimulation.BusinessSimulator), "buildingRegistration").GetValue(__instance), pilotShop)) return true;
            suppressedBatches++; return false;
        }
        private static bool SuppressNativeDraw(Address address, ref List<CustomerEntry> __result)
        {
            if (pilotShop == null || address != pilotShop.Address) return true;
            suppressedDraws++; __result = new List<CustomerEntry>(); return false;
        }
        private static bool ApproveResidentPrice(string itemName, float price, ref bool __result)
        {
            if (!approving || itemName != Product) return true;
            __result = Math.Abs(price - approvedPrice) < .001f; return false;
        }

        private FoodSupplySnapshot ReadSupply()
        {
            long at = Stopwatch.GetTimestamp(); var result = FoodSupplyReader.Read(pilotShop!, Product);
            maxReadMs = Math.Max(maxReadMs, (Stopwatch.GetTimestamp() - at) * 1000.0 / Stopwatch.Frequency); return result;
        }
        private void TickPilot()
        {
            if (!armed || pilotShop == null || household == null) return;
            int day = SaveGameManager.Current.Day, hour = SaveGameManager.Current.Hour;
            double nativeMinute = day * 1440.0 + hour * 60 + SaveGameManager.Current.Minute;
            if (nativeMinute - Simulation.State.Minute > .001) return;
            if (lastDaily != day)
            {
                lastDaily = day; daily.Add(JObject.FromObject(new { day, productUnits = pilotShop.itemInstances[productSource].cargoInstances[0].amount,
                    bagUnits = pilotShop.itemInstances[counterSource].cargoInstances[0].amount,
                    pendingOrders = pilotShop.unprocessedCompletedOrders.Count,
                    history = pilotShop.orderHistory.Select(h => new { h.dayNumber, h.totalRevenue, h.totalCustomers }).ToArray() }));
                SavePilotEvidence();
            }
            if (buyer != null)
            {
                if (arrival > 0 && !bought && Simulation.State.Minute >= serveAt)
                {
                    bought = true;
                    if (buyer.Phase == CitizenPhase.AtPlace && buyer.PlaceId == PlaceId(pilotShop)) Purchase();
                    else receipts.Add(JObject.FromObject(new { intentKey, refused = "Resident no longer at counter" }));
                    SavePilotEvidence();
                }
                if (buyer.Phase == CitizenPhase.AtPlace && buyer.PlaceId == buyer.HomeId)
                {
                    decisions.Add(JObject.FromObject(new { intentKey, returnedAt = Simulation.State.Minute, buyer.CompletedJourneys }));
                    buyer = null; SavePilotEvidence();
                }
                return;
            }
            if (day < startDay || day >= startDay + 3 || hour != 10) return;
            if (triggerDay != day)
            {
                triggerDay = day; supply = ReadSupply();
                var market = FoodBusinessCatalogue.Read(new[] { PlaceId(pilotShop) }, new[] { Product });
                pilotBusiness = market.Businesses.Single(); pilotBusiness.MealsPerHour = supply.MealsPerHour;
                pilotBusiness.ServiceEvidence = supply.Evidence; pilotBusiness.Offers.Single().Units = supply.Units;
                var shop = Simulation.State.Places.Single(p => p.Id == PlaceId(pilotShop));
                candidates = Simulation.State.Citizens.Where(p => p.Phase == CitizenPhase.AtPlace && p.Activity == Activity.Home && p.PlaceId == p.HomeId
                    && Math.Min(p.NextEventMinute, p.ActivityUntilMinute) - Simulation.State.Minute >= 25)
                    .OrderBy(p => Position.Distance(Simulation.PositionOf(p), shop.Position)).Take(40).ToArray(); candidateIndex = 0;
            }
            if (candidates == null || candidateIndex >= candidates.Length || supply?.MealsPerHour == null || supply.MealsPerHour <= 0 || supply.Units <= 0) return;
            TryCandidate(candidates[candidateIndex++]); // at most two native walking searches per frame
        }

        private void TryCandidate(CitizenState person)
        {
            if (person.Phase != CitizenPhase.AtPlace || person.Activity != Activity.Home || person.PlaceId != person.HomeId) return;
            double now = Simulation.State.Minute;
            double deadline = Math.Min(now + 75, Math.Min(person.NextEventMinute, person.ActivityUntilMinute));
            if (deadline - now < 25) return;
            var shop = Simulation.State.Places.Single(p => p.Id == PlaceId(pilotShop!));
            var home = Simulation.State.Places.Single(p => p.Id == person.HomeId);
            var routes = (IRoutePlanner)typeof(PopulationRuntime).GetField("routes", Fields)!.GetValue(Runtime)!;
            var preference = new TravelPreferences { OwnsCar = false, AllowTransit = false, WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres };
            long started = Stopwatch.GetTimestamp(); var outward = routes.Plan(home, shop, now, preference); var back = routes.Plan(shop, home, now, preference);
            maxRouteMs = Math.Max(maxRouteMs, (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
            if (outward == null || back == null || outward.Legs.Any(l => l.Mode != TravelMode.Walk) || back.Legs.Any(l => l.Mode != TravelMode.Walk)) return;
            var resident = household!.Residents.Single(r => r.Identity.Id == person.Identity.Id);
            var c = new FoodConsumer { Id = person.Identity.Id, Name = person.Identity.Name, OriginPlaceId = home.Id,
                ValueOfTimeCentsPerMinute = 4 + (int)(StableRandom.Hash(person.Identity.Id + ":food-time", household.Seed) % 9),
                ServiceQualityValueCents = 100 + (int)(StableRandom.Hash(person.Identity.Id + ":food-quality", household.Seed) % 301),
                DistrictAppealValueCents = 50 + (int)(StableRandom.Hash(person.Identity.Id + ":food-district", household.Seed) % 151) };
            FoodHouseholdPolicy.SetAllowance(c, resident, household.Seed, household.HomeDistrictMixes.SingleOrDefault(m => m.DistrictId == resident.HomeDistrictId), null);
            var intent = new FoodIntent { Id = FoodDemand.IntentId(c.Id, triggerDay), ConsumerId = c.Id, Day = triggerDay, RequestedAt = now,
                DesiredAt = now, Deadline = deadline, OriginPlaceId = home.Id, OriginActivity = Activity.Home, EatingMinutes = 15, MaximumQueueMinutes = 10,
                ContextEvidence = "Controlled pilot meal opportunity at actual home; return before the resident's next scheduled event" };
            FoodHouseholdPolicy.SetOutsideOption(c, resident, intent, household.Seed);
            var state = FoodDemand.CreateObserved(household.CityKey, "Explicit live pilot decision; current native supply; controlled opportunity time", triggerDay, 1,
                new[] { c }, new[] { pilotBusiness! }, new[] { new FoodTravelQuote { ConsumerId = c.Id, BusinessId = pilotBusiness!.Id, IntentId = intent.Id,
                    OriginPlaceId = home.Id, Minutes = outward.Minutes, ReturnMinutes = back.Minutes, Evidence = "Complete native walking routes" } }, new[] { intent });
            state.Scope = "Controlled live pilot opportunity at actual home. Native supply and routes at decision time; selected trip belongs to the population scheduler. Provisional spending allowance, no household ledger.";
            var model = new FoodDemand(state); model.AdvanceTo(now, 1); var choice = state.Intents.Single();
            decisions.Add(JObject.FromObject(new { choice.Id, status = choice.Status.ToString(), c.DailyAllowanceCents, c.HomeDistrictId,
                outward = outward.Minutes, back = back.Minutes, choice.Outcome }));
            if (choice.Status != FoodIntentStatus.Travelling) return;
            double service = 60.0 / supply!.MealsPerHour!.Value;
            if (!Simulation.TryBeginOptionalShopVisit(c.Id, shop.Id, outward, back.Minutes, 15 + service, deadline)) return;
            buyer = person; buyerPolicy = c; intentKey = choice.Id; arrival = 0; serveAt = 0; bought = false;
            candidates = null; // one purchased outing per day in this bounded integration test
            File.WriteAllText(Path.Combine(Root, "decision-" + triggerDay + ".json"), FoodShadowCodec.Write(state));
            SavePilotEvidence();
        }

        private void OnPilotActivity(string id, CitizenState person, double at)
        {
            if (buyer == null || id != buyer.Identity.Id || arrival > 0 || person.Phase != CitizenPhase.AtPlace || person.Activity != Activity.Shop
                || person.PlaceId != PlaceId(pilotShop!)) return;
            arrival = at; serveAt = at + 60.0 / supply!.MealsPerHour!.Value;
        }

        private void Purchase()
        {
            var current = ReadSupply(); var price = pilotShop!.retailPrices.Single(p => p.itemName == Product).price;
            if (!current.Units.HasValue || current.Units <= 0 || !current.MealsPerHour.HasValue || current.MealsPerHour <= 0
                || Math.Round(price * 100) > buyerPolicy!.DailyAllowanceCents || !BusinessHelper.IsBusinessOpen(pilotShop))
            { receipts.Add(JObject.FromObject(new { intentKey, refused = "Revalidated supply, hours or affordability failed", current })); return; }
            var beforeFood = pilotShop.itemInstances[productSource].cargoInstances[0].amount;
            var beforeBags = pilotShop.itemInstances[counterSource].cargoInstances[0].amount;
            int beforeOrders = pilotShop.unprocessedCompletedOrders.Count;
            var customer = new CustomerEntry { spawnTime = TimeHelper.Now(), order = new Order { entries = new List<OrderEntry> { new OrderEntry { itemName = Product } } } };
            var simulator = ScriptableObject.CreateInstance<FullServiceBusinessSimulator>();
            try
            {
                simulator.SetUp(pilotShop, SaveGameManager.Current.Hour); approvedPrice = price; approving = true;
                AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessCustomer").Invoke(simulator, new object[] { customer });
            }
            finally { approving = false; UnityEngine.Object.Destroy(simulator); }
            var paid = customer.order.entries.Single(e => e.itemName == Product);
            var receipt = new { intentKey, resident = buyer!.Identity.Id, arrival, servedAt = Simulation.State.Minute, paid = paid.paid, price = paid.price,
                foodBefore = beforeFood, foodAfter = pilotShop.itemInstances[productSource].cargoInstances[0].amount,
                bagsBefore = beforeBags, bagsAfter = pilotShop.itemInstances[counterSource].cargoInstances[0].amount,
                ordersBefore = beforeOrders, ordersAfter = pilotShop.unprocessedCompletedOrders.Count, completed = customer.order.completed,
                nativeSale = true, note = "Native retail ProcessCustomer executed after actual admission and service; revenue awaits normal daily settlement" };
            receipts.Add(JObject.FromObject(receipt));
            if (!paid.paid || receipt.foodAfter != beforeFood - 1 || receipt.bagsAfter != beforeBags - 1 || receipt.ordersAfter != beforeOrders + 1)
                throw new InvalidOperationException("Native purchase conservation failed.");
        }

        private object SupplyChecks()
        {
            if (pilotShop == null) throw new InvalidOperationException("Set up pilot first.");
            string before = JsonConvert.SerializeObject(pilotShop);
            var baseline = ReadSupply(); string afterRead = JsonConvert.SerializeObject(pilotShop);
            var producer = pilotShop.itemInstances[productSource].cargoInstances[0]; var counter = pilotShop.itemInstances[counterSource].cargoInstances[0];
            int food = producer.amount, bags = counter.amount; bool closed = pilotShop.temporarilyClosed;
            var shifts = pilotShop.scheduleDays.SelectMany(d => d.workShifts).ToArray(); var employees = shifts.Select(s => s.employeeId).ToArray();
            FoodSupplySnapshot emptyFood, emptyBags, unstaffed, closedShop;
            try
            {
                producer.amount = 0; emptyFood = ReadSupply(); producer.amount = food;
                counter.amount = 0; emptyBags = ReadSupply(); counter.amount = bags;
                foreach (var shift in shifts) shift.employeeId = null!;
                unstaffed = ReadSupply(); for (int i = 0; i < shifts.Length; i++) shifts[i].employeeId = employees[i];
                pilotShop.temporarilyClosed = true; closedShop = ReadSupply();
            }
            finally
            {
                producer.amount = food; counter.amount = bags; pilotShop.temporarilyClosed = closed;
                for (int i = 0; i < shifts.Length; i++) shifts[i].employeeId = employees[i];
            }
            var result = new { baseline, emptyFood, emptyBags, unstaffed, closedShop,
                readUnchanged = before == afterRead, restored = before == JsonConvert.SerializeObject(pilotShop),
                checks = new { foodZero = emptyFood.Units == 0, bagsPreventService = emptyBags.MealsPerHour == 0,
                    staffPreventService = unstaffed.MealsPerHour == 0, closurePreventsService = closedShop.MealsPerHour == 0 } };
            File.WriteAllText(Path.Combine(Root, "supply-checks.json"), JsonConvert.SerializeObject(result, Formatting.Indented)); return result;
        }
        private object PilotStatus() => new { armed, startDay, triggerDay, minute = Simulation.State.Minute, suppressedBatches, suppressedDraws,
            candidateIndex, candidates = candidates?.Length, decisionSupply = supply,
            buyerId = buyer?.Identity.Id, buyerPhase = buyer?.Phase.ToString(), arrival, serveAt, bought,
            receipts, decisions, daily, maxRouteMs, maxReadMs, supply = pilotShop == null ? null : ReadSupply(),
            homeMixes = household?.HomeDistrictMixes, note = "Bounded rig-only fixture; controlled meal opportunities, real resident journeys and native stock/order settlement" };
        private void SavePilotEvidence() => File.WriteAllText(Path.Combine(Root, "pilot-status.json"), JsonConvert.SerializeObject(PilotStatus(), Formatting.Indented));
        private void StopPilot()
        {
            armed = false;
            if (pilotObservationSource != null) { pilotObservationSource.ActivityObserved -= OnPilotActivity; pilotObservationSource = null; }
            if (pilotShop != null) { new Harmony(PatchId).UnpatchAll(PatchId); pilotShop = null; }
        }
    }
}
