using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    // F5d (city audit F5 #92): writer 27, independent schedules. Every visitor carries its own routine: the writer-26
    // visitor tuple (portal, presence, Employment, carried income, LastArrival) plus one routine object in
    // VisitorRoutines, aligned by index. There is no migration: schema 26 and older are refused unchanged.
    public static partial class SaveCodec
    {
        public const int IndependentSchema = PersonTable.IndependentSchema;
        /// <summary>F5e: writer 28 keeps this shape; the bump marks cities seated under the workplace area bands.</summary>
        public const int WorkplaceCapacitySchema = PersonTable.WorkplaceCapacitySchema;
        public const int MaxRoutineTuple = 512;

        private const int RoutineFields27 = 16;
        private const string NoPlace27 = "---";

        /// <summary>Properties that exist only from schema 27; writer 26 and every frozen reader omit them.</summary>
        internal static bool PersonProperty27(Type type, string? name) => type == typeof(CityState) && name == "VisitorRoutines";

        // ---- Visitor routine object: {"J": journey or null, "T": "<routine tuple>"} ----
        // Identity, traits and travel preferences are derived from (seed, PersonId); the home is the stay's portal;
        // DayPlaceId/EmploymentStartDay, the carried source and the single arrival come from the visitor tuple. A
        // visitor has no car, recent-visit list or optional stop, so none of those fields exist on the wire.
        //
        // Tuple: "1|<phase><activity><destination activity>|<place>|<destination>|<next>|<until>|<departure>|<completed>
        //   |<missed>|<joined>|<last check day>|<job changes>|<shop><leisure>|<commute>|<outing>|<weather>"
        // places: three base-36 digits of the ordinal Places index, "---" for none; minutes: invariant round-trip
        // ("R") text, refused unless canonical; commute "-" or "<place>,<starts>,<ends>,<prepared>,<travel>";
        // outing "-" or "<origin><activity>,<resume until>,<resume event>,<0|1 returning>"; weather "-" or
        // "<boundary>,<commitment>".

        /// <summary>The routine's scalar facts, carried between the property's value provider and the context converter.</summary>
        internal sealed class RoutineTuple27
        {
            public RoutineTuple27(VisitorRoutine routine) { Routine = routine; }
            public readonly VisitorRoutine Routine;
        }

        private sealed class RoutineTupleProvider : IValueProvider
        {
            public object? GetValue(object target) => new RoutineTuple27((VisitorRoutine)target);
            public void SetValue(object target, object? value)
            {
                if (!(value is RoutineTuple27 tuple)) throw new SaveValidationException("Invalid visitor routine tuple.");
                ApplyRoutine27((VisitorRoutine)target, tuple.Routine);
            }
        }

        private static IList<JsonProperty> RoutineProperties27(IList<JsonProperty> inherited, int schema)
        {
            var journey = inherited.Single(p => p.PropertyName == "Journey");
            journey.PropertyName = "J";
            var result = new List<JsonProperty> { journey, new JsonProperty
            {
                PropertyName = "T", UnderlyingName = "T", PropertyType = typeof(RoutineTuple27), DeclaringType = typeof(VisitorRoutine),
                ValueProvider = new RoutineTupleProvider(), Readable = true, Writable = true, Required = Required.Always
            } };
            if (schema >= EconomicLedgerSchema) result.Add(inherited.Single(p => p.PropertyName == "Accrual"));
            return result;
        }

        private static void ApplyRoutine27(VisitorRoutine target, VisitorRoutine parsed)
        {
            target.Phase = parsed.Phase; target.Activity = parsed.Activity; target.DestinationActivity = parsed.DestinationActivity;
            target.PlaceId = parsed.PlaceId; target.DestinationId = parsed.DestinationId;
            target.NextEventMinute = parsed.NextEventMinute; target.ActivityUntilMinute = parsed.ActivityUntilMinute; target.DepartureMinute = parsed.DepartureMinute;
            target.CompletedJourneys = parsed.CompletedJourneys; target.MissedActivities = parsed.MissedActivities;
            target.JoinedMinute = parsed.JoinedMinute; target.LastEmploymentCheckDay = parsed.LastEmploymentCheckDay; target.JobChanges = parsed.JobChanges;
            target.ShopId = parsed.ShopId; target.LeisureId = parsed.LeisureId;
            target.CommutePreparation = parsed.CommutePreparation; target.MealOuting = parsed.MealOuting; target.W = parsed.W;
        }

        private sealed class RoutineTupleConverter27 : JsonConverter<RoutineTuple27>
        {
            private readonly WireContext context;
            public RoutineTupleConverter27(WireContext context) { this.context = context; }
            public override void WriteJson(JsonWriter writer, RoutineTuple27? value, JsonSerializer serializer)
                => writer.WriteValue(RoutineTuple27Text(value!.Routine, context));
            public override RoutineTuple27 ReadJson(JsonReader reader, Type objectType, RoutineTuple27? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Require(reader.TokenType == JsonToken.String, "Invalid visitor routine tuple.");
                return new RoutineTuple27(ParseRoutine27((string)reader.Value!, PlaceCells.Of(context)));
            }
        }

        private static string RoutineTuple27Text(VisitorRoutine r, WireContext context)
        {
            // E6c: a vendor place (a visitor at or walking to a stand) takes the "v" form; see PlaceCells.
            string Place(string id) => PlaceCell30(context, id) ?? throw new SaveValidationException("Visitor routine names an unknown place.");
            string Optional(string id) => id.Length == 0 ? NoPlace27 : Place(id);
            var text = new StringBuilder(160).Append('1').Append('|')
                .Append(Digit((int)r.Phase)).Append(Digit((int)r.Activity)).Append(Digit((int)r.DestinationActivity)).Append('|')
                .Append(Place(r.PlaceId)).Append('|').Append(Optional(r.DestinationId)).Append('|')
                .Append(R27(r.NextEventMinute)).Append('|').Append(R27(r.ActivityUntilMinute)).Append('|').Append(R27(r.DepartureMinute)).Append('|')
                .Append(I27(r.CompletedJourneys)).Append('|').Append(I27(r.MissedActivities)).Append('|')
                .Append(R27(r.JoinedMinute)).Append('|').Append(I27(r.LastEmploymentCheckDay)).Append('|').Append(I27(r.JobChanges)).Append('|')
                .Append(Optional(r.ShopId)).Append(Optional(r.LeisureId)).Append('|');
            var prep = r.CommutePreparation;
            if (prep.PlaceId.Length == 0)
            {
                Require(prep.WorkStartsAt == 0 && prep.WorkEndsAt == 0 && prep.PreparedAt == 0 && prep.TravelMinutes == 0, "Unplaced commute preparation.");
                text.Append('-');
            }
            else text.Append(Place(prep.PlaceId)).Append(',').Append(R27(prep.WorkStartsAt)).Append(',').Append(R27(prep.WorkEndsAt))
                .Append(',').Append(R27(prep.PreparedAt)).Append(',').Append(R27(prep.TravelMinutes));
            text.Append('|');
            var outing = r.MealOuting;
            if (outing == null) text.Append('-');
            else text.Append(Place(outing.OriginId)).Append(Digit((int)outing.OriginActivity)).Append(',').Append(R27(outing.ResumeUntil))
                .Append(',').Append(R27(outing.ResumeEvent)).Append(',').Append(outing.Returning ? '1' : '0');
            text.Append('|');
            if (r.W == null) text.Append('-');
            else text.Append(R27(r.W.Boundary)).Append(',').Append(r.W.ToWire());
            Require(text.Length <= MaxRoutineTuple, "Oversized visitor routine tuple.");
            return text.ToString();
        }

        private static char Digit(int value) { Require(value >= 0 && value <= 9, "Invalid routine code."); return (char)('0' + value); }
        private static string I27(int value) { Require(value >= 0, "Negative routine counter."); return value.ToString(CultureInfo.InvariantCulture); }
        private static string R27(double value)
        {
            Require(Position.Finite(value) && value >= 0, "Invalid routine minute.");
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double ParseR27(string text, string label)
        {
            double value = 0;
            Require(text.Length >= 1 && text.Length <= 32 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && Position.Finite(value) && value >= 0 && value.ToString("R", CultureInfo.InvariantCulture) == text, "Invalid " + label + ".");
            return value;
        }

        private static int ParseI27(string text, string label)
        {
            Require(text.Length >= 1 && text.Length <= 10 && (text[0] != '0' || text.Length == 1) && text.All(c => c >= '0' && c <= '9')
                && long.Parse(text, CultureInfo.InvariantCulture) <= int.MaxValue, "Invalid " + label + ".");
            return int.Parse(text, CultureInfo.InvariantCulture);
        }

        private static string ParsePlace27(string text, int offset, PlaceCells places, bool optional, string label)
        {
            Require(text.Length >= offset + 3, "Invalid " + label + ".");
            if (optional && string.CompareOrdinal(text, offset, NoPlace27, 0, 3) == 0) return "";
            // The streaming check has no place table; it keeps a placeholder that is never published.
            return places.Parse(text, offset, "Invalid " + label + ".");
        }

        private static T Code27<T>(char c, int maximum, string label)
        {
            Require(c >= '0' && c <= '0' + maximum, "Unknown " + label + ".");
            return (T)(object)(c - '0');
        }

        /// <summary>Structural parse (the streaming check passes no place table). Cross-record rules run in ValidatePersons27.</summary>
        private static VisitorRoutine ParseRoutine27(string text, PlaceCells places)
        {
            Require(text.Length <= MaxRoutineTuple, "Oversized visitor routine tuple.");
            var parts = text.Split('|');
            Require(parts.Length == RoutineFields27 && parts[0] == "1", "Invalid visitor routine tuple version or arity.");
            Require(parts[1].Length == 3, "Invalid visitor routine codes.");
            var r = new VisitorRoutine
            {
                Phase = Code27<CitizenPhase>(parts[1][0], (int)CitizenPhase.WaitingForOpening, "visitor phase"),
                Activity = Code27<Activity>(parts[1][1], (int)Activity.Study, "visitor activity"),
                DestinationActivity = Code27<Activity>(parts[1][2], (int)Activity.Study, "visitor destination activity"),
            };
            Require(parts[2].Length == 3 && parts[3].Length == 3, "Invalid visitor routine place.");
            r.PlaceId = ParsePlace27(parts[2], 0, places, false, "visitor place");
            r.DestinationId = ParsePlace27(parts[3], 0, places, true, "visitor destination");
            r.NextEventMinute = ParseR27(parts[4], "visitor next event");
            r.ActivityUntilMinute = ParseR27(parts[5], "visitor activity end");
            r.DepartureMinute = ParseR27(parts[6], "visitor departure");
            r.CompletedJourneys = ParseI27(parts[7], "visitor journeys");
            r.MissedActivities = ParseI27(parts[8], "visitor missed activities");
            r.JoinedMinute = ParseR27(parts[9], "visitor joined minute");
            r.LastEmploymentCheckDay = ParseI27(parts[10], "visitor employment check");
            r.JobChanges = ParseI27(parts[11], "visitor job changes");
            Require(parts[12].Length == 6, "Invalid visitor venues.");
            r.ShopId = ParsePlace27(parts[12], 0, places, true, "visitor shop");
            r.LeisureId = ParsePlace27(parts[12], 3, places, true, "visitor leisure venue");
            if (parts[13] != "-")
            {
                var prep = parts[13].Split(',');
                Require(prep.Length == 5 && prep[0].Length == 3, "Invalid visitor commute preparation.");
                r.CommutePreparation = new CommutePreparation { PlaceId = ParsePlace27(prep[0], 0, places, false, "visitor commute place"),
                    WorkStartsAt = ParseR27(prep[1], "commute start"), WorkEndsAt = ParseR27(prep[2], "commute end"),
                    PreparedAt = ParseR27(prep[3], "commute preparation"), TravelMinutes = ParseR27(prep[4], "commute duration") };
            }
            if (parts[14] != "-")
            {
                var outing = parts[14].Split(',');
                Require(outing.Length == 4 && outing[0].Length == 4 && (outing[3] == "0" || outing[3] == "1"), "Invalid visitor meal outing.");
                r.MealOuting = new MealOuting { OriginId = ParsePlace27(outing[0], 0, places, false, "visitor meal origin"),
                    OriginActivity = Code27<Activity>(outing[0][3], (int)Activity.Study, "visitor meal origin activity"),
                    ResumeUntil = ParseR27(outing[1], "meal resume end"), ResumeEvent = ParseR27(outing[2], "meal resume event"), Returning = outing[3] == "1" };
            }
            if (parts[15] != "-")
            {
                int comma = parts[15].IndexOf(',');
                Require(comma > 0, "Invalid visitor weather commitment.");
                var parsed = WeatherCommitment.Parse(parts[15].Substring(comma + 1));
                double boundary = ParseR27(parts[15].Substring(0, comma), "visitor weather boundary");
                Require(parsed.ToWire() == parts[15].Substring(comma + 1), "Noncanonical visitor weather commitment.");
                r.W = parsed.Queue ? new WeatherCommitment(boundary, parsed.Weather, parsed.Value) : new WeatherCommitment(boundary, parsed.Weather, parsed.Value == 1);
                Require(r.W.ToWire() == parsed.ToWire(), "Noncanonical visitor weather commitment.");
            }
            return r;
        }

        private static void CheckRoutine27(JsonReader reader, TokenText text, PlaceCells cells)
        {
            Require(reader.TokenType == JsonToken.String, "Invalid visitor routine tuple.");
            string value = (string)reader.Value!;
            Require(value.Length <= MaxRoutineTuple && text.LiteralAsciiString((IJsonLineInfo)reader, value), "Invalid visitor routine tuple encoding.");
            ParseRoutine27(value, cells);
        }

        /// <summary>After a schema-27 decode and before validation: the writer-26 person derivations for residents and
        /// portals, then each visitor routine is linked to its person and receives the facts its tuple carries.</summary>
        private static void DerivePersons27(CityState state)
        {
            Require(state.Citizens != null && state.Visitors != null && state.VisitorRoutines != null, "Missing person table.");
            foreach (var person in state.Citizens)
            {
                Require(person != null, "Null citizen.");
                bool employed = person.DayPlaceId != null && person.DayPlaceId.Length > 0;
                Require(employed == person.EmploymentStartDay >= 0, "Resident Employment differs from its day place.");
                if (!employed) person.EmploymentStartDay = 0;
            }
            Require(state.Portals != null, "Missing station table.");
            Require(state.Visitors.Count == state.VisitorRoutines.Count, "Every visitor has exactly one routine.");
            Require(state.SourceIdentities != null && (state.Visitors.Count == 0 || state.SourceIdentities.Count > 0), "Derived visitor identities need the source population.");
            for (int i = 0; i < state.Visitors.Count; i++)
            {
                var visitor = state.Visitors[i];
                Require(visitor?.Stay != null && state.VisitorRoutines[i] != null, "Missing visitor stay or routine.");
                string mark = visitor!.Stay.OriginPortalId;
                Require(mark.Length > 1 && mark[0] == PortalIndexMark26, "A schema-27 visitor arrives through a portal.");
                int index = int.Parse(mark.Substring(1), CultureInfo.InvariantCulture);
                Require(index < state.Portals!.Count, "Visitor portal index is outside the station table.");
                visitor.Stay.OriginPortalId = state.Portals[index];
            }
            // Identity, traits and preferences are derived in Core (contract B6), never decoded.
            try { VisitorSchedules.DeriveAll(state); }
            catch (InvalidOperationException e) { Fail(e.Message); }
            for (int i = 0; i < state.Visitors.Count; i++)
            {
                var visitor = state.Visitors[i]; var routine = state.VisitorRoutines[i];
                routine.HomeId = visitor.Stay.OriginPortalId;
                routine.DayPlaceId = visitor.Employment?.WorkplaceId ?? "";
                routine.EmploymentStartDay = visitor.Employment?.StartDay ?? 0;
                routine.Carried = visitor.CarriedIncomeSource;
                routine.RecentVisits = visitor.LastArrival == null ? new List<VisitRecord>()
                    : new List<VisitRecord> { new VisitRecord { AtMinute = visitor.LastArrival.AtMinute, PlaceId = visitor.LastArrival.PlaceId, Activity = visitor.LastArrival.Activity } };
            }
            var food = state.Food;
            if (food == null) { Require(state.Visitors.Count == 0, "Visitor persons require a food roster."); return; }
            food.RepresentationFactor = 1;
            Require(food.Residents != null, "Missing meal roster.");
            foreach (var meal in food.Residents)
            {
                Require(meal != null, "Null meal roster record.");
                meal.AnchorId = meal.ResidentId; meal.Slot = 0;
                meal.AssignHost(meal.ResidentId, 0);
            }
        }

        /// <summary>F5g: a sequence number below NextVisitorSequence without a visitor is one the fill skipped for a commuter,
        /// so its frozen derived identity must be a student or retiree (owner decision, 8 Nov 2026). A missing number with a
        /// working identity is a lost person and refuses the payload. A lost visitor whose identity is a student or retiree
        /// cannot be told from a skipped number, so the guarantee is weaker than the old count equality for those.</summary>
        private static void ValidateSkippedSequences(CityState state, List<int> present)
        {
            long gaps = (long)state.NextVisitorSequence - 1 - present.Count;
            if (gaps == 0) return;
            Require(gaps <= PopulationFillPolicy.MaximumSkippedSequences, "Visitor creation and active counts disagree.");
            Require(state.SourceIdentities.Count > 0 && VisitorSchedules.OnlyNonWorkingSkipped(state, present),
                "Visitor creation and active counts disagree: a skipped visitor sequence is not a student or retiree.");
        }

        /// <summary>Writer-27 person rules, on every read and before every write. Returns records charged.</summary>
        private static int ValidatePersons27(CityState state, Dictionary<string, Place> places, Dictionary<string, int> assigned, ref int embeddedRecords)
        {
            Require(state.Visitors != null && state.Portals != null && state.VisitorRoutines != null, "Missing person table.");
            Require((long)state.Citizens.Count + state.Visitors.Count <= MaxPersons, "Persons exceed the bound of " + MaxPersons + ".");
            Require(state.Citizens.Count <= MaxResidents, "Residents exceed the bound of " + MaxResidents + ".");
            Require(state.Visitors.Count <= MaxVisitors, "Visitors exceed the bound of " + MaxVisitors + ".");
            Require(state.Visitors.Count == state.VisitorRoutines.Count, "Every visitor has exactly one routine.");
            Require(state.IdentityPolicyVersion == IdentityPolicy.CurrentVersion, "Unknown identity policy version.");
            Require(state.Portals.Count <= MaxPortals, "Oversized station table.");
            for (int i = 0; i < state.Portals.Count; i++)
            {
                Text(state.Portals[i], "Station ID", MaxPortalCharacters);
                Require(i == 0 || string.CompareOrdinal(state.Portals[i - 1], state.Portals[i]) < 0, "Station table is unordered or duplicated.");
                Require(places.TryGetValue(state.Portals[i], out var portal) && PortalPlaces.Is(portal), "A station is not a portal place.");
            }
            foreach (var place in state.Places)
                if (place.EvidenceSource == PortalPlaces.Evidence)
                    Require(PortalPlaces.Is(place) && state.Portals.BinarySearch(place.Id, StringComparer.Ordinal) >= 0, "A portal place is missing from the station table.");
            int day = (int)(state.Minute / 1440);
            foreach (var person in state.Citizens)
            {
                if (person.DayPlaceId.Length == 0) continue;
                Require(person.EmploymentStartDay >= (int)(person.JoinedMinute / 1440) && person.EmploymentStartDay <= person.LastEmploymentCheckDay,
                    "Resident Employment start is outside its residence or employment checks.");
            }
            var residents = new HashSet<string>(state.Citizens.Select(c => c.Identity.Id), StringComparer.Ordinal);
            Require(state.Food != null || state.Visitors.Count == 0, "Visitor persons require a food roster.");
            int visitorSequences = 0;
            var sequenceNumbers = new List<int>(state.Visitors.Count);
            string? previous = null;
            for (int i = 0; i < state.Visitors.Count; i++)
            {
                var visitor = state.Visitors[i];
                var routine = state.VisitorRoutines[i];
                Require(visitor != null && visitor.Stay != null && routine != null, "Missing visitor stay or routine.");
                Text(visitor.Id, "Visitor ID", 256);
                // Schema 27 has no represented consumers: every visitor is a pv: person (contract B1).
                Require(PersonIds.Classify(visitor.Id) == PersonIdFormat.Visitor, "Invalid visitor ID.");
                Require(previous == null || string.CompareOrdinal(previous, visitor.Id) < 0, "Visitor persons are unordered or duplicated.");
                previous = visitor.Id;
                Require(!residents.Contains(visitor.Id), "Visitor duplicates a resident ID.");
                visitorSequences++;
                int sequenceNumber = int.Parse(visitor.Id.Substring(PersonIds.VisitorPrefix.Length), CultureInfo.InvariantCulture);
                Require(sequenceNumber < state.NextVisitorSequence, "Visitor ID is not yet allocated.");
                sequenceNumbers.Add(sequenceNumber);
                Require(ReferenceEquals(visitor.Routine, routine) && ReferenceEquals(routine.Person, visitor)
                    && routine.Identity != null && routine.Identity.Id == visitor.Id, "Visitor routine is not its person's.");
                var stay = visitor.Stay;
                Known(stay.Pattern, "visitor stay pattern"); Known(stay.Presence, "visitor presence");
                // F5g fills the day-visitor pool in the same tuple shape (pattern code 'D', already in the alphabet). A day
                // visitor never holds an Employment or a city workplace.
                Require(stay.Pattern == VisitorPattern.Commuter || visitor.Employment == null && routine.DayPlaceId.Length == 0,
                    "A day visitor holds no employment.");
                Require(stay.OriginPortalId.Length != 0 && state.Portals.BinarySearch(stay.OriginPortalId, StringComparer.Ordinal) >= 0, "Visitor portal is missing from the station table.");
                Require(routine.HomeId == stay.OriginPortalId, "A visitor's off-map home is its portal.");
                Require(stay.FirstDay >= 0 && stay.FirstDay <= day, "Invalid visitor first day.");
                bool dormant = VisitorSchedules.Dormant(routine);
                Require(dormant == (stay.Presence == VisitorPresence.Dormant), "Visitor presence differs from its routine.");
                if (dormant)
                {
                    Require(stay.NextStayStart == routine.NextEventMinute && stay.NextStayStart >= state.Minute, "A dormant visitor's next stay is its next event.");
                    Require(visitor.LastArrival == null && routine.RecentVisits.Count == 0, "A dormant visitor holds no arrival.");
                }
                else Require(BitConverter.DoubleToInt64Bits(stay.NextStayStart) == 0, "An active visitor has no next stay start.");
                // A visitor owns no car or traffic record (section 9 Q1 default).
                Require(routine.Car == null && !routine.Preferences.OwnsCar && routine.Preferences.AllowTransit, "A visitor owns no car.");
                Require(routine.Journey?.Legs == null || routine.Journey.Legs.All(l => l == null || l.Mode != TravelMode.Car), "A visitor journey has no car leg.");
                Require(routine.OptionalStop == null && routine.OptionalStopChoice == null, "A visitor takes no optional stop.");
                Require(routine.RecentVisits.Count <= 1, "A visitor keeps only its latest arrival.");
                var arrival = visitor.LastArrival;
                if (arrival != null)
                {
                    Minute(arrival.AtMinute, "Arrival minute");
                    Require(arrival.AtMinute <= state.Minute, "Arrival is in the future.");
                    Known(arrival.Activity, "arrival activity");
                    Require(places.TryGetValue(arrival.PlaceId, out var arrived) && (int)arrived.Kind == (int)arrival.Activity, "Unknown arrival place.");
                    var latest = routine.RecentVisits.Count == 0 ? null : routine.RecentVisits[0];
                    Require(latest != null && latest.AtMinute == arrival.AtMinute && latest.PlaceId == arrival.PlaceId && latest.Activity == arrival.Activity,
                        "A visitor's arrival differs from its routine.");
                }
                else Require(routine.RecentVisits.Count == 0, "A visitor's arrival differs from its routine.");
                var persona = routine.Identity.Persona;
                var job = visitor.Employment;
                Require((job == null) == (routine.DayPlaceId.Length == 0), "Visitor Employment differs from its routine.");
                if (job != null)
                {
                    Require(persona != Persona.Retiree, "A retiree cannot have a day-place assignment.");
                    var kind = persona == Persona.Student ? EmploymentKind.Study : EmploymentKind.Work;
                    Require(job.PersonId == visitor.Id && job.IsActive && job.Kind == kind && job.WorkplaceId == routine.DayPlaceId
                        && job.StartDay == routine.EmploymentStartDay && job.ScheduleCode == PersonTable.PersonaScheduleCode
                        && job.NativeEmployment == NativeEmployment.None, "Invalid visitor employment.");
                    Reference(places, job.WorkplaceId, kind == EmploymentKind.Study ? PlaceKind.Study : PlaceKind.Work);
                    Require(job.StartDay >= stay.FirstDay && job.StartDay <= day && job.StartDay <= routine.LastEmploymentCheckDay, "Invalid visitor employment start.");
                    int count = assigned.TryGetValue(job.WorkplaceId, out int used) ? used + 1 : 1;
                    if (count > places[job.WorkplaceId].Capacity) Fail("Place assignment exceeds capacity: " + job.WorkplaceId);
                    assigned[job.WorkplaceId] = count;
                }
                if (visitor.CarriedIncomeSource.HasValue)
                {
                    Known(visitor.CarriedIncomeSource.Value, "carried income source");
                    Require(job == null && visitor.CarriedIncomeSource == routine.Carried, "A carried income source applies only without own employment.");
                }
                else Require(routine.Carried == null, "A carried income source applies only without own employment.");
                Require(routine.JoinedMinute <= state.Minute && routine.LastEmploymentCheckDay >= (int)(routine.JoinedMinute / 1440) && routine.LastEmploymentCheckDay <= day + 1,
                    "Invalid visitor lifecycle clock.");
                if (routine.W != null)
                {
                    Minute(routine.W.Boundary, "Weather boundary"); Dense((int)routine.W.Weather, 2, "visitor weather");
                    Require(routine.W.Queue ? routine.W.Value >= 2 && routine.W.Value <= 10 : routine.W.Value == 0 || routine.W.Value == 1, "Invalid weather commitment value.");
                    Require(routine.W.Queue || routine.W.Weather == WeatherSample.Rain || routine.W.Value == 1, "Nonneutral dry/unknown outing capture.");
                    Require(routine.W.Queue ? routine.W.Boundary <= routine.ActivityUntilMinute : routine.W.Boundary <= state.Minute, "Invalid visitor weather boundary.");
                }
                Traits(routine.Traits, "visitor");
                ValidateCommutePreparation(routine, places, state.Minute);
                OptionalReference(places, routine.ShopId, PlaceKind.Shop);
                OptionalReference(places, routine.LeisureId, PlaceKind.Leisure);
                ValidateSchedule(routine, state, places, ref embeddedRecords);
                if (routine.MealOuting != null) Require(state.Food != null, "Meal outing without economy.");
                Require(embeddedRecords <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
            }
            Require(state.NextVisitorSequence >= 1 && state.NextVisitorSequence - 1 >= visitorSequences, "Visitor creation and active counts disagree.");
            ValidateSkippedSequences(state, sequenceNumbers);
            ValidateOwnership27(state);
            // One record per person tuple (resident P and visitor) and per station entry, as the streaming check charges.
            return state.Citizens.Count + state.Visitors.Count + state.Portals.Count;
        }

        /// <summary>The F5b ownership contract over the writer-27 person table. Resident IDs stay opaque (contract B1), so
        /// a resident ID outside the c&lt;n&gt;/pc:&lt;n&gt; formats gives one tolerated violation; every visitor has a portal.</summary>
        private static void ValidateOwnership27(CityState state)
        {
            var food = state.Food;
            var persons = new List<PersonFacts>(state.Citizens.Count + state.Visitors.Count);
            int tolerated = 0;
            var employments = new List<Employment>();
            foreach (var resident in state.Citizens)
            {
                persons.Add(new PersonFacts(resident.Identity.Id, PersonKind.Resident, resident.HomeId, null, resident.Car != null));
                if (!PersonIds.FitsKind(PersonIds.Classify(resident.Identity.Id), PersonKind.Resident)) tolerated++;
                var job = PersonTable.ResidentEmployment(resident);
                if (job != null) employments.Add(job);
            }
            foreach (var visitor in state.Visitors)
            {
                persons.Add(new PersonFacts(visitor.Id, PersonKind.Visitor, "", visitor.Stay, visitor.Routine?.Car != null));
                if (visitor.Employment != null) employments.Add(visitor.Employment);
            }
            persons.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var input = new OwnershipInput
            {
                Persons = persons, Employments = employments, PortalIds = state.Portals,
                HomePlaceIds = state.Places.Where(p => p.Kind == PlaceKind.Home && !PortalPlaces.Is(p)).Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                Workplaces = state.Places.Where(p => p.Kind == PlaceKind.Work || p.Kind == PlaceKind.Study)
                    .OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new WorkplaceFacts(p.Id, p.Kind)).ToList(),
                FoodEnabled = food != null, FinancesEnabled = food != null && food.HouseholdFinanceVersion != 0,
            };
            if (food != null)
            {
                input.Roster = food.Residents.Select(m => new RosterFacts(m.ResidentId, m.AnchorId, m.Slot)).ToList();
                input.Wallets = food.Residents.Where(m => m.Budget != null).Select(m => new WalletFacts(MoneyOwnerKind.Person, m.ResidentId)).ToList();
                input.Intents = food.Residents.Select(m => new DemandUnitFacts(m.IntentId, m.ResidentId)).ToList();
            }
            var validator = new OwnershipValidator();
            if (validator.Validate(input) || validator.ViolationCount == tolerated
                && validator.Violations.All(v => v.Rule == OwnershipRule.PersonIdFormat)) return;
            var shown = validator.Violations.FirstOrDefault(v => v.Rule != OwnershipRule.PersonIdFormat);
            Fail("Ownership contract violated: " + (shown.Subject == null ? validator.ViolationCount + " violations" : shown.ToString()) + ".");
        }
    }
}
