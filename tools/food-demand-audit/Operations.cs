using System;
using System.Collections.Generic;
using System.Linq;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings;
using Buildings.Office.Headquarters;
using UI.Smartphone.Apps.BizMan.Schedule;
using BusinessLayoutSets;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UI.Smartphone.Apps.BizMan;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    // Diagnostic only. Stock and money are never assigned here: ordinary native
    // 08:00 wholesale delivery owns both, including failure, fee and refund rules.
    public sealed partial class Probe
    {
        private const string OperationsMarker = "persistentcity:test-operations:";
        private const string OperationsProduct = "ba:itemname_croissant";
        private const string OperationsBags = "ba:itemname_paperbag";
        private object OperationsOperation(string op, JObject request)
        {
            var save = SaveGameManager.Current;
            string name = save?.SaveGameName ?? "";
            if (!Allowed() || !System.Text.RegularExpressions.Regex.IsMatch(name, "^FT Food Routines Ownership [A-Za-z0-9_-]{1,40}$")
                || !save!.modData.TryGetValue("persistentcity:test-ownership-baseline", out var marker) || marker != name)
                throw new InvalidOperationException("Marked disposable Ownership copy on the isolated agent-test rig required.");
            if (op == "operations-catalogue") return OperationsCatalogue();
            string place = (string?)request["shop"] ?? throw new InvalidOperationException("Exact shop place ID required.");
            var shop = save.BuildingRegistrations.Single(r => PlaceId(r) == place);
            if (op == "operations-status") return OperationsStatus(shop);
            if (Time.timeScale != 0 || (string?)request["expectedName"] != name)
                throw new InvalidOperationException("Mutation requires the exact paused disposable copy.");
            if (op == "operations-setup") return OperationsSetup(shop, request);
            if (!save.modData.TryGetValue(OperationsMarker + place, out var initialized) || initialized != "ready"
                || !shop.RentedByPlayer || shop.BusinessName != "Persistent City food routines")
                throw new InvalidOperationException("Completed operations fixture required; failed setup branches must be discarded.");
            if (op == "operations-order") return OperationsOrder(shop, request);
            if (op == "operations-recruit-fit") return OperationsRecruitFit(shop, request);
            if (op == "operations-price") return OperationsPrice(shop, request);
            if (op == "operations-staff-gap" || op == "operations-staff-restore") return OperationsStaffGap(shop, request, op.EndsWith("restore", StringComparison.Ordinal));
            throw new InvalidOperationException("Unknown operations diagnostic command.");
        }

        private object OperationsCatalogue()
        {
            var layouts = BusinessLayoutSetHelper.GetAllBusinessLayoutSets();
            return new {
                shops = SaveGameManager.Current.BuildingRegistrations.Where(r => !r.RentedByPlayer && r.HasEstablishedBusiness
                    && r.itemInstances.Count == 0 && r.businessTypeName == "ba:businesstype_coffeeshop"
                    && r.BuildingCached.GetCustomerCapacity >= 30).Select(r => new { shop = PlaceId(r), r.BusinessName,
                        r.BuildingCached.BuildingSize, r.BuildingCached.BuildingVersion, buildingCapacity = r.BuildingCached.GetCustomerCapacity,
                        layouts = layouts.Where(p => p.Value.BusinessType == r.businessTypeName
                            && p.Value.BuildingSize == r.BuildingCached.BuildingSize && p.Value.BuildingVersion == r.BuildingCached.BuildingVersion)
                            .Select(p => new { key = p.Key, p.Value.LayoutName,
                                registers = p.Value.Items.Count(i => ItemsGetter.GetByName(i.itemName).HasTag(TagRef.Itemtag.isfullservicecashregister)),
                                registerCapacities = p.Value.Items.Where(i => ItemsGetter.GetByName(i.itemName).HasTag(TagRef.Itemtag.isfullservicecashregister))
                                    .Select(i => new { i.itemName, ItemsGetter.GetByName(i.itemName).addedCustomersPerHour }).ToArray(),
                                bakeryCases = p.Value.Items.Count(i => i.itemName == "ba:itemname_bakeryshowcase"),
                                storage = p.Value.Items.Count(i => ItemsGetter.GetByName(i.itemName).HasTag(TagRef.Itemtag.isbusinessstorage)) }).ToArray() }).ToArray(),
                suppliers = SaveGameManager.Current.BuildingRegistrations.Where(r => r.BuildingCached.SpecialService?.settings is WholesaleStoreSettings)
                    .Select(r => new { supplier = PlaceId(r), r.BusinessName, products = r.GetListOfItemsForSale()
                        .Where(p => p == OperationsProduct || p == OperationsBags).ToArray(),
                        deliveryFee = ((WholesaleStoreSettings)r.BuildingCached.SpecialService.settings).deliveryFee }).ToArray(),
                hourDemands = JobDemandHelper.TypeDemands.Select(JobDemandHelper.GetByName).OfType<HoursWorkingPerWeek>()
                    .Select(d => new { d.demandName, d.MinHours, d.MaxHours }).ToArray(),
                nextRegularDeliveryDay = DeliveryHelper.GetNextDeliveryDay(), regularDeliveryHour = 8,
                urgentMultiplier = DeliveryHelper.GetWholesaleUrgentFeeMultiplier(), locked = DeliveryHelper.IsLockPeriod()
            };
        }

        private object OperationsSetup(BuildingRegistration shop, JObject request)
        {
            int counters = (int?)request["counters"] ?? 2;
            var save = SaveGameManager.Current;
            string key = OperationsMarker + PlaceId(shop);
            if (counters < 2 || counters > 3 || save.modData.ContainsKey(key) || shop.RentedByPlayer
                || !shop.HasEstablishedBusiness || shop.itemInstances.Count != 0 || shop.businessTypeName != "ba:businesstype_coffeeshop"
                || shop.BuildingCached.GetCustomerCapacity < 30 || (string?)request["confirmation"] != "create-empty-operations-fixture")
                throw new InvalidOperationException("Fresh unfurnished large café, 2–3 registers and explicit initial-fixture instruction required.");
            if (BuildingManager.Instance?.buildingRegistration == shop)
                throw new InvalidOperationException("Leave the target interior before preparing its initial fixture.");
            string layoutKey = (string?)request["layout"] ?? throw new InvalidOperationException("Choose an exact native layout from operations-catalogue.");
            var source = BusinessLayoutSetHelper.GetAllBusinessLayoutSets()[layoutKey];
            if (source.BusinessType != shop.businessTypeName || source.BuildingSize != shop.BuildingCached.BuildingSize
                || source.BuildingVersion != shop.BuildingCached.BuildingVersion)
                throw new InvalidOperationException("Layout/building mismatch.");
            // Detached clone: never modify the game's shared layout catalogue. Keep
            // exact native positions/attachments; choose only the verified pastry menu.
            var layout = JsonConvert.DeserializeObject<BusinessLayoutSet>(JsonConvert.SerializeObject(source))!;
            var registers = layout.Items.Where(i => ItemsGetter.GetByName(i.itemName).HasTag(TagRef.Itemtag.isfullservicecashregister))
                .Take(counters).ToArray();
            if (registers.Length != counters || registers.Any(i => SerializableVector3.IsZero(i.position)))
                throw new InvalidOperationException("Insufficient native register placements.");
            for (int i = 0; i < registers.Length; i++) for (int j = i + 1; j < registers.Length; j++)
                if (Vector3.Distance((Vector3)registers[i].position, (Vector3)registers[j].position) < .5f)
                    throw new InvalidOperationException("Native register slots overlap.");
            var selected = new HashSet<string>(registers.Select(i => i.id));
            foreach (var item in layout.Items.Where(i => i.itemName == "ba:itemname_bakeryshowcase"
                || ItemsGetter.GetByName(i.itemName).HasTag(TagRef.Itemtag.isbusinessstorage))) selected.Add(item.id);
            for (int pass = 0; pass < 8; pass++)
            {
                int before = selected.Count;
                foreach (var item in layout.Items.Where(i => selected.Contains(i.id)).ToArray())
                    if (!string.IsNullOrEmpty(item.parentId)) selected.Add(item.parentId);
                foreach (var item in layout.Items.Where(i => i.stackedItems != null && i.stackedItems.Any(c => selected.Contains(c.childId)))) selected.Add(item.id);
                if (before == selected.Count) break;
            }
            layout.Items = layout.Items.Where(i => selected.Contains(i.id)).ToList();
            if (!layout.Items.Any(i => i.itemName == "ba:itemname_bakeryshowcase")
                || !layout.Items.Any(i => ItemsGetter.GetByName(i.itemName).HasTag(TagRef.Itemtag.isbusinessstorage)))
                throw new InvalidOperationException("Native pastry cases and business storage required.");
            foreach (var item in layout.Items)
            {
                item.stackedItems = (item.stackedItems ?? new List<AttachableChild>()).Where(c => selected.Contains(c.childId)).ToList();
                if (item.itemName == "ba:itemname_bakeryshowcase")
                    item.playerItemPurchaserSettings = new PlayerItemPurchaserSettings { enabled = false, itemName = OperationsProduct, itemQuantity = 0 };
            }
            string HourDemand(int hours) => JobDemandHelper.TypeDemands.Select(JobDemandHelper.GetByName).OfType<HoursWorkingPerWeek>()
                .Where(d => d.MinHours <= hours && hours <= d.MaxHours).OrderBy(d => d.MaxHours).ThenBy(d => d.demandName, StringComparer.Ordinal)
                .FirstOrDefault()?.demandName ?? throw new InvalidOperationException("No native weekly-hours demand accepts " + hours);
            string forty = HourDemand(40), thirty = HourDemand(30);
            save.modData[key] = "setup-in-progress"; // No retry can duplicate assets or hires after partial failure.
            shop.RentedByPlayer = true; shop.AvailableForRent = false; shop.temporarilyClosed = false;
            shop.BusinessName = "Persistent City food routines"; shop.Layout = null; shop.businessOwnerRivalId = ""; shop.takenOver = true;
            shop.ResetScheduleDays();
            BusinessLayoutSetHelper.InsertLayoutSet(shop, layout, shouldRandomlyFillShelves: false);
            var activeCounters = shop.itemInstances.Values.Where(i => i.ItemCached.HasTag(TagRef.Itemtag.isfullservicecashregister)).OrderBy(i => i.id).ToArray();
            if (activeCounters.Length != counters || shop.itemInstances.Values.Any(i => ItemHelper.GetMissingRequirements(i).Any())
                || shop.itemInstances.Values.SelectMany(i => i.cargoInstances).Any(c => c.amount != 0))
                throw new InvalidOperationException("Native furniture validation failed or nonzero cargo appeared; discard this setup branch.");
            foreach (var counter in activeCounters) counter.GetStockInstance().itemName = OperationsBags;
            foreach (var day in shop.scheduleDays)
            {
                day.isOpen = true; day.openingHourSlots = new List<OpeningHourSlot> { new OpeningHourSlot { startingHour = 8, endingHour = 18 } };
                day.workShifts.Clear();
            }
            foreach (var counter in activeCounters)
            {
                var first = RecruitmentHelper.GenerateCandidate(counter.ItemCached.suitableSkills[0], 75, shop.Address, new List<string> { forty });
                var second = RecruitmentHelper.GenerateCandidate(counter.ItemCached.suitableSkills[0], 75, shop.Address, new List<string> { thirty });
                EmployeeHelper.HireCandidate(first); EmployeeHelper.HireCandidate(second);
                foreach (var day in shop.scheduleDays)
                {
                    int d = (int)day.day; var employee = d == 1 || d == 2 || d == 4 || d == 5 ? first : second;
                    day.AddWorkShift(new WorkShift { type = WorkShiftType.Default, employeeId = employee.id,
                        itemInstanceId = counter.id, startingHour = 8, endingHour = 18 });
                }
                first.UpdateWeeklyHoursAndDays(); first.UpdateAssignedWorkStationItems();
                second.UpdateWeeklyHoursAndDays(); second.UpdateAssignedWorkStationItems();
            }
            shop.RentPerDay = shop.BuildingCached.GetBuildingDailyMarketRent();
            shop.cachedAvailableProducts = new List<string> { OperationsProduct };
            shop.retailPrices = new List<RetailPrice> { new RetailPrice { itemName = OperationsProduct, price = 10.25f } };
            BusinessHelper.UpdateCustomerCapacity(shop);
            save.modData[key] = "ready";
            pilotShop = shop;
            GameEvent.Invoke("ba:gameevent_changedbuildingregistration");
            return new { layoutKey, initialEquipmentValue = shop.itemInstances.Values.Sum(i => i.ItemCached.DefaultMarketPrice),
                initialGoods = 0, note = "Initial fixture assets only; no paid goods yet. Native hours demand retained, other demands/absence unmodified. Capacity is not verified until a staffed opening hour.", status = OperationsStatus(shop) };
        }

        private object OperationsOrder(BuildingRegistration shop, JObject request)
        {
            string supplierId = (string?)request["supplier"] ?? throw new InvalidOperationException("Exact supplier ID required.");
            var supplier = SaveGameManager.Current.BuildingRegistrations.Single(r => PlaceId(r) == supplierId);
            if (!(supplier.BuildingCached.SpecialService?.settings is WholesaleStoreSettings settings)
                || !shop.itemInstances.Values.Any(i => i.ItemCached.HasTag(TagRef.Itemtag.isbusinessstorage)))
                throw new InvalidOperationException("Actual native wholesaler and destination business storage required.");
            var rows = request["items"] as JArray ?? throw new InvalidOperationException("Explicit order lines required.");
            if (rows.Count < 1 || rows.Count > 2) throw new InvalidOperationException("Only pastry and paper-bag lines supported.");
            var existing = SaveGameManager.Current.DeliveryContracts.SingleOrDefault(c => c.businessAddress == shop.Address && c.wholesaleAddress == supplier.Address);
            if (existing != null && (existing.enabled || !DeliveryHelper.CanModifyContract(existing.nextDeliveryDay)))
                throw new InvalidOperationException("Existing pending/locked contract cannot be replaced.");
            var contract = existing == null ? new DeliveryContract { wholesaleAddress = supplier.Address, businessAddress = shop.Address,
                deliveryFee = settings.deliveryFee, items = new List<DeliveryContractItem>() }
                : JsonConvert.DeserializeObject<DeliveryContract>(JsonConvert.SerializeObject(existing))!;
            var seen = new HashSet<string>();
            foreach (var row in rows)
            {
                string product = (string?)row["product"] ?? ""; int units = (int?)row["units"] ?? 0;
                if (!seen.Add(product) || (product != OperationsProduct && product != OperationsBags) || units <= 0 || units > 5000
                    || !supplier.GetListOfItemsForSale().Contains(product)) throw new InvalidOperationException("Unavailable or out-of-bound order line.");
                var item = contract.items.SingleOrDefault(i => i.itemName == product);
                if (item == null) { item = new DeliveryContractItem { itemName = product }; contract.items.Add(item); }
                var model = new BizManDeliveriesProductModel(item, false, false, () => { }, _ => { }, shop.Address, supplier.Address);
                model.UpdateAmount(units); // Native maximum / already-ordered-this-week clamp.
                if (item.amount != units) throw new InvalidOperationException("Native weekly order limit refused requested units: " + product);
            }
            foreach (var item in contract.items.Where(i => !seen.Contains(i.itemName))) item.amount = 0;
            contract.repeatingOrder = false; contract.isUrgentOrder = false;
            contract.enabled = true; contract.UpdateNextDeliveryDay();
            if ((bool?)request["urgent"] == true && contract.nextDeliveryDay != SaveGameManager.Current.Day + 1)
            { contract.nextDeliveryDay = SaveGameManager.Current.Day + 1; contract.isUrgentOrder = true; }
            if (!contract.HasItemsToDeliver() || contract.TotalPricePerDelivery <= 0 || float.IsNaN(contract.TotalPricePerDelivery)
                || float.IsInfinity(contract.TotalPricePerDelivery)) throw new InvalidOperationException("No valid native paid order quote.");
            if (existing != null) SaveGameManager.Current.DeliveryContracts.Remove(existing);
            SaveGameManager.Current.DeliveryContracts.Add(contract);
            GameEvent.Invoke(existing == null ? "ba:gameevent_newdeliverycontract" : "ba:gameevent_updateddeliverycontract");
            return new { booked = true, paid = false, priceAtRequest = contract.TotalPricePerDelivery,
                contract.nextDeliveryDay, deliveryHour = 8, contract.isUrgentOrder, contract.items,
                note = "Native contract model mirrors manager/BizMan validation; no phone UI/messages simulated. Resume normal clock for native paid delivery. Quote may change with market events." };
        }

        private object OperationsPrice(BuildingRegistration shop, JObject request)
        {
            float price = (float?)request["price"] ?? float.NaN, expected = (float?)request["expectedPrice"] ?? float.NaN;
            if (float.IsNaN(price) || float.IsInfinity(price) || price < .01f || price > 100
                || float.IsNaN(expected) || float.IsInfinity(expected) || Math.Abs(price * 100.0 - Math.Round(price * 100.0)) > .001)
                throw new InvalidOperationException("Finite price in cents between0.01 and100 and expected previous price required.");
            if (PricingManagerHelper.GetPlanCoveringNeighborhood(shop.Neighborhood) != null)
                throw new InvalidOperationException("Native pricing-manager control disables manual editing.");
            var retail = shop.retailPrices.Single(p => p.itemName == OperationsProduct);
            if (float.IsNaN(retail.price) || float.IsInfinity(retail.price)
                || Math.Abs(retail.price - expected) > .0001f || !shop.cachedAvailableProducts.Contains(OperationsProduct))
                throw new InvalidOperationException("Existing croissant offer must match expected previous price.");
            var stored = shop.storedRetailPrices.SingleOrDefault(p => p.itemName == OperationsProduct);
            if (stored == null) { stored = new RetailPrice { itemName = OperationsProduct, price = retail.price }; shop.storedRetailPrices.Add(stored); }
            float before = retail.price;
            retail.price = price; stored.price = price;
            ItemHelper.GetLowestMarketPrice(OperationsProduct, shop.Neighborhood, forceUpdate: true);
            SaveGameManager.MarkChange();
            return new { product = OperationsProduct, before, price = retail.price, storedPrice = stored.price, status = OperationsStatus(shop) };
        }

        private sealed class StaffingGap
        {
            public int Day;
            public int Weekday;
            public int FromHour;
            public int ToHour;
            public string Fingerprint = "";
            public List<WorkShift> Before = new List<WorkShift>();
            public List<string> Employees = new List<string>();
            public List<string> Stations = new List<string>();
        }
        private static string ScheduleFingerprint(BuildingRegistration shop)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(shop.scheduleDays))));
        }
        private object OperationsStaffGap(BuildingRegistration shop, JObject request, bool restore)
        {
            var save = SaveGameManager.Current; string key = OperationsMarker + PlaceId(shop) + ":staff-gap";
            StaffingGap record;
            if (restore)
            {
                if (!save.modData.TryGetValue(key, out var json)) throw new InvalidOperationException("No owned staffing gap to restore.");
                record = JsonConvert.DeserializeObject<StaffingGap>(json)!;
                if (record == null || record.Day != save.Day || record.Fingerprint != ScheduleFingerprint(shop)
                    || record.Stations.Any(id => !shop.itemInstances.ContainsKey(id))
                    || record.Employees.Any(id => EmployeeHelper.GetEmployeeById(id, false)?.assignedAddress != shop.Address))
                    throw new InvalidOperationException("Only the same day's unchanged owned schedule removal may be restored; use ordinary Schedule UI after other edits.");
                var day = shop.scheduleDays.Single(d => (int)d.day == record.Weekday);
                foreach (var shift in day.workShifts.ToArray()) day.RemoveWorkShift(shift);
                foreach (var shift in record.Before) day.AddWorkShift(shift);
            }
            else
            {
                if (save.modData.ContainsKey(key)) throw new InvalidOperationException("Existing staffing gap must be restored first.");
                int from = (int?)request["fromHour"] ?? -1, to = (int?)request["toHour"] ?? -1;
                if (from < save.Hour || from < 8 || to > 18 || to <= from || to - from > 2)
                    throw new InvalidOperationException("A current/future one-to-two-hour interval inside08–18 today is required.");
                int weekday = (int)TimeHelper.GetDayOfWeek(save.Day);
                var day = shop.scheduleDays.Single(d => (int)d.day == weekday);
                var registers = shop.itemInstances.Values.Where(i => i.ItemCached.HasTag(TagRef.Itemtag.isfullservicecashregister)).Select(i => i.id).ToHashSet();
                var removed = day.workShifts.Where(w => registers.Contains(w.itemInstanceId) && w.startingHour < to && from < w.endingHour).ToArray();
                if (!day.isOpen || removed.Length < 2 || removed.Length > 3 || removed.Select(w => w.itemInstanceId).Distinct().Count() != registers.Count)
                    throw new InvalidOperationException("All2–3 native register shifts must be present for a bounded service gap.");
                if (removed.Any(w => string.IsNullOrEmpty(w.employeeId)
                    || EmployeeHelper.GetEmployeeById(w.employeeId, false)?.assignedAddress != shop.Address
                    || !shop.itemInstances.ContainsKey(w.itemInstanceId)))
                    throw new InvalidOperationException("Every removed native shift must retain an existing assigned employee and station.");
                record = new StaffingGap { Day = save.Day, Weekday = weekday, FromHour = from, ToHour = to,
                    Before = JsonConvert.DeserializeObject<List<WorkShift>>(JsonConvert.SerializeObject(day.workShifts))!,
                    Employees = removed.Select(w => w.employeeId).Distinct().ToList(), Stations = registers.ToList() };
                foreach (var shift in removed)
                {
                    day.RemoveWorkShift(shift);
                    if (shift.startingHour < from)
                    { var part = JsonConvert.DeserializeObject<WorkShift>(JsonConvert.SerializeObject(shift))!; part.endingHour = from; day.AddWorkShift(part); }
                    if (to < shift.endingHour)
                    { var part = JsonConvert.DeserializeObject<WorkShift>(JsonConvert.SerializeObject(shift))!; part.startingHour = to; day.AddWorkShift(part); }
                }
                record.Fingerprint = ScheduleFingerprint(shop);
                save.modData[key] = JsonConvert.SerializeObject(record);
            }
            foreach (string id in record.Employees)
            {
                var employee = EmployeeHelper.GetEmployeeById(id, false) ?? throw new InvalidOperationException("Gap employee no longer exists.");
                employee.UpdateWeeklyHoursAndDays(); employee.UpdateAssignedWorkStationItems();
            }
            if (restore) save.modData.Remove(key);
            ScheduleHelper.OnWorkShiftChanged?.Invoke();
            ScheduleHelper.RequestScheduleScrollerReload.Invoke(false);
            SaveGameManager.MarkChange();
            return new { restored = restore, record.Day, record.FromHour, record.ToHour, record.Fingerprint,
                note = "Native schedule gap only; no absence or wage counters changed. Normal work resumes after the bounded interval even before original shift shape is restored.", status = OperationsStatus(shop) };
        }

        private object OperationsRecruitFit(BuildingRegistration shop, JObject request)
        {
            if ((string?)request["confirmation"] != "replace-incompatible-fixture-staff")
                throw new InvalidOperationException("Explicit incompatible-fixture staffing replacement required.");
            var ids = (request["employees"] as JArray)?.Values<string>().ToArray() ?? Array.Empty<string>();
            if (ids.Length < 1 || ids.Length > 2 || ids.Distinct().Count() != ids.Length)
                throw new InvalidOperationException("Select one or two exact incompatible fixture employees.");
            var originals = ids.Select(id => EmployeeHelper.GetEmployeeById(id, false)
                ?? throw new InvalidOperationException("Unknown employee.")).ToArray();
            foreach (var employee in originals)
                if (employee.IsCandidate || employee.assignedAddress != shop.Address || employee.assignedWeeklyHours > 40
                    || !employee.demands.Any(n => !JobDemandHelper.GetByName(n).Fulfilled(employee, shop.scheduleDays)))
                    throw new InvalidOperationException("Only currently incompatible staff assigned to this fixture may be selected.");
            var accepted = new List<EmployeeInstance>();
            var hired = new List<EmployeeInstance>();
            var attempts = new List<object>();
            var originalSchedule = shop.scheduleDays;
            var proposed = JsonConvert.DeserializeObject<List<ScheduleDay>>(JsonConvert.SerializeObject(originalSchedule))!;
            try
            {
                foreach (var original in originals)
                {
                    var hoursDemand = original.demands.Select(JobDemandHelper.GetByName).OfType<HoursWorkingPerWeek>()
                        .FirstOrDefault(d => d.MinHours <= original.assignedWeeklyHours && original.assignedWeeklyHours <= d.MaxHours)
                        ?? throw new InvalidOperationException("Original native hours demand must accept this exact rota.");
                    EmployeeInstance? fit = null;
                    for (int attempt = 0; attempt < 16; attempt++)
                    {
                        var candidate = RecruitmentHelper.GenerateCandidate(original.GetPrimarySkill(),
                            original.characterData.skills.Single(k => k.name == original.GetPrimarySkill()).value,
                            shop.Address, new List<string> { hoursDemand.demandName });
                        try
                        {
                            var preview = JsonConvert.DeserializeObject<EmployeeInstance>(JsonConvert.SerializeObject(candidate))!;
                            preview.assignedWorkStationItems = original.assignedWorkStationItems.ToList();
                            var previewDays = JsonConvert.DeserializeObject<List<ScheduleDay>>(JsonConvert.SerializeObject(proposed))!;
                            foreach (var day in previewDays) foreach (var shift in day.workShifts)
                                if (shift.employeeId == original.id) shift.employeeId = candidate.id;
                            preview.UpdateWeeklyHoursAndDays(previewDays);
                            var unmet = preview.demands.Where(n => !JobDemandHelper.GetByName(n).Fulfilled(preview, previewDays)).ToArray();
                            attempts.Add(new { original = original.id, candidate = candidate.id, attempt = attempt + 1, unmet });
                            if (unmet.Length == 0 && preview.assignedWeeklyHours <= 40) { fit = candidate; proposed = previewDays; break; }
                        }
                        finally
                        {
                            if (!ReferenceEquals(fit, candidate)) EmployeeHelper.DiscardCandidate(candidate);
                        }
                    }
                    if (fit == null) throw new InvalidOperationException("No compatible native candidate within16 attempts; existing staff and rota untouched.");
                    accepted.Add(fit);
                }
                // Both replacements are proven on detached schedules before any hire.
                // Keep originals employed until actual native schedule validation passes.
                foreach (var candidate in accepted)
                {
                    EmployeeHelper.HireCandidate(candidate);
                    hired.Add(candidate);
                }
                shop.scheduleDays = proposed;
                foreach (var candidate in hired)
                {
                    candidate.UpdateWeeklyHoursAndDays(); candidate.UpdateAssignedWorkStationItems();
                    if (candidate.assignedWeeklyHours > 40 || candidate.demands.Any(n => !JobDemandHelper.GetByName(n).Fulfilled(candidate, proposed)))
                        throw new InvalidOperationException("Actual native replacement assignment failed demand validation.");
                }
                foreach (var original in originals)
                    if (proposed.Any(d => d.workShifts.Any(w => w.employeeId == original.id)))
                        throw new InvalidOperationException("Replacement rota still references an original employee.");
            }
            catch
            {
                shop.scheduleDays = originalSchedule;
                foreach (var candidate in accepted)
                {
                    if (EmployeeHelper.GetEmployeeInstances().Contains(candidate)) candidate.RemoveEmployee();
                    else EmployeeHelper.DiscardCandidate(candidate);
                }
                foreach (var original in originals)
                { original.UpdateWeeklyHoursAndDays(); original.UpdateAssignedWorkStationItems(); }
                // Native hire/discard notifications and RNG are not fabricated away.
                // No original was fired; existing cash, preferences and absence fields are untouched.
                throw;
            }
            // The ordinary MyEmployees fire action uses RemoveEmployee. Do this last:
            // a scheduling failure above cannot destroy an original employee's state.
            foreach (var original in originals) original.RemoveEmployee();
            GameEvent.Invoke("ba:gameevent_changedbuildingregistration");
            return new { replaced = originals.Select(e => e.id).ToArray(), hired = hired.Select(e => e.id).ToArray(), attempts,
                note = "Native candidates screened against every existing demand. Exact old shifts transferred; preferences, wages and normal sick-day generation retained.", status = OperationsStatus(shop) };
        }

        private object OperationsCashFlow(BuildingRegistration shop)
        {
            var save = SaveGameManager.Current;
            int fromDay = Math.Max(0, save.Day - 3);
            var recent = save.Transactions.Skip(Math.Max(0, save.Transactions.Count - 500))
                .Where(t => t.timestamp != null && t.timestamp.Day >= fromDay).ToArray();
            var own = recent.Where(t => t.address == shop.Address).ToArray();
            var refunds = recent.Where(t => t.address == null && t.transactionType == "ba:transaction_deliverycontractrefund").ToArray();
            object Row(Transaction t) => new { t.transactionType, t.timestamp, t.amount, t.balance, t.transactionData };
            return new { nativeGlobalCash = save.Money, fromDay, toDay = save.Day,
                scannedLastTransactions = Math.Min(save.Transactions.Count, 500),
                exactShopTransactions = own.Skip(Math.Max(0, own.Length - 100)).Select(Row).ToArray(),
                exactShopTransactionsTruncated = own.Length > 100,
                unattributedDeliveryRefunds = refunds.Skip(Math.Max(0, refunds.Length - 30)).Select(Row).ToArray(),
                unattributedRefundsTruncated = refunds.Length > 30,
                note = "Native cash flow, separate from cost of goods sold. Refunds omit native address and are not assigned to this shop merely by a shared business name. The bounded transaction window is not a complete balance reconciliation." };
        }

        private object OperationsStatus(BuildingRegistration shop)
        {
            var staff = EmployeeHelper.GetEmployeeInstances().Where(e => e.assignedAddress != null && e.assignedAddress.Equals(shop.Address)).ToArray();
            var supply = FoodSupplyReader.Read(shop, OperationsProduct);
            return new { shop = PlaceId(shop), shop.BusinessName, shop.RentedByPlayer, shop.customerCapacity,
                buildingCapacity = shop.BuildingCached.GetCustomerCapacity, supply, cashFlow = OperationsCashFlow(shop),
                staffedCapacityAtLeast30Now = BusinessHelper.IsBusinessOpen(shop) && supply.StaffedCounters >= 2 && supply.Units > 0 && supply.MealsPerHour >= 30,
                nativeEquipmentCaps = shop.itemInstances.Values.GetItemsSortedByCapacity(shop).ToArray(),
                furniture = shop.itemInstances.Values.Select(i => new { i.id, i.itemName, i.position, i.parentId,
                    missing = ItemHelper.GetMissingRequirements(i).Select(r => r.GetType().Name).ToArray(), cargo = i.cargoInstances }).ToArray(),
                staff = staff.Select(e => new { e.id, e.assignedWeeklyHours, e.assignedWeeklyDays, e.hourlyWage, available = e.IsEmployeeAvailable(),
                    demands = e.demands.Select(n => new { name = n, met = JobDemandHelper.GetByName(n).Fulfilled(e, shop.scheduleDays) }).ToArray() }).ToArray(),
                weeklyHoursWithin40 = staff.All(e => e.assignedWeeklyHours <= 40),
                schedules = shop.scheduleDays, contracts = SaveGameManager.Current.DeliveryContracts.Where(c => c.businessAddress == shop.Address)
                    .Select(c => new { supplier = PlaceId(SaveGameManager.Current.BuildingRegistrations.Single(r => r.Address == c.wholesaleAddress)),
                        c.enabled, c.nextDeliveryDay, c.isUrgentOrder, c.repeatingOrder, c.deliveryFee, quote = c.TotalPricePerDelivery, c.items }).ToArray()
            };
        }
    }
}
