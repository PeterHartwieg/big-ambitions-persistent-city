using System;
using System.Globalization;
using System.Text;
using Buildings;

namespace PersistentCity.Runtime
{
    /// <summary>Explicit main-thread bridge read only. No hook, retained city, scan or allocation
    /// until Capture is called. Not a report, quote, stable identity or proof of payment.</summary>
    public static class AgencyBindingDiagnostic
    {
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static void Address(StringBuilder b, string? street, int number)
            => b.Append(Uri.EscapeDataString(street ?? "")).Append(':').Append(number.ToString(CultureInfo.InvariantCulture));

        public static string Capture()
        {
            var save = SaveGameManager.Current;
            // Instance can search/register a singleton; GetSingleton is a verified dictionary read.
            var city = Extensions.SingletonLifecycle.GetSingleton<CityManager>();
            if (save == null || city == null || city.cityBuildingControllers == null) return "C3 unavailable: no loaded city";
            var b = new StringBuilder(2048);
            b.Append("C3 read-only; tuple/queue ordinal is NOT durable identity\nday=").Append(save.Day.ToString(CultureInfo.InvariantCulture))
                .Append(" money(binary32 dollars)=").Append(Number(save.Money)).Append('\n');
            var dialog = DialogController.current;
            if (dialog != null && dialog.contact != null)
            {
                b.Append("dialog=").Append(dialog.dialogType).Append(" provider=");
                Address(b, dialog.contact.streetName, dialog.contact.streetNumber);
                b.Append(" contact=").Append(Uri.EscapeDataString(dialog.contact.id ?? "")).Append('\n');
            }
            foreach (var controller in city.cityBuildingControllers)
            {
                if (controller == null || controller.building == null) continue;
                var service = controller.building.SpecialService;
                if (service == null || !(service.settings is MarketingAgencySettings agency)) continue;
                b.Append("agency=");
                // Building.Address lazily writes its native cache; use raw fields instead.
                Address(b, controller.building.StreetName, controller.building.StreetNumber);
                b.Append(" businessType=").Append(service.businessTypeName).Append(" dialogType=").Append(service.dialogType)
                    .Append(" options=");
                if (agency.marketingTypesAvailable != null)
                    foreach (var option in agency.marketingTypesAvailable) b.Append(option).Append(';');
                b.Append('\n');
            }
            // A fixed-size ring retains the most recent sixteen marketing records, not the whole queue.
            // Strings are built only for retained records, avoiding allocations for discarded entries.
            var recent = new Transaction?[16];
            int count = 0;
            if (save.Transactions != null)
                foreach (var transaction in save.Transactions)
                    if (transaction != null && (transaction.transactionType == "ba:transaction_marketing"
                        || (transaction.transactionCategories != null && transaction.transactionCategories.Contains("ba:transactioncategory_marketing"))))
                        recent[count++ % recent.Length] = transaction;
            b.Append("marketingRecordsInQueue=").Append(count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            for (int i = Math.Max(0, count - recent.Length); i < count; i++)
            {
                var t = recent[i % recent.Length]!;
                b.Append("marketing ordinal=").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" type=").Append(t.transactionType)
                    .Append(" categories=");
                if (t.transactionCategories != null) foreach (var category in t.transactionCategories) b.Append(category).Append(';');
                b.Append(" amount=").Append(Number(t.amount)).Append(" balance(floored)=").Append(Number(t.balance));
                if (t.timestamp != null) b.Append(" day=").Append(t.timestamp.Day.ToString(CultureInfo.InvariantCulture))
                    .Append(" hour=").Append(t.timestamp.Hour.ToString(CultureInfo.InvariantCulture)).Append(" minute=").Append(Number(t.timestamp.Minute));
                b.Append(" address=");
                if (t.address != null) Address(b, t.address.streetName, t.address.streetNumber);
                b.Append(" data=");
                if (t.transactionData != null) foreach (var pair in t.transactionData)
                    b.Append(Uri.EscapeDataString(pair.Key)).Append('=').Append(Uri.EscapeDataString(pair.Value ?? "")).Append(';');
                b.Append('\n');
            }
            if (save.financialSummaries != null)
                foreach (var summary in save.financialSummaries)
                {
                    if (summary == null || summary.dayNumber < save.Day - 14 || summary.businessIncomeStatements == null) continue;
                    foreach (var statement in summary.businessIncomeStatements)
                    {
                        if (statement == null) continue;
                        b.Append("expense day=").Append(summary.dayNumber.ToString(CultureInfo.InvariantCulture)).Append(" address=");
                        if (statement.Address != null) Address(b, statement.Address.streetName, statement.Address.streetNumber);
                        b.Append(" MarketingExpenses=").Append(Number(statement.MarketingExpenses)).Append('\n');
                    }
                }
            return b.ToString();
        }
    }
}
