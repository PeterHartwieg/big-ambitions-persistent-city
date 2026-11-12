using System;
using System.Linq;
using HarmonyLib;
using PersistentCity.Core;
using UI;
using UI.Load;

namespace PersistentCity.Runtime
{
    internal static class NativeCityClock
    {
        private const string Owner = "hart.persistentcity.cityclock.v1";
        private static Harmony? patches;
        private static object? save;
        private static Func<double>? nativeClock;
        private static Func<double>? logicalClock;
        private static Func<double, bool>? endingEpochDrained;
        private static Func<bool>? populationCaughtUp, economyActive;
        public static long HeldEconomySkipTicks { get; private set; }
        public static long HeldOrdinaryTicks { get; private set; }
        public static long BoundaryClips { get; private set; }
        public static double WithheldMinutes { get; private set; }
        public static double LagMinutes { get; private set; }
        public static double MaximumLagMinutes { get; private set; }
        public static bool AccountingLimited { get; private set; }
        public static bool Refused { get; private set; }
        private static long waitingSince, lastHeldAt;
        public static double WaitingSeconds => waitingSince == 0 ? 0
            : (System.Diagnostics.Stopwatch.GetTimestamp() - waitingSince) / (double)System.Diagnostics.Stopwatch.Frequency;
        public static double MaximumRecoverySeconds { get; private set; }
        public static bool PacingAffected => Refused || waitingSince != 0 || lastHeldAt != 0
            && (System.Diagnostics.Stopwatch.GetTimestamp() - lastHeldAt) < 3L * System.Diagnostics.Stopwatch.Frequency;
        public static void ObserveSimulationLag(double target, double logical)
        {
            LagMinutes = Math.Max(0, target - logical);
            MaximumLagMinutes = Math.Max(MaximumLagMinutes, LagMinutes);
        }
        public static bool CheckInputEpoch(double native, double logical)
        {
            ObserveSimulationLag(AccountingClock.Target(native), logical);
            Refused |= !AccountingClock.Compatible(logical, native);
            if (endingEpochDrained != null && !endingEpochDrained(native)) Refused = true;
            return !Refused;
        }
        public static double Factor { get; private set; } = 1;
        public static bool CoordinatesDrivenRides { get; private set; }
        public static bool Active => patches != null && save != null && ReferenceEquals(save, SaveGameManager.Current);
        public static bool ProcessingEconomySkip => Active && !LoadScene.isLoading
            && InstanceBehavior<UIs>.Instance?.timeMachine?.isRunning == true && economyActive?.Invoke() == true;
        private static bool OrdinaryTick => Active && !LoadScene.isLoading
            && InstanceBehavior<UIs>.Instance?.timeMachine != null && !InstanceBehavior<UIs>.Instance.timeMachine.isRunning;

