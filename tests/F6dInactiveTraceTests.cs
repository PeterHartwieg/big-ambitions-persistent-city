using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

// This source also compiles at b2a0ab7 with F6D_BASELINE. The pinned hashes were collected there.
public sealed class F6dInactiveTraceTests
{
    private readonly ITestOutputHelper output;
    public F6dInactiveTraceTests(ITestOutputHelper output) { this.output = output; }
    private static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    private static string AsVendorWriter(CityState state)
    {
        int schema = state.Schema;
        try { state.Schema = SaveCodec.VendorSchema; return SaveCodec.WriteForSchema(state, SaveCodec.VendorSchema); }
        finally { state.Schema = schema; }
    }
    private static SaveReadResult ReadVendor(string payload)
    {
#if F6D_BASELINE
        return SaveCodec.Read(payload);
#else
        return SaveCodec.ReadWriter30(payload);
#endif
    }
    private static string Run(int seed, bool predecessor)
    {
        var (state, market) = Schema28Tests.Fresh(3, seed);
#if !F6D_BASELINE
        state.Schema = SaveCodec.EconomicLedgerSchema;
#endif
        var planner = new FreshCityTraceTests.Walk();
        var city = new CitySimulator(state, planner); city.AttachFoodMarket(market);
        var trace = new StringBuilder();
        double start = state.Minute;
        for (int hour = 1; hour <= 72; hour++)
        {
            double target = start + hour * 60;
            while (!city.AdvanceTo(target, 64).CaughtUp) { }
#if !F6D_BASELINE
            Assert.Empty(city.State.Economy.Outside); Assert.Empty(city.State.Economy.Shops);
            Assert.All(PersonTable.Schedules(city.State), p => Assert.Equal(default(EmploymentAccrual), p.Accrual));
            Assert.All(city.State.Food!.Residents, m => Assert.Equal(default(OwnerAccounts), m.Budget!.Ledger));
            Assert.All(city.State.Food.History, h => Assert.True(h.PeriodCauses.Counts.All(n => n == 0)));
#endif
            // Full predecessor state at each hour: identity, money, held inputs, memories, journeys and visits.
            trace.Append(Sha(AsVendorWriter(city.State))).Append('\n');
            trace.Append(string.Join(",", market.Orders)).Append('\n');
            if (hour % 24 == 0)
            {
#if F6D_BASELINE
                var read = predecessor ? ReadVendor(AsVendorWriter(city.State)) : SaveCodec.Read(SaveCodec.Write(city.State));
#else
                var read = predecessor ? ReadVendor(AsVendorWriter(city.State)) : SaveCodec.ReadLedgerWriter(DevelopmentChain.WriteLedger(city.State));
#endif
                Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
                city = new CitySimulator(read.State!, planner); city.AttachFoodMarket(market);
            }
        }
        Assert.NotEmpty(market.Orders);
        return Sha(trace.ToString());
    }
    [Theory]
    [InlineData(5150, "256d605ec0384ff6fdaaa26d44dc48dd825706c88a4f195eab2297bb79679381")]
    [InlineData(9217, "046eed1463692641d46f2d7c93ed1604ac3d181d49005184fe362521b921fbb7")]
    public void FreshThreeDayStateAndOrderTraceMatchesThePredecessor(int seed, string pin)
    {
        string predecessor = Run(seed, true), current = Run(seed, false);
        Assert.Equal(predecessor, current);
        output.WriteLine($"seed {seed}: {current}");
        if (Environment.GetEnvironmentVariable("PC_F6D_PINS") is string path)
            File.AppendAllText(path, $"trace {seed} {current}" + Environment.NewLine);
        Assert.Equal(pin, current);
    }
}
