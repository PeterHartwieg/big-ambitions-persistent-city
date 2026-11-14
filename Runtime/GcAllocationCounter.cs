using System;
using System.Runtime.InteropServices;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal static class GcAllocationCounter
    {
        private enum Source { Heap, Thread, BoehmLibrary, BoehmWindows, BoehmInternal }
        private const int ProbeBytes = 1 << 16;
        private static Source source;

        public static string Name => source switch
        {
            Source.Thread => "thread",
            Source.BoehmLibrary or Source.BoehmWindows or Source.BoehmInternal => "boehm_total",
            _ => "heap_used",
        };

        public static string Probe()
        {
            source = Source.Heap;
            foreach (var candidate in Candidates)
                if (Allocates(candidate)) { source = candidate; break; }
            return Name;
        }

        private static readonly Source[] Candidates = { Source.Thread, Source.BoehmLibrary, Source.BoehmWindows, Source.BoehmInternal };

        public static long Read() => Read(source);

        public static int Collections => source == Source.Heap ? GC.CollectionCount(0) : 0;

        private static bool Allocates(Source candidate)
        {
            try
            {
                long before = Read(candidate);
                var block = new byte[ProbeBytes];
                long after = Read(candidate);
                GC.KeepAlive(block);
                return before >= 0 && after - before >= ProbeBytes;
            }
            catch (Exception) { return false; }
        }

        private static long Read(Source from) => from switch
        {
            Source.Thread => GC.GetAllocatedBytesForCurrentThread(),
            Source.BoehmLibrary => (long)LibraryTotal().ToUInt64(),
            Source.BoehmWindows => (long)WindowsTotal().ToUInt64(),
            Source.BoehmInternal => (long)InternalTotal().ToUInt64(),
            _ => GC.GetTotalMemory(false),
        };

        [DllImport("libmonobdwgc-2.0", EntryPoint = "GC_get_total_bytes")] private static extern UIntPtr LibraryTotal();
        [DllImport("mono-2.0-bdwgc", EntryPoint = "GC_get_total_bytes")] private static extern UIntPtr WindowsTotal();
        [DllImport("__Internal", EntryPoint = "GC_get_total_bytes")] private static extern UIntPtr InternalTotal();
    }

    internal static class PhysicalAllocation
    {
        private static GcPauseSampler? sampler;

        public static bool Begin(GcPauseSampler? running)
        {
            sampler = null;
            if (running == null || !running.Running) return false;
            running.BeginNested(GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            if (running.Nested) sampler = running;
            return sampler != null;
        }

        public static void Charge(GcAllocationSegment segment)
        {
            var current = sampler;
            if (current != null) current.Charge(segment, GcAllocationCounter.Read(), GcAllocationCounter.Collections);
        }

        public static void End()
        {
            var current = sampler;
            sampler = null;
            current?.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, GcAllocationCounter.Read(), GcAllocationCounter.Collections);
        }
    }
}