        public static void Start(object currentSave, double factor) => Start(currentSave, factor, null, null, null);
        public static void Start(object currentSave, double factor, Func<bool>? readPopulationReady, Func<bool>? ownsEconomy, Func<double>? readNative, Func<double>? readLogical = null, Func<double, bool>? readEndingEpochDrained = null)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor < .1 || factor > 1)
                throw new ArgumentOutOfRangeException(nameof(factor), "GameClockFactor must be between 0.1 and 1.");
            if (Armed) Stop();
            if (patches != null) throw new InvalidOperationException("City clock already active.");
            if (factor == 1 && readPopulationReady == null) return;
            if (!ReferenceEquals(currentSave, SaveGameManager.Current)) throw new InvalidOperationException("City clock save changed.");
            if (Harmony.HasAnyPatches(Owner)) throw new InvalidOperationException("City clock patch ID already owned.");
            var installing = new Harmony(Owner);
            try
            {
                var tick = AccessTools.Method(typeof(GameManager), nameof(GameManager.RunMainGameTick), new[] { typeof(float) })
                    ?? throw new MissingMethodException("GameManager.RunMainGameTick(float)");
                installing.Patch(tick, prefix: new HarmonyMethod(typeof(NativeCityClock), nameof(TickPrefix)) { priority = Priority.First + 1 });
                PatchAccounting(installing, tick);
                var ride = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DrivenRides.Ride", false)).FirstOrDefault(t => t != null);
                if (ride != null && factor != 1)
                {
                    var cap = AccessTools.Method(ride, "CapTick", new[] { typeof(float).MakeByRefType() })
                        ?? throw new MissingMethodException("DrivenRides.Ride.CapTick(ref float)");
                    if (!cap.IsStatic || cap.ReturnType != typeof(void)) throw new InvalidOperationException("Unsupported Driven Rides clock contract.");
                    installing.Patch(cap, prefix: new HarmonyMethod(typeof(NativeCityClock), nameof(RidePrefix)),
                        postfix: new HarmonyMethod(typeof(NativeCityClock), nameof(RidePostfix)));
                    CoordinatesDrivenRides = true;
                }
                Factor = factor; save = currentSave; patches = installing;
                populationCaughtUp = readPopulationReady; economyActive = ownsEconomy; nativeClock = readNative; logicalClock = readLogical;
                endingEpochDrained = readEndingEpochDrained;
                ResetDiagnostics();
            }
            catch { installing.UnpatchAll(Owner); CoordinatesDrivenRides = false; throw; }
        }

        public static bool Armed => patches != null && save == null;
        public static bool Bound => patches != null && save != null;
        public static void Arm()
        {
            if (patches != null) return;
            if (Harmony.HasAnyPatches(Owner)) throw new InvalidOperationException("City clock patch ID already owned.");
            var installing = new Harmony(Owner);
            try
            {
                var tick = AccessTools.Method(typeof(GameManager), nameof(GameManager.RunMainGameTick), new[] { typeof(float) })
                    ?? throw new MissingMethodException("GameManager.RunMainGameTick(float)");
                PatchAccounting(installing, tick);
                ResetDiagnostics();
                patches = installing;
            }
            catch { installing.UnpatchAll(Owner); throw; }
        }

        private static void PatchAccounting(Harmony installing, System.Reflection.MethodInfo tick)
            => installing.Patch(tick, prefix: new HarmonyMethod(typeof(NativeCityClock), nameof(AccountingPrefix)) { priority = Priority.Last });

        private static void TickPrefix(ref float deltaTimeWithMultiplier)
        {
            if (!Active || Refused || LoadScene.isLoading || deltaTimeWithMultiplier <= 0 || float.IsInfinity(deltaTimeWithMultiplier)) return;
            if (OrdinaryTick) deltaTimeWithMultiplier *= (float)Factor;
        }
        public static bool StartupHold { get; set; }
        private static void AccountingPrefix(ref float deltaTimeWithMultiplier)
        {
            AccountingLimited = false;
            // Installed but not bound to the current save (the startup guard, or a save change PopulationRuntime has
            // not handled yet): no native minute may pass before that save's snapshot attaches.
            if (patches != null && !Active) { if (deltaTimeWithMultiplier > 0) deltaTimeWithMultiplier = 0; return; }
            if (LoadScene.isLoading && StartupHold && deltaTimeWithMultiplier > 0) { deltaTimeWithMultiplier = 0; return; }
            if (!Active || LoadScene.isLoading || economyActive?.Invoke() != true || nativeClock == null || populationCaughtUp == null) return;
            double native = nativeClock(), logical = logicalClock?.Invoke() ?? native;
            if (!CheckInputEpoch(native, logical)) return;
            if (deltaTimeWithMultiplier <= 0) return;
            float requested = deltaTimeWithMultiplier;
            bool ready = !Refused && populationCaughtUp();
            float allowed = AccountingClock.Limit(ready, native, requested);
            AccountingLimited = allowed < requested;
            WithheldMinutes += requested - allowed;
            if (!ready)
            {
                if (ProcessingEconomySkip) HeldEconomySkipTicks++; else HeldOrdinaryTicks++;
                lastHeldAt = System.Diagnostics.Stopwatch.GetTimestamp();
                if (waitingSince == 0) waitingSince = lastHeldAt;
            }
            else
            {
                MaximumRecoverySeconds = Math.Max(MaximumRecoverySeconds, WaitingSeconds);
                waitingSince = 0;
                if (AccountingLimited) BoundaryClips++;
            }
            deltaTimeWithMultiplier = allowed;
        }
        private static void ResetDiagnostics()
        {
            HeldEconomySkipTicks = HeldOrdinaryTicks = BoundaryClips = waitingSince = lastHeldAt = 0;
            WithheldMinutes = LagMinutes = MaximumLagMinutes = MaximumRecoverySeconds = 0;
            AccountingLimited = Refused = StartupHold = false;
        }
        private static void RidePrefix(float __0, out float __state) => __state = OrdinaryTick ? __0 : float.NaN;
        private static void RidePostfix(ref float __0, float __state)
        {
            if (!float.IsNaN(__state) && __0 > 0 && OrdinaryTick) __0 = __state;
        }
        public static void Stop()
        {
            save = null;
            try { patches?.UnpatchAll(Owner); }
            finally { patches = null; Factor = 1; CoordinatesDrivenRides = false; nativeClock = logicalClock = null; endingEpochDrained = null; populationCaughtUp = economyActive = null; ResetDiagnostics(); }
        }
    }
}
