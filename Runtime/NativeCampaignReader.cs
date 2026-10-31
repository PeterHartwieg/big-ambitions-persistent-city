using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>A caller-selected existing registration. The caller owns generation-safe identity resolution.</summary>
    public sealed class CampaignBusinessSource
    {
        public CampaignBusinessSource(string businessId, object? registration)
        { BusinessId = businessId; Registration = registration; }
        public string BusinessId { get; }
        public object? Registration { get; }
    }

    /// <summary>Explicit main-thread fields-only reader. Never scheduled by Update, choice or meals.
    /// Bind metadata once per shape, with a hard cache bound; do not invoke native getters/helpers.</summary>
    public sealed class NativeCampaignReader
    {
        public const int MaximumBusinesses = 1024;
        public const int MaximumCampaignsPerBusiness = 8;
        public const int MaximumBusinessesPerUnit = 64;
        private const int MaximumShapes = 32;
        private readonly Dictionary<Type, Shape> shapes = new Dictionary<Type, Shape>();

        private sealed class Shape
        {
            private readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            public Shape(Type type)
            {
                // Only a fixed field allowlist. Properties, method dispatch and setters are excluded.
                foreach (string name in new[] { "marketingCampaigns", "promotion", "marketingTypeName", "enabled",
                    "agencyAddress", "streetName", "streetNumber", "trafficIndex", "marketing", "total" })
                {
                    var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                    if (field == null) continue;
                    if (name == "marketingCampaigns" && (!field.FieldType.IsGenericType
                        || field.FieldType.GetGenericTypeDefinition() != typeof(List<>)
                        || field.FieldType.GetGenericArguments()[0].FullName != "Entities.MarketingCampaign")) continue;
                    if (name == "promotion" && field.FieldType.FullName != "Promotion") continue;
                    if (name == "agencyAddress" && field.FieldType.FullName != "Address") continue;
                    if (name == "marketingTypeName" && !VerifiedEnum(field.FieldType)) continue;
                    fields.Add(name, field);
                }
            }
            public object? Read(object source, string name, Type? expected)
            {
                if (!fields.TryGetValue(name, out var field) || (expected != null && field.FieldType != expected)) return null;
                return field.GetValue(source);
            }
        }

        private object? Field(object source, string name, Type? expected = null)
        {
            Type type = source.GetType();
            if (!shapes.TryGetValue(type, out var shape))
            {
                if (shapes.Count >= MaximumShapes) return null;
                shapes.Add(type, shape = new Shape(type));
            }
            return shape.Read(source, name, expected);
        }

        public Refresh Begin(IReadOnlyList<CampaignBusinessSource> sources, double atMinute, long revision)
            => new Refresh(this, sources, atMinute, revision);

        public sealed class Refresh
        {
            private readonly NativeCampaignReader reader;
            private readonly CampaignBusinessSource[] sources;
            private readonly BusinessCampaignSnapshot[] results;
            private readonly double atMinute;
            private readonly long revision;
            private bool refused;
            private CampaignSnapshot? snapshot;
            internal Refresh(NativeCampaignReader reader, IReadOnlyList<CampaignBusinessSource> input, double atMinute, long revision)
            {
                if (input == null || input.Count < 1 || input.Count > MaximumBusinesses
                    || !Position.Finite(atMinute) || atMinute < 0) throw new ArgumentException("Invalid bounded campaign scope/time.");
                this.reader = reader; this.atMinute = atMinute; this.revision = revision;
                sources = new CampaignBusinessSource[input.Count]; results = new BusinessCampaignSnapshot[input.Count];
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < input.Count; i++)
                {
                    var source = input[i];
                    if (source == null || string.IsNullOrWhiteSpace(source.BusinessId) || source.BusinessId.Length > 1024
                        || !ids.Add(source.BusinessId)) throw new ArgumentException("Business scope must have bounded unique identities.");
                    sources[i] = source;
                }
            }
            public int Cursor { get; private set; }
            public bool Refused => refused;
            // No partially refreshed image escapes. A caller must hold its logical boundary/revision stable
            // across units (including native campaign/UI writers); otherwise discard, never backdate.
            public CampaignSnapshot? Snapshot => snapshot;
            public int Step(int businessBudget, double currentMinute, long currentRevision)
            {
                if (businessBudget < 1 || businessBudget > MaximumBusinessesPerUnit) throw new ArgumentOutOfRangeException(nameof(businessBudget));
                if (refused) return 0;
                if (currentMinute != atMinute || currentRevision != revision)
                { refused = true; snapshot = null; Array.Clear(results, 0, results.Length); return 0; }
                int start = Cursor;
                while (Cursor < sources.Length && Cursor - start < businessBudget)
                {
                    var source = sources[Cursor];
                    results[Cursor++] = reader.ReadBusiness(source.BusinessId, source.Registration);
                }
                if (Cursor == sources.Length && snapshot == null) snapshot = new CampaignSnapshot(atMinute, revision, results);
                return Cursor - start;
            }
        }

        private BusinessCampaignSnapshot ReadBusiness(string id, object? registration)
        {
            if (registration == null) return UnknownBusiness(id);
            try
            {
                var promotion = Field(registration, "promotion");
                int? traffic = Index(promotion, "trafficIndex"), marketing = Index(promotion, "marketing"), total = Index(promotion, "total");
                var raw = Field(registration, "marketingCampaigns");
                // Native storage is List<MarketingCampaign>. Arbitrary enumerators/providers are never executed.
                if (raw == null || !raw.GetType().IsGenericType || raw.GetType().GetGenericTypeDefinition() != typeof(List<>)
                    || !(raw is IList list)) return UnknownBusiness(id, traffic, marketing, total);
                if (list.Count > MaximumCampaignsPerBusiness)
                    return new BusinessCampaignSnapshot(id, CampaignReadStatus.Refused, Array.Empty<CampaignObservation>(), traffic, marketing, total);
                var campaigns = new CampaignObservation[list.Count];
                for (int i = 0; i < campaigns.Length; i++)
                    campaigns[i] = ReadCampaign(list[i]);
                bool known = traffic.HasValue && marketing.HasValue && total.HasValue;
                for (int i = 0; i < campaigns.Length; i++)
                {
                    var c = campaigns[i];
                    // At most eight entries: bounded pair checks avoid allocating two hash sets per business.
                    if (c.TypeCode is int code)
                        for (int j = 0; j < campaigns.Length; j++)
                            if (j != i && campaigns[j].TypeCode == code)
                            { campaigns[i] = new CampaignObservation(code, c.TypeName, c.Enabled, c.AgencyId, CampaignReadStatus.Refused); break; }
                    if (campaigns[i].Status != CampaignReadStatus.Observed) known = false;
                }
                return new BusinessCampaignSnapshot(id, known ? CampaignReadStatus.Observed : CampaignReadStatus.Unknown,
                    campaigns, traffic, marketing, total);
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is MemberAccessException || error is TargetException || error is AmbiguousMatchException)
            { return UnknownBusiness(id); }
        }

        private int? Index(object? promotion, string name)
        {
            if (promotion == null || !(Field(promotion, name, typeof(int)) is int value) || value < 0 || value > 100) return null;
            return value;
        }
        private CampaignObservation ReadCampaign(object? campaign)
        {
            if (campaign == null) return new CampaignObservation(null, null, null, null, CampaignReadStatus.Unknown);
            object? rawType = Field(campaign, "marketingTypeName");
            int? code = null;
            // The enum's complete metadata contract was checked when binding its containing shape.
            if (rawType != null) code = Convert.ToInt32(rawType, CultureInfo.InvariantCulture);
            string? name = code.HasValue ? CampaignTypeGroups.VerifiedName(code.Value) : null;
            bool? enabled = Field(campaign, "enabled", typeof(bool)) as bool?;
            object? address = Field(campaign, "agencyAddress");
            string? agency = null;
            if (address != null && Field(address, "streetName", typeof(string)) is string street
                && !string.IsNullOrWhiteSpace(street) && street.Length <= 256
                && Field(address, "streetNumber", typeof(int)) is int number && number > 0)
                agency = "address:" + Uri.EscapeDataString(street) + ":" + number.ToString(CultureInfo.InvariantCulture);
            return new CampaignObservation(code, name, enabled, agency,
                name != null && enabled == true && agency != null ? CampaignReadStatus.Observed : CampaignReadStatus.Unknown);
        }
        private static bool VerifiedEnum(Type type)
        {
            if (!type.IsEnum || type.FullName != "Entities.MarketingTypeName" || Enum.GetUnderlyingType(type) != typeof(int)) return false;
            var names = Enum.GetNames(type);
            if (names.Length != 7) return false;
            for (int i = 0; i < 7; i++)
                if (Enum.GetName(type, i) != (i == 6 ? "None" : CampaignTypeGroups.VerifiedName(i))) return false;
            return true;
        }
        private static BusinessCampaignSnapshot UnknownBusiness(string id, int? traffic = null, int? marketing = null, int? total = null)
            => new BusinessCampaignSnapshot(id, CampaignReadStatus.Unknown, Array.Empty<CampaignObservation>(), traffic, marketing, total);
    }
}
