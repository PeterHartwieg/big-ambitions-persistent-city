using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed class FoodActivitySpan
    {
        public string ConsumerId { get; set; } = "";
        public string PlaceId { get; set; } = "";
        public Activity Activity { get; set; }
        public double From { get; set; }
        public double Until { get; set; }
    }

    public sealed class FoodRoutineResident
    {
        public CitizenIdentity Identity { get; set; } = new CitizenIdentity();
        public string HomeId { get; set; } = "";
        public string HomeDistrictId { get; set; } = "";
        public double WalkingToleranceMetres { get; set; }
    }

    public sealed class FoodRoutineCapture
    {
        public string CityKey { get; set; } = "";
        public int Seed { get; set; }
        public double StartedAt { get; set; }
        public double EndsAt { get; set; }
        public List<FoodRoutineResident> Residents { get; set; } = new List<FoodRoutineResident>();
        public List<FoodActivitySpan> Spans { get; set; } = new List<FoodActivitySpan>();
        public List<FoodDistrictBudgetMix> HomeDistrictMixes { get; set; } = new List<FoodDistrictBudgetMix>();
    }

    /// <summary>
    /// Records completed presence intervals from the real population event engine. Travel,
    /// opening waits and failed attendance supply no presence. Does not schedule or move anyone.
    /// Use for an explicitly bounded diagnostic run; disposal unsubscribes without changing state.
    /// </summary>
    public sealed class FoodRoutineRecorder : IDisposable
    {
        private readonly CitySimulator simulation;
        private readonly FoodRoutineCapture capture;
        private readonly HashSet<string> cohort;
        private readonly Dictionary<string, FoodActivitySpan> open = new Dictionary<string, FoodActivitySpan>(StringComparer.Ordinal);
        private readonly Dictionary<string, FoodActivitySpan> last = new Dictionary<string, FoodActivitySpan>(StringComparer.Ordinal);
        private bool disposed, truncated;
        private double observedUntil;
        public const int MaximumSpans = 200000;

        public FoodRoutineRecorder(CitySimulator simulation, IReadOnlyList<FoodDistrictBudgetMix>? homeDistrictMixes = null)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            if (simulation.State.Citizens.Count > 5000) throw new ArgumentException("Food observation is limited to 5,000 residents.");
            capture = new FoodRoutineCapture { CityKey = simulation.State.PopulationKey, Seed = simulation.State.Seed,
                StartedAt = simulation.State.Minute, EndsAt = simulation.State.Minute };
            var mixes = homeDistrictMixes ?? Array.Empty<FoodDistrictBudgetMix>();
            FoodHouseholdPolicy.ValidateMixes(mixes);
            capture.HomeDistrictMixes = mixes.Select(m => new FoodDistrictBudgetMix { DistrictId = m.DistrictId,
                WorkingPercent = m.WorkingPercent, MiddlePercent = m.MiddlePercent, UpperPercent = m.UpperPercent, Evidence = m.Evidence }).ToList();
            var homes = simulation.State.Places.Where(p => p.Kind == PlaceKind.Home).ToDictionary(p => p.Id, StringComparer.Ordinal);
            observedUntil = capture.StartedAt;
            foreach (var person in simulation.State.Citizens.OrderBy(p => p.Identity.Id, StringComparer.Ordinal))
                capture.Residents.Add(new FoodRoutineResident { Identity = Population.CloneIdentity(person.Identity), HomeId = person.HomeId,
                    HomeDistrictId = homes.TryGetValue(person.HomeId, out var home) ? home.District : "",
                    WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres });
            cohort = new HashSet<string>(capture.Residents.Select(p => p.Identity.Id), StringComparer.Ordinal);
            foreach (var person in simulation.State.Citizens) Observe(person.Identity.Id, person, simulation.State.Minute);
            simulation.ActivityObserved += Observe;
        }

        private void Observe(string priorId, CitizenState person, double minute)
        {
            if (disposed || truncated) return;
            if (minute < observedUntil || minute - capture.StartedAt > 8 * 1440)
            { truncated = true; open.Clear(); return; }
            observedUntil = minute;
            Close(priorId, minute);
            // Replacement residents do not inherit a departed resident's needs or identity.
            if (cohort.Contains(person.Identity.Id) && person.Phase == CitizenPhase.AtPlace && person.PlaceId.Length > 0)
                open[person.Identity.Id] = new FoodActivitySpan { ConsumerId = person.Identity.Id, PlaceId = person.PlaceId,
                    Activity = person.Activity, From = minute };
        }

        private void Close(string id, double minute)
        {
            if (!open.TryGetValue(id, out var span)) return;
            open.Remove(id);
            if (minute <= span.From) return;
            span.Until = minute;
            if (last.TryGetValue(id, out var previous) && previous.Until == span.From
                && previous.PlaceId == span.PlaceId && previous.Activity == span.Activity)
                previous.Until = minute;
            else
            {
                if (capture.Spans.Count >= MaximumSpans) { truncated = true; return; }
                capture.Spans.Add(span); last[id] = span;
            }
        }

        public FoodRoutineCapture Finish()
        {
            if (disposed) throw new InvalidOperationException("Recorder already finished or disposed.");
            double until = simulation.State.Minute;
            foreach (var id in open.Keys.ToArray()) Close(id, until);
            Dispose();
            if (truncated || until < observedUntil || until - capture.StartedAt > 8 * 1440)
                throw new InvalidOperationException("Incomplete, rewound or oversized routine observation; retain the population state.");
            capture.EndsAt = until;
            capture.Spans = capture.Spans.OrderBy(s => s.From).ThenBy(s => s.ConsumerId, StringComparer.Ordinal).ToList();
            return capture;
        }

        public void Dispose()
        {
            if (disposed) return;
            simulation.ActivityObserved -= Observe;
            disposed = true; open.Clear();
        }
    }

    public sealed class FoodRoutinePlan
    {
        public List<FoodConsumer> Consumers { get; } = new List<FoodConsumer>();
        public List<FoodIntent> Opportunities { get; } = new List<FoodIntent>();
    }

    public static class FoodRoutinePlanning
    {
        /// <summary>Provisional break flexibility, not a native employment contract. Stable
        /// per resident: 25% have 45 minutes, 60% an hour, 15% 75 minutes. The caller always
        /// clips this allowance to actual presence and the next commitment.</summary>
        public static int BreakMinutes(string residentId, int seed, Activity activity)
        {
            if (activity != Activity.Work && activity != Activity.Study) return 75;
            uint flexibility = StableRandom.Hash(residentId + ":food-break-flexibility", seed) % 100;
            return flexibility < 25 ? 45 : flexibility < 85 ? 60 : 75;
        }
        public static double DesiredMealMinute(CitizenState person, int seed, int day)
        {
            string id = person.Identity.Id;
            string key = id + ":food-day:" + day.ToString(CultureInfo.InvariantCulture);
            double wake = DailyRhythm.OnDay(person, seed, day - 1).EndsAt;
            double desired = wake + 240 + (int)(StableRandom.Hash(id + ":meal-rhythm", seed) % 61) - 30
                + (int)(StableRandom.Hash(key, seed) % 41) - 20;
            return Math.Max(day * 1440.0, Math.Min(day * 1440.0 + 1200, desired));
        }

        /// <summary>
        /// One purchased midday meal opportunity, not three invented restaurant meals or a nutrition model.
        /// Wake-related appetite varies modestly by resident/day. Actual presence supplies origins and
        /// departure deadlines. Work/study breaks are explicit provisional policy, not observed rosters.
        /// </summary>
        public static FoodRoutinePlan Build(FoodRoutineCapture capture, int firstDay, int days, int? dailyAllowanceCents = null,
            bool allowOutsideOptions = true)
        {
            if (capture == null || days < 1 || days > 7 || firstDay < 0 || firstDay > 100000
                || dailyAllowanceCents < 0 || dailyAllowanceCents > 1000000
                || !Position.Finite(capture.StartedAt) || !Position.Finite(capture.EndsAt)
                || capture.StartedAt > firstDay * 1440.0 || capture.EndsAt < (firstDay + days) * 1440.0)
                throw new ArgumentException("A complete bounded observation is required for every requested day.");
            if (capture.Residents.Count < 1 || capture.Residents.Count > 5000 || capture.Spans.Count > FoodRoutineRecorder.MaximumSpans
                || capture.Residents.Select(p => p.Identity.Id).Distinct(StringComparer.Ordinal).Count() != capture.Residents.Count)
                throw new ArgumentException("Invalid observed cohort.");
            var result = new FoodRoutinePlan();
            FoodHouseholdPolicy.ValidateMixes(capture.HomeDistrictMixes);
            var mixes = capture.HomeDistrictMixes.ToDictionary(m => m.DistrictId, StringComparer.Ordinal);
            var indexed = capture.Spans.GroupBy(s => s.ConsumerId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.From).ToArray(), StringComparer.Ordinal);
            foreach (var resident in capture.Residents.OrderBy(p => p.Identity.Id, StringComparer.Ordinal))
            {
                string id = resident.Identity.Id;
                var rhythm = new CitizenState { Identity = resident.Identity };
                if (resident.HomeDistrictId == null || resident.HomeDistrictId.Length > 1024 || !Enum.IsDefined(typeof(Persona), resident.Identity.Persona))
                    throw new ArgumentException("Invalid resident home district/personality.");
                var consumer = new FoodConsumer { Id = id, Name = resident.Identity.Name, OriginPlaceId = resident.HomeId,
                    ValueOfTimeCentsPerMinute = 4 + (int)(StableRandom.Hash(id + ":food-time", capture.Seed) % 9),
                    ServiceQualityValueCents = 100 + (int)(StableRandom.Hash(id + ":food-quality", capture.Seed) % 301),
                    DistrictAppealValueCents = 50 + (int)(StableRandom.Hash(id + ":food-district", capture.Seed) % 151) };
                mixes.TryGetValue(resident.HomeDistrictId, out var mix);
                FoodHouseholdPolicy.SetAllowance(consumer, resident, capture.Seed, mix, dailyAllowanceCents);
                result.Consumers.Add(consumer);
                if (!indexed.TryGetValue(id, out var spans)) spans = Array.Empty<FoodActivitySpan>();
                double previousEnd = capture.StartedAt;
                foreach (var span in spans)
                {
                    if (string.IsNullOrWhiteSpace(span.PlaceId) || !Enum.IsDefined(typeof(Activity), span.Activity)
                        || !Position.Finite(span.From) || !Position.Finite(span.Until) || span.From < previousEnd
                        || span.Until <= span.From || span.Until > capture.EndsAt)
                        throw new ArgumentException("Overlapping or invalid observed presence intervals.");
                    previousEnd = span.Until;
                }
                for (int day = firstDay; day < firstDay + days; day++)
                {
                    double desired = DesiredMealMinute(rhythm, capture.Seed, day);
                    double expires = Math.Min((day + 1) * 1440.0, desired + 240);
                    var intent = new FoodIntent { Id = FoodDemand.IntentId(id, day), ConsumerId = id, Day = day,
                        DesiredAt = desired, RequestedAt = desired, Deadline = expires, EatingMinutes = 15,
                        ContextEvidence = "No eligible observed stop before the meal window expired; purchased-meal opportunity unresolved, not a hunger measurement" };
                    foreach (var span in spans)
                    {
                        if (span.Until <= desired || span.From >= expires) continue;
                        double at = Math.Max(desired, span.From);
                        bool working = span.Activity == Activity.Work || span.Activity == Activity.Study;
                        int available = BreakMinutes(id, capture.Seed, span.Activity);
                        double end = Math.Min(expires, Math.Min(span.Until, at + available));
                        if (end - at < intent.EatingMinutes + 10) continue;
                        intent.OriginPlaceId = span.PlaceId; intent.OriginActivity = span.Activity;
                        intent.RequestedAt = at; intent.Deadline = end;
                        intent.MaximumQueueMinutes = (working ? 10 : 15) + (int)(StableRandom.Hash(id + ":queue-patience", capture.Seed) % 6);
                        intent.TimeCostMultiplier = 1 + Math.Min(1, (at - desired) / 120.0);
                        intent.ContextEvidence = working
                            ? "Observed work/study presence; provisional " + available.ToString(CultureInfo.InvariantCulture) + "-minute lunch break, including travel, service, eating and return"
                            : "Observed " + span.Activity + " presence; at most 75 minutes, ending before the observed next departure";
                        break;
                    }
                    if (allowOutsideOptions) FoodHouseholdPolicy.SetOutsideOption(consumer, resident, intent, capture.Seed);
                    result.Opportunities.Add(intent);
                }
            }
            var ids = new HashSet<string>(result.Consumers.Select(p => p.Id), StringComparer.Ordinal);
            if (capture.Spans.Any(s => !ids.Contains(s.ConsumerId))) throw new ArgumentException("Presence belongs to an unknown resident.");
            return result;
        }
    }
}
