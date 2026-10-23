using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;

[assembly: InternalsVisibleTo("PersistentCity.Tests")]

namespace PersistentCity.Compatibility
{
    public enum RloReadStatus { Ready, MissingDependency, Incompatible, NotReady }

    public sealed class RloReadResult
    {
        internal RloReadResult(RloReadStatus status, string message, string version = "", string key = "", int seed = 0, IReadOnlyList<CitizenIdentity>? identities = null)
        { Status = status; Message = message; SourceVersion = version; PopulationKey = key; CitySeed = seed; Identities = identities ?? Array.Empty<CitizenIdentity>(); }
        public RloReadStatus Status { get; }
        public string Message { get; }
        public string SourceVersion { get; }
        public string PopulationKey { get; }
        public int CitySeed { get; }
        public IReadOnlyList<CitizenIdentity> Identities { get; }

        /// <summary>Checks a structurally validated snapshot against the actual current RLO identities.</summary>
        public bool MatchesPopulation(CityState state)
        {
            if (Status != RloReadStatus.Ready || state.PopulationKey != PopulationKey || state.Seed != CitySeed || state.SourceIdentities.Count != Identities.Count)
                return false;
            var expected = new Dictionary<string, CitizenIdentity>(StringComparer.Ordinal);
            foreach (var identity in Identities) expected.Add(identity.Id, identity);
            foreach (var actual in state.SourceIdentities)
            {
                if (!expected.TryGetValue(actual.Id, out var identity)
                    || actual.Name != identity.Name || actual.District != identity.District
                    || actual.Age != identity.Age || actual.Female != identity.Female
                    || actual.Persona != identity.Persona || actual.AppearanceSeed != identity.AppearanceSeed)
                    return false;
                expected.Remove(actual.Id);
            }
            return expected.Count == 0;
        }
    }

    /// <summary>Read-only, fail-closed contract for explicitly reviewed RLO releases. Call on the game's main thread.</summary>
    public sealed class RloIdentityAdapter
    {
        public const string ReviewedVersion = "0.41.1";
        public const string ReviewedCommit = "dbd15c0958770219c0f4d969a8b2ac28593014d2";
        private const string LegacyVersion = "0.40.4";
        private const string LegacyCommit = "46b3b8835e113ec3807344a9b7899439684004ad";
        private const string NewerVersion = "0.45.0";
        private const string NewerCommit = "b12d3581860ce380397223a2790fcce8b72b3482";
        internal const string ReviewedVersions = "0.40.4, 0.41.1 or 0.45.0";
        public const int ReviewedPopulation = 400;
        private const string Prefix = "RealLifeOverhaul.Flare.";
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

        public RloReadResult Read(Assembly? rloAssembly) => ReadCore(rloAssembly, null, false);
        public RloReadResult ReadSnapshot(Assembly? rloAssembly, object? director) => ReadCore(rloAssembly, director, true);

        private RloReadResult ReadCore(Assembly? assembly, object? director, bool supplied)
        {
            if (assembly == null) return new RloReadResult(RloReadStatus.MissingDependency, "RealLifeOverhaul is required and is not loaded.");
            string version = "";
            try
            {
                version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "";
                if (assembly.GetName().Name != "RealLifeOverhaul") throw new ContractException("The supplied assembly is not RealLifeOverhaul.");
                // A release with RLO's CityApi is read through it on any version. Without it, only reviewed releases are read.
                RloBridge? bridge = RloBridge.Find(assembly);
                if (bridge != null)
                {
                    if (supplied) throw new ContractException("A captured FlareDirector cannot be read through RLO's CityApi; use Read.");
                    return ReadBridge(bridge, version);
                }
                if (!IsReviewedAssembly(assembly)) throw new ContractException("RLO assembly/informational version pair has not been reviewed and has no CityApi; required: " + ReviewedVersions + " or CityApi contract " + RloBridge.SupportedContract + ".");
                return ReadContract(assembly, version, director, supplied);
            }
            catch (Exception e) { return Failure(e, version); }
        }

        internal static bool IsReviewedVersion(string version) =>
            VersionForm(version, ReviewedVersion, ReviewedCommit) || VersionForm(version, LegacyVersion, LegacyCommit)
            || VersionForm(version, NewerVersion, NewerCommit);

