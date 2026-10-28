using System;
using PersistentCity.Core;
using Xunit;
namespace PersistentCity.Tests;
public sealed class HouseholdFinanceTests
{
    [Fact]
    public void ScarceIncomeFundsFoodBeforeHousingAndCreatesArrearsRatherThanCash()
    {
        var b = new ResidentBudget { DailyIncomeCents = 1200, DailyHomeFoodCents = 500,
            DailyHousingCents = 1000, DailyEssentialsCents = 200, NextPostingMinute = 0 };
        HouseholdFinance.PostDay(b, 0);
        Assert.Equal(Money.FromCents(0), b.Balance); Assert.Equal(Money.FromCents(500), b.HomeFoodPaid);
        Assert.Equal(Money.FromCents(700), b.EssentialsPaid); Assert.Equal(Money.FromCents(500), b.Arrears);
        Assert.Equal(Money.FromCents(0), HouseholdFinance.SpendingLimit(b));
        Assert.Throws<InvalidOperationException>(() => HouseholdFinance.PostDay(b, 0));
        HouseholdFinance.PostDay(b, 1440);
        Assert.Equal(Money.FromCents(1000), b.Arrears); Assert.Equal(Money.FromCents(2400), b.IncomeReceived);
        Assert.Equal(b.OpeningBalance + b.IncomeReceived,
            b.Balance + b.HomeFoodPaid + b.EssentialsPaid);
    }
    [Fact]
    public void AnUnfundedBasketDoesNotBecomeFreeFood()
    {
        var b = new ResidentBudget { DailyIncomeCents = 100, DailyHomeFoodCents = 500,
            DailyHousingCents = 100, NextPostingMinute = 0 };
        HouseholdFinance.PostDay(b, 0);
        Assert.Equal(-1, b.HomeFoodFundedDay); Assert.Equal(Money.FromCents(0), b.HomeFoodPaid);
        Assert.Equal(1, b.DaysWithoutHomeFood); Assert.Equal(Money.FromCents(0), b.Balance);
    }
    [Theory]
    [InlineData(Persona.Student, ResidentIncomeSource.ExternalSupport)]
    [InlineData(Persona.Retiree, ResidentIncomeSource.ExternalPension)]
    [InlineData(Persona.Commuter, ResidentIncomeSource.ExternalEmployment)]
    public void NonEarnersHaveExplicitIncomeSources(Persona persona, ResidentIncomeSource source)
    {
        var c = new CitizenState { Identity = new CitizenIdentity { Id = "r", Age = 30, Persona = persona }, DayPlaceId = "work" };
        var b = HouseholdFinance.Create(c, 1, 40, null);
        Assert.Equal(source, b.IncomeSource); Assert.True(b.DailyIncomeCents > 0);
        Assert.Equal(Money.FromCents(7) * b.DailyIncomeCents, b.OpeningBalance);
        Assert.Equal(Money.FromCents(0), b.IncomeReceived);
    }
    [Fact]
    public void JobLossAndRecoveryAffectOnlyNextUnpostedIncomeAndPreserveOriginalBasis()
    {
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "worker", Age = 30, Persona = Persona.Commuter }, DayPlaceId = "work" };
        var budget = HouseholdFinance.Create(person, 7, 0, null);
        int basis = budget.BaseIncomeCents; Money opening = budget.OpeningBalance;
        HouseholdFinance.PostDay(budget, 0, person);
        Money posted = budget.IncomeReceived, balance = budget.Balance;
        person.DayPlaceId = "";
        Assert.Equal(posted, budget.IncomeReceived); Assert.Equal(balance, budget.Balance);
        Assert.Throws<InvalidOperationException>(() => HouseholdFinance.PostDay(budget, 0, person));
        Assert.Equal(ResidentIncomeSource.ExternalEmployment, budget.IncomeSource);
        budget = Newtonsoft.Json.JsonConvert.DeserializeObject<ResidentBudget>(Newtonsoft.Json.JsonConvert.SerializeObject(budget))!;
        HouseholdFinance.PostDay(budget, 1440, person);
        Assert.Equal(ResidentIncomeSource.ExternalSupport, budget.IncomeSource);
        Assert.Equal(basis * CityBalance.SupportIncomePercent / 100, budget.DailyIncomeCents);
        Assert.Equal(posted + Money.FromCents(budget.DailyIncomeCents), budget.IncomeReceived);
        person.DayPlaceId = "new-work";
        HouseholdFinance.PostDay(budget, 2880, person);
        Assert.Equal(ResidentIncomeSource.ExternalEmployment, budget.IncomeSource);
        Assert.Equal(basis, budget.BaseIncomeCents); Assert.Equal(basis, budget.DailyIncomeCents);
        Assert.Equal(opening, budget.OpeningBalance);
        Assert.Equal(budget.OpeningBalance + budget.IncomeReceived,
            budget.Balance + budget.HomeFoodPaid + budget.EssentialsPaid + budget.PreparedFoodSpent + budget.HeldPurchase);
    }

    [Fact]
    public void InitiallySeekingWorkerRetainsFullIncomeBasisForLaterEmployment()
    {
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "worker", Age = 30, Persona = Persona.Commuter } };
        var budget = HouseholdFinance.Create(person, 7, 0, null);
        Assert.Equal(budget.BaseIncomeCents * CityBalance.SupportIncomePercent / 100, budget.DailyIncomeCents);
        Money opening = budget.OpeningBalance;
        person.DayPlaceId = "work"; HouseholdFinance.PostDay(budget, 0, person);
        Assert.Equal(budget.BaseIncomeCents, budget.DailyIncomeCents); Assert.Equal(opening, budget.OpeningBalance);
    }
}
