using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodReceiptEvidenceTests
{
    private static ResidentMeal Meal(string resident = "resident", int day = 1) => new()
    {
        ResidentId = resident, Day = day, Kind = MealKind.Lunch,
        BusinessId = "business:shop:instance", ProductId = FoodProducts.HotDog, PriceCents = 825
    };

    [Fact]
    public void ExactPaidEvidenceCanResolveAnInterruptedPopulationConfirmationWithoutAnotherSale()
    {
        var meal = Meal(); var data = new Dictionary<string, string>();
        Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(data, meal));
        FoodSaleReceipts.Begin(data, meal);
        Assert.Equal(FoodReceiptStatus.Pending, FoodSaleReceipts.ReadStatus(data, meal));
        FoodSaleReceipts.Confirm(data, meal);
        // Native receipt and a population-side uncertain meal can be saved together when
        // confirmation handling is interrupted. Reading evidence must not execute anything.
        meal.Status = MealStatus.Uncertain;
        var restoredNativeData = new Dictionary<string, string>(data);
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(restoredNativeData, meal));
        Assert.True(FoodSaleReceipts.AlreadyPaid(restoredNativeData, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(restoredNativeData, meal));
        Assert.Equal(data.ToArray(), restoredNativeData.ToArray());
    }

    [Fact]
    public void PendingEvidenceSurvivesManyDaysAndStillBlocksRetry()
    {
        var meal = Meal(); var data = new Dictionary<string, string> { ["rlo:data"] = "keep" };
        FoodSaleReceipts.Begin(data, meal); var before = data.ToArray();
        foreach (int day in new[] { 3, 30, 365, 10000 }) FoodSaleReceipts.Prune(data, day);
        Assert.Equal(before, data.ToArray());
        Assert.Equal(FoodReceiptStatus.Pending, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(data, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(data, meal));
    }

    [Fact]
    public void OldPaidEvidenceIsRetainedWhileItsPopulationOutcomeRemainsUnresolved()
    {
        var pendingConfirmation = Meal("uncertain"); var settled = Meal("settled");
        var recent = Meal("recent", 100); var data = new Dictionary<string, string>();
        foreach (var meal in new[] { pendingConfirmation, settled, recent })
        { FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal); }
        FoodSaleReceipts.Prune(data, 100, new[] { pendingConfirmation.IntentId });
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(data, pendingConfirmation));
        Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(data, settled));
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(data, recent));
        FoodSaleReceipts.Prune(data, 100);
        Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(data, pendingConfirmation));
        Assert.Single(data);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("business")]
    [InlineData("product")]
    public void AReceiptForDifferentTermsNeverConfirmsThePurchase(string change)
    {
        var meal = Meal(); var data = new Dictionary<string, string>();
        FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
        if (change == "price") meal.PriceCents++;
        else if (change == "business") meal.BusinessId = "replacement-business";
        else meal.ProductId = FoodProducts.Burger;
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(data, meal));
        Assert.Single(data);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("paid|0|business|product")]
    [InlineData("paid|825|%|product")]
    [InlineData("paid|825|business|")]
    [InlineData("future-format|825|business|product")]
    public void MalformedOrUnknownEvidenceIsPreservedForInvestigation(string marker)
    {
        var meal = Meal(); var data = new Dictionary<string, string>();
        FoodSaleReceipts.Begin(data, meal); string key = data.Keys.Single(); data[key] = marker;
        FoodSaleReceipts.Prune(data, 10000);
        Assert.Equal(marker, data[key]);
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, meal));
    }

    [Fact]
    public void MissingMarkerDoesNotResolveAnUncertainPurchaseOrChangeItsHeldFunds()
    {
        var meal = Meal(); meal.Status = MealStatus.Uncertain;
        meal.Budget = new ResidentBudget { BalanceCents = 1000, HeldPurchaseCents = 825 };
        var data = new Dictionary<string, string>();
        Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(data, meal));
        FoodSaleReceipts.Prune(data, 10000, new[] { meal.IntentId });
        Assert.Equal(MealStatus.Uncertain, meal.Status);
        Assert.Equal(825, meal.Budget.HeldPurchaseCents);
        Assert.Equal(1000, meal.Budget.BalanceCents);
    }

    [Fact]
    public void UnknownIntentKeyDoesNotExpireJustBecauseItEndsInAnOldDay()
    {
        var data = new Dictionary<string, string> {
            ["persistentcity:food-sale-v1:future:resident:1"] = "paid|825|business|product",
            ["persistentcity:food-sale-v1:food:resident:future-meal:1"] = "paid|825|business|product"
        };
        var before = data.ToArray();
        FoodSaleReceipts.Prune(data, 10000);
        Assert.Equal(before, data.ToArray());
    }
}
