using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        public const int MaxNeedsWire = 16384;

        private static string NeedsText(PersonNeeds needs, WireContext context)
        {
            string Cell(string id) => id.Length == 0 ? NoPlace27
                : PlaceCell30(context, id) ?? throw new SaveValidationException("A need names an unknown place.");
            var text = new StringBuilder(64).Append("1|")
                .Append(needs.LastImpulseDay < 0 ? "-" : needs.LastImpulseDay.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Cell(needs.ImpulseVenue)).Append('|');
            for (int i = 0; i < needs.Needs.Count; i++)
            {
                var need = needs.Needs[i];
                if (i > 0) text.Append(';');
                text.Append(Uri.EscapeDataString(need.Category)).Append(',').Append(need.NextDueDay.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Cell(need.LastVenue)).Append(',').Append(need.Deferrals.ToString(CultureInfo.InvariantCulture));
            }
            Require(text.Length <= MaxNeedsWire, "Oversized needs.");
            return text.ToString();
        }

        private static PersonNeeds ParseNeeds(string text, PlaceCells cells)
        {
            const string error = "Invalid needs.";
            Require(text.Length >= 8 && text.Length <= MaxNeedsWire && text[0] == '1' && text[1] == '|', error);
            var parts = text.Split('|');
            Require(parts.Length == 4, error);
            int impulseDay = parts[1] == "-" ? -1 : (int)PlanNumber(parts[1], int.MaxValue);
            string impulseVenue = PlanPlace(parts[2], cells);
            Require(impulseDay >= 0 || impulseVenue.Length == 0, error);
            var needs = new PersonNeeds(impulseDay, impulseVenue);
            if (parts[3].Length == 0) return needs;
            string previous = "";
            foreach (string cell in parts[3].Split(';'))
            {
                var fields = cell.Split(',');
                Require(fields.Length == 4 && fields[0].Length > 0, error);
                string category;
                try { category = Uri.UnescapeDataString(fields[0]); }
                catch (UriFormatException) { throw new SaveValidationException(error); }
                Require(Uri.EscapeDataString(category) == fields[0] && category.Length <= BusinessTypes.MaximumNameLength
                    && BusinessTypes.Normalize(category) == category, error);
                Require(string.CompareOrdinal(previous, category) < 0, error);
                previous = category;
                int due = (int)PlanNumber(fields[1], int.MaxValue);
                string venue = PlanPlace(fields[2], cells);
                int deferrals = (int)PlanNumber(fields[3], NeedModel.MaximumDeferrals);
                Require(needs.Add(new NeedState(category, due, venue, deferrals)), error);
            }
            return needs;
        }

        private static void CheckNeeds(JsonReader reader, TokenText text)
        {
            if (reader.TokenType == JsonToken.Null) return;
            Require(reader.TokenType == JsonToken.String && text.LiteralAsciiString((IJsonLineInfo)reader, (string)reader.Value!), "Invalid needs encoding.");
            ParseNeeds((string)reader.Value!, PlaceCells.Streaming);
        }

        private static void ValidateNeeds(CityState state)
        {
            int today = DayPlanner.PlanDay(state.Minute);
            foreach (var person in state.Citizens)
            {
                var needs = person?.Needs;
                if (needs == null) continue;
                Require(!person!.IsVisitor && needs.LastImpulseDay <= Math.Max(today, -1), "Invalid needs.");
            }
            if (state.VisitorRoutines != null) foreach (var routine in state.VisitorRoutines) Require(routine?.Needs == null, "A visitor cannot keep needs.");
        }

        private sealed class NeedsConverter : JsonConverter<PersonNeeds>
        {
            private readonly WireContext context;
            public NeedsConverter(WireContext context) { this.context = context; }
            public override void WriteJson(JsonWriter writer, PersonNeeds? value, JsonSerializer serializer)
            { if (value == null) writer.WriteNull(); else writer.WriteValue(NeedsText(value, context)); }
            public override PersonNeeds? ReadJson(JsonReader reader, Type objectType, PersonNeeds? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                Require(reader.TokenType == JsonToken.String, "Invalid needs.");
                return ParseNeeds((string)reader.Value!, PlaceCells.Of(context));
            }
        }

        private const string ResidentMissedField = "\"MissedActivities\":";
        private const string ResidentNextEventField = ",\"NextEventMinute\":";

        internal static bool PreNeedsBody(string source)
        {
            if (!source.StartsWith(BodyPrefix, StringComparison.Ordinal)) return false;
            int missed = source.IndexOf(ResidentMissedField, StringComparison.Ordinal);
            if (missed < 0) return false;
            int next = source.IndexOf(ResidentNextEventField, missed, StringComparison.Ordinal);
            return next >= 0 && source.IndexOf("\"Needs\":", missed, next - missed, StringComparison.Ordinal) < 0;
        }

        internal static SaveReadResult PreNeedsRefusal() => new SaveReadResult(SaveReadStatus.DevelopmentSchema, null,
            DevelopmentSchemaMessage + " (schema 1, before needs)", CityState.CurrentSchema);
    }
}
