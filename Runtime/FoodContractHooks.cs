using System;
using System.Collections.Generic;
using System.Threading;

namespace PersistentCity.Runtime
{
    public enum FoodContractHookKind : byte
    {
        Catalogue,
        Registration,
        Holder,
        Quantity,
        Unattributed,
    }

    public sealed class FoodContractHook
    {
        internal FoodContractHook(int id, string writer, string type, string method, bool isStatic, FoodContractHookKind kind, params string[] parameters)
        { Id = id; Writer = writer; Type = type; Method = method; IsStatic = isStatic; Kind = kind; Parameters = Array.AsReadOnly(parameters); }
        public int Id { get; }
        public string Writer { get; }
        public string Type { get; }
        public string Method { get; }
        public bool IsStatic { get; }
        public FoodContractHookKind Kind { get; }
        public IReadOnlyList<string> Parameters { get; }
        public string Signature => (IsStatic ? "static " : "") + Type + "." + Method + "(" + string.Join(", ", Parameters) + ")";
    }

    public static class FoodContractHookTable
    {
        public const int E5aMembers = 19;
        public static readonly IReadOnlyList<FoodContractHook> All = Array.AsReadOnly(new[]
        {
            new FoodContractHook(0, "W11", "ItemsGetter", "OnItemsLoaded", true, FoodContractHookKind.Catalogue, "IList`1"),
            new FoodContractHook(1, "W11", "ItemsGetter", "RegisterModItem", true, FoodContractHookKind.Catalogue, "Item"),
            new FoodContractHook(2, "W11", "ItemsGetter", "UnregisterModItem", true, FoodContractHookKind.Catalogue, "String"),
            new FoodContractHook(3, "W11", "ItemsGetter", "ResetStaticData", true, FoodContractHookKind.Catalogue),
            new FoodContractHook(4, "W8", "BusinessHelper", "UpdateCachedAvailableProducts", true, FoodContractHookKind.Registration, "BuildingRegistration"),
            new FoodContractHook(5, "W9", "BuildingRegistration", "AddItemInstanceToBuilding", false, FoodContractHookKind.Registration, "ItemInstance"),
            new FoodContractHook(6, "W9", "BuildingRegistration", "RemoveItemInstanceFromBuilding", false, FoodContractHookKind.Registration, "ItemInstance", "Boolean"),
            new FoodContractHook(7, "W9", "BuildingRegistration", "OnBusinessTypeChanged", false, FoodContractHookKind.Registration),
            new FoodContractHook(8, "W9", "BuildingRegistration", "Reset", false, FoodContractHookKind.Registration, "Boolean"),
            new FoodContractHook(9, "W3", "ItemHelper", "FillUpShowcaseShelfOrPointOfSale", true, FoodContractHookKind.Quantity, "ItemInstance"),
            new FoodContractHook(10, "W1", "ItemHelper", "SubtractFromStock", true, FoodContractHookKind.Quantity, "ItemInstance", "Int32"),
            new FoodContractHook(11, "W5", "ItemInstance", "AddToCargo", false, FoodContractHookKind.Holder, "CargoInstance", "Int32"),
            new FoodContractHook(12, "W5", "ItemInstance", "RemoveFromCargo", false, FoodContractHookKind.Holder, "CargoInstance"),
            new FoodContractHook(13, "W5", "ItemInstance", "ReduceFromCargo", false, FoodContractHookKind.Quantity, "CargoInstance", "Int32"),
            new FoodContractHook(14, "W5", "ItemInstance", "MergeCargo", false, FoodContractHookKind.Quantity, "CargoInstance", "CargoInstance"),
            new FoodContractHook(15, "W5", "ItemInstance", "MergeCargo", false, FoodContractHookKind.Quantity, "CargoInstance", "CargoInstance", "Int32"),
            new FoodContractHook(16, "W2", "CargoInstance", "MergeAmount", false, FoodContractHookKind.Quantity, "CargoInstance", "Int32"),
            new FoodContractHook(17, "W2", "ReStockingHelper", "RefillSingleItemWithLimit", true, FoodContractHookKind.Quantity, "ItemInstance"),
            new FoodContractHook(18, "W2", "ReStockingHelper", "TryAddStockAmount", true, FoodContractHookKind.Quantity, "ItemInstance", "CargoInstance", "Int32"),
            new FoodContractHook(19, "W6", "CargoInstance", "ParseIntoItemInstance", false, FoodContractHookKind.Holder, "ItemInstance"),
            new FoodContractHook(20, "W9", "BuildingRegistration", "TemporarilyClose", false, FoodContractHookKind.Registration, "Boolean"),
            new FoodContractHook(21, "W8", "BuildingRegistration", "RemoveUnusedRetailPrices", false, FoodContractHookKind.Registration),
            new FoodContractHook(22, "W7", "CargoInstance", "ResetItemCached", false, FoodContractHookKind.Unattributed),
        });
    }

    public enum FoodContractHint : byte { NewUnit, Coalesced, Unknown }

    public interface IFoodContractHintSink
    {
        FoodContractHint Catalogue();
        FoodContractHint Registration(object registration);
        FoodContractHint Holder(string street, int number);
        FoodContractHint AllShops();
    }

    public interface IFoodContractPatcher
    {
        string? Patch(FoodContractHook hook);
        void UnpatchAll();
    }

    public sealed class FoodContractHintRouter : IDisposable
    {
        private static FoodContractHintRouter? active;
        private static long generation;
        public static FoodContractHintRouter? Active => active;
        public static long Generation => Interlocked.Read(ref generation);

