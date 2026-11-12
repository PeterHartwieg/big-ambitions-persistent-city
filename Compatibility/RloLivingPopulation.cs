using System;
using System.Reflection;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    /// <summary>
    /// RLO 1.0's Living population master switch, bound by plain reflection the way <see cref="RloBridge"/> binds CityApi:
    /// <c>public static bool RealLifeOverhaul.Settings.LivingPopulation</c>, the field RLO's options toggle assigns during
    /// play and its director reads about once a second. This is not CityApi (the Flare donor contract) and does not touch
    /// Flare. Binding happens once; a read is one static field read. A missing or changed surface on a release that has a
    /// living population reads Unknown, never Off. Main thread only.
    /// </summary>
    internal sealed class RloLivingPopulation
    {
        public const string AssemblyName = "RealLifeOverhaul";
        public const string SettingsTypeName = "RealLifeOverhaul.Settings";
        public const string FieldName = "LivingPopulation";
        /// <summary>RLO's population director: present in every release that has a living population.</summary>
        public const string PopulationTypeName = "RealLifeOverhaul.Population.PopulationDirector";
        /// <summary>The name RLO gives the native stationary zones it copies for landmarks, campus and Uptown ground
        /// (NewGround/PeopleGame.ZoneName): the game's own StationaryAiSpawner, so the same system Persistent City replaces.</summary>
        public const string CrowdZoneName = "RealLifeOverhaul new ground zone";

        private readonly FieldInfo? field;
        private readonly RloLivingReading fixedReading;

        private RloLivingPopulation(FieldInfo? field, RloLivingReading fixedReading) { this.field = field; this.fixedReading = fixedReading; }

        /// <summary>Null or a non-RLO assembly reads Absent; RLO without the switch and without a population reads
        /// NotInRelease; any other missing or changed shape reads Unknown with the reason.</summary>
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

        /// <summary>The switch now. Throws nothing: a failed read is Unknown.</summary>
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
