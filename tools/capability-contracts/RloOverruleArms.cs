using System;
using System.Linq;
using System.Reflection;
using AI.Customers.CustomerEntries;
using PersistentCity.Core;
using PersistentCity.Runtime;

// RLO 1.0 overrule (review Sol-r1-4): the real HookConflicts calculator scan on the stub factory, and the checkout
// decision of one city load with the Living population switch readable and unreadable.
internal sealed class RloRetailCalculator : CustomerEntriesCalculatorRetail { }

internal static partial class Program
{
    private static void RloOverruleArms()
    {
        var retail = typeof(CustomerEntriesCalculatorFactory).GetField("CustomerEntriesCalculatorRetail", BindingFlags.Static | BindingFlags.NonPublic)!;
        object native = retail.GetValue(null)!;
        try
        {
            Check(HookConflicts.CalculatorReplacements().Count == 0, "native calculators: no replacement");
            retail.SetValue(null, new RloRetailCalculator()); // as RLO's PopulationGame.InstallCalculators assigns its subclass
            var found = HookConflicts.CalculatorReplacements();
            Check(found.Count == 1 && found[0].Target == "CustomerEntriesCalculatorFactory.CustomerEntriesCalculatorRetail"
                && found[0].NativeType == "CustomerEntriesCalculatorRetail" && found[0].ReplacementType == typeof(RloRetailCalculator).FullName,
                "replaced calculator found: " + string.Join(", ", found.Select(f => f.Target + "=" + f.ReplacementType)));
            var scan = HookConflicts.Scan();
            Check(scan.Replacements.Count == 1 && scan.CheckoutRefusal == null, "a replacement is reported and does not refuse: " + scan.CheckoutRefusal);
            Check(scan.Text().Contains("[non-Harmony]") && scan.ActivationLine().Contains("replaced without Harmony"), "replacement named in both lines");

            RloOverrules.Reading = new RloLivingReading(RloLivingState.On);
            var on = NativeFoodContract.CheckoutForLoad(false, out bool settled);
            Check(settled && on.Refusal == null, "Living on with a replaced calculator: checkout active: " + on.Refusal);
            RloOverrules.Reading = new RloLivingReading(RloLivingState.Unknown, "missing public static field RealLifeOverhaul.Settings.LivingPopulation");
            var unknown = NativeFoodContract.CheckoutForLoad(false, out _);
            Check(unknown.Refusal != null && unknown.Refusal.Contains("Living population state unknown"), "unknown switch refuses: " + unknown.Refusal);
            Console.WriteLine("rlo overrule: calculator replacement reported, not refusing; Living on active; unknown refuses food.checkout.shop");
        }
        finally
        {
            retail.SetValue(null, native);
            RloOverrules.Reading = new RloLivingReading(RloLivingState.Absent);
            HookConflicts.TakeLoadScan();
        }
    }
}