        private IFoodContractHintSink? sink;
        private IFoodContractPatcher? patcher;
        private readonly int mainThread;
        private readonly long[] calls = new long[FoodContractHookTable.All.Count];
        private readonly long[] newUnits = new long[FoodContractHookTable.All.Count];
        private readonly long[] coalesced = new long[FoodContractHookTable.All.Count];
        private readonly long[] unknown = new long[FoodContractHookTable.All.Count];
        private readonly string?[] failures = new string?[FoodContractHookTable.All.Count];
        private long offThread;
        private int pendingCatalogue, pendingShops;

        private FoodContractHintRouter(IFoodContractHintSink sink, IFoodContractPatcher patcher, int mainThread)
        { this.sink = sink; this.patcher = patcher; this.mainThread = mainThread; }

        public static FoodContractHintRouter Install(IFoodContractHintSink sink, IFoodContractPatcher patcher, int mainThread)
        {
            if (sink == null || patcher == null) throw new ArgumentNullException(sink == null ? nameof(sink) : nameof(patcher));
            if (active != null) throw new InvalidOperationException("Food contract hooks are already installed.");
            var router = new FoodContractHintRouter(sink, patcher, mainThread);
            router.InstalledGeneration = Interlocked.Increment(ref generation);
            active = router;
            foreach (var hook in FoodContractHookTable.All)
            {
                try { router.failures[hook.Id] = patcher.Patch(hook); }
                catch (Exception e) { router.failures[hook.Id] = e.GetType().Name + ": " + e.Message; }
            }
            return router;
        }

        public long InstalledGeneration { get; private set; }
        public bool Attached => sink != null;
        public bool Disposed { get; private set; }
        public long OffThreadCalls => Interlocked.Read(ref offThread);
        public long Faults { get; private set; }
        public string? LastFault { get; private set; }
        public string? Failure(int hook) => failures[hook];
        public bool AllInstalled { get { foreach (var f in failures) if (f != null) return false; return true; } }
        public int InstalledCount { get { int n = 0; foreach (var f in failures) if (f == null) n++; return Disposed ? 0 : n; } }
        public long Calls(int hook) => calls[hook];
        public long NewUnits(int hook) => newUnits[hook];
        public long CoalescedHints(int hook) => coalesced[hook];
        public long UnknownHints(int hook) => unknown[hook];
        public long TotalCalls { get { long n = 0; foreach (var c in calls) n += c; return n; } }
        public long TotalNewUnits { get { long n = 0; foreach (var c in newUnits) n += c; return n; } }

        private bool Enter(int hook, FoodContractHookKind kind)
        {
            if (Environment.CurrentManagedThreadId != mainThread)
            {
                Interlocked.Increment(ref offThread);
                if (kind == FoodContractHookKind.Catalogue) Interlocked.Exchange(ref pendingCatalogue, 1);
                else if (kind != FoodContractHookKind.Quantity && kind != FoodContractHookKind.Unattributed)
                    Interlocked.Exchange(ref pendingShops, 1);
                return false;
            }
            calls[hook]++;
            return sink != null;
        }
        private void Count(int hook, FoodContractHint hint)
        {
            if (hint == FoodContractHint.NewUnit) newUnits[hook]++;
            else if (hint == FoodContractHint.Coalesced) coalesced[hook]++;
            else unknown[hook]++;
        }
        private void Fault(Exception e) { Faults++; LastFault = e.GetType().Name + ": " + e.Message; }

        public static void Catalogue(int hook)
        {
            var r = active;
            if (r == null || !r.Enter(hook, FoodContractHookKind.Catalogue)) return;
            try { r.Count(hook, r.sink!.Catalogue()); } catch (Exception e) { r.Fault(e); }
        }
        public static void Registration(int hook, object? registration)
        {
            var r = active;
            if (r == null || !r.Enter(hook, FoodContractHookKind.Registration)) return;
            if (registration == null) { r.unknown[hook]++; return; }
            try { r.Count(hook, r.sink!.Registration(registration)); } catch (Exception e) { r.Fault(e); }
        }
        public static void Holder(int hook, string? street, int number)
        {
            var r = active;
            if (r == null || !r.Enter(hook, FoodContractHookKind.Holder)) return;
            if (string.IsNullOrEmpty(street)) { r.unknown[hook]++; return; }
            try { r.Count(hook, r.sink!.Holder(street!, number)); } catch (Exception e) { r.Fault(e); }
        }
        public static void Quantity(int hook)
        {
            var r = active;
            if (r != null) r.Enter(hook, FoodContractHookKind.Quantity);
        }
        public static void Unattributed(int hook)
        {
            var r = active;
            if (r != null) r.Enter(hook, FoodContractHookKind.Unattributed);
        }

        public void Drain()
        {
            if (sink == null) return;
            bool catalogue = Interlocked.Exchange(ref pendingCatalogue, 0) != 0;
            bool shops = Interlocked.Exchange(ref pendingShops, 0) != 0;
            try
            {
                if (catalogue) sink.Catalogue();
                else if (shops) sink.AllShops();
            }
            catch (Exception e) { Fault(e); }
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (ReferenceEquals(active, this)) active = null;
            Interlocked.Increment(ref generation);
            var p = patcher; patcher = null; sink = null;
            try { p?.UnpatchAll(); } catch (Exception e) { Fault(e); }
        }
    }
}
