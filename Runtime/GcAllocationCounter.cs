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
            _ => RetimeAllocationTally.HeapUsedCounter,
        };

        private static bool probed;

        public static string Probe()
        {
            if (probed) return Name;
            source = Source.Heap;
            foreach (var candidate in Candidates)
                if (Allocates(candidate)) { source = candidate; break; }
            probed = true;
            return Name;
        }

        public static RetimeAllocationMark Mark() => new RetimeAllocationMark(Read(), Collections);

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
            ModFrames.Charge(segment);
        }

        public static void Mark()
        {
            var current = sampler;
            if (current != null) current.Mark(GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            ModFrames.MarkDetail();
        }

        public static void Detail(GcAllocationSegment segment)
        {
            var current = sampler;
            if (current != null) current.Detail(segment, GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            ModFrames.Detail(segment);
        }

        public static void Settle(GcAllocationSegment sub)
        {
            var current = sampler;
            if (current != null)
            {
                long counter = GcAllocationCounter.Read(); int collections = GcAllocationCounter.Collections;
                current.Detail(sub, counter, collections);
                current.Charge(GcAllocationSegment.PhysicalUnattributed, counter, collections);
            }
            ModFrames.Detail(sub);
            ModFrames.Charge(GcAllocationSegment.PhysicalUnattributed);
        }

        public static void End() => End(GcAllocationSegment.PhysicalUnattributedUpdate);

        public static void End(GcAllocationSegment remainder)
        {
            var current = sampler;
            sampler = null;
            if (current != null)
            {
                long counter = GcAllocationCounter.Read(); int collections = GcAllocationCounter.Collections;
                current.Sub(remainder, counter, collections);
                current.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, counter, collections);
            }
            ModFrames.Detail(remainder);
            ModFrames.Charge(GcAllocationSegment.PhysicalUnattributed);
        }
    }

    internal static class AdvanceAllocation
    {
        private static GcPauseSampler? sampler;

        public static void Begin(GcPauseSampler? running)
        {
            sampler = null;
            if (running == null || !running.Running) return;
            running.BeginNested(GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            if (running.Nested) sampler = running;
        }

        public static void Detail(GcAllocationSegment segment)
        {
            var current = sampler;
            if (current != null) current.Detail(segment, GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            ModFrames.Charge(segment);
        }

        public static void Sub(GcAllocationSegment segment)
        {
            var current = sampler;
            if (current != null) current.Sub(segment, GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            ModFrames.Detail(segment);
        }

        public static void End()
        {
            var current = sampler;
            sampler = null;
            current?.EndDetails(GcAllocationSegment.AdvanceOther, GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            ModFrames.Charge(GcAllocationSegment.AdvanceOther);
        }
    }

    internal static class ModFrames
    {
        private static ModFrameRecorder? recorder;
        private static GcPauseSampler? owner;

        public static bool Begin(GcPauseSampler? sampler, long frame)
        {
            recorder = null; owner = null;
            var candidate = sampler?.FrameRecorder;
            if (candidate == null || !candidate.Armed) return false;
            recorder = candidate; owner = sampler;
            candidate.Begin(frame, System.Diagnostics.Stopwatch.GetTimestamp(), GcAllocationCounter.Read(), GC.CollectionCount(0), GcAllocationCounter.Collections, ThreadCpuClock.Nanoseconds());
            return true;
        }

        public static void Charge(GcAllocationSegment segment) => Charge(segment, System.Diagnostics.Stopwatch.GetTimestamp());

        public static void Charge(GcAllocationSegment segment, long timestamp)
        {
            var current = recorder;
            if (current != null) current.Charge(segment, timestamp, GcAllocationCounter.Read(), GC.CollectionCount(0), GcAllocationCounter.Collections);
        }

        public static void Detail(GcAllocationSegment segment) => Detail(segment, System.Diagnostics.Stopwatch.GetTimestamp());

        public static void Detail(GcAllocationSegment segment, long timestamp)
        {
            var current = recorder;
            if (current != null) current.Detail(segment, timestamp, GcAllocationCounter.Read(), GC.CollectionCount(0), GcAllocationCounter.Collections);
        }

        public static void NoteGrow(in GrowTiming timing) => recorder?.NoteGrow(timing);

        public static void MarkDetail()
        {
            var current = recorder;
            if (current != null) current.MarkDetail(System.Diagnostics.Stopwatch.GetTimestamp(), GcAllocationCounter.Read(), GC.CollectionCount(0));
        }

        public static void End()
        {
            var current = recorder;
            var sampler = owner;
            recorder = null; owner = null;
            if (current == null) return;
            long ended = System.Diagnostics.Stopwatch.GetTimestamp();
            current.End(ended, GcAllocationCounter.Read(), GC.CollectionCount(0), GcAllocationCounter.Collections, sampler, ThreadCpuClock.Nanoseconds());
        }
    }
}
