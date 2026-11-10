using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.DayNightCycle;
using HarmonyLib;
using Helpers;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>C5d research billing (plan C5 sections 3.5 and 3.6). A postfix on native MarketingHelper.RunDaily
    /// bills each Surveyed order with its own ChangeMoneySafe call, before native CreateFinancialSummary(Day - 1).
    /// Never touches native campaigns. C5e wires Install and Attach; C5f sends the agency message after the bill.
    /// Main thread only.</summary>
    internal static class ResearchBilling
    {
        internal const string PatchId = "persistentcity.research-billing";
        private static readonly Harmony patches = new Harmony(PatchId);
        private static MethodInfo? runDaily;
        private static ResearchChargeSession? session;
        private static Func<bool>? sessionActive;
        private static readonly ResearchNoticeTracker notices = new ResearchNoticeTracker();
        private static bool installed;

        /// <summary>Empty when the contract check passed and the postfix is installed.</summary>
        internal static string Status { get; private set; } = "Research billing is not installed.";
        internal static bool Installed => installed;
        /// <summary>A ledger is attached and no foreign patch turned billing off since.</summary>
        internal static bool Attached => session != null;
        internal static ResearchChargeTally LastBoundary => session?.LastBoundary ?? default;

        /// <summary>Checks the native billing contract and installs the postfix. A mismatch leaves research billing off
        /// for the session with a status line and Persistent City running; nothing substitutes a fee path.</summary>
        internal static bool Install()
        {
            if (installed) return true;
            string? refusal;
            try { refusal = ResearchBillingContract.Check(out runDaily); }
            catch (Exception error) { refusal = "contract check failed: " + error.Message; }
            if (refusal == null)
            {
                try
                {
                    patches.Patch(runDaily!, postfix: new HarmonyMethod(typeof(ResearchBilling), nameof(RunDailyPostfix)));
                    installed = true;
                }
                catch (Exception error) { patches.UnpatchAll(PatchId); refusal = "postfix install failed: " + error.Message; }
            }
            Status = refusal == null ? "" : "Research billing off: " + refusal;
            if (refusal != null) Debug.LogWarning("[Persistent City] " + Status);
            return installed;
        }

        internal static void Uninstall()
        {
            session = null; sessionActive = null;
            if (installed) patches.UnpatchAll(PatchId);
            installed = false; runDaily = null;
            Status = "Research billing is not installed.";
        }

        /// <param name="active">False once Persistent City is stopped, failed or detached.</param>
        /// <param name="eligible">The research eligibility rule (player-rented food business PC owns).</param>
        /// <param name="guardedUnit">PopulationRuntime.RunSaveEpochUnit of the attached runtime.</param>
        internal static void Attach(ResearchLedger ledger, Func<bool> active, Func<BuildingRegistration, bool> eligible, Action<Action> guardedUnit)
        {
            if (!installed) throw new InvalidOperationException(Status);
            session = new ResearchChargeSession(ledger, new NativeResearchChargePort(eligible), active, guardedUnit);
            sessionActive = active;
        }

        internal static void Detach() { session = null; sessionActive = null; }

        // Native RunDaily calls this after every player business's campaign bill. Idle (no attached city or no
        // Surveyed order) reads one field and the open-order count.
        private static void RunDailyPostfix()
        {
            var current = session;
            if (current == null || !current.HasWork) return;
            try
            {
                // A foreign prefix added after install could skip native RunDaily; refuse instead of billing.
                string? foreign = ResearchBillingContract.ForeignPatch(runDaily!);
                if (foreign != null) { Status = "Research billing off: " + foreign; session = null; Debug.LogWarning("[Persistent City] " + Status); return; }
                notices.Before(current.Ledger);
                try
                {
                    var tally = current.Boundary();
                    if (tally.PaymentUnknown > 0 || tally.ChargingOff)
                        Debug.LogWarning("[Persistent City] Research billing: paymentUnknown=" + tally.PaymentUnknown + " chargingOff=" + tally.ChargingOff);
                }
                finally
                {
                    // C5f: one agency message per order moved to Paid or Declined. A runtime stopped during the boundary
                    // (for example by a refused save) publishes no later payload with the move, so it sends nothing.
                    if (sessionActive?.Invoke() == true) notices.After(ResearchMessages.Send); else notices.Forget();
                }
            }
            catch (Exception error)
            {
                // Native RunDaily must continue; any order not yet moved stays Surveyed or was blocked by its own unit.
                Debug.LogWarning("[Persistent City] Research billing was contained: " + error);
            }
        }
    }

    /// <summary>The native members research billing uses, with exact signatures, read by reflection before any code
    /// that touches them runs.</summary>
    internal static class ResearchBillingContract
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

        internal static string? Check(out MethodInfo? runDaily)
        {
#if PC_FAULT_POINTS
            if (FaultPoints.Research() is string fault) { runDaily = null; return fault; } // F8c test build only (rig check 7)
#endif
            runDaily = typeof(MarketingHelper).GetMethod("RunDaily", Static, null, Type.EmptyTypes, null);
            if (runDaily == null || runDaily.ReturnType != typeof(void)) return "MarketingHelper.RunDaily() changed.";
            var safe = typeof(GameManager).GetMethod("ChangeMoneySafe", Static, null,
                new[] { typeof(float), typeof(TransactionInfo), typeof(int?), typeof(Address), typeof(bool), typeof(bool) }, null);
            if (safe == null || safe.ReturnType != typeof(bool)) return "GameManager.ChangeMoneySafe changed.";
            if (typeof(TransactionInfo).GetConstructor(new[] { typeof(string), typeof(string), typeof(Dictionary<string, string>), typeof(bool) }) == null)
                return "TransactionInfo(type, category, data, isTaxDeductible) changed.";
            if (typeof(TransactionInfo).GetMethod("SetTaxDeductibleName", Instance, null, new[] { typeof(string) }, null) == null)
                return "TransactionInfo.SetTaxDeductibleName changed.";
            string? field = Field(typeof(Transaction), "transactionType", typeof(string)) ?? Field(typeof(Transaction), "transactionCategories", typeof(List<string>))
                ?? Field(typeof(Transaction), "amount", typeof(float)) ?? Field(typeof(Transaction), "balance", typeof(float))
                ?? Field(typeof(Transaction), "isTaxDeductible", typeof(bool)) ?? Field(typeof(Transaction), "timestamp", typeof(Timestamp))
                ?? Field(typeof(Transaction), "address", typeof(Address)) ?? Field(typeof(Transaction), "transactionData", typeof(Dictionary<string, string>))
                ?? Field(typeof(Timestamp), "Day", typeof(int))
                ?? Field(typeof(GameInstance), "Money", typeof(float)) ?? Field(typeof(GameInstance), "Day", typeof(int))
                ?? Field(typeof(GameInstance), "Transactions", typeof(Queue<Transaction>))
                ?? Field(typeof(GameInstance), "BuildingRegistrations", typeof(List<BuildingRegistration>))
                ?? Field(typeof(BuildingRegistration), "RentedByPlayer", typeof(bool)) ?? Field(typeof(BuildingRegistration), "BusinessName", typeof(string))
                ?? Field(typeof(BuildingRegistration), "StreetName", typeof(string)) ?? Field(typeof(BuildingRegistration), "StreetNumber", typeof(int));
            if (field != null) return field;
            var address = typeof(BuildingRegistration).GetProperty("Address", Instance);
            if (address == null || address.PropertyType != typeof(Address) || address.GetGetMethod() == null) return "BuildingRegistration.Address changed.";
            if (typeof(Transaction).GetField("transactionData", Instance)!.IsNotSerialized) return "Transaction.transactionData is not serialized.";
            if (!TagStoredByConstructor()) return "Transaction does not keep the research tag in its data.";
            return ForeignPatch(runDaily);
        }

        /// <summary>A prefix, transpiler or finalizer of another owner can skip or change the native bill.</summary>
        internal static string? ForeignPatch(MethodBase method)
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return null;
            foreach (var group in new[] { info.Prefixes, info.Transpilers, info.Finalizers })
                foreach (var patch in group)
                    if (patch.owner != ResearchBilling.PatchId) return "foreign Harmony patch on MarketingHelper.RunDaily by " + patch.owner + ".";
            return null;
        }

        private static string? Field(Type type, string name, Type fieldType)
        {
            var field = type.GetField(name, Instance);
            return field != null && field.FieldType == fieldType ? null : type.Name + "." + name + " changed.";
        }

        // The native Transaction constructor copies TransactionInfo.Data. A record object is built but never queued.
        private static bool TagStoredByConstructor()
        {
            var data = new Dictionary<string, string> { { ResearchCharge.BusinessNameKey, "contract" }, { ResearchCharge.TagKey, "pcr-0" } };
            var record = new Transaction(new TransactionInfo(ResearchCharge.MarketingType, ResearchCharge.MarketingCategory, data));
            return record.transactionData != null && record.transactionData.TryGetValue(ResearchCharge.TagKey, out var tag) && tag == "pcr-0";
        }
    }

    /// <summary>IResearchChargePort over native GameManager.ChangeMoneySafe and the native transaction queue.</summary>
    internal sealed class NativeResearchChargePort : IResearchChargePort
    {
        private readonly Func<BuildingRegistration, bool> eligible;
        private readonly List<ResearchChargeRecord> scratch = new List<ResearchChargeRecord>(4);
        private readonly List<int> positions = new List<int>(4);

        internal NativeResearchChargePort(Func<BuildingRegistration, bool> eligible)
        { this.eligible = eligible ?? throw new ArgumentNullException(nameof(eligible)); }

        // Checked by ResearchBillingContract before the postfix was installed.
        public bool TagStored => true;
        public float Balance => SaveGameManager.Current.Money;
        public int BilledDay => SaveGameManager.Current.Day - 1;

        public bool TryResolve(ResearchOrder order, out ResearchChargeTarget? target)
        {
            target = null;
            var registrations = SaveGameManager.Current?.BuildingRegistrations;
            if (registrations == null) return false;
            foreach (var registration in registrations)
            {
                if (registration == null || !registration.RentedByPlayer || string.IsNullOrEmpty(registration.BusinessName)) continue;
                if (NativeFoodMarket.PlaceId(registration) != order.BusinessAddress) continue;
                var address = registration.Address;
                if (address == null || !eligible(registration)) return false;
                target = new ResearchChargeTarget(registration.BusinessName, address);
                return true;
            }
            return false;
        }

        public void CollectTagged(List<ResearchChargeRecord> into)
        {
            foreach (var transaction in SaveGameManager.Current.Transactions)
                if (TryTagged(transaction, out var record)) into.Add(record);
        }

        public object? Tail()
        {
            Transaction? last = null;
            foreach (var transaction in SaveGameManager.Current.Transactions) last = transaction;
            return last;
        }

        public void CollectTaggedAfter(object? tail, List<ResearchChargeRecord> into)
        {
            // If the tail was dropped from the front, every record still queued was appended after it.
            scratch.Clear(); positions.Clear();
            int index = 0, tailIndex = -1;
            foreach (var transaction in SaveGameManager.Current.Transactions)
            {
                if (tail != null && ReferenceEquals(transaction, tail)) tailIndex = index;
                else if (TryTagged(transaction, out var record)) { scratch.Add(record); positions.Add(index); }
                index++;
            }
            for (int i = 0; i < scratch.Count; i++) if (positions[i] > tailIndex) into.Add(scratch[i]);
        }

        public bool Charge(in ResearchChargeRequest request, Action enteringNative)
        {
            var data = new Dictionary<string, string>(2)
            {
                { ResearchCharge.BusinessNameKey, request.Target.BusinessName }, { ResearchCharge.TagKey, request.OrderId }
            };
            var info = new TransactionInfo(ResearchCharge.MarketingType, ResearchCharge.MarketingCategory, data);
            if (request.TaxDeductible) info.SetTaxDeductibleName(ResearchCharge.TaxDeductibleName);
            var address = (Address)request.Target.Address;
            enteringNative();
            return GameManager.ChangeMoneySafe(request.Amount, info, request.Day, address, force: false, showNotification: false);
        }

        private static bool TryTagged(Transaction? transaction, out ResearchChargeRecord record)
        {
            record = default;
            var data = transaction?.transactionData;
            if (data == null || !data.TryGetValue(ResearchCharge.TagKey, out var tag)) return false;
            var categories = transaction!.transactionCategories;
            bool marketing = transaction.transactionType == ResearchCharge.MarketingType
                && categories != null && categories.Contains(ResearchCharge.MarketingCategory);
            record = new ResearchChargeRecord(tag, marketing, transaction.isTaxDeductible, transaction.amount, transaction.balance,
                transaction.timestamp != null ? transaction.timestamp.Day : int.MinValue, transaction.address);
            return true;
        }
    }
}
