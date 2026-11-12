using System;
using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    internal static class FoodReceiptEvidence
    {
        internal static bool IsConfirmedPaid(bool currentSave, IDictionary<string, string>? receipts, ResidentMeal meal)
            => meal.TargetKind == FoodAlternativeKind.Player && currentSave && receipts != null
                && FoodSaleReceipts.ReadStatus(receipts, meal) == FoodReceiptStatus.Paid;

        internal static bool TryConfirmedTicket(bool currentSave, IDictionary<string, string>? receipts, ResidentMeal meal,
            out Money total, out IReadOnlyList<FoodTicketLine> extras)
        {
            total = Money.Zero; extras = Array.Empty<FoodTicketLine>();
            return meal.TargetKind == FoodAlternativeKind.Player && currentSave && receipts != null
                && FoodSaleReceipts.TryReadPaidTicket(receipts, meal, out total, out extras);
        }
    }
}
