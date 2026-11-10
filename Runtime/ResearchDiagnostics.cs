using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Entities;
using Helpers;
using IngameDebugConsole;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>C5f agency messages (plan C5 section 3.7). After the native daily bill, the agency contact the order
    /// was placed with gets one native TextMessage: the delivery message for Paid, the decline message for Declined.
    /// No ledger, money or campaign change. Main thread only.</summary>
    internal static class ResearchMessages
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;
        private static bool contained;

        /// <summary>Empty when the contract check passed.</summary>
        internal static string Status { get; private set; } = "Research messages are not checked.";
        internal static bool ContractPassed { get; private set; }
        internal static int Sent { get; private set; }
        internal static int Skipped { get; private set; }

        /// <summary>The native contact message members, with exact signatures, read by reflection (plan C5 section 3.6).
        /// A mismatch keeps research unavailable for the session with a status line.</summary>
        internal static bool Check()
        {
            string? refusal;
            try { refusal = Contract(); }
            catch (Exception error) { refusal = "contract check failed: " + error.Message; }
            ContractPassed = refusal == null;
            Status = refusal == null ? "" : "Research messages off: " + refusal;
            if (refusal != null) Debug.LogWarning("[Persistent City] " + Status);
            return ContractPassed;
        }

        private static string? Contract()
        {
            if (typeof(TextMessage).GetConstructor(new[] { typeof(string), typeof(Dictionary<string, string>), typeof(bool), typeof(bool), typeof(bool), typeof(AdditionalMessageData) }) == null)
                return "TextMessage(key, data, read, isNewInteraction, isSpecialMessage, additionalData) changed.";
            var send = typeof(Contact).GetMethod("SendMessage", Instance, null, new[] { typeof(TextMessage), typeof(bool), typeof(bool) }, null);
            if (send == null || send.ReturnType != typeof(void)) return "Contact.SendMessage(TextMessage, bool, bool) changed.";
            string? field = Field(typeof(Contact), "id", typeof(string)) ?? Field(typeof(Contact), "description", typeof(string))
                ?? Field(typeof(Contact), "streetName", typeof(string)) ?? Field(typeof(Contact), "streetNumber", typeof(int))
                ?? Field(typeof(TextMessage), "messageKey", typeof(string)) ?? Field(typeof(TextMessage), "messageData", typeof(Dictionary<string, string>))
                ?? Field(typeof(GameInstance), "Contacts", typeof(List<Contact>))
                ?? Field(typeof(GameInstance), "BuildingRegistrations", typeof(List<BuildingRegistration>))
                ?? Field(typeof(BuildingRegistration), "BusinessName", typeof(string)) ?? Field(typeof(BuildingRegistration), "businessTypeName", typeof(string))
                ?? Field(typeof(BuildingRegistration), "StreetName", typeof(string)) ?? Field(typeof(BuildingRegistration), "StreetNumber", typeof(int));
            if (field != null) return field;
            if (typeof(Address).GetConstructor(new[] { typeof(string), typeof(int) }) == null) return "Address(string, int) changed.";
            var registration = typeof(BuildingHelper).GetMethod("GetBuildingRegistration", Static, null, new[] { typeof(Address) }, null);
            if (registration == null || registration.ReturnType != typeof(BuildingRegistration)) return "BuildingHelper.GetBuildingRegistration(Address) changed.";
            return null;
        }

        private static string? Field(Type type, string name, Type fieldType)
        {
            var field = type.GetField(name, Instance);
            return field != null && field.FieldType == fieldType ? null : type.Name + "." + name + " changed.";
        }

        /// <summary>Sends one notice. Contained: a failure is logged once and native RunDaily continues.</summary>
        internal static void Send(ResearchNotice notice)
        {
            try
            {
                var save = SaveGameManager.Current;
                string? business = ContractPassed && save != null ? BusinessName(save, notice.BusinessAddress) : null;
                var contact = business != null ? AgencyContact(save!, notice.AgencyAddress) : null;
                if (contact == null)
                {
                    Skipped++;
                    Debug.LogWarning("[Persistent City] Research " + notice.Kind + " message for " + notice.OrderId + " not sent: "
                        + (!ContractPassed ? Status : business == null ? "business not found." : "agency contact not found."));
                    return;
                }
                contact.SendMessage(new TextMessage(notice.MessageKey, new Dictionary<string, string>(1, StringComparer.Ordinal)
                {
                    { ResearchNoticeText.BusinessNameArgument, business! }
                }));
                Sent++;
                Debug.Log("[Persistent City] Research " + notice.Kind + " message sent for " + notice.OrderId + ".");
            }
            catch (Exception error)
            {
                Skipped++;
                if (!contained) { contained = true; Debug.LogWarning("[Persistent City] Research message was contained: " + error); }
            }
        }

        private static string? BusinessName(GameInstance save, string place)
        {
            var registrations = save.BuildingRegistrations;
            if (registrations == null) return null;
            foreach (var registration in registrations)
                if (registration != null && !string.IsNullOrEmpty(registration.BusinessName) && !string.IsNullOrEmpty(registration.StreetName)
                    && string.Equals(NativeFoodMarket.PlaceId(registration), place, StringComparison.Ordinal)) return registration.BusinessName;
            return null;
        }

        // The contact native Contact.GetContact keeps for the agency registration (same id and description), else the
        // contact at the agency's address. Never creates a contact.
        private static Contact? AgencyContact(GameInstance save, string agency)
        {
            var contacts = save.Contacts;
            if (contacts == null || !ResearchNoticeText.TryParseAgency(agency, out var street, out int number)) return null;
            var registration = BuildingHelper.GetBuildingRegistration(new Address(street, number));
            if (registration != null && !string.IsNullOrEmpty(registration.BusinessName))
                foreach (var contact in contacts)
                    if (contact != null && contact.id == registration.BusinessName && contact.description == registration.businessTypeName) return contact;
            foreach (var contact in contacts)
                if (contact != null && contact.streetNumber == number && string.Equals(contact.streetName, street, StringComparison.Ordinal)) return contact;
            return null;
        }
    }

    /// <summary>C5f `pcresearch` console lines, a separate command name so the census file is untouched:
    /// `pcresearch status`, `pcresearch orders`, `pcresearch report &lt;id&gt;` (numbers only for Paid) and
    /// `pcresearch window open|close`. Read only; work only on request.</summary>
    public sealed partial class PopulationRuntime
    {
        private const string ResearchUsage = "Use: pcresearch status | pcresearch orders | pcresearch report <id> | pcresearch window open|close";
        private readonly ResearchReportWindow researchWindow = new ResearchReportWindow();
        private bool researchCommands;
        private Func<string, string>? researchNames;

        private void RegisterResearchDiagnostics()
        {
            try
            {
                CommandHelper.AddCommand<string>("pcresearch", "Persistent City market research: pcresearch status | pcresearch orders", ResearchCommand);
                CommandHelper.AddCommand<string, string>("pcresearch", "Persistent City market research: pcresearch report <id> | pcresearch window open|close", ResearchDetailCommand);
                researchCommands = true;
            }
            catch (Exception error) { log?.Warn("Research console command not registered: " + error.Message); }
        }

        private void UnregisterResearchDiagnostics()
        {
            researchWindow.Hide();
            if (!researchCommands) return;
            researchCommands = false;
            try { CommandHelper.RemoveCommand<string>(ResearchCommand); }
            catch (Exception error) { log?.Warn("Research console command not removed: " + error.Message); }
            try { CommandHelper.RemoveCommand<string, string>(ResearchDetailCommand); }
            catch (Exception error) { log?.Warn("Research console detail command not removed: " + error.Message); }
        }

        private ResearchLedger? researchSnapshot;
        private CitySimulator? researchSnapshotCity;
        private float researchSnapshotUntil;
        /// <summary>For the open research window: a detached copy of the attached city's ledger (F9e), or null. Taken at most
        /// every 0.25 s while the window draws, so OnGUI does not copy the ledger on every call; the window itself rebuilds at
        /// most once a second.</summary>
        private ResearchLedger? ResearchLedgerNow
        {
            get
            {
                var city = failed || !ReferenceEquals(saveIdentity, SaveGameManager.Current) ? null : simulation;
                if (city == null) { researchSnapshot = null; researchSnapshotCity = null; return null; }
                float now = Time.unscaledTime;
                if (!ReferenceEquals(city, researchSnapshotCity) || now >= researchSnapshotUntil)
                { researchSnapshot = CityViewReports.ResearchSnapshot(city.View); researchSnapshotCity = city; researchSnapshotUntil = now + .25f; }
                return researchSnapshot;
            }
        }
        /// <summary>For console commands: a fresh detached copy of the attached city's ledger, or null.</summary>
        private ResearchLedger? ResearchLedgerFresh => failed || simulation == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current)
            ? null : CityViewReports.ResearchSnapshot(simulation.View);
        /// <summary>The attached city's research through its read-only view, or null.</summary>
        private IResearchView? ResearchViewNow => failed || simulation == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current)
            ? null : simulation.View.Research;

        private string ResearchBusinessName(string place) => foodMarket?.NameOf(place) ?? place;

        private void ResearchCommand(string action)
        {
            if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase)) Debug.Log("[Persistent City] research status (read-only)\n" + ResearchStatusText());
            else if (string.Equals(action, "orders", StringComparison.OrdinalIgnoreCase)) Debug.Log("[Persistent City] research orders (read-only)\n" + ResearchOrdersText());
            else Debug.LogWarning("[Persistent City] Unknown command. " + ResearchUsage);
        }

        private void ResearchDetailCommand(string action, string detail)
        {
            if (string.Equals(action, "report", StringComparison.OrdinalIgnoreCase))
            {
                var ledger = ResearchLedgerFresh;
                var order = ledger?.Find(detail);
                if (ledger == null) Debug.LogWarning("[Persistent City] research unavailable: no active Persistent City population in this save.");
                else if (order == null) Debug.LogWarning("[Persistent City] Unknown research order: " + detail);
                else Debug.Log("[Persistent City] research report (read-only)\n" + ResearchReportText.Report(ledger, order, ResearchBusinessName(order.BusinessAddress)));
                return;
            }
            if (string.Equals(action, "window", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(detail, "open", StringComparison.OrdinalIgnoreCase) || string.Equals(detail, "close", StringComparison.OrdinalIgnoreCase)))
            {
                bool open = string.Equals(detail, "open", StringComparison.OrdinalIgnoreCase);
                if (open) researchWindow.Show(); else researchWindow.Hide();
                Debug.Log("[Persistent City] research window=" + (open ? "open" : "closed"));
                return;
            }
            Debug.LogWarning("[Persistent City] Unknown command. " + ResearchUsage);
        }

        private string ResearchStatusText()
        {
            var activation = settings.MarketResearch.Activation;
            var ledger = ResearchViewNow;
            var boundary = ResearchBilling.LastBoundary;
            var text = new StringBuilder(512);
            text.Append("available=").Append(activation.Available ? "true" : "false");
            if (!activation.Available) text.Append(" (").Append(activation.Refusal).Append(')');
            text.AppendLine();
            text.Append("attached=").Append(activation.Attached).Append(" dialogContract=").Append(activation.DialogContract)
                .Append(" billingContract=").Append(activation.BillingContract).Append(" deliveryContract=").Append(activation.DeliveryContract)
                .Append(" ledgerAttached=").Append(activation.LedgerAttached).AppendLine();
            foreach (var line in new[] { ResearchAgencyDialog.Status, ResearchBilling.Status, ResearchMessages.Status })
                if (line.Length > 0) text.AppendLine(line);
            text.Append("lastCampaignScreen=").Append(ResearchAgencyDialog.LastRefusal).AppendLine();
            text.Append("orders open=").Append(ledger?.OpenOrders.Count ?? 0).Append(" delivered=").Append(ledger?.KeptOrders.Count ?? 0)
                .Append(" closed=").Append(ledger?.ClosedOrders.Count ?? 0).Append(" nextSequence=").Append(ledger?.NextSequence ?? 0).AppendLine();
            text.Append("lastBill attempts=").Append(boundary.Attempts).Append(" paid=").Append(boundary.Paid + boundary.PaidFromScan)
                .Append(" declined=").Append(boundary.Declined + boundary.BalancePrecision).Append(" cancelled=").Append(boundary.Cancelled)
                .Append(" paymentUnknown=").Append(boundary.PaymentUnknown).Append(" deferred=").Append(boundary.Deferred).AppendLine();
            text.Append("messages sent=").Append(ResearchMessages.Sent).Append(" skipped=").Append(ResearchMessages.Skipped).AppendLine();
            text.Append("window=").Append(researchWindow.Open ? "open" : "closed").Append(" builds=").Append(researchWindow.Builds);
            return text.ToString();
        }

        private string ResearchOrdersText()
        {
            var ledger = ResearchLedgerFresh;
            if (ledger == null) return "No active Persistent City population in this save.";
            var text = new StringBuilder(1024);
            foreach (var order in ResearchReportText.Orders(ledger))
                text.AppendLine(ResearchReportText.Line(order, ResearchBusinessName(order.BusinessAddress)));
            if (text.Length == 0) text.Append("No research orders.");
            return text.ToString();
        }
    }
}
