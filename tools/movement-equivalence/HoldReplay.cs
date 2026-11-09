using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.MovementEquivalence;

/// <summary>F2e replay checkpoint, kept where the native clock enters an integral minute (every earlier event drained):
/// the saved city, the native checkout double's state, the native clock and the saved hold sequence.</summary>
public sealed class HoldCheckpoint
{
    public HoldCheckpoint(double minute, string payload, MovementMarket market, int hour, float minuteOfHour, long sequence, int framesPerSecond)
    {
        Minute = minute; Payload = payload; Market = market; Hour = hour; MinuteOfHour = minuteOfHour; Sequence = sequence; FramesPerSecond = framesPerSecond;
    }
    public double Minute { get; }
    public string Payload { get; }
    public MovementMarket Market { get; }
    public int Hour { get; }
    public float MinuteOfHour { get; }
    public long Sequence { get; }
    public int FramesPerSecond { get; }
}

/// <summary>
/// F2e player double for the E2 arm (docs/plans/f2-movement.md Decision 4). It stands in front of every third followed
/// body (by ordinal hash of the person) inside the contact window, so those bodies count as blocked, and reports their
/// blocked game minutes as Runtime PlayerContact does: every <see cref="ReportMinutes"/>, each report at most 0.25 and at
/// most the time since that person's previous report, a refusal for an undrained minute kept for the next frame. Every
/// applied hold is written as its Player.log line and parsed back, so the replay reads exactly the logged stream.
/// </summary>
public sealed class PlayerDouble
{
    public const double ContactFrom = 7 * 60, ContactTo = 10 * 60;
    public const double ReportMinutes = 0.05;
    private sealed class Track { public double Blocked, LastMinute = double.NaN, LastReport = double.NegativeInfinity; public Route? Journey; public bool Spent; }
    private readonly Dictionary<string, Track> tracks = new(StringComparer.Ordinal);
    private readonly List<(CitizenState Citizen, bool Car)> followed = new();
    public long Reports, WalkHolds, CarHolds, Absorbed, ChargedMinutesTimes1000;
    public readonly SortedDictionary<string, long> Refusals = new(StringComparer.Ordinal);
    public readonly List<string> Lines = new();

    private static bool Blocks(string id)
    {
        uint value = 2166136261u;
        foreach (char c in id) { value ^= c; value *= 16777619u; }
        return value % 3 == 0;
    }

    public void Update(CitySimulator city, PresentationDriver driver, List<PlayerHoldRecord> stream)
    {
        double minute = city.State.Minute, day = minute % 1440;
        if (day < ContactFrom || day >= ContactTo) { tracks.Clear(); return; }
        followed.Clear();
        driver.Followed(followed);
        foreach (var (citizen, car) in followed)
        {
            string id = citizen.Identity.Id;
            if (!Blocks(id)) continue;
            if (!tracks.TryGetValue(id, out var track) || !ReferenceEquals(track.Journey, citizen.Journey))
                tracks[id] = track = new Track { Journey = citizen.Journey, LastMinute = minute };
            if (minute > track.LastMinute) track.Blocked += minute - track.LastMinute;
            track.LastMinute = minute;
            if (track.Spent || track.Blocked < ReportMinutes) continue;
            double report = Math.Min(track.Blocked, Math.Min(PlayerHolds.MaximumReportMinutes, minute - track.LastReport));
            if (!(report > 0)) continue;
            Reports++;
            var result = city.HoldForPlayer(id, report, car ? PlayerHoldCause.PlayerVehicle : PlayerHoldCause.PlayerFoot);
            if (result.Refusal == PlayerHoldRefusal.NotDrained) continue;
            track.Blocked = 0; track.LastReport = minute;
            track.Spent |= result.Refusal == PlayerHoldRefusal.AllowanceSpent;
            if (!result.Applied)
            {
                Refusals.TryGetValue(result.Refusal.ToString(), out long n); Refusals[result.Refusal.ToString()] = n + 1;
                continue;
            }
            string line = PlayerHolds.Format(result.Record);
            Lines.Add(line);
            if (!PlayerHolds.TryParse(line, out var parsed)) throw new HarnessViolation("unreadable hold line " + line);
            stream.Add(parsed);
            if (result.Record.Vehicle) CarHolds++; else WalkHolds++;
            if (result.Record.Charge < result.Record.Input) Absorbed++;
            ChargedMinutesTimes1000 += (long)Math.Round(result.Record.Charge * 1000);
        }
    }
}

/// <summary>
/// F2e E2 replay (plan section 3, E2): load a starting checkpoint with bodies off and feed it the stream segment up to an
/// end checkpoint through <see cref="PlayerHoldReplay"/>, with the same frame loop as <see cref="MovementRun"/> (native
/// clock under the F1 fence, budgeted slices). Each advance stops on the next stamped minute. The saved bytes where the
/// native clock enters the end checkpoint's minute must equal that checkpoint's.
/// </summary>
public static class HoldReplay
{
    public sealed class Outcome
    {
        public bool Equal;
        public string Failure = "";
        public int Applied;
        public string Payload = "";
    }

    public static IReadOnlyList<PlayerHoldRecord> Segment(IReadOnlyList<PlayerHoldRecord> stream, HoldCheckpoint from, HoldCheckpoint to)
        => stream.Where(h => h.Sequence > from.Sequence && h.Sequence <= to.Sequence).ToList();

