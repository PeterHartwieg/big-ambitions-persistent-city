using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum OwnershipRule
    {
        PersonOrder, CatalogueOrder, PersonIdFormat, ResidentShape, VisitorShape, VisitorCar, InvalidVisitorStay,
        UnknownHome, UnknownPortal, UnknownWorkplace,
        DuplicateWallet, OrphanWallet, MissingWallet, WalletWithoutFinances, HouseholdMoney, WorkplaceMoney, VisitorStayMoney,
        DuplicateRoster, OrphanRoster, MissingRoster, RosterNotOwn, RosterWithoutFood,
        IntentWithoutOwner, IntentWithTwoOwners, DuplicateIntent, IntentNotDerived,
        EmploymentWithoutPerson, TwoActiveEmployments, InvalidEmployment, EmploymentKindMismatch, PlayerOwnedWorkplace,
        NativeEmploymentNotNone, DayVisitorEmployment,
        WageOnStudy, WageWithoutEmployment, DuplicateWage
    }

    public readonly struct OwnershipViolation
    {
        public OwnershipViolation(OwnershipRule rule, string subject) { Rule = rule; Subject = subject; }
        public OwnershipRule Rule { get; }
        public string Subject { get; }
        public override string ToString() => Rule + " " + Subject;
    }

    public readonly struct PersonFacts
    {
        public PersonFacts(string id, PersonKind kind, string homeId = "", VisitorStay? stay = null, bool hasCar = false)
        { Id = id; Kind = kind; HomeId = homeId; Stay = stay; HasCar = hasCar; }
        public string Id { get; }
        public PersonKind Kind { get; }
        public string HomeId { get; }
        public VisitorStay? Stay { get; }
        public bool HasCar { get; }
    }

    public enum MoneyOwnerKind { Person, Household, Workplace, VisitorStay }

    public readonly struct WalletFacts
    {
        public WalletFacts(MoneyOwnerKind ownerKind, string ownerId) { OwnerKind = ownerKind; OwnerId = ownerId; }
        public MoneyOwnerKind OwnerKind { get; }
        public string OwnerId { get; }
    }

    public readonly struct RosterFacts
    {
        public RosterFacts(string residentId, string anchorId, int slot) { ResidentId = residentId; AnchorId = anchorId; Slot = slot; }
        public string ResidentId { get; }
        public string AnchorId { get; }
        public int Slot { get; }
    }

    public readonly struct DemandUnitFacts
    {
        public DemandUnitFacts(string intentId, string ownerId) { IntentId = intentId; OwnerId = ownerId; }
        public string IntentId { get; }
        public string OwnerId { get; }
    }

    public readonly struct WorkplaceFacts
    {
        public WorkplaceFacts(string id, PlaceKind kind, bool playerOwned = false) { Id = id; Kind = kind; PlayerOwned = playerOwned; }
        public string Id { get; }
        public PlaceKind Kind { get; }
        public bool PlayerOwned { get; }
    }

    public readonly struct WagePosting
    {
        public WagePosting(string personId, int day, string workplaceId) { PersonId = personId; Day = day; WorkplaceId = workplaceId; }
        public string PersonId { get; }
        public int Day { get; }
        public string WorkplaceId { get; }
    }

    public sealed class OwnershipInput
    {
        private static readonly IReadOnlyList<PersonFacts> NoPersons = Array.Empty<PersonFacts>();
        public IReadOnlyList<PersonFacts> Persons { get; set; } = NoPersons;
        public IReadOnlyList<WalletFacts> Wallets { get; set; } = Array.Empty<WalletFacts>();
        public IReadOnlyList<RosterFacts> Roster { get; set; } = Array.Empty<RosterFacts>();
        public IReadOnlyList<DemandUnitFacts> Intents { get; set; } = Array.Empty<DemandUnitFacts>();
        public IReadOnlyList<Employment> Employments { get; set; } = Array.Empty<Employment>();
        public IReadOnlyList<WagePosting> Wages { get; set; } = Array.Empty<WagePosting>();
        public IReadOnlyList<string> HomePlaceIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> PortalIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<WorkplaceFacts> Workplaces { get; set; } = Array.Empty<WorkplaceFacts>();
        public bool FinancesEnabled { get; set; }
        public bool FoodEnabled { get; set; }
    }

    public sealed class OwnershipValidator
    {
        public const string DemandUnitPrefix = "food:";
        public const int StoredViolationLimit = 64;

        private readonly List<OwnershipViolation> violations = new List<OwnershipViolation>();
        private readonly Dictionary<string, int> intentOwners = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<long> wageKeys = new HashSet<long>();
        private int[] wallets = Array.Empty<int>(), roster = Array.Empty<int>(), active = Array.Empty<int>(), firstEmployment = Array.Empty<int>();
        private int[] nextEmployment = Array.Empty<int>();
        private int hint;

        public IReadOnlyList<OwnershipViolation> Violations => violations;
        public int ViolationCount { get; private set; }

        public bool Validate(OwnershipInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            violations.Clear(); ViolationCount = 0; hint = 0;
            var persons = input.Persons;
            int n = persons.Count;
            for (int i = 1; i < n; i++)
                if (string.CompareOrdinal(persons[i - 1].Id, persons[i].Id) >= 0) { Add(OwnershipRule.PersonOrder, persons[i].Id); return false; }
            if (!Sorted(input.HomePlaceIds) || !Sorted(input.PortalIds) || !SortedWorkplaces(input.Workplaces))
            { Add(OwnershipRule.CatalogueOrder, ""); return false; }

            Reset(ref wallets, n); Reset(ref roster, n); Reset(ref active, n); Reset(ref firstEmployment, n, -1);
            for (int i = 0; i < n; i++) CheckPerson(input, persons[i]);
            CheckWallets(input, n);
            CheckRoster(input, n);
            CheckIntents(input);
            CheckEmployments(input);
            CheckWages(input);
            return ViolationCount == 0;
        }

        private void CheckPerson(OwnershipInput input, in PersonFacts person)
        {
            if (!PersonIds.FitsKind(PersonIds.Classify(person.Id), person.Kind)) Add(OwnershipRule.PersonIdFormat, person.Id);
            if (person.Kind == PersonKind.Resident)
            {
                if (person.HomeId.Length == 0 || person.Stay != null) Add(OwnershipRule.ResidentShape, person.Id);
                else if (Find(input.HomePlaceIds, person.HomeId) < 0) Add(OwnershipRule.UnknownHome, person.Id);
                return;
            }
            if (person.Kind != PersonKind.Visitor || person.HomeId.Length != 0 || person.Stay == null)
            { Add(OwnershipRule.VisitorShape, person.Id); return; }
            if (person.HasCar && !PersonPolicy.VisitorsOwnCars) Add(OwnershipRule.VisitorCar, person.Id);
            var stay = person.Stay;
            if ((uint)stay.Pattern > (uint)VisitorPattern.DayVisit || (uint)stay.Presence > (uint)VisitorPresence.Dormant
                || stay.FirstDay < 0 || !(stay.NextStayStart >= 0) || double.IsInfinity(stay.NextStayStart))
                Add(OwnershipRule.InvalidVisitorStay, person.Id);
            if (Find(input.PortalIds, stay.OriginPortalId) < 0) Add(OwnershipRule.UnknownPortal, person.Id);
        }

        private void CheckWallets(OwnershipInput input, int n)
        {
            for (int index = 0; index < input.Wallets.Count; index++)
            {
                var wallet = input.Wallets[index];
                if (wallet.OwnerKind != MoneyOwnerKind.Person)
                {
                    Add(wallet.OwnerKind == MoneyOwnerKind.Household ? OwnershipRule.HouseholdMoney
                        : wallet.OwnerKind == MoneyOwnerKind.Workplace ? OwnershipRule.WorkplaceMoney : OwnershipRule.VisitorStayMoney, wallet.OwnerId);
                    continue;
                }
                int owner = Person(input.Persons, wallet.OwnerId);
                if (owner < 0) { Add(OwnershipRule.OrphanWallet, wallet.OwnerId); continue; }
                if (!input.FinancesEnabled) { Add(OwnershipRule.WalletWithoutFinances, wallet.OwnerId); continue; }
                if (++wallets[owner] == 2) Add(OwnershipRule.DuplicateWallet, wallet.OwnerId);
            }
            if (!input.FinancesEnabled) return;
            for (int i = 0; i < n; i++) if (wallets[i] == 0) Add(OwnershipRule.MissingWallet, input.Persons[i].Id);
        }

        private void CheckRoster(OwnershipInput input, int n)
        {
            for (int index = 0; index < input.Roster.Count; index++)
            {
                var record = input.Roster[index];
                if (!input.FoodEnabled) { Add(OwnershipRule.RosterWithoutFood, record.ResidentId); continue; }
                int owner = Person(input.Persons, record.ResidentId);
                if (owner < 0) { Add(OwnershipRule.OrphanRoster, record.ResidentId); continue; }
                if (record.Slot != 0 || !string.Equals(record.AnchorId, record.ResidentId, StringComparison.Ordinal))
                    Add(OwnershipRule.RosterNotOwn, record.ResidentId);
                if (++roster[owner] == 2) Add(OwnershipRule.DuplicateRoster, record.ResidentId);
            }
            if (!input.FoodEnabled) return;
            for (int i = 0; i < n; i++) if (roster[i] == 0) Add(OwnershipRule.MissingRoster, input.Persons[i].Id);
        }

        private void CheckIntents(OwnershipInput input)
        {
            intentOwners.Clear();
            bool foreign = false;
            for (int index = 0; index < input.Intents.Count; index++)
            {
                var intent = input.Intents[index];
                int owner = Person(input.Persons, intent.OwnerId);
                if (owner < 0) { Add(OwnershipRule.IntentWithoutOwner, intent.IntentId); continue; }
                if (!DerivedFrom(intent.IntentId, intent.OwnerId)) { foreign = true; continue; }
                if (intentOwners.ContainsKey(intent.IntentId)) Add(OwnershipRule.DuplicateIntent, intent.IntentId);
                else intentOwners.Add(intent.IntentId, owner);
            }
            if (!foreign) return;
            for (int index = 0; index < input.Intents.Count; index++)
            {
                var intent = input.Intents[index];
                if (Person(input.Persons, intent.OwnerId) < 0 || DerivedFrom(intent.IntentId, intent.OwnerId)) continue;
                Add(intentOwners.ContainsKey(intent.IntentId) ? OwnershipRule.IntentWithTwoOwners : OwnershipRule.IntentNotDerived, intent.IntentId);
            }
        }

        private void CheckEmployments(OwnershipInput input)
        {
            var employments = input.Employments;
            if (nextEmployment.Length < employments.Count) nextEmployment = new int[Math.Max(employments.Count, nextEmployment.Length * 2)];
            for (int e = 0; e < employments.Count; e++)
            {
                var job = employments[e];
                int owner = Person(input.Persons, job.PersonId);
                if (owner < 0) { Add(OwnershipRule.EmploymentWithoutPerson, job.PersonId); continue; }
                nextEmployment[e] = firstEmployment[owner]; firstEmployment[owner] = e;
                if (job.NativeEmployment != NativeEmployment.None) Add(OwnershipRule.NativeEmploymentNotNone, job.PersonId);
                if ((uint)job.Kind > (uint)EmploymentKind.Study || job.StartDay < 0
                    || (job.EndDay != Employment.Open && job.EndDay < job.StartDay))
                    Add(OwnershipRule.InvalidEmployment, job.PersonId);
                var person = input.Persons[owner];
                if (person.Kind == PersonKind.Visitor && person.Stay != null && person.Stay.Pattern != VisitorPattern.Commuter)
                    Add(OwnershipRule.DayVisitorEmployment, job.PersonId);
                int place = FindWorkplace(input.Workplaces, job.WorkplaceId);
                if (place < 0) Add(OwnershipRule.UnknownWorkplace, job.PersonId);
                else
                {
                    var workplace = input.Workplaces[place];
                    if (workplace.Kind != (job.Kind == EmploymentKind.Study ? PlaceKind.Study : PlaceKind.Work))
                        Add(OwnershipRule.EmploymentKindMismatch, job.PersonId);
                    if (workplace.PlayerOwned) Add(OwnershipRule.PlayerOwnedWorkplace, job.PersonId);
                }
                if (job.IsActive && ++active[owner] == 2) Add(OwnershipRule.TwoActiveEmployments, job.PersonId);
            }
        }

        private void CheckWages(OwnershipInput input)
        {
            wageKeys.Clear();
            for (int index = 0; index < input.Wages.Count; index++)
            {
                var wage = input.Wages[index];
                int owner = Person(input.Persons, wage.PersonId);
                if (owner < 0) { Add(OwnershipRule.WageWithoutEmployment, wage.PersonId); continue; }
                if (!wageKeys.Add(((long)owner << 32) | (uint)wage.Day)) { Add(OwnershipRule.DuplicateWage, wage.PersonId); continue; }
                Employment? source = null;
                for (int e = firstEmployment[owner]; e >= 0; e = nextEmployment[e])
                {
                    var job = input.Employments[e];
                    if (job.Covers(wage.Day) && string.Equals(job.WorkplaceId, wage.WorkplaceId, StringComparison.Ordinal)) { source = job; break; }
                }
                if (source == null) Add(OwnershipRule.WageWithoutEmployment, wage.PersonId);
                else if (source.Kind == EmploymentKind.Study) Add(OwnershipRule.WageOnStudy, wage.PersonId);
            }
        }

        internal static bool DerivedFrom(string intentId, string ownerId)
        {
            int at = DemandUnitPrefix.Length;
            if (!intentId.StartsWith(DemandUnitPrefix, StringComparison.Ordinal)) return false;
            foreach (char c in ownerId)
            {
                if (c > 0x7F) return intentId.StartsWith(DemandUnitPrefix + Uri.EscapeDataString(ownerId) + ":", StringComparison.Ordinal)
                    && intentId.Length > DemandUnitPrefix.Length + Uri.EscapeDataString(ownerId).Length + 1;
                if (PersonIds.Unreserved(c)) { if (at >= intentId.Length || intentId[at] != c) return false; at++; }
                else
                {
                    if (at + 2 >= intentId.Length || intentId[at] != '%' || intentId[at + 1] != Hex(c >> 4) || intentId[at + 2] != Hex(c & 0xF)) return false;
                    at += 3;
                }
            }
            return at < intentId.Length - 1 && intentId[at] == ':';
        }

        private static char Hex(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);

        private void Add(OwnershipRule rule, string subject)
        {
            ViolationCount++;
            if (violations.Count < StoredViolationLimit) violations.Add(new OwnershipViolation(rule, subject));
        }

        private static void Reset(ref int[] buffer, int length, int value = 0)
        {
            if (buffer.Length < length) buffer = new int[Math.Max(length, buffer.Length * 2)];
            for (int i = 0; i < length; i++) buffer[i] = value;
        }

        private static bool Sorted(IReadOnlyList<string> ids)
        {
            for (int i = 1; i < ids.Count; i++) if (string.CompareOrdinal(ids[i - 1], ids[i]) >= 0) return false;
            return true;
        }

        private static bool SortedWorkplaces(IReadOnlyList<WorkplaceFacts> places)
        {
            for (int i = 1; i < places.Count; i++) if (string.CompareOrdinal(places[i - 1].Id, places[i].Id) >= 0) return false;
            return true;
        }

        private static int Find(IReadOnlyList<string> ids, string id)
        {
            int low = 0, high = ids.Count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1), c = string.CompareOrdinal(ids[mid], id);
                if (c == 0) return mid;
                if (c < 0) low = mid + 1; else high = mid - 1;
            }
            return -1;
        }

        private int Person(IReadOnlyList<PersonFacts> persons, string id)
        {
            int count = persons.Count;
            if (hint < count && string.Equals(persons[hint].Id, id, StringComparison.Ordinal)) return hint;
            if (hint + 1 < count && string.Equals(persons[hint + 1].Id, id, StringComparison.Ordinal)) return ++hint;
            int found = FindPerson(persons, id);
            if (found >= 0) hint = found;
            return found;
        }

        private static int FindPerson(IReadOnlyList<PersonFacts> persons, string id)
        {
            int low = 0, high = persons.Count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1), c = string.CompareOrdinal(persons[mid].Id, id);
                if (c == 0) return mid;
                if (c < 0) low = mid + 1; else high = mid - 1;
            }
            return -1;
        }

        private static int FindWorkplace(IReadOnlyList<WorkplaceFacts> places, string id)
        {
            int low = 0, high = places.Count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1), c = string.CompareOrdinal(places[mid].Id, id);
                if (c == 0) return mid;
                if (c < 0) low = mid + 1; else high = mid - 1;
            }
            return -1;
        }
    }
}
