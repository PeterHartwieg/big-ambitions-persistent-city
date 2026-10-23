using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private const string ClockAuditOwner="persistentcity.clock-audit.probe";
        private static Probe? clockAudit;
        private bool clockAuditActive;
        private float auditStarted;
        private int peakServing,peakTravelling;
        private double maxLag;
        private readonly List<double> advanceSamples=new List<double>();
        private void StartClockAudit()
        {
            StopClockAudit();clockAudit=this;clockAuditActive=true;auditStarted=Time.realtimeSinceStartup;
            peakServing=peakTravelling=0;maxLag=0;advanceSamples.Clear();
            new Harmony(ClockAuditOwner).Patch(AccessTools.Method(typeof(PopulationRuntime),"AdvancePopulation"),
                prefix:new HarmonyMethod(typeof(Probe),nameof(AdvanceBefore)),postfix:new HarmonyMethod(typeof(Probe),nameof(AdvanceAfter)));
        }
        private static void AdvanceBefore(out long __state) => __state=clockAudit?.clockAuditActive==true?Stopwatch.GetTimestamp():0;
        private static void AdvanceAfter(long __state)
        {
            if(__state==0 || clockAudit==null)return;
            if(clockAudit.advanceSamples.Count>=2000000)throw new InvalidOperationException("Clock audit sample limit");
            clockAudit.advanceSamples.Add((Stopwatch.GetTimestamp()-__state)*1000.0/Stopwatch.Frequency);
        }
        private void SampleClockAudit()
        {
            if(!clockAuditActive)return;
            var state=Simulation.State;int serving=0,travelling=0;
            foreach(var meal in state.Food!.Residents) {if(meal.Status==MealStatus.Serving)serving++;if(meal.Status==MealStatus.Travelling)travelling++;}
            peakServing=Math.Max(peakServing,serving);peakTravelling=Math.Max(peakTravelling,travelling);
            double native=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute;
            maxLag=Math.Max(maxLag,native-state.Minute);
        }
        private object ClockAuditStatus()
        {
            var sorted=advanceSamples.OrderBy(x=>x).ToArray();
            var policy=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.NativeCityClock");
            object? Read(string name)=>policy?.GetProperty(name,BindingFlags.Public|BindingFlags.Static)?.GetValue(null);
            var ride=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("DrivenRides.Ride",false)).FirstOrDefault(t=>t!=null);
            bool driving=ride?.GetProperty("IsDriving",BindingFlags.Public|BindingFlags.Static)?.GetValue(null) is bool flag && flag;
            return new {factor=Read("Factor"),active=Read("Active"),drivenRides=Read("CoordinatesDrivenRides"),
                rideActive=ride?.GetProperty("Active",BindingFlags.Public|BindingFlags.Static)?.GetValue(null)!=null,rideDriving=driving,
                realSeconds=Time.realtimeSinceStartup-auditStarted,frames=sorted.Length,totalAdvanceMs=sorted.Sum(),
                p95AdvanceMs=sorted.Length==0?0:sorted[(int)(sorted.Length*.95)],maxAdvanceMs=sorted.Length==0?0:sorted.Last(),
                peakServing,peakTravelling,maxLag,timeScale=Time.timeScale,fixedDeltaTime=Time.fixedDeltaTime,
                note="Diagnostic advancement timing includes trace capture, excludes rendering and route preparation. Peaks/lag sampled every 0.2 real seconds."};
        }
        private void StopClockAudit()
        {clockAuditActive=false;if(ReferenceEquals(clockAudit,this)){new Harmony(ClockAuditOwner).UnpatchAll(ClockAuditOwner);clockAudit=null;}}
    }
}