        internal static bool IsReviewedAssembly(Assembly? assembly)
        {
            if (assembly == null || assembly.GetName().Name != "RealLifeOverhaul") return false;
            string information = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            Version? version = assembly.GetName().Version;
            // Match each assembly version to its own reviewed informational version/commit.
            // A future release or a crossed old/new pair is not a compatible contract.
            return version == new Version(0, 41, 1, 0) && VersionForm(information, ReviewedVersion, ReviewedCommit)
                || version == new Version(0, 40, 4, 0) && VersionForm(information, LegacyVersion, LegacyCommit)
                || version == new Version(0, 45, 0, 0) && VersionForm(information, NewerVersion, NewerCommit);
        }

        private static bool VersionForm(string information, string version, string commit) =>
            information == version || information == version + ".0" || information == version + "+" + commit;

        // Separated for synthetic contract tests; the public entry points always check assembly identity/version first.
        internal RloReadResult ReadContract(Assembly assembly, string version, object? director, bool supplied)
        {
            try
            {
                Type directorType = RequiredType(assembly, "FlareDirector");
                Type citizenType = RequiredType(assembly, "FlareCitizen");
                Type fileType = RequiredType(assembly, "FlareFile");
                Type rules = RequiredType(assembly, "FlareLiveRules");
                Type avatarRules = RequiredType(assembly, "FlareAvatarRules");
                Type avatarType = RequiredType(assembly, "FlareAvatarPlan");
                Type personaType = RequiredType(assembly, "FlarePersona");
                Type multiplayer = assembly.GetType("RealLifeOverhaul.GoingPublic", false) ?? throw new ContractException("Missing public contract type: RealLifeOverhaul.GoingPublic.");
                PropertyInfo inSession = Property(multiplayer, "InSession", typeof(bool), true);
                if (!personaType.IsEnum || Enum.GetUnderlyingType(personaType) != typeof(int)) throw new ContractException("FlarePersona must be an Int32 enum.");
                foreach (Persona p in Enum.GetValues(typeof(Persona)))
                    if (!Enum.IsDefined(personaType, p.ToString()) || Convert.ToInt32(Enum.Parse(personaType, p.ToString()), CultureInfo.InvariantCulture) != (int)p)
                        throw new ContractException("FlarePersona values differ from the reviewed mapping.");
                if (Enum.GetNames(personaType).Length != Enum.GetNames(typeof(Persona)).Length) throw new ContractException("FlarePersona has unreviewed values.");

                PropertyInfo instance = Property(directorType, "Instance", directorType, true);
                PropertyInfo running = Property(directorType, "Running", typeof(bool));
                PropertyInfo file = Property(directorType, "File", fileType);
                PropertyInfo citizens = Property(directorType, "Citizens", typeof(IReadOnlyList<>).MakeGenericType(citizenType));
                FieldInfo count = Field(rules, "Citizens", typeof(int), true);
                if (!count.IsLiteral || (int)count.GetRawConstantValue()! != ReviewedPopulation) throw new ContractException("RLO population constant differs from reviewed 400.");
                MethodInfo citySeed = Method(rules, "CitySeed", typeof(int), fileType);
                MethodInfo avatar = Method(avatarRules, "Citizen", avatarType, typeof(int), typeof(int));
                FieldInfo index = Field(citizenType, "Index", typeof(int));
                FieldInfo first = Field(citizenType, "First", typeof(string));
                FieldInfo last = Field(citizenType, "Last", typeof(string));
                FieldInfo district = Field(citizenType, "District", typeof(string));
                FieldInfo age = Field(citizenType, "Age", typeof(int));
                FieldInfo female = Field(citizenType, "Female", typeof(bool));
                FieldInfo persona = Field(citizenType, "Persona", personaType);
                FieldInfo lookSeed = Field(avatarType, "Seed", typeof(int));
                FieldInfo lookAge = Field(avatarType, "Age", typeof(int));
                FieldInfo lookFemale = Field(avatarType, "Female", typeof(bool));

                if ((bool)inSession.GetValue(null)!) return NotReady("Multiplayer sessions are unsupported by Persistent City v0.1; leave the Going Public session first.", version);
                if (!supplied) director = instance.GetValue(null);
                if (director == null) return NotReady("FlareDirector.Instance is not available yet.", version);
                if (director.GetType() != directorType) throw new ContractException("Director snapshot is not the reviewed RLO type.");
                if (!(bool)running.GetValue(director)!) return NotReady("Flare is not running; enable RLO's Social Media option and load a city with Flare available.", version);
                object? liveFile = file.GetValue(director);
                if (liveFile == null) return NotReady("Flare's current account is not available yet.", version);
                int seed = (int)citySeed.Invoke(null, new[] { liveFile })!;
                if (seed == 0) return NotReady("Flare has no initialized city seed yet.", version);
                if (!(citizens.GetValue(director) is IEnumerable population)) throw new ContractException("Citizens is not enumerable.");
                var identities = new List<CitizenIdentity>(ReviewedPopulation);
                var ids = new HashSet<int>();
                foreach (object c in population)
                {
                    if (identities.Count >= ReviewedPopulation) throw new ContractException("Citizens exceeds reviewed 400.");
                    if (c == null || c.GetType() != citizenType) throw new ContractException("Citizens contains an unexpected type.");
                    int id = (int)index.GetValue(c)!;
                    if (id < 0 || id >= ReviewedPopulation || !ids.Add(id)) throw new ContractException("Citizens has a duplicate or out-of-range Index.");
                    string? given = (string?)first.GetValue(c), family = (string?)last.GetValue(c);
                    if (string.IsNullOrWhiteSpace(given) || string.IsNullOrWhiteSpace(family)) throw new ContractException("A citizen has no complete RLO name.");
                    int years = (int)age.GetValue(c)!;
                    bool sex = (bool)female.GetValue(c)!;
                    int character = Convert.ToInt32(persona.GetValue(c), CultureInfo.InvariantCulture);
                    if (years < 1 || years > 130 || !Enum.IsDefined(typeof(Persona), character)) throw new ContractException("A citizen has an invalid age or persona.");
                    object plan = avatar.Invoke(null, new object[] { seed, id })!;
                    int appearance = (int)lookSeed.GetValue(plan)!;
                    if (appearance == 0 || (int)lookAge.GetValue(plan)! != years || (bool)lookFemale.GetValue(plan)! != sex)
                        return NotReady("Current citizen age/sex and city avatar seed disagree; wait for RLO's city refresh.", version);
                    identities.Add(new CitizenIdentity { Id = "c" + id.ToString(CultureInfo.InvariantCulture), Name = given + " " + family,
                        District = (string?)district.GetValue(c) ?? "", Age = years, Female = sex, Persona = (Persona)character, AppearanceSeed = appearance });
                }
                if (identities.Count == 0) return NotReady("Flare's actual citizen population is not initialized yet.", version);
                if (identities.Count != ReviewedPopulation) throw new ContractException("Citizens must contain the reviewed 400 identities; found " + identities.Count + ".");
                if (!(bool)running.GetValue(director)! || !ReferenceEquals(liveFile, file.GetValue(director)) || seed != (int)citySeed.Invoke(null, new[] { liveFile })!)
                    return NotReady("RLO's current account changed during the read; retry on the main thread.", version);
                identities.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
                return new RloReadResult(RloReadStatus.Ready, "Read 400 actual RLO citizens through the reviewed reflection contract.", version, PopulationHash(seed, identities), seed, identities.AsReadOnly());
            }
            catch (Exception e) { return Failure(e, version); }
        }

