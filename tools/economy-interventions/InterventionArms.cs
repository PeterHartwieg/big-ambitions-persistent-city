using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.EconomyInterventions;

public sealed record Arm(string Family, string Name, string Description, IReadOnlyList<InterventionInput> Inputs);

/// <summary>The F6 plan section 3.5 intervention families. Weeks 1-2 baseline, weeks 3-4
/// intervention, weeks 5-6 reverted. Every input is a whole-minute boundary; reverting inputs
/// restore the fixture's base terms.</summary>
public static class InterventionArms
{
    public const int FirstInterventionDay = EconomyRun.BaselineDays + 1;
    public const int FirstRevertedDay = FirstInterventionDay + EconomyRun.InterventionDays;
    public const string Product = FoodProducts.Burger;
    public const int LunchFrom = 11 * 60, LunchTo = 14 * 60;
    // Percentage of F6b's default lane profile; no change to meal-period units.
    public const int RivalLanesHalvedPercent = 50, RivalLanesDefaultPercent = 100;

    private static double At(int day, int minuteOfDay = 0) => day * 1440.0 + minuteOfDay;
    private static int Base(string product) => EconomyFixture.Menu.Single(m => m.Product == product).Cents;
    private static int Scaled(string product, int percent) => (int)Math.Round(Base(product) * percent / 100.0, MidpointRounding.AwayFromZero);
    private static IEnumerable<int> InterventionDays() => Enumerable.Range(FirstInterventionDay, EconomyRun.InterventionDays);

    private static IEnumerable<InterventionInput> Window(Func<double, IEnumerable<InterventionInput>> apply, Func<double, IEnumerable<InterventionInput>> revert)
        => apply(At(FirstInterventionDay)).Concat(revert(At(FirstRevertedDay)));
    private static InterventionInput Price(double at, string product, int cents) => new(at, InputKind.ProductPrice, EconomyFixture.Shop, product, cents);

