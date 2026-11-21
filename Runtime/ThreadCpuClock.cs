using System;
using System.Runtime.InteropServices;

namespace PersistentCity.Runtime
{
    internal static class ThreadCpuClock
    {
        private enum Source { Unprobed, Mac, Linux, None }
        private const int MacThreadClock = 16, LinuxThreadClock = 3;
        private static Source source;

        public static bool Available
        {
            get
            {
                if (source == Source.Unprobed) Probe();
                return source != Source.None;
            }
        }

        public static string Name => !Available ? "unavailable" : source == Source.Mac ? "mac_thread_cputime" : "linux_thread_cputime";

        public static long Nanoseconds()
        {
            switch (source)
            {
                case Source.Mac: return (long)MacThreadNanoseconds(MacThreadClock);
                case Source.Linux: return LinuxClock(LinuxThreadClock, out var spec) == 0 ? spec.Seconds * 1000000000L + spec.Nanoseconds : -1;
                case Source.None: return -1;
                default: Probe(); return source == Source.None ? -1 : Nanoseconds();
            }
        }

        private static void Probe()
        {
            source = Source.None;
            if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX) return;
            try { if (MacThreadNanoseconds(MacThreadClock) > 0) { source = Source.Mac; return; } }
            catch (Exception) { }
            try { if (LinuxClock(LinuxThreadClock, out var spec) == 0 && spec.Seconds * 1000000000L + spec.Nanoseconds > 0) source = Source.Linux; }
            catch (Exception) { }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TimeSpec { public long Seconds; public long Nanoseconds; }

        [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "clock_gettime_nsec_np")] private static extern ulong MacThreadNanoseconds(int clock);
        [DllImport("libc", EntryPoint = "clock_gettime")] private static extern int LinuxClock(int clock, out TimeSpec spec);
    }
}