    /// <summary>Replays <paramref name="segment"/> from <paramref name="from"/> to <paramref name="to"/>. A segment the
    /// validation refuses throws <see cref="ArgumentException"/> before anything runs.</summary>
    public static Outcome Replay(HoldCheckpoint from, HoldCheckpoint to, IReadOnlyList<PlayerHoldRecord> segment)
    {
        var replay = new PlayerHoldReplay(segment, from.Sequence, to.Sequence);
        var read = SaveCodec.Read(from.Payload);
        if (read.Status != SaveReadStatus.Loaded) throw new HarnessViolation("checkpoint refused: " + read.Error);
        var market = from.Market.Copy();
        var city = MovementFixture.Attach(read.State!, market, new GridPlanner(), fresh: false);
        int hour = from.Hour; float minute = from.MinuteOfHour;
        double native = hour * 60.0 + minute;
        market.Clock = native;
        if (!city.BeginAccountingObservations(native)) throw new HarnessViolation("replay epoch not published");
        double frameSeconds = 1.0 / from.FramesPerSecond;
        var outcome = new Outcome();
        bool first = true;
        for (long frames = 0; ; frames++)
        {
            if (frames > 50_000_000) throw new HarnessViolation("nonterminating replay");
            if (!first)
            {
                double target0 = AccountingClock.Target(native);
                bool ready = city.IsCaughtUpTo(target0);
                float requested = (float)(frameSeconds * MovementRun.GameMinutesPerRealSecond);
                minute += AccountingClock.Limit(ready, native, requested);
                while (minute >= 60) { minute -= 60; hour++; }
                double previous = native;
                native = market.Clock = hour * 60.0 + minute;
                if (!city.BeginAccountingObservations(native)) throw new HarnessViolation("replay observation epoch not published");
                if (native >= to.Minute && previous < to.Minute)
                {
                    outcome.Applied = replay.Applied;
                    outcome.Failure = replay.Failure.Length != 0 ? replay.Failure
                        : replay.Applied != replay.Count ? "the replay ended with " + (replay.Count - replay.Applied) + " holds unapplied" : "";
                    outcome.Payload = SaveCodec.Write(city.State);
                    outcome.Equal = outcome.Failure.Length == 0 && outcome.Payload == to.Payload;
                    return outcome;
                }
            }
            first = false;
            double target = Math.Max(city.State.Minute, AccountingClock.Target(native));
            // The live frame's slice budget; a stop on a stamped minute costs one extra caught-up call, which is refunded.
            int budget = MovementRun.EventUnitsPerFrame;
            for (int unit = 0; unit < budget; unit++)
            {
                double limit = Math.Max(city.State.Minute, replay.Limit(target));
                var advanced = city.AdvanceFrameSlice(limit, 1);
                if (!advanced.CaughtUp) continue;
                if (limit < target) budget++;
                if (!replay.ApplyDue(city))
                {
                    outcome.Failure = replay.Failure; outcome.Applied = replay.Applied;
                    return outcome;
                }
                if (limit >= target) break;
            }
            city.SynchronizeSnapshot();
        }
    }

    /// <summary>The E2 arm: one contact run with checkpoints, then replays from each checkpoint to the last, plus the
    /// refusal cases. Returns (check, pass, detail) rows like <see cref="SelfChecks.Run"/>.</summary>
    public static List<(string Check, bool Pass, string Detail)> Check(int residents, int days = 1)
    {
        var rows = new List<(string, bool, string)>();
        double start = MovementFixture.StartMinute;
        var options = new MovementOptions { Arm = "contact/e2", Residents = residents, Days = days, PlayerContact = true,
            Bodies = new DriverSettings { Pedestrians = 48, Vehicles = 24, Lag = LagModel.Native },
            Checkpoints = new[] { start + 1, start + 8 * 60, start + 9 * 60, start + 12 * 60 } };
        var live = MovementRun.Run(options);
        var player = live.Player!;
        rows.Add(("e2 contact arm applied walk and car holds", player.WalkHolds > 0 && player.CarHolds > 0,
            "walk " + player.WalkHolds + ", car " + player.CarHolds + ", absorbed " + player.Absorbed + ", refused " + string.Join(",", player.Refusals.Select(r => r.Key + "=" + r.Value))));
        var end = live.Checkpoints[live.Checkpoints.Count - 1];
        foreach (var from in live.Checkpoints.Take(live.Checkpoints.Count - 1))
        {
            var outcome = Replay(from, end, Segment(live.Holds, from, end));
            rows.Add(("e2 replay from " + from.Minute + " equals the contact run at " + end.Minute, outcome.Equal,
                "holds " + outcome.Applied + (outcome.Failure.Length > 0 ? ", " + outcome.Failure : "")));
        }
        var segment = Segment(live.Holds, live.Checkpoints[0], end).ToList();
        bool Refused(List<PlayerHoldRecord> edited, long from, long to)
        {
            try { _ = new PlayerHoldReplay(edited, from, to); return false; }
            catch (ArgumentException) { return true; }
        }
        long s0 = live.Checkpoints[0].Sequence, s1 = end.Sequence;
        if (segment.Count >= 3)
        {
            rows.Add(("e2 replay refuses a gap", Refused(segment.Where((_, i) => i != segment.Count / 2).ToList(), s0, s1), ""));
            var duplicate = new List<PlayerHoldRecord>(segment); duplicate.Insert(1, segment[1]);
            rows.Add(("e2 replay refuses a duplicate", Refused(duplicate, s0, s1), ""));
            var swapped = new List<PlayerHoldRecord>(segment); (swapped[1], swapped[2]) = (swapped[2], swapped[1]);
            rows.Add(("e2 replay refuses a reordering", Refused(swapped, s0, s1), ""));
            rows.Add(("e2 replay refuses a wrong start", Refused(segment, s0 + 1, s1), ""));
            rows.Add(("e2 replay refuses a wrong end", Refused(segment, s0, s1 + 1), ""));
        }
        else rows.Add(("e2 segment long enough for refusal cases", false, "holds " + segment.Count));
        return rows;
    }
}
