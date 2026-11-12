using System;
using System.Reflection;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    internal sealed class RloLivingPopulation
    {
        public const string AssemblyName = "RealLifeOverhaul";
        public const string SettingsTypeName = "RealLifeOverhaul.Settings";
        public const string FieldName = "LivingPopulation";
        public const string PopulationTypeName = "RealLifeOverhaul.Population.PopulationDirector";
        public const string CrowdZoneName = "RealLifeOverhaul new ground zone";

        private readonly FieldInfo? field;
        private readonly RloLivingReading fixedReading;

        private RloLivingPopulation(FieldInfo? field, RloLivingReading fixedReading) { this.field = field; this.fixedReading = fixedReading; }

        public static RloLivingPopulation Bind(Assembly? rlo)
        {
            if (rlo == null || rlo.GetName().Name != AssemblyName) return new RloLivingPopulation(null, new RloLivingReading(RloLivingState.Absent));
            try
            {
                Type? settings = rlo.GetType(SettingsTypeName, false);
                FieldInfo? living = settings?.GetField(FieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (living == null && rlo.GetType(PopulationTypeName, false) == null)
                    return new RloLivingPopulation(null, new RloLivingReading(RloLivingState.NotInRelease));
                string? problem = settings == null ? "missing type " + SettingsTypeName
                    : !settings.IsPublic || !settings.IsAbstract || !settings.IsSealed ? SettingsTypeName + " is not a public static class"
                    : living == null ? "missing public static field " + SettingsTypeName + "." + FieldName
                    : living.FieldType != typeof(bool) ? SettingsTypeName + "." + FieldName + " is " + living.FieldType.Name + ", not Boolean"
                    : living.IsLiteral || living.IsInitOnly ? SettingsTypeName + "." + FieldName + " cannot change during play"
                    : null;
                return problem != null ? new RloLivingPopulation(null, new RloLivingReading(RloLivingState.Unknown, problem))
                    : new RloLivingPopulation(living, default);
            }
            catch (Exception error)
            {
                return new RloLivingPopulation(null, new RloLivingReading(RloLivingState.Unknown, "could not bind: " + error.Message));
            }
        }

        public bool Bound => field != null;

        public RloLivingReading Read()
        {
            if (field == null) return fixedReading;
            try
            {
                return field.GetValue(null) is bool on ? new RloLivingReading(on ? RloLivingState.On : RloLivingState.Off)
                    : new RloLivingReading(RloLivingState.Unknown, SettingsTypeName + "." + FieldName + " read no Boolean");
            }
            catch (Exception error) { return new RloLivingReading(RloLivingState.Unknown, "read failed: " + error.Message); }
        }
    }
}
