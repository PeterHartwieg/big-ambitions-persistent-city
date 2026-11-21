using System.Threading;

namespace PersistentCity.Core
{
    public readonly struct DiagnosticMark
    {
        public DiagnosticMark(int sequence, string source) { Sequence = sequence; Source = source; }
        public int Sequence { get; }
        public string Source { get; }
    }

    public static class DiagnosticFrames
    {
        private sealed class Stamp
        {
            internal Stamp(int sequence, string source) { Sequence = sequence; Source = source; }
            internal readonly int Sequence;
            internal readonly string Source;
        }

        private static Stamp last = new Stamp(0, "");

        public static DiagnosticMark Current
        {
            get { var stamp = Volatile.Read(ref last); return new DiagnosticMark(stamp.Sequence, stamp.Source); }
        }

        public static int Marks => Volatile.Read(ref last).Sequence;

        public static string LastSource => Volatile.Read(ref last).Source;

        public static void Mark(string source)
        {
            string label = source ?? "";
            while (true)
            {
                var seen = Volatile.Read(ref last);
                if (ReferenceEquals(Interlocked.CompareExchange(ref last, new Stamp(seen.Sequence + 1, label), seen), seen)) return;
            }
        }
    }
}
