using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;
using Activity = PersistentCity.Core.Activity;

namespace PersistentCity.Tests;

// Opt-in, foreground offline measurements. Does not launch the regression matrix or game.
[Collection(StreetFoodHooks.Collection)]
public sealed class F6eMeasure
{
    private readonly ITestOutputHelper output;
    public F6eMeasure(ITestOutputHelper output) { this.output = output; }
    private void Report(string name, double[] ms, long bytes)
    {
        Array.Sort(ms);
        var row = new { name, n = ms.Length, p50 = ms[ms.Length / 2], p95 = ms[(int)Math.Ceiling(ms.Length * .95) - 1],
            max = ms[^1], bytesPerOperation = bytes < 0 ? (double?)null : bytes / (double)ms.Length, spikes30 = ms.Where(x => x >= 30).ToArray() };
        string text = JsonConvert.SerializeObject(row); output.WriteLine(text);
        File.AppendAllText(Environment.GetEnvironmentVariable("PC_F6E_MEASURE")!, text + Environment.NewLine);
    }
    private static double Ms(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    private static FoodCommerce Food(CitySimulator city) => (FoodCommerce)typeof(CitySimulator)
        .GetField("food", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(city)!;

    [Fact]
    public void AccrualPostingAndSaveLatency()
    {
        if (Environment.GetEnvironmentVariable("PC_F6E_MEASURE") == null) return;
        var p = new CitizenState { Identity = new CitizenIdentity { Id = "c0", Age = 30, Persona = Persona.Commuter },
            DayPlaceId = "work", PlaceId = "work", Activity = Activity.Work, Phase = CitizenPhase.AtPlace,
            ActivityUntilMinute = 1980, RecentVisits = new List<VisitRecord> { new VisitRecord { AtMinute = 1500, PlaceId = "work", Activity = Activity.Work } } };
        var b = new ResidentBudget { BaseIncomeCents = 20000 };
        var window = new WorkWindow("work", Activity.Work, 1500, 1980);
        var initial = new EmploymentAccrual { Day = 1, Shift = EmploymentWages.Working, UnpostedWage = Money.FromUnits(960) };
        // Reset outside the measured interval; each sample is the same conserved shift credit.
        var times = new double[20000]; long allocated = 0;
        for (int i = -1000; i < times.Length; i++)
        {
            p.Accrual = initial; b.Ledger = default; b.Balance = Money.Zero;
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            EmploymentWages.Settle(p, b, window, 1980);
            double elapsed = Ms(start); long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (i >= 0) { times[i] = elapsed; allocated += used; }
        }
        Report("shift settlement", times, allocated); Assert.Equal(0, allocated);
        foreach (int count in new[] { 100000, 10000, 1914 })
            foreach (bool attendance in new[] { false, true }) Posting(count, attendance);
        // Fresh F5 per-person cities, including factor-2 visitors, in the unchanged F6d/F7c shape.
        foreach (int count in new[] { 957, 5000, 50000 })
        {
            var state = EconomyScenario.Create(count, 11); state.Food = EconomyScenario.Food();
            PortalPlaces.Add(state, RepresentativeFixture.Portals);
            var city = new CitySimulator(state, new RepresentativeFixture.Routes()); city.AttachFoodMarket(new RepresentativeFixture.Market()); city.EnableHouseholdFinances();
            string payload = SaveCodec.Write(state);
            var write = new double[5]; var read = new double[5];
            for (int i = -1; i < 5; i++)
            {
                long start = Stopwatch.GetTimestamp(); string text = SaveCodec.Write(state); double w = Ms(start);
                start = Stopwatch.GetTimestamp(); var result = SaveWire.Read(text); double r = Ms(start);
                Assert.True(result.Status == SaveReadStatus.Loaded, result.Error);
                Assert.Equal(payload, text);
                if (i >= 0) { write[i] = w; read[i] = r; }
            }
            Report($"fresh {count}x2 write ({payload.Length} characters)", write, -1);
            Report($"fresh {count}x2 read", read, -1);
        }
    }
    private void Posting(int count, bool attendance)
    {
        var state = new CityState { Minute = 1440, Seed = 7, Schema = attendance ? CityState.CurrentSchema : CityState.EconomicLedgerSchema,
            Food = new FoodCommerceState { RepresentationFactor = 1, HouseholdFinanceVersion = 1 } };
        state.Places.Add(new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count });
        for (int i = 0; i < count; i++)
        {
            string id = "c" + i.ToString(CultureInfo.InvariantCulture);
            var p = new CitizenState { Identity = new CitizenIdentity { Id = id, Age = 22, Persona = Persona.Student },
                HomeId = "home", PlaceId = "home", ActivityUntilMinute = 1e8, NextEventMinute = 1e8 };
            state.Citizens.Add(p);
            state.Food.Residents.Add(new ResidentMeal { ResidentId = id, AnchorId = id, DueAt = 1e8,
                Budget = HouseholdFinance.Create(p, 7, 1440, null, id) });
        }
        var city = new CitySimulator(state, new AttendanceWageTests.Routes()); city.AttachFoodMarket(new AttendanceWageTests.Market());
        var food = Food(city);
        var step = (Func<bool>)typeof(FoodCommerce).GetMethod("StepBudget", BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate(typeof(Func<bool>), food);
        var samples = new List<double>(); long allocation = 0;
        for (int day = 1; day <= 54; day++)
        {
            state.Minute = day * 1440;
            while (food.BudgetPostings < (long)day * count)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                bool done = step(); double elapsed = Ms(start); long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
                Assert.True(done); Assert.InRange(food.LastBudgetPostings, 1, 32);
                if (day > 50) { samples.Add(elapsed); allocation += used; }
            }
        }
        Report($"posting {count} attendance={attendance}", samples.ToArray(), allocation);
        Assert.Equal(0, allocation);
    }
}
