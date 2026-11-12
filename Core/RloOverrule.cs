using System;
using System.Collections.Generic;
using System.Text;

namespace PersistentCity.Core
{

    public enum RloLivingState : byte { Absent, NotInRelease, Off, On, Unknown }

    public readonly struct RloLivingReading
    {
        public RloLivingReading(RloLivingState state, string reason = "") { State = state; Reason = reason ?? ""; }
        public RloLivingState State { get; }
        public string Reason { get; }
        public override string ToString() => Reason.Length == 0 ? RloOverrule.Word(State) : RloOverrule.Word(State) + " (" + Reason + ")";
    }

    public enum NativeShopPath : byte { Generation, Batch, Process, Draw }

    public static class RloOverrule
    {
        public static bool AdmitsNative(NativeShopPath path, bool ownedHour, bool ownCheckout = false)
            => !ownedHour || path == NativeShopPath.Process && ownCheckout;

        public static string? CheckoutRefusal(string? hookRefusal, RloLivingReading living)
            => hookRefusal ?? (living.State == RloLivingState.Unknown
                ? "RLO Living population state unknown (" + (living.Reason.Length == 0 ? "unreadable" : living.Reason)
                    + "); no shop is taken over in an unknown state"
                : null);

        public static bool HoldsAcquisitions(RloLivingState living) => living == RloLivingState.Unknown;

        public static bool QuotesClaimedLines(RloLivingState living) => living == RloLivingState.Absent;

        public static bool RetiresNativeCar(bool active, bool heldGroup, bool presetPath, bool ignored, bool ordinary, bool taxi, bool visible)
            => active && heldGroup && !presetPath && !ignored && ordinary && !taxi && !visible;

        public static bool RetiresNewNativeLoad(bool heldGroup, bool presetPath, bool ignored, bool ordinary, bool taxi)
            => RetiresNativeCar(true, heldGroup, presetPath, ignored, ordinary, taxi, visible: false);

        public static string Word(RloLivingState state) => state switch
        {
            RloLivingState.Absent => "RLO not loaded", RloLivingState.NotInRelease => "no Living population in this RLO release",
            RloLivingState.Off => "Living population off", RloLivingState.On => "Living population on", _ => "Living population unknown",
        };

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
        public int Toggles { get; private set; }
        public IReadOnlyList<string> Changes => changes;

        public bool Refresh() => Apply(Safe());

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

    public sealed class RloSceneHold
    {
        private readonly Func<bool?> read;
        private readonly Func<bool?>? saved;
        private readonly Action<bool> write;
        private bool? original;

        public RloSceneHold(string name, Func<bool?> read, Action<bool> write, Func<bool?>? saved = null)
        {
            Name = name ?? ""; this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.write = write ?? throw new ArgumentNullException(nameof(write)); this.saved = saved;
        }

        public string Name { get; }
        public bool Holding => original.HasValue;

        public bool Hold()
        {
            bool? now = read();
            if (!now.HasValue) return false;
            if (!original.HasValue || now.Value) original = now.Value;
            if (now.Value) write(false);
            return true;
        }

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

    public sealed class NativeLoadWatch
    {
        public static double MaxMove(double seconds) => 10 + 100 * Math.Max(0, seconds);

        private bool[] leftover = Array.Empty<bool>();
        private double[] x = Array.Empty<double>(), z = Array.Empty<double>();

        public bool Primed { get; private set; }

        public void Prime(int count, Func<int, bool> activeHeld, Func<int, (double X, double Z)> position)
        {
            if (activeHeld == null) throw new ArgumentNullException(nameof(activeHeld));
            if (position == null) throw new ArgumentNullException(nameof(position));
            Grow(count);
            for (int i = 0; i < count; i++)
            {
                leftover[i] = activeHeld(i);
                if (leftover[i]) { var p = position(i); x[i] = p.X; z[i] = p.Z; }
            }
            Primed = true;
        }

        public bool IsLoad(int index, bool activeHeld, double posX, double posZ, double seconds)
        {
            if (!Primed) throw new InvalidOperationException("Prime the watch when the hold starts.");
            Grow(index + 1);
            if (!activeHeld) { leftover[index] = false; return false; }
            if (!leftover[index]) return true;
            double dx = posX - x[index], dz = posZ - z[index], limit = MaxMove(seconds);
            if (dx * dx + dz * dz > limit * limit) { leftover[index] = false; return true; }
            x[index] = posX; z[index] = posZ;
            return false;
        }

        public void Accept(int index, double posX, double posZ) { Grow(index + 1); leftover[index] = true; x[index] = posX; z[index] = posZ; }

        public void Forget(int index) { if (index >= 0 && index < leftover.Length) leftover[index] = false; }

        public void Added(int index)
        {
            if (!Primed || index < 0) return;
            Grow(index + 1);
            leftover[index] = false;
        }

        public void Reset() { Primed = false; Array.Clear(leftover, 0, leftover.Length); }

        private void Grow(int count)
        {
            if (count <= leftover.Length) return;
            int size = Math.Max(count, leftover.Length * 2);
            Array.Resize(ref leftover, size); Array.Resize(ref x, size); Array.Resize(ref z, size);
        }
    }
}
