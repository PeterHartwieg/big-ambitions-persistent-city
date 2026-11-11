using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>
    /// GR-1 person wallet on the wire. The wallet is written once, with its person (<see cref="CitizenState.Wallet"/> on a
    /// resident and on a visitor's routine); the food roster record carries none and is linked to its person's wallet after
    /// a read and after a snapshot capture. Older development cities wrote the budget inside the roster record and had no
    /// place business type; they are refused as older development cities and kept unchanged.
    /// </summary>
    public static partial class SaveCodec
    {
        /// <summary>Links every roster record to its person's wallet: a resident by its ID, a visitor by its routine. Only for
        /// read or snapshot state: it overwrites each record's link and never adopts a record's own budget (the live city
        /// adopts with FoodCommerce.AdoptRecordWallets).</summary>
        internal static void LinkWallets(CityState state)
        {
            var food = state.Food;
            if (food?.Residents == null || state.Citizens == null) return;
            var persons = WalletOwners(state);
            foreach (var meal in food.Residents)
            {
                if (meal == null) continue;
                var wallet = meal.ResidentId != null && persons.TryGetValue(meal.ResidentId, out var person) ? person.Wallet : null;
                if (!ReferenceEquals(meal.Budget, wallet)) meal.Budget = wallet; // an attached record's setter reports to the panel counters
            }
        }

        private static Dictionary<string, CitizenState> WalletOwners(CityState state)
        {
            var persons = new Dictionary<string, CitizenState>(state.Citizens.Count + (state.VisitorRoutines?.Count ?? 0), StringComparer.Ordinal);
            foreach (var person in state.Citizens)
                if (person?.Identity?.Id != null) persons[person.Identity.Id] = person;
            if (state.VisitorRoutines != null)
                foreach (var routine in state.VisitorRoutines)
                    if (routine?.Identity?.Id != null) persons[routine.Identity.Id] = routine;
            return persons;
        }

        /// <summary>Every wallet belongs to exactly one person, and a roster record holds its own person's wallet or none.
        /// Without a food scope no person has a wallet.</summary>
        private static void ValidateWallets(CityState state)
        {
            var food = state.Food;
            if (food == null)
            {
                foreach (var person in state.Citizens) Require(person?.Wallet == null, "Wallet without household finances.");
                if (state.VisitorRoutines != null)
                    foreach (var routine in state.VisitorRoutines) Require(routine?.Wallet == null, "Wallet without household finances.");
                return;
            }
            var owners = WalletOwners(state);
            var seen = new HashSet<ResidentBudget>(WalletIdentity.Instance);
            foreach (var person in owners.Values)
                if (person.Wallet != null) Require(seen.Add(person.Wallet), "Two persons share one wallet.");
            RequireLinked(food, owners);
        }

        /// <summary>Snapshot capture checks the live roster first: a record holding any other wallet than its person's
        /// would otherwise be saved with the person's alone.</summary>
        internal static void RequireLinkedWallets(CityState live)
        {
            if (live.Food?.Residents == null || live.Citizens == null) return;
            RequireLinked(live.Food, WalletOwners(live));
        }

        private static void RequireLinked(FoodCommerceState food, Dictionary<string, CitizenState> owners)
        {
            foreach (var meal in food.Residents)
            {
                Require(meal != null, "Null meal record.");
                owners.TryGetValue(meal!.ResidentId ?? "", out var owner);
                if (!ReferenceEquals(meal.Budget, owner?.Wallet))
                    Fail("A food record's wallet is not its person's: " + meal.ResidentId + (owner == null ? " (no person)" : owner.Wallet == null ? " (person has none)" : meal.Budget == null ? " (record has none)" : " (another wallet)") + ".");
            }
        }

        private const string FirstPlace = "\"Places\":[{\"AttractionWeight\":";

        /// <summary>The writer sorts fields ordinally, so every current place starts with AttractionWeight then
        /// BusinessType; a pre-GR-1 body goes straight from AttractionWeight to Capacity. One ordinal search, no decode.</summary>
        internal static bool PreWalletBody(string source)
        {
            if (!source.StartsWith(BodyPrefix, StringComparison.Ordinal)) return false;
            int at = source.IndexOf(FirstPlace, StringComparison.Ordinal);
            if (at < 0) return false;
            int comma = source.IndexOf(',', at + FirstPlace.Length);
            return comma > 0 && string.CompareOrdinal(source, comma, ",\"Capacity\":", 0, 12) == 0;
        }

        private sealed class WalletIdentity : IEqualityComparer<ResidentBudget>
        {
            public static readonly WalletIdentity Instance = new WalletIdentity();
            public bool Equals(ResidentBudget? a, ResidentBudget? b) => ReferenceEquals(a, b);
            public int GetHashCode(ResidentBudget value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }

        internal static SaveReadResult PreWalletRefusal() => new SaveReadResult(SaveReadStatus.DevelopmentSchema, null,
            DevelopmentSchemaMessage + " (schema 1, before person wallets and place business types)", CityState.CurrentSchema);
    }
}
