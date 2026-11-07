using System;
using System.Globalization;

namespace PersistentCity.Core
{
    // F5b identity and ownership core (city audit F5 #92, docs/plans/f5-population.md section 3).
    // F5c-lite persists these types in schema 26 (Core/PersonTable.cs, Persistence/PersonWire26.cs); no routine,
    // choice or money path reads them yet.

    public enum PersonKind { Resident, Visitor }

    /// <summary>Commuter stays recur on weekdays and may hold one Employment; DayVisit pool members return
    /// on seeded days and never hold one (contract B2).</summary>
    public enum VisitorPattern { Commuter, DayVisit }

    /// <summary>Active during a stay, Dormant between stays. A dormant visitor keeps its wallet, daily postings
    /// and pending obligations but creates no new demand.</summary>
    public enum VisitorPresence { Active, Dormant }

    public enum EmploymentKind { Work, Study }

    /// <summary>Fixed at <see cref="None"/> in F5's shape. Any other value is refused until a later reviewed
    /// contract defines it; no F5 code calls native hiring or staff APIs.</summary>
    public enum NativeEmployment { None = 0 }

    /// <summary>Part of a visitor's person record. Holds no money (contract B4): the portal is a saved portal ID,
    /// never a native object (rule 5).</summary>
    public sealed class VisitorStay
    {
        public string OriginPortalId { get; set; } = "";
        public VisitorPattern Pattern { get; set; }
        public VisitorPresence Presence { get; set; }
        public int FirstDay { get; set; }
        public double NextStayStart { get; set; }
    }

    /// <summary>Person → workplace link (contract B3), formalizing today's DayPlaceId. At most one active per
    /// person. F5 stores no wage field: F6 posts wages from this record into the person's single wallet keyed by
    /// (PersonId, day), and never on Study.</summary>
    public sealed class Employment
    {
        public const int Open = -1;
        public string PersonId { get; set; } = "";
        /// <summary>Saved Place.Id of kind Work (for Work) or Study (for Study).</summary>
        public string WorkplaceId { get; set; } = "";
        public EmploymentKind Kind { get; set; }
        public int StartDay { get; set; }
        /// <summary>Last employed day, or <see cref="Open"/> while active.</summary>
        public int EndDay { get; set; } = Open;
        public int ScheduleCode { get; set; }
        public int ShiftDraw { get; set; }
        public NativeEmployment NativeEmployment { get; set; }
        public bool IsActive => EndDay == Open;
        public bool Covers(int day) => day >= StartDay && (EndDay == Open || day <= EndDay);
    }

    public enum PersonIdFormat { Invalid, SourceResident, Resident, LegacyConsumer, Visitor }

    /// <summary>PersonId is the existing opaque ordinal string (contract B1): <c>c&lt;n&gt;</c> (RLO source),
    /// <c>pc:&lt;n&gt;</c>, legacy <c>consumer:&lt;escaped anchor&gt;:&lt;slot&gt;</c> kept verbatim, and new
    /// <c>pv:&lt;n&gt;</c>. IDs are never reused. <see cref="Classify"/> checks the shape only; callers must
    /// never parse an anchor out of an ID.</summary>
    public static class PersonIds
    {
        public const string SourceResidentPrefix = "c";
        public const string ResidentPrefix = "pc:";
        public const string LegacyConsumerPrefix = "consumer:";
        public const string VisitorPrefix = "pv:";

        /// <summary>Every person list is sorted by this order (rule 4).</summary>
        public static StringComparer Order => StringComparer.Ordinal;
        public static int Compare(string a, string b) => string.CompareOrdinal(a, b);

        public static string Visitor(int sequence)
        {
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
            return VisitorPrefix + sequence.ToString(CultureInfo.InvariantCulture);
        }

        public static PersonIdFormat Classify(string? id)
        {
            if (string.IsNullOrEmpty(id)) return PersonIdFormat.Invalid;
            if (id!.StartsWith(ResidentPrefix, StringComparison.Ordinal))
                return Canonical(id, ResidentPrefix.Length, id.Length, true) ? PersonIdFormat.Resident : PersonIdFormat.Invalid;
            if (id.StartsWith(VisitorPrefix, StringComparison.Ordinal))
                return Canonical(id, VisitorPrefix.Length, id.Length, true) ? PersonIdFormat.Visitor : PersonIdFormat.Invalid;
            if (id.StartsWith(LegacyConsumerPrefix, StringComparison.Ordinal))
            {
                int slot = id.LastIndexOf(':');
                int anchor = LegacyConsumerPrefix.Length;
                if (slot <= anchor || !Canonical(id, slot + 1, id.Length, true)) return PersonIdFormat.Invalid;
                // The anchor is FoodRepresentation's escaped form: unreserved characters and %XX only.
                for (int i = anchor; i < slot; i++)
                {
                    char c = id[i];
                    if (c == '%')
                    {
                        if (i + 2 >= slot || !UpperHex(id[i + 1]) || !UpperHex(id[i + 2])) return PersonIdFormat.Invalid;
                        i += 2;
                    }
                    else if (!Unreserved(c)) return PersonIdFormat.Invalid;
                }
                return PersonIdFormat.LegacyConsumer;
            }
            if (id[0] == 'c') return Canonical(id, 1, id.Length, false) ? PersonIdFormat.SourceResident : PersonIdFormat.Invalid;
            return PersonIdFormat.Invalid;
        }

