using System.Threading;

namespace PersistentCity.Core
{
    public static class DiagnosticFrames
    {
        private static int marks;
        private static string lastSource = "";

        public static int Marks => Volatile.Read(ref marks);

        public static string LastSource => Volatile.Read(ref lastSource);

        public static void Mark(string source)
        {
            Volatile.Write(ref lastSource, source ?? "");
            Interlocked.Increment(ref marks);
        }
    }
}
