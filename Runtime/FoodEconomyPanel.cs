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
        private readonly FoodPanelRefresh economyRefresh = new FoodPanelRefresh();
        private Func<string>? economyBuilder;
        private int economyShopPage;
        private Vector2 economyScroll;
        private GUIStyle? economyLabel;
        private Rect economyRect;
        /// <summary>Rig diagnostic: inspector cost by mode (closed / hover / open), picks and popup builds.</summary>
        public object InspectorTiming => inspector.Timing;
        public void ResetInspectorTiming() => inspector.ResetTiming();
        private static string LocalizedName(string id) => string.IsNullOrWhiteSpace(id) ? "Unspecified"
            : LocalizorManager.Localize(id, (object?)null).ToString();
        public object EconomyOverview => simulation == null || simulation.View.Food == null
            ? new { active = false } : CityViewReports.EconomyOverview(simulation.View);
        public object NativeEconomyAccounts => foodMarket?.ReadAccounts() ?? Array.Empty<NativeFoodAccount>();
        /// <summary>Rig diagnostic (F6c): incremental panel totals against a full rescan, request/rebuild
        /// counts and each managed shop's week projection. Built only when requested.</summary>
        public object FoodFeedbackOverview
        {
            get
            {
                var money = simulation?.FoodMoney; var city = simulation?.View; var food = city?.Food;
                if (money == null || city == null || food == null) return new { active = false };
                long requests = money.Requests, rebuilds = money.Rebuilds;
                var current = money.Snapshot(); var rescan = CityViewReports.RescanFoodMoney(city);
                int day = (int)(city.Minute / 1440); var history = CityViewReports.FoodHistory(city);
                int recordingSince = CityViewReports.FoodRecordingSince(city); int v2RecordingSince = CityViewReports.FoodRecordingSinceV2(city);
                return new { active = true, day, panelBuilds = economyRefresh.Builds, requestsBefore = requests, rebuildsBefore = rebuilds, countersMatchRescan = current.Equals(rescan),
                    counters = current.ToString(), rescan = rescan.ToString(), moneyText = current.Text(), rivalLabel = FoodFeedback.RivalEstimateLabel,
                    shops = food.ScopeList.Select(s => new { s.PlaceId, s.BusinessId, weeks = history.WeeksText(s.PlaceId, s.BusinessId, day, recordingSince),
                        feedbackV2 = FeedbackV2Text(history, s.PlaceId, s.BusinessId, foodMarket?.JournalFor(s.BusinessId), day, v2RecordingSince),
                        evicted = foodMarket?.JournalFor(s.BusinessId)?.Evicted ?? 0 }).ToArray() };
            }
        }
        private static string FeedbackV2Text(FoodHistoryReport history, string placeId, string businessId, ShopObservation? journal, int day, int recordingSince)
        {
            var text = new StringBuilder(); history.AppendPeriods(text, placeId, businessId, day, recordingSince);
            history.AppendJournal(text, journal, placeId, businessId, day, recordingSince, LocalizedName); return text.ToString();
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
            { showEconomy = !showEconomy; economyRefresh.Invalidate(); input.Use(); }
            if (showEconomy) DrawEconomy(); else economyRect = default;
            if (researchWindow.Open) researchWindow.Draw(ResearchLedgerNow, researchNames ??= ResearchBusinessName);
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
            if (GUILayout.Button("Research reports", GUILayout.Width(140))) researchWindow.Toggle();
            if (GUILayout.Button("Close · Ctrl+F8", GUILayout.Width(140))) showEconomy = false;
            GUILayout.EndHorizontal();
            economyLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            var food = simulation!.View.Food;
            if (food == null)
            {
                GUILayout.Label("Preparing resident food demand. Eligible player businesses are managed automatically once the city is ready.", economyLabel);
                GUILayout.EndArea(); return;
            }
            int pages = Math.Max(1, (food.ScopeList.Count + FoodFeedbackV2.ShopsPerPage - 1) / FoodFeedbackV2.ShopsPerPage);
            economyShopPage = Math.Min(economyShopPage, pages - 1);
            if (pages > 1)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous shops")) { economyShopPage = (economyShopPage + pages - 1) % pages; economyRefresh.Invalidate(); }
                GUILayout.Label("Shops · page " + (economyShopPage + 1) + " / " + pages);
                if (GUILayout.Button("Next shops")) { economyShopPage = (economyShopPage + 1) % pages; economyRefresh.Invalidate(); }
                GUILayout.EndHorizontal();
            }
            economyBuilder ??= BuildEconomyText;
            economyRefresh.Update(showEconomy, Time.unscaledTime, economyBuilder);
            economyScroll = GUILayout.BeginScrollView(economyScroll);
            GUILayout.Label(economyRefresh.Text, economyLabel);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private string BuildEconomyText()
        {
            var city = simulation!.View; var food = city.Food!;
            var text = new StringBuilder(); int day = (int)(city.Minute / 1440);
            // F6c: incremental totals instead of consumer scans; one pass each over history, places and accounts.
            var counters = simulation.FoodMoney;
            if (counters == null) return "Preparing resident food demand. The food market is not attached yet.";
            var money = counters.Snapshot();
            text.AppendLine(PopulationWords.Headline(day, city.ResidentList.Count, city.VisitorCount, food.ConsumerCount));
            text.AppendLine("Accounting lag " + NativeCityClock.LagMinutes.ToString("F3", CultureInfo.InvariantCulture)
                + " game minutes; calendar holds " + NativeCityClock.HeldOrdinaryTicks + " ordinary / " + NativeCityClock.HeldEconomySkipTicks
                + " skip ticks. Configured calendar factor " + NativeCityClock.Factor.ToString("F3", CultureInfo.InvariantCulture) + ".");
            text.AppendLine("Calendar boundary caps " + NativeCityClock.BoundaryClips + "; requested game minutes withheld "
                + NativeCityClock.WithheldMinutes.ToString("F3", CultureInfo.InvariantCulture) + ". Holds and boundary caps extend wall time; they are not repaid by speeding up.");
            if (NativeCityClock.PacingAffected) text.AppendLine(AccountingStatusText());
            if (food.MealRhythmVersion != 0) text.AppendLine("Breakfast, lunch and dinner follow each resident's waking hours and preferences. All meals share one wallet; eating at home remains an option.");
            text.AppendLine(PopulationWords.Lines(city.VisitorCount, city.PortalCount));
            text.AppendLine("Workplaces and travel patterns shape nearby demand.");
            int uncertain = money.Uncertain;
            if (uncertain > 0) text.AppendLine(uncertain + " purchases await confirmation. Their funds remain held; other consumers continue normally.");
            int blockedReturns = money.BlockedReturns;
            if (blockedReturns > 0) text.AppendLine(blockedReturns + " consumers are waiting for a safe return route. They cannot start another meal trip.");
            var scopes = food.ScopeList;
            if (scopes.Count == 0) text.AppendLine("No supported player food businesses are currently managed. Unsupported menus retain the game's customer demand.");
            if (foodMarket != null)
                foreach (string explanation in foodMarket.UnmanagedShopStatuses()) text.AppendLine(explanation);
            text.Append(FoodFeedback.RivalEstimateLabel).Append(" · ").AppendLine(simulation.FreeRivalOutflow()?.Text() ?? "Rival food outflow: unknown (history not recorded).");
            text.AppendLine();
            var nativeAccounts = foodMarket?.ReadAccounts() ?? Array.Empty<NativeFoodAccount>();
            var regulars = simulation.ProjectRegulars(ObservedOwnShop);
            var history = CityViewReports.FoodHistory(city); int recordingSince = CityViewReports.FoodRecordingSince(city); int v2RecordingSince = CityViewReports.FoodRecordingSinceV2(city);
            var shopPlaces = new Dictionary<string, IPlaceView?>(scopes.Count, StringComparer.Ordinal);
            for (int i = 0; i < scopes.Count; i++) shopPlaces[scopes[i].PlaceId] = null;
            var places = city.PlaceList;
            for (int i = 0; i < places.Count; i++) { var p = places[i]; if (shopPlaces.TryGetValue(p.Id, out var found) && found == null) shopPlaces[p.Id] = p; }
            var accountsByPlace = new Dictionary<string, List<NativeFoodAccount>>(StringComparer.Ordinal);
            foreach (var a in nativeAccounts)
            {
                if (!accountsByPlace.TryGetValue(a.PlaceId, out var list)) accountsByPlace.Add(a.PlaceId, list = new List<NativeFoodAccount>());
                list.Add(a);
            }
            int firstShop = economyShopPage * FoodFeedbackV2.ShopsPerPage;
            for (int shopIndex = firstShop; shopIndex < Math.Min(scopes.Count, firstShop + FoodFeedbackV2.ShopsPerPage); shopIndex++)
            {
                var scope = scopes[shopIndex];
                string? regularText = regulars?.OwnShopText(scope.BusinessId);
                var place = shopPlaces[scope.PlaceId] ?? throw new InvalidOperationException("Managed shop place is missing.");
                text.AppendLine(foodMarket?.NameOf(scope.PlaceId) ?? (place.Name.Length > 0 ? place.Name : scope.PlaceId));
                if (regularText != null) text.AppendLine(regularText);
                var excluded = foodMarket?.ScopeStatus(scope.PlaceId) ?? "Waiting for business ownership";
                if (excluded.Length > 0) text.AppendLine(excluded + ". Resident sales are paused for this shop.");
                history.AppendShopDays(text, scope.PlaceId, scope.BusinessId, day, recordingSince, Amount, LocalizedName);
                history.AppendPeriods(text, scope.PlaceId, scope.BusinessId, day, v2RecordingSince);
                history.AppendJournal(text, foodMarket?.JournalFor(scope.BusinessId),
                    scope.PlaceId, scope.BusinessId, day, v2RecordingSince, LocalizedName);
                var accounts = accountsByPlace.TryGetValue(scope.PlaceId, out var placeAccounts)
                    ? placeAccounts.OrderByDescending(a => a.Day).Take(3).ToArray() : Array.Empty<NativeFoodAccount>();
                foreach (var a in accounts)
                    text.AppendLine("Address accounts, day " + a.Day + ": " + Dollars(a.Revenue) + " sales − " + Dollars(a.Resources) + " goods − "
                        + Dollars(a.Wages + a.Rent + a.OtherOperatingCosts) + " operating = " + Dollars(a.OperatingProfit) + " operating result");
                if (accounts.Length == 0) text.AppendLine("Native daily cost statement pending settlement.");
                text.AppendLine();
            }
            if (money.Budgets > 0) text.Append(money.Text());
            CityViewReports.AppendCityEconomy(text, money, city, day);
            text.AppendLine();
            text.AppendLine("Offer counts describe evaluated choices, not unique people or all district demand. Native operating results exclude initial investment, loans and personal taxes. Consumer income is modeled outside income/support; native business accounts remain authoritative.");
            return text.ToString();
        }
    }
}
