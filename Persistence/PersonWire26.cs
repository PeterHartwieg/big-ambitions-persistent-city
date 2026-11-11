using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    // F5c-lite (city audit F5 #92): writer 26, the person shape. docs/save-format.md describes the wire.
    // There is no migration: a city whose schema is not CityState.CurrentSchema is refused unchanged.
    public static partial class SaveCodec
    {
        public const string DevelopmentSchemaMessage = "This Persistent City save was created by an older development build; start a new city. The saved state is kept unchanged.";

        // Named schema-26 bounds (plan section 5 Bounds), checked while streaming and before every write.
        public const int MaxPersons = PersonTable.MaximumPersons;
        public const int MaxResidents = PersonTable.MaximumResidents;
        public const int MaxVisitors = PersonTable.MaximumVisitors;
        public const int MaxPortals = PersonTable.MaximumPortals;
        public const int MaxPortalCharacters = 128;
        /// <summary>Only residents own cars, so traffic vehicle lists keep the resident bound.</summary>
        public const int MaxTrafficVehicles = MaxResidents;
        public const int MaxResidentTuple = 32;
        public const int MaxVisitorTuple = 512;

        private const string ResidentPrefix26 = "1R";
        private const string VisitorPrefix26 = "1V";
        private const string NoField26 = "-";
        private const string NoPortal26 = "---";
        private const char PortalIndexMark26 = '#';
        private static readonly string EmploymentTail26 = "." + PersonTable.PersonaScheduleCode.ToString(CultureInfo.InvariantCulture)
            + "." + ((int)NativeEmployment.None).ToString(CultureInfo.InvariantCulture);

        /// <summary>Header probe for the native authority bootstrap: the stored schema if a development build wrote it (any
        /// number other than <see cref="CityState.CurrentSchema"/>, or the retired compact wire at schema 1). Reads only to the root Schema field; never allocates
        /// the population graph.</summary>
        public static int? DevelopmentSchemaOf(string? source)
        {
            if (source == null || source.Length > MaxInputCharacters) return null;
            // The retired F7d columnar wire carried schema 1 but is an older development city all the same.
            if (RetiredCompact(source)) return CityState.CurrentSchema;
            try
            {
                using var reader = Reader(source);
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return null;
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    string name = (string)reader.Value!;
                    if (!reader.Read()) return null;
                    if (name != "Schema") { reader.Skip(); continue; }
                    if (reader.TokenType != JsonToken.Integer || !(reader.Value is long schema)) return null;
                    return schema != CityState.CurrentSchema && schema >= int.MinValue && schema <= int.MaxValue ? (int)schema : (int?)null;
                }
            }
            catch (JsonException) { }
            return null;
        }

        // ---- Resident person tuple: CitizenState field "P" ----
        // "1R-" without an Employment, else "1RE<start day>.<schedule>.<native>". The workplace is the saved
        // DayPlaceId and the kind follows persona (Study for students), so neither is stored twice.

        private static string ResidentTuple26(CitizenState person) => person.DayPlaceId.Length == 0 ? ResidentPrefix26 + NoField26
            : ResidentPrefix26 + "E" + person.EmploymentStartDay.ToString(CultureInfo.InvariantCulture) + EmploymentTail26;

        /// <summary>Returns the start day, or -1 for "no Employment"; the reader reconciles it with DayPlaceId.</summary>
        private static int ParseResidentTuple26(string text)
        {
            Require(text.Length >= 2 && text[0] == '1', "Invalid person tuple version.");
            Require(text[1] != 'V', "A visitor row cannot carry resident identity, home, car or recent visits.");
            Require(text[1] == 'R', "Unknown person kind.");
            Require(text.Length <= MaxResidentTuple, "Oversized person tuple.");
            if (text.Length == 3 && text[2] == '-') return -1;
            Require(text.Length > 3 && text[2] == 'E' && text.EndsWith(EmploymentTail26, StringComparison.Ordinal), "Invalid resident employment.");
            return Day26(text, 3, text.Length - EmploymentTail26.Length, "Invalid resident employment start.");
        }

        private static int Day26(string text, int start, int end, string error)
        {
            int length = end - start;
            Require(length >= 1 && length <= 6 && (text[start] != '0' || length == 1), error);
            int value = 0;
            for (int i = start; i < end; i++) { Require(text[i] >= '0' && text[i] <= '9', error); value = value * 10 + text[i] - '0'; }
            Require(value <= MemoryWire.MaximumDay, error);
            return value;
        }

        private sealed class ResidentTupleProvider : IValueProvider
        {
            public object? GetValue(object target) => ResidentTuple26((CitizenState)target);
            public void SetValue(object target, object? value)
            {
                if (!(value is string text)) throw new SaveValidationException("Invalid person tuple.");
                ((CitizenState)target).EmploymentStartDay = ParseResidentTuple26(text);
            }
        }

        private static JsonProperty ResidentTupleProperty() => new JsonProperty
        {
            PropertyName = "P", UnderlyingName = "P", PropertyType = typeof(string), DeclaringType = typeof(CitizenState),
            ValueProvider = new ResidentTupleProvider(), Readable = true, Writable = true, Required = Required.Always
        };

        // ---- Visitor person tuple: one string in CityState.Visitors ----
        // "1V|<id>|<portal><pattern><presence>.<first day>.<next stay start binary64>|<employment>|<carried>|<arrival>"
        // portal: "---" or a base-36 index into Portals; pattern C/D; presence A/D; next: 16 hex digits.
        // employment: "-" or W/S + base-36 Places index + ".<start>.<schedule>.<native>"; carried: "-" or an
        // income-source digit; arrival: "-" or the B0 visit token (version, binary64 minute, place, activity).

        private sealed class VisitorConverter26 : JsonConverter<VisitorPerson>
        {
            private readonly WireContext context;
            public VisitorConverter26(WireContext context) { this.context = context; }
            public override void WriteJson(JsonWriter writer, VisitorPerson? value, JsonSerializer serializer)
            {
                var v = value!;
                var stay = v.Stay;
                int portal = stay.OriginPortalId.Length == 0 ? -1 : context.Portals.BinarySearch(stay.OriginPortalId, StringComparer.Ordinal);
                Require(stay.OriginPortalId.Length == 0 || portal >= 0, "Visitor portal is missing from the station table.");
                var text = new System.Text.StringBuilder(64).Append(VisitorPrefix26).Append('|').Append(v.Id).Append('|')
                    .Append(portal < 0 ? NoPortal26 : MemoryWire.Number(portal, 3))
                    .Append(stay.Pattern == VisitorPattern.Commuter ? 'C' : 'D').Append(stay.Presence == VisitorPresence.Active ? 'A' : 'D')
                    .Append('.').Append(stay.FirstDay.ToString(CultureInfo.InvariantCulture)).Append('.').Append(Bits26(stay.NextStayStart)).Append('|');
                var job = v.Employment;
                if (job == null) text.Append(NoField26);
                else text.Append(job.Kind == EmploymentKind.Work ? 'W' : 'S').Append(MemoryWire.Number(context.Index[job.WorkplaceId], 3))
                    .Append('.').Append(job.StartDay.ToString(CultureInfo.InvariantCulture)).Append(EmploymentTail26);
                text.Append('|').Append(v.CarriedIncomeSource.HasValue ? ((char)('0' + (int)v.CarriedIncomeSource.Value)).ToString() : NoField26).Append('|');
                var arrival = v.LastArrival;
                if (arrival == null) text.Append(NoField26);
                else text.Append('1').Append(Bits26(arrival.AtMinute)).Append(PlaceCell30(context, arrival.PlaceId) ?? throw new SaveValidationException("Visitor arrival names an unknown place."))
                    .Append((char)('0' + (int)arrival.Activity));
                writer.WriteValue(text.ToString());
            }
            public override VisitorPerson ReadJson(JsonReader reader, Type objectType, VisitorPerson? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Require(reader.TokenType == JsonToken.String, "Invalid visitor tuple.");
                return ParseVisitor26((string)reader.Value!, context.Places, PlaceCells.Of(context));
            }
        }

        private static string Bits26(double minute) => unchecked((ulong)BitConverter.DoubleToInt64Bits(minute)).ToString("X16", CultureInfo.InvariantCulture);

        private static double Minute26(string text, int start, string error)
        {
            for (int i = start; i < start + 16; i++) Require(text[i] >= '0' && text[i] <= '9' || text[i] >= 'A' && text[i] <= 'F', error);
            double value = BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(text.Substring(start, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            Require(Position.Finite(value), error);
            return value;
        }

        /// <summary>Structural parse only (plan section 3 Arrival evidence: reading and admission are separate).
        /// Places and portals resolve by index; cross-record rules run in <see cref="ValidatePersons27"/>.</summary>
        private static VisitorPerson ParseVisitor26(string text, IReadOnlyList<string>? places, PlaceCells cells)
        {
            Require(text.Length <= MaxVisitorTuple, "Oversized visitor tuple.");
            var parts = text.Split('|');
            Require(parts[0].Length >= 2 && parts[0][0] == '1', "Invalid visitor tuple version.");
            Require(parts[0][1] != 'R', "A resident row requires identity, home and recent visits; it cannot be a visitor tuple.");
            Require(parts[0] == VisitorPrefix26, "Unknown person kind.");
            Require(parts.Length == 6, "Visitor tuple has the wrong arity.");
            var visitor = new VisitorPerson { Id = parts[1] };
            var format = PersonIds.Classify(visitor.Id);
            Require(PersonIds.FitsKind(format, PersonKind.Visitor), "Invalid visitor ID.");

            string stay = parts[2];
            int firstDot = stay.IndexOf('.'), lastDot = stay.LastIndexOf('.');
            Require(stay.Length >= 3 + 2 + 1 + 1 + 1 + 16 && firstDot == 5 && lastDot == stay.Length - 17 && lastDot > firstDot + 1, "Invalid visitor stay.");
            int portalIndex = -1;
            if (stay.Substring(0, 3) != NoPortal26)
            {
                try { portalIndex = MemoryWire.Number(stay, 0, 3); } catch (FormatException) { Fail("Invalid visitor portal index."); }
                Require(portalIndex < MaxPortals, "Visitor portal index is outside the station table.");
            }
            Require(stay[3] == 'C' || stay[3] == 'D', "Unknown visitor stay pattern.");
            Require(stay[4] == 'A' || stay[4] == 'D', "Unknown visitor presence.");
            visitor.Stay = new VisitorStay
            {
                // Resolved against the saved station table by DerivePersons27, whatever the root field order.
                OriginPortalId = portalIndex < 0 ? "" : PortalIndexMark26 + portalIndex.ToString(CultureInfo.InvariantCulture),
                Pattern = stay[3] == 'C' ? VisitorPattern.Commuter : VisitorPattern.DayVisit,
                Presence = stay[4] == 'A' ? VisitorPresence.Active : VisitorPresence.Dormant,
                FirstDay = Day26(stay, firstDot + 1, lastDot, "Invalid visitor first day."),
                NextStayStart = Minute26(stay, lastDot + 1, "Invalid visitor next stay start.")
            };

            string job = parts[3];
            if (job != NoField26)
            {
                Require(job.Length > 4 + EmploymentTail26.Length + 1 && (job[0] == 'W' || job[0] == 'S') && job[4] == '.'
                    && job.EndsWith(EmploymentTail26, StringComparison.Ordinal), "Invalid visitor employment.");
                int place = PlaceIndex26(job, 1, places, "Invalid visitor workplace index.");
                visitor.Employment = new Employment { PersonId = visitor.Id, WorkplaceId = places != null ? places[place] : "",
                    Kind = job[0] == 'W' ? EmploymentKind.Work : EmploymentKind.Study, ScheduleCode = PersonTable.PersonaScheduleCode,
                    StartDay = Day26(job, 5, job.Length - EmploymentTail26.Length, "Invalid visitor employment start.") };
            }

            string carried = parts[4];
            if (carried != NoField26)
            {
                Require(carried.Length == 1 && carried[0] >= '0' && carried[0] <= '0' + (int)ResidentIncomeSource.ExternalSupport, "Unknown carried income source.");
                visitor.CarriedIncomeSource = (ResidentIncomeSource)(carried[0] - '0');
            }

            string arrival = parts[5];
            if (arrival != NoField26)
            {
                Require(arrival.Length == 21 && arrival[0] == '1', "Invalid arrival tuple length/version.");
                double at = Minute26(arrival, 1, "Invalid arrival minute.");
                Minute(at, "Arrival minute");
                // E6c: the arrival place may be a vendor place ("v" and a vendor table row); the workplace never is.
                string place = cells.Parse(arrival, 17, "Invalid arrival place index.");
                Require(arrival[20] >= '0' && arrival[20] <= '0' + (int)Activity.Study, "Unknown arrival activity.");
                visitor.LastArrival = new VisitRecord { AtMinute = at, PlaceId = places != null ? place : "", Activity = (Activity)(arrival[20] - '0') };
            }
            return visitor;
        }

        private static int PlaceIndex26(string text, int offset, IReadOnlyList<string>? places, string error)
        {
            int index = -1;
            try { index = MemoryWire.Number(text, offset, 3); } catch (FormatException) { Fail(error); }
            Require(index < (places?.Count ?? MaxPlaces), error);
            return index;
        }

        /// <summary>Streaming check of one visitor tuple before deserialization: canonical ASCII, bounded, well formed.</summary>
        private static void CheckVisitor26(JsonReader reader, TokenText text, PlaceCells cells)
        {
            Require(reader.TokenType == JsonToken.String, "Invalid visitor tuple.");
            string value = (string)reader.Value!;
            Require(value.Length <= MaxVisitorTuple && text.LiteralAsciiString((IJsonLineInfo)reader, value), "Invalid visitor tuple encoding.");
            ParseVisitor26(value, null, cells);
        }

        private static void CheckPortals26(JsonReader reader, TokenText text, ref int records)
        {
            Require(reader.TokenType == JsonToken.StartArray, "Expected the station table.");
            int count = 0;
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Require(++count <= MaxPortals, "Oversized station table.");
                Require(reader.TokenType == JsonToken.String && ((string)reader.Value!).Length <= MaxPortalCharacters
                    && ExactString25(text, (IJsonLineInfo)reader, (string)reader.Value!), "Invalid station ID.");
                Charge(ref records);
            }
            Require(reader.TokenType == JsonToken.EndArray, "Incomplete station table.");
        }

        private static void CheckResidentTuple26(JsonReader reader, TokenText text, ref int records)
        {
            Require(reader.TokenType == JsonToken.String, "Invalid person tuple.");
            string value = (string)reader.Value!;
            ParseResidentTuple26(value);
            Require(text.LiteralAsciiString((IJsonLineInfo)reader, value), "Invalid person tuple encoding.");
            Charge(ref records); // one fixed tuple per resident person
        }
    }
}
