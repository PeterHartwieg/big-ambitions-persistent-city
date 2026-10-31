using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace FoodDemandAuditProbe
{
    // Fixed value storage: retained frames cannot alias mutable component arrays.
    public struct ComponentSlots
    {
        public const int Capacity = 15;
        private long t0,t1,t2,t3,t4,t5,t6,t7,t8,t9,t10,t11,t12,t13,t14;
        private int c0,c1,c2,c3,c4,c5,c6,c7,c8,c9,c10,c11,c12,c13,c14;
        public void Add(int i, long ticks) { switch(i) {
            case 0: t0 += ticks; c0++; break;
            case 1: t1 += ticks; c1++; break;
            case 2: t2 += ticks; c2++; break;
            case 3: t3 += ticks; c3++; break;
            case 4: t4 += ticks; c4++; break;
            case 5: t5 += ticks; c5++; break;
            case 6: t6 += ticks; c6++; break;
            case 7: t7 += ticks; c7++; break;
            case 8: t8 += ticks; c8++; break;
            case 9: t9 += ticks; c9++; break;
            case 10: t10 += ticks; c10++; break;
            case 11: t11 += ticks; c11++; break;
            case 12: t12 += ticks; c12++; break;
            case 13: t13 += ticks; c13++; break;
            case 14: t14 += ticks; c14++; break;
            default: throw new ArgumentOutOfRangeException(nameof(i)); } }
        public long Ticks(int i) { switch(i) {
            case 0: return t0; case 1: return t1; case 2: return t2; case 3: return t3; case 4: return t4; case 5: return t5; case 6: return t6; case 7: return t7; case 8: return t8; case 9: return t9; case 10: return t10; case 11: return t11; case 12: return t12; case 13: return t13; case 14: return t14;
            default: throw new ArgumentOutOfRangeException(nameof(i)); } }
        public int Calls(int i) { switch(i) {
            case 0: return c0; case 1: return c1; case 2: return c2; case 3: return c3; case 4: return c4; case 5: return c5; case 6: return c6; case 7: return c7; case 8: return c8; case 9: return c9; case 10: return c10; case 11: return c11; case 12: return c12; case 13: return c13; case 14: return c14;
            default: throw new ArgumentOutOfRangeException(nameof(i)); } }
    }
    public struct DiagnosticDispatchObservation
    {
        public string operation;
        public int unityFrame;
        public long ticks;
    }
    public sealed class DiagnosticDispatchLog
    {
        private readonly DiagnosticDispatchObservation[] rows;
        private int count;
        public long Total { get; private set; }
        public long Dropped => Total - count;
        public DiagnosticDispatchLog(int capacity) { if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity)); rows=new DiagnosticDispatchObservation[capacity]; }
        public void Clear() { Array.Clear(rows,0,count); count=0; Total=0; }
        public void Add(string operation,int frame,long ticks) {
            Total++; if(count<rows.Length) rows[count++]=new DiagnosticDispatchObservation { operation=operation,unityFrame=frame,ticks=ticks };
        }
        // Only called by reports, never by the sampling/dispatch path.
        public DiagnosticDispatchObservation[] Snapshot() { var copy=new DiagnosticDispatchObservation[count];Array.Copy(rows,copy,count);return copy; }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct FrameObservation
    {
        public long StartTicks, EndTicks, Gc0Delta, Gc1Delta, Gc2Delta, AllocationBytes;
        public int PriorUnityFrame, CurrentUnityFrame;
        public double CpuLatestMs, GpuLatestMs, NativeMinute;
        public ulong TimingSourceTimestamp;
        public float TimeScale;
        public bool GcAvailable, AllocationAvailable, CpuAvailable, GpuAvailable, TimingTimestampAvailable, TimingRepeated, NativeMinuteAvailable;
    }
    public sealed class FrameObservationLog
    {
        public const int DefaultCapacity = 65536;
        private readonly FrameObservation[] rows;
        private long priorTicks;
        private int priorFrame, gc0,gc1,gc2;
        private ulong priorTiming;
        public int Count { get; private set; }
        public long Dropped { get; private set; }
        public long Duplicates { get; private set; }
        public long InvalidOrder { get; private set; }
        public long SkippedUnityFrames { get; private set; }
        public int Capacity => rows.Length;
        // Marshaled layout includes native bool padding; this is not actual managed array size.
        public long MarshaledStorageEstimateBytes => (long)Marshal.SizeOf(typeof(FrameObservation)) * rows.Length;
        public long LastTicks => priorTicks;
        public FrameObservationLog(int capacity, long ticks, int frame, int g0, int g1, int g2) {
            if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));
            rows=new FrameObservation[capacity];ResetSeed(ticks,frame,g0,g1,g2);
        }
        public void ResetSeed(long ticks,int frame,int g0,int g1,int g2) {
            if(Count!=0||Dropped!=0)throw new InvalidOperationException("Cannot reset a populated observation log.");
            priorTicks=ticks;priorFrame=frame;gc0=g0;gc1=g1;gc2=g2;priorTiming=0;
        }
        public FrameObservation Get(int index) { if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));return rows[index]; }
        // Duplicate/reversed observations do not move the seed or manufacture an interval.
        public bool Add(long ticks,int frame,int g0,int g1,int g2,FrameObservation value) {
            if(frame==priorFrame){Duplicates++;return false;}
            if(frame<priorFrame||ticks<priorTicks){InvalidOrder++;return false;}
            value.StartTicks=priorTicks;value.EndTicks=ticks;value.PriorUnityFrame=priorFrame;value.CurrentUnityFrame=frame;
            value.GcAvailable=g0>=gc0&&g1>=gc1&&g2>=gc2;
            value.Gc0Delta=(long)g0-gc0;value.Gc1Delta=(long)g1-gc1;value.Gc2Delta=(long)g2-gc2;
            value.TimingTimestampAvailable=value.TimingSourceTimestamp!=0;
            value.TimingRepeated=value.TimingTimestampAvailable&&value.TimingSourceTimestamp==priorTiming;
            if(value.TimingTimestampAvailable)priorTiming=value.TimingSourceTimestamp;
            SkippedUnityFrames+=(long)frame-priorFrame-1;
            priorTicks=ticks;priorFrame=frame;gc0=g0;gc1=g1;gc2=g2;
            if(Count<rows.Length)rows[Count++]=value;else Dropped++;
            return true;
        }
        private static string Number(double n) => n.ToString("R",CultureInfo.InvariantCulture);
        private static string Optional(bool available,double n) => available?Number(n):"";
        public void WriteCsv(TextWriter writer) {
            writer.WriteLine("startTicks,endTicks,priorUnityFrame,currentUnityFrame,gc0Delta,gc1Delta,gc2Delta,gcAvailable,allocationBytes,allocationAvailable,cpuLatestMs,cpuAvailable,gpuLatestMs,gpuAvailable,timingSourceTimestamp,timingTimestampAvailable,timingRepeated,nativeMinute,nativeMinuteAvailable,timeScale,paused");
            for(int i=0;i<Count;i++) { var r=rows[i];
                writer.WriteLine(string.Join(",",r.StartTicks.ToString(CultureInfo.InvariantCulture),r.EndTicks.ToString(CultureInfo.InvariantCulture),r.PriorUnityFrame.ToString(CultureInfo.InvariantCulture),r.CurrentUnityFrame.ToString(CultureInfo.InvariantCulture),
                    Optional(r.GcAvailable,r.Gc0Delta),Optional(r.GcAvailable,r.Gc1Delta),Optional(r.GcAvailable,r.Gc2Delta),r.GcAvailable?"1":"0",
                    r.AllocationAvailable?r.AllocationBytes.ToString(CultureInfo.InvariantCulture):"",r.AllocationAvailable?"1":"0",Optional(r.CpuAvailable,r.CpuLatestMs),r.CpuAvailable?"1":"0",Optional(r.GpuAvailable,r.GpuLatestMs),r.GpuAvailable?"1":"0",
                    r.TimingTimestampAvailable?r.TimingSourceTimestamp.ToString(CultureInfo.InvariantCulture):"",r.TimingTimestampAvailable?"1":"0",r.TimingRepeated?"1":"0",Optional(r.NativeMinuteAvailable,r.NativeMinute),r.NativeMinuteAvailable?"1":"0",Number(r.TimeScale),r.TimeScale==0?"1":"0"));
            }
        }
    }
}
