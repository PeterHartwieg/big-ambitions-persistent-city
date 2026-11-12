using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        public const int MaxVendorLive = VendorTable.MaximumLive;
        public const int MaxVendorLiveAndRetired = VendorTable.MaximumLiveAndRetired;
        public const int MaxVendorTombstones = VendorTable.MaximumTombstones;
        public const int MaxVendorRows = MaxVendorLiveAndRetired + MaxVendorTombstones;
        private const int VendorIndexWidth = 2;
        private const char VendorPlaceForm = 'v';
        private const int VendorVersion30 = 1;
        private const long MaxVendorHour = (long)(MaxMinute / 60);
        private const int MaxVendorDay = (int)(MaxMinute / 1440);

        private sealed class PlaceCells
        {
            public static readonly PlaceCells Streaming = new PlaceCells(null, null);
            public static PlaceCells Of(WireContext context) => new PlaceCells(context.Places, context.VendorPlaces);
            private readonly IReadOnlyList<string>? places, vendors;
            private PlaceCells(IReadOnlyList<string>? places, IReadOnlyList<string>? vendors)
            { this.places = places; this.vendors = vendors; }

            public string Parse(string text, int offset, string error)
            {
                Require(text.Length >= offset + 3, error);
                int index = -1;
                if (text[offset] == VendorPlaceForm)
                {
                    try { index = MemoryWire.Number(text, offset + 1, VendorIndexWidth); } catch (FormatException) { Fail(error); }
                    Require(index < (vendors?.Count ?? MaxVendorRows), error);
                    return places != null ? vendors![index] : "#";
                }
                try { index = MemoryWire.Number(text, offset, 3); } catch (FormatException) { Fail(error); }
                Require(index < (places?.Count ?? MaxPlaces), error);
                if (places == null) return "#";
                Require(!VendorNames.IsVendorPlace(places[index]), error);
                return places[index];
            }
        }

        private static string? PlaceCell30(WireContext context, string id)
            => context.VendorIndex.TryGetValue(id, out int row) ? VendorPlaceForm + MemoryWire.Number(row, VendorIndexWidth)
            : context.Index.TryGetValue(id, out int at) ? MemoryWire.Number(at, 3) : null;

        private static int IdentityBound(string? value, int max) => VendorNames.Bound(value, max);


        private sealed class VendorConverter30 : JsonConverter<VendorTable>
        {
            private readonly WireContext context;
            public VendorConverter30(WireContext context) { this.context = context; }
            public override void WriteJson(JsonWriter writer, VendorTable? value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                writer.WriteValue(VendorVersion30);
                writer.WriteStartArray();
                foreach (var row in value!.Rows)
                {
                    writer.WriteStartArray();
                    writer.WriteValue((int)row.State); writer.WriteValue(row.VehicleId);
                    if (row.State == VendorRowState.Tombstone)
                    {
                        writer.WriteValue(row.Generation); writer.WriteValue(row.PlaceId); writer.WriteValue(row.BusinessId);
                    }
                    else
                    {
                        writer.WriteValue(row.Kind); writer.WriteValue(row.Generation); writer.WriteValue(row.PlaceId);
                        WriteStand(writer, row.Stand); WriteStand(writer, row.DeferredStand);
                        var a = row.Authority;
                        writer.WriteStartArray();
                        writer.WriteValue(a.Desired); writer.WriteValue(a.RequestedHour); writer.WriteValue(a.FromHour); writer.WriteValue(a.UntilHour);
                        writer.WriteValue(a.Suspended); writer.WriteValue(a.OpeningId); writer.WriteValue(a.HandbackPending);
                        writer.WriteEndArray();
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
                writer.WriteEndArray();
            }

            private static void WriteStand(JsonWriter writer, VendorStand? stand)
            {
                if (stand == null) { writer.WriteNull(); return; }
                writer.WriteStartArray();
                writer.WriteValue(stand.OpeningId);
                writer.WriteValue(stand.Position.X); writer.WriteValue(stand.Position.Y); writer.WriteValue(stand.Position.Z);
                writer.WriteValue(stand.OpenedDay); writer.WriteValue(stand.OpenedMinute); writer.WriteValue(stand.ClosesDay); writer.WriteValue(stand.ClosesMinute);
                writer.WriteValue(stand.Published);
                writer.WriteEndArray();
            }

            public override VendorTable ReadJson(JsonReader reader, Type objectType, VendorTable? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var decoded = context.TakeVendors();
                if (decoded != null) { reader.Skip(); return decoded; }
                int records = 0;
                return ReadVendors30(reader, null, ref records);
            }
        }

        private static void CheckVendors30(JsonReader reader, TokenText text, ref int records) => ReadVendors30(reader, text, ref records);

        private static VendorTable ReadVendors30(JsonReader reader, TokenText? text, ref int records)
        {
            Require(reader.TokenType == JsonToken.StartArray, "Expected the vendor table.");
            Require(reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, VendorVersion30, VendorVersion30)
                && (text == null || text.CanonicalInteger((IJsonLineInfo)reader)), "Invalid vendor table version.");
            if (!reader.Read() || reader.TokenType != JsonToken.StartArray) Fail("Expected the vendor rows.");
            var rows = new List<VendorRowImage>();
            int live = 0, retired = 0, tombstones = 0;
            string? previous = null;
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Require(reader.TokenType == JsonToken.StartArray, "Expected a vendor row.");
                var state = (VendorRowState)Long30(reader, text, 0, (int)VendorRowState.Tombstone, "Unknown vendor row state.");
                if (state == VendorRowState.Live && ++live > MaxVendorLive) Fail("Live vendor rows exceed the bound of 64.");
                if (state == VendorRowState.Retired) retired++;
                if (state != VendorRowState.Tombstone && live + retired > MaxVendorLiveAndRetired) Fail("Live and retired vendor rows exceed the bound of 128.");
                if (state == VendorRowState.Tombstone && ++tombstones > MaxVendorTombstones) Fail("Vendor tombstones exceed the bound of 1,024.");
                Charge(ref records);
                var row = new VendorRowImage { State = state, VehicleId = String30(reader, text, VendorNames.MaximumIdLength, "Invalid vendor vehicle id.") };
                Require(VendorNames.ValidId(row.VehicleId), "Invalid vendor vehicle id.");
                Require(previous == null || StringComparer.Ordinal.Compare(previous, row.VehicleId) < 0, "Duplicate or unordered vendor vehicle id.");
                previous = row.VehicleId;
                if (state == VendorRowState.Tombstone)
                {
                    row.Generation = (int)Long30(reader, text, 1, int.MaxValue, "Invalid vendor generation.");
                    Require(String30(reader, text, VendorNames.MaximumPlaceIdLength, "Invalid vendor place.") == VendorNames.PlaceId(row.VehicleId), "Vendor place differs from its vehicle id.");
                    Require(String30(reader, text, VendorNames.MaximumBusinessIdLength, "Invalid vendor business.") == VendorNames.BusinessId(row.VehicleId, row.Generation), "Vendor business differs from its vehicle id and generation.");
                }
                else
                {
                    row.Kind = String30(reader, text, 16, "Invalid vendor kind.");
                    row.Generation = (int)Long30(reader, text, 1, int.MaxValue, "Invalid vendor generation.");
                    Require(String30(reader, text, VendorNames.MaximumPlaceIdLength, "Invalid vendor place.") == VendorNames.PlaceId(row.VehicleId), "Vendor place differs from its vehicle id.");
                    row.Stand = Stand30(reader, text);
                    row.DeferredStand = Stand30(reader, text);
                    Require(reader.Read() && reader.TokenType == JsonToken.StartArray, "Expected a vendor authority.");
                    var a = row.Authority;
                    a.Desired = Bool30(reader, "Invalid vendor desired flag.");
                    a.RequestedHour = Long30(reader, text, -1, MaxVendorHour, "Invalid vendor request hour.");
                    a.FromHour = Long30(reader, text, -1, MaxVendorHour, "Invalid vendor from hour.");
                    a.UntilHour = Long30(reader, text, -1, MaxVendorHour, "Invalid vendor until hour.");
                    a.Suspended = Bool30(reader, "Invalid vendor suspended flag.");
                    a.OpeningId = Long30(reader, text, -1, long.MaxValue, "Invalid vendor opening id.");
                    a.HandbackPending = Bool30(reader, "Invalid vendor handback flag.");
                    Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus vendor authority slot.");
                }
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus vendor row slot.");
                rows.Add(row);
            }
            if (reader.TokenType != JsonToken.EndArray) Fail("Incomplete vendor rows.");
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus vendor table slot.");
            var table = RestoreVendors30(rows);
            ValidateVendorTable30(table);
            return table;
        }

        private static VendorStand? Stand30(JsonReader reader, TokenText? text)
        {
            Require(reader.Read() && (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.StartArray), "Invalid vendor stand.");
            if (reader.TokenType == JsonToken.Null) return null;
            var stand = new VendorStand { OpeningId = Long30(reader, text, 1, long.MaxValue, "Invalid vendor stand opening id.") };
            double x = Coordinate30(reader, text), y = Coordinate30(reader, text), z = Coordinate30(reader, text);
            stand.Position = new Position(x, y, z);
            stand.OpenedDay = (int)Long30(reader, text, 0, MaxVendorDay, "Invalid vendor stand opening day.");
            stand.OpenedMinute = (int)Long30(reader, text, 0, 1439, "Invalid vendor stand opening minute.");
            stand.ClosesDay = (int)Long30(reader, text, -1, MaxVendorDay, "Invalid vendor stand closing day.");
            stand.ClosesMinute = (int)Long30(reader, text, -1, 1439, "Invalid vendor stand closing minute.");
            stand.Published = Bool30(reader, "Invalid vendor stand published flag.");
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus vendor stand slot.");
            return stand;
        }

        private static long Long30(JsonReader reader, TokenText? text, long min, long max, string error)
        {
            Require(reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, min, max)
                && (text == null || text.CanonicalInteger((IJsonLineInfo)reader)), error);
            return (long)reader.Value!;
        }

        private static bool Bool30(JsonReader reader, string error)
        {
            Require(reader.Read() && reader.TokenType == JsonToken.Boolean, error);
            return (bool)reader.Value!;
        }

        private static double Coordinate30(JsonReader reader, TokenText? text)
        {
            Require(reader.Read() && (reader.TokenType == JsonToken.Float && reader.Value is double
                || reader.TokenType == JsonToken.Integer && reader.Value is long && (text == null || text.CanonicalInteger((IJsonLineInfo)reader))), "Invalid vendor stand position.");
            double value = reader.Value is double d ? d : (long)reader.Value!;
            Bounded(value, -MaxCoordinate, MaxCoordinate, "vendor stand position");
            return value;
        }

        private static string String30(JsonReader reader, TokenText? text, int max, string error)
        {
            Require(reader.Read() && reader.TokenType == JsonToken.String, error);
            string value = (string)reader.Value!;
            Require(value.Length <= max && (text == null || ExactString25(text, (IJsonLineInfo)reader, value)), error);
            return value;
        }


        private sealed class VendorRowImage
        {
            public VendorRowState State;
            public string VehicleId = "", Kind = "";
            public int Generation = 1;
            public VendorStand? Stand, DeferredStand;
            public VendorAuthority Authority = new VendorAuthority();
        }

        private static VendorTable RestoreVendors30(List<VendorRowImage> images)
        {
            var table = new VendorTable();
            foreach (var order in new[] { VendorRowState.Tombstone, VendorRowState.Retired, VendorRowState.Live })
                foreach (var image in images)
                {
                    if (image.State != order) continue;
                    var row = table.Admit(image.VehicleId, image.Kind);
                    row.Generation = image.Generation;
                    row.Stand = image.Stand; row.DeferredStand = image.DeferredStand; row.Authority = image.Authority;
                    row.PlacePublished = image.State != VendorRowState.Tombstone && image.Stand != null;
                    table.Move(row, image.State);
                }
            table.CheckInvariant();
            return table;
        }

        internal static VendorTable? CopyVendors30(VendorTable? live)
        {
            if (live == null) return null;
            var images = new List<VendorRowImage>(live.Rows.Count);
            foreach (var row in live.Rows)
                images.Add(new VendorRowImage
                {
                    State = row.State, VehicleId = row.VehicleId, Kind = row.Kind, Generation = row.Generation,
                    Stand = Stand(row.Stand), DeferredStand = Stand(row.DeferredStand), Authority = Authority(row.Authority),
                });
            var copy = RestoreVendors30(images);
            for (int i = 0; i < live.Rows.Count; i++) copy.Rows[i].PlacePublished = live.Rows[i].PlacePublished;
            return copy;

            static VendorStand? Stand(VendorStand? s) => s == null ? null : new VendorStand
            {
                OpeningId = s.OpeningId, Position = s.Position, OpenedDay = s.OpenedDay, OpenedMinute = s.OpenedMinute,
                ClosesDay = s.ClosesDay, ClosesMinute = s.ClosesMinute, Published = s.Published,
            };
            static VendorAuthority Authority(VendorAuthority? a) => a == null ? null! : new VendorAuthority
            {
                Desired = a.Desired, RequestedHour = a.RequestedHour, FromHour = a.FromHour, UntilHour = a.UntilHour,
                Suspended = a.Suspended, OpeningId = a.OpeningId, HandbackPending = a.HandbackPending,
            };
        }


        private static void ValidateVendorTable30(VendorTable table)
        {
            Require(table != null, "Missing vendor table.");
            Require(table!.Live <= MaxVendorLive && table.Live + table.Retired <= MaxVendorLiveAndRetired && table.Tombstones <= MaxVendorTombstones
                && table.Live + table.Retired + table.Tombstones == table.Rows.Count, "Vendor rows exceed their bounds.");
            string? previous = null;
            foreach (var row in table.Rows)
            {
                Require(row != null && VendorNames.ValidId(row.VehicleId), "Invalid vendor vehicle id.");
                Require(previous == null || StringComparer.Ordinal.Compare(previous, row!.VehicleId) < 0, "Duplicate or unordered vendor vehicle id.");
                previous = row!.VehicleId;
                Require(row.Generation >= 1, "Invalid vendor generation.");
                if (row.State == VendorRowState.Tombstone)
                {
                    var a = row.Authority;
                    Require(row.Kind == "" && row.Stand == null && row.DeferredStand == null && !row.PlacePublished
                        && a != null && !a.Desired && a.RequestedHour == -1 && a.FromHour == -1 && a.UntilHour == -1
                        && !a.Suspended && a.OpeningId == -1 && !a.HandbackPending, "A vendor tombstone keeps only its identity.");
                    continue;
                }
                Require(row.Kind == "cart" || row.Kind == "truck", "Unknown vendor kind.");
                Require(row.PlacePublished == (row.Stand != null), "A vendor place exists exactly while its row has a stand.");
                Require(row.DeferredStand == null || row.Stand != null, "A deferred vendor stand needs the stand it replaces.");
                Stand(row.Stand); Stand(row.DeferredStand);
                var authority = row.Authority;
                Require(authority != null, "Missing vendor authority.");
                Require(authority!.RequestedHour >= -1 && authority.RequestedHour <= MaxVendorHour && authority.FromHour >= -1 && authority.FromHour <= MaxVendorHour
                    && authority.UntilHour >= -1 && authority.UntilHour <= MaxVendorHour && authority.OpeningId >= -1, "Invalid vendor authority.");
            }

            static void Stand(VendorStand? stand)
            {
                if (stand == null) return;
                Require(stand.OpeningId >= 1, "Invalid vendor stand opening id.");
                Point(stand.Position);
                Require(stand.OpenedDay >= 0 && stand.OpenedDay <= MaxVendorDay && stand.OpenedMinute >= 0 && stand.OpenedMinute < 1440, "Invalid vendor stand opening time.");
                bool noClose = stand.ClosesDay == -1 && stand.ClosesMinute == -1;
                Require(noClose || stand.ClosesDay <= MaxVendorDay && stand.ClosesMinute >= 0 && stand.ClosesMinute < 1440 && stand.ClosesAt > stand.OpenedAt,
                    "Invalid vendor stand closing time.");
            }
        }

        private sealed class VendorIndex30
        {
            public static readonly VendorIndex30 Empty = new VendorIndex30();
            public readonly Dictionary<string, VendorRow> ByPlace = new Dictionary<string, VendorRow>(StringComparer.Ordinal);
            public readonly List<Place> FoodPlaces = new List<Place>();
            public bool IsFoodPlace(string id) => ByPlace.TryGetValue(id, out var row) && row.State != VendorRowState.Tombstone && row.Stand != null;
        }

        private static VendorIndex30 ValidateVendors30(CityState state, ref int records)
        {
            var table = state.Vendors;
            ValidateVendorTable30(table);
            var index = new VendorIndex30();
            foreach (var row in table.Rows)
            {
                Charge(ref records);
                index.ByPlace.Add(row.PlaceId, row);
                if (row.State != VendorRowState.Tombstone && row.Stand != null)
                    index.FoodPlaces.Add(new Place { Id = row.PlaceId, Name = "Street Food " + row.Kind, Kind = PlaceKind.Shop, Capacity = 1,
                        EvidenceSource = "streetfood", Position = row.Stand.Position, OpenMinute = 0, CloseMinute = 0, WeeklyHours = VendorRules.StandHours(row.Stand) });
            }
            foreach (var place in state.Places) Require(place == null || !VendorNames.IsVendorPlace(place.Id), "A city place uses the vendor place prefix.");
            if (state.Food == null) Require(index.FoodPlaces.Count == 0, "Vendor places need a food scope.");
            return index;
        }

        private static void ValidateScopeChannels30(FoodCommerceState food, VendorIndex30 vendors)
        {
            var vehiclePlaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scope in food.Shops)
            {
                if (scope == null || scope.PlaceId == null) continue;
                Require(scope.Channel == FoodScopeChannel.Shop || scope.Channel == FoodScopeChannel.Vehicle, "Unknown food scope channel.");
                bool vendorPlace = VendorNames.IsVendorPlace(scope.PlaceId);
                if (scope.Channel == FoodScopeChannel.Shop) { Require(!vendorPlace, "A shop scope names a vendor place."); continue; }
                Require(vendorPlace, "A vehicle scope names a city place.");
                Require(vendors.IsFoodPlace(scope.PlaceId), "A vehicle scope has no live or retired vendor row with a stand.");
                Require(scope.BusinessId == vendors.ByPlace[scope.PlaceId].BusinessId, "A vehicle scope differs from its vendor row's business.");
                vehiclePlaces.Add(scope.PlaceId);
            }
            foreach (var place in vendors.FoodPlaces) Require(vehiclePlaces.Contains(place.Id), "A vendor row with a stand has no vehicle scope.");
        }

        private static bool VendorReference30(VendorRow row, string businessId)
        {
            string prefix = row.Signature + ":generation:";
            if (!businessId.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string digits = businessId.Substring(prefix.Length);
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int generation)
                && generation >= 1 && generation <= row.Generation && digits == generation.ToString(CultureInfo.InvariantCulture);
        }


        private static JsonProperty ChannelProperty30() => new JsonProperty
        {
            PropertyName = "Channel", UnderlyingName = "Channel", PropertyType = typeof(FoodScopeChannel), DeclaringType = typeof(FoodScope),
            ValueProvider = new ChannelProvider30(), Readable = true, Writable = true, Required = Required.Always
        };

        private sealed class ChannelProvider30 : IValueProvider
        {
            public object GetValue(object target) => ((FoodScope)target).Channel;
            public void SetValue(object target, object? value)
            {
                long code = value is FoodScopeChannel channel ? (long)channel : value is long number ? number : -1;
                Require(code == (long)FoodScopeChannel.Shop || code == (long)FoodScopeChannel.Vehicle, "Unknown food scope channel.");
                ((FoodScope)target).UseChannel((FoodScopeChannel)code);
            }
        }
    }
}
