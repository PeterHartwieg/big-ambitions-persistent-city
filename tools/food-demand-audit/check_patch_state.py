#!/usr/bin/env python3
"""Run the actual clock/component patch callbacks together on a detached Harmony target.

Extracts callback bodies unchanged; minimal owner/Unity-clock doubles replace the game.
No game load, rig access or deployment. Outputs remain under ignored build/.
The pinned net472 Harmony detour backend is incompatible with the inspected Mac
.NET 10 host (dyld_image_info Span exception before the first patch). A failed
host run is not regression evidence; concurrent native audit/frames remains
the required check on that host. Do not replace the helper's pinned dependency.
"""
from pathlib import Path
import argparse
import subprocess

parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--harmony', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
source = Path(__file__).resolve().parent

def block(text, signature):
    start = text.index(signature)
    brace = text.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]

frames = (source / 'Frames.cs').read_text()
clock = (source / 'ClockAudit.cs').read_text()
assert 'new HarmonyMethod(typeof(ComponentPatches), nameof(ComponentPatches.Before))' in frames
assert 'new HarmonyMethod(typeof(ComponentPatches), nameof(ComponentPatches.Finally))' in frames
assert 'new HarmonyMethod(typeof(Probe),nameof(AdvanceBefore))' in clock
callbacks = '\n'.join(block(frames, signature) for signature in (
    'private struct ComponentMeasurement', 'private static class ComponentPatches',
    'private static void ComponentBefore', 'private static Exception? ComponentFinally'))
prefix = clock[clock.index('private static void AdvanceBefore'):]
callbacks += '\n' + prefix[:prefix.index(';') + 1]
callbacks += '\n' + block(clock, 'private static void AdvanceAfter')

fixture = r'''
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
static class Time { public static int frameCount = 1; }
sealed class Probe
{
    private static Probe? frameOwner, clockAudit;
    private bool recordingFrames = true, clockAuditActive = true;
    private int measurementDepth = 1, currentWorkFrame = 1, componentTimedExceptions, componentCrossFrameCalls;
    private readonly Dictionary<MethodBase, int> componentMethods = new();
    private readonly List<ComponentTiming> componentTimings = new() { new ComponentTiming() };
    private readonly List<double> advanceSamples = new();
    private const int AdvanceSampleLimit = 2000000;
    private long droppedAdvanceSamples;
    private readonly WorkFrame workFrame = new();
    private sealed class WorkFrame { public long[] ComponentTicks = new long[1]; public int[] ComponentCalls = new int[1]; }
    private sealed class ComponentTiming { public long Calls, OutsidePrimaryScopeCalls, TotalTicks; public List<double> CallTimes = new(); }
    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    private void ObserveWorkFrame(int frame) { if(frame != currentWorkFrame) throw new Exception("unexpected fixture frame"); }
    CALLBACKS
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Target(bool fail) { if(fail) throw new InvalidOperationException("original target failure"); }
    private static void Check(bool condition, string message) { if(!condition) throw new Exception(message); }
    public static void Main()
    {
        var target = AccessTools.Method(typeof(Probe), nameof(Target));
        var clockPrefix = AccessTools.Method(typeof(Probe), nameof(AdvanceBefore));
        var componentPrefix = AccessTools.Method(typeof(ComponentPatches), nameof(ComponentPatches.Before));
        Check(clockPrefix.DeclaringType != componentPrefix.DeclaringType, "Harmony state namespaces collide");
        Check(clockPrefix.GetParameters()[0].ParameterType != componentPrefix.GetParameters()[1].ParameterType,
            "fixture must exercise distinct state types");
        for(int order=0;order<2;order++)
        {
            var owner = new Probe(); frameOwner = clockAudit = owner;
            owner.componentMethods.Add(target, 0);
            var audit = new Harmony("test.clock"); var frame = new Harmony("test.frame");
            void PatchClock() => audit.Patch(target, prefix:new HarmonyMethod(clockPrefix), postfix:new HarmonyMethod(typeof(Probe),nameof(AdvanceAfter)));
            void PatchFrame() => frame.Patch(target, prefix:new HarmonyMethod(componentPrefix), finalizer:new HarmonyMethod(typeof(ComponentPatches),nameof(ComponentPatches.Finally)));
            try
            {
                if(order==0) { PatchClock(); PatchFrame(); } else { PatchFrame(); PatchClock(); }
                Target(false);
                Check(owner.advanceSamples.Count==1 && owner.componentTimings[0].Calls==1
                    && owner.workFrame.ComponentCalls[0]==1, "simultaneous callbacks lost independent state");
                bool original=false;
                try { Target(true); } catch(InvalidOperationException ex) { original=ex.Message=="original target failure"; }
                Check(original && owner.componentTimedExceptions==1 && owner.componentCrossFrameCalls==0,
                    "component finalizer failed to retain original exception/state");
                frame.UnpatchAll("test.frame"); Target(false);
                Check(owner.advanceSamples.Count==2 && owner.componentTimings[0].Calls==2,
                    "frame cleanup removed clock audit or retained component callback");
                audit.UnpatchAll("test.clock"); Target(false);
                Check(owner.advanceSamples.Count==2, "clock cleanup retained callback");
            }
            finally { frame.UnpatchAll("test.frame"); audit.UnpatchAll("test.clock"); frameOwner=clockAudit=null; }
        }
        Console.WriteLine("PASS actual extracted ClockAudit/Frames callbacks with pinned Harmony: both install orders, independent states, original exception, separate owner cleanup. No Unity execution.");
    }
}
'''.replace('CALLBACKS', callbacks)
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=False)
(output / 'Program.cs').write_text(fixture)
from xml.sax.saxutils import escape
(output / 'PatchState.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include="0Harmony"><HintPath>''' + escape(str(args.harmony.resolve())) + '''</HintPath></Reference></ItemGroup></Project>''')
subprocess.run(['dotnet', 'run', '--project', str(output / 'PatchState.csproj')], check=True)
