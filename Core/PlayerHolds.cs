using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>F2e: who blocked the person. Only the player's own avatar or vehicle is a cause; native bodies, RLO actors and
    /// service vehicles never are (Runtime PlayerContact reads only the player).</summary>
    public enum PlayerHoldCause { PlayerFoot, PlayerVehicle }

    /// <summary>Why a hold applied nothing. A refused hold is not logged and takes no sequence number.</summary>
    public enum PlayerHoldRefusal
    {
        None, InvalidInput, UnknownPerson, Visitor, NotTravelling, NotDrained, AllowanceSpent, WrongLeg, AtLegStart, NoLink, Unchanged
    }

    /// <summary>F2e: the outcome of <see cref="TrafficKernel.HoldVehicle"/>. Default means refused (nothing changed). The
    /// three motion fields are the exact saved doubles after the hold; the charge is the change of the vehicle's earliest
    /// discharge, which is all that counts against a journey's allowance.</summary>
    public readonly struct TrafficHoldResult
    {
        internal TrafficHoldResult(bool applied, double charge, double motionStartFraction, double motionStartedAt, double readyAt)
        { Applied = applied; Charge = charge; MotionStartFraction = motionStartFraction; MotionStartedAt = motionStartedAt; ReadyAt = readyAt; }
        public bool Applied { get; }
        public double Charge { get; }
        public double MotionStartFraction { get; }
        public double MotionStartedAt { get; }
        public double ReadyAt { get; }
    }

    /// <summary>One applied hold as it appears in the stream: its sequence, the simulator minute it was applied at, the
    /// reported input, the charge against the allowance and, for a car, the exact mutation (NaN for a walk).</summary>
    public readonly struct PlayerHoldRecord
    {
        public PlayerHoldRecord(long sequence, double minute, string personId, PlayerHoldCause cause, double input, double charge,
            double motionStartFraction = double.NaN, double motionStartedAt = double.NaN, double readyAt = double.NaN)
        {
            Sequence = sequence; Minute = minute; PersonId = personId; Cause = cause; Input = input; Charge = charge;
            MotionStartFraction = motionStartFraction; MotionStartedAt = motionStartedAt; ReadyAt = readyAt;
        }
        public long Sequence { get; }
        public double Minute { get; }
        public string PersonId { get; }
        public PlayerHoldCause Cause { get; }
        public double Input { get; }
        public double Charge { get; }
        public double MotionStartFraction { get; }
        public double MotionStartedAt { get; }
        public double ReadyAt { get; }
        public bool Vehicle => !double.IsNaN(MotionStartFraction);
    }

    public readonly struct PlayerHoldResult
    {
        internal PlayerHoldResult(PlayerHoldRefusal refusal, PlayerHoldRecord record = default) { Refusal = refusal; Record = record; }
        public PlayerHoldRefusal Refusal { get; }
        public bool Applied => Refusal == PlayerHoldRefusal.None;
        /// <summary>The logged record of an applied hold; default when refused.</summary>
        public PlayerHoldRecord Record { get; }
    }

    /// <summary>
    /// F2e player holds (docs/plans/f2-movement.md Decision 4): limits, the Player.log stream line and replay segment rules.
    /// A stream line is <c>[Persistent City] hold seq=… minute=… person=… cause=… input=… charge=…</c>, plus
    /// <c>motionStart=… motionAt=… readyAt=…</c> for a car. Doubles are written with "G17", which round-trips on every
    /// runtime, so replay compares them bit for bit.
    /// </summary>
    public static class PlayerHolds
    {
        /// <summary>Most a person can be held per journey in total (plan Q2 default, accepted 6 November 2026).</summary>
        public const double MaximumMinutesPerJourney = 1;
        /// <summary>Largest single report; Runtime re-reports while the contact persists.</summary>
        public const double MaximumReportMinutes = 0.25;
        public const string LinePrefix = "[Persistent City] hold seq=";
        /// <summary>Replayed holds are logged with this prefix, which the stream parser never reads as an original.</summary>
        public const string ReplayLinePrefix = "[Persistent City] hold replay seq=";

        public static string CauseName(PlayerHoldCause cause) => cause == PlayerHoldCause.PlayerVehicle ? "player-vehicle" : "player-foot";
        public static bool TryCause(string text, out PlayerHoldCause cause)
        {
            cause = text == "player-vehicle" ? PlayerHoldCause.PlayerVehicle : PlayerHoldCause.PlayerFoot;
            return text == "player-vehicle" || text == "player-foot";
        }

        private static string D(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

        public static string Format(PlayerHoldRecord record, bool replay = false)
        {
            var text = new StringBuilder(160).Append(replay ? ReplayLinePrefix : LinePrefix)
                .Append(record.Sequence.ToString(CultureInfo.InvariantCulture))
                .Append(" minute=").Append(D(record.Minute))
                .Append(" person=").Append(Uri.EscapeDataString(record.PersonId))
                .Append(" cause=").Append(CauseName(record.Cause))
                .Append(" input=").Append(D(record.Input))
                .Append(" charge=").Append(D(record.Charge));
            if (record.Vehicle)
                text.Append(" motionStart=").Append(D(record.MotionStartFraction))
                    .Append(" motionAt=").Append(D(record.MotionStartedAt))
                    .Append(" readyAt=").Append(D(record.ReadyAt));
            return text.ToString();
        }

        /// <summary>Reads one original stream line (anything before the prefix, such as a log timestamp, is ignored).
        /// Keys must appear exactly in the written order.</summary>
        public static bool TryParse(string? line, out PlayerHoldRecord record)
        {
            record = default;
            if (line == null) return false;
            int at = line.IndexOf(LinePrefix, StringComparison.Ordinal);
            if (at < 0) return false;
            string[] parts = line.Substring(at + LinePrefix.Length).TrimEnd('\r', '\n', ' ').Split(' ');
            if (parts.Length != 6 && parts.Length != 9) return false;
            if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long sequence) || sequence < 1) return false;
            string[] keys = { "", "minute=", "person=", "cause=", "input=", "charge=", "motionStart=", "motionAt=", "readyAt=" };
            for (int i = 1; i < parts.Length; i++) if (!parts[i].StartsWith(keys[i], StringComparison.Ordinal)) return false;
            string Value(int i) => parts[i].Substring(keys[i].Length);
            bool Number(int i, out double value) => double.TryParse(Value(i), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Position.Finite(value);
            if (!Number(1, out double minute) || !TryCause(Value(3), out var cause) || !Number(4, out double input) || !Number(5, out double charge)) return false;
            string person;
            try { person = Uri.UnescapeDataString(Value(2)); } catch (UriFormatException) { return false; }
            if (person.Length == 0) return false;
            if (parts.Length == 6) { record = new PlayerHoldRecord(sequence, minute, person, cause, input, charge); return true; }
            if (!Number(6, out double start) || !Number(7, out double startedAt) || !Number(8, out double ready)) return false;
            record = new PlayerHoldRecord(sequence, minute, person, cause, input, charge, start, startedAt, ready);
            return true;
        }

        /// <summary>
        /// A replay segment is the contiguous stream from the starting checkpoint's saved <see cref="CityState.PlayerHoldSequence"/>
        /// to the end checkpoint's: the first record is start + 1, each next one is one more, minutes never go back, and the last
        /// one is <paramref name="end"/> (when given). A missing, duplicated or out-of-order sequence number, or a wrong start or
        /// end, refuses the whole segment.
        /// </summary>
        public static bool ValidateSegment(IReadOnlyList<PlayerHoldRecord> records, long start, long? end, out string error)
        {
            error = "";
            if (records == null) { error = "no hold stream"; return false; }
            long expected = start + 1;
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Sequence != expected)
                {
                    bool later = false;
                    for (int j = i + 1; j < records.Count && !later; j++) later = records[j].Sequence == expected;
                    error = i == 0 && record.Sequence != expected && !later
                        ? "the stream does not start at the checkpoint sequence " + start + " (first seq=" + record.Sequence + ")"
                        : i > 0 && record.Sequence == records[i - 1].Sequence ? "duplicate hold seq=" + record.Sequence
                        : later || record.Sequence < expected ? "hold seq=" + record.Sequence + " is out of order (expected " + expected + ")"
                        : "gap in the hold stream: seq=" + expected + " is missing";
                    return false;
                }
                if (i > 0 && record.Minute < records[i - 1].Minute) { error = "hold seq=" + record.Sequence + " goes back in time"; return false; }
                expected++;
            }
            if (end.HasValue && expected - 1 != end.Value)
            {
                error = "the stream does not end at the checkpoint sequence " + end.Value + " (last seq=" + (expected - 1) + ")";
                return false;
            }
            return true;
        }

        internal static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b)
            || double.IsNaN(a) && double.IsNaN(b);
    }

    /// <summary>F2e: the last 256 applied holds for <c>pc movement holds since &lt;seq&gt;</c>. Runtime memory only; the
    /// complete stream is Player.log.</summary>
    public sealed class PlayerHoldRing
    {
        public const int Capacity = 256;
        private readonly PlayerHoldRecord[] ring = new PlayerHoldRecord[Capacity];
        private int count, head;
        public int Count => count;
        public long LastSequence => count == 0 ? 0 : ring[(head + count - 1) % Capacity].Sequence;
        public long OldestSequence => count == 0 ? 0 : ring[head].Sequence;

        /// <summary>A new city (load) can start at any saved sequence; a sequence that does not continue the ring clears it.</summary>
        public void Add(PlayerHoldRecord record)
        {
            if (count != 0 && record.Sequence != LastSequence + 1) { count = 0; head = 0; }
            if (count == Capacity) { head = (head + 1) % Capacity; count--; }
            ring[(head + count) % Capacity] = record;
            count++;
        }

        /// <summary>Entries after <paramref name="sequence"/>; gap when a later entry was evicted (or never kept).</summary>
        public void Since(long sequence, List<PlayerHoldRecord> into, out bool gap)
        {
            gap = count != 0 && sequence + 1 < OldestSequence;
            for (int i = 0; i < count; i++)
            {
                var record = ring[(head + i) % Capacity];
                if (record.Sequence > sequence) into.Add(record);
            }
        }
    }

    /// <summary>
    /// F2e E2 replay of one validated segment into a city loaded from the segment's starting checkpoint. The caller limits
    /// each advance to <see cref="Limit"/> so the simulator stops exactly on each stamped minute, then calls
    /// <see cref="ApplyDue"/> once it has drained that minute. Each hold goes through the same
    /// <see cref="CitySimulator.HoldForPlayer"/> with the recorded input; the result must reproduce the recorded sequence,
    /// charge and mutation bit for bit, or the replay stops with <see cref="Failure"/>.
    /// </summary>
    public sealed class PlayerHoldReplay
    {
        private readonly List<PlayerHoldRecord> records;
        private int next;
        public PlayerHoldReplay(IReadOnlyList<PlayerHoldRecord> segment, long start, long? end)
        {
            if (!PlayerHolds.ValidateSegment(segment, start, end, out string error)) throw new ArgumentException("Hold segment refused: " + error);
            records = new List<PlayerHoldRecord>(segment);
        }
        public int Count => records.Count;
        public int Applied => next;
        public PlayerHoldRecord this[int index] => records[index];
        public string Failure { get; private set; } = "";
        public bool Done => next >= records.Count && Failure.Length == 0;
        public double NextMinute => Failure.Length != 0 || next >= records.Count ? double.PositiveInfinity : records[next].Minute;
        /// <summary>The advance target for this frame: never past the next stamped minute.</summary>
        public double Limit(double target) => Math.Min(target, NextMinute);

        /// <summary>Apply every record stamped at the simulator's minute, once it has drained that minute. False on failure.</summary>
        public bool ApplyDue(CitySimulator city)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            while (Failure.Length == 0 && next < records.Count && records[next].Minute <= city.State.Minute)
            {
                var record = records[next];
                if (record.Minute != city.State.Minute) { Failure = "the city passed hold seq=" + record.Sequence + " at minute " + record.Minute.ToString("G17", CultureInfo.InvariantCulture); break; }
                if (!city.IsCaughtUpTo(record.Minute)) return true; // wait until the stamped minute has drained
                if (city.State.PlayerHoldSequence != record.Sequence - 1)
                { Failure = "the city's hold sequence " + city.State.PlayerHoldSequence + " does not precede seq=" + record.Sequence; break; }
                var result = city.HoldForPlayer(record.PersonId, record.Input, record.Cause);
                var replayed = result.Record;
                if (!result.Applied) { Failure = "hold seq=" + record.Sequence + " was refused on replay (" + result.Refusal + ")"; break; }
                if (replayed.Sequence != record.Sequence || replayed.Cause != record.Cause || replayed.PersonId != record.PersonId
                    || !PlayerHolds.SameBits(replayed.Charge, record.Charge) || !PlayerHolds.SameBits(replayed.MotionStartFraction, record.MotionStartFraction)
                    || !PlayerHolds.SameBits(replayed.MotionStartedAt, record.MotionStartedAt) || !PlayerHolds.SameBits(replayed.ReadyAt, record.ReadyAt))
                { Failure = "hold seq=" + record.Sequence + " did not reproduce: recorded " + PlayerHolds.Format(record) + ", replayed " + PlayerHolds.Format(replayed); break; }
                next++;
            }
            return Failure.Length == 0;
        }
    }

    public sealed partial class CitySimulator
    {
        /// <summary>
        /// F2e: the visible world's only write into Core (plan Decision 4). The player blocked this person for the last
        /// <paramref name="blockedMinutes"/> (in (0, 0.25]; Runtime reports at most the time since its previous report for this
        /// person). The hold is applied here, at once, and nothing about it is kept afterwards except the person's consumed
        /// allowance and the city's hold sequence, both saved.
        /// A walk leg moves DepartureMinute and NextEventMinute later and reschedules the due event, never below the current
        /// leg's start (a traffic handoff already made lies at or before it). A traffic leg rebases the kernel vehicle
        /// (<see cref="TrafficKernel.HoldVehicle"/>). The person's remaining allowance (1 game minute per journey) clamps the
        /// input; only the charge counts against it. Applied only when the simulator has drained its minute, so a replay that
        /// stops on the same minute applies it to the same state.
        /// </summary>
        public PlayerHoldResult HoldForPlayer(string citizenId, double blockedMinutes, PlayerHoldCause cause)
        {
            if (!Position.Finite(blockedMinutes) || !(blockedMinutes > 0) || blockedMinutes > PlayerHolds.MaximumReportMinutes
                || (uint)cause > (uint)PlayerHoldCause.PlayerVehicle) return new PlayerHoldResult(PlayerHoldRefusal.InvalidInput);
            if (citizenId == null || !citizenEvents.TryGetValue(citizenId, out var item)) return new PlayerHoldResult(PlayerHoldRefusal.UnknownPerson);
            var person = item.Citizen;
            if (person.IsVisitor) return new PlayerHoldResult(PlayerHoldRefusal.Visitor);
            if (person.Phase != CitizenPhase.Travelling || person.Journey == null) return new PlayerHoldResult(PlayerHoldRefusal.NotTravelling);
            if (pending != null || !IsCaughtUpTo(State.Minute) || traffic != null && traffic.Clock != State.Minute)
                return new PlayerHoldResult(PlayerHoldRefusal.NotDrained);
            double remaining = PlayerHolds.MaximumMinutesPerJourney - person.PlayerHoldMinutes;
            if (!(remaining > 0)) return new PlayerHoldResult(PlayerHoldRefusal.AllowanceSpent);
            double input = Math.Min(blockedMinutes, remaining);
            double charge;
            PlayerHoldRecord record;
            if (traffic != null && trafficCitizens.ContainsKey(citizenId))
            {
                if (cause != PlayerHoldCause.PlayerVehicle) return new PlayerHoldResult(PlayerHoldRefusal.WrongLeg);
                if (!traffic.GetPermission(citizenId).LinkId.HasValue) return new PlayerHoldResult(PlayerHoldRefusal.NoLink);
                var held = traffic.HoldVehicle(citizenId, input);
                if (!held.Applied) return new PlayerHoldResult(PlayerHoldRefusal.Unchanged);
                charge = held.Charge;
                // The published journey follows the kernel's rebased coordinate, as SynchronizeSnapshot would.
                double progress = traffic.GetPermission(citizenId).NominalMinutes;
                person.DepartureMinute = State.Minute - progress;
                person.NextEventMinute = person.DepartureMinute + person.Journey.Minutes;
                record = new PlayerHoldRecord(State.PlayerHoldSequence + 1, State.Minute, citizenId, cause, blockedMinutes, charge,
                    held.MotionStartFraction, held.MotionStartedAt, held.ReadyAt);
            }
            else
            {
                if (cause != PlayerHoldCause.PlayerFoot) return new PlayerHoldResult(PlayerHoldRefusal.WrongLeg);
                var route = person.Journey;
                double elapsed = NormalizeJourneyElapsed(State.Minute - person.DepartureMinute, route.Minutes, State.Minute);
                if (!(elapsed >= 0 && elapsed <= route.Minutes)) return new PlayerHoldResult(PlayerHoldRefusal.WrongLeg);
                var sample = SampleRoute(route, elapsed, person.Car?.ParkedPosition);
                if (sample.Mode != TravelMode.Walk || sample.Stationary) return new PlayerHoldResult(PlayerHoldRefusal.WrongLeg);
                double legStart = 0;
                for (int i = 0; i < sample.LegIndex; i++) legStart += route.Legs[i].Minutes;
                double applied = Math.Min(input, elapsed - legStart);
                if (!(applied > 0)) return new PlayerHoldResult(PlayerHoldRefusal.AtLegStart);
                double departure = person.DepartureMinute + applied;
                if (State.Minute - departure < legStart) departure = State.Minute - legStart;
                charge = departure - person.DepartureMinute;
                if (!(charge > 0)) return new PlayerHoldResult(PlayerHoldRefusal.AtLegStart);
                if (!due.Remove(item)) throw new InvalidOperationException("A travelling walker has no queued event to hold.");
                person.DepartureMinute = departure;
                person.NextEventMinute += charge;
                item.Minute = ScheduledMinute(person);
                if (!due.Add(item)) throw new InvalidOperationException("A player hold duplicated a resident event.");
                record = new PlayerHoldRecord(State.PlayerHoldSequence + 1, State.Minute, citizenId, cause, blockedMinutes, charge);
            }
            person.PlayerHoldMinutes = Math.Min(PlayerHolds.MaximumMinutesPerJourney, person.PlayerHoldMinutes + charge);
            State.PlayerHoldSequence = record.Sequence;
            return new PlayerHoldResult(PlayerHoldRefusal.None, record);
        }
    }
}
