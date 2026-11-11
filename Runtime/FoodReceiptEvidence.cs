using System;
using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    /// <summary>The paid-receipt evidence NativeFoodMarket gives Core (IFoodSaleEvidence, IFoodTicketEvidence). Unity-free, so
    /// the unit tests execute the same reader production uses. It does not depend on the checkout contract: in checkout-off
    /// mode (F8c) a paid receipt still settles its obligation without a checkout, and a pending one stays uncertain.</summary>
    internal static class FoodReceiptEvidence
    {
        /// <param name="currentSave">The market's save is the loaded one; evidence from another save never counts.</param>
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
