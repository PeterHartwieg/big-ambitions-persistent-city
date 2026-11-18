using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    internal sealed class RloBridge
    {
        public const string ApiTypeName = "RealLifeOverhaul.ModApi.CityApi";
        public const int SupportedContract = 1;

        internal enum Status { Ready = 0, Off = 1, NotYet = 2, Refreshing = 3, Shared = 4, Failed = 5 }

        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private delegate int CitizensCall(object into, out int citySeed);
        private readonly CitizensCall citizens;
        private readonly Func<List<int>, int> reservedCitizens;
        private readonly Func<List<object>, List<object>, int> reservedBodies;
        private readonly Func<List<object>, List<int>, List<int>, int> slowedLanes;
        private readonly PropertyInfo version;
        private readonly Type citizenType;
        private readonly Func<object, int> index, age, appearance;
        private readonly Func<object, bool> female;
        private readonly Func<object, string?> first, last, district, persona;
        private readonly IList citizenBuffer;

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

            index = Reader<int>(Field("Index", typeof(int))); first = Reader<string?>(Field("First", typeof(string)));
            last = Reader<string?>(Field("Last", typeof(string))); district = Reader<string?>(Field("District", typeof(string)));
            age = Reader<int>(Field("Age", typeof(int))); female = Reader<bool>(Field("Female", typeof(bool)));
            persona = Reader<string?>(Field("Persona", typeof(string))); appearance = Reader<int>(Field("AppearanceSeed", typeof(int)));
            var citizenList = typeof(List<>).MakeGenericType(citizenType);
            var readCitizens = Method(api, "ReadCitizens", status, citizenList, typeof(int).MakeByRefType());
            if (!readCitizens.GetParameters()[1].IsOut) throw Invalid("CityApi.ReadCitizens citySeed must be an out parameter");
            citizens = (CitizensCall)Call(readCitizens, typeof(CitizensCall), typeof(object), typeof(int).MakeByRefType());
            reservedCitizens = (Func<List<int>, int>)Call(Method(api, "ReadReservedCitizens", status, typeof(List<int>)), typeof(Func<List<int>, int>), typeof(List<int>));
            reservedBodies = (Func<List<object>, List<object>, int>)Call(Method(api, "ReadReservedBodies", status, typeof(List<object>), typeof(List<object>)),
                typeof(Func<List<object>, List<object>, int>), typeof(List<object>), typeof(List<object>));
            slowedLanes = (Func<List<object>, List<int>, List<int>, int>)Call(Method(api, "ReadSlowedLanes", status, typeof(List<object>), typeof(List<int>), typeof(List<int>)),
                typeof(Func<List<object>, List<int>, List<int>, int>), typeof(List<object>), typeof(List<int>), typeof(List<int>));
            citizenBuffer = (IList)Activator.CreateInstance(citizenList)!;
        }

        public static RloBridge? Find(Assembly assembly)
        {
            Type? api = assembly.GetType(ApiTypeName, false);
            return api == null ? null : Bind(api);
        }

        internal static RloBridge Bind(Type api)
        {
            try { return new RloBridge(api); }
            catch (Exception error) when (!(error is InvalidOperationException)) { throw Invalid("could not bind", error); }
        }

        public void CheckContractVersion()
        {
            int current;
            try { current = (int)version.GetValue(null)!; }
            catch (Exception error) { throw Invalid("could not bind", error); }
            if (current != SupportedContract) throw Unsupported(current);
        }

        public Status ReadCitizens(List<RloBridgeCitizen> read, out int citySeed)
        {
            read.Clear();
            int seed = 0, value;
            var list = citizenBuffer;
            try
            {
                try { value = citizens(list, out seed); }
                catch (Exception error) { throw Threw("ReadCitizens", error); }
                citySeed = seed;
                Status status = Checked("ReadCitizens", value);
                if (status != Status.Ready) return status;
                for (int i = 0; i < list.Count; i++)
                {
                    object? c = list[i];
                    if (c == null || c.GetType() != citizenType) throw Invalid("ReadCitizens returned an unexpected element");
                    read.Add(new RloBridgeCitizen(index(c), first(c), last(c), district(c), age(c), female(c), persona(c), appearance(c)));
                }
                return status;
            }
            finally { list.Clear(); }
        }

        public Status ReadReservedCitizens(List<int> indexes)
        {
            int value;
            try { value = reservedCitizens(indexes); }
            catch (Exception error) { throw Threw("ReadReservedCitizens", error); }
            return Checked("ReadReservedCitizens", value);
        }

        public Status ReadReservedBodies(List<object> pedestrians, List<object> characters)
        {
            int value;
            try { value = reservedBodies(pedestrians, characters); }
            catch (Exception error) { throw Threw("ReadReservedBodies", error); }
            return Checked("ReadReservedBodies", value);
        }

        public Status ReadSlowedLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits)
        {
            int value;
            try { value = slowedLanes(lanes, originalLimits, currentLimits); }
            catch (Exception error) { throw Threw("ReadSlowedLanes", error); }
            return Checked("ReadSlowedLanes", value);
        }

        private static readonly Persona[] PersonaValues = (Persona[])Enum.GetValues(typeof(Persona));
        private static readonly string[] PersonaNames = Array.ConvertAll(PersonaValues, p => p.ToString());

        public static bool TryPersona(string? name, out Persona value)
        {
            for (int i = 0; i < PersonaNames.Length; i++)
                if (string.Equals(PersonaNames[i], name, StringComparison.Ordinal)) { value = PersonaValues[i]; return true; }
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

        private static Exception Threw(string name, Exception error) => Invalid(name + " threw, which the contract forbids", error);

        private static Status Checked(string name, int value)
        {
            if (value < (int)Status.Ready || value > (int)Status.Failed) throw Invalid(name + " returned unknown status " + value);
            return (Status)value;
        }

        private static Delegate Call(MethodInfo target, Type delegateType, params Type[] parameters)
        {
            var method = new DynamicMethod("PersistentCityRlo" + target.Name, typeof(int), parameters, typeof(RloBridge).Module, true);
            var il = method.GetILGenerator();
            var expected = target.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, (short)i);
                if (parameters[i] != expected[i].ParameterType) il.Emit(OpCodes.Castclass, expected[i].ParameterType);
            }
            il.Emit(OpCodes.Call, target);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(delegateType);
        }

        private static Func<object, T> Reader<T>(FieldInfo field)
        {
            var method = new DynamicMethod("PersistentCityRloRead" + field.Name, typeof(T), new[] { typeof(object) }, typeof(RloBridge).Module, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, field.DeclaringType!); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
            return (Func<object, T>)method.CreateDelegate(typeof(Func<object, T>));
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