        public static bool FitsKind(PersonIdFormat format, PersonKind kind) => kind == PersonKind.Resident
            ? format == PersonIdFormat.SourceResident || format == PersonIdFormat.Resident
            : format == PersonIdFormat.LegacyConsumer || format == PersonIdFormat.Visitor;

        /// <summary>RFC 3986 unreserved characters, which Uri.EscapeDataString leaves as they are.</summary>
        internal static bool Unreserved(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
            || c == '-' || c == '.' || c == '_' || c == '~';

        private static bool UpperHex(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

        // Invariant decimal int without sign or leading zero; positive when required.
        private static bool Canonical(string text, int start, int end, bool positive)
        {
            int length = end - start;
            if (length < 1 || length > 10) return false;
            if (text[start] == '0') return length == 1 && !positive;
            long value = 0;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9') return false;
                value = value * 10 + (c - '0');
            }
            return value <= int.MaxValue;
        }
    }

    /// <summary>HouseholdId is derived from the resident's saved HomeId (one residential interior = one
    /// household) and never stored. Households hold no money (contract B4).</summary>
    public static class HouseholdIds
    {
        public const string Prefix = "h:";
        public static string For(string homePlaceId)
        {
            if (string.IsNullOrEmpty(homePlaceId)) throw new ArgumentException("A household requires a home.", nameof(homePlaceId));
            return Prefix + homePlaceId;
        }
    }

    /// <summary>Defaults for the F5 plan's questions for Peter (section 9) that F5b touches. A different answer
    /// changes one constant.</summary>
    public static class PersonPolicy
    {
        /// <summary>Q1 default: commuting visitors come by subway and walk; they own no car or traffic record.</summary>
        public const bool VisitorsOwnCars = false;
        /// <summary>Q3 default: today's represented consumers become commuting visitors with the same money and memories.</summary>
        public const VisitorPattern MigratedConsumerPattern = VisitorPattern.Commuter;
    }

    /// <summary>Per-person income source (section 3 Resources), replacing the anchor lookup once a person runs on
    /// its own schedule. Amount rules stay HouseholdFinance's until F6.</summary>
    public static class PersonIncome
    {
        /// <summary>An eligible active Work employment gives <c>ExternalEmployment</c>. Otherwise a migrated
        /// visitor's carried source applies until its first own employment outcome clears it; otherwise
        /// retirees get <c>ExternalPension</c> and everyone else <c>ExternalSupport</c>. For a resident with no
        /// carried source and an employment built from its DayPlaceId this equals today's rule.</summary>
        public static ResidentIncomeSource Select(Persona persona, int age, Employment? employment, ResidentIncomeSource? carried)
        {
            if (employment != null && employment.IsActive && employment.Kind == EmploymentKind.Work
                && employment.WorkplaceId.Length != 0 && age >= 18 && persona != Persona.Student && persona != Persona.Retiree)
                return ResidentIncomeSource.ExternalEmployment;
            if (carried.HasValue) return carried.Value;
            return persona == Persona.Retiree ? ResidentIncomeSource.ExternalPension : ResidentIncomeSource.ExternalSupport;
        }

        /// <summary>F5e: <see cref="Select"/> for a resident's Employment as <see cref="PersonTable.ResidentEmployment"/>
        /// builds it from DayPlaceId (Study for students, Work otherwise), without allocating the record per posting.
        /// A Study enrolment never yields <c>ExternalEmployment</c>: study carries no wage.</summary>
        public static ResidentIncomeSource ForDayPlace(Persona persona, int age, string dayPlaceId)
        {
            bool work = dayPlaceId.Length != 0 && persona != Persona.Student;
            if (work && age >= 18 && persona != Persona.Retiree) return ResidentIncomeSource.ExternalEmployment;
            return persona == Persona.Retiree ? ResidentIncomeSource.ExternalPension : ResidentIncomeSource.ExternalSupport;
        }
    }
}
