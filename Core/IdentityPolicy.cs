using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct IdentityDraw
    {
        public IdentityDraw(int policyVersion, int appearance, int surname, int persona)
        { PolicyVersion = policyVersion; Appearance = appearance; Surname = surname; Persona = persona; }
        public int PolicyVersion { get; }
        public int Appearance { get; }
        public int Surname { get; }
        public int Persona { get; }
    }

    public sealed class IdentityPolicy
    {
        public const int Version1 = 1;
        public const int CurrentVersion = Version1;

        private readonly CitizenIdentity[] donors;
        private readonly Dictionary<int, int[]> compatibleByAge = new Dictionary<int, int[]>();

        private IdentityPolicy(CitizenIdentity[] donors) => this.donors = donors;

        public int PolicyVersion => Version1;
        public int DonorCount => donors.Length;

        public static IdentityPolicy V1(IEnumerable<CitizenIdentity> sourceIdentities)
        {
            if (sourceIdentities == null) throw new ArgumentNullException(nameof(sourceIdentities));
            var sorted = new List<CitizenIdentity>(sourceIdentities);
            if (sorted.Count == 0) throw new InvalidOperationException("Derived identities require the preserved source population.");
            sorted.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (int i = 1; i < sorted.Count; i++)
                if (string.Equals(sorted[i - 1].Id, sorted[i].Id, StringComparison.Ordinal))
                    throw new InvalidOperationException("Duplicate donor identity " + sorted[i].Id + ".");
            var policy = new IdentityPolicy(sorted.ToArray());
            var pool = new List<int>();
            foreach (var donor in policy.donors)
            {
                if (policy.compatibleByAge.ContainsKey(donor.Age)) continue;
                pool.Clear();
                for (int i = 0; i < policy.donors.Length; i++)
                    if (FitsAge(policy.donors[i].Persona, donor.Age)) pool.Add(i);
                policy.compatibleByAge.Add(donor.Age, pool.ToArray());
            }
            return policy;
        }

        public IdentityDraw Draw(int seed, string personId)
        {
            if (string.IsNullOrEmpty(personId)) throw new ArgumentException("A derived identity needs a PersonId.", nameof(personId));
            int appearance = (int)(Hash(seed, personId, ":identity:v1:appearance") % (uint)donors.Length);
            int surname = (int)(Hash(seed, personId, ":identity:v1:surname") % (uint)donors.Length);
            var compatible = compatibleByAge[donors[appearance].Age];
            int persona = compatible.Length == 0 ? appearance
                : compatible[Hash(seed, personId, ":identity:v1:persona") % (uint)compatible.Length];
            return new IdentityDraw(Version1, appearance, surname, persona);
        }

        public Persona PersonaOf(int seed, string personId) => donors[Draw(seed, personId).Persona].Persona;

        public CitizenIdentity Derive(int seed, string personId)
        {
            var draw = Draw(seed, personId);
            var appearance = donors[draw.Appearance];
            string given = appearance.Name.Trim(), family = donors[draw.Surname].Name.Trim();
            int space = given.LastIndexOf(' ');
            if (space > 0) given = given.Substring(0, space);
            space = family.LastIndexOf(' ');
            if (space >= 0 && space < family.Length - 1) family = family.Substring(space + 1);
            return new CitizenIdentity
            {
                Id = personId, Name = given + " " + family, District = "",
                Age = appearance.Age, Female = appearance.Female, AppearanceSeed = appearance.AppearanceSeed,
                Persona = donors[draw.Persona].Persona
            };
        }

        private static uint Hash(int seed, string personId, string purpose)
            => StableRandom.Finish(StableRandom.Append(StableRandom.Append(StableRandom.Begin(seed), personId), purpose));

        private static bool FitsAge(Persona persona, int age) => persona == Persona.Student ? age >= 18 && age <= 25
            : persona == Persona.Parent ? age >= 27 && age <= 50
            : persona == Persona.Retiree ? age >= 65 && age <= 82
            : age >= 20 && age <= 64;
    }
}
