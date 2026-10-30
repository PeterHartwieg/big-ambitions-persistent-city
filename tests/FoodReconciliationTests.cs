using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncertainCheckoutUsesExactEvidenceWithoutReplayingNativeSale(bool threeMeals)
    {
        var (city, market, routes) = Ready(threeMeals: threeMeals);
        var person = city.State.Citizens.Single();
        person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        market.ThrowAfterSale = true;
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        var meal = city.State.Food.Residents.Single();
        Assert.Equal(MealStatus.Uncertain, meal.Status);
        string intent = meal.IntentId; double checkout = market.Times.Single();
        double reconcileAt = meal.DueAt;
        city = Reload(city, market, routes);
        Advance(city, reconcileAt - .0001);
        decimal balance = city.State.Food!.Residents.Single().Budget!.BalanceCents;
        market.ConfirmEvidence = true;
        market.EarliestMinute = reconcileAt; // Original checkout is now too old for Buy.
        Advance(city, reconcileAt);
        meal = city.State.Food!.Residents.Single();
        Assert.Equal(MealStatus.Paid, meal.Status);
        Assert.Equal(intent, meal.IntentId);
        Assert.Equal(balance, meal.Budget!.BalanceCents);
        Assert.Equal(0, meal.Budget.HeldPurchaseCents);
        Assert.Equal(market.Price, meal.Budget.PreparedFoodSpentCents);
        Assert.Equal(1, meal.Purchases);
        Assert.Single(market.Orders); Assert.Equal(99, market.Units);
        var history = Assert.Single(city.State.Food.History, h => h.Purchases > 0);
        Assert.Equal((int)(checkout / 1440), history.Day);
        Assert.Equal(1, history.PurchaseHours[(int)(checkout % 1440 / 60)]);
        city = Reload(city, market, routes);
        Assert.Equal(1, city.State.Food!.Residents.Single().Purchases);
        Assert.Equal(market.Price, city.State.Food.Residents.Single().Budget!.PreparedFoodSpentCents);
        Assert.Single(market.Orders);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrUnavailableEvidencePreservesOriginalIntentAndHeldFunds(bool throws)
    {
        var (city, market, routes) = Ready();
        city.EnableHouseholdFinances(); market.ThrowAfterSale = true;
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 40);
        var original = city.State.Food.Residents.Single();
        Assert.Equal(MealStatus.Uncertain, original.Status);
        string intent = original.IntentId;
        market.ThrowOnEvidence = throws;
        for (int day = 0; day < 5; day++)
        {
            city = Reload(city, market, routes);
            Advance(city, city.State.Food!.Residents.Single().DueAt);
            var meal = city.State.Food.Residents.Single();
            Assert.Equal(intent, meal.IntentId);
            Assert.Equal(MealStatus.Uncertain, meal.Status);
            Assert.Equal(market.Price, meal.Budget!.HeldPurchaseCents);
            Assert.Equal(0, meal.Budget.PreparedFoodSpentCents);
            Assert.Single(market.Orders);
        }
    }
}
