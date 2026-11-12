using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public enum VenueEventKind { Visit, Bought, Refused, Missed }

    /// <summary>GR-7 (R6): one non-food visit outcome. A refused purchase carries exactly one <see cref="PurchaseRefusal"/> and
    /// a missed visit exactly one <see cref="PlanMissCause"/>; the other cause is None.</summary>
    public readonly struct VenueEvent
    {
        public VenueEvent(double minute, string residentId, string venueId, VenueEventKind kind, PurchaseRefusal refusal, PlanMissCause miss, Money total)
        {
            if (kind == VenueEventKind.Refused ? refusal == PurchaseRefusal.None || miss != PlanMissCause.None
                : kind == VenueEventKind.Missed ? miss == PlanMissCause.None || refusal != PurchaseRefusal.None
                : refusal != PurchaseRefusal.None || miss != PlanMissCause.None)
                throw new ArgumentException("A refused or missed visit records exactly one cause; a visit or a purchase none.");
            Minute = minute; ResidentId = residentId ?? ""; VenueId = venueId ?? ""; Kind = kind; Refusal = refusal; Miss = miss; Total = total;
        }
        public double Minute { get; }
        public string ResidentId { get; }
        public string VenueId { get; }
        public VenueEventKind Kind { get; }
        public PurchaseRefusal Refusal { get; }
        public PlanMissCause Miss { get; }
        public Money Total { get; }
        public int Day => (int)Math.Floor(Minute / 1440);
        /// <summary>The cause's word key, or empty for a visit or a purchase.</summary>
        public string CauseKey => Kind == VenueEventKind.Refused ? VenueWords.RefusalKey(Refusal) : Kind == VenueEventKind.Missed ? VenueWords.MissKey(Miss) : "";
    }

    /// <summary>GR-7: the recent non-food outcomes the simulator observed, oldest first, at most <see cref="Capacity"/>. Derived
    /// and never saved: a reloaded city starts an empty log, and nothing in the simulation reads it.</summary>
    public sealed class VenueFeedbackLog
    {
        public const int Capacity = 4096;
        private readonly VenueEvent[] ring = new VenueEvent[Capacity];
        private int start, count;
        public int Count => count;
        public long Recorded { get; private set; }
        public VenueEvent this[int index] => index >= 0 && index < count ? ring[(start + index) % Capacity] : throw new ArgumentOutOfRangeException(nameof(index));

        public void Add(VenueEvent item)
        {
            if (count == Capacity) { ring[start] = item; start = (start + 1) % Capacity; }
            else ring[(start + count++) % Capacity] = item;
            Recorded++;
        }
    }

    /// <summary>One venue's row in the business panel for one day.</summary>
    public sealed class VenueFeedbackRow
    {
        internal VenueFeedbackRow(string venueId) { VenueId = venueId; }
        public string VenueId { get; }
        public int Visits { get; internal set; }
        public int Bought { get; internal set; }
        public Money Sales { get; internal set; }
        /// <summary>Refused purchases and missed visits by cause key, in key order.</summary>
        public SortedDictionary<string, int> Refusals { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public int RefusalCount { get { int n = 0; foreach (var pair in Refusals) n += pair.Value; return n; } }
    }

    /// <summary>GR-7 business panel and inspector words for non-food visits, purchases and refusals.</summary>
    public static class VenueFeedbackPanel
    {
        /// <summary>Rows for <paramref name="venueIds"/> on <paramref name="day"/>, in the given order; a venue without events
        /// gets an empty row.</summary>
        public static IReadOnlyList<VenueFeedbackRow> Build(VenueFeedbackLog log, IReadOnlyList<string> venueIds, int day)
        {
            var rows = new List<VenueFeedbackRow>(venueIds.Count);
            var byId = new Dictionary<string, VenueFeedbackRow>(StringComparer.Ordinal);
            foreach (var id in venueIds) if (!byId.ContainsKey(id)) { var row = new VenueFeedbackRow(id); byId.Add(id, row); rows.Add(row); }
            for (int i = 0; i < log.Count; i++)
            {
                var item = log[i];
                if (item.Day != day || !byId.TryGetValue(item.VenueId, out var row)) continue;
                switch (item.Kind)
                {
                    case VenueEventKind.Visit: row.Visits++; break;
                    case VenueEventKind.Bought: row.Bought++; row.Sales += item.Total; break;
                    default: row.Refusals[item.CauseKey] = row.Refusals.TryGetValue(item.CauseKey, out int n) ? n + 1 : 1; break;
                }
            }
            return rows;
        }

        /// <summary>The panel lines of one row. <paramref name="words"/> localizes a key and falls back to the English given.</summary>
        public static string Text(VenueFeedbackRow row, string name, Func<string, string, string>? words = null)
        {
            Func<string, string, string> w = words ?? ((_, english) => english);
            var text = new StringBuilder();
            text.Append(name).Append(": ")
                .Append(Format(w(VenueWords.PanelVisits, VenueWords.English(VenueWords.PanelVisits)), row.Visits.ToString(CultureInfo.InvariantCulture))).Append(" · ")
                .Append(Format(w(VenueWords.PanelBought, VenueWords.English(VenueWords.PanelBought)), row.Bought.ToString(CultureInfo.InvariantCulture), Dollars(row.Sales)));
            if (row.Refusals.Count == 0) text.Append(" · ").Append(w(VenueWords.PanelNoRefusals, VenueWords.English(VenueWords.PanelNoRefusals)));
            foreach (var pair in row.Refusals)
                text.Append(" · ").Append(Format(w(VenueWords.PanelRefused, VenueWords.English(VenueWords.PanelRefused)),
                    pair.Value.ToString(CultureInfo.InvariantCulture), w(pair.Key, VenueWords.English(pair.Key))));
            return text.ToString();
        }

        /// <summary>Today's non-food outcomes of one resident, oldest first, one line each.</summary>
        public static IReadOnlyList<string> ResidentLines(VenueFeedbackLog log, string residentId, int day, Func<string, string> placeName,
            Func<string, string, string>? words = null)
        {
            Func<string, string, string> w = words ?? ((_, english) => english);
            var lines = new List<string>();
            for (int i = 0; i < log.Count; i++)
            {
                var item = log[i];
                if (item.Day != day || !string.Equals(item.ResidentId, residentId, StringComparison.Ordinal)) continue;
                string place = placeName(item.VenueId), at = ResidentInsight.Clock(item.Minute);
                string key = item.Kind == VenueEventKind.Visit ? VenueWords.ResidentVisit : item.Kind == VenueEventKind.Bought ? VenueWords.ResidentBought
                    : item.Kind == VenueEventKind.Refused ? VenueWords.ResidentRefused : VenueWords.ResidentMissed;
                string cause = item.CauseKey.Length == 0 ? "" : w(item.CauseKey, VenueWords.English(item.CauseKey));
                lines.Add(Format(w(key, VenueWords.English(key)), at, place, item.Kind == VenueEventKind.Bought ? Dollars(item.Total) : cause));
            }
            return lines;
        }

        private static string Format(string template, params string[] values)
        {
            var text = new StringBuilder(template);
            for (int i = 0; i < values.Length; i++) text.Replace("{" + i.ToString(CultureInfo.InvariantCulture) + "}", values[i]);
            return text.ToString();
        }

        // Whole dollars for display only.
        private static string Dollars(Money amount)
            => ((decimal)amount.Units / Money.UnitsPerDollar).ToString("C0", CultureInfo.GetCultureInfo("en-US"));
    }

    /// <summary>GR-7 words: localization keys in Runtime/Localization/en.json with their English text, which is also the
    /// fallback when the table is not loaded.</summary>
    public static class VenueWords
    {
        public const string Prefix = "persistentcity_venue_";
        public const string PanelHeading = Prefix + "panel_heading", PanelVisits = Prefix + "panel_visits", PanelBought = Prefix + "panel_bought",
            PanelRefused = Prefix + "panel_refused", PanelNoRefusals = Prefix + "panel_no_refusals",
            ResidentVisit = Prefix + "resident_visit", ResidentBought = Prefix + "resident_bought",
            ResidentRefused = Prefix + "resident_refused", ResidentMissed = Prefix + "resident_missed";

        private static readonly Dictionary<string, string> Table = Build();

        private static Dictionary<string, string> Build()
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PanelHeading] = "Other businesses today: resident visits, purchases and refusals",
                [PanelVisits] = "{0} visits",
                [PanelBought] = "{0} purchases, {1}",
                [PanelRefused] = "{0} refused: {1}",
                [PanelNoRefusals] = "no refusals",
                [ResidentVisit] = "{0} visited {1}",
                [ResidentBought] = "{0} bought at {1} for {2}",
                [ResidentRefused] = "{0} did not buy at {1}: {2}",
                [ResidentMissed] = "{0} missed a visit to {1}: {2}",
            };
            foreach (PurchaseRefusal refusal in Enum.GetValues(typeof(PurchaseRefusal)))
                if (refusal != PurchaseRefusal.None) table[RefusalKey(refusal)] = RefusalEnglish(refusal);
            foreach (PlanMissCause miss in Enum.GetValues(typeof(PlanMissCause)))
                if (miss != PlanMissCause.None) table[MissKey(miss)] = MissEnglish(miss);
            return table;
        }

        /// <summary>Every key with its English text.</summary>
        public static IReadOnlyDictionary<string, string> All => Table;
        public static string English(string key) => Table.TryGetValue(key, out var english) ? english : key;
        public static string RefusalKey(PurchaseRefusal refusal) => Prefix + "refusal_" + refusal.ToString().ToLowerInvariant();
        public static string MissKey(PlanMissCause miss) => Prefix + "missed_" + miss.ToString().ToLowerInvariant();

        private static string RefusalEnglish(PurchaseRefusal refusal)
        {
            switch (refusal)
            {
                case PurchaseRefusal.Empty: return "nothing they wanted was on sale";
                case PurchaseRefusal.Ceiling: return "over their spending limit";
                case PurchaseRefusal.Wallet: return "not enough money";
                case PurchaseRefusal.Unsupported: return "the business serves its own customers";
                case PurchaseRefusal.Closed: return "the business was closed";
                case PurchaseRefusal.PlayerInside: return "you were inside";
                case PurchaseRefusal.VenueFull: return "the business was full this hour";
                case PurchaseRefusal.VenueNotReady: return "the business was not ready to serve";
                case PurchaseRefusal.AlreadyRecorded: return "this purchase was already recorded";
                case PurchaseRefusal.Dropped: return "they left without buying";
                case PurchaseRefusal.NativeDeclined: return "the checkout declined the sale";
                case PurchaseRefusal.Interrupted: return "the sale was interrupted";
                case PurchaseRefusal.Reversed: return "the sale was reversed";
                case PurchaseRefusal.Uncertain: return "the sale awaits confirmation";
                default: return refusal.ToString();
            }
        }

        private static string MissEnglish(PlanMissCause miss)
        {
            switch (miss)
            {
                case PlanMissCause.Closed: return "it was closed";
                case PlanMissCause.Late: return "they ran out of time";
                case PlanMissCause.NoRoute: return "they found no way there";
                case PlanMissCause.Refused: return "they were turned away";
                default: return miss.ToString();
            }
        }
    }

    public sealed partial class CitySimulator
    {
        private VenueFeedbackLog venueFeedback = new VenueFeedbackLog();
        /// <summary>GR-7: non-food visits, purchases and refusals observed since this simulator (or the one it replaced) started.</summary>
        public VenueFeedbackLog VenueFeedback => venueFeedback;

        /// <summary>A place's readable label for feedback lines (the inspector's address label), or "an unknown place".</summary>
        public string PlaceLabel(string placeId, Func<string, int, string>? formatAddress = null)
            => places.TryGetValue(placeId ?? "", out var place) ? ResidentAddress.From(place, formatAddress).Label : "an unknown place";

        /// <summary>Records the non-food items this event admitted or missed, compared on the same day's plan before commit.
        /// A purchase refusal is recorded by the purchase itself (<see cref="RecordPurchase"/>), once, with its own cause.</summary>
        private void RecordVisitFeedback(CitizenState live, CitizenState working)
        {
            var before = live.Plan; var after = working.Plan;
            if (after == null) return;
            bool same = before != null && before.Day == after.Day && before.Items.Count == after.Items.Count;
            for (int i = 0; i < after.Items.Count; i++)
            {
                var item = after.Items[i];
                if (item.Kind != PlanItemKind.Need && item.Kind != PlanItemKind.Leisure && item.Kind != PlanItemKind.Outing) continue;
                var now = after.StateOf(i);
                var was = same ? before!.StateOf(i) : PlanItemState.Pending;
                if (now == was) continue;
                if (now == PlanItemState.Active)
                    venueFeedback.Add(new VenueEvent(State.Minute, live.Identity.Id, VisitedVenue(working, item), VenueEventKind.Visit, PurchaseRefusal.None, PlanMissCause.None, Money.Zero));
                // A Refused miss here is a paid entry the resident could not afford, turned away before any purchase.
                else if (now == PlanItemState.Missed)
                    venueFeedback.Add(new VenueEvent(State.Minute, live.Identity.Id, item.VenueId, VenueEventKind.Missed, PurchaseRefusal.None, after.CauseOf(i), Money.Zero));
            }
        }

        // An optional alternative may replace the planned venue; the admission names where the person actually is.
        private static string VisitedVenue(CitizenState working, PlanItem item)
            => working.Phase == CitizenPhase.AtPlace && working.PlaceId.Length != 0 ? working.PlaceId : item.VenueId;

        private void RecordPurchase(CitizenState live, PurchaseResult result, string venueId)
        {
            if (result.IsBought)
                venueFeedback.Add(new VenueEvent(State.Minute, live.Identity.Id, venueId, VenueEventKind.Bought, PurchaseRefusal.None, PlanMissCause.None, result.Total));
            else if (result.Refusal != PurchaseRefusal.AlreadyRecorded && result.Refusal != PurchaseRefusal.Unsupported)
                venueFeedback.Add(new VenueEvent(State.Minute, live.Identity.Id, venueId, VenueEventKind.Refused, result.Refusal, PlanMissCause.None, Money.Zero));
        }
    }
}
