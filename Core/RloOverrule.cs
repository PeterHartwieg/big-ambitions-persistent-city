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

        public static string Word(RloLivingState state) => state switch
        {
            RloLivingState.Absent => "RLO not loaded", RloLivingState.NotInRelease => "no Living population in this RLO release",
            RloLivingState.Off => "Living population off", RloLivingState.On => "Living population on", _ => "Living population unknown",
        };

        /// <summary>One status line per overlap area: what Persistent City owns and what stays RLO's.
        /// <paramref name="calculatorsReplaced"/>: native customer calculators held by another type (F8h scan), -1 unread.</summary>
        public static string Describe(RloLivingReading living, int toggles, int calculatorsReplaced, string? checkoutRefusal,
            bool crowdsOverruled, int rloCrowdZones, bool trafficHeld)
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
                    ? "ordinary native groups held for Persistent City cars; cars RLO holds are never retired"
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
}
