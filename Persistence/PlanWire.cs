using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>
    /// GR-2 day plan on the wire: one ASCII string per person ("Plan" on a resident and on a visitor's routine), null before
    /// the person's first plan. The schema stays 1; a city written before day plans has no "Plan" field and is refused as an
    /// older development city and kept unchanged.
    ///
    /// "1|day|seed|shift|days off|workplace|items", items joined by ';' (empty for none). Item:
    /// "KAPMSC,venue,earliest,latest[,ends]": kind, activity, priority, meal, state and cause as one digit each; venue a place
    /// cell (PlaceCells) or "---"; minutes as invariant round-trip text, refused unless canonical; ends is written only when
    /// it differs from the latest start (today never). Workplace a place cell or "---".
    /// </summary>
    public static partial class SaveCodec
    {
        public const int MaxPlanWire = 512;

        private static string PlanText(DayPlan plan, WireContext context)
        {
            string Cell(string id) => id.Length == 0 ? NoPlace27
                : PlaceCell30(context, id) ?? throw new SaveValidationException("A day plan names an unknown place.");
            var text = new StringBuilder(160).Append("1|").Append(plan.Day.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(plan.Seed.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Digit((int)plan.Shift)).Append('|')
                .Append(plan.DaysOff.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Cell(plan.WorkplaceId)).Append('|');
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (i > 0) text.Append(';');
                text.Append(Digit((int)item.Kind)).Append(Digit((int)item.Activity)).Append(Digit((int)item.Priority)).Append(Digit((int)item.Meal))
                    .Append(Digit((int)plan.StateOf(i))).Append(Digit((int)plan.CauseOf(i))).Append(',').Append(Cell(item.VenueId)).Append(',')
                    .Append(R27(item.EarliestStart)).Append(',').Append(R27(item.LatestStart));
                if (item.EndsAt != item.LatestStart) text.Append(',').Append(R27(item.EndsAt));
            }
            Require(text.Length <= MaxPlanWire, "Oversized day plan.");
            return text.ToString();
        }

        /// <summary>The one plan parser: the streaming check (placeholder places) and the reader (the place table).</summary>
        private static DayPlan ParsePlan(string text, PlaceCells cells)
        {
            const string error = "Invalid day plan.";
            Require(text.Length >= 12 && text.Length <= MaxPlanWire && text[0] == '1' && text[1] == '|', error);
            var parts = text.Split('|');
            Require(parts.Length == 7, error);
            int day = (int)PlanNumber(parts[1], int.MaxValue);
            uint seed = (uint)PlanNumber(parts[2], uint.MaxValue);
            Require(parts[3].Length == 1 && parts[3][0] >= '0' && parts[3][0] <= '3', error);
            int daysOff = (int)PlanNumber(parts[4], 127);
            string workplace = PlanPlace(parts[5], cells);
            var items = new List<PlanItem>();
            var marks = new List<(PlanItemState, PlanMissCause)>();
            if (parts[6].Length != 0)
                foreach (string cell in parts[6].Split(';'))
                {
                    var fields = cell.Split(',');
                    Require((fields.Length == 4 || fields.Length == 5) && fields[0].Length == 6, error);
                    var code = fields[0];
                    for (int i = 0; i < 6; i++) Require(code[i] >= '0' && code[i] <= '9', error);
                    var kind = (PlanItemKind)(code[0] - '0'); var activity = (Activity)(code[1] - '0'); var priority = (PlanPriority)(code[2] - '0');
                    var meal = (MealKind)(code[3] - '0'); var state = (PlanItemState)(code[4] - '0'); var cause = (PlanMissCause)(code[5] - '0');
                    Require(Enum.IsDefined(typeof(PlanItemKind), kind) && Enum.IsDefined(typeof(Activity), activity) && Enum.IsDefined(typeof(PlanPriority), priority)
                        && Enum.IsDefined(typeof(MealKind), meal) && Enum.IsDefined(typeof(PlanItemState), state) && Enum.IsDefined(typeof(PlanMissCause), cause)
                        && (state == PlanItemState.Missed) == (cause != PlanMissCause.None), error);
                    // A meal names its kind and picks its venue when it starts; every other item has a venue and no meal kind.
                    Require(kind == PlanItemKind.Meal ? meal != MealKind.Legacy && activity == Activity.Home && fields[1] == NoPlace27
                        : meal == MealKind.Legacy && activity != Activity.Home && fields[1] != NoPlace27, error);
                    string venue = PlanPlace(fields[1], cells);
                    double earliest = ParseR27(fields[2], "plan item start"), latest = ParseR27(fields[3], "plan item latest start");
                    double ends = fields.Length == 5 ? ParseR27(fields[4], "plan item end") : latest;
                    Require(earliest <= latest && latest <= ends && ends <= MaxMinute && (fields.Length == 4 || ends != latest), error);
                    items.Add(new PlanItem(kind, activity, venue, earliest, latest, ends, priority, meal));
                    marks.Add((state, cause));
                }
            Require(items.Count <= DayPlan.MaximumItems, error);
            for (int i = 1; i < items.Count; i++) Require(items[i - 1].EarliestStart <= items[i].EarliestStart, error);
            var plan = new DayPlan(day, seed, (ShiftType)(parts[3][0] - '0'), daysOff, workplace, items);
            for (int i = 0; i < marks.Count; i++) plan.Restore(i, marks[i].Item1, marks[i].Item2);
            return plan;
        }

        private static string PlanPlace(string cell, PlaceCells cells)
        {
            if (cell == NoPlace27) return "";
            Require(cell.Length == (cell.Length > 0 && cell[0] == VendorPlaceForm ? 1 + VendorIndexWidth : 3), "Invalid day plan place.");
            return cells.Parse(cell, 0, "Invalid day plan place.");
        }

        private static long PlanNumber(string text, long maximum)
        {
            Require(text.Length >= 1 && text.Length <= 10 && (text.Length == 1 || text[0] != '0'), "Invalid day plan number.");
            long value = 0;
            foreach (char c in text) { Require(c >= '0' && c <= '9', "Invalid day plan number."); value = value * 10 + (c - '0'); }
            Require(value <= maximum, "Invalid day plan number.");
            return value;
        }

        private static void CheckPlan(JsonReader reader, TokenText text)
        {
            if (reader.TokenType == JsonToken.Null) return;
            Require(reader.TokenType == JsonToken.String && text.LiteralAsciiString((IJsonLineInfo)reader, (string)reader.Value!), "Invalid day plan encoding.");
            ParsePlan((string)reader.Value!, PlaceCells.Streaming);
        }

        /// <summary>A plan belongs to a day that has begun (its boundary has passed) and plans its own person's workplace,
        /// unless the plan is still waiting for its next event to replan; a meal item needs the food economy.</summary>
        private static void ValidatePlans(CityState state)
        {
            int today = DayPlanner.PlanDay(state.Minute);
            void Check(CitizenState person)
            {
                var plan = person?.Plan;
                if (plan == null) return;
                Require(plan.Day <= today, "A day plan for a day that has not begun.");
                foreach (var item in plan.Items)
                    Require(item.Kind != PlanItemKind.Meal || state.Food != null, "Meal plan item without economy.");
            }
            foreach (var person in state.Citizens) Check(person);
            if (state.VisitorRoutines != null) foreach (var routine in state.VisitorRoutines) Check(routine);
        }

        private sealed class PlanConverter : JsonConverter<DayPlan>
        {
            private readonly WireContext context;
            public PlanConverter(WireContext context) { this.context = context; }
            public override void WriteJson(JsonWriter writer, DayPlan? value, JsonSerializer serializer)
            { if (value == null) writer.WriteNull(); else writer.WriteValue(PlanText(value, context)); }
            public override DayPlan? ReadJson(JsonReader reader, Type objectType, DayPlan? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                Require(reader.TokenType == JsonToken.String, "Invalid day plan.");
                return ParsePlan((string)reader.Value!, PlaceCells.Of(context));
            }
        }

        private const string ResidentPlaceField = "\"PlaceId\":";
        private const string ResidentHoldField = ",\"PlayerHoldMinutes\":";

        /// <summary>The writer sorts fields ordinally, so a current resident has "Plan" between "PlaceId" and
        /// "PlayerHoldMinutes"; a pre-GR-2 resident goes straight from one to the other. One ordinal search on the first
        /// resident, no decode. A city without residents has nothing to detect and fails the strict read.</summary>
        internal static bool PrePlanBody(string source)
        {
            if (!source.StartsWith(BodyPrefix, StringComparison.Ordinal)) return false;
            int hold = source.IndexOf(ResidentHoldField, StringComparison.Ordinal);
            if (hold < 0) return false;
            int place = source.LastIndexOf(ResidentPlaceField, hold, StringComparison.Ordinal);
            return place >= 0 && source.IndexOf("\"Plan\":", place, hold - place, StringComparison.Ordinal) < 0;
        }

        internal static SaveReadResult PrePlanRefusal() => new SaveReadResult(SaveReadStatus.DevelopmentSchema, null,
            DevelopmentSchemaMessage + " (schema 1, before day plans)", CityState.CurrentSchema);
    }
}
