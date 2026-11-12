using System;
using System.Collections.Generic;
using System.Text;

namespace PersistentCity.Core
{
    // Peter, 12 Nov 2026 (t137u): Persistent City stays independent, stays compatible with Real Life Overhaul 1.0 and
    // overrules RLO wherever the two overlap, with RLO's Living population on or off. RLO keeps everything Persistent City
    // does not simulate. Compatibility/RloLivingPopulation reads the switch; Runtime applies the decisions here.

    /// <summary>RLO's Living population master switch (<c>RealLifeOverhaul.Settings.LivingPopulation</c>) as read now.
    /// Absent: RLO is not loaded. NotInRelease: the loaded RLO has neither the switch nor a living population (0.4x).
    /// Unknown: the surface is missing or has another shape, or a read failed; nothing overlapping starts on it.</summary>
    public enum RloLivingState : byte { Absent, NotInRelease, Off, On, Unknown }

    public readonly struct RloLivingReading
    {
        public RloLivingReading(RloLivingState state, string reason = "") { State = state; Reason = reason ?? ""; }
        public RloLivingState State { get; }
        public string Reason { get; }
        public override string ToString() => Reason.Length == 0 ? RloOverrule.Word(State) : RloOverrule.Word(State) + " (" + Reason + ")";
    }

    /// <summary>The native customer paths of one player shop that Persistent City gates for an owned food hour.
    /// Generation is the native draw for the hour (where RLO's replaced calculators scale the count), Batch the hourly
    /// sales batch, Process one native customer, Draw the native list read through CustomerEntriesHelper.</summary>
    public enum NativeShopPath : byte { Generation, Batch, Process, Draw }

    public static class RloOverrule
    {
        /// <summary>The owned-shop gate, the same with Living population on, off or unknown: an owned food hour admits no
        /// native generation, batch, list or customer except Persistent City's own checkout (or its probe), whose private
        /// order no RLO basket code can reach. RLO's scaled calculators and basket lines therefore add neither customers
        /// nor sold lines at owned shops. A native-owned hour is RLO's and the game's, untouched.</summary>
        public static bool AdmitsNative(NativeShopPath path, bool ownedHour, bool ownCheckout = false)
            => !ownedHour || path == NativeShopPath.Process && ownCheckout;

        /// <summary>The food.checkout.shop refusal of one city load: a foreign Harmony refusal (F8h) first, then an
        /// unreadable Living population switch. RLO's presence, Living on or off, and calculator replacement never refuse:
        /// Persistent City overrules them at the shops it owns.</summary>
        public static string? CheckoutRefusal(string? hookRefusal, RloLivingReading living)
            => hookRefusal ?? (living.State == RloLivingState.Unknown
                ? "RLO Living population state unknown (" + (living.Reason.Length == 0 ? "unreadable" : living.Reason)
                    + "); no shop is taken over in an unknown state"
                : null);

        /// <summary>Review Sol-r1-1: while the switch is Unknown, a boundary takes no shop over (as while the food contract
        /// warms: FoodAuthority.Boundary with contractReady false defers the acquisition). Hours already owned keep their
        /// gate and checkout, and hand-backs never wait.</summary>
        public static bool HoldsAcquisitions(RloLivingState living) => living == RloLivingState.Unknown;

        /// <summary>Review Sol-r1-3, the non-food need purchase (NativeSalePath): the claimed native entry is a service
        /// slot; its order lines become the resident's basket only without RLO. With RLO loaded its loyalty and interiors
        /// code appends lines to those entries, which nothing marks, so the basket is written as for an unclaimed slot
        /// and the claimed entry is still retired: no RLO line is sold to a resident, and no native customer is added.</summary>
        public static bool QuotesClaimedLines(RloLivingState living) => living == RloLivingState.Absent;

        /// <summary>Review Sol-r2-1: whether the own-car adapter retires a live native car now. While Persistent City holds
        /// a group at share 0, native draws no car of it, so an active car of a held group was left over or loaded past the
        /// shares (RLO's rival pass-by calls TrafficManager.LoadVehicle directly); it is retired while out of view. A car
        /// RLO holds for a scene (preset path or native ignore list), a taxi, a service or modded car, and a car of a group
        /// not held all stay.</summary>
        public static bool RetiresNativeCar(bool active, bool heldGroup, bool presetPath, bool ignored, bool ordinary, bool taxi, bool visible)
            => active && heldGroup && !presetPath && !ignored && ordinary && !taxi && !visible;

