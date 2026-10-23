using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    /// <summary>Read-only scene/body reservations, through RLO's CityApi when it has one and otherwise for explicitly
    /// reviewed RLO releases. Main thread only.</summary>
    public sealed class RloPhysicalReservations
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private readonly List<CitizenIdentity> sources = new List<CitizenIdentity>();
        private readonly HashSet<string> reserved = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<object> pedestrians = new HashSet<object>(References.Instance);
        private readonly HashSet<object> characters = new HashSet<object>(References.Instance);
        private readonly HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<int> readIndexes = new List<int>();
        private readonly List<object> readPedestrians = new List<object>(), readCharacters = new List<object>();
        private RloBridge? bridge;
        private bool citizensKnown = true, bodiesKnown = true;
        private PropertyInfo socialInstance = null!, socialCast = null!, cityCharacter = null!, incidentsInstance = null!, ragdollsInstance = null!, ragdollActive = null!, ragdollCharacter = null!;
        private FieldInfo castWears = null!, castOut = null!, castPeople = null!, socialPeople = null!, personIndex = null!, personName = null!, personAge = null!, personFemale = null!, personSeed = null!, casualties = null!, casualtyTakeover = null!, takeoverPedestrian = null!, ragdolls = null!;
        public bool Ready { get; private set; }
        public string LastError { get; private set; } = "";
        public IReadOnlyCollection<string> ReservedCitizenIds => reserved;
        public bool IsReserved(string citizenId) => !Ready || !citizensKnown || reserved.Contains(citizenId);
        public bool IsReservedNativePedestrian(object pedestrian, object? character)
            => !Ready || !bodiesKnown || pedestrians.Contains(pedestrian) || (character != null && characters.Contains(character));

        public RloPhysicalReservations(Assembly rlo, IReadOnlyList<CitizenIdentity> sourceIdentities)
        {
            try
            {
                if (rlo == null || rlo.GetName().Name != "RealLifeOverhaul") throw new InvalidOperationException("Physical reservations require RealLifeOverhaul.");
                bridge = RloBridge.Find(rlo);
                if (bridge == null && !RloIdentityAdapter.IsReviewedAssembly(rlo))
                    throw new InvalidOperationException("Physical reservations require reviewed RLO " + RloIdentityAdapter.ReviewedVersions + " or CityApi contract " + RloBridge.SupportedContract + ".");
                if (sourceIdentities == null || sourceIdentities.Count == 0) throw new InvalidOperationException("Missing original identity catalogue.");
                foreach (var identity in sourceIdentities)
                {
                    // Generated residents can reuse donors' looks but never become the donor.
                    if (identity.Id.Length > 1 && identity.Id[0] == 'c'
                        && int.TryParse(identity.Id.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                        && index >= 0 && identity.Id == "c" + index.ToString(CultureInfo.InvariantCulture))
                        sources.Add(Population.CloneIdentity(identity));
                }
                if (sources.Count == 0) throw new InvalidOperationException("Missing original Flare c-index identities.");
                if (bridge != null)
                {
                    foreach (var source in sources) sourceIds.Add(source.Id);
                    Ready = true; Update();
                    return;
                }
                Type director = Type(rlo, "SocialDirector"), cast = Type(rlo, "SocialCast"), person = Type(rlo, "SocialPerson"), cityPerson = Type(rlo, "CityPerson"), state = Type(rlo, "SocialState");
                socialInstance = Property(director, "Instance", director, true); socialCast = Property(director, "Cast", cast);
                castWears = Field(cast, "_wears", typeof(int[])); castOut = Field(cast, "_out", typeof(bool[])); castPeople = Field(cast, "_people", cityPerson.MakeArrayType());
                socialPeople = Field(state, "People", person.MakeArrayType(), true);
                personIndex = Field(person, "Index", typeof(int)); personName = Field(person, "Name", typeof(string));
                personAge = Field(person, "Age", typeof(int)); personFemale = Field(person, "Female", typeof(bool)); personSeed = Field(person, "Seed", typeof(int));
                cityCharacter = cityPerson.GetProperty("Character", Instance) ?? throw new MissingMemberException("CityPerson.Character");
                if (cityCharacter.PropertyType.Name != "ThirdPersonCharacter" || cityCharacter.GetMethod == null) throw new InvalidOperationException("Unexpected CityPerson.Character contract.");
                Type incident = Type(rlo, "PedestrianIncidents"), casualty = Type(rlo, "Casualty"), takeover = Type(rlo, "PedestrianTakeover"), system = Type(rlo, "RagdollSystem"), ragdoll = Type(rlo, "Ragdoll");
                incidentsInstance = Property(incident, "Instance", incident, true); casualties = Field(incident, "_casualties", casualty.MakeArrayType());
                casualtyTakeover = Field(casualty, "_pedestrian", takeover);
                takeoverPedestrian = takeover.GetField("_pedestrian", Instance) ?? throw new MissingFieldException("PedestrianTakeover._pedestrian");
                if (takeoverPedestrian.FieldType.FullName != "Streets.Pedestrians.Pedestrian") throw new InvalidOperationException("Unexpected native pedestrian takeover contract.");
                ragdollsInstance = Property(system, "Instance", system, true); ragdolls = Field(system, "_ragdolls", ragdoll.MakeArrayType());
                ragdollActive = Property(ragdoll, "Active", typeof(bool));
                ragdollCharacter = ragdoll.GetProperty("Character", Instance) ?? throw new MissingMemberException("Ragdoll.Character");
                if (ragdollCharacter.PropertyType.Name != "BaseHuman" || ragdollCharacter.GetMethod == null) throw new InvalidOperationException("Unexpected ragdoll character contract.");
                Ready = true; Update();
            }
            catch (Exception error) { Fail(error); }
        }

        public void Update()
        {
            if (!Ready) return;
            if (bridge != null) { UpdateThroughApi(bridge); return; }
            try
            {
                reserved.Clear(); pedestrians.Clear(); characters.Clear();
                object? director = socialInstance.GetValue(null);
                if (director != null && socialCast.GetValue(director) is object cast)
                {
                    var wears = (int[])castWears.GetValue(cast)!; var live = (bool[])castOut.GetValue(cast)!;
                    var bodies = (Array)castPeople.GetValue(cast)!; var people = (Array)socialPeople.GetValue(null)!;
                    if (wears.Length != 2 || live.Length != 2 || bodies.Length != 2 || people.Length > 1000) throw new InvalidOperationException("Unexpected social cast array shape.");
                    for (int slot = 0; slot < 2; slot++)
                    {
                        object? body = bodies.GetValue(slot);
                        if (body != null && cityCharacter.GetValue(body) is object character) characters.Add(character);
                        if (!live[slot]) continue;
                        int index = wears[slot];
                        if (index < 0 || index >= people.Length || people.GetValue(index) is not object who || (int)personIndex.GetValue(who)! != index)
                            throw new InvalidOperationException("Active social cast identity is missing.");
                        // Reviewed FlareInviteLinks.MeetOnline copies this tuple; SocialPerson
                        // has no persisted cIndex link. Do not infer identity from a name alone.
                        foreach (var source in sources)
                            if (source.Name == (string)personName.GetValue(who)! && source.Age == (int)personAge.GetValue(who)!
                                && source.Female == (bool)personFemale.GetValue(who)! && source.AppearanceSeed == (int)personSeed.GetValue(who)!)
                                reserved.Add(source.Id);
                    }
                }
                object? incidents = incidentsInstance.GetValue(null);
                if (incidents != null)
                    foreach (object? casualty in (IEnumerable)casualties.GetValue(incidents)!)
                    {
                        object? takeover = casualty == null ? null : casualtyTakeover.GetValue(casualty);
                        if (takeover != null && takeoverPedestrian.GetValue(takeover) is object pedestrian) pedestrians.Add(pedestrian);
                    }
                object? system = ragdollsInstance.GetValue(null);
                if (system != null)
                    foreach (object? ragdoll in (IEnumerable)ragdolls.GetValue(system)!)
                        if (ragdoll != null && (bool)ragdollActive.GetValue(ragdoll)! && ragdollCharacter.GetValue(ragdoll) is object character) characters.Add(character);
            }
            catch (Exception error) { Fail(error); }
        }
        // Ready and Off are complete answers. NotYet and Refreshing say nothing about what RLO holds, so that read
        // reserves everyone until a complete answer arrives. Shared, Failed or a broken answer stops the guard,
        // which then reserves everyone for good.
        private void UpdateThroughApi(RloBridge api)
        {
            try
            {
                RloBridge.Status people = api.ReadReservedCitizens(readIndexes);
                citizensKnown = people == RloBridge.Status.Ready || people == RloBridge.Status.Off;
                if (citizensKnown)
                {
                    reserved.Clear();
                    if (people == RloBridge.Status.Ready)
                        foreach (int index in readIndexes)
                        {
                            // Only original donors can be RLO's people; generated residents are never reserved by a donor index.
                            string id = "c" + index.ToString(CultureInfo.InvariantCulture);
                            if (sourceIds.Contains(id)) reserved.Add(id);
                        }
                }
                else if (people != RloBridge.Status.Refreshing && people != RloBridge.Status.NotYet)
                    throw new InvalidOperationException("ReadReservedCitizens: " + RloBridge.Describe(people));

                RloBridge.Status bodies = api.ReadReservedBodies(readPedestrians, readCharacters);
                bodiesKnown = bodies == RloBridge.Status.Ready || bodies == RloBridge.Status.Off;
                if (bodiesKnown)
                {
                    pedestrians.Clear(); characters.Clear();
                    if (bodies == RloBridge.Status.Ready)
                    {
                        foreach (object? body in readPedestrians) if (body != null) pedestrians.Add(body);
                        foreach (object? body in readCharacters) if (body != null) characters.Add(body);
                    }
                }
                else if (bodies != RloBridge.Status.Refreshing && bodies != RloBridge.Status.NotYet)
                    throw new InvalidOperationException("ReadReservedBodies: " + RloBridge.Describe(bodies));
            }
            catch (Exception error) { Fail(error); }
            finally { readIndexes.Clear(); readPedestrians.Clear(); readCharacters.Clear(); }
        }

        private void Fail(Exception error) { Ready = false; LastError = error.GetType().Name + ": " + error.Message; }
        private static Type Type(Assembly assembly, string name) => assembly.GetType("RealLifeOverhaul." + name, true)!;
        private static FieldInfo Field(Type type, string name, Type expected, bool isStatic = false)
        {
            var field = type.GetField(name, isStatic ? Static : Instance);
            if (field == null || field.FieldType != expected) throw new MissingFieldException(type.FullName, name);
            return field;
        }
        private static PropertyInfo Property(Type type, string name, Type expected, bool isStatic = false)
        {
            var property = type.GetProperty(name, isStatic ? Static : Instance);
            if (property == null || property.PropertyType != expected || property.GetMethod == null) throw new MissingMemberException(type.FullName, name);
            return property;
        }
        private sealed class References : IEqualityComparer<object>
        {
            public static readonly References Instance = new References();
            public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