        // Separated for tests with a stand-in CityApi; Read reaches it only for an assembly named RealLifeOverhaul.
        internal RloReadResult ReadBridge(RloBridge bridge, string version)
        {
            try
            {
                var read = new List<RloBridgeCitizen>(ReviewedPopulation);
                RloBridge.Status status = bridge.ReadCitizens(read, out int seed);
                if (status == RloBridge.Status.Failed) throw new ContractException(RloBridge.Describe(status));
                if (status != RloBridge.Status.Ready) return NotReady(RloBridge.Describe(status), version);
                if (read.Count == 0) return NotReady("Flare's actual citizen population is not initialized yet.", version);
                if (seed == 0) return NotReady("Flare has no initialized city seed yet.", version);
                // Persistent City's own donor requirements; the contract itself does not fix the count.
                if (read.Count != ReviewedPopulation) throw new ContractException("Citizens must contain the reviewed 400 identities; found " + read.Count + ".");
                var identities = new List<CitizenIdentity>(ReviewedPopulation);
                var ids = new HashSet<int>();
                foreach (var c in read)
                {
                    if (c.Index < 0 || c.Index >= ReviewedPopulation || !ids.Add(c.Index)) throw new ContractException("Citizens has a duplicate or out-of-range Index.");
                    if (string.IsNullOrWhiteSpace(c.First) || string.IsNullOrWhiteSpace(c.Last)) throw new ContractException("A citizen has no complete RLO name.");
                    if (c.Age < 1 || c.Age > 130 || !RloBridge.TryPersona(c.Persona, out Persona character)) throw new ContractException("A citizen has an invalid age or persona.");
                    if (c.AppearanceSeed == 0) throw new ContractException("A citizen has no appearance seed.");
                    identities.Add(new CitizenIdentity { Id = "c" + c.Index.ToString(CultureInfo.InvariantCulture), Name = c.First + " " + c.Last,
                        District = c.District ?? "", Age = c.Age, Female = c.Female, Persona = character, AppearanceSeed = c.AppearanceSeed });
                }
                identities.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
                return new RloReadResult(RloReadStatus.Ready, "Read 400 actual RLO citizens through RLO's CityApi contract " + bridge.ContractVersion + ".", version,
                    PopulationHash(seed, identities), seed, identities.AsReadOnly());
            }
            catch (Exception e) { return Failure(e, version); }
        }

