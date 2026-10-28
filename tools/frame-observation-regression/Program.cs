using System;
using System.IO;
using FoodDemandAuditProbe;
static class Program
{
    static int checks;
    static void Check(bool ok,string name) { checks++;if(!ok)throw new Exception(name); }
    static void Main() {
        var log=new FrameObservationLog(2,100,10,1,2,3);
        var row=new FrameObservation { AllocationAvailable=true,AllocationBytes=0,CpuAvailable=true,CpuLatestMs=0,GpuAvailable=true,GpuLatestMs=8,TimingSourceTimestamp=1000,TimeScale=1 };
        Check(!log.Add(110,10,9,9,9,row),"same frame rejected");
        Check(log.Add(120,11,2,4,6,row),"next frame");
        var first=log.Get(0);
        Check(first.StartTicks==100&&first.EndTicks==120&&first.PriorUnityFrame==10&&first.CurrentUnityFrame==11,"duplicate does not move seed");
        Check(first.GcAvailable&&first.Gc0Delta==1&&first.Gc1Delta==2&&first.Gc2Delta==3,"GC per interval");
        Check(first.AllocationAvailable&&first.AllocationBytes==0&&first.CpuAvailable&&first.CpuLatestMs==0,"zero differs from unknown");
        Check(first.TimingTimestampAvailable&&!first.TimingRepeated,"first timestamp");
        Check(log.Add(150,14,2,4,7,row),"gap accepted");
        Check(log.Get(1).TimingRepeated&&log.SkippedUnityFrames==2,"repeated timing and gap explicit");
        Check(!log.Add(149,15,2,4,7,row)&&!log.Add(160,13,2,4,7,row),"invalid orders rejected");
        Check(log.Add(170,15,0,4,7,default),"counter reset observation accepted");
        Check(log.Count==2&&log.Dropped==1&&log.LastTicks==170&&log.InvalidOrder==2&&log.Duplicates==1,"bounded truncation and continued seed");
        var unknown=new FrameObservationLog(3,0,0,0,0,0);
        unknown.Add(10,1,0,0,0,default);
        unknown.Add(20,2,0,0,0,row);
        unknown.Add(30,3,0,0,0,default);
        Check(!unknown.Get(0).TimingTimestampAvailable&&!unknown.Get(0).TimingRepeated&&!unknown.Get(2).TimingTimestampAvailable&&!unknown.Get(2).CpuAvailable,"stale return cannot inherit availability");
        var reset=new FrameObservationLog(1,0,0,3,2,1);reset.Add(1,1,1,2,1,default);
        Check(!reset.Get(0).GcAvailable&&reset.Get(0).Gc0Delta==-2,"counter reset unavailable");
        using(var writer=new StringWriter(System.Globalization.CultureInfo.InvariantCulture)) {
            unknown.WriteCsv(writer);var lines=writer.ToString().Trim().Split('\n');var fields=lines[1].TrimEnd('\r').Split(',');
            Check(lines.Length==4&&fields.Length==21,"CSV rows/schema");
            Check(fields[8]==""&&fields[9]=="0"&&fields[10]==""&&fields[11]=="0"&&fields[14]=="","unknown CSV empty");
            var zero=lines[2].TrimEnd('\r').Split(',');Check(zero[8]=="0"&&zero[9]=="1"&&zero[10]=="0"&&zero[11]=="1","zero CSV retained");
        }
        var slots=new ComponentSlots();for(int i=0;i<ComponentSlots.Capacity;i++)slots.Add(i,i+1);var retained=slots;slots.Add(0,100);
        Check(retained.Ticks(0)==1&&retained.Calls(0)==1&&slots.Ticks(0)==101&&slots.Calls(0)==2,"retained value isolation");
        for(int i=0;i<ComponentSlots.Capacity;i++)Check(retained.Ticks(i)==i+1&&retained.Calls(i)==1,"all component slots");
        bool rejected=false;try{slots.Add(ComponentSlots.Capacity,1);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"capacity guard");
        var hot=new FrameObservationLog(11000,0,0,0,0,0);for(int i=1;i<1000;i++)hot.Add(i,i,0,0,0,row);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=1000;i<11000;i++){hot.Add(i,i,0,0,0,row);slots.Add(i%7,1);}long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(allocated==0&&hot.Count==10999&&hot.Dropped==0,"retained writes and component accumulation allocate zero after initialization");
        var full=new FrameObservationLog(1,0,0,0,0,0);full.Add(1,1,0,0,0,row);before=GC.GetAllocatedBytesForCurrentThread();for(int i=2;i<10002;i++)full.Add(i,i,0,0,0,row);allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(allocated==0&&full.Dropped==10000,"drop path allocates zero");
        var dispatch=new DiagnosticDispatchLog(2);const string operation="status";
        before=GC.GetAllocatedBytesForCurrentThread();dispatch.Add(operation,11,120);dispatch.Add("frames-stop",12,130);dispatch.Add(operation,13,140);allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        var snapshot=dispatch.Snapshot();Check(allocated==0&&dispatch.Total==3&&dispatch.Dropped==1&&snapshot.Length==2,"dispatch bounded no allocations");
        Check(snapshot[0].operation=="status"&&snapshot[0].unityFrame==11&&snapshot[0].ticks==120&&snapshot[1].operation=="frames-stop","dispatch exact operation/frame/time");
        dispatch.Clear();Check(dispatch.Total==0&&dispatch.Dropped==0&&dispatch.Snapshot().Length==0&&snapshot[0].operation=="status","dispatch clear and snapshot isolation");
        var seeded=new FrameObservationLog(1,0,0,0,0,0);seeded.ResetSeed(100,10,1,2,3);seeded.Add(120,11,1,2,3,default);Check(seeded.Get(0).StartTicks==100,"allocation-before-seed support");
        rejected=false;try{seeded.ResetSeed(0,0,0,0,0);}catch(InvalidOperationException){rejected=true;}Check(rejected,"cannot erase populated trace seed");
        Console.WriteLine(checks+" checks passed; marshaled row estimate bytes="+(hot.MarshaledStorageEstimateBytes/hot.Capacity)+"; default marshaled storage estimate="+(hot.MarshaledStorageEstimateBytes/hot.Capacity*FrameObservationLog.DefaultCapacity));
    }
}
