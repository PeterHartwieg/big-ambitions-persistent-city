using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5EventShellTests
{
    private static void SameMembers(CitizenState expected, CitizenState actual, string label)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var members = typeof(CitizenState).GetFields(all).Cast<MemberInfo>()
            .Concat(typeof(CitizenState).GetProperties(all).Where(p => p.CanRead && p.GetIndexParameters().Length == 0)).ToList();
        Assert.True(members.Count > 37);
        foreach (var member in members)
        {
            object? a = member is FieldInfo f ? f.GetValue(expected) : ((PropertyInfo)member).GetValue(expected);
            object? b = member is FieldInfo g ? g.GetValue(actual) : ((PropertyInfo)member).GetValue(actual);
            if (ReferenceEquals(a, b)) continue;
            Assert.True(a != null && b != null, label + ": " + member.Name);
            Assert.True(JsonConvert.SerializeObject(a) == JsonConvert.SerializeObject(b), label + ": " + member.Name);
        }
    }

    [Fact]
    public void AReusedEventShellMatchesAFreshCloneForEveryCitizen()
    {
        var state = ResumablePlanningTests.City(48);
        var city = new CitySimulator(state, new ResumablePlanningTests.SyncPlanner());
        city.AdvanceTo(ResumablePlanningTests.Target(2));
        var citizens = city.State.Citizens;
        Assert.Contains(citizens, c => c.Car != null);
        var shell = CitySimulator.CloneForEvent(citizens[^1]);
        shell.MealOuting = new MealOuting { OriginId = "stale" }; shell.Car = new CarState { Id = "stale" };
        shell.OptionalStopChoice = true; shell.PlayerHoldMinutes = 9; shell.RecentVisits.Add(new VisitRecord { PlaceId = "stale" });
        foreach (var live in citizens)
        {
            var fresh = CitySimulator.CloneForEvent(live);
            var reused = CitySimulator.CopyForEvent(live, shell);
            Assert.Same(shell, reused);
            SameMembers(fresh, reused, live.Identity.Id);
            Assert.NotSame(live.Preferences, reused.Preferences);
            Assert.NotSame(live.CommutePreparation, reused.CommutePreparation);
            Assert.NotSame(live.RecentVisits, reused.RecentVisits);
            Assert.True(live.Plan == null || !ReferenceEquals(live.Plan, reused.Plan));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PooledEventShellsProduceTheSameCityThroughSuspensionsAndRestarts(bool restarts)
    {
        string Run(bool pooled)
        {
            var planner = new ResumablePlanningTests.DeferredPlanner { ContinuesFor = index => 1 + index % 3 };
            if (restarts) planner.RestartAt = (_, index, continues) => (index * 7 + continues) % 5 == 0;
            var city = new CitySimulator(ResumablePlanningTests.City(48), planner) { PoolEventShells = pooled };
            for (int day = 1; day <= 3; day++)
            {
                double target = ResumablePlanningTests.Target(day);
                int guard = 0;
                while (true)
                {
                    Assert.True(++guard < 10_000_000);
                    if (city.PlanningPending) { city.ContinuePlanning(0); continue; }
                    if (city.AdvanceFrameSlice(target, 1).CaughtUp) break;
                }
            }
            city.SynchronizeSnapshot();
            if (restarts) Assert.True(city.EventRestarts > 0);
            return SaveCodec.Write(city.State);
        }
        Assert.Equal(Run(false), Run(true));
    }
}
