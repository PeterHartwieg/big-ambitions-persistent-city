using System;
using System.Globalization;

namespace PersistentCity.Core
{
    // F5b identity and ownership core (city audit F5 #92, docs/plans/f5-population.md section 3).
    // F5c-lite persists these types in schema 26 (Core/PersonTable.cs, Persistence/PersonWire26.cs); no routine,
    // choice or money path reads them yet.

    public enum PersonKind { Resident, Visitor }

    public enum VisitorPattern { Commuter, DayVisit }

    public enum VisitorPresence { Active, Dormant }

    public enum EmploymentKind { Work, Study }

    /// <summary>Fixed at <see cref="None"/> in F5's shape. Any other value is refused until a later reviewed
    /// contract defines it; no F5 code calls native hiring or staff APIs.</summary>
    public enum NativeEmployment { None = 0 }

    public sealed class VisitorStay
    {
        public string OriginPortalId { get; set; } = "";
        public VisitorPattern Pattern { get; set; }
        public VisitorPresence Presence { get; set; }
        public int FirstDay { get; set; }
        public double NextStayStart { get; set; }
    }

    public sealed class Employment
    {
        public const int Open = -1;
        public string PersonId { get; set; } = "";
        public string WorkplaceId { get; set; } = "";
        public EmploymentKind Kind { get; set; }
        public int StartDay { get; set; }
        public int EndDay { get; set; } = Open;
        public int ScheduleCode { get; set; }
        public int ShiftDraw { get; set; }
        public NativeEmployment NativeEmployment { get; set; }
        public bool IsActive => EndDay == Open;
        public bool Covers(int day) => day >= StartDay && (EndDay == Open || day <= EndDay);
    }

    public enum PersonIdFormat { Invalid, SourceResident, Resident, LegacyConsumer, Visitor }

    public static class PersonIds
    {
        public const string SourceResidentPrefix = "c";
        public const string ResidentPrefix = "pc:";
        public const string LegacyConsumerPrefix = "consumer:";
        public const string VisitorPrefix = "pv:";

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

        internal static bool Unreserved(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
            || c == '-' || c == '.' || c == '_' || c == '~';

        private static bool UpperHex(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

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

    public static class HouseholdIds
    {
        public const string Prefix = "h:";
        public static string For(string homePlaceId)
        {
            if (string.IsNullOrEmpty(homePlaceId)) throw new ArgumentException("A household requires a home.", nameof(homePlaceId));
            return Prefix + homePlaceId;
        }
    }

    public static class PersonPolicy
    {
        public const bool VisitorsOwnCars = false;
        public const VisitorPattern MigratedConsumerPattern = VisitorPattern.Commuter;
    }

    public static class PersonIncome
    {
        public static ResidentIncomeSource Select(Persona persona, int age, Employment? employment, ResidentIncomeSource? carried)
        {
            if (employment != null && employment.IsActive && employment.Kind == EmploymentKind.Work
                && employment.WorkplaceId.Length != 0 && age >= 18 && persona != Persona.Student && persona != Persona.Retiree)
                return ResidentIncomeSource.ExternalEmployment;
            if (carried.HasValue) return carried.Value;
            return persona == Persona.Retiree ? ResidentIncomeSource.ExternalPension : ResidentIncomeSource.ExternalSupport;
        }

        public static ResidentIncomeSource ForDayPlace(Persona persona, int age, string dayPlaceId)
        {
            bool work = dayPlaceId.Length != 0 && persona != Persona.Student;
            if (work && age >= 18 && persona != Persona.Retiree) return ResidentIncomeSource.ExternalEmployment;
            return persona == Persona.Retiree ? ResidentIncomeSource.ExternalPension : ResidentIncomeSource.ExternalSupport;
        }
    }
}
