using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    // Shared E5 DTOs: exact ordinal names, exact money, detached evidence only.
    // No meal class, wire code, saved state, staffing or sale permission is implied.
    public sealed class FoodStockOfferImage
    {
        internal FoodStockOfferImage(string productId, Money price, int rawUnits)
        { ProductId = productId; Price = price; RawUnits = rawUnits; }
        public string ProductId { get; }
        public Money Price { get; }
        public int RawUnits { get; }
    }

    public sealed class FoodHolderResourceImage
    {
        internal FoodHolderResourceImage(string holderId, string definitionId, string resourceId, bool bag)
        { HolderId = holderId; DefinitionId = definitionId; ResourceId = resourceId; IsBagCounter = bag; }
        public string HolderId { get; }
        public string DefinitionId { get; }
        public string ResourceId { get; }
        public bool IsBagCounter { get; }
    }

    public enum FoodEligibilityRefusal
    {
        None, CatalogueUnavailable, CatalogueBounds, CatalogueIdentity, CatalogueChanged,
        BusinessContract, MenuBounds, MenuIdentity, Price, MissingDefinition, RetailType,
        Seasonal, Metadata, FurnitureBounds, FurnitureIdentity, CargoBounds, SharedCargo,
        Workstation, CounterProductSharing, Whitelist, Resources, CargoShape, ConvertedInput,
        Outputs, Overflow, NoHolder, NoCounter, Dirty, NativeRead
    }

    public sealed class FoodEligibilityImage
    {
        internal FoodEligibilityImage(long catalogueRevision, long shopRevision, FoodEligibilityRefusal refusal,
            FoodStockOfferImage[]? offers = null, FoodHolderResourceImage[]? holders = null, int bags = 0, int counters = 0)
        {
            CatalogueRevision = catalogueRevision; ShopRevision = shopRevision; Refusal = refusal;
            Offers = Array.AsReadOnly(offers ?? Array.Empty<FoodStockOfferImage>());
            Holders = Array.AsReadOnly(holders ?? Array.Empty<FoodHolderResourceImage>());
            RawBagUnits = bags; RawCounters = counters;
        }
        public long CatalogueRevision { get; }
        public long ShopRevision { get; }
        public FoodEligibilityRefusal Refusal { get; }
        public bool StockContractMatched => Refusal == FoodEligibilityRefusal.None;
        public IReadOnlyList<FoodStockOfferImage> Offers { get; }
        public IReadOnlyList<FoodHolderResourceImage> Holders { get; }
        public int RawBagUnits { get; }
        public int RawCounters { get; }
        public string Eligibility => "Unknown: meal admission, native staffing/requirements, tag-cache agreement and checkout not evaluated";
    }
}
