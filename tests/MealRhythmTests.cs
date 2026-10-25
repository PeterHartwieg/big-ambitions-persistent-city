using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    private static void RemoveMealRhythmFields(JObject payload)
    {
        RemoveMenuFields(payload);
        var food = (JObject)payload["Food"]!; food.Remove("MealRhythmVersion");
        foreach (var meal in food["Residents"]!.Children<JObject>()) { meal.Remove("Kind"); meal.Remove("DayAllowanceUsedCents"); }
        foreach (var history in food["History"]!.Children<JObject>()) history.Remove("PurchaseMeals");
    }

    [Fact]
    public void MealsVaryByResidentAndDayAndStayOrderedWithinWakingHours()
    {
        var breakfast = new HashSet<double>(); var dinner = new HashSet<double>();
        foreach (Persona persona in Enum.GetValues<Persona>())
        for (int resident = 0; resident < 100; resident++)
        {
            var person = new CitizenState { Identity = new CitizenIdentity { Id = "r" + resident, Persona = persona } };
            for (int day = 0; day < 14; day++)
            {
                double previous = DailyRhythm.OnDay(person, 7, day - 1).EndsAt;
                foreach (MealKind kind in new[] { MealKind.Breakfast, MealKind.Lunch, MealKind.Dinner })
                {
                    double desired = MealRhythm.DesiredAt(person, 7, day, kind), expires = MealRhythm.ExpiresAt(person, 7, day, kind);
                    Assert.True(desired > previous); Assert.True(expires > desired);
                    Assert.True(expires <= DailyRhythm.OnDay(person, 7, day).StartsAt);
                    Assert.True(expires <= (day + 1) * 1440); previous = expires;
                    if (kind == MealKind.Breakfast) breakfast.Add(desired % 1440);
                    if (kind == MealKind.Dinner) dinner.Add(desired % 1440);
                }
            }
        }
        Assert.True(breakfast.Count > 120); Assert.True(dinner.Count > 120);
    }

    [Fact]
    public void ThreeMealPurchasesShareOneWalletAndOneDailyPostingAcrossReloads()
    {
        var (city, market, routes) = Ready(threeMeals: true); city.EnableHouseholdFinances();
        var person = city.State.Citizens.Single();
        foreach (MealKind kind in new[] { MealKind.Breakfast, MealKind.Lunch, MealKind.Dinner })
        {
            double desired = MealRhythm.DesiredAt(person, city.State.Seed, 1, kind);
            Advance(city, desired + 2); city = Reload(city, market, routes);
            Advance(city, desired + 7); city = Reload(city, market, routes);
            Advance(city, desired + 55); city = Reload(city, market, routes);
            Assert.Equal(kind, city.State.Food!.Residents.Single().Kind);
            Assert.Equal(MealStatus.Paid, city.State.Food.Residents.Single().Status);
            Assert.Null(city.State.Citizens.Single().MealOuting);
        }
        var meal = city.State.Food!.Residents.Single(); var b = meal.Budget!;
        Assert.Equal(3, market.Orders.Count); Assert.Equal(3, market.Orders.Distinct().Count());
        Assert.Equal(97, market.Units); Assert.Equal(300, b.PreparedFoodSpentCents);
        Assert.Equal(b.DailyIncomeCents, b.IncomeReceivedCents); Assert.Equal(b.DailyHomeFoodCents, b.HomeFoodPaidCents);
        Assert.Equal(b.OpeningBalanceCents + b.IncomeReceivedCents, b.BalanceCents + b.EssentialsPaidCents + b.HomeFoodPaidCents + b.PreparedFoodSpentCents);
        Assert.Equal(new long[] { 0, 1, 1, 1 }, city.State.Food.History.Single().PurchaseMeals);
    }

    [Fact]
    public void BreakfastSpendingCanMakeLaterMealsUnaffordable()
    {
        var (city, market, routes) = Ready(threeMeals: true); city.EnableHouseholdFinances();
        Advance(city, city.State.Food!.Residents.Single().DesiredAt - 1);
        var b = city.State.Food.Residents.Single().Budget!;
        long remaining = b.DailyHousingCents + b.DailyEssentialsCents + b.DailyHomeFoodCents + 150;
        b.EssentialsPaidCents += b.BalanceCents - remaining; b.BalanceCents = remaining;
        city = Reload(city, market, routes); Advance(city, 2860);
        Assert.Single(market.Orders); Assert.Contains(":breakfast:", market.Orders.Single());
        Assert.Equal(50, HouseholdFinance.SpendingLimit(city.State.Food!.Residents.Single().Budget!));
    }

    [Fact]
    public void TemporaryAllowanceAlsoIsSharedAcrossTheThreeMeals()
    {
        var (city, market, _) = Ready(threeMeals: true);
        city.State.Food!.Residents.Single().AllowanceCents = 150;
        Advance(city, 2860);
        Assert.Single(market.Orders); Assert.Equal(100, city.State.Food.Residents.Single().DayAllowanceUsedCents);
    }

    [Fact]
    public void UncertainBreakfastKeepsItsReceiptAndHoldThroughLaterMealsAndDays()
    {
        var (city, market, routes) = Ready(threeMeals: true); city.EnableHouseholdFinances(); market.ThrowAfterSale = true;
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 60);
        string id = city.State.Food.Residents.Single().IntentId;
        city = Reload(city, market, routes); Advance(city, 5 * 1440, 1); city = Reload(city, market, routes);
        Assert.Single(market.Orders); var meal = city.State.Food!.Residents.Single();
        Assert.Equal(id, meal.IntentId); Assert.Equal(MealStatus.Uncertain, meal.Status);
        Assert.Equal(100, meal.Budget!.HeldPurchaseCents); Assert.Equal(0, meal.Budget.PreparedFoodSpentCents);
    }

    [Theory]
    [InlineData(2)] [InlineData(7)] [InlineData(40)]
    public void SchemaTenPendingOrPaidPurchasesAreRefusedWithoutReissuing(int afterDecision)
    {
        var (city, market, _) = Ready(); city.EnableHouseholdFinances();
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + afterDecision);
        var payload = JObject.Parse(SaveCodec.Write(city.State)); payload["Schema"] = 10; RemoveMealRhythmFields(payload);
        string source = payload.ToString(); var orders = market.Orders.ToArray();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = source };
        var read = SaveStore.ReadFrom(data, city.State.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(source, data[SaveStore.StateKey]);
        Assert.Equal(orders, market.Orders);
    }

    [Fact]
    public void MealReceiptsAreSeparateAndPrunedWithTheirNativeDay()
    {
        var data = new Dictionary<string, string>();
        foreach (MealKind kind in Enum.GetValues<MealKind>())
        {
            var meal = new ResidentMeal { ResidentId = "r:breakfast", Kind = kind, Day = 2, PriceCents = 100, BusinessId = "shop", ProductId = "pastry" };
            Assert.False(FoodSaleReceipts.AlreadyPaid(data, meal)); FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
            Assert.True(FoodSaleReceipts.AlreadyPaid(data, meal));
        }
        Assert.Equal(4, data.Count); FoodSaleReceipts.Prune(data, 3); Assert.Equal(4, data.Count);
        FoodSaleReceipts.Prune(data, 4); Assert.Empty(data);
    }

    [Fact]
    public void ThreeMealCadenceIsDeterministicForTwelveHundredResidents()
    {
        var (a, ma, _) = Ready(1200, threeMeals: true); var (b, mb, routes) = Ready(1200, threeMeals: true);
        ma.Units = mb.Units = 10000; a.EnableHouseholdFinances(); b.EnableHouseholdFinances();
        Advance(a, 2860); Advance(b, 2400, 1); b = Reload(b, mb, routes); Advance(b, 2860, 1);
        Assert.Equal(ma.Orders, mb.Orders); Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
        Assert.Equal(1200, a.State.Food!.Residents.Count);
        Assert.All(a.State.Food.Residents, m => Assert.InRange(m.Purchases, 0, 3));
    }

    [Fact]
    public void BreakfastFavorsSpeedAndDinnerQualityAndPastrySuitabilityDiffers()
    {
        FoodConsumer Make(MealKind kind)
        { var c = new FoodConsumer { ValueOfTimeCentsPerMinute = 10, ServiceQualityValueCents = 300 }; MealRhythm.Apply(c, kind); return c; }
        var breakfast = Make(MealKind.Breakfast); var dinner = Make(MealKind.Dinner);
        Assert.True(breakfast.ValueOfTimeCentsPerMinute > dinner.ValueOfTimeCentsPerMinute);
        Assert.True(breakfast.ServiceQualityValueCents < dinner.ServiceQualityValueCents);
        FoodIntent Choice(MealKind kind)
        {
            var c = Make(kind); c.Id = "resident"; c.DailyAllowanceCents = 2000;
            var intent = new FoodIntent { Id = "trial", ConsumerId = c.Id, OriginPlaceId = "home", Deadline = 1000,
                EatingMinutes = 15, MaximumQueueMinutes = 15, OutsideOptionScoreCents = 1600 };
            var shops = new[] { new FoodBusiness { Id = "b", PlaceId = "shop", MealsPerHour = 15,
                Hours = Enumerable.Range(0,7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = "ba:itemname_croissant", PriceCents = 800, Units = 10 } } } };
            FoodDemand.ChooseObserved(c, shops, new[] { new FoodTravelQuote { ConsumerId = c.Id, IntentId = intent.Id, BusinessId = "b", Minutes = 5, ReturnMinutes = 5 } }, intent, 700, mealKind: kind);
            return intent;
        }
        Assert.Equal(FoodIntentStatus.Travelling, Choice(MealKind.Breakfast).Status);
        Assert.Equal(FoodIntentStatus.DeclinedPurchase, Choice(MealKind.Dinner).Status);
    }

    [Fact]
    public void SameResidentChoosesQuickCheapBreakfastButHigherQualityDinner()
    {
        string Choose(MealKind kind)
        {
            var consumer = new FoodConsumer { Id = "r", DailyAllowanceCents = 2000, ValueOfTimeCentsPerMinute = 10, ServiceQualityValueCents = 300 };
            MealRhythm.Apply(consumer, kind);
            var intent = new FoodIntent { Id = "trial", ConsumerId = "r", OriginPlaceId = "home", Deadline = 1000, EatingMinutes = 15, MaximumQueueMinutes = 15 };
            FoodBusiness Shop(string id, int price, int quality) => new() { Id = id, PlaceId = id, CustomerServicePercent = quality, MealsPerHour = 15,
                Hours = Enumerable.Range(0,7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = "meal", PriceCents = price, Units = 10 } } };
            FoodTravelQuote Route(string id, int minutes) => new() { BusinessId = id, ConsumerId = "r", IntentId = intent.Id, Minutes = minutes, ReturnMinutes = minutes };
            FoodDemand.ChooseObserved(consumer, new[] { Shop("quick", 900, 25), Shop("quality", 1000, 100) },
                new[] { Route("quick", 5), Route("quality", 8) }, intent, 700, mealKind: kind);
            return intent.BusinessId;
        }
        Assert.Equal("quick", Choose(MealKind.Breakfast)); Assert.Equal("quality", Choose(MealKind.Dinner));
    }

    [Theory]
    [InlineData("Kind")] [InlineData("MealRhythmVersion")] [InlineData("PurchaseMeals")]
    public void InvalidMealStateIsRejectedWithoutRepair(string field)
    {
        var (city, _, _) = Ready(threeMeals: true); Advance(city, city.State.Food!.Residents.Single().DesiredAt + 60);
        var payload = JObject.Parse(SaveCodec.Write(city.State));
        if (field == "Kind") payload["Food"]!["Residents"]![0]![field] = 99;
        else if (field == "MealRhythmVersion") payload["Food"]![field] = 99;
        else payload["Food"]!["History"]![0]![field]![1] = 99;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(payload.ToString()).Status);
    }
}
