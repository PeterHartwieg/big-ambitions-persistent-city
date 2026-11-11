using System;
using System.Collections.Generic;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>Where the player is: the avatar on foot, or the vehicle the player drives.</summary>
    internal readonly struct PlayerPose
    {
        internal PlayerPose(Position position, bool driving) { Position = position; Driving = driving; Valid = position.IsFinite; }
        internal Position Position { get; }
        internal bool Driving { get; }
        internal bool Valid { get; }
    }

    /// <summary>
    /// F2e contact detection (docs/plans/f2-movement.md Decision 4). A contact is a following body that is visibly blocked
    /// (since the previous frame the logical pose moved on while the body's own progress stood still or its lag grew) while the player's
    /// avatar is within 1 m ahead of the walker, or the player's vehicle is within 4 m ahead of the car on the same road
    /// segment. Only the player counts: the pose comes from GameManager's own player controller or selected vehicle, so
    /// native bodies, RLO actors and service vehicles can never cause a hold. Blocked game minutes accumulate per person
    /// and are reported every <see cref="ReportMinutes"/> (and once more when the contact ends), each report at most
    /// <see cref="PlayerHolds.MaximumReportMinutes"/> and at most the time since that person's previous report. Core
    /// applies the hold at once (CitySimulator.HoldForPlayer); this class keeps only presentation memory.
    /// </summary>
    internal sealed class PlayerContact
    {
        internal const double WalkerMetres = 1;
        internal const double CarMetres = 4;
        /// <summary>Half width of the car's road segment within which the player's vehicle counts as on it.</summary>
        internal const double CarLaneMetres = 2.5;
        internal const double ReportMinutes = 0.05;
        // Progress below this (route minutes) is standing still.
        private const double StillMinutes = 1e-9;

        /// <summary>The last 256 applied holds, for pc movement holds since. Runtime memory only; Player.log keeps the
        /// complete stream.</summary>
        internal static readonly PlayerHoldRing Ring = new PlayerHoldRing();
        internal static long Contacts, Reports, Applied, Refused;

        private sealed class Track
        {
            public Route Journey = null!;
            public bool Car;
            public double Logical, Body, Minute, Blocked;
            public double LastReport = double.NegativeInfinity;
            public long Frame;
            public bool Ended, Spent;
        }
        private readonly Dictionary<string, Track> tracks = new Dictionary<string, Track>(StringComparer.Ordinal);

        /// <summary>The player's pose this frame, or false (no player controller, inside a building view).</summary>
        internal static bool TryReadPlayer(out PlayerPose pose)
        {
            pose = default;
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null) return false;
            if (GameManager.IsDrivingVehicle())
            {
                var vehicle = game.selectedVehicle;
                if (vehicle == null) return false;
                pose = new PlayerPose(Point(vehicle.transform.position), true);
            }
            else
            {
                var player = game.playerController;
                if (player == null) return false;
                pose = new PlayerPose(Point(player.transform.position), false);
            }
            return pose.Valid;
        }
        private static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);

        /// <summary>
        /// One following body this frame. <paramref name="logical"/> and <paramref name="body"/> are route minutes of the
        /// logical pose and of the body; <paramref name="from"/>/<paramref name="to"/> are the body's own leg. Returns the
        /// minutes to report now (0 for none); the caller passes Core's answer to <see cref="Settle"/>.
        /// </summary>
        internal double Observe(string id, Route journey, bool car, double logical, double body, Position at, Position from, Position to,
            in PlayerPose player, double minute, long frame)
        {
            bool contact = player.Valid && player.Driving == car && Ahead(at, from, to, player.Position, car);
            // A track continues only from the previous frame on the same journey; a released and re-acquired body starts over.
            if (!tracks.TryGetValue(id, out var track) || !ReferenceEquals(track.Journey, journey) || track.Car != car || track.Frame != frame - 1)
            {
                if (!contact) { tracks.Remove(id); return 0; }
                if (tracks.Count >= 64) Prune(frame);
                tracks[id] = new Track { Journey = journey, Car = car, Logical = logical, Body = body, Minute = minute, Frame = frame };
                return 0;
            }
            track.Frame = frame;
            // The logical pose moved on while the body stood still or fell further behind. A car the kernel itself holds in
            // its queue does not move logically, so it is never held for the player's car sitting in the same queue.
            bool blocked = logical - track.Logical > StillMinutes
                && (body - track.Body <= StillMinutes || logical - body > track.Logical - track.Body + StillMinutes);
            if (contact && blocked && minute > track.Minute) { track.Blocked += minute - track.Minute; Contacts++; }
            track.Logical = logical; track.Body = body; track.Minute = minute;
            track.Ended = !contact;
            if (track.Blocked <= 0 || track.Spent) { if (track.Ended) tracks.Remove(id); return 0; }
            if (!track.Ended && track.Blocked < ReportMinutes) return 0;
            double report = Math.Min(track.Blocked, Math.Min(PlayerHolds.MaximumReportMinutes, minute - track.LastReport));
            if (!(report > 0)) return 0;
            Reports++;
            return report;
        }

        /// <summary>Core's answer to a report. A hold refused only because the simulator had not drained its minute keeps the
        /// blocked time for the next frame; any other answer ends this report.</summary>
        internal void Settle(string id, PlayerHoldResult result, CityState city)
        {
            if (!tracks.TryGetValue(id, out var track)) return;
            if (result.Refusal == PlayerHoldRefusal.NotDrained) return;
            if (result.Applied) { Applied++; Publish(result.Record, city); } else Refused++;
            // The journey's allowance is used up: no further report for it (the walker steps around, the car waits).
            track.Spent |= result.Refusal == PlayerHoldRefusal.AllowanceSpent;
            track.Blocked = 0; track.LastReport = track.Minute;
            if (track.Ended) tracks.Remove(id);
        }

        private readonly List<string> stale = new List<string>();
        private void Prune(long frame)
        {
            foreach (var pair in tracks) if (pair.Value.Frame < frame - 1) stale.Add(pair.Key);
            foreach (string id in stale) tracks.Remove(id);
            stale.Clear();
        }
        internal int Tracked => tracks.Count;
        internal bool Tracking(string id) => tracks.ContainsKey(id);

        /// <summary>Cheap pre-check: can the player be in contact with a body here at all (right kind, within reach)?</summary>
        internal static bool InReach(Position at, in PlayerPose player, bool car)
        {
            if (!player.Valid || player.Driving != car) return false;
            double dx = player.Position.X - at.X, dz = player.Position.Z - at.Z, reach = car ? Math.Sqrt(CarMetres * CarMetres + CarLaneMetres * CarLaneMetres) : WalkerMetres;
            return dx * dx + dz * dz <= reach * reach;
        }

        /// <summary>The Player.log hold stream line and the ring entry of an applied hold of <paramref name="city"/>. A replayed
        /// hold is also this city's hold, so it enters the ring; its log line keeps the replay prefix.</summary>
        internal static void Publish(PlayerHoldRecord record, CityState city, bool replay = false)
        {
            Ring.Bind(city);
            Ring.Add(record);
            Debug.Log(PlayerHolds.Format(record, replay));
        }

        /// <summary>`pc movement holds since`: the kept holds of <paramref name="city"/> after <paramref name="sequence"/>.
        /// Holds of a city that is no longer loaded are never reported.</summary>
        internal static void Since(CityState city, long sequence, List<PlayerHoldRecord> into, out bool gap)
        {
            Ring.Bind(city);
            Ring.Since(sequence, into, out gap);
        }

        /// <summary>Is the player ahead of the body within the contact distance (walker), or ahead of the car on the same road
        /// segment within 4 m (car)? Horizontal plane; "ahead" is along the body's own leg.</summary>
        internal static bool Ahead(Position at, Position from, Position to, Position player, bool car)
        {
            double dx = to.X - from.X, dz = to.Z - from.Z, length = Math.Sqrt(dx * dx + dz * dz);
            if (!(length > 1e-6)) return false;
            dx /= length; dz /= length;
            double px = player.X - at.X, pz = player.Z - at.Z;
            double along = px * dx + pz * dz, lateral = Math.Abs(px * dz - pz * dx);
            if (!(along > 0)) return false;
            if (!car) return along * along + lateral * lateral <= WalkerMetres * WalkerMetres;
            if (along > CarMetres || lateral > CarLaneMetres) return false;
            double t = ((player.X - from.X) * dx + (player.Z - from.Z) * dz) / length;
            return t >= 0 && t <= 1;
        }
    }
}
