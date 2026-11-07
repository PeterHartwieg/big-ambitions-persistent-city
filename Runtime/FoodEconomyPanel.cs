using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using System.Linq;
using System.Text;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private bool showEconomy;
        private float nextEconomyText;
        private string economyText = "";
        private Vector2 economyScroll;
        private GUIStyle? economyLabel;
        private Rect economyRect;
        /// <summary>Rig diagnostic: inspector cost by mode (closed / hover / open), picks and popup builds.</summary>
        public object InspectorTiming => inspector.Timing;
        public void ResetInspectorTiming() => inspector.ResetTiming();
        private static string LocalizedName(string id) => string.IsNullOrWhiteSpace(id) ? "Unspecified"
            : LocalizorManager.Localize(id, (object?)null).ToString();
        public object EconomyOverview => simulation == null || simulation.State.Food == null
            ? new { active = false } : FoodEconomyInsight.Capture(simulation.State);
        public object NativeEconomyAccounts => foodMarket?.ReadAccounts() ?? Array.Empty<NativeFoodAccount>();
        /// <summary>Rig diagnostic (F6c): incremental panel totals against a full rescan, request/rebuild
        /// counts and each managed shop's week projection. Built only when requested.</summary>
        public object FoodFeedbackOverview
        {
            get
            {
                var money = simulation?.FoodMoney; var food = simulation?.State.Food;
                if (money == null || food == null) return new { active = false };
                long requests = money.Requests, rebuilds = money.Rebuilds;
                var current = money.Snapshot(); var rescan = FoodMoneyCounters.Rescan(food);
                int day = (int)(simulation!.State.Minute / 1440); var index = FoodFeedback.IndexHistory(food.History);
                int recordingSince = FoodFeedback.RecordingSince(food);
                return new { active = true, day, requestsBefore = requests, rebuildsBefore = rebuilds, countersMatchRescan = current.Equals(rescan),
                    counters = current.ToString(), rescan = rescan.ToString(), moneyText = current.Text(), rivalLabel = FoodFeedback.RivalEstimateLabel,
                    shops = food.Shops.Select(s => new { s.PlaceId, s.BusinessId, weeks = FoodFeedback.Weeks(
                        index.TryGetValue(FoodFeedback.Key(s.PlaceId, s.BusinessId), out var rows) ? rows : new List<FoodShopDay>(), s.PlaceId, s.BusinessId, day, recordingSince).Text() }).ToArray() };
            }
        }
        // Whole-dollar display only; never changes a settlement amount. Totals may exceed one amount's range.
        private static string Amount(Money amount) => Amount(amount.Units);
        private static string Amount(System.Numerics.BigInteger units)
            => ((decimal)units / Money.UnitsPerDollar).ToString("C0", CultureInfo.GetCultureInfo("en-US"));
        private static string Dollars(float amount) => amount.ToString("C0", CultureInfo.GetCultureInfo("en-US"));
        private void OnGUI()
        {
            bool preparing = SaveGameManager.Current != null && (simulation == null
                || !ReferenceEquals(saveIdentity, SaveGameManager.Current) || routes == null || !routes.ReadyForPopulation);
            if (failed || preparing)
            {
                GUI.Box(new Rect(20, 20, Math.Min(760, Screen.width - 40), 82), "");
                economyLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
                string message = failed ? "Persistent City is paused. " + status
                    : simulation != null && ReferenceEquals(saveIdentity, SaveGameManager.Current)
                        ? PreparingAccountingText
                        : NativeCityClock.Active ? PreparingAccountingText : "Persistent City: " + (status.Length == 0 ? "Preparing the city." : status);
                GUI.Label(new Rect(30, 28, Math.Min(740, Screen.width - 60), 68), message, economyLabel);
                return;
            }
            if (simulation == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current)) return;
            GUI.depth = -10000;
            if (NativeCityClock.PacingAffected && !showEconomy)
            {
                economyLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
                GUI.Box(new Rect(20, 20, Math.Min(760, Screen.width - 40), 82), "");
                GUI.Label(new Rect(30, 28, Math.Min(740, Screen.width - 60), 68), AccountingStatusText(), economyLabel);
            }
            else if (NativeCityClock.BoundaryClips > 0 && !showEconomy)
            {
                // Quiet baseline status; reserve the large card for actual workload holds.
                GUI.Label(new Rect(20, 20, Math.Min(760, Screen.width - 40), 22), BoundaryAccountingText);
            }
            var input = Event.current;
            if (input.type == EventType.KeyDown && input.control && input.keyCode == KeyCode.F8)
            { showEconomy = !showEconomy; nextEconomyText = 0; input.Use(); }
            if (showEconomy) DrawEconomy(); else economyRect = default;
            if (!inspectorFailed) inspector.Draw();
        }
        private void DrawEconomy()
        {
            float width = Math.Min(760, Screen.width - 40), height = Math.Min(620, Screen.height - 40);
            var panel = new Rect(20, 20, width, height);
            economyRect = panel;
            var previousColor = GUI.color;
            GUI.color = new Color(.075f, .085f, .105f, 1);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.BeginHorizontal(); GUILayout.Label("PERSISTENT CITY · FOOD BUSINESSES");
            if (GUILayout.Button("Close · Ctrl+F8", GUILayout.Width(140))) showEconomy = false;
            GUILayout.EndHorizontal();
            economyLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            if (simulation!.State.Food == null)
            {
                GUILayout.Label("Preparing resident food demand. Eligible player businesses are managed automatically once the city is ready.", economyLabel);
                GUILayout.EndArea(); return;
            }
            if (Time.unscaledTime >= nextEconomyText)
            { economyText = BuildEconomyText(); nextEconomyText = Time.unscaledTime + 3; }
            economyScroll = GUILayout.BeginScrollView(economyScroll);
            GUILayout.Label(economyText, economyLabel);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private string BuildEconomyText()
        {
            var city = simulation!.State; var food = city.Food!;
            var text = new StringBuilder(); int day = (int)(city.Minute / 1440);
            // F6c: incremental totals instead of consumer scans; one pass each over history, places and accounts.
            var counters = simulation.FoodMoney;
            if (counters == null) return "Preparing resident food demand. The food market is not attached yet.";
            var money = counters.Snapshot();
            text.AppendLine("Day " + day + " · " + city.Citizens.Count + " residents · " + city.Visitors.Count + " visitors · " + food.Residents.Count + " people with meals");
            text.AppendLine("Accounting lag " + NativeCityClock.LagMinutes.ToString("F3", CultureInfo.InvariantCulture)
                + " game minutes; calendar holds " + NativeCityClock.HeldOrdinaryTicks + " ordinary / " + NativeCityClock.HeldEconomySkipTicks
                + " skip ticks. Configured calendar factor " + NativeCityClock.Factor.ToString("F3", CultureInfo.InvariantCulture) + ".");
            text.AppendLine("Calendar boundary caps " + NativeCityClock.BoundaryClips + "; requested game minutes withheld "
                + NativeCityClock.WithheldMinutes.ToString("F3", CultureInfo.InvariantCulture) + ". Holds and boundary caps extend wall time; they are not repaid by speeding up.");
            if (NativeCityClock.PacingAffected) text.AppendLine(AccountingStatusText());
            if (food.MealRhythmVersion != 0) text.AppendLine("Breakfast, lunch and dinner follow each resident's waking hours and preferences. All meals share one wallet; eating at home remains an option.");
            text.AppendLine("Each consumer has a separate budget for bills, food at home and prepared meals. Workplaces and travel patterns shape nearby demand.");
            int uncertain = money.Uncertain;
            if (uncertain > 0) text.AppendLine(uncertain + " purchases await confirmation. Their funds remain held; other consumers continue normally.");
            int blockedReturns = money.BlockedReturns;
            if (blockedReturns > 0) text.AppendLine(blockedReturns + " consumers are waiting for a safe return route. They cannot start another meal trip.");
            if (food.Shops.Count == 0) text.AppendLine("No supported player food businesses are currently managed. Unsupported menus retain the game's customer demand.");
            if (foodMarket != null)
                foreach (string explanation in foodMarket.UnmanagedShopStatuses()) text.AppendLine(explanation);
            text.AppendLine(simulation.FreeOutsideSpending().Text());
            text.Append(FoodFeedback.RivalEstimateLabel).Append(" · ").AppendLine(simulation.FreeRivalOutflow()?.Text() ?? "Rival food outflow: unknown (history not recorded).");
            text.AppendLine();
            var nativeAccounts = foodMarket?.ReadAccounts() ?? Array.Empty<NativeFoodAccount>();
            var regulars = simulation.ProjectRegulars(ObservedOwnShop);
            var history = FoodFeedback.IndexHistory(food.History); int recordingSince = FoodFeedback.RecordingSince(food);
            var shopPlaces = new Dictionary<string, Place?>(food.Shops.Count, StringComparer.Ordinal);
            foreach (var scope in food.Shops) shopPlaces[scope.PlaceId] = null;
            foreach (var p in city.Places) if (shopPlaces.TryGetValue(p.Id, out var found) && found == null) shopPlaces[p.Id] = p;
            var accountsByPlace = new Dictionary<string, List<NativeFoodAccount>>(StringComparer.Ordinal);
            foreach (var a in nativeAccounts)
            {
                if (!accountsByPlace.TryGetValue(a.PlaceId, out var list)) accountsByPlace.Add(a.PlaceId, list = new List<NativeFoodAccount>());
                list.Add(a);
            }
            var noRows = new List<FoodShopDay>();
            foreach (var scope in food.Shops)
            {
                string? regularText = regulars?.OwnShopText(scope.BusinessId);
                var place = shopPlaces[scope.PlaceId] ?? throw new InvalidOperationException("Managed shop place is missing.");
                var rows = history.TryGetValue(FoodFeedback.Key(scope.PlaceId, scope.BusinessId), out var shopRows) ? shopRows : noRows;
                FoodShopDay? observed = null;
                foreach (var row in rows) if (observed == null || row.Day > observed.Day) observed = row;
                text.AppendLine(foodMarket?.NameOf(scope.PlaceId) ?? (place.Name.Length > 0 ? place.Name : scope.PlaceId));
                if (regularText != null) text.AppendLine(regularText);
                var excluded = foodMarket?.ScopeStatus(scope.PlaceId) ?? "Waiting for business ownership";
                if (excluded.Length > 0) text.AppendLine(excluded + ". Resident sales are paused for this shop.");
                if (observed == null) text.AppendLine("No evaluated food offers recorded yet.");
                else
                {
                    FoodFeedback.Weeks(rows, scope.PlaceId, scope.BusinessId, day, recordingSince).AppendTo(text);
                    text.AppendLine("Latest recorded day " + observed.Day + ": " + observed.Purchases + " purchases · " + observed.ExistingStopPurchases + " during existing visits · " + Amount(observed.Revenue) + " sales");
                    text.AppendLine("Meals: breakfast " + observed.PurchaseMeals[1] + " · lunch " + observed.PurchaseMeals[2] + " · dinner " + observed.PurchaseMeals[3]
                        + (observed.PurchaseMeals[0] > 0 ? " · earlier single-meal routine " + observed.PurchaseMeals[0] : ""));
                    foreach (var product in observed.ProductSales.OrderByDescending(p => p.Purchases))
                        text.AppendLine(LocalizedName(product.ProductId) + ": " + product.Purchases + " sales · breakfast " + product.PurchaseMeals[1]
                            + " / lunch " + product.PurchaseMeals[2] + " / dinner " + product.PurchaseMeals[3]);
                    text.AppendLine("Evaluated offer outcomes that day: " + observed.PriceRejections + " unaffordable, " + observed.AccessRejections + " travel/time, "
                        + observed.ClosedRejections + " closed/late, " + observed.StockRejections + " stock unavailable, " + observed.StaffRejections + " no staffed counter, " + observed.ServiceRejections + " service/queue, "
                        + observed.PreferredAlternatives + " preferred another option, " + observed.UnknownEvidence + " unknown.");
                    text.AppendLine("Purchase hours: " + string.Join(", ", observed.PurchaseHours.Select((n, h) => new { n, h })
                        .Where(p => p.n > 0).Select(p => p.h.ToString("00") + ":00 " + p.n)));
                }
                var accounts = accountsByPlace.TryGetValue(scope.PlaceId, out var placeAccounts)
                    ? placeAccounts.OrderByDescending(a => a.Day).Take(3).ToArray() : Array.Empty<NativeFoodAccount>();
                foreach (var a in accounts)
                    text.AppendLine("Address accounts, day " + a.Day + ": " + Dollars(a.Revenue) + " sales − " + Dollars(a.Resources) + " goods − "
                        + Dollars(a.Wages + a.Rent + a.OtherOperatingCosts) + " operating = " + Dollars(a.OperatingProfit) + " operating result");
                if (accounts.Length == 0) text.AppendLine("Native daily cost statement pending settlement.");
                text.AppendLine();
            }
            if (money.Budgets > 0) text.Append(money.Text());
            text.AppendLine();
            text.AppendLine("Offer counts describe evaluated choices, not unique people or all district demand. Native operating results exclude initial investment, loans and personal taxes. Consumer income is modeled outside income/support; native business accounts remain authoritative.");
            return text.ToString();
        }
    }
}
