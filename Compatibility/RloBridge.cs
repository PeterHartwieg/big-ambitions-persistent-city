using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    /// <summary>
    /// RLO's public read contract for other mods (<c>RealLifeOverhaul.ModApi.CityApi</c>), bound by reflection.
    /// The contract carries its own version, so any RLO release with a supported version is accepted without review.
    /// See docs/rlo-bridge.md. Main thread only.
    /// </summary>
    internal sealed class RloBridge
    {
        public const string ApiTypeName = "RealLifeOverhaul.ModApi.CityApi";
        public const int SupportedContract = 1;

        internal enum Status { Ready = 0, Off = 1, NotYet = 2, Refreshing = 3, Shared = 4, Failed = 5 }

        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private readonly MethodInfo citizens, reservedCitizens, reservedBodies, slowedLanes;
        private readonly PropertyInfo version;
        private readonly Type citizenType;
        private readonly FieldInfo index, first, last, district, age, female, persona, appearance;
        private readonly object[] citizenArgs = new object[2], indexArgs = new object[1], bodyArgs = new object[2], laneArgs = new object[3];

        public int ContractVersion { get; }

        private RloBridge(Type api)
        {
            string space = api.Namespace + ".";
            Type status = api.Assembly.GetType(space + "CityApiStatus", false) ?? throw Invalid("missing public type CityApiStatus");
            citizenType = api.Assembly.GetType(space + "CityApiCitizen", false) ?? throw Invalid("missing public type CityApiCitizen");
            if (!api.IsPublic || !api.IsAbstract || !api.IsSealed) throw Invalid("CityApi must be a public static class");
            if (!status.IsPublic || !status.IsEnum || Enum.GetUnderlyingType(status) != typeof(int)) throw Invalid("CityApiStatus must be a public Int32 enum");
            foreach (Status known in Enum.GetValues(typeof(Status)))
                if (!Enum.IsDefined(status, known.ToString()) || Convert.ToInt32(Enum.Parse(status, known.ToString()), CultureInfo.InvariantCulture) != (int)known)
                    throw Invalid("CityApiStatus." + known + " is missing or has another value");
            if (!citizenType.IsPublic || !citizenType.IsClass) throw Invalid("CityApiCitizen must be a public class");

            var property = api.GetProperty("ContractVersion", PublicStatic);
            if (property == null || property.PropertyType != typeof(int) || property.GetMethod == null || !property.GetMethod.IsPublic || property.GetIndexParameters().Length != 0)
                throw Invalid("missing public static int CityApi.ContractVersion");
            version = property;
            ContractVersion = (int)version.GetValue(null)!;
            if (ContractVersion != SupportedContract) throw Unsupported(ContractVersion);

            index = Field("Index", typeof(int)); first = Field("First", typeof(string)); last = Field("Last", typeof(string));
            district = Field("District", typeof(string)); age = Field("Age", typeof(int)); female = Field("Female", typeof(bool));
            persona = Field("Persona", typeof(string)); appearance = Field("AppearanceSeed", typeof(int));
            citizens = Method(api, "ReadCitizens", status, typeof(List<>).MakeGenericType(citizenType), typeof(int).MakeByRefType());
            if (!citizens.GetParameters()[1].IsOut) throw Invalid("CityApi.ReadCitizens citySeed must be an out parameter");
            reservedCitizens = Method(api, "ReadReservedCitizens", status, typeof(List<int>));
            reservedBodies = Method(api, "ReadReservedBodies", status, typeof(List<object>), typeof(List<object>));
            slowedLanes = Method(api, "ReadSlowedLanes", status, typeof(List<object>), typeof(List<int>), typeof(List<int>));
            citizenArgs[0] = Activator.CreateInstance(typeof(List<>).MakeGenericType(citizenType))!;
        }

        /// <summary>Null when the assembly has no CityApi (an RLO release from before the contract). Throws when it has
        /// one that Persistent City cannot use; that release is then incompatible, with no reflection fallback.</summary>
        public static RloBridge? Find(Assembly assembly)
        {
            Type? api = assembly.GetType(ApiTypeName, false);
            return api == null ? null : Bind(api);
        }

        // Tests bind a stand-in API from their own assembly; the game always goes through Find.
        internal static RloBridge Bind(Type api)
        {
            try { return new RloBridge(api); }
            catch (Exception error) when (!(error is InvalidOperationException)) { throw Invalid("could not bind", error); }
        }

        /// <summary>Asks RLO for its contract version again, as a fresh binding would. Throws, as binding does, if it has changed.</summary>
        public void CheckContractVersion()
        {
            int current;
            try { current = (int)version.GetValue(null)!; }
            catch (Exception error) { throw Invalid("could not bind", error); }
            if (current != SupportedContract) throw Unsupported(current);
        }

        /// <summary>Copies the citizens into Persistent City identities, in RLO's order. Ready leaves <paramref name="read"/> filled.</summary>
        public Status ReadCitizens(List<RloBridgeCitizen> read, out int citySeed)
        {
            read.Clear();
            citizenArgs[1] = 0;
            Status status = Call(citizens, citizenArgs);
            citySeed = (int)citizenArgs[1];
            var list = (IList)citizenArgs[0];
            try
            {
                if (status != Status.Ready) return status;
                foreach (object? c in list)
                {
                    if (c == null || c.GetType() != citizenType) throw Invalid("ReadCitizens returned an unexpected element");
                    read.Add(new RloBridgeCitizen((int)index.GetValue(c)!, (string?)first.GetValue(c), (string?)last.GetValue(c), (string?)district.GetValue(c),
                        (int)age.GetValue(c)!, (bool)female.GetValue(c)!, (string?)persona.GetValue(c), (int)appearance.GetValue(c)!));
                }
                return status;
            }
            finally { list.Clear(); }
        }

        public Status ReadReservedCitizens(List<int> indexes) { indexArgs[0] = indexes; return Call(reservedCitizens, indexArgs); }

        public Status ReadReservedBodies(List<object> pedestrians, List<object> characters)
        { bodyArgs[0] = pedestrians; bodyArgs[1] = characters; return Call(reservedBodies, bodyArgs); }

        public Status ReadSlowedLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits)
        { laneArgs[0] = lanes; laneArgs[1] = originalLimits; laneArgs[2] = currentLimits; return Call(slowedLanes, laneArgs); }

        /// <summary>Core's persona for one of the contract's persona names, or false for any other text (numbers included).</summary>
        public static bool TryPersona(string? name, out Persona value)
        {
            foreach (Persona p in Enum.GetValues(typeof(Persona)))
                if (string.Equals(p.ToString(), name, StringComparison.Ordinal)) { value = p; return true; }
            value = default;
            return false;
        }

        public static string Describe(Status status)
        {
            switch (status)
            {
                case Status.Off: return "RLO's Flare is switched off or not running; enable RLO's Social Media option and load a city with Flare available.";
                case Status.NotYet: return "RLO has no Flare account, city seed or citizens yet.";
                case Status.Refreshing: return "RLO is refreshing the city's citizens; retry on the main thread.";
                case Status.Shared: return "Multiplayer sessions are unsupported by Persistent City v0.1; leave the Going Public session first.";
                case Status.Failed: return "RLO's CityApi reported a fault.";
                default: return "Ready.";
            }
        }

        private static Status Call(MethodInfo method, object[] args)
        {
            object? result;
            try { result = method.Invoke(null, args); }
            catch (TargetInvocationException error) { throw Invalid(method.Name + " threw, which the contract forbids", error.InnerException ?? error); }
            int value = Convert.ToInt32(result, CultureInfo.InvariantCulture);
            if (!Enum.IsDefined(typeof(Status), value)) throw Invalid(method.Name + " returned unknown status " + value);
            return (Status)value;
        }

        private FieldInfo Field(string name, Type expected)
        {
            var field = citizenType.GetField(name, PublicInstance);
            if (field == null || field.FieldType != expected) throw Invalid("missing or changed field CityApiCitizen." + name);
            return field;
        }

        private static MethodInfo Method(Type api, string name, Type returns, params Type[] parameters)
        {
            var method = api.GetMethod(name, PublicStatic, null, parameters, null);
            if (method == null || method.ReturnType != returns) throw Invalid("missing or changed method CityApi." + name);
            return method;
        }

        private static InvalidOperationException Unsupported(int contract) =>
            Invalid("contract " + contract + " is not supported; Persistent City reads contract " + SupportedContract);

        internal static InvalidOperationException Invalid(string message, Exception? inner = null) =>
            new InvalidOperationException("RLO CityApi: " + message + ".", inner);
    }

    internal readonly struct RloBridgeCitizen
    {
        public RloBridgeCitizen(int index, string? first, string? last, string? district, int age, bool female, string? persona, int appearanceSeed)
        { Index = index; First = first; Last = last; District = district; Age = age; Female = female; Persona = persona; AppearanceSeed = appearanceSeed; }
        public int Index { get; }
        public string? First { get; }
        public string? Last { get; }
        public string? District { get; }
        public int Age { get; }
        public bool Female { get; }
        public string? Persona { get; }
        public int AppearanceSeed { get; }
    }
}
