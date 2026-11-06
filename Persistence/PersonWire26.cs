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
    // There is no migration: schemas 1-25 come from older development builds and are refused unchanged.
    public static partial class SaveCodec
    {
        public const int PersonSchema = 26;
        /// <summary>Newest schema written by a development build before the person shape; it and older ones are refused.</summary>
        public const int LatestDevelopmentSchema = 25;
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

        /// <summary>Properties that exist only from schema 26; every older writer and frozen reader omits them.</summary>
        internal static bool PersonProperty26(Type type, string? name)
            => type == typeof(CityState) && (name == "Visitors" || name == "NextVisitorSequence" || name == "IdentityPolicyVersion" || name == "Portals")
            || type == typeof(CitizenState) && name == "EmploymentStartDay";

        /// <summary>Header probe for the native authority bootstrap: the stored schema if a development build wrote it.
        /// Reads only to the root Schema field; never allocates the population graph.</summary>
        public static int? DevelopmentSchemaOf(string? source)
        {
            if (source == null || source.Length > MaxInputCharacters) return null;
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
                    return schema >= 1 && schema <= LatestDevelopmentSchema ? (int)schema : (int?)null;
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
                else text.Append('1').Append(Bits26(arrival.AtMinute)).Append(MemoryWire.Number(context.Index[arrival.PlaceId], 3)).Append((char)('0' + (int)arrival.Activity));
                writer.WriteValue(text.ToString());
            }
            public override VisitorPerson ReadJson(JsonReader reader, Type objectType, VisitorPerson? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Require(reader.TokenType == JsonToken.String, "Invalid visitor tuple.");
                return ParseVisitor26((string)reader.Value!, context.Places);
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
        /// Places and portals resolve by index; cross-record rules run in <see cref="ValidatePersons26"/>.</summary>
        private static VisitorPerson ParseVisitor26(string text, IReadOnlyList<string>? places)
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
                // Resolved against the saved station table by DerivePersons26, whatever the root field order.
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
                int place = PlaceIndex26(arrival, 17, places, "Invalid arrival place index.");
                Require(arrival[20] >= '0' && arrival[20] <= '0' + (int)Activity.Study, "Unknown arrival activity.");
                visitor.LastArrival = new VisitRecord { AtMinute = at, PlaceId = places != null ? places[place] : "", Activity = (Activity)(arrival[20] - '0') };
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
        private static void CheckVisitor26(JsonReader reader, TokenText text)
        {
            Require(reader.TokenType == JsonToken.String, "Invalid visitor tuple.");
            string value = (string)reader.Value!;
            Require(value.Length <= MaxVisitorTuple && text.LiteralAsciiString((IJsonLineInfo)reader, value), "Invalid visitor tuple encoding.");
            ParseVisitor26(value, null);
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

        private static void CheckResidentTuple26(JsonReader reader, TokenText text)
        {
            Require(reader.TokenType == JsonToken.String, "Invalid person tuple.");
            string value = (string)reader.Value!;
            ParseResidentTuple26(value);
            Require(text.LiteralAsciiString((IJsonLineInfo)reader, value), "Invalid person tuple encoding.");
        }

        /// <summary>After a schema-26 decode and before validation: reconcile resident Employment with DayPlaceId and
        /// derive each roster record's runtime host and slot from the person table (factor-1 roster rule).</summary>
        private static void DerivePersons26(CityState state)
        {
            Require(state.Citizens != null && state.Visitors != null, "Missing person table.");
            foreach (var person in state.Citizens)
            {
                Require(person != null, "Null citizen.");
                bool employed = person.DayPlaceId != null && person.DayPlaceId.Length > 0;
                Require(employed == person.EmploymentStartDay >= 0, "Resident Employment differs from its day place.");
                if (!employed) person.EmploymentStartDay = 0;
            }
            Require(state.Portals != null, "Missing station table.");
            foreach (var visitor in state.Visitors)
            {
                Require(visitor?.Stay != null, "Missing visitor stay.");
                string mark = visitor!.Stay.OriginPortalId;
                if (mark.Length == 0) continue;
                Require(mark[0] == PortalIndexMark26, "Invalid visitor portal.");
                int index = int.Parse(mark.Substring(1), CultureInfo.InvariantCulture);
                Require(index < state.Portals!.Count, "Visitor portal index is outside the station table.");
                visitor.Stay.OriginPortalId = state.Portals[index];
            }
            var food = state.Food;
            if (food == null) { Require(state.Visitors.Count == 0, "Visitor persons require a food roster in writer 26."); return; }
            int residents = state.Citizens.Count;
            Require(residents > 0 && state.Visitors.Count % residents == 0 && state.Visitors.Count / residents <= 2,
                "Visitor persons are not the represented consumers of the residents.");
            food.RepresentationFactor = 1 + state.Visitors.Count / residents;
            var hosts = RepresentedHosts26(state, food.RepresentationFactor);
            Require(food.Residents != null, "Missing meal roster.");
            var residentIds = new HashSet<string>(state.Citizens.Select(c => c.Identity?.Id ?? ""), StringComparer.Ordinal);
            foreach (var meal in food.Residents)
            {
                Require(meal != null, "Null meal roster record.");
                if (residentIds.Contains(meal.ResidentId)) { meal.AnchorId = meal.ResidentId; meal.Slot = 0; }
                else if (hosts.TryGetValue(meal.ResidentId, out var host)) { meal.AnchorId = host.Anchor; meal.Slot = host.Slot; }
                else Fail("Meal roster record has no person: " + meal.ResidentId);
            }
        }

        /// <summary>The represented consumer IDs FoodRepresentation would enrol for this factor, by host.</summary>
        private static Dictionary<string, (string Anchor, int Slot)> RepresentedHosts26(CityState state, int factor)
        {
            var hosts = new Dictionary<string, (string, int)>(state.Citizens.Count * Math.Max(0, factor - 1), StringComparer.Ordinal);
            foreach (var person in state.Citizens)
                for (int slot = 1; slot < factor; slot++)
                    if (person?.Identity != null) hosts[FoodRepresentation.ConsumerId(person.Identity.Id, slot)] = (person.Identity.Id, slot);
            return hosts;
        }

        /// <summary>Writer-26 person rules, on every read and before every write. Returns records charged.</summary>
        private static int ValidatePersons26(CityState state, Dictionary<string, Place> places, Dictionary<string, int> assigned)
        {
            Require(state.Visitors != null && state.Portals != null, "Missing person table.");
            long persons = (long)state.Citizens.Count + state.Visitors.Count;
            Require(persons <= MaxPersons, "Persons exceed the bound of " + MaxPersons + ".");
            Require(state.Citizens.Count <= MaxResidents, "Residents exceed the bound of " + MaxResidents + ".");
            Require(state.Visitors.Count <= MaxVisitors, "Visitors exceed the bound of " + MaxVisitors + ".");
            Require(state.IdentityPolicyVersion == IdentityPolicy.CurrentVersion, "Unknown identity policy version.");
            Require(state.Portals.Count <= MaxPortals, "Oversized station table.");
            for (int i = 0; i < state.Portals.Count; i++)
            {
                Text(state.Portals[i], "Station ID", MaxPortalCharacters);
                Require(i == 0 || string.CompareOrdinal(state.Portals[i - 1], state.Portals[i]) < 0, "Station table is unordered or duplicated.");
            }
            int day = (int)(state.Minute / 1440);
            foreach (var person in state.Citizens)
            {
                if (person.DayPlaceId.Length == 0) continue;
                Require(person.EmploymentStartDay >= (int)(person.JoinedMinute / 1440) && person.EmploymentStartDay <= person.LastEmploymentCheckDay,
                    "Resident Employment start is outside its residence or employment checks.");
            }
            var residents = new HashSet<string>(state.Citizens.Select(c => c.Identity.Id), StringComparer.Ordinal);
            var food = state.Food;
            int factor = food?.RepresentationFactor ?? 1;
            Require(food != null || state.Visitors.Count == 0, "Visitor persons require a food roster in writer 26.");
            Require((long)state.Visitors.Count == (long)(factor - 1) * state.Citizens.Count, "Visitor persons differ from the represented roster.");
            var hosts = RepresentedHosts26(state, factor);
            int visitorSequences = 0;
            string? previous = null;
            foreach (var visitor in state.Visitors)
            {
                Require(visitor != null && visitor.Stay != null, "Missing visitor stay.");
                Text(visitor.Id, "Visitor ID", 256);
                Require(PersonIds.FitsKind(PersonIds.Classify(visitor.Id), PersonKind.Visitor), "Invalid visitor ID.");
                Require(previous == null || string.CompareOrdinal(previous, visitor.Id) < 0, "Visitor persons are unordered or duplicated.");
                previous = visitor.Id;
                Require(!residents.Contains(visitor.Id), "Visitor duplicates a resident ID.");
                if (PersonIds.Classify(visitor.Id) == PersonIdFormat.Visitor)
                {
                    visitorSequences++;
                    Require(int.Parse(visitor.Id.Substring(PersonIds.VisitorPrefix.Length), CultureInfo.InvariantCulture) < state.NextVisitorSequence, "Visitor ID is not yet allocated.");
                }
                // Writer 26 has no portal arrivals: every visitor is one resident's represented consumer (section 9 Q3).
                Require(hosts.ContainsKey(visitor.Id), "Visitor has no represented host; writer 26 creates no other visitors.");
                var stay = visitor.Stay;
                Known(stay.Pattern, "visitor stay pattern"); Known(stay.Presence, "visitor presence");
                Require(stay.OriginPortalId.Length == 0 || state.Portals.BinarySearch(stay.OriginPortalId, StringComparer.Ordinal) >= 0, "Visitor portal is missing from the station table.");
                Require(stay.FirstDay >= 0 && stay.FirstDay <= day, "Invalid visitor first day.");
                if (stay.Presence == VisitorPresence.Active) Require(BitConverter.DoubleToInt64Bits(stay.NextStayStart) == 0, "An active visitor has no next stay start.");
                else
                {
                    Minute(stay.NextStayStart, "Visitor next stay start");
                    Require(stay.NextStayStart > state.Minute, "A dormant visitor's next stay must be in the future.");
                    Require(visitor.LastArrival == null, "A dormant visitor holds no arrival.");
                }
                var job = visitor.Employment;
                if (job != null)
                {
                    Require(stay.Pattern == VisitorPattern.Commuter, "A day visitor holds no employment.");
                    Require(job.PersonId == visitor.Id && job.IsActive && job.ScheduleCode == PersonTable.PersonaScheduleCode && job.NativeEmployment == NativeEmployment.None,
                        "Invalid visitor employment.");
                    Reference(places, job.WorkplaceId, job.Kind == EmploymentKind.Study ? PlaceKind.Study : PlaceKind.Work);
                    Require(job.StartDay >= stay.FirstDay && job.StartDay <= day, "Invalid visitor employment start.");
                    int count = assigned.TryGetValue(job.WorkplaceId, out int used) ? used + 1 : 1;
                    if (count > places[job.WorkplaceId].Capacity) Fail("Place assignment exceeds capacity: " + job.WorkplaceId);
                    assigned[job.WorkplaceId] = count;
                }
                if (visitor.CarriedIncomeSource.HasValue) Known(visitor.CarriedIncomeSource.Value, "carried income source");
                var arrival = visitor.LastArrival;
                if (arrival != null)
                {
                    Minute(arrival.AtMinute, "Arrival minute");
                    Require(arrival.AtMinute <= state.Minute, "Arrival is in the future.");
                    Known(arrival.Activity, "arrival activity");
                    Require(places.ContainsKey(arrival.PlaceId), "Unknown arrival place.");
                }
            }
            Require(state.NextVisitorSequence >= 1 && state.NextVisitorSequence - 1 == visitorSequences, "Visitor creation and active counts disagree.");
            return state.Visitors.Count;
        }
    }
}
