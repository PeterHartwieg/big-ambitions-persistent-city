using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PersistentCity.Core
{
    // F8c (docs/plans/f8-capability-boundaries.md D3): exact native member contracts, checked completely before the first
    // patch, ownership request or native call. Pure reflection over the types the caller supplies, so tests check it
    // against stand-in types without game assemblies.

    public enum NativeMemberKind : byte { Method, Field }

    /// <summary>One native member with its exact shape: parameter list, return type and static-ness, or field type.</summary>
    public sealed class NativeMemberSpec
    {
        private NativeMemberSpec(NativeMemberKind kind, Type declaring, string name, Type valueType, bool isStatic, Type[] parameters)
        { Kind = kind; Declaring = declaring; Name = name; ValueType = valueType; IsStatic = isStatic; parameters_ = parameters; }
        private readonly Type[] parameters_;
        public NativeMemberKind Kind { get; }
        public Type Declaring { get; }
        public string Name { get; }
        /// <summary>The return type of a method or the type of a field.</summary>
        public Type ValueType { get; }
        public bool IsStatic { get; }
        public IReadOnlyList<Type> Parameters => parameters_;

        public static NativeMemberSpec Method(Type declaring, string name, Type returns, bool isStatic, params Type[] parameters)
            => new NativeMemberSpec(NativeMemberKind.Method, declaring ?? throw new ArgumentNullException(nameof(declaring)), name, returns, isStatic, parameters ?? Type.EmptyTypes);
        public static NativeMemberSpec Field(Type declaring, string name, Type fieldType, bool isStatic = false)
            => new NativeMemberSpec(NativeMemberKind.Field, declaring ?? throw new ArgumentNullException(nameof(declaring)), name, fieldType, isStatic, Type.EmptyTypes);

        /// <summary>The same member expected under another name (a renamed member, as a fault point simulates it).</summary>
        public NativeMemberSpec Renamed(string name) => new NativeMemberSpec(Kind, Declaring, name, ValueType, IsStatic, parameters_);
        /// <summary>The same method expected with one more trailing parameter (another signature).</summary>
        public NativeMemberSpec WithExtraParameter(Type parameter) => new NativeMemberSpec(Kind, Declaring, Name, ValueType, IsStatic, parameters_.Concat(new[] { parameter }).ToArray());

        public string Label => Declaring.Name + "." + Name + (Kind == NativeMemberKind.Method ? "(" + string.Join(", ", parameters_.Select(p => p.Name)) + ")" : "");
    }

    public static class NativeMemberContract
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>Null when the member exists with exactly the expected shape; otherwise a named refusal. The member is
        /// looked up on the declaring type and its base types, as Harmony's AccessTools does.</summary>
        public static string? Check(NativeMemberSpec spec, out MemberInfo? member)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            member = null;
            if (spec.Kind == NativeMemberKind.Field)
            {
                FieldInfo? field = null;
                for (Type? t = spec.Declaring; t != null && field == null; t = t.BaseType) field = t.GetField(spec.Name, Any);
                if (field == null) return spec.Label + " is missing.";
                if (field.FieldType != spec.ValueType) return spec.Label + " changed: field type " + field.FieldType.Name + ", expected " + spec.ValueType.Name + ".";
                if (field.IsStatic != spec.IsStatic) return spec.Label + " changed: " + (field.IsStatic ? "static" : "instance") + " field.";
                member = field; return null;
            }
            MethodInfo? method = null;
            var others = new List<MethodInfo>();
            for (Type? t = spec.Declaring; t != null && method == null; t = t.BaseType)
                foreach (var candidate in t.GetMethods(Any))
                {
                    if (!string.Equals(candidate.Name, spec.Name, StringComparison.Ordinal)) continue;
                    if (SameParameters(candidate, spec.Parameters)) { method = candidate; break; }
                    others.Add(candidate);
                }
            if (method == null)
                return others.Count == 0 ? spec.Label + " is missing."
                    : spec.Label + " changed: no method with this parameter list (found " + string.Join("; ", others.Select(Describe)) + ").";
            if (method.ReturnType != spec.ValueType) return spec.Label + " changed: returns " + method.ReturnType.Name + ", expected " + spec.ValueType.Name + ".";
            if (method.IsStatic != spec.IsStatic) return spec.Label + " changed: " + (method.IsStatic ? "static" : "instance") + " method.";
            if (method.IsGenericMethodDefinition || method.IsAbstract) return spec.Label + " changed: generic or abstract method.";
            member = method; return null;
        }

        /// <summary>Checks every spec; the first refusal in list order, or null. <paramref name="resolved"/> holds the
        /// members that passed, by index (null where a member failed), so one failure does not hide which others resolved.</summary>
        public static string? CheckAll(IReadOnlyList<NativeMemberSpec> specs, out MemberInfo?[] resolved)
        {
            resolved = new MemberInfo?[specs.Count];
            string? first = null;
            for (int i = 0; i < specs.Count; i++)
            {
                string? refusal = Check(specs[i], out resolved[i]);
                first ??= refusal;
            }
            return first;
        }

        private static bool SameParameters(MethodInfo method, IReadOnlyList<Type> expected)
        {
            var actual = method.GetParameters();
            if (actual.Length != expected.Count) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i].ParameterType != expected[i]) return false;
            return true;
        }

        private static string Describe(MethodInfo method)
            => method.Name + "(" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name)) + ")";
    }

    /// <summary>F8c checkout-off mode, as NativeFoodMarket applies it: with a refused checkout contract no shop is eligible
    /// (so every refresh observes it ineligible and the release path hands owned shops back), no shop is served, the price
    /// prefix is not installed and every shop shows the scope reason.</summary>
    public sealed class FoodCheckoutMode
    {
        public FoodCheckoutMode(string? refusal) { Refusal = refusal; }
        public string? Refusal { get; }
        /// <summary>SupportedMenu may admit a shop (the eligibility NativeFoodOwnership.Observe receives).</summary>
        public bool Eligible => Refusal == null;
        /// <summary>CanServe may answer true for an owned, serving shop.</summary>
        public bool Serves => Refusal == null;
        public bool InstallsPricePrefix => Refusal == null;
        public string? ScopeReason => Refusal == null ? null : NativeFoodContractSpec.CheckoutScopeReason(Refusal);
        /// <summary>The eligibility one refresh passes for a shop whose own menu checks gave <paramref name="supported"/>.</summary>
        public bool Observe(bool supported) => Eligible && supported;
    }

    /// <summary>The game types the food-economy contract names. Runtime passes the real types; tests pass stand-ins.</summary>
    public sealed class NativeFoodTypes
    {
        public Type GameManager { get; set; } = typeof(object);
        public Type RetailBusinessSimulator { get; set; } = typeof(object);
        public Type BusinessSimulator { get; set; } = typeof(object);
        public Type CustomerEntriesHelper { get; set; } = typeof(object);
        public Type CustomerEntry { get; set; } = typeof(object);
        public Type BuildingRegistration { get; set; } = typeof(object);
        public Type Address { get; set; } = typeof(object);
        public Type DayOfWeek { get; set; } = typeof(object);
        public Type CitizenData { get; set; } = typeof(object);
        public Type ItemInstance { get; set; } = typeof(object);
        public Type PointOfSale { get; set; } = typeof(object);
    }

    /// <summary>D3 for H1, H2 and FoodSupplyReader: the members, with the shapes read from the installed game at the F8c
    /// base. <see cref="Authority"/> is food.authority (required: the six food-authority hook targets and the two
    /// BusinessSimulator fields they read); <see cref="Checkout"/> is food.checkout.shop (optional per shop).</summary>
    public static class NativeFoodContractSpec
    {
        public const int AuthorityTargets = 6;

        public static NativeMemberSpec[] Authority(NativeFoodTypes t) => new[]
        {
            NativeMemberSpec.Method(t.GameManager, "RunMainGameTick", typeof(void), false, typeof(float)),
            NativeMemberSpec.Method(t.RetailBusinessSimulator, "ProcessAllCustomersFromThisHour", typeof(void), false),
            NativeMemberSpec.Method(t.RetailBusinessSimulator, "ProcessCustomer", typeof(void), false, t.CustomerEntry),
            NativeMemberSpec.Method(t.CustomerEntriesHelper, "GetEntriesByAddress", typeof(List<>).MakeGenericType(t.CustomerEntry), true, t.Address),
            NativeMemberSpec.Method(t.CustomerEntriesHelper, "UpdateCustomerEntriesForPlayerBusiness", typeof(void), true, t.BuildingRegistration, t.DayOfWeek),
            NativeMemberSpec.Method(t.BuildingRegistration, "Reset", typeof(void), false, typeof(bool)),
            NativeMemberSpec.Field(t.BusinessSimulator, "buildingRegistration", t.BuildingRegistration),
            NativeMemberSpec.Field(t.BusinessSimulator, "currentHour", typeof(int)),
        };

        public const int PriceCheck = 0, ProcessCustomer = 1, Producers = 2, Counters = 3, Capacity = 4;

        public static NativeMemberSpec[] Checkout(NativeFoodTypes t) => new[]
        {
            NativeMemberSpec.Method(t.CitizenData, "IsPriceAcceptable", typeof(bool), false, typeof(string), typeof(float)),
            NativeMemberSpec.Method(t.RetailBusinessSimulator, "ProcessCustomer", typeof(void), false, t.CustomerEntry),
            NativeMemberSpec.Field(t.RetailBusinessSimulator, "availableProducers", typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(List<>).MakeGenericType(t.ItemInstance))),
            NativeMemberSpec.Field(t.RetailBusinessSimulator, "pointOfSales", typeof(List<>).MakeGenericType(t.PointOfSale)),
            NativeMemberSpec.Method(t.RetailBusinessSimulator, "GetMaxCustomerCapacityThisHour", typeof(int), false),
        };

        /// <summary>The start refusal for a failed required contract: a capability refusal, not a damaged save and not an older city.</summary>
        public static string AuthorityStartMessage(string refusal)
        {
            var registry = new CapabilityRegistry();
            registry.Detect(CapabilityId.FoodAuthority, CapabilityState.Off, refusal);
            return registry.Start().Message;
        }

        /// <summary>The scope reason a shop shows while food.checkout.shop is off.</summary>
        public static string CheckoutScopeReason(string refusal) => "Excluded: food checkout contract off: " + refusal;
    }
}
