using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    // C5c (docs/plans/c5-agency-research.md section 5): the research section, CityState.Research.
    // There is no migration: a city from an older development schema is refused unchanged.
    public static partial class SaveCodec
    {
        /// <summary>The first writer with the research section: the next schema after F5e's.</summary>
        public const int ResearchSchema = WorkplaceCapacitySchema + 1;
        public const int MaxResearchOpenOrders = ResearchLedger.MaximumOpenOrders;
        public const int MaxResearchKeptOrders = ResearchLedger.MaximumKeptReports;
        public const int MaxResearchClosedOrders = ResearchLedger.MaximumClosedOrders;
        /// <summary>C4's widest tier (In-depth) has 64 cells.</summary>
        public const int MaxResearchCells = 64;
        public const int MaxResearchProductCode = 64;
        /// <summary>A C4 query key: base64 of a SHA-256 hash.</summary>
        public const int ResearchReportIdLength = 44;
        /// <summary>C4's consumer cap; no cell counts more consumers than a survey may examine.</summary>
        public const int MaxResearchCellValue = MarketResearchEngine.MaximumConsumers;

        private const int ResearchVersion29 = 1;

        /// <summary>Properties that exist only from the research schema; every older writer and frozen reader omits them.</summary>
        internal static bool ResearchProperty29(Type type, string? name) => type == typeof(CityState) && name == "Research";

        // ---- Wire ----
        // Research: [1, NextSequence, [open], [kept], [closed]]
        // order:    [1, sequence, product, price units, business, agency, tax, radius, order minute, survey minute,
        //            state, decline reason, report ID, report | null]
        // report:   [1, ID, tier, logical day, address | null, radius, [[metric, band, value | null], ...]]
        // Lists keep the ledger's own order: open by sequence, kept by delivery, closed by closing.

        private sealed class ResearchConverter29 : JsonConverter<ResearchLedger>
        {
            public override void WriteJson(JsonWriter writer, ResearchLedger? value, JsonSerializer serializer)
            {
                var ledger = value!;
                writer.WriteStartArray();
                writer.WriteValue(ResearchVersion29); writer.WriteValue(ledger.NextSequence);
                WriteOrders(writer, ledger.Open); WriteOrders(writer, ledger.Kept); WriteOrders(writer, ledger.Closed);
                writer.WriteEndArray();
            }

            private static void WriteOrders(JsonWriter writer, IReadOnlyList<ResearchOrder> orders)
            {
                writer.WriteStartArray();
                foreach (var order in orders)
                {
                    writer.WriteStartArray();
                    writer.WriteValue(ResearchVersion29); writer.WriteValue(order.Sequence); writer.WriteValue(order.ProductCode);
                    writer.WriteValue(order.Price.Units); writer.WriteValue(order.BusinessAddress); writer.WriteValue(order.AgencyAddress);
                    writer.WriteValue(order.TaxDeductible); writer.WriteValue(order.RadiusMetres);
                    writer.WriteValue(order.OrderMinute); writer.WriteValue(order.SurveyAtMinute);
                    writer.WriteValue((int)order.State); writer.WriteValue((int)order.DeclineReason); writer.WriteValue(order.ReportId);
                    var report = order.Report;
                    if (report == null) writer.WriteNull();
                    else
                    {
                        writer.WriteStartArray();
                        writer.WriteValue(ResearchVersion29); writer.WriteValue(report.Id); writer.WriteValue((int)report.Tier);
                        writer.WriteValue(report.LogicalDay);
                        if (report.Address == null) writer.WriteNull(); else writer.WriteValue(report.Address);
                        writer.WriteValue(report.RadiusMetres);
                        writer.WriteStartArray();
                        foreach (var cell in report.FixtureCells)
                        {
                            writer.WriteStartArray();
                            writer.WriteValue((int)cell.Metric); writer.WriteValue((int)cell.Band);
                            if (cell.Value.HasValue) writer.WriteValue(cell.Value.Value); else writer.WriteNull();
                            writer.WriteEndArray();
                        }
                        writer.WriteEndArray();
                        writer.WriteEndArray();
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
            }

            public override ResearchLedger ReadJson(JsonReader reader, Type objectType, ResearchLedger? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                int records = 0;
                return ReadResearch29(reader, null, ref records);
            }
        }

        /// <summary>Streaming check before deserialization: canonical tokens, bounds before allocation, then the
        /// same restore the reader runs, so a payload that passes here decodes to exactly one ledger.</summary>
        private static void CheckResearch29(JsonReader reader, TokenText text, ref int records) => ReadResearch29(reader, text, ref records);

        /// <summary>Strict decode of the research section. With <paramref name="text"/> it also checks the original
        /// token text (canonical integers and money, exact strings). Charges one record per order.</summary>
        private static ResearchLedger ReadResearch29(JsonReader reader, TokenText? text, ref int records)
        {
            Require(reader.TokenType == JsonToken.StartArray, "Expected the research section.");
            Version29(reader, text, "Invalid research section version.");
            long next = Long29(reader, text, 1, long.MaxValue, "Invalid research next sequence.");
            var open = Orders29(reader, text, MaxResearchOpenOrders, ref records, "open");
            var kept = Orders29(reader, text, MaxResearchKeptOrders, ref records, "kept");
            var closed = Orders29(reader, text, MaxResearchClosedOrders, ref records, "closed");
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus research section slot.");
            return RestoreResearch29(next, open, kept, closed);
        }

        private static List<ResearchOrderImage> Orders29(JsonReader reader, TokenText? text, int limit, ref int records, string list)
        {
            // Messages are built only on failure: these checks run for every order of every save and load.
            if (!reader.Read() || reader.TokenType != JsonToken.StartArray) Fail("Expected the research " + list + " orders.");
            var orders = new List<ResearchOrderImage>();
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                if (orders.Count >= limit) Fail("Research " + list + " orders exceed the bound of " + limit.ToString(CultureInfo.InvariantCulture) + ".");
                Charge(ref records);
                orders.Add(Order29(reader, text));
            }
            if (reader.TokenType != JsonToken.EndArray) Fail("Incomplete research " + list + " orders.");
            return orders;
        }

        private static ResearchOrderImage Order29(JsonReader reader, TokenText? text)
        {
            Require(reader.TokenType == JsonToken.StartArray, "Expected a research order.");
            Version29(reader, text, "Invalid research order version.");
            var order = new ResearchOrderImage
            {
                Sequence = Long29(reader, text, 1, long.MaxValue, "Invalid research order sequence."),
                ProductCode = String29(reader, text, MaxResearchProductCode, false, "Invalid research product code.")
            };
            Require(reader.Read() && reader.TokenType == JsonToken.Integer && reader.Value is long
                && (text == null || TokenText.CanonicalUnits(text.Number((IJsonLineInfo)reader))), "Research price must be a canonical whole number of units.");
            order.Price = Money.FromUnits((long)reader.Value!);
            order.BusinessAddress = String29(reader, text, ResearchLedger.MaximumAddressLength, false, "Invalid research business address.");
            order.AgencyAddress = String29(reader, text, ResearchLedger.MaximumAddressLength, false, "Invalid research agency address.");
            Require(reader.Read() && reader.TokenType == JsonToken.Boolean, "Invalid research tax flag.");
            order.TaxDeductible = (bool)reader.Value!;
            order.RadiusMetres = (int)Long29(reader, text, 0, 2000, "Invalid research radius.");
            order.OrderMinute = Double29(reader, text, "Invalid research order minute.");
            order.SurveyAtMinute = Double29(reader, text, "Invalid research survey minute.");
            order.State = (ResearchOrderState)Long29(reader, text, 0, (int)ResearchOrderState.Replaced, "Unknown research order state.");
            order.DeclineReason = (ResearchDeclineReason)Long29(reader, text, 0, (int)ResearchDeclineReason.BalancePrecision, "Unknown research decline reason.");
            order.ReportId = String29(reader, text, ResearchReportIdLength, true, "Invalid research report ID.");
            Require(reader.Read(), "Missing research report.");
            if (reader.TokenType != JsonToken.Null) order.Report = Report29(reader, text);
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus research order slot.");
            return order;
        }

        private static MarketResearchReport Report29(JsonReader reader, TokenText? text)
        {
            Require(reader.TokenType == JsonToken.StartArray, "Expected a research report.");
            Version29(reader, text, "Invalid research report version.");
            string id = String29(reader, text, ResearchReportIdLength, false, "Invalid research report ID.");
            var tier = (MarketResearchTier)Long29(reader, text, 0, (int)MarketResearchTier.InDepth, "Unknown research tier.");
            int day = (int)Long29(reader, text, 0, (long)(MaxMinute / 1440), "Invalid research logical day.");
            Require(reader.Read() && (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.String), "Invalid research report address.");
            string? address = null;
            if (reader.TokenType == JsonToken.String)
            {
                address = (string)reader.Value!;
                Require(address.Length <= ResearchLedger.MaximumAddressLength && !string.IsNullOrWhiteSpace(address)
                    && (text == null || ExactString25(text, (IJsonLineInfo)reader, address)), "Invalid research report address.");
            }
            int radius = (int)Long29(reader, text, 0, 2000, "Invalid research report radius.");
            Require(reader.Read() && reader.TokenType == JsonToken.StartArray, "Expected research cells.");
            var cells = new List<MarketResearchCell>(ResearchCellCount29(tier));
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Require(cells.Count < MaxResearchCells, "Research cells exceed the bound of 64.");
                Require(reader.TokenType == JsonToken.StartArray, "Expected a research cell.");
                var metric = (MarketResearchMetric)Long29(reader, text, 0, (int)MarketResearchMetric.RivalMarketShare, "Unknown research metric.");
                var band = (MarketResearchBand)Long29(reader, text, 0, (int)MarketResearchBand.Other, "Unknown research band.");
                Require(reader.Read() && (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.Integer), "Invalid research cell value.");
                int? value = null;
                if (reader.TokenType == JsonToken.Integer)
                {
                    Require(IntegerToken(reader.Value, 0, MaxResearchCellValue) && (text == null || text.CanonicalInteger((IJsonLineInfo)reader)), "Invalid research cell value.");
                    value = (int)(long)reader.Value!;
                }
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus research cell slot.");
                cells.Add(new MarketResearchCell(metric, band, value));
            }
            Require(reader.TokenType == JsonToken.EndArray, "Incomplete research cells.");
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus research report slot.");
            return new MarketResearchReport(id, tier, day, address, radius, cells.ToArray());
        }

        private static void Version29(JsonReader reader, TokenText? text, string error)
            => Require(reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, ResearchVersion29, ResearchVersion29)
                && (text == null || text.CanonicalInteger((IJsonLineInfo)reader)), error);

        private static long Long29(JsonReader reader, TokenText? text, long min, long max, string error)
        {
            Require(reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, min, max)
                && (text == null || text.CanonicalInteger((IJsonLineInfo)reader)), error);
            return (long)reader.Value!;
        }

        private static double Double29(JsonReader reader, TokenText? text, string error)
        {
            Require(reader.Read() && (reader.TokenType == JsonToken.Float && reader.Value is double
                || reader.TokenType == JsonToken.Integer && reader.Value is long && (text == null || text.CanonicalInteger((IJsonLineInfo)reader))), error);
            double value = reader.Value is double d ? d : (long)reader.Value!;
            Require(Position.Finite(value) && value >= 0 && value <= MaxMinute, error);
            return value;
        }

        private static string String29(JsonReader reader, TokenText? text, int max, bool emptyAllowed, string error)
        {
            Require(reader.Read() && reader.TokenType == JsonToken.String, error);
            string value = (string)reader.Value!;
            Require(value.Length <= max && (emptyAllowed || !string.IsNullOrWhiteSpace(value))
                && (text == null || ExactString25(text, (IJsonLineInfo)reader, value)), error);
            return value;
        }

        // ---- Rules shared by the reader and the writer ----

        /// <summary>The ledger's own restore, then the report rules it does not own: the C4 cell layout of the tier,
        /// the query key form and the logical day of the survey minute.</summary>
        private static ResearchLedger RestoreResearch29(long next, List<ResearchOrderImage> open, List<ResearchOrderImage> kept, List<ResearchOrderImage> closed)
        {
            foreach (var rows in new[] { open, kept, closed }) foreach (var order in rows) ValidateResearchReport29(order);
            if (!ResearchLedger.TryRestore(next, open, kept, closed, out var ledger, out string error)) Fail("Invalid research ledger: " + error + ".");
            return ledger!;
        }

        /// <summary>Write-side check of the in-memory ledger through the same restore the reader runs. Returns the
        /// records it charges, the same count as the streaming check.</summary>
        private static int ValidateResearch29(CityState state)
        {
            var ledger = state.Research;
            Require(ledger != null, "Missing research ledger.");
            Require(ledger.Open.Count <= MaxResearchOpenOrders && ledger.Kept.Count <= MaxResearchKeptOrders && ledger.Closed.Count <= MaxResearchClosedOrders,
                "Research orders exceed their bounds.");
            var open = Images29(ledger.Open); var kept = Images29(ledger.Kept); var closed = Images29(ledger.Closed);
            foreach (var rows in new[] { open, kept, closed })
                foreach (var order in rows)
                {
                    Text(order.ProductCode!, "research product code", MaxResearchProductCode);
                    Require(order.ReportId != null && order.ReportId.Length <= ResearchReportIdLength, "Invalid research report ID.");
                    Minute(order.OrderMinute, "research order minute"); Minute(order.SurveyAtMinute, "research survey minute");
                }
            RestoreResearch29(ledger.NextSequence, open, kept, closed);
            return open.Count + kept.Count + closed.Count;
        }

        private static List<ResearchOrderImage> Images29(IReadOnlyList<ResearchOrder> orders)
        {
            var images = new List<ResearchOrderImage>(orders.Count);
            foreach (var order in orders) images.Add(ResearchOrderImage.Of(order));
            return images;
        }

        private static void ValidateResearchReport29(ResearchOrderImage order)
        {
            if (!string.IsNullOrEmpty(order.ReportId)) Require(ReportId29(order.ReportId!), "Invalid research report ID.");
            var report = order.Report;
            if (report == null) return;
            Require(ReportId29(report.Id), "Invalid research report ID.");
            Require(report.LogicalDay == (int)Math.Floor(order.SurveyAtMinute / 1440), "Research report day differs from its survey minute.");
            var cells = report.FixtureCells;
            Require(cells.Count <= MaxResearchCells, "Research cells exceed the bound of 64.");
            Require(cells.Count == ResearchCellCount29(report.Tier), "Research cell count differs from its tier.");
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var (metric, band) = ResearchCell29(i);
                Require(cell != null && cell.Metric == metric && cell.Band == band, "Research cell differs from the C4 layout.");
                Require(!cell!.Value.HasValue || cell.Value.Value >= 0 && cell.Value.Value <= MaxResearchCellValue, "Invalid research cell value.");
                // C4 has no input for loyalty or rival share yet; those cells are always unavailable.
                Require(i < 62 || !cell.Value.HasValue, "Research cell has a value C4 cannot produce.");
            }
        }

        private static bool ReportId29(string id)
        {
            if (id.Length != ResearchReportIdLength || id[ResearchReportIdLength - 1] != '=') return false;
            for (int i = 0; i < ResearchReportIdLength - 1; i++)
            {
                char c = id[i];
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '+' || c == '/')) return false;
            }
            return true;
        }

        /// <summary>C4's report lengths: Basic 13, Detailed 62, In-depth 64 (MarketResearchEngine.Project).</summary>
        internal static int ResearchCellCount29(MarketResearchTier tier) => tier == MarketResearchTier.Basic ? 13 : tier == MarketResearchTier.Detailed ? 62 : 64;

        /// <summary>The fixed metric and band of cell <paramref name="index"/>, as MarketResearchEngine.Project lays them out.
        /// ResearchSaveTests pins this table against the engine's own reports.</summary>
        internal static (MarketResearchMetric Metric, MarketResearchBand Band) ResearchCell29(int index)
        {
            if (index < 5) return ((MarketResearchMetric)index, MarketResearchBand.All);
            if (index < 8) return (MarketResearchMetric.HouseholdFood, index == 5 ? MarketResearchBand.Funded : index == 6 ? MarketResearchBand.Unfunded : MarketResearchBand.Unknown);
            if (index < 12) return (MarketResearchMetric.DiscretionaryBudget, index == 8 ? MarketResearchBand.Low : index == 9 ? MarketResearchBand.Neutral
                : index == 10 ? MarketResearchBand.High : MarketResearchBand.Unknown);
            if (index == 12) return (MarketResearchMetric.UsualLocationUnknown, MarketResearchBand.All);
            if (index < 58) return ((MarketResearchMetric)((int)MarketResearchMetric.CroissantTaste + (index - 13) / 5), (MarketResearchBand)((int)MarketResearchBand.Low + (index - 13) % 5));
            if (index < 61) return (MarketResearchMetric.UsualDayGroup, index == 58 ? MarketResearchBand.Work : index == 59 ? MarketResearchBand.Study : MarketResearchBand.Other);
            if (index == 61) return (MarketResearchMetric.TraitsUnknown, MarketResearchBand.Unknown);
            return (index == 62 ? MarketResearchMetric.LoyaltyDistribution : MarketResearchMetric.RivalMarketShare, MarketResearchBand.Unknown);
        }
    }
}