        /// <summary>Review Sol-r3-1: a car loaded into a held group during this frame (<see cref="NativeLoadWatch"/>) has
        /// not rendered yet, so it is retired whatever its renderers report; the other rules are those of
        /// <see cref="RetiresNativeCar"/>.</summary>
        public static bool RetiresNewNativeLoad(bool heldGroup, bool presetPath, bool ignored, bool ordinary, bool taxi)
            => RetiresNativeCar(true, heldGroup, presetPath, ignored, ordinary, taxi, visible: false);

        public static string Word(RloLivingState state) => state switch
        {
            RloLivingState.Absent => "RLO not loaded", RloLivingState.NotInRelease => "no Living population in this RLO release",
            RloLivingState.Off => "Living population off", RloLivingState.On => "Living population on", _ => "Living population unknown",
        };

        /// <summary>One status line per overlap area: what Persistent City owns and what stays RLO's.
        /// <paramref name="calculatorsReplaced"/>: native customer calculators held by another type (F8h scan), -1 unread.</summary>
        public static string Describe(RloLivingReading living, int toggles, int calculatorsReplaced, string? checkoutRefusal,
            bool crowdsOverruled, int rloCrowdZones, bool trafficHeld, int sceneSwitchesHeld = 0)
        {
            var text = new StringBuilder("Persistent City RLO overrule: ").Append(living.ToString());
            if (toggles > 0) text.Append(", ").Append(toggles).Append(" toggle(s) this session");
            text.Append('\n');
            if (living.State == RloLivingState.Absent) return text.Append("nothing to overrule\n").ToString();
            text.Append("shops: ").Append(checkoutRefusal == null
                ? "owned food shops take no native draw, batch or customer; Persistent City tickets are the only sales there"
                : "food.checkout.shop off (" + checkoutRefusal + "); every shop stays native");
            if (calculatorsReplaced > 0) text.Append("; ").Append(calculatorsReplaced).Append(" replaced customer calculator(s) overruled at owned shops");
            else if (calculatorsReplaced < 0) text.Append("; calculator scan not read");
            text.Append('\n')
                .Append("baskets: RLO loyalty and interiors lines stay on native entries; owned checkout sells private orders only\n")
                .Append("charges: Persistent City books no native rent, wage or tax; RLO household books stay RLO's\n")
                .Append("crowds: ").Append(crowdsOverruled
                    ? "ordinary standers off in every native zone" + (rloCrowdZones > 0 ? ", " + rloCrowdZones + " of them RLO's" : "") + "; RLO's own scene people kept"
                    : "not overruled (stationary crowds inactive)").Append('\n')
                .Append("traffic: ").Append(trafficHeld
                    ? "ordinary native groups held for Persistent City cars; " + (sceneSwitchesHeld > 0
                        ? "RLO staged accidents and reckless drivers stood down"
                        : "RLO accident switches not bound") + "; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired"
                    : "own cars inactive; native traffic as the game and RLO run it").Append('\n');
            return text.ToString();
        }
    }

    /// <summary>The Living population switch followed through a session on a coarse cadence (RLO itself checks it about
    /// once a real second). Unknown is sticky for the session: a surface that failed once is not trusted again until the
    /// next process. Main thread only; one read per <see cref="PollSeconds"/>.</summary>
    public sealed class RloLivingTracker
    {
        public const double PollSeconds = 1.0;
        private readonly Func<RloLivingReading> read;
        private double nextPoll = double.NegativeInfinity;
        private readonly List<string> changes = new List<string>();

        public RloLivingTracker(Func<RloLivingReading> read)
        {
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            Current = Safe();
        }

        public RloLivingReading Current { get; private set; }
        /// <summary>On/Off changes seen this session.</summary>
        public int Toggles { get; private set; }
        public IReadOnlyList<string> Changes => changes;

        /// <summary>Reads now (a city load), whatever the cadence. True when the state changed.</summary>
        public bool Refresh() => Apply(Safe());

        /// <summary>Reads at most once per <see cref="PollSeconds"/> of <paramref name="realSeconds"/>. True when the state changed.</summary>
        public bool Poll(double realSeconds)
        {
            if (realSeconds < nextPoll) return false;
            nextPoll = realSeconds + PollSeconds;
            return Apply(Safe());
        }