        private static string PopulationHash(int seed, List<CitizenIdentity> identities)
        {
            // Length-prefixed UTF-8 strings prevent ambiguous concatenation; fields and order are versioned.
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write("persistentcity:rlo-population:v1"); writer.Write(seed); writer.Write(identities.Count);
                foreach (var c in identities) { writer.Write(c.Id); writer.Write(c.Name); writer.Write(c.District); writer.Write(c.Age); writer.Write(c.Female); writer.Write((int)c.Persona); writer.Write(c.AppearanceSeed); }
            }
            using var hash = SHA256.Create();
            // This is the original donor-contract namespace, not the installed RLO version. Reviewed 0.41.1 and 0.45.0 have
            // identical donor/appearance algorithms, and CityApi contract 1 promises the same citizens; retain existing save keys.
            return "rlo:0.40.4:" + BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        private static Type RequiredType(Assembly assembly, string name)
        {
            var type = assembly.GetType(Prefix + name, false);
            if (type == null || !type.IsPublic) throw new ContractException("Missing public contract type: " + Prefix + name + ".");
            return type;
        }
        private static PropertyInfo Property(Type type, string name, Type expected, bool isStatic = false)
        {
            var p = type.GetProperty(name, isStatic ? PublicStatic : PublicInstance);
            if (p == null || p.PropertyType != expected || p.GetMethod == null || !p.GetMethod.IsPublic || p.GetIndexParameters().Length != 0)
                throw new ContractException("Missing or changed public property: " + type.Name + "." + name + ".");
            return p;
        }
        private static FieldInfo Field(Type type, string name, Type expected, bool isStatic = false)
        {
            var f = type.GetField(name, isStatic ? PublicStatic : PublicInstance);
            if (f == null || f.FieldType != expected) throw new ContractException("Missing or changed public field: " + type.Name + "." + name + ".");
            return f;
        }
        private static MethodInfo Method(Type type, string name, Type returns, params Type[] parameters)
        {
            var method = type.GetMethod(name, PublicStatic, null, parameters, null);
            if (method == null || method.ReturnType != returns) throw new ContractException("Missing or changed public method: " + type.Name + "." + name + ".");
            return method;
        }
        private static RloReadResult NotReady(string message, string version) => new RloReadResult(RloReadStatus.NotReady, message, version);
        private static RloReadResult Failure(Exception exception, string version)
        {
            Exception detail = exception is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : exception;
            return new RloReadResult(RloReadStatus.Incompatible, detail.GetType().Name + ": " + detail.Message, version);
        }
        private sealed class ContractException : Exception { public ContractException(string message) : base(message) { } }
    }
}
