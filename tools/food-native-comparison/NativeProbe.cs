using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI.Citizens;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Items;
using Extensions;
using BigAmbitions.DayNightCycle;
using Buildings.Retail.Simulation;
using Entities;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: RegisterModClass(typeof(FoodComparisonProbe.Entry))]
namespace FoodComparisonProbe
{
    [ModEntryOnCityLoad] public sealed class Entry : IModBigAmbitions
    {
        GameObject? host;
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context)
        {
            if (Probe.Allowed()) { host = new GameObject("Food comparison diagnostic"); host.AddComponent<Probe>().Init(context.ModRootPath); }
            return Task.CompletedTask;
        }
        public Task OnUnloadAsync() { if (host != null) Object.Destroy(host); host = null; return Task.CompletedTask; }
    }
    public sealed class Probe : MonoBehaviour
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        string root = "", last = ""; float poll;
        public static bool Allowed() => Environment.GetCommandLineArgs().Contains("-agent-tests") &&
            (Application.persistentDataPath.EndsWith("/com.hovgaardgames.bigambitions.agenttests.mac", StringComparison.Ordinal)
            || Application.persistentDataPath.Replace('\\', '/').EndsWith("/Hovgaard Games/Big Ambitions Agent Tests", StringComparison.Ordinal));
        public void Init(string path) { root = path; }
        void Update()
        {
            if (!Allowed() || SaveGameManager.Current?.SaveGameName?.StartsWith("FT", StringComparison.Ordinal) != true || Time.unscaledTime < poll) return;
            poll = Time.unscaledTime + .5f;
            string path = Path.Combine(root, "request.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 2048) return;
            string text = File.ReadAllText(path); if (text == last) return; last = text;
            string id = "invalid";
            try
            {
                var request = JObject.Parse(text); id = (string)request["id"]!;
                if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,60}$")) return;
                if ((string?)request["op"] != "capture") throw new InvalidOperationException("Only capture is supported.");
                var result = Capture(); File.WriteAllText(Path.Combine(root, "response-" + id + ".json"), result.ToString());
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(root, "response-" + id + ".json"), new JObject { ["error"] = e.ToString() }.ToString()); }
        }
        static JObject Capture()
        {
            var randomBefore = UnityEngine.Random.state;
            var save = SaveGameManager.Current;
            string before = Fingerprint();
            var markets = new JArray();
            var allFood = CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null)
                .Select(c => c.buildingRegistration).Where(r => r.HasEstablishedBusiness && !r.AvailableForRent &&
                (r.businessTypeName == "ba:businesstype_fastfoodrestaurant" || r.businessTypeName == "ba:businesstype_coffeeshop" || r.businessTypeName == "ba:businesstype_supermarket"))
                .OrderBy(r => r.StreetName, StringComparer.Ordinal).ThenBy(r => r.StreetNumber).ToArray();
            string[] allowed = { "ba:itemname_burger", "ba:itemname_hotdog", "ba:itemname_pizza", "ba:itemname_croissant", "ba:itemname_salad" };
            var eligible = allFood.Where(r => r.retailPrices != null && r.cachedAvailableProducts != null && r.retailPrices.Any(p => allowed.Contains(p.itemName) && r.cachedAvailableProducts.Contains(p.itemName) && p.price > 0)).ToArray();
            var common = allowed.OrderByDescending(p => eligible.Count(r => r.retailPrices.Any(x => x.itemName == p && r.cachedAvailableProducts.Contains(p)))).First();
            var selected = eligible.Where(r => r.retailPrices.Any(x => x.itemName == common && r.cachedAvailableProducts.Contains(common)))
                .Take(4).ToArray();
            try
            {
                foreach (var reg in selected)
                {
                    var hours = new JArray();
                    foreach (var day in reg.scheduleDays ?? new List<ScheduleDay>())
                        if (day.isOpen && !reg.temporarilyClosed) foreach (var slot in day.openingHourSlots)
                            hours.Add(new JObject { ["Day"] = (int)day.day - 1, ["FromMinute"] = slot.startingHour * 60, ["ToMinute"] = slot.endingHour * 60 });
                    var business = new JObject { ["Id"] = PlaceId(reg), ["PlaceId"] = PlaceId(reg), ["Name"] = reg.BusinessName,
                        ["Hours"] = hours, ["Offers"] = new JArray(reg.retailPrices.Where(p => allowed.Contains(p.itemName) && reg.cachedAvailableProducts.Contains(p.itemName))
                            .Select(p => new JObject { ["ProductId"] = p.itemName, ["PriceCents"] = (int)Math.Round(p.price * 100), ["Units"] = null })) };
                    var calc = new CustomerEntriesCalculatorRetail(); calc.Init(reg);
                    var hourly = new JArray();
                    float initial = calc.GetInitialCustomers();
                    for (int hour = 0; hour < 24; hour++) hourly.Add(new JObject { ["hour"] = hour, ["open"] = Helpers.BusinessHelper.IsBusinessOpen(reg, hour),
                        ["potentialCustomers"] = calc.GetCustomersByHour(hour, TimeHelper.GetDayOfWeek(), initial) });
                    var curves = new JArray();
                    foreach (var price in reg.retailPrices.Where(p => allowed.Contains(p.itemName) && reg.cachedAvailableProducts.Contains(p.itemName)))
                    {
                        var byClass = new JArray();
                        foreach (SocialClass social in Enum.GetValues(typeof(SocialClass)))
                        {
                            var citizen = new CitizenData { Neighbourhood = reg.Neighborhood, SocialClass = social };
                            var points = new JArray();
                            foreach (double factor in new[] { .5, .75, 1, 1.1, 1.25, 1.5, 2, 3 })
                            {
                                float amount = (float)Math.Round(price.price * factor, 2);
                                var clone = (BuildingRegistration)typeof(object).GetMethod("MemberwiseClone", Fields)!.Invoke(reg, null)!;
                                clone.retailPrices = reg.retailPrices.Select(p => new RetailPrice { itemName = p.itemName, price = p.itemName == price.itemName ? amount : p.price }).ToList();
                                var simulator = ScriptableObject.CreateInstance<RetailBusinessSimulator>();
                                try
                                {
                                    typeof(Helpers.BusinessSimulation.BusinessSimulator).GetField("buildingRegistration", Fields)!.SetValue(simulator, clone);
                                    var entry = new OrderEntry { itemName = price.itemName };
                                    typeof(RetailBusinessSimulator).GetMethod("CompleteOrderEntry", Fields)!.Invoke(simulator, new object?[] { entry, citizen, null });
                                    typeof(RetailBusinessSimulator).GetField("businessType", Fields)!.SetValue(simulator, Helpers.BusinessTypeHelper.GetData(reg.businessTypeName));
                                    var noService = new OrderEntry { itemName = price.itemName };
                                    typeof(RetailBusinessSimulator).GetMethod("ProcessItemInOrder", Fields)!.Invoke(simulator, new object?[] { noService, citizen, null });
                                    var noStock = new OrderEntry { itemName = price.itemName };
                                    var point = new PointOfSale { instance = new ItemInstance("ba:itemname_cashregister") {
                                        cargoInstances = new List<CargoInstance> { new CargoInstance { itemName = "ba:itemname_paperbag", amount = 100 } } }, customerService = 100 };
                                    typeof(RetailBusinessSimulator).GetMethod("ProcessItemInOrder", Fields)!.Invoke(simulator, new object?[] { noStock, citizen, point });
                                    points.Add(new JObject { ["factor"] = factor, ["requestedPrice"] = amount, ["nativePrice"] = entry.price, ["nativePaid"] = entry.paid,
                                        ["citizenAccepts"] = citizen.IsPriceAcceptable(price.itemName, amount), ["nativePaidWithoutService"] = noService.paid, ["nativePaidWithoutProducer"] = noStock.paid });
                                }
                                finally { Object.Destroy(simulator); }
                            }
                            byClass.Add(new JObject { ["socialClass"] = social.ToString(), ["points"] = points });
                        }
                        curves.Add(new JObject { ["product"] = price.itemName, ["basePrice"] = price.price, ["classes"] = byClass });
                    }
                    var closed = (BuildingRegistration)typeof(object).GetMethod("MemberwiseClone", Fields)!.Invoke(reg, null)!;
                    closed.temporarilyClosed = true;
                    markets.Add(new JObject { ["business"] = business, ["neighbourhood"] = reg.Neighborhood,
                        ["type"] = reg.businessTypeName, ["nativeCapacity"] = reg.customerCapacity, ["hourlyScope"] = "Retail calculator potential at current promotion/capacity; before AI random thinning, basket composition, stock and checkout; not observed sales", ["nativeInitialCustomers"] = initial,
                        ["nativeHourly"] = hourly, ["priceCurves"] = curves, ["nativeClosedHoursOpen"] = Enumerable.Range(0, 24).Count(h => Helpers.BusinessHelper.IsBusinessOpen(closed, h)),
                        ["classShares"] = JToken.FromObject(new { working = NeighborhoodHelper.GetData(reg.Neighborhood).workingClassPercentage,
                            middle = NeighborhoodHelper.GetData(reg.Neighborhood).middleClassPercentage,
                            upper = NeighborhoodHelper.GetData(reg.Neighborhood).upperClassClassPercentage }) });
                }
            }
            finally { UnityEngine.Random.state = randomBefore; }
            string after = Fingerprint();
            if (before != after) throw new InvalidOperationException("Observed native financial/market fields changed during capture.");
            var methods = new[] { typeof(CitizenData).GetMethod("IsPriceAcceptable"), typeof(ItemHelper).GetMethod("CalculateMaxAcceptablePrice"),
                typeof(CustomerEntriesCalculatorRetail).GetMethod("GetCustomersByHour"), typeof(RetailBusinessSimulator).GetMethod("CompleteOrderEntry", Fields) };
            return new JObject { ["evidence"] = "Actual native methods in an isolated loaded FT game. Detached price quotes; no sales booked. Current mod patches apply.",
                ["save"] = save.SaveGameName, ["day"] = save.Day, ["hour"] = save.Hour, ["minute"] = save.Minute,
                ["gameAssembly"] = typeof(CitizenData).Assembly.FullName, ["foodBusinessesObserved"] = allFood.Length, ["eligibleBusinesses"] = eligible.Length,
                ["commonProduct"] = common, ["catalogueSummary"] = new JArray(allFood.Select(r => new JObject { ["name"] = r.BusinessName, ["type"] = r.businessTypeName, ["advertised"] = new JArray(r.cachedAvailableProducts ?? new List<string>()) })), ["markets"] = markets, ["nativeMarketUnchanged"] = before == after,
                ["patches"] = new JArray(methods.Where(m => m != null).Select(m => new JObject { ["method"] = m!.DeclaringType!.FullName + "." + m.Name,
                    ["owners"] = new JArray(Harmony.GetPatchInfo(m)?.Owners ?? Enumerable.Empty<string>()) })) };
        }
        static string PlaceId(BuildingRegistration r) => "address:" + Uri.EscapeDataString(r.StreetName) + ":" + r.StreetNumber + ":shop";
        static string Fingerprint() => JsonConvert.SerializeObject(SaveGameManager.Current.BuildingRegistrations.Select(r => new {
            r.StreetName, r.StreetNumber, r.retailPrices, r.temporarilyClosed, r.scheduleDays, r.unprocessedCompletedOrders, r.orderHistory,
            stock = r.itemInstances?.Select(i => new { i.Key, cargo = i.Value.cargoInstances.Select(c => new { c.itemName, c.amount, c.pricePerUnit }) }) }).ToArray());
    }
}
