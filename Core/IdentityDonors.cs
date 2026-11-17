using System;

namespace PersistentCity.Core
{
    public enum DonorKind { None, Imported, Remixed, Visitor }

    public static class DonorKinds
    {
        public static string Name(DonorKind kind) => kind == DonorKind.Imported ? "imported"
            : kind == DonorKind.Remixed ? "remixed" : kind == DonorKind.Visitor ? "visitor" : "";
    }

    public readonly struct IdentityDonor : IEquatable<IdentityDonor>
    {
        private readonly string? appearance, surname, persona;

        public IdentityDonor(DonorKind kind, string appearance, string surname, string persona, int policyVersion)
        {
            Kind = kind; this.appearance = appearance; this.surname = surname; this.persona = persona; PolicyVersion = policyVersion;
        }

        public DonorKind Kind { get; }
        public string AppearanceId => appearance ?? "";
        public string SurnameId => surname ?? "";
        public string PersonaId => persona ?? "";
        public int PolicyVersion { get; }

        public bool Equals(IdentityDonor other) => Kind == other.Kind && PolicyVersion == other.PolicyVersion
            && string.Equals(AppearanceId, other.AppearanceId, StringComparison.Ordinal)
            && string.Equals(SurnameId, other.SurnameId, StringComparison.Ordinal)
            && string.Equals(PersonaId, other.PersonaId, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is IdentityDonor other && Equals(other);
        public override int GetHashCode() => (int)Kind ^ PolicyVersion ^ StringComparer.Ordinal.GetHashCode(AppearanceId);
        public override string ToString() => Kind + ":" + AppearanceId + "/" + SurnameId + "/" + PersonaId + "@v" + PolicyVersion;
    }

    public sealed class IdentityDonors
    {
        private const string ResidentPrefix = "pc:";
        private readonly IdentityPolicy? policy;
        private readonly int seed, version;

        private IdentityDonors(IdentityPolicy? policy, int seed, int version)
        {
            this.policy = policy; this.seed = seed; this.version = version;
        }

        public static IdentityDonors For(CityState city)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            IdentityPolicy? policy = null;
            if (city.SourceIdentities != null && city.SourceIdentities.Count > 0)
            {
                try { policy = IdentityPolicy.V1(city.SourceIdentities); }
                catch (InvalidOperationException) { policy = null; }
            }
            return new IdentityDonors(policy, city.Seed, city.IdentityPolicyVersion);
        }

        public IdentityDonor Of(CitizenState person)
        {
            string? id = person?.Identity?.Id;
            if (policy == null || string.IsNullOrEmpty(id)) return default;
            IdentityDraw draw;
            DonorKind kind;
            if (person!.IsVisitor) { draw = policy.Draw(seed, id!); kind = DonorKind.Visitor; }
            else if (id!.StartsWith(ResidentPrefix, StringComparison.Ordinal)) { draw = policy.DrawResident(seed, id); kind = DonorKind.Remixed; }
            else
            {
                int self = policy.IndexOf(id);
                if (self < 0) return default;
                draw = new IdentityDraw(IdentityPolicy.Version1, self, self, self);
                kind = DonorKind.Imported;
            }
            return new IdentityDonor(kind, policy.DonorId(draw.Appearance), policy.DonorId(draw.Surname), policy.DonorId(draw.Persona), version);
        }
    }
}
