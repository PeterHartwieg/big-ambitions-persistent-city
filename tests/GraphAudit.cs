using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PersistentCity.Core;

namespace PersistentCity.Tests;

/// <summary>
/// F7c: compares two object graphs field for field (every instance field, private ones included) and records every
/// object the two graphs share. A snapshot must equal the live graph it was captured from and share only immutable
/// objects. Lists and arrays compare by count and items, never by capacity.
/// </summary>
internal sealed class GraphAudit
{
    /// <summary>Transient caches and panel counters that are never saved and that a capture leaves empty.</summary>
    private static readonly HashSet<string> Transient = new(StringComparer.Ordinal)
    { "counters", "OpeningCache", "cachedView", "cachedDay", "activeCampaign", "SnapshotSynchronizer", "estimatedMinutes" };

    private readonly HashSet<(object, object)> visited = new(PairComparer.Instance);
    public readonly List<object> Shared = new();
    public readonly List<string> Differences = new();

    public static GraphAudit Compare(object live, object copy)
    {
        var audit = new GraphAudit();
        audit.Walk(live, copy, "state");
        return audit;
    }

    private void Walk(object? a, object? b, string path)
    {
        if (Differences.Count > 20) return;
        if (a == null || b == null) { if (a != null || b != null) Differences.Add(path + ": null differs"); return; }
        var type = a.GetType();
        if (type != b.GetType()) { Differences.Add(path + ": type " + type.Name + " vs " + b.GetType().Name); return; }
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(BigInteger))
        { if (!a.Equals(b)) Differences.Add(path + ": " + a + " vs " + b); return; }
        if (!type.IsValueType)
        {
            if (ReferenceEquals(a, b)) { Shared.Add(a); return; }
            if (!visited.Add((a, b))) return;
        }
        if (a is Delegate) { Differences.Add(path + ": delegate in a saved graph"); return; }
        if (a is IList left)
        {
            var right = (IList)b;
            if (left.Count != right.Count) { Differences.Add(path + ": count " + left.Count + " vs " + right.Count); return; }
            for (int i = 0; i < left.Count; i++) Walk(left[i], right[i], path + "[" + i + "]");
            return;
        }
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string name = field.Name.StartsWith("<", StringComparison.Ordinal) ? field.Name.Substring(1, field.Name.IndexOf('>') - 1) : field.Name;
                if (Transient.Contains(name)) continue;
                Walk(field.GetValue(a), field.GetValue(b), path + "." + name);
            }
    }

    private sealed class PairComparer : IEqualityComparer<(object, object)>
    {
        public static readonly PairComparer Instance = new();
        public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
        public int GetHashCode((object, object) p) => RuntimeHelpers.GetHashCode(p.Item1) * 31 + RuntimeHelpers.GetHashCode(p.Item2);
    }

    /// <summary>Types a snapshot may share with the live graph: they have no setters, or Core treats them as derived.</summary>
    public static bool Immutable(object shared) => shared is WeatherCommitment || shared is FoodOfferMetadata || shared is FoodDecisionTuning
        || shared is FoodCampaignImage || shared is MarketResearchReport || shared is ResearchProduct || shared is RoutineKeys
        || shared is Route route && route.IsCommitted;
}

/// <summary>
/// F7c: sets every public writable property reachable from a <see cref="CityState"/> to a distinct non-default value, so a
/// copy that forgets a member fails <see cref="GraphAudit"/>. A property of a type this filler does not know fails the
/// test, so a new saved member forces the capture and this filler to be updated together.
/// </summary>
internal sealed class GraphFill
{
    private int next = 1;
    public readonly List<string> Unknown = new();

    public CityState City()
    {
        var state = new CityState();
        Fill(state, 0);
        return state;
    }

    private void Fill(object target, int depth)
    {
        foreach (var property in target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetSetMethod() == null || property.GetIndexParameters().Length != 0) continue;
            object? value = Value(property.PropertyType, depth, target.GetType().Name + "." + property.Name);
            if (value != null) property.SetValue(target, value);
        }
        if (target is ResidentMeal meal) meal.AssignHost("host:" + next++, 1);
    }

    private object? Value(Type type, int depth, string where)
    {
        int n = next++;
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null) return Value(nullable, depth, where);
        if (type == typeof(string)) return "s" + n;
        if (type == typeof(int)) return n;
        if (type == typeof(long)) return (long)n;
        if (type == typeof(double)) return n + 0.5;
        if (type == typeof(bool)) return true;
        if (type.IsEnum) { var values = Enum.GetValues(type); return values.GetValue(Math.Min(1, values.Length - 1)); }
        if (type == typeof(BigInteger)) return new BigInteger(n);
        if (type == typeof(int[])) return new int[] { n, n + 1 };
        if (type == typeof(Money)) return Money.FromUnits(n);
        if (type == typeof(Position)) return new Position(n, n + 0.25, n + 0.5);
        if (type == typeof(long[])) return new long[] { n, n + 1 };
        if (type == typeof(ResearchLedger)) return ResearchSaveTests.AllStates();
        if (type == typeof(RivalOutflowHistory))
        {
            var history = new RivalOutflowHistory { StartDay = n, ThroughDay = n + 1, PartialStart = true, Baseline = n, ExpiredUnits = n + 1, ExpiredCount = n + 2 };
            history.Rows.Add(new RivalOutflowHistory.Row { Day = n, Units = n + 3, Count = n + 4 });
            return history;
        }
        if (type == typeof(WeatherCommitment) || type == typeof(FoodOfferMetadata) || type == typeof(FoodDecisionTuning) || type == typeof(FoodCampaignImage))
            return null; // immutable, shared by reference
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(type)!;
            var item = Value(type.GetGenericArguments()[0], depth + 1, where + "[]");
            if (item != null) list.Add(item);
            return list;
        }
        if (type.Namespace == "PersistentCity.Core" && (type.IsValueType || type.IsClass && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null))
        {
            if (depth > 6) return null;
            var value = Activator.CreateInstance(type)!;
            Fill(value, depth + 1);
            if (value is FoodProductSales sales) sales.PreserveOpaqueIdentity();
            return value;
        }
        Unknown.Add(where + ": " + type.Name);
        return null;
    }
}
