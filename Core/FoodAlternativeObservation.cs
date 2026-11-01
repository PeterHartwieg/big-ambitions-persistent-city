using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    // Observation contracts only. These are not persisted candidates or sale authority.
    public enum FoodAlternativeKind { Player, Rival }
    public enum FoodAvailabilityEvidenceKind { Unknown, ObservedPlayerSupply, EstimatedFromPriceAndHours }
    public enum FoodStockEvidenceKind { Unknown }
    public enum FoodCatalogueRefusal { Missing, AmbiguousIdentity, InvalidPrice, InvalidHours, InvalidRating, UnknownOwner }

    public readonly struct FoodAdvertisedOffer
    {
        public string ProductId { get; }
        public Money Price { get; }
        public FoodStockEvidenceKind Stock => FoodStockEvidenceKind.Unknown;
        public FoodAdvertisedOffer(string productId, Money price)
        {
            if (string.IsNullOrWhiteSpace(productId) || !FoodProducts.Known(productId) || price <= Money.Zero
                || price > Money.FromCents(Money.MaximumNativePriceCents)) throw new ArgumentException("Invalid advertised food offer.");
            ProductId = productId; Price = price;
        }
    }

    public readonly struct FoodAdvertisedHours
    {
        public int Day { get; }
        public int FromMinute { get; }
        public int ToMinute { get; }
        public FoodAdvertisedHours(int day, int fromMinute, int toMinute)
        {
            if (day < 0 || day > 6 || fromMinute < 0 || fromMinute >= toMinute || toMinute > 1440)
                throw new ArgumentException("Invalid advertised hours.");
            Day = day; FromMinute = fromMinute; ToMinute = toMinute;
        }
    }

    /// <summary>Detached advertised facts. Unknown stock for both kinds; player supply must be
    /// observed separately. The rival estimate label authorizes no estimate or service capacity here.</summary>
    public sealed class FoodAlternativeObservation
    {
        public string Id { get; }
        public string PlaceId { get; }
        public string StreetName { get; }
        public int StreetNumber { get; }
        public int CreationDay { get; }
        public string BusinessType { get; }
        public string OwnerId { get; }
        public FoodAlternativeKind Kind { get; }
        public FoodAvailabilityEvidenceKind AvailabilityEvidence => Kind == FoodAlternativeKind.Rival
            ? FoodAvailabilityEvidenceKind.EstimatedFromPriceAndHours : FoodAvailabilityEvidenceKind.Unknown;
        public FoodStockEvidenceKind Stock => FoodStockEvidenceKind.Unknown;
        public bool Accessible { get; }
        public bool HasReadableDoor { get; }
        public bool TemporarilyClosed { get; }
        public int? CustomerServicePercent { get; }
        public int UnsupportedProductCount { get; }
        public IReadOnlyList<FoodAdvertisedOffer> Offers { get; }
        // Advertised schedule is preserved even while temporarily closed.
        public IReadOnlyList<FoodAdvertisedHours> Hours { get; }
        public FoodAlternativeObservation(string id, string placeId, string streetName, int streetNumber,
            int creationDay, string businessType, string ownerId, FoodAlternativeKind kind, bool accessible,
            bool hasReadableDoor, bool temporarilyClosed, int? customerServicePercent, int unsupportedProductCount,
            IEnumerable<FoodAdvertisedOffer> offers, IEnumerable<FoodAdvertisedHours> hours)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(placeId) || string.IsNullOrWhiteSpace(streetName)
                || string.IsNullOrWhiteSpace(businessType) || string.IsNullOrWhiteSpace(ownerId) || creationDay < 0
                || (kind != FoodAlternativeKind.Player && kind != FoodAlternativeKind.Rival)
                || customerServicePercent < 0 || customerServicePercent > 100 || unsupportedProductCount < 0)
                throw new ArgumentException("Invalid advertised business facts.");
            var copy = offers.OrderBy(o => o.ProductId, StringComparer.Ordinal).ToArray();
            if (copy.Any(o => string.IsNullOrWhiteSpace(o.ProductId) || !FoodProducts.Known(o.ProductId) || o.Price <= Money.Zero
                || o.Price > Money.FromCents(Money.MaximumNativePriceCents))
                || copy.Select(o => o.ProductId).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Ambiguous advertised products.");
            Id = id; PlaceId = placeId; StreetName = streetName; StreetNumber = streetNumber;
            CreationDay = creationDay; BusinessType = businessType; OwnerId = ownerId; Kind = kind;
            Accessible = accessible; HasReadableDoor = hasReadableDoor; TemporarilyClosed = temporarilyClosed;
            CustomerServicePercent = customerServicePercent; UnsupportedProductCount = unsupportedProductCount;
            Offers = Array.AsReadOnly(copy);
            var hourCopy = hours.ToArray();
            if (hourCopy.Any(h => h.Day < 0 || h.Day > 6 || h.FromMinute < 0 || h.FromMinute >= h.ToMinute || h.ToMinute > 1440))
                throw new ArgumentException("Invalid advertised hours.");
            Hours = Array.AsReadOnly(hourCopy.OrderBy(h => h.Day).ThenBy(h => h.FromMinute).ThenBy(h => h.ToMinute).ToArray());
        }
    }
}
