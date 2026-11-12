using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public enum PlayerHoldCause { PlayerFoot, PlayerVehicle }

    public enum PlayerHoldRefusal
    {
        None, InvalidInput, UnknownPerson, Visitor, NotTravelling, NotDrained, AllowanceSpent, WrongLeg, AtLegStart, NoLink, Unchanged
    }

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
        public PlayerHoldRecord Record { get; }
    }

    public static class PlayerHolds
    {
        public const double MaximumMinutesPerJourney = 1;
        public const double MaximumReportMinutes = 0.25;
        public const string LinePrefix = "[Persistent City] hold seq=";
        public const string ReplayLinePrefix = "[Persistent City] hold replay seq=";
        public const string ReadbackLinePrefix = "[Persistent City] hold readback seq=";

        public static string CauseName(PlayerHoldCause cause) => cause == PlayerHoldCause.PlayerVehicle ? "player-vehicle" : "player-foot";
        public static bool TryCause(string text, out PlayerHoldCause cause)
        {
            cause = text == "player-vehicle" ? PlayerHoldCause.PlayerVehicle : PlayerHoldCause.PlayerFoot;
            return text == "player-vehicle" || text == "player-foot";
        }

        private static string D(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

        public static string Format(PlayerHoldRecord record, bool replay = false) => Format(record, replay ? ReplayLinePrefix : LinePrefix);

        public static string Format(PlayerHoldRecord record, string prefix)
        {
            if (prefix != LinePrefix && prefix != ReplayLinePrefix && prefix != ReadbackLinePrefix) throw new ArgumentException("Unknown hold line prefix.", nameof(prefix));
            var text = new StringBuilder(160).Append(prefix)
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
                    error = i == 0 && (record.Sequence < expected || !later)
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

        public const string SegmentLinePrefix = "segment start=";

        public static bool ReadSegment(IEnumerable<string> lines, out List<PlayerHoldRecord> records, out long start, out long end, out string error)
        {
            records = new List<PlayerHoldRecord>(); start = end = -1; error = "";
            bool bounded = false;
            foreach (string line in lines)
            {
                if (TryParse(line, out var record)) { records.Add(record); continue; }
                int at = line.IndexOf(SegmentLinePrefix, StringComparison.Ordinal);
                if (at < 0) continue;
                string[] parts = line.Substring(at + SegmentLinePrefix.Length).Trim().Split(' ');
                if (bounded || parts.Length != 2 || !parts[1].StartsWith("end=", StringComparison.Ordinal)
                    || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out start)
                    || !long.TryParse(parts[1].Substring("end=".Length), NumberStyles.None, CultureInfo.InvariantCulture, out end) || end < start)
                { error = bounded ? "more than one segment line" : "unreadable segment line"; return false; }
                bounded = true;
            }
            if (!bounded) { error = "no segment line: give the checkpoints' sequences as 'segment start=<seq> end=<seq>'"; return false; }
            return ValidateSegment(records, start, end, out error);
        }

        internal static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b)
            || double.IsNaN(a) && double.IsNaN(b);
    }

    public sealed class PlayerHoldRing
    {
        public const int Capacity = 256;
        private readonly PlayerHoldRecord[] ring = new PlayerHoldRecord[Capacity];
        private int count, head;
        private object? owner;

        public void Bind(object city)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (ReferenceEquals(owner, city)) return;
            owner = city; count = 0; head = 0;
        }
        public int Count => count;
        public long LastSequence => count == 0 ? 0 : ring[(head + count - 1) % Capacity].Sequence;
        public long OldestSequence => count == 0 ? 0 : ring[head].Sequence;

        public void Add(PlayerHoldRecord record)
        {
            if (count != 0 && record.Sequence != LastSequence + 1) { count = 0; head = 0; }
            if (count == Capacity) { head = (head + 1) % Capacity; count--; }
            ring[(head + count) % Capacity] = record;
            count++;
        }

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
        public double Limit(double target) => Math.Min(target, NextMinute);

        public bool ApplyDue(CitySimulator city)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            while (Failure.Length == 0 && next < records.Count && records[next].Minute <= city.State.Minute)
            {
                var record = records[next];
                if (record.Minute != city.State.Minute) { Failure = "the city passed hold seq=" + record.Sequence + " at minute " + record.Minute.ToString("G17", CultureInfo.InvariantCulture); break; }
                if (!city.IsCaughtUpTo(record.Minute)) return true;
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
                var held = traffic.HoldVehicle(citizenId, input, remaining);
                if (!held.Applied) return new PlayerHoldResult(PlayerHoldRefusal.Unchanged);
                charge = held.Charge;
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
                if (!due.Remove(item)) return new PlayerHoldResult(PlayerHoldRefusal.NotDrained);
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
