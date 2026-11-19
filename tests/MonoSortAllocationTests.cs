using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class MonoSortAllocationTests
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(c => c.Value);

    private static int OperandSize(OpCode code, byte[] il, int at) => code.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, at),
        _ => 4,
    };

    internal static IEnumerable<MethodBase> Calls(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null) yield break;
        for (int at = 0; at < il.Length;)
        {
            short value = il[at] == 0xFE ? (short)(0xFE00 | il[at + 1]) : il[at];
            at += il[at] == 0xFE ? 2 : 1;
            var code = Codes[value];
            if (code == OpCodes.Call || code == OpCodes.Callvirt)
            {
                MethodBase? target = null;
                try
                {
                    target = method.Module.ResolveMethod(BitConverter.ToInt32(il, at),
                        method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null,
                        method.IsGenericMethod ? method.GetGenericArguments() : null);
                }
                catch (ArgumentException) { }
                if (target != null) yield return target;
            }
            at += OperandSize(code, il, at);
        }
    }

    internal static bool AllocatesUnderMono(MethodBase target)
    {
        if (target.Name != "Sort") return false;
        var type = target.DeclaringType;
        var parameters = target.GetParameters();
        if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return parameters.All(p => !p.ParameterType.IsGenericType || p.ParameterType.GetGenericTypeDefinition() != typeof(Comparison<>));
        if (type == typeof(Array))
            return parameters.All(p => !p.ParameterType.IsGenericType || p.ParameterType.GetGenericTypeDefinition() != typeof(Comparison<>));
        return false;
    }

    private static readonly HashSet<string> Cold = new HashSet<string>(StringComparer.Ordinal)
    {
        "BusinessTypeIndex.Rebuild", "RivalFoodSettings.ClampServiceLimits", "RivalServiceLoad.Image", "GcPauseSampler.Spread", "GcPauseSampler.PauseOverNeighboursSpread",
    };

    private static IEnumerable<(string Site, string Call)> AllocatingSorts(Assembly assembly, Func<Type, bool> include)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in assembly.GetTypes().Where(include))
            foreach (var method in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)))
                foreach (var call in Calls(method).Where(AllocatesUnderMono))
                {
                    var owner = type;
                    while (owner.DeclaringType != null && owner.Name.StartsWith("<", StringComparison.Ordinal)) owner = owner.DeclaringType;
                    yield return (owner.Name + "." + method.Name, call.DeclaringType!.Name + "." + call);
                }
    }

    [Fact]
    public void ListSortWithAComparerAllocatesADelegateInTheGameRuntimeSoPerTickCodeUsesCachedComparisons()
    {
        var found = AllocatingSorts(typeof(CitySimulator).Assembly, _ => true)
            .Concat(AllocatingSorts(typeof(PhysicalPopulation).Assembly, t => t.Namespace == "PersistentCity.Runtime" && t.FullName!.StartsWith("PersistentCity.Runtime.PhysicalPopulation", StringComparison.Ordinal)))
            .Where(f => !Cold.Contains(f.Site))
            .Select(f => f.Site + " -> " + f.Call).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found));
    }

    [Fact]
    public void TheScanFindsAnAllocatingSort()
    {
        var method = typeof(MonoSortAllocationTests).GetMethod(nameof(Sample), BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Contains(Calls(method), AllocatesUnderMono);
        var cached = typeof(MonoSortAllocationTests).GetMethod(nameof(CachedSample), BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.DoesNotContain(Calls(cached), AllocatesUnderMono);
    }

    private static readonly Comparison<int> Ascending = (a, b) => a.CompareTo(b);
    private static void Sample(List<int> values) => values.Sort();
    private static void CachedSample(List<int> values) => values.Sort(Ascending);
}
