using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5c-lite: a fresh seeded city's behaviour over its first days. The trace reads only behaviour (phases,
/// places, events, meals, purchases, money, orders), never the save shape, and reloads through the live codec at
/// each day boundary. The no-food trace stays pinned at base e9995e5; X3 repinned the food trace once at the one
/// writer (docs/checks/x3.md).</summary>
public sealed class FreshCityTraceTests
{
    internal sealed class Walk : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
            => new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
                Minutes = 1 + Position.Distance(from.Position, to.Position) / 80 } } };
    }

    /// <summary>The fresh trace city.</summary>
    internal static (CityState State, WeightedFoodChoiceTests.Market Market) Fresh(int factor, bool food, int seed = 4242)
    {
        var identities = Enumerable.Range(0, 8).Select(n => new CitizenIdentity {
            Id = "c" + n, Name = "Resident " + n, Age = n == 7 ? 70 : n == 6 ? 19 : 25 + n,
            Persona = n == 7 ? Persona.Retiree : n == 6 ? Persona.Student : n % 2 == 0 ? Persona.Foodie : Persona.Commuter }).ToArray();
        var places = new List<Place> {
            new() { Id = "home:a", Name = "Home A", Kind = PlaceKind.Home, ResidentialAreaSquareMetres = 140, Position = new(0, 0, 0) },
            new() { Id = "home:b", Name = "Home B", Kind = PlaceKind.Home, ResidentialAreaSquareMetres = 90, Position = new(40, 0, 10) },
            new() { Id = "home:c", Name = "Home C", Kind = PlaceKind.Home, ResidentialAreaSquareMetres = 60, Position = new(-30, 0, 20) },
            new() { Id = "work:a", Name = "Office", Kind = PlaceKind.Work, Capacity = 16, Position = new(200, 0, 0) },
            new() { Id = "study:a", Name = "School", Kind = PlaceKind.Study, Capacity = 16, Position = new(-150, 0, 40) },
            new() { Id = "leisure:a", Name = "Park", Kind = PlaceKind.Leisure, Capacity = 16, Position = new(60, 0, -80) } };
        places.AddRange(Enumerable.Range(0, 3).Select(i => new Place { Id = "shop:" + i, Name = "Shop " + i, Kind = PlaceKind.Shop,
            Capacity = 16, Position = new(20 + i * 30, 0, 15 - i * 5) }));
        var state = Population.CreateFromHousing(identities, places, "fresh-trace", "0.40.4", seed, 1440 + 6 * 60);
        var market = new WeightedFoodChoiceTests.Market();
        if (food)
        {
            state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
                RepresentationFactor = factor,
                Shops = Enumerable.Range(0, 3).Select(i => new FoodScope { PlaceId = "shop:" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
            market.Shops = state.Food.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId, Name = s.PlaceId, MealsPerHour = 30,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 6 * 60, ToMinute = 22 * 60 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(250), Units = 1000 } } }).ToArray();
        }
        return (state, market);
    }

    private static string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    // The represented consumer's host and slot are behaviour (whose day it eats on). At the base they were stored as
    // AnchorId/Slot; schema 26 stores each record as its own person's and keeps the host as the scheduling adapter
    // HostId/HostSlot, so the trace text and its pinned hash are unchanged.
    private static void Snapshot(StringBuilder trace, CityState state, WeightedFoodChoiceTests.Market market)
    {
        trace.Append('@').Append(R(state.Minute)).Append('\n');
        foreach (var c in state.Citizens)
            trace.Append(c.Identity.Id).Append('|').Append((int)c.Phase).Append('|').Append((int)c.Activity).Append('|')
                .Append(c.PlaceId).Append('|').Append(c.DestinationId).Append('|').Append(c.DayPlaceId).Append('|')
                .Append(R(c.NextEventMinute)).Append('|').Append(R(c.ActivityUntilMinute)).Append('|').Append(R(c.DepartureMinute)).Append('|')
                .Append(c.CompletedJourneys).Append('|').Append(c.MissedActivities).Append('|').Append(c.RecentVisits.Count).Append('|')
                .Append(c.MealOuting == null ? "-" : c.MealOuting.OriginId + (c.MealOuting.Returning ? "<" : ">")).Append('|').Append(c.JobChanges).Append('\n');
        if (state.Food != null)
            foreach (var m in state.Food.Residents.OrderBy(m => m.ResidentId, StringComparer.Ordinal))
                trace.Append(m.ResidentId).Append('|').Append(m.HostId).Append('|').Append(m.HostSlot).Append('|').Append((int)m.Status).Append('|')
                    .Append(m.Day).Append('|').Append((int)m.Kind).Append('|').Append(m.PlaceId).Append('|').Append(m.ProductId).Append('|')
                    .Append(R(m.DueAt)).Append('|').Append(m.Purchases).Append('|').Append(m.Spent.Units).Append('|')
                    .Append(m.Budget == null ? "-" : m.Budget.Balance.Units + "/" + m.Budget.IncomeReceived.Units + "/" + (int)m.Budget.IncomeSource)
                    .Append('|').Append(m.Excursion == null ? "-" : R(m.Excursion.ReturnsAt)).Append('\n');
        trace.Append("orders=").Append(string.Join(",", market.Orders)).Append('\n');
    }

    internal static string Trace(int factor, bool food, int days = 3)
    {
        var (state, market) = Fresh(factor, food);
        var planner = new Walk();
        var city = new CitySimulator(state, planner);
        if (food) city.AttachFoodMarket(market);
        var trace = new StringBuilder();
        double start = city.State.Minute;
        for (int hour = 1; hour <= days * 24; hour++)
        {
            double at = start + hour * 60;
            while (!city.AdvanceTo(at, 64).CaughtUp) { }
            Snapshot(trace, city.State, market);
            if (hour % 24 == 0)
            {
                var read = DevelopmentChain.Read(DevelopmentChain.Write(city.State));
                Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
                city = new CitySimulator(read.State!, planner);
                if (food) city.AttachFoodMarket(market);
            }
        }
        if (food) Assert.NotEmpty(market.Orders);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(trace.ToString()))).ToLowerInvariant();
    }

    // The no-food trace still matches base e9995e5. X3 repinned the food trace at the one writer: the old pin ran the
    // pre-attendance ledger path, which no longer exists.
    [Theory]
    [InlineData(1, true, "734a0638790204aa7d7b8342800f56d462e995d6eeb6f1da51784da5452541f6")]
    [InlineData(1, false, "27c36028fbcc6b9ffe3ad602d8e81c4ce101a0395ac9d2a8d78b4b838cf34910")]
    public void FreshCityBehaviourTraceIsPinned(int factor, bool food, string expected)
    {
        string actual = Trace(factor, food);
        Assert.True(expected == actual, "trace " + factor + "/" + food + ": " + actual);
    }
}
