using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public enum SaveReadStatus { Missing, Loaded, Invalid, Future }

    public sealed class SaveReadResult
    {
        internal SaveReadResult(SaveReadStatus status, CityState? state, string error, int sourceSchema = 0)
        { Status = status; State = state; Error = error; SourceSchema = sourceSchema; }
        public SaveReadStatus Status { get; }
        public CityState? State { get; }
        public string Error { get; }
        /// <summary>Original loaded or future wire schema; zero for missing or invalid state.</summary>
        public int SourceSchema { get; }
    }

    public sealed class SaveValidationException : Exception
    {
        public SaveValidationException(string message) : base(message) { }
    }

    /// <summary>Strict current-development codec. Unsupported and failed reads never replace a city.</summary>
    public static class SaveCodec
    {
        public const string StateKey = "persistentcity:state_v1";
        // Storage and nested-record limits are separate from the identity ceiling;
        // not every individual maximum can be combined into one valid payload.
        public const int MaxInputCharacters = 256 * 1024 * 1024;
        public const int MaxCitizens = 50000;
        public const int MaxEmbeddedRecords = 4000000;
        public const int MaxPlaces = 20000;
        public const int MaxRouteLegs = 512;
        public const int MaxRecentVisits = 64;
        public const int MaxOpeningWindows = 64;
        public const int MaxTrafficLinks = 250000;
        public const int MaxTrafficLinkCapacity = 1024;
        private const double MaxMinute = 1000000000;
        private const double MaxCoordinate = 10000000;

        public static string Write(CityState state)
        {
            if (state == null) throw new SaveValidationException("State is null.");
            state.PrepareForSave();
            Validate(state);
            var json = JsonConvert.SerializeObject(state, Formatting.None, Settings());
            Require(json.Length <= MaxInputCharacters, "Serialized state exceeds the input size limit.");
            return json;
        }

        /// <summary>Run the save contract without retaining the serialized activation payload.</summary>
        public static void ValidateForActivation(CityState state)
        {
            if (state == null) throw new SaveValidationException("State is null.");
            state.PrepareForSave();
            Validate(state);
            using var count = new CharacterCountingWriter();
            var serializer = JsonSerializer.CreateDefault(Settings());
            // Match JsonConvert.SerializeObject(state, Formatting.None, Settings()), including
            // application-wide defaults that the explicit save settings do not override.
            serializer.Formatting = Formatting.None;
            using (var writer = new JsonTextWriter(count)) serializer.Serialize(writer, state);
            Require(count.Count <= MaxInputCharacters, "Serialized state exceeds the input size limit.");
        }

        // Count UTF-16 characters, just like string.Length, including JSON escapes.
        // Finish serialization before checking the bound to preserve serialization failures.
        private sealed class CharacterCountingWriter : TextWriter
        {
            public override System.Text.Encoding Encoding => System.Text.Encoding.Unicode;
            public override IFormatProvider FormatProvider => CultureInfo.InvariantCulture;
            public long Count { get; private set; }
            public override void Write(char value) => Count++;
            public override void Write(string? value) => Count += value?.Length ?? 0;
            public override void Write(char[] buffer, int index, int count) => Count += count;
        }

        public static SaveReadResult Read(string? source)
        {
            if (source == null) return new SaveReadResult(SaveReadStatus.Missing, null, "");
            if (source.Length > MaxInputCharacters) return Invalid("State exceeds the input size limit.");
            try
            {
                int schema = ReadSchema(source);
                if (schema > CityState.CurrentSchema) return new SaveReadResult(SaveReadStatus.Future, null, "State uses a newer schema; preserve the original payload.", sourceSchema: schema);
                Require(schema == CityState.CurrentSchema, "Unsupported experimental state schema; preserve the original payload and use a new development city.");
                var serializer = JsonSerializer.Create(Settings());
                using (var shape = Reader(source))
                {
                    Require(shape.Read(), "Missing state.");
                    int records = 0;
                    CheckShape(shape, typeof(CityState), serializer.ContractResolver, ref records);
                    Require(!shape.Read(), "State contains trailing JSON content.");
                }
                using var reader = Reader(source);
                var state = serializer.Deserialize<CityState>(reader);
                Require(state != null, "State is null.");
                Validate(state!);
                return new SaveReadResult(SaveReadStatus.Loaded, state, "", sourceSchema: schema);
            }
            catch (Exception exception) when (exception is JsonException || exception is SaveValidationException || exception is OverflowException || exception is FormatException || exception is ArgumentException)
            { return Invalid("Invalid state: " + exception.Message); }
        }

        private static SaveReadResult Invalid(string error) => new SaveReadResult(SaveReadStatus.Invalid, null, error);

        // Json.NET otherwise coerces strings to numbers and rounds floating-point integers.
        // A supported payload has one exact wire shape; corruption must not silently change meaning.
        private static JsonTextReader Reader(string source) => new JsonTextReader(new StringReader(source))
        { MaxDepth = 48, DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double };

        private static int ReadSchema(string source)
        {
            using var reader = Reader(source);
            Require(reader.Read() && reader.TokenType == JsonToken.StartObject, "State must be a JSON object.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            int? schema = null;
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                Require(reader.TokenType == JsonToken.PropertyName, "Expected a field.");
                string name = (string)reader.Value!;
                Require(names.Add(name), "Duplicate field: " + name);
                Require(reader.Read(), "Missing field value.");
                if (name == "Schema")
                {
                    Require(reader.TokenType == JsonToken.Integer, "Invalid state schema.");
                    Require(int.TryParse(Convert.ToString(reader.Value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value), "Invalid state schema.");
                    schema = value;
                }
                reader.Skip();
            }
            Require(reader.TokenType == JsonToken.EndObject && !reader.Read(), "Incomplete or trailing JSON content.");
            Require(schema.HasValue, "Missing state schema.");
            return schema!.Value;
        }

        // Validate directly from tokens before deserialization: no full payload DOM, no coercion,
        // and bounded collection allocation even for hostile arrays of otherwise valid records.
        private static void CheckShape(JsonReader reader, Type type, IContractResolver resolver, ref int records)
        {
            var token = reader.TokenType;
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (token == JsonToken.Null) { Require(nullable != null || NullableRecord(type), "Unexpected null value."); return; }
            if (nullable != null) type = nullable;
            if (type == typeof(string)) { Require(token == JsonToken.String, "Expected a string."); Require(((string)reader.Value!).Length <= 512, "Oversized string."); return; }
            if (type == typeof(bool)) { Require(token == JsonToken.Boolean, "Expected a boolean."); return; }
            if (type == typeof(int) || type.IsEnum) { Require(token == JsonToken.Integer, "Expected an integer."); Require(int.TryParse(Convert.ToString(reader.Value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "Integer is out of range."); return; }
            if (type == typeof(long)) { Require(token == JsonToken.Integer, "Expected an integer."); Require(long.TryParse(Convert.ToString(reader.Value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "Integer is out of range."); return; }
            if (type == typeof(double)) { Require(token == JsonToken.Integer || token == JsonToken.Float, "Expected a number."); return; }
            if (type == typeof(Position))
            {
                Require(token == JsonToken.StartObject, "Expected a position object.");
                var components = new HashSet<string>(StringComparer.Ordinal);
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    Require(reader.TokenType == JsonToken.PropertyName, "Expected position field.");
                    string name = (string)reader.Value!;
                    Require((name == "X" || name == "Y" || name == "Z") && components.Add(name), "Unknown or duplicate position field.");
                    Require(reader.Read() && (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float), "Invalid position number.");
                }
                Require(reader.TokenType == JsonToken.EndObject && components.Count == 3, "Missing position field.");
                return;
            }
            var contract = resolver.ResolveContract(type);
            if (contract is JsonArrayContract array)
            {
                Require(token == JsonToken.StartArray, "Expected an array.");
                var item = array.CollectionItemType!;
                int limit = item == typeof(FoodProductSales) ? 4 : item == typeof(FoodBuyerDistrict) ? 100 : item == typeof(long) ? 24 : item == typeof(FoodShopDay) ? 14 * MaxPlaces : item == typeof(ResidentMeal) ? MaxCitizens * 3 : item == typeof(FoodScope) ? 1024
                    : item == typeof(FoodRoutineStop) ? MaxCitizens
                    : item == typeof(FoodDistrictBudgetMix) ? 100 : item == typeof(CitizenState) ? MaxCitizens
                    : item == typeof(Place) ? MaxPlaces : item == typeof(CitizenIdentity) ? 5000
                    : item == typeof(RouteLeg) ? MaxRouteLegs : item == typeof(VisitRecord) ? MaxRecentVisits
                    : item == typeof(TrafficRouteLeg) ? MaxRouteLegs : item == typeof(TrafficVehicleState) ? MaxCitizens
                    : item == typeof(TrafficLinkDefinition) || item == typeof(TrafficLinkState) || item == typeof(TrafficEventState) || item == typeof(int) ? MaxTrafficLinks
                    : item == typeof(string) ? MaxCitizens
                    : item == typeof(OpeningWindow) ? MaxOpeningWindows : MaxEmbeddedRecords;
                int count = 0;
                while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                {
                    Require(++count <= limit, "Oversized collection.");
                    if (item == typeof(RouteLeg) || item == typeof(VisitRecord) || item == typeof(OpeningWindow)
                        || item == typeof(TrafficRouteLeg) || item == typeof(TrafficVehicleState) || item == typeof(TrafficLinkDefinition)
                        || item == typeof(TrafficLinkState) || item == typeof(TrafficEventState) || item == typeof(int) || item == typeof(string))
                        Require(++records <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
                    CheckShape(reader, item, resolver, ref records);
                }
                Require(reader.TokenType == JsonToken.EndArray, "Incomplete array."); return;
            }
            Require(token == JsonToken.StartObject && contract is JsonObjectContract, "Expected an object.");
            var properties = ((JsonObjectContract)contract).Properties;
            var names = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                Require(reader.TokenType == JsonToken.PropertyName, "Expected a field.");
                string name = (string)reader.Value!;
                Require(names.Add(name), "Duplicate field: " + name);
                var property = properties.GetProperty(name, StringComparison.Ordinal);
                Require(property != null, "Unknown field: " + name);
                Require(reader.Read(), "Missing field value.");
                CheckShape(reader, property.PropertyType!, resolver, ref records);
            }
            Require(reader.TokenType == JsonToken.EndObject && names.Count == properties.Count, "Missing object fields.");
        }
        private static bool NullableRecord(Type type) => type == typeof(Route) || type == typeof(CarState)
            || type == typeof(TrafficKernelState) || type == typeof(TrafficSignalSchedule)
            || type == typeof(FoodCommerceState) || type == typeof(MealOuting) || type == typeof(MealExcursion) || type == typeof(ResidentBudget);
        // Contracts depend only on the sealed save DTO types, never a city or serializer setting.
        // DefaultContractResolver owns a thread-safe metadata cache; settings/serializer instances
        // remain per operation, including application-wide JsonConvert defaults.
        private static readonly IContractResolver StateContracts = new StateContractResolver();
        private static JsonSerializerSettings Settings() => new JsonSerializerSettings
        {
            ContractResolver = StateContracts,
            Converters = { new PositionConverter() },
            Culture = CultureInfo.InvariantCulture,
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 48,
            FloatParseHandling = FloatParseHandling.Double
        };

        private static void Validate(CityState state)
        {
            Require(state != null, "State is null.");
            Require(state.Schema == CityState.CurrentSchema, "Unsupported state schema.");
            ValidateFood(state);
            Require(state.TravelTimingVersion == 0 || state.TravelTimingVersion == 1, "Invalid travel timing version.");
            Bounded(state.TravelClockRate, 0.01, 1000, "Travel clock rate");
            Text(state.PopulationKey, "Population key", 256);
            Text(state.RloVersion, "RLO version", 128);
            Minute(state.Minute, "City minute");
            Require(state.Places != null && state.Places.Count > 0 && state.Places.Count <= MaxPlaces, "Invalid place count.");
            Require(state.Citizens != null && state.Citizens.Count > 0 && state.Citizens.Count <= MaxCitizens, "Invalid citizen count.");
            var places = new Dictionary<string, Place>(StringComparer.Ordinal);
            int embeddedRecords = 0;
            foreach (var place in state.Places!)
            {
                Require(place != null, "Null place.");
                Text(place.Id, "Place ID", 256); Text(place.Name, "Place name", 512); Text(place.District, "Place district", 256, true);
                Require(!places.ContainsKey(place.Id), "Duplicate place ID: " + place.Id);
                places.Add(place.Id, place); Known(place.Kind, "Place kind"); Point(place.Position);
                Bounded(place.AttractionWeight, 0.01, 100, "Attraction weight");
                Require(place.NativeTrafficIndex >= -1 && place.NativeTrafficIndex <= 1000000, "Invalid native traffic index.");
                Text(place.EvidenceSource, "Place evidence source", 256);
                Bounded(place.ResidentialAreaSquareMetres, 0, 1000000000, "Residential area");
                Require(place.Capacity >= 0 && place.Capacity <= 1000000, "Invalid place capacity.");
                Require(place.OpenMinute >= 0 && place.OpenMinute < 1440 && place.CloseMinute >= 0 && place.CloseMinute <= 1440, "Invalid opening hours.");
                Require(place.WeeklyHours != null && place.WeeklyHours.Count <= MaxOpeningWindows, "Invalid weekly opening window count.");
                embeddedRecords += place.WeeklyHours.Count;
                Require(embeddedRecords <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
                foreach (var window in place.WeeklyHours)
                {
                    Require(window != null, "Null weekly opening window.");
                    Require(window.Day >= 0 && window.Day <= 6, "Unknown weekly opening day.");
                    Require(window.FromMinute >= 0 && window.FromMinute <= 1440 && window.ToMinute >= 0 && window.ToMinute <= 1440,
                        "Invalid weekly opening minute.");
                    int duration = window.ToMinute > window.FromMinute ? window.ToMinute - window.FromMinute : 1440 - window.FromMinute + window.ToMinute;
                    Require(window.FromMinute != window.ToMinute && duration > 0, "Weekly opening window has no duration.");
                }
            }
            ValidateExpansion(state);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var assigned = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var citizen in state.Citizens!)
            {
                Require(citizen != null && citizen.Identity != null && citizen.Preferences != null, "Missing citizen identity or preferences.");
                var identity = citizen.Identity;
                Text(identity.Id, "Citizen ID", 256); Text(identity.Name, "Citizen name", 512); Text(identity.District, "Citizen district", 256, true);
                Require(ids.Add(identity.Id), "Duplicate citizen ID: " + identity.Id);
                Require(identity.Age >= 0 && identity.Age <= 130, "Invalid citizen age."); Known(identity.Persona, "Persona");
                Reference(places, citizen.HomeId, PlaceKind.Home);
                // DailyRoutine derives the activity from persona. A mismatched assignment would
                // load successfully, then create a destination/activity mismatch on its next trip.
                if (identity.Persona == Persona.Retiree)
                    Require(citizen.DayPlaceId == "", "A retiree cannot have a day-place assignment.");
                else
                    OptionalReference(places, citizen.DayPlaceId, identity.Persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work);
                ValidateCommutePreparation(citizen, places, state.Minute);
                CountAssignment(citizen.HomeId);
                if (citizen.DayPlaceId != "") CountAssignment(citizen.DayPlaceId);
                OptionalReference(places, citizen.ShopId, PlaceKind.Shop);
                OptionalReference(places, citizen.LeisureId, PlaceKind.Leisure);
                Known(citizen.Phase, "Citizen phase"); Known(citizen.Activity, "Activity"); Known(citizen.DestinationActivity, "Destination activity");
                var current = Reference(places, citizen.PlaceId);
                Require((int)current.Kind == (int)citizen.Activity, "Current activity does not match current place.");
                Bounded(citizen.Preferences.WalkingToleranceMetres, 0, MaxCoordinate, "Walking tolerance");
                Bounded(citizen.Preferences.FareWeightMinutes, 0, 1000000, "Fare weight");
                Minute(citizen.DepartureMinute, "Departure minute"); Minute(citizen.NextEventMinute, "Next event minute"); Minute(citizen.ActivityUntilMinute, "Activity end minute");
                Require(citizen.NextEventMinute >= state.Minute, "Next event precedes the city clock.");
                Require(citizen.CompletedJourneys >= 0 && citizen.MissedActivities >= 0, "Negative citizen counters.");
                if (citizen.Phase == CitizenPhase.AtPlace)
                {
                    Require(citizen.DestinationId == "" && citizen.Journey == null && citizen.DepartureMinute == 0, "At-place citizen has an active destination or journey.");
                }
                else
                {
                    var destination = Reference(places, citizen.DestinationId);
                    Require((int)destination.Kind == (int)citizen.DestinationActivity, "Destination activity does not match destination place.");
                    if (citizen.Phase == CitizenPhase.WaitingForRoute || citizen.Phase == CitizenPhase.WaitingForOpening)
                        Require(citizen.Journey == null && citizen.DepartureMinute == 0, "Waiting citizen has an active journey.");
                    else
                    {
                        var route = citizen.Journey;
                        Require(route != null && route.Legs != null && route.Legs.Count > 0 && route.Legs.Count <= MaxRouteLegs, "Missing or oversized route.");
                        embeddedRecords += route!.Legs.Count;
                        Position previous = current.Position;
                        foreach (var leg in route.Legs)
                        {
                            Require(leg != null, "Null route leg."); Point(leg.From); Point(leg.To); Known(leg.Mode, "Travel mode");
                            Bounded(leg.Minutes, double.Epsilon, 1000000, "Leg duration"); Text(leg.ServiceId, "Service ID", 256, true);
                            Require(Same(previous, leg.From), "Route legs are disconnected or start at the wrong place."); previous = leg.To;
                        }
                        Require(Same(previous, destination.Position), "Route ends at the wrong destination.");
                        Require(citizen.DepartureMinute <= state.Minute, "Journey departure is in the future.");
                        double arrival = citizen.DepartureMinute + route.Minutes;
                        // A completed physical lease rebases departure = clock - duration.
                        // Adding duration back can differ from clock by a few ulps. Normalize
                        // only this computed value when the explicit event is exactly queued
                        // at the current clock; persisted dates themselves remain strict.
                        double clockRoundoff = 4 * 2.2204460492503131e-16
                            * Math.Max(1, Math.Max(Math.Abs(state.Minute), Math.Abs(route.Minutes)));
                        if (citizen.NextEventMinute == state.Minute && Math.Abs(arrival - state.Minute) <= clockRoundoff)
                            arrival = state.Minute;
                        Require(arrival <= MaxMinute && Near(arrival, citizen.NextEventMinute), "Journey arrival does not match the next event.");
                        Require(arrival >= state.Minute, "Journey ended before the city clock.");
                        Point(route.PositionAt(state.Minute - citizen.DepartureMinute));
                    }
                }
                if (citizen.Phase == CitizenPhase.WaitingForOpening)
                {
                    Require(citizen.DestinationActivity != Activity.Home && citizen.NextEventMinute < citizen.ActivityUntilMinute,
                        "Opening wait must target an activity before its deadline.");
                    Require(DailyRoutine.Open(places[citizen.DestinationId], citizen.NextEventMinute),
                        "Opening wait does not end at an open venue.");
                }
                Require(citizen.RecentVisits != null && citizen.RecentVisits.Count <= MaxRecentVisits, "Invalid recent visit count.");
                embeddedRecords += citizen.RecentVisits!.Count;
                double lastVisit = 0;
                foreach (var visit in citizen.RecentVisits)
                {
                    Require(visit != null, "Null visit."); Minute(visit.AtMinute, "Visit minute"); Known(visit.Activity, "Visit activity");
                    var visited = Reference(places, visit.PlaceId);
                    Require((int)visited.Kind == (int)visit.Activity, "Visit activity does not match place.");
                    Require(visit.AtMinute >= lastVisit && visit.AtMinute <= state.Minute, "Visits are unordered or in the future."); lastVisit = visit.AtMinute;
                }
                Require(embeddedRecords <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
            }
            ValidateTraffic(state, embeddedRecords);
            void CountAssignment(string id)
            {
                int count = assigned.TryGetValue(id, out int previous) ? previous + 1 : 1;
                Require(count <= places[id].Capacity, "Place assignment exceeds capacity: " + id);
                assigned[id] = count;
            }
        }

        private static void ValidateTraffic(CityState state, int embeddedRecords)
        {
            Text(state.TrafficTopologyId, "Traffic topology ID", 64, true);
            if (state.Traffic == null)
            {
                Require(state.TrafficTopologyId == "", "Traffic topology has no kernel snapshot.");
                return;
            }
            Require(state.TravelTimingVersion == 1, "Traffic requires native travel timing.");
            Require(state.TrafficTopologyId.Length == 64 && state.TrafficTopologyId.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'),
                "Invalid traffic topology fingerprint.");
            var traffic = state.Traffic;
            Require(traffic.Clock == state.Minute, "Traffic and city clocks disagree.");
            Minute(traffic.Clock, "Traffic clock"); Minute(traffic.RequestedTime, "Requested traffic clock");
            Require(traffic.Schema == 2 && traffic.RequestedTime >= traffic.Clock, "Invalid traffic schema or target.");
            Require(traffic.Definitions != null && traffic.Links != null && traffic.Vehicles != null && traffic.Events != null,
                "Missing traffic collections.");
            Count(traffic.Definitions.Count, MaxTrafficLinks); Count(traffic.Links.Count, MaxTrafficLinks);
            Count(traffic.Vehicles.Count, MaxCitizens); Count(traffic.Events.Count, MaxTrafficLinks);
            foreach (var definition in traffic.Definitions)
            {
                Require(definition != null, "Null traffic definition.");
                Require(definition.Capacity >= 1 && definition.Capacity <= MaxTrafficLinkCapacity, "Invalid traffic capacity.");
                Bounded(definition.FreeFlowMinutes, double.Epsilon, MaxMinute, "Traffic free-flow duration");
                Bounded(definition.HeadwayMinutes, 0, MaxMinute, "Traffic headway");
                if (definition.Signal != null)
                {
                    var signal = definition.Signal;
                    Bounded(signal.CycleMinutes, double.Epsilon, MaxMinute, "Traffic signal cycle");
                    Bounded(signal.GreenStartMinutes, 0, signal.CycleMinutes, "Traffic green start");
                    Bounded(signal.GreenDurationMinutes, 0, signal.CycleMinutes, "Traffic green duration");
                    Bounded(signal.PhaseOffsetMinutes, -MaxMinute, MaxMinute, "Traffic signal phase");
                }
            }
            foreach (var link in traffic.Links)
            {
                Require(link != null && link.Vehicles != null && link.WaitingSources != null && link.IncomingLinks != null,
                    "Missing traffic link collections.");
                Count(link.Vehicles.Count, MaxCitizens); Count(link.WaitingSources.Count, MaxCitizens); Count(link.IncomingLinks.Count, MaxTrafficLinks);
                Minute(link.NextDischargeAt, "Traffic next discharge");
                foreach (string id in link.Vehicles.Concat(link.WaitingSources)) Text(id, "Traffic vehicle reference", 256);
            }
            var citizens = state.Citizens.ToDictionary(c => c.Identity.Id, StringComparer.Ordinal);
            foreach (var vehicle in traffic.Vehicles)
            {
                Require(vehicle != null && vehicle.Route != null, "Missing traffic vehicle route.");
                Text(vehicle.VehicleId, "Traffic vehicle ID", 256); Count(vehicle.Route.Count, MaxRouteLegs);
                Minute(vehicle.EnqueuedAt, "Traffic enqueue time"); Minute(vehicle.EnteredAt, "Traffic entry time");
                Minute(vehicle.ReadyAt, "Traffic readiness"); Minute(vehicle.MotionStartedAt, "Traffic motion start");
                Minute(vehicle.NominalMinutes, "Traffic nominal progress");
                if (vehicle.CompletedAt.HasValue) Minute(vehicle.CompletedAt.Value, "Traffic completion time");
                Require(citizens.TryGetValue(vehicle.VehicleId, out var citizen) && citizen.Phase == CitizenPhase.Travelling
                    && citizen.Journey != null && citizen.Preferences.OwnsCar && citizen.Car != null
                    && citizen.Car.Id == citizen.Identity.Id + ":car", "Traffic vehicle lacks its travelling car owner.");
                int geometryIndex = 0; double geometryStart = 0, previousEnd = 0;
                foreach (var leg in vehicle.Route)
                {
                    Require(leg != null, "Null traffic route segment.");
                    Minute(leg.StartNominalMinutes, "Traffic geometry start"); Minute(leg.EndNominalMinutes, "Traffic geometry end");
                    Bounded(leg.FreeFlowMinutes, double.Epsilon, MaxMinute, "Traffic route free-flow duration");
                    Bounded(leg.StartLinkFraction, 0, 1, "Traffic section entry fraction");
                    Bounded(leg.EndLinkFraction, 0, 1, "Traffic section exit fraction");
                    Require(leg.StartLinkFraction < leg.EndLinkFraction, "Empty traffic spatial section.");
                    Require(leg.StartNominalMinutes >= previousEnd && leg.EndNominalMinutes > leg.StartNominalMinutes
                        && leg.EndNominalMinutes <= citizen.Journey.Minutes,
                        "Traffic geometry is outside its committed journey.");
                    previousEnd = leg.EndNominalMinutes;
                    while (geometryIndex < citizen.Journey.Legs.Count
                        && geometryStart + citizen.Journey.Legs[geometryIndex].Minutes <= leg.StartNominalMinutes)
                    {
                        geometryStart += citizen.Journey.Legs[geometryIndex].Minutes;
                        geometryIndex++;
                    }
                    int scanIndex = geometryIndex; double scanStart = geometryStart;
                    while (scanIndex < citizen.Journey.Legs.Count && scanStart < leg.EndNominalMinutes)
                    {
                        var geometry = citizen.Journey.Legs[scanIndex];
                        double end = scanStart + geometry.Minutes;
                        if (Math.Min(end, leg.EndNominalMinutes) > Math.Max(scanStart, leg.StartNominalMinutes))
                            Require(IsMovingTrafficGeometry(geometry),
                                "Traffic geometry includes a walk or stationary wait.");
                        if (end > leg.EndNominalMinutes) break;
                        scanStart = end; scanIndex++;
                    }
                    geometryIndex = scanIndex; geometryStart = scanStart;
                }
            }
            foreach (var item in traffic.Events) { Require(item != null, "Null traffic event."); Minute(item.At, "Traffic event time"); }
            TrafficKernel restored;
            try { restored = TrafficKernel.Restore(traffic, false); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { throw new SaveValidationException("Invalid traffic state: " + exception.Message); }
            foreach (var vehicle in traffic.Vehicles)
            {
                var citizen = citizens[vehicle.VehicleId];
                double expected = restored.GetPermission(vehicle.VehicleId).NominalMinutes;
                double actual = state.Minute - citizen.DepartureMinute;
                Require(Near(expected, actual), "Traffic progress and committed journey disagree.");
            }

            void Count(int count, int limit)
            {
                Require(count <= limit, "Oversized traffic collection.");
                embeddedRecords += count;
                Require(embeddedRecords <= MaxEmbeddedRecords, "Too many embedded traffic records.");
            }
        }

        private static bool IsMovingTrafficGeometry(RouteLeg geometry)
        {
            double metres = Position.Distance(geometry.From, geometry.To);
            if (geometry.Mode != TravelMode.Car || !Position.Finite(metres) || metres <= 0) return false;
            if (metres > 0.000001) return true;
            // Clock retiming can retain a positive sub-micrometre native fragment. The codec
            // checks its tag shape; binding still proves its directed edge and geometry.
            string[] parts = geometry.ServiceId.Split(':');
            return parts.Length == 6 && parts[0] == "native" && parts[1] == "road" && parts[2] == "fragment1"
                && parts[3].Length == 64 && parts[3].All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f')
                && int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out int from) && from >= 0
                && int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out int to) && to >= 0 && from != to;
        }

        private static void ValidateCommutePreparation(CitizenState citizen, Dictionary<string, Place> places, double minute)
        {
            var preparation = citizen.CommutePreparation;
            Require(preparation != null, "Missing commute preparation.");
            Text(preparation.PlaceId, "Commute preparation place", 256, true);
            Minute(preparation.WorkStartsAt, "Prepared work start");
            Minute(preparation.WorkEndsAt, "Prepared work end");
            Minute(preparation.PreparedAt, "Commute preparation minute");
            Bounded(preparation.TravelMinutes, 0, MaxRouteLegs * 1000000.0, "Prepared travel duration");
            if (preparation.PlaceId == "")
            {
                Require(preparation.WorkStartsAt == 0 && preparation.WorkEndsAt == 0
                    && preparation.PreparedAt == 0 && preparation.TravelMinutes == 0, "Empty commute preparation has measured values.");
                return;
            }
            Require(citizen.Identity.Persona != Persona.Retiree, "A retiree cannot prepare a work commute.");
            // A safe job change may leave the prior attempt until the next routine evaluation.
            Reference(places, preparation.PlaceId, citizen.Identity.Persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work);
            Require(preparation.WorkEndsAt > preparation.WorkStartsAt, "Prepared work window has no duration.");
            Require(preparation.PreparedAt <= minute && preparation.PreparedAt <= preparation.WorkStartsAt,
                "Commute preparation occurs in the future or after work starts.");
        }

        private static void ValidateExpansion(CityState state)
        {
            Require(state.SourceIdentities != null && state.SourceIdentities.Count > 0 && state.SourceIdentities.Count <= 5000,
                "Invalid immutable identity source catalogue size.");
            Require(state.NextResidentSequence >= 1 && state.NextResidentSequence < int.MaxValue,
                "Invalid resident sequence.");
            Require(state.PopulationTarget == state.Citizens.Count, "Active population differs from its housing allocation.");
            Require(state.InitialImportedResidents >= 0 && state.InitialImportedResidents <= state.SourceIdentities!.Count
                && state.InitialImportedResidents <= state.PopulationTarget, "Invalid imported resident count.");
            Require((long)state.Citizens.Count == (long)state.InitialImportedResidents + state.NextResidentSequence - 1,
                "Resident creation and active counts disagree.");
            var source = new Dictionary<string, CitizenIdentity>(StringComparer.Ordinal);
            foreach (var identity in state.SourceIdentities!)
            {
                ValidateIdentity(identity);
                Require(!identity.Id.StartsWith("pc:", StringComparison.Ordinal) && !source.ContainsKey(identity.Id), "Invalid or duplicate source ID.");
                source.Add(identity.Id, identity);
            }
            var importedIds = new HashSet<string>(source.Keys.OrderBy(id => id, StringComparer.Ordinal).Take(state.InitialImportedResidents), StringComparer.Ordinal);
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var citizen in state.Citizens)
            {
                Require(citizen != null, "Null citizen.");
                Provenance(citizen.Identity);
                Require(active.Add(citizen.Identity.Id), "Duplicate active ID.");
                Minute(citizen.JoinedMinute, "Joined minute");
                Require(citizen.JoinedMinute <= state.Minute, "Resident joins in the future.");
                Require(citizen.LastEmploymentCheckDay >= (int)(citizen.JoinedMinute / 1440)
                    && citizen.LastEmploymentCheckDay <= (int)(state.Minute / 1440), "Invalid daily employment checkpoint.");
                Require(citizen.JobChanges >= 0, "Invalid job-change count.");
                if (citizen.Car != null)
                {
                    Require(citizen.Preferences != null && citizen.Preferences.OwnsCar, "Unowned persistent car.");
                    Require(citizen.Car.Id == citizen.Identity.Id + ":car", "Car identity does not belong to resident.");
                    Point(citizen.Car.ParkedPosition);
                    Text(citizen.Car.ModelId, "Car model ID", 128, true);
                }
                if (citizen.Journey?.Legs != null)
                {
                    bool began = false, ended = false;
                    foreach (var leg in citizen.Journey.Legs)
                    {
                        Require(leg != null, "Null route leg.");
                        if (leg.Mode == TravelMode.Car)
                        {
                            Require(citizen.Car != null && !ended, "Car journey has no car or multiple driving blocks.");
                            if (!began) Require(Position.Distance(citizen.Car!.ParkedPosition, leg.From) <= 0.01, "Car journey starts away from its parked car.");
                            began = true;
                        }
                        else if (began) ended = true;
                    }
                    if (began) Require(citizen.Journey.Legs.All(l => l.Mode == TravelMode.Car || l.Mode == TravelMode.Walk),
                        "A car journey can only contain driving and walking access.");
                }
            }
            void Provenance(CitizenIdentity identity)
            {
                ValidateIdentity(identity);
                if (source.TryGetValue(identity.Id, out var original))
                    Require(importedIds.Contains(identity.Id) && Population.EqualIdentity(identity, original), "Imported identity is unallocated or differs from immutable source.");
                else
                {
                    Require(identity.Id.StartsWith("pc:", StringComparison.Ordinal), "Unknown source identity.");
                    string suffix = identity.Id.Substring(3);
                    Require(int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
                        && sequence >= 1 && sequence < state.NextResidentSequence
                        && suffix == sequence.ToString(CultureInfo.InvariantCulture), "Generated resident ID is invalid or not yet allocated.");
                }
            }
        }

        private sealed class FoodPlaceLookup
        {
            private readonly List<Place> source;
            private readonly HashSet<(string, int)>? keys;
            public FoodPlaceLookup(List<Place> source)
            {
                this.source = source;
                if (source.Count > MaxPlaces || source.Any(p => p == null)) return;
                keys = new HashSet<(string, int)>();
                foreach (var place in source) keys.Add((place.Id, (int)place.Kind));
            }
            public bool Contains(string id, int kind) => keys != null
                ? keys.Contains((id, kind))
                : source.Any(p => p.Id == id && (int)p.Kind == kind);
        }

        private static void ValidateFood(CityState state)
        {
            Require(state != null && state.Citizens != null && state.Places != null, "Missing food population.");
            var food = state.Food;
            if (food == null) { Require(state.Citizens.All(c => c != null && c.MealOuting == null), "Meal outing without economy."); return; }
            Require(food.Shops != null && food.Shops.Count <= 1024, "Invalid food scope.");
            Require(food.RepresentationFactor >= 1 && food.RepresentationFactor <= 3, "Invalid consumer representation factor.");
            Require(food.Residents != null && food.Residents.Count == state.Citizens.Count * food.RepresentationFactor, "Meal roster differs from represented population.");
            Require(food.Districts != null, "Missing food budget districts.");
            FoodHouseholdPolicy.ValidateMixes(food.Districts);
            Require(food.HouseholdFinanceVersion == 0 || food.HouseholdFinanceVersion == HouseholdFinance.PolicyVersion, "Unknown household finance policy.");
            Require(food.MealRhythmVersion == 0 || food.MealRhythmVersion == MealRhythm.PolicyVersion, "Unknown meal rhythm policy.");
            // Duplicate IDs/kinds must not fail earlier than the existing catalogue validation.
            // A malformed null entry retains Any's original short-circuit/exception behavior.
            var foodPlaces = new FoodPlaceLookup(state.Places);
            var shops = new HashSet<string>(StringComparer.Ordinal);
            var businessIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var shop in food.Shops)
            {
                Require(shop != null && shops.Add(shop.PlaceId) && businessIds.Add(shop.BusinessId), "Duplicate food scope.");
                Text(shop.PlaceId, "Food place", 256); Text(shop.ProductId, "Food product", 256); Text(shop.BusinessId, "Food business", 512);
                Require(shop.AdditionalProductIds != null && shop.AdditionalProductIds.Count <= 3
                    && shop.Products().Distinct(StringComparer.Ordinal).Count() == shop.AdditionalProductIds.Count + 1, "Invalid food menu.");
                foreach (var product in shop.AdditionalProductIds) Text(product, "Food menu product", 256);
                Require(foodPlaces.Contains(shop.PlaceId, (int)PlaceKind.Shop), "Unknown food shop.");
                Minute(shop.NextServiceAt, "Food service clock");
            }
            Require(food.History != null && food.History.Count <= 14 * MaxPlaces, "Oversized market history.");
            var historyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in food.History)
            {
                Require(row != null && row.Day >= 0 && row.Day <= (int)(state.Minute / 1440) && foodPlaces.Contains(row.PlaceId, (int)PlaceKind.Shop)
                    && historyKeys.Add(row.Day + ":" + row.PlaceId + "\n" + row.BusinessId), "Invalid market history ownership.");
                Text(row.BusinessId, "Historical business", 512);
                Require(row.OffersConsidered >= 0 && row.ChosenOffers >= 0 && row.PriceRejections >= 0
                    && row.AccessRejections >= 0 && row.ClosedRejections >= 0 && row.StockRejections >= 0 && row.StaffRejections >= 0
                    && row.ServiceRejections >= 0 && row.UnknownEvidence >= 0 && row.PreferredAlternatives >= 0
                    && row.Purchases >= 0 && row.ExistingStopPurchases >= 0 && row.ExistingStopPurchases <= row.Purchases
                    && row.RevenueCents >= 0, "Invalid market history counters.");
                Require(row.OffersConsidered == checked(row.ChosenOffers + row.PriceRejections + row.AccessRejections
                    + row.ClosedRejections + row.StockRejections + row.StaffRejections + row.ServiceRejections + row.UnknownEvidence + row.PreferredAlternatives),
                    "Market offer outcomes do not reconcile.");
                Require(row.PurchaseHours != null && row.PurchaseHours.Length == 24 && row.PurchaseHours.All(n => n >= 0)
                    && row.PurchaseHours.Sum() == row.Purchases, "Hourly purchases do not reconcile.");
                Require(row.PurchaseMeals != null && row.PurchaseMeals.Length == 4 && row.PurchaseMeals.All(n => n >= 0)
                    && row.PurchaseMeals.Sum() == row.Purchases, "Meal purchases do not reconcile.");
                Require(row.ProductSales != null && row.ProductSales.Count <= 4 && row.ProductSales.All(p => p != null)
                    && row.ProductSales.Select(p => p.ProductId).Distinct(StringComparer.Ordinal).Count() == row.ProductSales.Count,
                    "Invalid product sales history.");
                foreach (var product in row.ProductSales)
                    Require(product != null && !string.IsNullOrWhiteSpace(product.ProductId)
                        && product.Purchases > 0 && product.RevenueCents >= 0 && product.PurchaseMeals != null
                        && product.PurchaseMeals.Length == 4 && product.PurchaseMeals.All(n => n >= 0)
                        && product.PurchaseMeals.Sum() == product.Purchases, "Invalid product purchase counters.");
                Require(row.ProductSales.Sum(p => p.Purchases) == row.Purchases
                    && row.ProductSales.Sum(p => p.RevenueCents) == row.RevenueCents
                    && Enumerable.Range(0, 4).All(k => row.ProductSales.Sum(p => p.PurchaseMeals[k]) == row.PurchaseMeals[k]),
                    "Product sales do not reconcile.");
                Require(row.BuyerDistricts != null && row.BuyerDistricts.Count <= 100
                    && row.BuyerDistricts.All(d => d != null && d.Purchases > 0)
                    && row.BuyerDistricts.Select(d => d.DistrictId).Distinct(StringComparer.Ordinal).Count() == row.BuyerDistricts.Count
                    && row.BuyerDistricts.Sum(d => d.Purchases) == row.Purchases, "Customer districts do not reconcile.");
                foreach (var district in row.BuyerDistricts) Text(district.DistrictId, "Buyer district", 256, emptyAllowed: true);
            }
            var citizens = state.Citizens.ToDictionary(c => c.Identity.Id, StringComparer.Ordinal);
            Require(food.RoutineStops != null && food.RoutineStops.Count <= citizens.Count, "Invalid shared routine stops.");
            var stopOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stop in food.RoutineStops)
            {
                Require(stop != null && citizens.ContainsKey(stop.AnchorId) && stopOwners.Add(stop.AnchorId), "Invalid routine stop owner.");
                Known(stop.Activity, "Shared routine activity");
                Require(stop.Activity != Activity.Shop && foodPlaces.Contains(stop.PlaceId, (int)stop.Activity), "Invalid shared routine place.");
                Minute(stop.CapturedAt, "Routine capture"); Minute(stop.DepartureAt, "Routine departure");
                Require(stop.CapturedAt <= state.Minute && stop.DepartureAt > stop.CapturedAt, "Invalid routine opportunity.");
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var meal in food.Residents)
            {
                Require(meal != null && seen.Add(meal.ResidentId) && citizens.ContainsKey(meal.AnchorId)
                    && meal.Slot >= 0 && meal.Slot < food.RepresentationFactor
                    && meal.ResidentId == FoodRepresentation.ConsumerId(meal.AnchorId, meal.Slot), "Unknown or duplicate consumer/anchor slot.");
                Require(meal.PreferenceBonusCents >= 0 && meal.PreferenceBonusCents <= 150
                    && (meal.PreferredProductId.Length == 0 ? meal.PreferenceBonusCents == 0 : FoodProducts.Known(meal.PreferredProductId)), "Invalid consumer taste.");
                Require(meal.Slot > 0 || meal.Excursion == null && !meal.NeedsHomeResync, "Physical anchor cannot own an abstract excursion.");
                Require(meal.Slot == 0 || !meal.ExistingStop, "Represented consumer cannot claim anchor's existing-stop purchase.");
                Known(meal.Status, "Meal status");
                Known(meal.Kind, "Meal kind");
                Require(food.MealRhythmVersion != 0 || meal.Kind == MealKind.Legacy, "Meal kind differs from rhythm policy.");
                Require(meal.DayAllowanceUsedCents >= 0 && meal.DayAllowanceUsedCents <= 1000000, "Invalid daily food allowance use.");
                Require((meal.Budget != null) == (food.HouseholdFinanceVersion != 0), "Household budget ownership differs from policy.");
                if (meal.Budget != null) ValidateBudget(meal.Budget, state.Minute);
                Require(meal.Day >= 0 && meal.Day <= 100000 && meal.AllowanceCents >= 0 && meal.AllowanceCents <= 1000000
                    && meal.PriceCents >= 0 && meal.PriceCents <= meal.AllowanceCents && meal.Purchases >= 0 && meal.SpentCents >= 0,
                    "Invalid meal allowance/accounting.");
                Minute(meal.DesiredAt, "Meal appetite time"); Minute(meal.ExpiresAt, "Meal expiry"); Minute(meal.DueAt, "Meal event");
                Minute(meal.ServiceAt, "Meal service time"); Minute(meal.Deadline, "Meal return deadline");
                Bounded(meal.ServiceMinutes, 0, 120, "Meal service duration"); Bounded(meal.ReturnMinutes, 0, 1440, "Meal return duration");
                Require(meal.DesiredAt >= meal.Day * 1440.0 && meal.DesiredAt < (meal.Day + 1) * 1440.0
                    && meal.ExpiresAt > meal.DesiredAt && meal.ExpiresAt <= (meal.Day + 1) * 1440.0
                    && meal.ExpiresAt <= meal.DesiredAt + 240 && meal.DueAt >= state.Minute, "Invalid meal schedule.");
                Text(meal.PlaceId, "Meal shop", 256, true); Text(meal.BusinessId, "Meal business", 512, true);
                Text(meal.ProductId, "Meal product", 256, true); Text(meal.Reason, "Meal reason", 512, true);
                if (meal.PlaceId.Length > 0)
                    Require(foodPlaces.Contains(meal.PlaceId, (int)PlaceKind.Shop) && meal.BusinessId.Length > 0 && meal.ProductId.Length > 0, "Meal lost its historical business/place identity.");
                if (meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving)
                    Require(meal.PlaceId.Length > 0 && meal.PriceCents > 0 && meal.ServiceMinutes > 0
                        && meal.Deadline <= meal.ExpiresAt, "Active meal lost its outing.");
                Require(meal.ExistingStopPurchases >= 0 && meal.ExistingStopPurchases <= meal.Purchases, "Invalid existing-stop purchase count.");
                Require(!meal.ExistingStop || meal.PlaceId.Length > 0 && meal.ReturnMinutes == 0
                    && meal.Status != MealStatus.Travelling, "Existing-stop purchase cannot own a second trip.");
                ValidateExcursion(meal, state, foodPlaces);
                var outing = meal.Slot == 0 ? citizens[meal.AnchorId].MealOuting : null;
                Require(!meal.ExistingStop || outing == null, "Existing-stop purchase also owns an outing.");
                if (outing != null)
                {
                    Text(outing.OriginId, "Meal origin", 256); Known(outing.OriginActivity, "Meal origin activity");
                    Require(foodPlaces.Contains(outing.OriginId, (int)outing.OriginActivity), "Invalid meal origin.");
                    Minute(outing.ResumeUntil, "Meal resume end"); Minute(outing.ResumeEvent, "Meal resume event");
                    Require(outing.ResumeEvent <= outing.ResumeUntil && meal.PlaceId.Length > 0, "Invalid meal return ownership.");
                }
            }
        }

        private static void ValidateExcursion(ResidentMeal meal, CityState state, FoodPlaceLookup foodPlaces)
        {
            var excursion = meal.Excursion;
            if (meal.Slot > 0 && (meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving))
                Require(excursion != null, "Active represented meal lost its excursion.");
            if (excursion == null) return;
            Require(meal.Slot > 0 && !meal.NeedsHomeResync && meal.Status != MealStatus.Pending
                && excursion.DestinationId == meal.PlaceId, "Invalid represented excursion owner.");
            Known(excursion.OriginActivity, "Excursion origin activity");
            Require(excursion.ReturnGeometry.Length == 44, "Missing committed return geometry signature.");
            Text(excursion.UnresolvedReason, "Excursion unresolved reason", 256, true);
            Require(excursion.Unresolved == (excursion.UnresolvedReason.Length != 0), "Excursion unresolved evidence differs from state.");
            Require(excursion.MaximumQueueMinutes >= 0 && excursion.MaximumQueueMinutes <= 60, "Invalid represented queue limit.");
            Require(foodPlaces.Contains(excursion.OriginId, (int)excursion.OriginActivity)
                && foodPlaces.Contains(excursion.DestinationId, (int)PlaceKind.Shop), "Invalid excursion endpoints.");
            Require(excursion.OriginPosition.IsFinite && excursion.DestinationPosition.IsFinite, "Invalid excursion endpoint geometry.");
            Minute(excursion.DepartedAt, "Excursion departure"); Minute(excursion.ArrivesAt, "Excursion arrival");
            Minute(excursion.AdmissionAt, "Excursion admission");
            Minute(excursion.SharedDepartureAt, "Excursion shared departure"); Minute(excursion.ReturnStartsAt, "Excursion return start");
            Minute(excursion.ReturnsAt, "Excursion return completion");
            Require(excursion.DepartedAt <= state.Minute && excursion.ArrivesAt > excursion.DepartedAt
                && excursion.ArrivesAt <= meal.Deadline && meal.Deadline <= excursion.SharedDepartureAt,
                "Excursion does not fit its committed opportunity.");
            Require(excursion.AdmissionAt >= excursion.ArrivesAt && excursion.AdmissionAt < meal.Deadline, "Invalid represented admission time.");
            Require(!excursion.Returning || excursion.ReturnsAt > excursion.ReturnStartsAt
                && excursion.ReturnsAt <= excursion.SharedDepartureAt && excursion.ReturnsAt <= meal.Deadline,
                "Invalid represented return interval.");
            Require(!excursion.Unresolved || meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving,
                "Unresolved excursion cannot remain serviceable.");
            Require(!excursion.Returning || meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving,
                "Returning excursion cannot purchase.");
        }

        private static void ValidateBudget(ResidentBudget b, double minute)
        {
            Known(b.IncomeSource, "Resident income source");
            Require(b.BaseIncomeCents > 0 && b.BaseIncomeCents <= 1000000
                && b.DailyIncomeCents >= 0 && b.DailyIncomeCents <= 1000000
                && b.DailyHousingCents >= 0 && b.DailyHousingCents <= 1000000
                && b.DailyEssentialsCents >= 0 && b.DailyEssentialsCents <= 1000000
                && b.DailyHomeFoodCents > 0 && b.DailyHomeFoodCents <= 1000000, "Invalid household policy amounts.");
            Require(b.OpeningBalanceCents >= 0 && b.BalanceCents >= 0 && b.IncomeReceivedCents >= 0
                && b.EssentialsPaidCents >= 0 && b.HomeFoodPaidCents >= 0 && b.PreparedFoodSpentCents >= 0
                && b.HeldPurchaseCents >= 0 && b.HeldPurchaseCents <= 1000000 && b.ArrearsCents >= 0,
                "Negative household accounting.");
            Require(checked(b.OpeningBalanceCents + b.IncomeReceivedCents) == checked(b.BalanceCents
                + b.EssentialsPaidCents + b.HomeFoodPaidCents + b.PreparedFoodSpentCents + b.HeldPurchaseCents),
                "Household cash is not conserved.");
            Minute(b.NextPostingMinute, "Next household posting");
            Require(b.LastPostedDay >= -1 && b.LastPostedDay <= (int)(minute / 1440)
                && b.HomeFoodFundedDay >= -1 && b.HomeFoodFundedDay <= b.LastPostedDay
                && b.NextPostingMinute >= minute && b.DaysWithoutHomeFood >= 0
                && (b.LastPostedDay < 0 || b.NextPostingMinute == (b.LastPostedDay + 1) * 1440.0), "Invalid household posting ownership.");
        }

        private static void ValidateIdentity(CitizenIdentity identity)
        {
            Require(identity != null, "Null identity.");
            Text(identity.Id, "Citizen ID", 256); Text(identity.Name, "Citizen name", 512); Text(identity.District, "Citizen district", 256, true);
            Require(identity.Age >= 0 && identity.Age <= 130, "Invalid citizen age."); Known(identity.Persona, "Persona");
        }

        private static Place Reference(Dictionary<string, Place> places, string id, params PlaceKind[] kinds)
        {
            Text(id, "Place reference", 256);
            Require(places.TryGetValue(id, out var place), "Unknown place reference: " + id);
            Require(kinds.Length == 0 || kinds.Contains(place!.Kind), "Place reference has the wrong role: " + id);
            return place!;
        }
        private static void OptionalReference(Dictionary<string, Place> places, string id, params PlaceKind[] kinds)
        { Require(id != null, "Null place reference."); if (id != "") Reference(places, id, kinds); }
        private static void Known<T>(T value, string label) where T : struct => Require(Enum.IsDefined(typeof(T), value), "Unknown " + label + ".");
        private static void Text(string value, string label, int max, bool emptyAllowed = false) => Require(value != null && value.Length <= max && (emptyAllowed || !string.IsNullOrWhiteSpace(value)), "Invalid " + label + ".");
        private static void Minute(double value, string label) => Bounded(value, 0, MaxMinute, label);
        private static void Bounded(double value, double min, double max, string label) => Require(Position.Finite(value) && value >= min && value <= max, "Invalid " + label + ".");
        private static void Point(Position point) { Bounded(point.X, -MaxCoordinate, MaxCoordinate, "position X"); Bounded(point.Y, -MaxCoordinate, MaxCoordinate, "position Y"); Bounded(point.Z, -MaxCoordinate, MaxCoordinate, "position Z"); }
        private static bool Same(Position a, Position b) => Position.Distance(a, b) <= 0.1;
        private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-7;
        private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new SaveValidationException(message); }

        private sealed class StateContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization).Where(property => property.Writable)
                    .OrderBy(property => property.PropertyName, StringComparer.Ordinal).Select(property =>
                {
                    property.Required = NullableRecord(property.PropertyType!) || Nullable.GetUnderlyingType(property.PropertyType!) != null ? Required.AllowNull : Required.Always;
                    return property;
                }).ToList();
            }
        }

        private sealed class PositionConverter : JsonConverter<Position>
        {
            public override void WriteJson(JsonWriter writer, Position value, JsonSerializer serializer)
            {
                writer.WriteStartObject(); writer.WritePropertyName("X"); writer.WriteValue(value.X); writer.WritePropertyName("Y"); writer.WriteValue(value.Y); writer.WritePropertyName("Z"); writer.WriteValue(value.Z); writer.WriteEndObject();
            }
            public override Position ReadJson(JsonReader reader, Type objectType, Position existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var token = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Require(token.Count == 3, "Position requires exactly X, Y and Z.");
                double Coordinate(string name)
                {
                    var component = token[name];
                    Require(component != null && (component.Type == JTokenType.Float || component.Type == JTokenType.Integer), "Missing or invalid position " + name + ".");
                    return component!.Value<double>();
                }
                var point = new Position(Coordinate("X"), Coordinate("Y"), Coordinate("Z")); Point(point); return point;
            }
        }
    }
}