    public static IReadOnlyList<Arm> All()
    {
        var shop = EconomyFixture.Shop;
        var arms = new List<Arm>
        {
            new("price", "product-minus-25", "Burger price -25 %", Window(
                a => new[] { Price(a, Product, Scaled(Product, 75)) }, r => new[] { Price(r, Product, Base(Product)) }).ToList()),
            new("price", "product-plus-25", "Burger price +25 %", Window(
                a => new[] { Price(a, Product, Scaled(Product, 125)) }, r => new[] { Price(r, Product, Base(Product)) }).ToList()),
            new("price", "menu-plus-50", "Whole menu +50 %", Window(
                a => EconomyFixture.Menu.Select(m => Price(a, m.Product, Scaled(m.Product, 150))),
                r => EconomyFixture.Menu.Select(m => Price(r, m.Product, m.Cents))).ToList()),
            new("staffing", "lunch-counters-halved", "Lunch staffed counters halved (60 -> 30 meals/h, 11:00-14:00)",
                InterventionDays().SelectMany(d => new[] {
                    new InterventionInput(At(d, LunchFrom), InputKind.MealsPerHour, shop, "", EconomyFixture.MealsPerHour / 2),
                    new InterventionInput(At(d, LunchTo), InputKind.MealsPerHour, shop, "", EconomyFixture.MealsPerHour) }).ToList()),
            new("staffing", "zero-staff-day", "One day with zero staff (day 17)", new[] {
                new InterventionInput(At(17), InputKind.MealsPerHour, shop, "", 0),
                new InterventionInput(At(18), InputKind.MealsPerHour, shop, "", EconomyFixture.MealsPerHour) }),
            new("hours", "breakfast-removed", "Breakfast window removed (opens 11:00)", Window(
                a => new[] { new InterventionInput(a, InputKind.Hours, shop, "", LunchFrom, EconomyFixture.CloseMinute) },
                r => new[] { new InterventionInput(r, InputKind.Hours, shop, "", EconomyFixture.OpenMinute, EconomyFixture.CloseMinute) }).ToList()),
            new("hours", "close-2h-earlier", "Evening close 2 h earlier (20:00)", Window(
                a => new[] { new InterventionInput(a, InputKind.Hours, shop, "", EconomyFixture.OpenMinute, EconomyFixture.CloseMinute - 120) },
                r => new[] { new InterventionInput(r, InputKind.Hours, shop, "", EconomyFixture.OpenMinute, EconomyFixture.CloseMinute) }).ToList()),
            new("shortage", "product-3-lunches", "Burger stocked out on 3 lunch periods (days 15, 17, 19)",
                new[] { 15, 17, 19 }.SelectMany(d => new[] {
                    new InterventionInput(At(d, LunchFrom), InputKind.Stock, shop, Product, 0),
                    new InterventionInput(At(d, LunchTo), InputKind.Stock, shop, Product, -1) }).ToList()),
            new("shortage", "whole-shop-day", "Whole-shop stockout day (day 16)",
                EconomyFixture.Menu.Select(m => new InterventionInput(At(16), InputKind.Stock, shop, m.Product, 0))
                    .Concat(EconomyFixture.Menu.Select(m => new InterventionInput(At(17), InputKind.Stock, shop, m.Product, -1))).ToList()),
            new("employment", "workplaces-closed-10pct", "10 % of workplaces closed for 2 weeks (1 of 10)", Window(
                a => new[] { new InterventionInput(a, InputKind.WorkplaceClosed, EconomyFixture.WorkplaceId(0), "", 1) },
                r => new[] { new InterventionInput(r, InputKind.WorkplaceClosed, EconomyFixture.WorkplaceId(0), "", 0) }).ToList()),
            new("employment", "worker-routes-blocked-5pct", "5 % of workers' work routes blocked for 2 weeks", Window(
                a => new[] { new InterventionInput(a, InputKind.WorkerRoutesBlockedPercent, "", "", 5) },
                r => new[] { new InterventionInput(r, InputKind.WorkerRoutesBlockedPercent, "", "", 0) }).ToList()),
            new("access", "route-time-plus-50", "Walking route time to the shop +50 %", Window(
                a => new[] { new InterventionInput(a, InputKind.RouteTimePercent, shop, "", 150) },
                r => new[] { new InterventionInput(r, InputKind.RouteTimePercent, shop, "", 100) }).ToList()),
            new("access", "door-closed-2-days", "Shop door temporarily closed on 2 days (days 16, 18)",
                new[] { 16, 18 }.SelectMany(d => new[] {
                    new InterventionInput(At(d), InputKind.Door, shop, "", 1),
                    new InterventionInput(At(d + 1), InputKind.Door, shop, "", 0) }).ToList()),
            new("access", "second-site-2x", "Second own site at twice the distance, open for 2 weeks", Window(
                a => new[] { new InterventionInput(a, InputKind.SiteOpen, EconomyFixture.FarShop, "", 1) },
                r => new[] { new InterventionInput(r, InputKind.SiteOpen, EconomyFixture.FarShop, "", 0) }).ToList()),
            new("rival", "lanes-halved", "Nearby rival's estimated lanes halved", Window(
                a => new[] { new InterventionInput(a, InputKind.RivalLanes, EconomyFixture.RivalBusiness, "", RivalLanesHalvedPercent) },
                r => new[] { new InterventionInput(r, InputKind.RivalLanes, EconomyFixture.RivalBusiness, "", RivalLanesDefaultPercent) }).ToList()),
            new("rival", "price-minus-25", "Rival price -25 % (whole menu)", Window(
                a => EconomyFixture.Menu.Select(m => new InterventionInput(a, InputKind.RivalPrice, EconomyFixture.RivalBusiness, m.Product, Scaled(m.Product, 75))),
                r => EconomyFixture.Menu.Select(m => new InterventionInput(r, InputKind.RivalPrice, EconomyFixture.RivalBusiness, m.Product, m.Cents))).ToList()),
        };
        return arms;
    }

    public static readonly Arm Control = new("control", "control", "Matched control: same seed, no intervention", Array.Empty<InterventionInput>());
}
