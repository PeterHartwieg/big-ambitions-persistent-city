using System;
using System.Collections.Generic;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal readonly struct PlayerPose
    {
        internal PlayerPose(Position position, bool driving) { Position = position; Driving = driving; Valid = position.IsFinite; }
        internal Position Position { get; }
        internal bool Driving { get; }
        internal bool Valid { get; }
    }

    internal sealed class PlayerContact
    {
        internal const double WalkerMetres = 1;
        internal const double CarMetres = 4;
        internal const double CarLaneMetres = 2.5;
        internal const double ReportMinutes = 0.05;
        private const double StillMinutes = 1e-9;

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

        internal double Observe(string id, Route journey, bool car, double logical, double body, Position at, Position from, Position to,
            in PlayerPose player, double minute, long frame)
        {
            bool contact = player.Valid && player.Driving == car && Ahead(at, from, to, player.Position, car);
            if (!tracks.TryGetValue(id, out var track) || !ReferenceEquals(track.Journey, journey) || track.Car != car || track.Frame != frame - 1)
            {
                if (!contact) { tracks.Remove(id); return 0; }
                if (tracks.Count >= 64) Prune(frame);
                tracks[id] = new Track { Journey = journey, Car = car, Logical = logical, Body = body, Minute = minute, Frame = frame };
                return 0;
            }
            track.Frame = frame;
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

        internal void Settle(string id, PlayerHoldResult result, CityState city)
        {
            if (!tracks.TryGetValue(id, out var track)) return;
            if (result.Refusal == PlayerHoldRefusal.NotDrained) return;
            if (result.Applied) { Applied++; Publish(result.Record, city); } else Refused++;
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

        internal static bool InReach(Position at, in PlayerPose player, bool car)
        {
            if (!player.Valid || player.Driving != car) return false;
            double dx = player.Position.X - at.X, dz = player.Position.Z - at.Z, reach = car ? Math.Sqrt(CarMetres * CarMetres + CarLaneMetres * CarLaneMetres) : WalkerMetres;
            return dx * dx + dz * dz <= reach * reach;
        }

        internal static void Publish(PlayerHoldRecord record, CityState city, bool replay = false)
        {
            Ring.Bind(city);
            Ring.Add(record);
            Debug.Log(PlayerHolds.Format(record, replay));
        }

        internal static void Since(CityState city, long sequence, List<PlayerHoldRecord> into, out bool gap)
        {
            Ring.Bind(city);
            Ring.Since(sequence, into, out gap);
        }

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
