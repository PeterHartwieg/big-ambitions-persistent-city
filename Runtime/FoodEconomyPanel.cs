using System;
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
                        ? "Persistent City is preparing city routes. Resident demand starts automatically when ready. Ctrl+F8 opens food business feedback."
                        : "Persistent City: " + (status.Length == 0 ? "Preparing the city." : status);
                GUI.Label(new Rect(30, 28, Math.Min(740, Screen.width - 60), 68), message, economyLabel);
                return;
            }
            if (simulation == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current)) return;
            var input = Event.current;
            if (input.type == EventType.KeyDown && input.control && input.keyCode == KeyCode.F8)
            { showEconomy = !showEconomy; nextEconomyText = 0; input.Use(); }
            GUI.depth = -10000;
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
            text.AppendLine("Day " + day + " · " + city.Citizens.Count + " physical residents · " + food.Residents.Count + " represented consumers");
            if (food.MealRhythmVersion != 0) text.AppendLine("Breakfast, lunch and dinner follow each resident's waking hours and preferences. All meals share one wallet; eating at home remains an option.");
            text.AppendLine("Each consumer has a separate budget for bills, food at home and prepared meals. Workplaces and travel patterns shape nearby demand.");
            int uncertain = food.Residents.Count(m => m.Status == MealStatus.Uncertain);
            if (uncertain > 0) text.AppendLine(uncertain + " purchases await confirmation. Their funds remain held; other consumers continue normally.");
            int blockedReturns = food.Residents.Count(m => m.Excursion?.Unresolved == true);
            if (blockedReturns > 0) text.AppendLine(blockedReturns + " consumers are waiting for a safe return route. They cannot start another meal trip.");
            if (food.Shops.Count == 0) text.AppendLine("No supported player food businesses are currently managed. Unsupported menus retain the game's customer demand.");
            if (foodMarket != null)
                foreach (string explanation in foodMarket.UnmanagedShopStatuses()) text.AppendLine(explanation);
            text.AppendLine();
            var nativeAccounts = foodMarket?.ReadAccounts() ?? Array.Empty<NativeFoodAccount>();
            var regulars = simulation.ProjectRegulars(ObservedOwnShop);
            foreach (var scope in food.Shops)
            {
                string? regularText = regulars?.OwnShopText(scope.BusinessId);
                var place = city.Places.First(p => p.Id == scope.PlaceId);
                var observed = food.History.Where(h => h.PlaceId == scope.PlaceId && h.BusinessId == scope.BusinessId).OrderByDescending(h => h.Day).FirstOrDefault();
                text.AppendLine(foodMarket?.NameOf(scope.PlaceId) ?? (place.Name.Length > 0 ? place.Name : scope.PlaceId));
                if (regularText != null) text.AppendLine(regularText);
                var excluded = foodMarket?.ScopeStatus(scope.PlaceId) ?? "Waiting for business ownership";
                if (excluded.Length > 0) text.AppendLine(excluded + ". Resident sales are paused for this shop.");
                if (observed == null) text.AppendLine("No evaluated food offers recorded yet.");
                else
                {
                    text.AppendLine("Day " + observed.Day + ": " + observed.Purchases + " purchases · " + observed.ExistingStopPurchases + " during existing visits · " + Amount(observed.Revenue) + " sales");
                    text.AppendLine("Meals: breakfast " + observed.PurchaseMeals[1] + " · lunch " + observed.PurchaseMeals[2] + " · dinner " + observed.PurchaseMeals[3]
                        + (observed.PurchaseMeals[0] > 0 ? " · earlier single-meal routine " + observed.PurchaseMeals[0] : ""));
                    foreach (var product in observed.ProductSales.OrderByDescending(p => p.Purchases))
                        text.AppendLine(LocalizedName(product.ProductId) + ": " + product.Purchases + " sales · breakfast " + product.PurchaseMeals[1]
                            + " / lunch " + product.PurchaseMeals[2] + " / dinner " + product.PurchaseMeals[3]);
                    text.AppendLine("Observed offer outcomes: " + observed.PriceRejections + " unaffordable, " + observed.AccessRejections + " travel/time, "
                        + observed.ClosedRejections + " closed/late, " + observed.StockRejections + " stock unavailable, " + observed.StaffRejections + " no staffed counter, " + observed.ServiceRejections + " service/queue, "
                        + observed.PreferredAlternatives + " preferred another option, " + observed.UnknownEvidence + " unknown.");
                    text.AppendLine("Purchase hours: " + string.Join(", ", observed.PurchaseHours.Select((n, h) => new { n, h })
                        .Where(p => p.n > 0).Select(p => p.h.ToString("00") + ":00 " + p.n)));
                }
                var accounts = nativeAccounts.Where(a => a.PlaceId == scope.PlaceId).OrderByDescending(a => a.Day).Take(3).ToArray();
                foreach (var a in accounts)
                    text.AppendLine("Address accounts, day " + a.Day + ": " + Dollars(a.Revenue) + " sales − " + Dollars(a.Resources) + " goods − "
                        + Dollars(a.Wages + a.Rent + a.OtherOperatingCosts) + " operating = " + Dollars(a.OperatingProfit) + " operating result");
                if (accounts.Length == 0) text.AppendLine("Native daily cost statement pending settlement.");
                text.AppendLine();
            }
            var budgets = food.Residents.Where(m => m.Budget != null).Select(m => m.Budget!).ToArray();
            if (budgets.Length > 0)
            {
                text.AppendLine("CURRENT RESIDENT ACCOUNTS · lifetime totals");
                text.AppendLine("Cash " + Amount(Money.TotalUnits(budgets.Select(b => b.Balance))) + " · received income/support " + Amount(Money.TotalUnits(budgets.Select(b => b.IncomeReceived))));
                text.AppendLine("Housing/essentials paid " + Amount(Money.TotalUnits(budgets.Select(b => b.EssentialsPaid))) + " · home-food baskets "
                    + Amount(Money.TotalUnits(budgets.Select(b => b.HomeFoodPaid))) + " · prepared food " + Amount(Money.TotalUnits(budgets.Select(b => b.PreparedFoodSpent))));
                text.AppendLine("Unpaid bills " + Amount(Money.TotalUnits(budgets.Select(b => b.Arrears))) + " · purchase funds awaiting confirmation " + Amount(Money.TotalUnits(budgets.Select(b => b.HeldPurchase))));
            }
            text.AppendLine();
            text.AppendLine("Offer counts describe evaluated choices, not unique people or all district demand. Native operating results exclude initial investment, loans and personal taxes. Consumer income is modeled outside income/support; native business accounts remain authoritative.");
            return text.ToString();
        }
    }
}
