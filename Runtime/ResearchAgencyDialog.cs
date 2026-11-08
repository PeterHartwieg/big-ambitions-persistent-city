using System;
using System.Collections.Generic;
using System.Reflection;
using Buildings;
using Dialogs;
using Entities;
using HarmonyLib;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using PersistentCity.Core;
using UI.Dialog;
using UI.Notification;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>What the attached city lends the agency dialog. Every member is read at call time, so a route rebuild
    /// (a new simulator) or a stopped runtime is seen on the next click.</summary>
    internal sealed class ResearchDesk
    {
        internal ResearchDesk(Func<bool> attached, Func<bool> available, Func<BuildingRegistration, bool> eligible,
            Func<ResearchSurveyRunner?> runner, Func<int> radiusMetres, Func<double> now, Action<string> log)
        {
            Attached = attached; Available = available; Eligible = eligible; Runner = runner; RadiusMetres = radiusMetres; Now = now; Log = log;
        }
        /// <summary>Persistent City is attached to the current save and not stopped.</summary>
        internal Func<bool> Attached { get; }
        internal Func<bool> Available { get; }
        /// <summary>Player-rented food business that Persistent City owns (plan section 9, Q4).</summary>
        internal Func<BuildingRegistration, bool> Eligible { get; }
        internal Func<ResearchSurveyRunner?> Runner { get; }
        internal Func<int> RadiusMetres { get; }
        /// <summary>The native minute of the order.</summary>
        internal Func<double> Now { get; }
        internal Action<string> Log { get; }
    }

    /// <summary>C5e research ordering in the agency call (plan C5 section 3.1). A postfix on the private native
    /// MarketingAgencyDialog.CampaignSettings() sets only the unused OnSecondOption slot and its text, and only when
    /// every ResearchDialogGate precondition holds; otherwise the native entry is returned unchanged. The button walks
    /// ResearchMenu and places the order through the C5b survey runner. Main thread only.</summary>
    internal static class ResearchAgencyDialog
    {
        internal const string PatchId = "persistentcity.research-dialog";
        private static readonly Harmony patches = new Harmony(PatchId);
        private static MethodInfo? campaignSettings, moreHelpOffered;
        private static ResearchDesk? desk;
        private static ResearchMenu? menu;
        private static bool installed;
        private static bool contained;

        /// <summary>Empty when the contract check passed and the postfix is installed.</summary>
        internal static string Status { get; private set; } = "Research dialog is not installed.";
        internal static bool Installed => installed;
        /// <summary>Why the last campaign screen stayed native (None when the button was added). Diagnostic only.</summary>
        internal static ResearchDialogRefusal LastRefusal { get; private set; } = ResearchDialogRefusal.NotAttached;

        /// <summary>Checks the native dialog contract and installs the postfix. A mismatch leaves research ordering off
        /// for the session with a status line and Persistent City running; nothing substitutes a dialog.</summary>
        internal static bool Install()
        {
            if (installed) return true;
            string? refusal;
            try { refusal = ResearchDialogContract.Check(out campaignSettings, out moreHelpOffered); }
            catch (Exception error) { refusal = "contract check failed: " + error.Message; }
            if (refusal == null)
            {
                try
                {
                    menu = ResearchMenu.Build(ResearchProducts.All);
                    patches.Patch(campaignSettings!, postfix: new HarmonyMethod(typeof(ResearchAgencyDialog), nameof(CampaignSettingsPostfix)));
                    installed = true;
                }
                catch (Exception error) { patches.UnpatchAll(PatchId); refusal = "postfix install failed: " + error.Message; }
            }
            Status = refusal == null ? "" : "Research dialog off: " + refusal;
            if (refusal != null) Debug.LogWarning("[Persistent City] " + Status);
            return installed;
        }

        internal static void Uninstall()
        {
            desk = null;
            if (installed) patches.UnpatchAll(PatchId);
            installed = false; campaignSettings = moreHelpOffered = null; menu = null;
            Status = "Research dialog is not installed.";
        }

        internal static void Attach(ResearchDesk attached) => desk = attached ?? throw new ArgumentNullException(nameof(attached));
        internal static void Detach() => desk = null;

        // Native CampaignSettings() returns a fresh entry each call. A contained exception leaves it as native built it.
        private static void CampaignSettingsPostfix(MarketingAgencyDialog __instance, DialogEntry __result)
        {
            try { LastRefusal = ResearchDialogGate.TryExtend(__result, Read, entry => Extend(entry, __instance)); }
            catch (Exception error)
            {
                LastRefusal = ResearchDialogRefusal.Contract;
                if (!contained) { contained = true; Debug.LogWarning("[Persistent City] Research dialog was contained: " + error); }
            }
        }

        /// <summary>The only change to the native entry.</summary>
        private static void Extend(DialogEntry entry, MarketingAgencyDialog dialog)
        {
            entry.OnSecondOption = () => OnResearch(dialog);
            entry.SecondOptionTextOverride = ResearchText.Button;
        }

        // Each field is read only when every earlier one passed, so a refused screen never scans registrations.
        private static ResearchDialogPreconditions Read(DialogEntry entry)
        {
            var current = desk;
            bool attached = current != null && current.Attached();
            bool contract = attached && installed && ResearchBilling.Installed;
            bool available = contract && current!.Available();
            bool shape = available && entry.Template == DialogEntry.TemplateType.Input
                && entry.InputTemplate == DialogEntry.InputTemplateName.MarketingCampaignSettings
                && entry.OnConfirm != null && entry.OnCancel != null;
            bool unused = shape && entry.OnSecondOption == null && entry.SecondOptionTextOverride == null;
            bool localized = unused && LocalizorManager.IsLocalizedKey(ResearchText.Button);
            if (localized && ResearchDialogContract.ForeignPatch(campaignSettings!) is string foreign)
            {
                contract = false;
                if (Status.Length == 0) { Status = "Research dialog off: " + foreign; Debug.LogWarning("[Persistent City] " + Status); }
            }
            bool eligible = localized && contract && HasEligibleBusiness(current!);
            return new ResearchDialogPreconditions(attached, contract, available, shape, unused, localized, eligible);
        }

        private static bool HasEligibleBusiness(ResearchDesk current)
        {
            var registrations = SaveGameManager.Current?.BuildingRegistrations;
            if (registrations == null) return false;
            foreach (var registration in registrations)
                if (registration != null && current.Eligible(registration)) return true;
            return false;
        }

        // The research button. Null keeps the campaign screen, as native OnMarketingSettingsSet does on an error.
        private static DialogEntry? OnResearch(MarketingAgencyDialog dialog)
        {
            var current = desk;
            var built = menu;
            if (current == null || built == null || !current.Attached()) { Notifications.ShowError(ResearchText.Unavailable); return null; }
            var business = DialogController.current?.GetInputTransformSafe<MarketingCampaignSettings>()?.selectedBusiness;
            return ResearchClick.Run(business != null, business != null && current.Eligible(business),
                key => Notifications.ShowError(key), () => Entry(dialog, built, ResearchMenu.Root, business!));
        }

        private static DialogEntry Entry(MarketingAgencyDialog dialog, ResearchMenu built, int index, BuildingRegistration business)
        {
            var node = built.Nodes[index];
            return new DialogEntry
            {
                messageData = node.MessageKey.Localize(node.Arguments(business.BusinessName)),
                Template = DialogEntry.TemplateType.Text,
                OnConfirm = () => Follow(dialog, built, node, node.Confirm, business),
                ConfirmTextOverride = node.Confirm.TextKey.Localize(),
                OnSecondOption = () => Follow(dialog, built, node, node.Second, business),
                SecondOptionTextOverride = node.Second.TextKey,
                OnCancel = DialogController.current.CancelDialog
            };
        }

        private static DialogEntry? Follow(MarketingAgencyDialog dialog, ResearchMenu built, ResearchMenuNode node, ResearchMenuChoice choice,
            BuildingRegistration business)
        {
            switch (choice.Target)
            {
                case ResearchMenuTarget.Node: return Entry(dialog, built, choice.Node, business);
                // The native screen again, through the patched method, so the preconditions are read afresh.
                case ResearchMenuTarget.CampaignScreen: return (DialogEntry)campaignSettings!.Invoke(dialog, null);
                default: return PlaceOrder(dialog, node.Product, business);
            }
        }

        private static DialogEntry? PlaceOrder(MarketingAgencyDialog dialog, ResearchProduct product, BuildingRegistration business)
        {
            var current = desk;
            var runner = current?.Runner();
            if (current == null || runner == null || !current.Attached() || !current.Available() || !ResearchBilling.Installed)
            { Notifications.ShowError(ResearchText.Unavailable); return null; }
            if (!current.Eligible(business)) { Notifications.ShowError(ResearchText.Ineligible); return null; }
            var contact = DialogController.current.contact;
            var building = BuildingHelper.GetBuilding(contact.Address);
            bool taxDeductible = building != null && building.SpecialService != null && building.SpecialService.hasTaxDeductiblePurchases;
            var refusal = runner.TryOrder(product.Code, NativeFoodMarket.PlaceId(business), AgencyId(contact), taxDeductible,
                current.RadiusMetres(), current.Now(), out var order, out var ledgerRefusal);
            if (refusal != ResearchSurveyRefusal.None)
            {
                Notifications.ShowError(RefusalKey(refusal, ledgerRefusal));
                current.Log("Research order refused: " + refusal + (refusal == ResearchSurveyRefusal.Ledger ? "/" + ledgerRefusal : "") + ".");
                return null;
            }
            current.Log("Research order " + order!.Id + " placed: " + product.Code + ".");
            var arguments = new Dictionary<string, string>(3, StringComparer.Ordinal)
            {
                { ResearchText.ProductArgument, ResearchText.ProductName(product.Code) },
                { ResearchText.BusinessArgument, business.BusinessName },
                { ResearchText.PriceArgument, ResearchText.Price(product) }
            };
            // The native continuation after a campaign change: the agency's "more help" entry.
            var moreHelp = (DialogEntry)moreHelpOffered!.Invoke(dialog, null);
            return new DialogEntry
            {
                messageData = ResearchText.Ordered.Localize(arguments),
                Template = DialogEntry.TemplateType.Text,
                OnVisible = moreHelp.ShowEntry
            };
        }

        internal static string RefusalKey(ResearchSurveyRefusal refusal, ResearchOrderRefusal ledger)
            => refusal != ResearchSurveyRefusal.Ledger ? ResearchText.Unavailable
                : ledger == ResearchOrderRefusal.BusinessHasOpenOrder ? ResearchText.OpenOrder
                : ledger == ResearchOrderRefusal.CityOrderLimit ? ResearchText.OrderLimit
                : ResearchText.Unavailable;

        private static string AgencyId(Contact contact) => "address:" + Uri.EscapeDataString(contact.streetName ?? "") + ":"
            + contact.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":agency";
    }

    /// <summary>The native dialog members research ordering uses, with exact signatures, read by reflection before the
    /// postfix is installed (plan C5 section 3.6).</summary>
    internal static class ResearchDialogContract
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

        internal static string? Check(out MethodInfo? campaignSettings, out MethodInfo? moreHelpOffered)
        {
            campaignSettings = typeof(MarketingAgencyDialog).GetMethod("CampaignSettings", Private, null, Type.EmptyTypes, null);
            moreHelpOffered = typeof(MarketingAgencyDialog).GetMethod("OnMoreHelpOffered", Private, null, Type.EmptyTypes, null);
            if (campaignSettings == null || campaignSettings.ReturnType != typeof(DialogEntry)) return "MarketingAgencyDialog.CampaignSettings() changed.";
            if (moreHelpOffered == null || moreHelpOffered.ReturnType != typeof(DialogEntry)) return "MarketingAgencyDialog.OnMoreHelpOffered() changed.";
            string? field = Field(typeof(DialogEntry), "OnConfirm", typeof(Func<DialogEntry>)) ?? Field(typeof(DialogEntry), "OnSecondOption", typeof(Func<DialogEntry>))
                ?? Field(typeof(DialogEntry), "OnCancel", typeof(Action)) ?? Field(typeof(DialogEntry), "OnVisible", typeof(Action))
                ?? Field(typeof(DialogEntry), "SecondOptionTextOverride", typeof(string))
                ?? Field(typeof(DialogEntry), "ConfirmTextOverride", typeof(LanguageChangeEventDataHolder))
                ?? Field(typeof(DialogEntry), "messageData", typeof(LanguageChangeEventDataHolder))
                ?? Field(typeof(DialogEntry), "Template", typeof(DialogEntry.TemplateType))
                ?? Field(typeof(DialogEntry), "InputTemplate", typeof(DialogEntry.InputTemplateName))
                ?? Field(typeof(MarketingCampaignSettings), "selectedBusiness", typeof(BuildingRegistration))
                ?? Field(typeof(DialogController), "contact", typeof(Contact))
                ?? Field(typeof(Contact), "streetName", typeof(string)) ?? Field(typeof(Contact), "streetNumber", typeof(int))
                ?? Field(typeof(Building), "SpecialService", typeof(SpecialService))
                ?? Field(typeof(SpecialService), "hasTaxDeductiblePurchases", typeof(bool))
                ?? Field(typeof(BuildingRegistration), "BusinessName", typeof(string)) ?? Field(typeof(BuildingRegistration), "RentedByPlayer", typeof(bool));
            if (field != null) return field;
            var current = typeof(DialogController).GetField("current", Static);
            if (current == null || current.FieldType != typeof(DialogController)) return "DialogController.current changed.";
            // Enum values are compiled in; a reordered enum keeps the number but not the name.
            if (Enum.GetName(typeof(DialogEntry.InputTemplateName), DialogEntry.InputTemplateName.MarketingCampaignSettings) != "MarketingCampaignSettings"
                || Enum.GetName(typeof(DialogEntry.TemplateType), DialogEntry.TemplateType.Input) != "Input"
                || Enum.GetName(typeof(DialogEntry.TemplateType), DialogEntry.TemplateType.Text) != "Text")
                return "DialogEntry template values changed.";
            string? method = Method(typeof(DialogController), "CancelDialog", Instance) ?? Method(typeof(DialogController), "SecondOptionCurrentEntry", Instance)
                ?? Method(typeof(DialogEntry), "ShowEntry", Instance);
            if (method != null) return method;
            var safe = typeof(DialogController).GetMethod("GetInputTransformSafe", Instance, null, Type.EmptyTypes, null);
            if (safe == null || !safe.IsGenericMethodDefinition) return "DialogController.GetInputTransformSafe<T>() changed.";
            var address = typeof(Contact).GetProperty("Address", Instance);
            if (address == null || address.PropertyType != typeof(Address)) return "Contact.Address changed.";
            var building = typeof(BuildingHelper).GetMethod("GetBuilding", Static, null, new[] { typeof(Address) }, null);
            if (building == null || building.ReturnType != typeof(Building)) return "BuildingHelper.GetBuilding(Address) changed.";
            var error = typeof(Notifications).GetMethod("ShowError", Static, null, new[] { typeof(string), typeof(string), typeof(bool) }, null);
            if (error == null) return "Notifications.ShowError(string, string, bool) changed.";
            var localize = typeof(LocalizorManager).GetMethod("Localize", Static, null, new[] { typeof(string), typeof(object) }, null);
            if (localize == null || localize.ReturnType != typeof(LanguageChangeEventDataHolder)) return "LocalizorManager.Localize changed.";
            if (typeof(LocalizorManager).GetMethod("IsLocalizedKey", Static, null, new[] { typeof(string) }, null) == null) return "LocalizorManager.IsLocalizedKey changed.";
            return ForeignPatch(campaignSettings);
        }

        /// <summary>A prefix, transpiler or finalizer of another owner can skip or replace the native entry.</summary>
        internal static string? ForeignPatch(MethodBase method)
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return null;
            foreach (var group in new[] { info.Prefixes, info.Transpilers, info.Finalizers })
                foreach (var patch in group)
                    if (patch.owner != ResearchAgencyDialog.PatchId) return "foreign Harmony patch on MarketingAgencyDialog.CampaignSettings by " + patch.owner + ".";
            return null;
        }

        private static string? Field(Type type, string name, Type fieldType)
        {
            var field = type.GetField(name, Instance);
            return field != null && field.FieldType == fieldType ? null : type.Name + "." + name + " changed.";
        }

        private static string? Method(Type type, string name, BindingFlags flags)
            => type.GetMethod(name, flags, null, Type.EmptyTypes, null) != null ? null : type.Name + "." + name + "() changed.";
    }
}
