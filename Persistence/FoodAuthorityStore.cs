using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>Small early-load authority manifest. No identities, wallets or order contents.</summary>
    public static class FoodAuthorityStore
    {
        public const string Key = "persistentcity:food-authority-v1";
        private static readonly string[] Fields = { "place", "signature", "generation", "desired", "requested", "from", "until", "suspended" };
        public static List<FoodAuthority> Read(string? source)
        {
            if (source == null) return new List<FoodAuthority>();
            if (source.Length > 8 * 1024 * 1024) throw new InvalidDataException("Food authority manifest exceeds its limit.");
            using var reader = new JsonTextReader(new StringReader(source)) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read() || root.Properties().Count() != 2 || root["version"]?.Type != JTokenType.Integer
                || (long)root["version"]! != 1 || !(root["shops"] is JArray shops) || shops.Count > SaveCodec.MaxPlaces)
                throw new InvalidDataException("Unsupported food authority manifest; original payload retained.");
            var result = new List<FoodAuthority>();
            foreach (var token in shops)
            {
                if (!(token is JObject row) || row.Properties().Count() != Fields.Length || Fields.Any(f => row[f] == null)
                    || row["place"]!.Type != JTokenType.String || row["signature"]!.Type != JTokenType.String
                    || row["desired"]!.Type != JTokenType.Boolean
                    || row["suspended"]!.Type != JTokenType.Boolean
                    || new[] { "generation", "requested", "from", "until" }.Any(f => row[f]!.Type != JTokenType.Integer))
                    throw new InvalidDataException("Invalid food authority record; original payload retained.");
                result.Add(new FoodAuthority { PlaceId = (string)row["place"]!, Signature = (string)row["signature"]!,
                    Generation = checked((int)(long)row["generation"]!), Desired = (bool)row["desired"]!,
                    RequestedHour = (long)row["requested"]!, FromHour = (long)row["from"]!, UntilHour = (long)row["until"]!,
                    Suspended = (bool)row["suspended"]! });
            }
            Validate(result); return result;
        }
        public static string Write(IReadOnlyCollection<FoodAuthority> records)
        {
            Validate(records);
            var rows = new JArray(records.OrderBy(r => r.PlaceId, StringComparer.Ordinal).Select(r => new JObject {
                ["place"] = r.PlaceId, ["signature"] = r.Signature, ["generation"] = r.Generation,
                ["desired"] = r.Desired, ["requested"] = r.RequestedHour, ["from"] = r.FromHour, ["until"] = r.UntilHour,
                ["suspended"] = r.Suspended }));
            return new JObject { ["version"] = 1, ["shops"] = rows }.ToString(Formatting.None);
        }
        private static void Validate(IReadOnlyCollection<FoodAuthority> records)
        {
            if (records.Count > SaveCodec.MaxPlaces || records.Any(r => r == null || string.IsNullOrWhiteSpace(r.PlaceId)
                || r.PlaceId.Length > 2048 || !r.PlaceId.StartsWith("address:", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(r.Signature) || r.Signature.Length > 4096 || !r.Signature.StartsWith("business:", StringComparison.Ordinal)
                || r.Generation <= 0 || r.RequestedHour < 0 || r.RequestedHour > 20000000 || r.FromHour < -1
                || r.FromHour > 20000000 || r.UntilHour < -1 || r.UntilHour > 20000000
                || r.Suspended && !r.Managed
                || r.UntilHour >= 0 && (r.FromHour < 0 || r.UntilHour <= r.FromHour))
                || records.Select(r => r.PlaceId).Distinct(StringComparer.Ordinal).Count() != records.Count)
                throw new InvalidDataException("Invalid food authority manifest; original payload retained.");
        }
    }
}
