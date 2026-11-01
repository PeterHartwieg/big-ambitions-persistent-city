using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        // Self-contained predecessor step. Integration may repoint this exact predecessor only;
        // no renumbering or acceptance of reserved/foreign schemas.
        private static void MigrateCampaigns19To22(CityState state)
        {
            Require(state.Schema == 19, "Campaign migration requires exact schema 19.");
            if (state.Food != null)
            {
                Require(state.Food.Campaigns.Count == 0, "Schema 19 cannot contain campaign evidence.");
                foreach (var meal in state.Food.Residents)
                    if (meal.Decision != null) ValidateAwareness(meal.Decision, meal, state.Food);
            }
            // Full predecessor semantics execute before changing the in-memory schema.
            Validate(state, 19);
            state.Schema = 22;
        }
        private static int ValidateCampaigns22(FoodCommerceState food)
        {
            Require(food.Campaigns != null && food.Campaigns.Count <= 1024, "Oversized/missing campaign images.");
            var references = new HashSet<int>();
            foreach (var reference in food.BusinessReferences) references.Add(reference.Slot);
            int previous = -1;
            foreach (var image in food.Campaigns!)
            {
                Require(image != null && image.BusinessRef > previous && references.Contains(image.BusinessRef), "Unordered/foreign campaign generation.");
                previous = image.BusinessRef;
                // Immutable images validate their bounded masks/header at construction and on decode.
            }
            return food.Campaigns.Count;
        }
        private static int ValidateAwareness22(FoodDecisionState decision, ResidentMeal meal, FoodCommerceState food)
        {
            var image = decision.Awareness;
            if(image!=null) Require(image.Union!=null,"Missing awareness union.");
            if (image == null || image.Union.Count <= 24 && image.Union.TrueForAll(c => c != null && (c.Sources & FoodAwarenessSource.Campaign) == 0))
                return ValidateAwareness(decision, meal, food);
            Require(image.CatalogueRevision != null && image.CatalogueRevision.Length == 44, "Invalid awareness catalogue revision.");
            byte[] hash = Convert.FromBase64String(image.CatalogueRevision);
            Require(hash.Length == 32 && Convert.ToBase64String(hash) == image.CatalogueRevision, "Noncanonical catalogue revision.");
            Require(image.Union.Count <= 32, "Oversized awareness union.");
            string previous = ""; var counts = new int[6]; var admitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in image.Union)
            {
                Require(candidate != null, "Null awareness candidate."); Text(candidate.BusinessId, "Awareness generation", 512);
                Require(StringComparer.Ordinal.Compare(previous, candidate.BusinessId) < 0, "Duplicate/unordered awareness generation.");
                previous = candidate.BusinessId; int flags = (int)candidate.Sources;
                Require(flags > 0 && flags < 64, "Unknown awareness source.");
                for (int source = 0; source < 6; source++) if ((flags & (1 << source)) != 0)
                    Require(++counts[source] <= (source >= 4 ? 8 : 4), "Oversized awareness source.");
                if (candidate.Eligible) admitted.Add(candidate.BusinessId);
            }
            foreach (var shop in decision.Shops) Require(admitted.Contains(shop.BusinessId), "Shortlist bypassed awareness admission.");
            return image.Union.Count;
        }
        private sealed class CampaignImageConverter22 : JsonConverter<FoodCampaignImage>
        {
            public override void WriteJson(JsonWriter writer, FoodCampaignImage? value, JsonSerializer serializer)
            {
                if (value == null) throw new SaveValidationException("Null campaign image.");
                writer.WriteValue(value.Encode());
            }
            public override FoodCampaignImage ReadJson(JsonReader reader, Type type, FoodCampaignImage? existing, bool hasExisting, JsonSerializer serializer)
            {
                Require(reader.TokenType == JsonToken.String, "Expected compact campaign image.");
                return FoodCampaignImage.Decode((string)reader.Value!);
            }
        }
    }
}
