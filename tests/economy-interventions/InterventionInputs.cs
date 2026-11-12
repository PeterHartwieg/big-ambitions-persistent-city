using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.EconomyInterventions;

public enum InputKind
{
    ProductPrice, MealsPerHour, Hours, Stock, Door, SiteOpen, RouteTimePercent,
    WorkplaceClosed, WorkerRoutesBlockedPercent, RivalPrice, RivalLanes,
}

public sealed record InterventionInput(double Minute, InputKind Kind, string Target, string Product, int Value, int Value2 = 0)
{
    public string Json() => "{\"minute\":" + Minute.ToString("R", CultureInfo.InvariantCulture)
        + ",\"kind\":\"" + Kind + "\",\"target\":\"" + Target + "\",\"product\":\"" + Product
        + "\",\"value\":" + Value.ToString(CultureInfo.InvariantCulture) + ",\"value2\":" + Value2.ToString(CultureInfo.InvariantCulture) + "}";
}

public sealed class InputViolation : Exception { public InputViolation(string message) : base(message) { } }

public sealed class InputSchedule
{
    private readonly Dictionary<long, InterventionInput[]> byMinute;
    public IReadOnlyList<InterventionInput> Inputs { get; }

    public InputSchedule(IEnumerable<InterventionInput> inputs, double startMinute, double endMinute)
    {
        var list = inputs.ToList();
        foreach (var input in list)
        {
            if (double.IsNaN(input.Minute) || double.IsInfinity(input.Minute) || input.Minute != Math.Floor(input.Minute))
                throw new InputViolation("input not at an epoch boundary: " + input.Json());
            if (input.Minute <= startMinute || input.Minute >= endMinute)
                throw new InputViolation("input outside the run window: " + input.Json());
        }
        Inputs = list.Select((input, index) => (input, index)).OrderBy(p => p.input.Minute).ThenBy(p => p.index).Select(p => p.input).ToArray();
        byMinute = Inputs.GroupBy(i => (long)i.Minute).ToDictionary(g => g.Key, g => g.ToArray());
    }

    public bool HasInputsAt(long epoch) => byMinute.ContainsKey(epoch);

    public int PublishAt(long epoch, bool endingEpochDrained, double logicalMinute, EconomyMarket market)
    {
        if (!byMinute.TryGetValue(epoch, out var due)) return 0;
        if (!endingEpochDrained) throw new InputViolation("input published before the ending epoch drained at " + epoch);
        if (logicalMinute >= epoch) throw new InputViolation("input published after epoch " + epoch + " was observed");
        foreach (var input in due) market.Apply(input, epoch);
        return due.Length;
    }
}