        private bool Apply(RloLivingReading next)
        {
            if (Current.State == RloLivingState.Unknown) return false;
            if (next.State == Current.State) { Current = next; return false; }
            if ((Current.State == RloLivingState.On || Current.State == RloLivingState.Off)
                && (next.State == RloLivingState.On || next.State == RloLivingState.Off)) Toggles++;
            changes.Add(RloOverrule.Word(Current.State) + " -> " + next);
            Current = next;
            return true;
        }

        private RloLivingReading Safe()
        {
            try { return read(); }
            catch (Exception error) { return new RloLivingReading(RloLivingState.Unknown, "read failed: " + error.Message); }
        }
    }

    /// <summary>Review Sol-r1-2: RLO switches whose scenes take over live native cars (staged accidents and reckless
    /// drivers pick ordinary moving cars from the native pool), held off while Persistent City holds the ordinary native
    /// groups for its own cars, and given back afterwards. The player's own value wins: one they set during the hold is
    /// the value restored. Pure; Compatibility/RloTrafficScenes reads and writes the fields.</summary>
    public sealed class RloSceneHold
    {
        private readonly Func<bool?> read;
        private readonly Func<bool?>? saved;
        private readonly Action<bool> write;
        private bool? original;

        /// <summary><paramref name="saved"/> reads the player's stored option (RLO keeps options in PlayerPrefs under
        /// <c>Settings.PrefsKey(id)</c>); null when it cannot be read. Review Sol-r2-2: the stored choice, not the field
        /// Persistent City holds off, decides what Release gives back.</summary>
        public RloSceneHold(string name, Func<bool?> read, Action<bool> write, Func<bool?>? saved = null)
        {
            Name = name ?? ""; this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.write = write ?? throw new ArgumentNullException(nameof(write)); this.saved = saved;
        }

        public string Name { get; }
        public bool Holding => original.HasValue;

        /// <summary>Starts or keeps the hold: records the player's value the first time, and again whenever the player
        /// switched it back on, then writes off. False when the switch cannot be read.</summary>
        public bool Hold()
        {
            bool? now = read();
            if (!now.HasValue) return false;
            if (!original.HasValue || now.Value) original = now.Value;
            if (now.Value) write(false);
            return true;
        }

        /// <summary>Gives the player's latest choice back: the stored option when it can be read (so an option switched
        /// off during the hold stays off), otherwise the value recorded at the hold. Nothing when not holding.</summary>
        public void Release()
        {
            if (!original.HasValue) return;
            bool value = Saved() ?? original.Value;
            original = null;
            if (value) write(true);
        }

        private bool? Saved()
        {
            if (saved == null) return null;
            try { return saved(); }
            catch (Exception) { return null; }
        }
    }

    /// <summary>Review Sol-r3-1: which native pool cars became active since the previous frame. Runtime asks it once a
    /// frame in LateUpdate (after every Update and coroutine, before rendering), so a car another mod loaded directly
    /// during the frame (RLO's rival pass-by: TrafficManager.LoadVehicle) is seen before it has rendered once. Slots the
    /// pool adds later count as inactive before. The first frame only records: the cars already active then are
    /// leftovers, retired out of view by the existing step. One bool per pool slot; no allocation after growth.</summary>
    public sealed class NativeLoadWatch
    {
        private bool[] wasActive = Array.Empty<bool>();
        private int known;
        private bool primed;

        /// <summary>Starts one frame's pass over a pool of <paramref name="count"/> slots.</summary>
        public void Begin(int count)
        {
            if (count > wasActive.Length) Array.Resize(ref wasActive, Math.Max(count, wasActive.Length * 2));
            if (!primed) known = count;
            for (int i = known; i < count; i++) wasActive[i] = false;
            known = Math.Max(known, count);
        }

        /// <summary>Records slot <paramref name="index"/> for this frame. True when it is active now and was not on the
        /// previous frame, never on the first frame.</summary>
        public bool BecameActive(int index, bool active)
        {
            bool fresh = primed && active && !wasActive[index];
            wasActive[index] = active;
            return fresh;
        }

        /// <summary>Ends the frame's pass.</summary>
        public void End() => primed = true;

        /// <summary>Forgets every slot (the adapter released its hold).</summary>
        public void Reset() { primed = false; known = 0; Array.Clear(wasActive, 0, wasActive.Length); }
    }
}
